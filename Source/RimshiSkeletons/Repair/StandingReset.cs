using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RimshiSkeletons
{
    /// <summary>
    /// The soft reset without a repair bed (docs/design/cpu-soft-cycle.md): ordered from the skeleton's "Soft cycle"
    /// button (SoftCycle.CycleGizmo) onto a cell, where it stands still for 5 days, or onto a free rib cage, where it hangs. Integrity comes
    /// back slowly for three days and quickly on the last (SoftCycle.StandingCurve). The job ending early for any reason
    /// (woken, downed, a mental break) cancels it with 0% integrity and an overload. Once it has begun, only the "Wake up"
    /// button ends it on the player's side: orders are refused and drafting is disabled (Patch_SoftResetRefusesOrders,
    /// Patch_SoftResetNoDraft), so a stray click cannot cost the cycle.
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
        public static void Begin(Pawn pawn) => Begin(pawn, lying: false);

        /// <summary>A downed skeleton's soft reset, begun from its "Soft cycle" button where it lies; it needs no job.</summary>
        public static void BeginLying(Pawn pawn)
        {
            if (!pawn.Downed)
            {
                return;
            }
            Begin(pawn, lying: true);
        }

        /// <summary>
        /// A downed skeleton whose CPU integrity has run out begins a lying soft reset by itself (Matthew, 2026-11-09), so
        /// the reset's mending (SoftCycleMending) slowly gets it back on its feet. Called from the integrity tick while it is
        /// at 0%, so one downed after running out starts too. Not while carried, in a mental state, hanging in a rib cage
        /// or lying in a powered repair bed (which is already repairing it, as for the "Soft cycle" button).
        /// </summary>
        public static bool TryAutoLying(Pawn pawn)
        {
            if (pawn?.health == null || pawn.Dead || !pawn.Downed || !pawn.Spawned || pawn.InMentalState || InProgress(pawn) || pawn.Deathresting
                || RibCage.Suspended(pawn) || !SkeletonUtility.HasFrame(pawn))
            {
                return false;
            }
            CompRimshi_RepairBed bed = SoftCycle.RepairBedOf(pawn.CurrentBed());
            if (bed != null && bed.Powered)
            {
                return false;
            }
            Begin(pawn, lying: true);
            if (!InProgress(pawn))
            {
                return false;
            }
            RimshiLog.Test("softcycle", "auto lying", pawn.LabelShort + " is downed with no CPU integrity left and began a soft reset where it lies at " + pawn.Position);
            if (PawnUtility.ShouldSendNotificationAbout(pawn))
            {
                Messages.Message(pawn.LabelShort + " has begun a soft reset where it lies.", pawn, MessageTypeDefOf.NeutralEvent);
            }
            return true;
        }

        private static void Begin(Pawn pawn, bool lying)
        {
            if (InProgress(pawn))
            {
                return;
            }
            Hediff_Rimshi_SoftReset reset = pawn.health.AddHediff(RimshiDefOf.Rimshi_SoftResetting) as Hediff_Rimshi_SoftReset;
            if (reset != null)
            {
                reset.lying = lying;
            }
            string where = lying ? "lying downed " + (pawn.CurrentBed() != null ? "in " + pawn.CurrentBed().LabelShort : "at " + pawn.Position)
                : RibCage.Suspended(pawn) ? "hanging in a rib cage" : "standing at " + pawn.Position;
            RimshiLog.Test("softcycle", "start standing", pawn.LabelShort + " began a soft reset " + where + ", integrity " + SoftCycle.Integrity(pawn)?.CurLevelPercentage.ToStringPercent());
        }

        /// <summary>
        /// A skeleton that began its reset downed and got up before it ended carries on with the reset on its feet (or
        /// hanging, in a rib cage), under the same rules as one it walked to.
        /// </summary>
        public static void Rise(Pawn pawn)
        {
            Building_Bed cage = RibCage.Suspended(pawn) ? pawn.CurrentBed() : null;
            Job job = cage != null ? JobMaker.MakeJob(RimshiDefOf.Rimshi_SoftReset, cage) : JobMaker.MakeJob(RimshiDefOf.Rimshi_SoftReset, pawn.Position);
            pawn.jobs.StartJob(job, JobCondition.InterruptForced);
            RimshiLog.Test("softcycle", "rose", pawn.LabelShort + " got up during a soft reset begun downed and carries on " + (cage != null ? "in a rib cage" : "standing at " + pawn.Position)
                + " (" + FractionDone(pawn).ToStringPercent("F0") + ")");
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

        /// <summary>Begun downed, where the skeleton lay, with no job (StandingReset.BeginLying).</summary>
        public bool lying;
        private int ticksWithoutJob;

        public float Fraction => SoftCycle.StandingCurve.Evaluate((float)resetTicks / GenDate.TicksPerDay);

        public override string LabelInBrackets => Fraction.ToStringPercent("F0");

        public override bool ShouldRemove => done || base.ShouldRemove;

        public override void PostTickInterval(int delta)
        {
            base.PostTickInterval(delta);
            if (done)
            {
                return;
            }
            if (lying)
            {
                if (!pawn.Downed)
                {
                    lying = false;
                    StandingReset.Rise(pawn);
                    return;
                }
                if (!pawn.Spawned)
                {
                    // Carried somewhere (a rescue, a kidnapper): held, neither progressing nor cancelled.
                    return;
                }
            }
            else if (pawn.CurJobDef != RimshiDefOf.Rimshi_SoftReset)
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
            if (pawn.IsHashIntervalTick(SoftCycleMending.Interval, delta))
            {
                SoftCycleMending.Step(pawn, SoftCycle.StandingResetTicks - resetTicks);
            }
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
            Scribe_Values.Look(ref lying, "lying");
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

        // Once begun, vanilla treats the reset like deathrest: no player order interrupts it (the patches below refuse them).
        public override bool PlayerInterruptable => !StandingReset.InProgress(pawn);

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

    /// <summary>
    /// A skeleton in a standing soft cycle refuses player orders (right-click jobs, move orders, forced work) instead of
    /// queueing them, so nothing ends the cycle except "Wake up". The reset's own job is still let through.
    /// </summary>
    [HarmonyPatch(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.TryTakeOrderedJob))]
    public static class Patch_SoftResetRefusesOrders
    {
        public static bool Prefix(Job job, Pawn ___pawn, ref bool __result)
        {
            if (job == null || job.def == RimshiDefOf.Rimshi_SoftReset || !StandingReset.InProgress(___pawn))
            {
                return true;
            }
            Messages.Message(___pawn.LabelShort + " is soft cycling and will not respond until woken.", ___pawn, MessageTypeDefOf.RejectInput, false);
            RimshiLog.Test("softcycle", "order refused", ___pawn.LabelShort + " refused " + job.def.defName + " while soft resetting");
            __result = false;
            return false;
        }
    }

    /// <summary>
    /// Drafting a skeleton in a standing soft cycle: the draft button is disabled, and the setter ignores drafting from
    /// anywhere else (hotkeys, multi-select, other mods).
    /// </summary>
    [HarmonyPatch(typeof(Pawn_DraftController), nameof(Pawn_DraftController.Drafted), MethodType.Setter)]
    public static class Patch_SoftResetNoDraft
    {
        public static bool Prefix(Pawn_DraftController __instance, bool value)
        {
            if (!value || !StandingReset.InProgress(__instance.pawn))
            {
                return true;
            }
            RimshiLog.Test("softcycle", "draft refused", __instance.pawn.LabelShort + " was not drafted while soft resetting");
            return false;
        }
    }

    [HarmonyPatch(typeof(Pawn_DraftController), "GetGizmos")]
    public static class Patch_SoftResetDraftGizmo
    {
        public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, Pawn_DraftController __instance)
        {
            bool resetting = StandingReset.InProgress(__instance.pawn);
            foreach (Gizmo gizmo in __result)
            {
                if (resetting && gizmo is Command_Toggle toggle && toggle.hotKey == KeyBindingDefOf.Command_ColonistDraft && !toggle.Disabled)
                {
                    toggle.Disable(__instance.pawn.LabelShort + " is soft cycling. Wake it up first.");
                }
                yield return gizmo;
            }
        }
    }
}
