using RimWorld;
using Verse;

namespace RimshiSkeletons
{
    public class HediffCompProperties_CorruptedProtocol : HediffCompProperties
    {
        /// <summary>Corruption removed by one reprogramming session, before skill.</summary>
        public float progressBase = 0.12f;

        /// <summary>Extra corruption removed per level of the warden's Intellectual skill.</summary>
        public float progressPerIntellectual = 0.025f;

        public HediffCompProperties_CorruptedProtocol()
        {
            compClass = typeof(HediffComp_CorruptedProtocol);
        }
    }

    /// <summary>
    /// Corrupted recall protocol. While the skeleton is free (spawned, up, not the colony's prisoner, not in the player
    /// faction) it is kept berserk. While it has the protocol it cannot be recruited by talking; captured, it is switched
    /// to the Reprogram interaction once (the player can change it). Reprogramming (ReprogramUtility) lowers the severity.
    /// </summary>
    public class HediffComp_CorruptedProtocol : HediffComp
    {
        public const int CheckInterval = 250;

        private bool modeSet;

        /// <summary>Logged once per pawn per session when the game refuses to start Berserk.</summary>
        private bool failLogged;

        public HediffCompProperties_CorruptedProtocol Props => (HediffCompProperties_CorruptedProtocol)props;

        public override void CompPostPostAdd(DamageInfo? dinfo)
        {
            base.CompPostPostAdd(dinfo);
            Refresh();
        }

        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            base.CompPostTickInterval(ref severityAdjustment, delta);
            if (Pawn.IsHashIntervalTick(CheckInterval, delta))
            {
                Refresh();
            }
        }

        public void Refresh(string why = null)
        {
            Pawn pawn = Pawn;
            if (pawn.Dead || (pawn.Faction != null && pawn.Faction.IsPlayer))
            {
                return;
            }
            if (pawn.guest != null && pawn.guest.Recruitable)
            {
                pawn.guest.Recruitable = false;
            }
            if (pawn.IsPrisonerOfColony)
            {
                if (!modeSet && pawn.guest != null)
                {
                    modeSet = true;
                    pawn.guest.SetExclusiveInteraction(RimshiDefOf.Rimshi_Reprogram);
                    RimshiLog.Test("reprogram", "captured", pawn.LabelShort + ": corruption " + parent.Severity.ToStringPercent() + ", interaction set to reprogram");
                }
                return;
            }
            if (pawn.Spawned && !pawn.Downed && !pawn.InMentalState)
            {
                if (pawn.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.Berserk, "corrupted recall protocol", forced: true, forceWake: true, transitionSilently: true))
                {
                    RimshiLog.Test("horde", "berserk", pawn.LabelShort + " (" + pawn.kindDef.defName + ") went berserk at " + pawn.Position + (why != null ? " (" + why + ")" : ""));
                }
                else if (!failLogged)
                {
                    failLogged = true;
                    RimshiLog.Fail("horde", "berserk refused", pawn.LabelShort + ": the game would not start Berserk (awake " + pawn.Awake() + ", job " + (pawn.CurJobDef?.defName ?? "none") + ")");
                }
            }
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref modeSet, "modeSet");
        }
    }

    public class HediffCompProperties_FailsafeCore : HediffCompProperties
    {
        /// <summary>Replaces vanilla's chance that a downed non-colonist is killed outright instead.</summary>
        public float deathOnDownedChance = 0.1f;

        public HediffCompProperties_FailsafeCore()
        {
            compClass = typeof(HediffComp_FailsafeCore);
        }
    }

    /// <summary>
    /// Failsafe core: sets the pawn's death on downed chance override (Pawn_HealthTracker.overrideDeathOnDownedChance) so
    /// it is rarely finished off when it goes down, and puts the old value back when the hediff is removed. Set again on
    /// load and every few seconds in case anything else changed it.
    /// </summary>
    public class HediffComp_FailsafeCore : HediffComp
    {
        private float previous = -1f;
        private bool applied;

        public HediffCompProperties_FailsafeCore Props => (HediffCompProperties_FailsafeCore)props;

        public override void CompPostPostAdd(DamageInfo? dinfo)
        {
            base.CompPostPostAdd(dinfo);
            Apply();
            RimshiLog.Test("horde", "failsafe", Pawn.LabelShort + ": death on downed chance " + Pawn.health.overrideDeathOnDownedChance.ToStringPercent() + " (was " + previous.ToStringPercent() + ")");
        }

        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            base.CompPostTickInterval(ref severityAdjustment, delta);
            if (Pawn.IsHashIntervalTick(HediffComp_CorruptedProtocol.CheckInterval, delta))
            {
                Apply();
            }
        }

        private void Apply()
        {
            if (!applied)
            {
                previous = Pawn.health.overrideDeathOnDownedChance;
                applied = true;
            }
            Pawn.health.overrideDeathOnDownedChance = Props.deathOnDownedChance;
        }

        public override void CompPostPostRemoved()
        {
            base.CompPostPostRemoved();
            if (applied)
            {
                Pawn.health.overrideDeathOnDownedChance = previous;
            }
        }

        public override void Notify_PawnDied(DamageInfo? dinfo, Hediff culprit = null)
        {
            base.Notify_PawnDied(dinfo, culprit);
            RimshiLog.Test("horde", "failsafe died", Pawn.LabelShort + " died despite its failsafe core (" + (culprit?.def.defName ?? dinfo?.Def?.defName ?? "unknown cause") + ")");
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref previous, "previous", -1f);
            Scribe_Values.Look(ref applied, "applied");
        }
    }
}
