using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RimshiSkeletons
{
    /// <summary>
    /// The soft reset without a repair bed (docs/design/cpu-soft-cycle.md): ordered from the skeleton's "Begin soft reset"
    /// button onto a cell, where it stands still for 5 days, or onto a free rib cage, where it hangs. Integrity comes
    /// back slowly for three days and quickly on the last (SoftCycle.StandingCurve). The job ending early for any reason
    /// (stopped, drafted, downed, a mental break) cancels it with 0% integrity and an overload.
    /// </summary>
    public static class StandingReset
    {
        public static Hediff_Rimshi_SoftReset Of(Pawn pawn) =>
            pawn?.health?.hediffSet?.GetFirstHediffOfDef(RimshiDefOf.Rimshi_SoftResetting) as Hediff_Rimshi_SoftReset;

        public static bool InProgress(Pawn pawn) => Of(pawn) is Hediff_Rimshi_SoftReset reset && !reset.done;

        public static float FractionDone(Pawn pawn) => Of(pawn)?.Fraction ?? 0f;

        public static bool ValidTarget(Pawn pawn, LocalTargetInfo target, out string reason)
        {
            reason = null;
            Map map = pawn.Map;
            if (map == null || !target.Cell.InBounds(map) || target.Cell.Fogged(map))
            {
                reason = "out of bounds";
                return false;
            }
            Building_Bed cage = RibCage.CageAt(target.Cell, map);
            if (cage != null)
            {
                if (!RibCage.CanHangIn(pawn, cage, out reason))
                {
                    return false;
                }
                return true;
            }
            if (!target.Cell.Standable(map) || target.Cell.IsForbidden(pawn))
            {
                reason = "not standable";
                return false;
            }
            if (!pawn.CanReach(target.Cell, PathEndMode.OnCell, Danger.Deadly) || !pawn.CanReserve(target.Cell))
            {
                reason = "unreachable or reserved";
                return false;
            }
            return true;
        }

        public static Command BeginGizmo(Pawn pawn, Texture2D icon)
        {
            var command = new Command_Target
            {
                defaultLabel = "Begin soft reset",
                defaultDesc = "Without a repair bed: pick a spot for " + pawn.LabelShort + " to stand still on (or a free rib cage to hang in) for 5 days while the CPU resets. "
                    + "Integrity comes back slowly for three days and quickly on the last. Finishing restores CPU integrity to 100% and clears any overload, but flushes short-term "
                    + "memories, wears away a little of two skills and leaves the skeleton feeling its memories are not its own. Interrupting it in any way cancels it with 0% integrity and an overload.",
                icon = icon,
                targetingParams = new TargetingParameters
                {
                    canTargetLocations = true,
                    canTargetBuildings = true,
                    canTargetPawns = false,
                    validator = t => ValidTarget(pawn, t.Cell, out _)
                },
                action = target => Order(pawn, target)
            };
            if (pawn.Downed || pawn.InMentalState)
            {
                command.Disable(pawn.LabelShort + " cannot start a soft reset now.");
            }
            return command;
        }

        public static void Order(Pawn pawn, LocalTargetInfo target)
        {
            if (!ValidTarget(pawn, target, out string reason))
            {
                Messages.Message("Cannot soft reset there: " + reason + ".", MessageTypeDefOf.RejectInput, false);
                return;
            }
            Building_Bed cage = RibCage.CageAt(target.Cell, pawn.Map);
            Job job = cage != null ? JobMaker.MakeJob(RimshiDefOf.Rimshi_SoftReset, cage) : JobMaker.MakeJob(RimshiDefOf.Rimshi_SoftReset, target.Cell);
            pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
        }

        /// <summary>Called from the job once the skeleton is in place.</summary>
        public static void Begin(Pawn pawn)
        {
            if (InProgress(pawn))
            {
                return;
            }
            pawn.health.AddHediff(RimshiDefOf.Rimshi_SoftResetting);
            RimshiLog.Test("softcycle", "start standing", pawn.LabelShort + " began a soft reset " + (RibCage.Suspended(pawn) ? "hanging in a rib cage" : "standing at " + pawn.Position)
                + ", integrity " + SoftCycle.Integrity(pawn)?.CurLevelPercentage.ToStringPercent());
        }

        public static void Finish(Pawn pawn)
        {
            Hediff_Rimshi_SoftReset reset = Of(pawn);
            if (reset == null || reset.done)
            {
                return;
            }
            // done hides the reset at once; the health tracker removes the hediff (ShouldRemove), which is safe from its own tick.
            reset.done = true;
            SoftCycle.Complete(pawn, SoftCycleKind.Standing);
            if (pawn.CurJobDef == RimshiDefOf.Rimshi_SoftReset)
            {
                pawn.jobs.EndCurrentJob(JobCondition.Succeeded);
            }
        }

        public static void Cancel(Pawn pawn, string reason)
        {
            Hediff_Rimshi_SoftReset reset = Of(pawn);
            if (reset == null || reset.done)
            {
                return;
            }
            reset.done = true;
            SoftCycle.Cancel(pawn, SoftCycleKind.Standing, reason);
            if (pawn.CurJobDef == RimshiDefOf.Rimshi_SoftReset)
            {
                pawn.jobs.EndCurrentJob(JobCondition.InterruptForced);
            }
        }
    }

    /// <summary>Tracks the standing reset's progress (saved), raises integrity along the curve and finishes it after 5 days.</summary>
    public class Hediff_Rimshi_SoftReset : HediffWithComps
    {
        public int resetTicks;
        public bool done;
        private int ticksWithoutJob;

        public float Fraction => SoftCycle.StandingCurve.Evaluate((float)resetTicks / GenDate.TicksPerDay);

        public override string LabelInBrackets => Fraction.ToStringPercent("F0");

        public override string TipStringExtra => base.TipStringExtra + "\n" + resetTicks.ToStringTicksToPeriod() + " of " + SoftCycle.StandingResetTicks.ToStringTicksToPeriod() + " done.";

        public override bool ShouldRemove => done || base.ShouldRemove;

        public override void PostTickInterval(int delta)
        {
            base.PostTickInterval(delta);
            if (done)
            {
                return;
            }
            if (pawn.CurJobDef != RimshiDefOf.Rimshi_SoftReset)
            {
                // The job's own end cancels the reset; this catches a reset left without one (a dev action, a lost job on load).
                ticksWithoutJob += delta;
                if (ticksWithoutJob > 250)
                {
                    StandingReset.Cancel(pawn, "no longer resetting");
                }
                return;
            }
            ticksWithoutJob = 0;
            float before = Fraction;
            resetTicks += delta;
            SoftCycle.Raise(pawn, before, Fraction);
            Need_Deathrest need = SoftCycle.Integrity(pawn);
            if (need != null)
            {
                need.lastDeathrestTick = Find.TickManager.TicksGame;
            }
            if (resetTicks >= SoftCycle.StandingResetTicks)
            {
                StandingReset.Finish(pawn);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref resetTicks, "resetTicks");
            Scribe_Values.Look(ref done, "done");
        }
    }

    /// <summary>
    /// Goes to the chosen cell and stands still facing south, or into the chosen rib cage and hangs there, until the reset
    /// finishes. Any other end cancels the reset.
    /// </summary>
    public class JobDriver_Rimshi_SoftReset : JobDriver
    {
        private const int MoteInterval = 160;

        private Building_Bed Cage => job.GetTarget(TargetIndex.A).Thing as Building_Bed;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            if (Cage != null)
            {
                return pawn.Reserve(Cage, job, Cage.SleepingSlotsCount, 0, null, errorOnFailed);
            }
            return pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);
        }

        public override string GetReport()
        {
            return "soft resetting CPU: " + StandingReset.FractionDone(pawn).ToStringPercent("F0");
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            AddFinishAction(delegate
            {
                if (StandingReset.InProgress(pawn))
                {
                    StandingReset.Cancel(pawn, pawn.Downed ? "downed" : pawn.InMentalState ? "mental break" : pawn.Drafted ? "drafted" : "interrupted");
                }
            });
            Toil reset;
            if (Cage != null)
            {
                yield return Toils_Bed.GotoBed(TargetIndex.A);
                reset = Toils_LayDown.LayDown(TargetIndex.A, hasBed: true, lookForOtherJobs: false, canSleep: false, gainRestAndHealth: false);
                reset.initAction = (System.Action)System.Delegate.Combine(reset.initAction, (System.Action)delegate { StandingReset.Begin(pawn); });
            }
            else
            {
                yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.OnCell);
                reset = ToilMaker.MakeToil("Rimshi_SoftReset");
                reset.initAction = delegate
                {
                    pawn.pather?.StopDead();
                    pawn.jobs.posture = PawnPosture.Standing;
                    pawn.rotationTracker.FaceCell(pawn.Position + IntVec3.South);
                    StandingReset.Begin(pawn);
                };
                reset.defaultCompleteMode = ToilCompleteMode.Never;
                reset.handlingFacing = true;
            }
            reset.tickIntervalAction = (System.Action<int>)System.Delegate.Combine(reset.tickIntervalAction, (System.Action<int>)delegate(int delta)
            {
                if (pawn.IsHashIntervalTick(MoteInterval, delta))
                {
                    MoteMaker.MakeAttachedOverlay(pawn, ThingDefOf.Mote_Deathresting, Vector3.zero);
                }
            });
            yield return reset;
        }
    }
}
