using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace RimshiSkeletons
{
    /// <summary>
    /// Skeletons are powered down, not anesthetized (docs/design/power-down.md). Their operations list offers
    /// "temporarily power down" (Rimshi_PowerDown) in place of vanilla's Anesthetize, and any anaesthetic a skeleton would
    /// get (that operation, the anaesthetic before other surgeries, quests) is maintenance mode instead.
    /// </summary>
    public static class SkeletonPowerDown
    {
        private static RecipeDef anesthetize;

        public static RecipeDef Anesthetize => anesthetize ?? (anesthetize = DefDatabase<RecipeDef>.GetNamedSilentFail("Anesthetize"));

        private static bool loggedSwap;

        public static bool PowersDown(Pawn pawn) => SkeletonUtility.HasMechanicalNature(pawn);

        public static void NoteSwap(Pawn pawn)
        {
            if (!loggedSwap)
            {
                loggedSwap = true;
                RimshiLog.Test("powerdown", "anesthetic swapped", pawn.LabelShort + " got maintenance mode instead of anesthetic");
            }
        }
    }

    /// <summary>"Temporarily power down": skeletons only. The bill puts the unit in maintenance mode as it starts.</summary>
    public class Recipe_Rimshi_PowerDown : Recipe_Surgery
    {
        public override bool AvailableOnNow(Thing thing, BodyPartRecord part = null)
        {
            return thing is Pawn pawn && SkeletonPowerDown.PowersDown(pawn) && base.AvailableOnNow(thing, part);
        }

        public override void ApplyOnPawn(Pawn pawn, BodyPartRecord part, Pawn billDoer, List<Thing> ingredients, Bill bill)
        {
            base.ApplyOnPawn(pawn, part, billDoer, ingredients, bill);
            bool down = pawn.health.hediffSet.HasHediff(RimshiDefOf.Rimshi_MaintenanceMode);
            RimshiLog.Test("powerdown", "powered down", pawn.LabelShort + (down ? " is in maintenance mode" : " has no maintenance mode hediff"));
        }
    }

    /// <summary>Vanilla's Anesthetize is not offered on skeletons; they get Rimshi_PowerDown instead.</summary>
    [HarmonyPatch(typeof(Recipe_Surgery), nameof(Recipe_Surgery.AvailableOnNow))]
    public static class Patch_NoAnesthetizeSkeletons
    {
        public static void Postfix(Recipe_Surgery __instance, Thing thing, ref bool __result)
        {
            if (__result && __instance.recipe != null && __instance.recipe == SkeletonPowerDown.Anesthetize && thing is Pawn pawn && SkeletonPowerDown.PowersDown(pawn))
            {
                __result = false;
            }
        }
    }

    /// <summary>Anaesthetic added by def (vanilla's TryAnesthetize, surgical inspection): a skeleton gets maintenance mode.</summary>
    [HarmonyPatch(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.AddHediff),
        new[] { typeof(HediffDef), typeof(BodyPartRecord), typeof(DamageInfo?), typeof(DamageWorker.DamageResult) })]
    public static class Patch_MaintenanceModeByDef
    {
        public static void Prefix(Pawn ___pawn, ref HediffDef def)
        {
            if (def != null && def == HediffDefOf.Anesthetic && SkeletonPowerDown.PowersDown(___pawn))
            {
                def = RimshiDefOf.Rimshi_MaintenanceMode;
                SkeletonPowerDown.NoteSwap(___pawn);
            }
        }
    }

    /// <summary>Anaesthetic made first and added after (quests, other mods): swapped the same way.</summary>
    [HarmonyPatch(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.AddHediff),
        new[] { typeof(Hediff), typeof(BodyPartRecord), typeof(DamageInfo?), typeof(DamageWorker.DamageResult) })]
    public static class Patch_MaintenanceModeByHediff
    {
        public static void Prefix(Pawn ___pawn, ref Hediff hediff, BodyPartRecord part)
        {
            if (hediff?.def == null || hediff.def != HediffDefOf.Anesthetic || !SkeletonPowerDown.PowersDown(___pawn))
            {
                return;
            }
            hediff = HediffMaker.MakeHediff(RimshiDefOf.Rimshi_MaintenanceMode, ___pawn, part);
            SkeletonPowerDown.NoteSwap(___pawn);
        }
    }
}
