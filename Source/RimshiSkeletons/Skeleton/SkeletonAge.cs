using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace RimshiSkeletons
{
    /// <summary>
    /// Skeletons are ancient: a newly generated skeleton gets a biological age of at least 120 and a chronological age
    /// of 400 to 1200 (Matthew, 2026-11-01). Human old age must not touch them at that age, so the birthday conditions
    /// (frail, bad back, cataract, dementia and the rest) are never given to a skeleton.
    /// </summary>
    public static class SkeletonAge
    {
        public const float MinBiologicalYears = 120f;
        public const float MaxBiologicalYears = 180f;
        public const float MinChronologicalYears = 400f;
        public const float MaxChronologicalYears = 1200f;

        /// <summary>True when the pawn's ages are already in the skeleton ranges.</summary>
        public static bool IsAncient(Pawn pawn)
        {
            if (pawn?.ageTracker == null)
            {
                return false;
            }
            float bio = pawn.ageTracker.AgeBiologicalYearsFloat;
            float chrono = pawn.ageTracker.AgeChronologicalYearsFloat;
            return bio >= MinBiologicalYears && chrono >= MinChronologicalYears && chrono <= MaxChronologicalYears + 1f && chrono >= bio;
        }

        /// <summary>Gives a new skeleton its ancient ages. Generation hediffs were rolled at the earlier age, so none come from old age.</summary>
        public static void MakeAncient(Pawn pawn, string why)
        {
            if (pawn?.ageTracker == null)
            {
                return;
            }
            int bioBefore = pawn.ageTracker.AgeBiologicalYears;
            int chronoBefore = pawn.ageTracker.AgeChronologicalYears;
            long bio = (long)(Rand.Range(MinBiologicalYears, MaxBiologicalYears) * GenDate.TicksPerYear);
            long chrono = (long)(Rand.Range(MinChronologicalYears, MaxChronologicalYears) * GenDate.TicksPerYear);
            pawn.ageTracker.AgeBiologicalTicks = bio;
            pawn.ageTracker.AgeChronologicalTicks = System.Math.Max(chrono, bio);
            // Ideology's age reversal demand counts from the age it was set at; restart it from the new age.
            pawn.ageTracker.ResetAgeReversalDemand(Pawn_AgeTracker.AgeReversalReason.Initial);
            RimshiLog.Test("age", "ancient", pawn.LabelShort + " (" + why + "): biological " + bioBefore + " to " + pawn.ageTracker.AgeBiologicalYears
                + ", chronological " + chronoBefore + " to " + pawn.ageTracker.AgeChronologicalYears);
        }

        /// <summary>Raises a pawn's biological age to the skeleton minimum, keeping its chronological age (the save converter).</summary>
        public static void RaiseBiologicalAge(Pawn pawn)
        {
            long min = (long)(MinBiologicalYears * GenDate.TicksPerYear);
            if (pawn?.ageTracker == null || pawn.ageTracker.AgeBiologicalTicks >= min)
            {
                return;
            }
            pawn.ageTracker.AgeBiologicalTicks = min;
            if (pawn.ageTracker.AgeChronologicalTicks < min)
            {
                pawn.ageTracker.AgeChronologicalTicks = min;
            }
            pawn.ageTracker.ResetAgeReversalDemand(Pawn_AgeTracker.AgeReversalReason.Initial);
        }
    }

    /// <summary>
    /// Every newly generated skeleton is made ancient, whatever its pawn kind, faction or xenotype (the mechanical nature
    /// gene marks all skeletons). Pawns that came back from the world pawns keep their age. Runs before
    /// Patch_ClearNewRelations, which empties the new pawn set.
    /// </summary>
    [HarmonyPatch(typeof(PawnGenerator), nameof(PawnGenerator.GeneratePawn), new[] { typeof(PawnGenerationRequest) })]
    public static class Patch_AncientSkeletons
    {
        [HarmonyPriority(Priority.High)]
        public static void Postfix(Pawn __result)
        {
            if (__result == null || !SkeletonUtility.HasMechanicalNature(__result))
            {
                return;
            }
            // Without the new pawn note (a game version without TryGenerateNewPawnInternal), age any skeleton not yet ancient.
            bool isNew = PatchTarget.Exists(typeof(PawnGenerator), "TryGenerateNewPawnInternal")
                ? Patch_NoteNewPawn.NewPawns.Contains(__result.thingIDNumber)
                : !SkeletonAge.IsAncient(__result);
            if (isNew)
            {
                SkeletonAge.MakeAncient(__result, "generated");
            }
        }
    }

    /// <summary>
    /// No human old age for skeletons: the birthday conditions are rolled here both while a pawn is generated and on each
    /// biological birthday (a custom xenotype without ageless still has birthdays).
    /// </summary>
    [HarmonyPatch(typeof(AgeInjuryUtility), nameof(AgeInjuryUtility.RandomHediffsToGainOnBirthday), new[] { typeof(Pawn), typeof(float) })]
    public static class Patch_NoSkeletonOldAge
    {
        private static readonly HashSet<int> logged = new HashSet<int>();

        public static void Postfix(Pawn pawn, ref IEnumerable<HediffGiver_Birthday> __result)
        {
            if (!SkeletonUtility.HasMechanicalNature(pawn))
            {
                return;
            }
            __result = Enumerable.Empty<HediffGiver_Birthday>();
            if (logged.Add(pawn.thingIDNumber))
            {
                RimshiLog.Test("age", "no old age", pawn.LabelShort + ": birthday conditions skipped at biological age " + pawn.ageTracker.AgeBiologicalYears);
            }
        }
    }
}
