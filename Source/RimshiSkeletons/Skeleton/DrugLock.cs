using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RimshiSkeletons
{
    /// <summary>
    /// Pawns with the mechanical nature gene (every skeleton) take no drugs: beer, smokeleaf, go-juice, penoxycyline,
    /// luciferium and every other mod's drug (anything with a drug category). The rules follow how vanilla keeps drugs from
    /// shamblers and other mutants, plus the routes a mutant never reaches because it has no joy or drug policy: the ingest
    /// menu, drug policies (scheduled and carried), recreation and social relaxing, addiction cravings, drug binges, combat
    /// drugs, administer bills and generation inventory. A drug that still reaches a skeleton (another mod's job) is used up
    /// and does nothing, and the log names it as a failure. Our own future drugs (defName Rimshi_) are exempt, so skeleton
    /// specific ones can be added later. Food, fuel and components are not drugs and are untouched.
    /// </summary>
    public static class DrugLock
    {
        public const string RefuseLabel = "No drugs (skeleton)";
        public const string RefuseTip = "Skeletons cannot take drugs: their frames have no blood or nerves for a drug to act on. Drug policies do nothing for them.";

        private static readonly HashSet<string> loggedRefusals = new HashSet<string>();
        private static readonly HashSet<string> loggedLeaks = new HashSet<string>();

        public static bool Refuses(Pawn pawn) => SkeletonUtility.HasMechanicalNature(pawn);

        public static bool IsBlockedDrug(ThingDef def) =>
            def?.ingestible != null && def.ingestible.drugCategory != DrugCategory.None && !def.defName.StartsWith("Rimshi_", StringComparison.Ordinal);

        public static bool Refuses(Pawn pawn, ThingDef drug) => IsBlockedDrug(drug) && Refuses(pawn);

        /// <summary>Logs a refused route once per session and route, since the AI asks these every few ticks.</summary>
        public static void NoteRefused(string route, Pawn pawn, ThingDef drug)
        {
            if (loggedRefusals.Add(route))
            {
                RimshiLog.Test("drugs", "refused", route + ": " + pawn.LabelShort + " refused " + drug?.defName);
            }
        }

        /// <summary>A drug got past every check and was ingested; its effects are skipped. Logged once per drug.</summary>
        public static void NoteLeak(Pawn pawn, Thing drug)
        {
            if (loggedLeaks.Add(drug.def.defName))
            {
                RimshiLog.Fail("drugs", "ingest blocked", pawn.LabelShort + ": " + drug.def.defName + " reached a skeleton past the drug checks; it did nothing");
            }
        }
    }

    /// <summary>Recreational and hard drugs: vanilla's single "can this pawn take it" check (inventory use, policies, ingest menu).</summary>
    [HarmonyPatch(typeof(PawnUtility), nameof(PawnUtility.CanTakeDrug))]
    public static class Patch_CanTakeDrug
    {
        public static void Postfix(Pawn pawn, ThingDef drug, ref bool __result)
        {
            if (__result && DrugLock.Refuses(pawn, drug))
            {
                __result = false;
            }
        }
    }

    /// <summary>Every drug, medical ones included: the check vanilla uses for mutants. The ingest menu shows it greyed out.</summary>
    [HarmonyPatch(typeof(FoodUtility), nameof(FoodUtility.DrugIsSuitable))]
    public static class Patch_DrugIsSuitable
    {
        public static void Postfix(Pawn p, ThingDef drug, ref bool __result)
        {
            if (__result && DrugLock.Refuses(p, drug))
            {
                __result = false;
                DrugLock.NoteRefused("ingest menu", p, drug);
            }
        }
    }

    /// <summary>Drug policies: nothing is scheduled or carried, whatever policy the skeleton is on.</summary>
    [HarmonyPatch]
    public static class Patch_DrugPolicyAllows
    {
        public static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(Pawn_DrugPolicyTracker), nameof(Pawn_DrugPolicyTracker.AllowedToTakeScheduledEver));
            yield return AccessTools.Method(typeof(Pawn_DrugPolicyTracker), nameof(Pawn_DrugPolicyTracker.AllowedToTakeToInventory));
        }

        public static void Postfix(Pawn_DrugPolicyTracker __instance, ThingDef thingDef, ref bool __result)
        {
            if (__result && DrugLock.Refuses(__instance.pawn, thingDef))
            {
                __result = false;
                DrugLock.NoteRefused("drug policy", __instance.pawn, thingDef);
            }
        }
    }

    /// <summary>Recreation: drugs are never picked to ingest for joy (beer, smokeleaf, the take drug joy giver).</summary>
    [HarmonyPatch(typeof(JoyGiver_Ingest), "CanIngestForJoy")]
    public static class Patch_CanIngestForJoy
    {
        public static bool Prepare() => PatchTarget.Exists(typeof(JoyGiver_Ingest), "CanIngestForJoy");

        public static void Postfix(Pawn pawn, Thing t, ref bool __result)
        {
            if (__result && DrugLock.Refuses(pawn, t?.def))
            {
                __result = false;
                DrugLock.NoteRefused("recreation", pawn, t.def);
            }
        }
    }

    /// <summary>Social relaxing: a skeleton sits with the others but nurses no beer or joint.</summary>
    [HarmonyPatch(typeof(JoyGiver_SocialRelax), "TryFindIngestibleToNurse")]
    public static class Patch_SocialRelaxNurse
    {
        public static bool Prepare() => PatchTarget.Exists(typeof(JoyGiver_SocialRelax), "TryFindIngestibleToNurse");

        public static bool Prefix(Pawn ingester, ref Thing ingestible, ref bool __result)
        {
            if (!DrugLock.Refuses(ingester))
            {
                return true;
            }
            ingestible = null;
            __result = false;
            return false;
        }
    }

    /// <summary>AI jobs that only ever hand out drugs: combat drugs, addiction cravings and drug binges give no job.</summary>
    [HarmonyPatch]
    public static class Patch_DrugJobGivers
    {
        public static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(JobGiver_TakeCombatEnhancingDrug), "TryGiveJob");
            yield return AccessTools.Method(typeof(JobGiver_SatisfyChemicalNeed), "TryGiveJob");
            yield return AccessTools.Method(typeof(JobGiver_TakeDrugsForDrugPolicy), "TryGiveJob");
        }

        public static void Postfix(Pawn pawn, ref Job __result)
        {
            if (__result != null && DrugLock.Refuses(pawn, __result.targetA.Thing?.def))
            {
                DrugLock.NoteRefused("ai job " + __result.def.defName, pawn, __result.targetA.Thing.def);
                __result = null;
            }
        }
    }

    [HarmonyPatch(typeof(JobGiver_BingeDrug), "BestIngestTarget")]
    public static class Patch_BingeDrugTarget
    {
        public static bool Prepare() => PatchTarget.Exists(typeof(JobGiver_BingeDrug), "BestIngestTarget");

        public static void Postfix(Pawn pawn, ref Thing __result)
        {
            if (__result != null && DrugLock.Refuses(pawn, __result.def))
            {
                DrugLock.NoteRefused("drug binge", pawn, __result.def);
                __result = null;
            }
        }
    }

    /// <summary>
    /// Drug binge mental breaks never happen to a skeleton, since it could not take the drug. Patched on the base check and
    /// on every vanilla worker that overrides it, as the binge worker may not call the base.
    /// </summary>
    [HarmonyPatch]
    public static class Patch_BingeCanOccur
    {
        public static IEnumerable<MethodBase> TargetMethods()
        {
            Type baseType = typeof(MentalStateWorker);
            foreach (Type type in baseType.Assembly.GetTypes().Where(t => baseType.IsAssignableFrom(t) && !t.IsAbstract))
            {
                MethodInfo method = type.GetMethod(nameof(MentalStateWorker.StateCanOccur), BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly,
                    null, new[] { typeof(Pawn) }, null);
                if (method != null && !method.IsAbstract)
                {
                    yield return method;
                }
            }
        }

        public static void Postfix(MentalStateWorker __instance, Pawn pawn, ref bool __result)
        {
            Type state = __instance.def?.stateClass;
            if (__result && state != null && typeof(MentalState_BingingDrug).IsAssignableFrom(state) && DrugLock.Refuses(pawn))
            {
                __result = false;
                DrugLock.NoteRefused("binge break", pawn, null);
            }
        }
    }

    /// <summary>Administer drug bills (any drug, medical included): not offered on skeletons, and queued ones stop.</summary>
    [HarmonyPatch(typeof(Recipe_AdministerIngestible), nameof(Recipe_AdministerIngestible.AvailableOnNow))]
    public static class Patch_AdministerAvailable
    {
        public static void Postfix(Recipe_AdministerIngestible __instance, Thing thing, ref bool __result)
        {
            if (__result && thing is Pawn pawn && DrugLock.Refuses(pawn) && __instance.recipe.ingredients.Count > 0
                && __instance.recipe.ingredients[0].filter.AllowedThingDefs.Any(DrugLock.IsBlockedDrug))
            {
                __result = false;
            }
        }
    }

    /// <summary>Generation: skeletons get no combat drugs or addiction drugs in their inventory (other mods' kinds included).</summary>
    [HarmonyPatch]
    public static class Patch_GenerateDrugInventory
    {
        private static readonly string[] Names = { "GiveDrugsIfAddicted", "GiveCombatEnhancingDrugs" };

        // PatchAll stops at a patch class that targets nothing, so this one skips itself if both methods are gone.
        public static bool Prepare() => Names.Count(name => PatchTarget.Exists(typeof(PawnInventoryGenerator), name)) > 0;

        public static IEnumerable<MethodBase> TargetMethods() =>
            Names.Select(name => AccessTools.Method(typeof(PawnInventoryGenerator), name)).Where(m => m != null).Cast<MethodBase>();

        public static bool Prefix(MethodBase __originalMethod, Pawn __0)
        {
            if (!DrugLock.Refuses(__0))
            {
                return true;
            }
            DrugLock.NoteRefused("generation " + __originalMethod.Name, __0, null);
            return false;
        }
    }

    /// <summary>
    /// Last guard, where a drug acts: a drug ingested by a skeleton anyway (another mod's job or ritual) gives no high,
    /// addiction, tolerance or overdose. It is still used up, so no job loops on it.
    /// </summary>
    [HarmonyPatch]
    public static class Patch_DrugEffects
    {
        public static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(CompDrug), nameof(CompDrug.PrePostIngested));
            yield return AccessTools.Method(typeof(CompDrug), nameof(CompDrug.PostIngested));
        }

        public static bool Prefix(CompDrug __instance, Pawn ingester)
        {
            if (!DrugLock.Refuses(ingester, __instance.parent.def))
            {
                return true;
            }
            DrugLock.NoteLeak(ingester, __instance.parent);
            return false;
        }
    }

    [HarmonyPatch(typeof(IngestionOutcomeDoer), nameof(IngestionOutcomeDoer.DoIngestionOutcome))]
    public static class Patch_DrugOutcome
    {
        public static bool Prefix(Pawn pawn, Thing ingested)
        {
            if (!DrugLock.Refuses(pawn, ingested?.def))
            {
                return true;
            }
            DrugLock.NoteLeak(pawn, ingested);
            return false;
        }
    }

    /// <summary>Assign tab and anywhere else the drug policy dropdown is drawn: a skeleton shows a greyed note instead.</summary>
    [HarmonyPatch(typeof(DrugPolicyUIUtility), nameof(DrugPolicyUIUtility.DoAssignDrugPolicyButtons))]
    public static class Patch_DrugPolicyButton
    {
        public static bool Prefix(Rect rect, Pawn pawn)
        {
            if (!DrugLock.Refuses(pawn))
            {
                return true;
            }
            TextAnchor anchor = Text.Anchor;
            Color color = GUI.color;
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = Color.gray;
            Widgets.Label(rect.ContractedBy(4f, 0f), DrugLock.RefuseLabel);
            GUI.color = color;
            Text.Anchor = anchor;
            TooltipHandler.TipRegion(rect, DrugLock.RefuseTip);
            return false;
        }
    }
}
