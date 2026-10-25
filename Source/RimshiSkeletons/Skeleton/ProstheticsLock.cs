using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace RimshiSkeletons
{
    /// <summary>
    /// Skeletons take no prosthetics, bionics, implants or organs for now: flesh-and-blood parts make no sense in a frame,
    /// and skeleton parts of our own (behind a skeleton workbench and research) come later. Generation gives skeletons no
    /// tech hediffs at all, whatever pawn kind or faction made them (other mods' kinds included), and install surgeries are
    /// not offered on skeletons. Removing a part a skeleton already has (an old save, a converted pawn) still works.
    /// Psylinks and mechlinks are not body parts and are left alone. Our own future part recipes (defName Rimshi_) are
    /// exempt. Only skeletons are affected; every other pawn gets the vanilla answer.
    /// </summary>
    public static class ProstheticsLock
    {
        private static readonly Dictionary<RecipeDef, bool> cache = new Dictionary<RecipeDef, bool>();
        private static bool loggedSpawn;

        public static bool Refuses(Pawn pawn) => SkeletonUtility.CarriesSkeletonGenes(pawn);

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
}
