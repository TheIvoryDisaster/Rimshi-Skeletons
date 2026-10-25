using HarmonyLib;
using RimWorld;
using Verse;

namespace RimshiSkeletons
{
    /// <summary>
    /// Skeletons can never reproduce. Fertility 0, Asexual and having no gender already stop Biotech pregnancy; these
    /// patches close the remaining routes (another mod's pairing, embryo implantation, a pregnancy added directly) for
    /// any pawn with the null chromosome gene.
    /// </summary>
    [HarmonyPatch(typeof(PregnancyUtility), nameof(PregnancyUtility.CanEverProduceChild))]
    public static class Patch_CanEverProduceChild
    {
        public static void Postfix(Pawn first, Pawn second, ref AcceptanceReport __result)
        {
            Pawn skeleton = SkeletonUtility.HasNullChromosome(first) ? first : SkeletonUtility.HasNullChromosome(second) ? second : null;
            if (skeleton != null)
            {
                __result = skeleton.LabelShort + " is a skeleton and cannot have children.";
            }
        }
    }

    [HarmonyPatch(typeof(PregnancyUtility), nameof(PregnancyUtility.PregnancyChanceForPawn))]
    public static class Patch_PregnancyChanceForPawn
    {
        public static void Postfix(Pawn pawn, ref float __result)
        {
            if (SkeletonUtility.HasNullChromosome(pawn))
            {
                __result = 0f;
            }
        }
    }

    [HarmonyPatch(typeof(PregnancyUtility), nameof(PregnancyUtility.PregnancyChanceImplantEmbryo))]
    public static class Patch_PregnancyChanceImplantEmbryo
    {
        public static void Postfix(Pawn surrogate, ref float __result)
        {
            if (SkeletonUtility.HasNullChromosome(surrogate))
            {
                __result = 0f;
            }
        }
    }

    /// <summary>A pregnancy added to a skeleton any other way (dev mode, another mod) is refused.</summary>
    [HarmonyPatch(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.AddHediff),
        new[] { typeof(Hediff), typeof(BodyPartRecord), typeof(DamageInfo?), typeof(DamageWorker.DamageResult) })]
    public static class Patch_NoSkeletonPregnancy
    {
        public static bool Prefix(Pawn ___pawn, Hediff hediff)
        {
            if (!(hediff is Hediff_Pregnant) || !SkeletonUtility.HasNullChromosome(___pawn))
            {
                return true;
            }
            RimshiLog.Test("reproduction", "pregnancy blocked", ___pawn.LabelShort + ": " + hediff.def.defName + " refused");
            return false;
        }
    }
}
