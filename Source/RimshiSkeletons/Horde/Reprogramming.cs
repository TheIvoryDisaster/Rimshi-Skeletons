using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RimshiSkeletons
{
    /// <summary>
    /// Reprogramming a captured horde skeleton. Each warden session lowers the corrupted protocol's severity by
    /// progressBase + progressPerIntellectual x the warden's Intellectual level (about 4 sessions at Intellectual 10). At
    /// zero the protocol and failsafe core are removed, the skeleton becomes recruitable again and joins the colony.
    /// </summary>
    public static class ReprogramUtility
    {
        public static Hediff Protocol(Pawn pawn) => pawn?.health?.hediffSet?.GetFirstHediffOfDef(RimshiDefOf.Rimshi_CorruptedProtocol);

        public static bool IsCorrupted(Pawn pawn) => Protocol(pawn) != null;

        public static float ProgressFor(Pawn warden, Hediff protocol)
        {
            var props = protocol.def.CompProps<HediffCompProperties_CorruptedProtocol>();
            int skill = warden.skills?.GetSkill(SkillDefOf.Intellectual)?.Level ?? 0;
            return (props?.progressBase ?? 0.12f) + (props?.progressPerIntellectual ?? 0.025f) * skill;
        }

        public static void DoSession(Pawn warden, Pawn prisoner)
        {
            Hediff protocol = Protocol(prisoner);
            if (protocol == null)
            {
                return;
            }
            float progress = ProgressFor(warden, protocol);
            float left = protocol.Severity - progress;
            warden.skills?.Learn(SkillDefOf.Intellectual, 150f);
            if (left > 0.001f)
            {
                protocol.Severity = left;
                if (prisoner.Spawned)
                {
                    MoteMaker.ThrowText(prisoner.DrawPos, prisoner.Map, "Corruption " + left.ToStringPercent("0"), 4f);
                }
                RimshiLog.Test("reprogram", "session", warden.LabelShort + " reprogrammed " + prisoner.LabelShort + ": -" + progress.ToStringPercent("0") + ", " + left.ToStringPercent("0") + " corruption left");
                return;
            }
            Finish(prisoner, warden);
        }

        public static void Finish(Pawn prisoner, Pawn warden)
        {
            Hediff failsafe = prisoner.health.hediffSet.GetFirstHediffOfDef(RimshiDefOf.Rimshi_FailsafeCore);
            if (failsafe != null)
            {
                prisoner.health.RemoveHediff(failsafe);
            }
            Hediff protocol = Protocol(prisoner);
            if (protocol != null)
            {
                prisoner.health.RemoveHediff(protocol);
            }
            if (prisoner.InMentalState)
            {
                prisoner.MentalState.RecoverFromState();
            }
            if (prisoner.guest != null)
            {
                prisoner.guest.Recruitable = true;
            }
            RecruitUtility.Recruit(prisoner, Faction.OfPlayer, warden);
            bool joined = prisoner.Faction == Faction.OfPlayer && !IsCorrupted(prisoner);
            if (joined)
            {
                Find.LetterStack.ReceiveLetter("Skeleton reprogrammed: " + prisoner.LabelShort,
                    (warden != null ? warden.LabelShort + " has" : "The colony has") + " rewritten " + prisoner.LabelShort + "'s corrupted recall protocol. It now serves the colony.",
                    LetterDefOf.PositiveEvent, prisoner);
                RimshiLog.Test("reprogram", "joined", prisoner.LabelShort + " joined " + prisoner.Faction.Name + (warden != null ? " (warden " + warden.LabelShort + ")" : ""));
            }
            else
            {
                RimshiLog.Fail("reprogram", "joined", prisoner.LabelShort + ": faction " + (prisoner.Faction?.Name ?? "none") + ", still corrupted " + IsCorrupted(prisoner));
            }
        }
    }

    /// <summary>Wardens reprogram prisoners set to Rimshi_Reprogram, on vanilla's prisoner chat schedule (a player order skips the wait).</summary>
    public class WorkGiver_Warden_Reprogram : WorkGiver_Warden
    {
        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            if (!ShouldTakeCareOfPrisoner(pawn, t, forced))
            {
                return false;
            }
            var prisoner = (Pawn)t;
            return prisoner.guest.IsInteractionEnabled(RimshiDefOf.Rimshi_Reprogram)
                && ReprogramUtility.IsCorrupted(prisoner)
                && (forced || prisoner.guest.ScheduledForInteraction)
                && prisoner.Awake()
                && !prisoner.InAggroMentalState
                && pawn.CanReserve(prisoner, 1, -1, null, forced);
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            return HasJobOnThing(pawn, t, forced) ? JobMaker.MakeJob(RimshiDefOf.Rimshi_ReprogramSkeleton, t) : null;
        }
    }

    public class JobDriver_ReprogramSkeleton : JobDriver
    {
        public const int SessionTicks = 600;

        private Pawn Prisoner => (Pawn)job.GetTarget(TargetIndex.A).Thing;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(Prisoner, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            this.FailOn(() => !Prisoner.IsPrisonerOfColony || !ReprogramUtility.IsCorrupted(Prisoner) || !Prisoner.guest.IsInteractionEnabled(RimshiDefOf.Rimshi_Reprogram));
            yield return Toils_Interpersonal.GotoPrisoner(pawn, Prisoner, RimshiDefOf.Rimshi_Reprogram);
            yield return Toils_General.Wait(SessionTicks, TargetIndex.A).WithProgressBarToilDelay(TargetIndex.A);
            yield return Toils_Interpersonal.SetLastInteractTime(TargetIndex.A);
            Toil session = ToilMaker.MakeToil("Rimshi_ReprogramSession");
            session.initAction = () => ReprogramUtility.DoSession(pawn, Prisoner);
            session.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return session;
        }
    }
}
