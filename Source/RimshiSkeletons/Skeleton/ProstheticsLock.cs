using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace RimshiSkeletons
{
    /// <summary>
    /// Pawns with the mechanical nature gene (every skeleton) take no prosthetics, bionics, implants, organs or psylinks
    /// for now: flesh-and-blood parts make no sense in a frame, and skeleton parts of our own (behind a skeleton workbench
    /// and research) come later. Generation gives them no tech hediffs at all, whatever pawn kind or faction made them
    /// (other mods' kinds included), and install surgeries are not offered on them. Removing a part one already has (an
    /// old save, a converted pawn) still works. Psylinks are refused on every route (neuroformer, royal titles at
    /// generation, bestowing, anima tree); mechlinks are the one self-installed implant still allowed. Our own future part
    /// recipes (defName Rimshi_) are exempt. Pawns without the gene get the vanilla answer.
    /// </summary>
    public static class ProstheticsLock
    {
        private static readonly Dictionary<RecipeDef, bool> cache = new Dictionary<RecipeDef, bool>();
        private static bool loggedSpawn;

        public const string RefusePsylink = "Skeletons cannot take a psylink: their frame has no psychic sense to amplify.";
        public const string RefuseImplant = "Skeletons cannot take implants yet; only a mechlink fits their frame.";

        public static bool Refuses(Pawn pawn) => SkeletonUtility.HasMechanicalNature(pawn);

        /// <summary>Self-installed implants (CompUseEffect_InstallImplant) a skeleton may still use: only the mechlink.</summary>
        public static bool AllowsSelfImplant(HediffDef hediff) => hediff != null && hediff.defName == "MechlinkImplant";

        /// <summary>
        /// True for surgeries that put a part or implant into a body: vanilla's install workers (artificial part, implant,
        /// natural organ) and their subclasses, plus any other mod's recipe whose added hediff counts as a part or implant.
        /// </summary>
        public static bool IsInstallRecipe(RecipeDef recipe)
        {
            if (recipe == null)
            {
                return false;
            }
            if (!cache.TryGetValue(recipe, out bool install))
            {
                install = Classify(recipe);
                cache[recipe] = install;
            }
            return install;
        }

        private static bool Classify(RecipeDef recipe)
        {
            if (recipe.defName.StartsWith("Rimshi_", StringComparison.Ordinal))
            {
                return false;
            }
            Type worker = recipe.workerClass;
            if (worker != null && (typeof(Recipe_InstallArtificialBodyPart).IsAssignableFrom(worker)
                || typeof(Recipe_InstallImplant).IsAssignableFrom(worker) || typeof(Recipe_InstallNaturalBodyPart).IsAssignableFrom(worker)))
            {
                return true;
            }
            HediffDef added = recipe.addsHediff;
            return added != null && (added.countsAsAddedPartOrImplant || added.addedPartProps != null
                || (added.hediffClass != null && (typeof(Hediff_AddedPart).IsAssignableFrom(added.hediffClass)
                    || typeof(Hediff_Implant).IsAssignableFrom(added.hediffClass))));
        }

        public static void NoteSpawnSkipped(Pawn pawn)
        {
            // Once per session: world generation makes hundreds of pawns.
            if (!loggedSpawn)
            {
                loggedSpawn = true;
                RimshiLog.Test("prosthetics", "spawn skipped", pawn.LabelShort + " (" + pawn.kindDef?.defName + "): no tech hediffs generated for a skeleton");
            }
        }
    }

    /// <summary>
    /// Pawn generation: skeletons skip tech hediffs entirely (random and required). Genes are added before hediffs, so the
    /// skeleton check already sees the xenotype here.
    /// </summary>
    [HarmonyPatch(typeof(PawnTechHediffsGenerator), nameof(PawnTechHediffsGenerator.GenerateTechHediffsFor))]
    public static class Patch_GenerateTechHediffs
    {
        public static bool Prefix(Pawn pawn)
        {
            if (!ProstheticsLock.Refuses(pawn))
            {
                return true;
            }
            ProstheticsLock.NoteSpawnSkipped(pawn);
            return false;
        }
    }

    /// <summary>Install surgeries: not offered on skeletons (health tab, and bills already queued).</summary>
    [HarmonyPatch(typeof(Recipe_Surgery), nameof(Recipe_Surgery.AvailableOnNow))]
    public static class Patch_InstallSurgeryAvailable
    {
        public static void Postfix(Recipe_Surgery __instance, Thing thing, ref bool __result)
        {
            if (__result && thing is Pawn pawn && ProstheticsLock.IsInstallRecipe(__instance.recipe) && ProstheticsLock.Refuses(pawn))
            {
                __result = false;
            }
        }
    }

    /// <summary>
    /// Last guard, where the part is actually put in: an install reaching a skeleton anyway (another mod's menu or worker
    /// that skips AvailableOnNow) does nothing, and the log names the recipe as a failure.
    /// </summary>
    [HarmonyPatch]
    public static class Patch_InstallSurgeryApply
    {
        public static IEnumerable<System.Reflection.MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(Recipe_InstallArtificialBodyPart), nameof(Recipe_InstallArtificialBodyPart.ApplyOnPawn));
            yield return AccessTools.Method(typeof(Recipe_InstallImplant), nameof(Recipe_InstallImplant.ApplyOnPawn));
            yield return AccessTools.Method(typeof(Recipe_InstallNaturalBodyPart), nameof(Recipe_InstallNaturalBodyPart.ApplyOnPawn));
        }

        public static bool Prefix(RecipeWorker __instance, Pawn pawn)
        {
            if (!ProstheticsLock.IsInstallRecipe(__instance.recipe) || !ProstheticsLock.Refuses(pawn))
            {
                return true;
            }
            RimshiLog.Fail("prosthetics", "install blocked", pawn.LabelShort + ": " + __instance.recipe.defName + " reached a skeleton past the surgery checks and was not done");
            return false;
        }
    }

    /// <summary>Self-installed implants (neuroformer and other mods' items): refused on skeletons, except the mechlink.</summary>
    [HarmonyPatch(typeof(CompUseEffect_InstallImplant), nameof(CompUseEffect_InstallImplant.CanBeUsedBy))]
    public static class Patch_SelfImplantUsable
    {
        public static void Postfix(CompUseEffect_InstallImplant __instance, Pawn p, ref AcceptanceReport __result)
        {
            if (__result.Accepted && !ProstheticsLock.AllowsSelfImplant(__instance.Props.hediffDef) && ProstheticsLock.Refuses(p))
            {
                __result = __instance.Props.hediffDef == HediffDefOf.PsychicAmplifier ? ProstheticsLock.RefusePsylink : ProstheticsLock.RefuseImplant;
                RimshiLog.Test("prosthetics", "self implant refused", p.LabelShort + ": " + __instance.parent.def.defName + " refused");
            }
        }
    }

    /// <summary>Anima tree linking: skeletons are never offered the ritual.</summary>
    [HarmonyPatch(typeof(CompPsylinkable), nameof(CompPsylinkable.CanPsylink))]
    public static class Patch_AnimaPsylink
    {
        public static void Postfix(Pawn pawn, ref AcceptanceReport __result)
        {
            if (__result.Accepted && ProstheticsLock.Refuses(pawn))
            {
                __result = ProstheticsLock.RefusePsylink;
            }
        }
    }

    /// <summary>
    /// Last guard for psylinks, where the hediff is added: royal titles at generation, bestowing ceremonies, the anima tree,
    /// dev mode or another mod all end here. The pregnancy guard (ReproductionPatches) patches the same method.
    /// </summary>
    [HarmonyPatch(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.AddHediff),
        new[] { typeof(Hediff), typeof(BodyPartRecord), typeof(DamageInfo?), typeof(DamageWorker.DamageResult) })]
    public static class Patch_NoSkeletonPsylink
    {
        public static bool Prefix(Pawn ___pawn, Hediff hediff)
        {
            if (!(hediff is Hediff_Psylink) || !ProstheticsLock.Refuses(___pawn))
            {
                return true;
            }
            RimshiLog.Test("prosthetics", "psylink blocked", ___pawn.LabelShort + ": " + hediff.def.defName + " refused");
            return false;
        }
    }

    /// <summary>
    /// A refused psylink must not hand out psycasts either: generation sets the level of the psylink after adding it, and
    /// leveling grants a psycast per level. Skeletons never level a psylink (one carried over from an old save stays as is).
    /// </summary>
    [HarmonyPatch(typeof(Hediff_Psylink), nameof(Hediff_Psylink.ChangeLevel), new[] { typeof(int), typeof(bool) })]
    public static class Patch_NoSkeletonPsylinkLevel
    {
        public static bool Prefix(Hediff_Psylink __instance) => !ProstheticsLock.Refuses(__instance.pawn);
    }
}
