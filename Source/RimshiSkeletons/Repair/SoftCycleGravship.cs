using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;

namespace RimshiSkeletons
{
    /// <summary>
    /// Soft cycles ride through a grav ship launch and landing (Matthew, 2026-11-10), the way vanilla keeps a sanguophage
    /// deathresting. Launching despawns everything aboard (GravshipUtility.GenerateGravship: PreSwapMap sets
    /// Thing.BeingTransportedOnGravship, then DeSpawn), and Pawn.DeSpawn stops every job. Vanilla deathrest survives that
    /// because it lives in its hediff, not its job: the deathrest job's fail conditions skip while the pawn is being
    /// transported, and on landing GravshipPlacementUtility.SpawnPawns tucks a deathresting pawn back into the bed it
    /// lands on. Our repair bed cycle is vanilla deathrest underneath, so it gets that for free; the standing reset lives
    /// in its job, whose end cancelled it ("interrupted"). Here:
    /// - the standing reset's job ending while the skeleton is aboard a launching ship holds the reset instead of
    ///   cancelling it (Hediff_Rimshi_SoftReset.aboardGravship); once the skeleton is spawned again it takes the reset's
    ///   job back where it landed (in the rib cage it hangs in, if any), or carries on lying if it landed downed;
    /// - a repair bed that reads unpowered just after a landing (power nets rebuild a tick later) holds the cycle for a
    ///   short grace instead of cancelling it.
    /// The ship's flight does not tick the pawns aboard, so a cycle neither progresses nor runs down in the air.
    /// </summary>
    public static class SoftCycleGravship
    {
        /// <summary>How long after a landing an unpowered repair bed holds its cycle instead of cancelling it.</summary>
        public const int LandingGraceTicks = 600;

        private static int lastLandingTick = -1;

        public static bool Aboard(Thing thing) => thing != null && thing.BeingTransportedOnGravship;

        public static bool WithinGrace(int landedTick, int now) => landedTick >= 0 && now >= landedTick && now - landedTick <= LandingGraceTicks;

        public static bool JustLanded => Find.TickManager != null && WithinGrace(lastLandingTick, Find.TickManager.TicksGame);

        /// <summary>An unpowered repair bed holds the cycle (rather than cancelling it) while it or its skeleton is on a moving ship, or just landed.</summary>
        public static bool HoldsUnpoweredBed(Pawn pawn, ThingWithComps bed) => Aboard(pawn) || Aboard(bed) || JustLanded;

        public static void NoteLanding() => lastLandingTick = Find.TickManager?.TicksGame ?? -1;

        /// <summary>Kind of cycle the skeleton is in, for the log; null when it is not cycling.</summary>
        public static string CycleKind(Pawn pawn)
        {
            if (pawn == null || !SkeletonUtility.HasFrame(pawn))
            {
                return null;
            }
            Hediff_Rimshi_SoftReset reset = StandingReset.Of(pawn);
            if (reset != null && !reset.done)
            {
                return reset.lying ? "lying soft reset" : "standing soft reset";
            }
            return pawn.Deathresting ? "repair bed soft cycle" : null;
        }

        /// <summary>
        /// The standing reset's job ended while the skeleton is being carried off on a grav ship: the reset is held, and
        /// resumes once the skeleton is spawned again. Returns false (the caller cancels as usual) when it is not aboard.
        /// </summary>
        public static bool HoldForFlight(Pawn pawn)
        {
            Hediff_Rimshi_SoftReset reset = StandingReset.Of(pawn);
            if (reset == null || reset.done || !Aboard(pawn))
            {
                return false;
            }
            if (!reset.aboardGravship)
            {
                reset.aboardGravship = true;
                RimshiLog.Test("softcycle", "gravship held", pawn.LabelShort + "'s soft reset is held aboard a launching grav ship at " + reset.Fraction.ToStringPercent("F0"));
            }
            return true;
        }

        /// <summary>Back on the ground after a flight: take the reset's job back where the skeleton landed.</summary>
        public static void Resume(Pawn pawn, Hediff_Rimshi_SoftReset reset)
        {
            reset.aboardGravship = false;
            if (reset.lying)
            {
                RimshiLog.Test("softcycle", "gravship resumed", pawn.LabelShort + " landed and carries on its soft reset lying at " + pawn.Position + " (" + reset.Fraction.ToStringPercent("F0") + ")");
                return;
            }
            if (pawn.Downed)
            {
                // Landed unable to stand: carry on lying, and rise into the standing reset once it can (as a lying reset does).
                reset.lying = true;
                RimshiLog.Test("softcycle", "gravship resumed", pawn.LabelShort + " landed downed and carries on its soft reset lying at " + pawn.Position + " (" + reset.Fraction.ToStringPercent("F0") + ")");
                return;
            }
            Building_Bed cage = RibCage.CageAt(pawn.Position, pawn.Map);
            Job job = cage != null ? JobMaker.MakeJob(RimshiDefOf.Rimshi_SoftReset, cage) : JobMaker.MakeJob(RimshiDefOf.Rimshi_SoftReset, pawn.Position);
            pawn.jobs.StartJob(job, JobCondition.InterruptForced);
            RimshiLog.Test("softcycle", "gravship resumed", pawn.LabelShort + " landed and carries on its soft reset " + (cage != null ? "in " + cage.LabelShort : "standing")
                + " at " + pawn.Position + " (" + reset.Fraction.ToStringPercent("F0") + ")");
        }

        public static void LogAboard(IEnumerable<Pawn> pawns, string when)
        {
            if (pawns == null)
            {
                return;
            }
            foreach (Pawn pawn in pawns)
            {
                string kind = CycleKind(pawn);
                if (kind == null)
                {
                    continue;
                }
                string where = "";
                if (when == "landed")
                {
                    Building_Bed bed = pawn.Spawned ? pawn.CurrentBed() ?? pawn.Position.GetEdifice(pawn.Map) as Building_Bed : null;
                    where = bed != null ? ", in " + bed.LabelShort : ", at " + pawn.Position;
                }
                string detail = pawn.LabelShort + " " + when + " mid " + kind + where;
                if (when == "landed")
                {
                    RimshiLog.Test("softcycle", "gravship landed", detail);
                }
                else
                {
                    RimshiLog.Test("softcycle", "gravship launched", detail);
                }
            }
        }
    }

    /// <summary>Logs the skeletons that leave mid-cycle when a grav ship launches.</summary>
    [HarmonyPatch(typeof(GravshipUtility), nameof(GravshipUtility.GenerateGravship))]
    public static class Patch_SoftCycleGravshipLaunch
    {
        public static void Postfix(Gravship __result)
        {
            SoftCycleGravship.LogAboard(__result?.Pawns, "launched");
        }
    }

    /// <summary>Starts the unpowered-bed grace and logs the skeletons that come down mid-cycle when a grav ship lands.</summary>
    [HarmonyPatch(typeof(GravshipPlacementUtility), nameof(GravshipPlacementUtility.PlaceGravshipInMap))]
    public static class Patch_SoftCycleGravshipLanding
    {
        public static void Postfix(Gravship gravship)
        {
            SoftCycleGravship.NoteLanding();
            SoftCycleGravship.LogAboard(gravship?.Pawns, "landed");
        }
    }
}
