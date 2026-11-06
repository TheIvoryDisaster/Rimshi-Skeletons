using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RimshiSkeletons
{
    /// <summary>
    /// How a free horde skeleton fights. Vanilla Berserk alone was not enough: JobGiver_Berserk idles half the time, only
    /// looks 40 cells (and 40 regions) around the pawn, and only ever melees, so a legion that lands outside that range
    /// wanders about with its charge rifles slung; and a horde skeleton that is somehow not berserk falls through to the
    /// humanlike "not a colonist, leave the map" fallback and walks off. HordeAI hunts across the whole map: the closest
    /// reachable hostile person, mechanoid or faction animal first (anything hostile at all if there is none), shooting
    /// with a ranged weapon and charging with a melee one.
    /// </summary>
    public static class HordeAI
    {
        /// <summary>How many of the nearest candidates get a (costly) reachability check each time a skeleton picks a target.</summary>
        public const int MaxReachChecks = 12;

        private static readonly IntRange MeleeExpiry = new IntRange(420, 900);
        private static readonly IntRange ShootExpiry = new IntRange(300, 600);
        private static readonly IntRange MoveExpiry = new IntRange(180, 300);

        /// <summary>Last tick each pawn logged an engage/no target line, so a scan sees what each skeleton did without spam.</summary>
        private static readonly Dictionary<int, int> lastLog = new Dictionary<int, int>();
        private const int LogInterval = 2500;

        /// <summary>A horde skeleton running loose: corrupted protocol, spawned, up, not anyone's prisoner and not the colony's.</summary>
        public static bool IsFreeHordePawn(Pawn pawn)
        {
            return pawn != null && pawn.Spawned && !pawn.Dead && !pawn.Downed && !pawn.IsPrisoner
                && (pawn.Faction == null || !pawn.Faction.IsPlayer) && ReprogramUtility.IsCorrupted(pawn);
        }

        public static Job TryGiveJob(Pawn pawn, string source)
        {
            if (!IsFreeHordePawn(pawn) || pawn.WorkTagIsDisabled(WorkTags.Violent))
            {
                return null;
            }
            Pawn target = FindTarget(pawn);
            if (target == null)
            {
                if (ShouldLog(pawn))
                {
                    RimshiLog.Test("horde", "no target", pawn.LabelShort + " (" + source + ") found nothing hostile it can reach on the map");
                }
                return null;
            }
            Job job = AttackJob(pawn, target, out string how);
            if (job != null && ShouldLog(pawn))
            {
                RimshiLog.Test("horde", "engage", pawn.LabelShort + " (" + source + ") " + how + " " + target.LabelShort + " (" + (target.Faction?.Name ?? "no faction") + ") "
                    + pawn.Position.DistanceTo(target.Position).ToString("0") + " cells away");
            }
            return job;
        }

        /// <summary>
        /// The closest reachable target: people, mechanoids and pawns with a faction first, then anything else hostile
        /// (wild animals). Candidates are sorted by straight-line distance and only the nearest few get a reach check.
        /// </summary>
        public static Pawn FindTarget(Pawn pawn)
        {
            var preferred = new List<Pawn>();
            var others = new List<Pawn>();
            var all = pawn.Map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < all.Count; i++)
            {
                Pawn other = all[i];
                if (other == pawn || other.Dead || other.Downed || other.IsPsychologicallyInvisible() || !pawn.HostileTo(other))
                {
                    continue;
                }
                if (other.RaceProps.Humanlike || other.RaceProps.IsMechanoid || other.Faction != null)
                {
                    preferred.Add(other);
                }
                else
                {
                    others.Add(other);
                }
            }
            return ClosestReachable(pawn, preferred) ?? ClosestReachable(pawn, others);
        }

        private static Pawn ClosestReachable(Pawn pawn, List<Pawn> candidates)
        {
            if (candidates.Count == 0)
            {
                return null;
            }
            IntVec3 from = pawn.Position;
            candidates.SortBy(c => (c.Position - from).LengthHorizontalSquared);
            int checks = Mathf.Min(candidates.Count, MaxReachChecks);
            for (int i = 0; i < checks; i++)
            {
                if (pawn.CanReach(candidates[i], PathEndMode.Touch, Danger.Deadly, canBashDoors: true, canBashFences: true))
                {
                    return candidates[i];
                }
            }
            return null;
        }

        /// <summary>Shoot from here if it can, move to a firing spot if not, and charge if it has no gun or no spot to shoot from.</summary>
        public static Job AttackJob(Pawn pawn, Pawn target, out string how)
        {
            Verb verb = pawn.TryGetAttackVerb(target, allowManualCastWeapons: true);
            if (verb != null && !verb.verbProps.IsMeleeAttack)
            {
                if (verb.CanHitTarget(target))
                {
                    how = "shooting";
                    Job shoot = JobMaker.MakeJob(JobDefOf.AttackStatic, target);
                    shoot.endIfCantShootTargetFromCurPos = true;
                    shoot.expiryInterval = ShootExpiry.RandomInRange;
                    shoot.checkOverrideOnExpire = true;
                    return shoot;
                }
                var request = new CastPositionRequest
                {
                    caster = pawn,
                    target = target,
                    verb = verb,
                    maxRangeFromTarget = verb.verbProps.range,
                    wantCoverFromTarget = verb.verbProps.range > 7f
                };
                if (CastPositionFinder.TryFindCastPosition(request, out IntVec3 dest) && dest != pawn.Position)
                {
                    how = "moving to shoot";
                    Job move = JobMaker.MakeJob(JobDefOf.Goto, dest);
                    move.locomotionUrgency = LocomotionUrgency.Jog;
                    move.expiryInterval = MoveExpiry.RandomInRange;
                    move.checkOverrideOnExpire = true;
                    return move;
                }
            }
            how = "charging";
            Job melee = JobMaker.MakeJob(JobDefOf.AttackMelee, target);
            melee.maxNumMeleeAttacks = 4;
            melee.expiryInterval = MeleeExpiry.RandomInRange;
            melee.checkOverrideOnExpire = true;
            melee.canBashDoors = true;
            melee.canBashFences = true;
            melee.locomotionUrgency = LocomotionUrgency.Sprint;
            return melee;
        }

        private static bool ShouldLog(Pawn pawn)
        {
            int now = Find.TickManager.TicksGame;
            if (lastLog.TryGetValue(pawn.thingIDNumber, out int last) && now - last < LogInterval)
            {
                return false;
            }
            lastLog[pawn.thingIDNumber] = now;
            return true;
        }
    }

    /// <summary>
    /// Berserk horde skeletons fight with HordeAI instead of JobGiver_Berserk's short-range melee search; anyone else
    /// berserk (and a horde skeleton with nothing to fight) gets the vanilla job.
    /// </summary>
    [HarmonyPatch(typeof(JobGiver_Berserk), "TryGiveJob")]
    public static class Patch_HordeBerserkJob
    {
        public static bool Prepare() => PatchTarget.Exists(typeof(JobGiver_Berserk), "TryGiveJob");

        public static bool Prefix(Pawn pawn, ref Job __result)
        {
            if (!HordeAI.IsFreeHordePawn(pawn))
            {
                return true;
            }
            Job job = HordeAI.TryGiveJob(pawn, "berserk");
            if (job == null)
            {
                return true;
            }
            __result = job;
            return false;
        }
    }

    /// <summary>Only for free horde skeletons (HordeAI.IsFreeHordePawn).</summary>
    public class ThinkNode_ConditionalFreeHorde : ThinkNode_Conditional
    {
        protected override bool Satisfied(Pawn pawn) => HordeAI.IsFreeHordePawn(pawn);
    }

    /// <summary>
    /// Backstop in the humanlike think tree (Humanlike_PostMentalState, ahead of lord duties and the leave-the-map
    /// fallback): a free horde skeleton that is not berserk for any reason still hunts instead of walking off the map,
    /// and its corrupted protocol is asked to put it back into Berserk straight away.
    /// </summary>
    public class JobGiver_HordeHunt : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            if (!pawn.InMentalState)
            {
                HediffComp_CorruptedProtocol comp = ReprogramUtility.Protocol(pawn)?.TryGetComp<HediffComp_CorruptedProtocol>();
                comp?.Refresh("not berserk in think tree");
            }
            return HordeAI.TryGiveJob(pawn, "hunt");
        }
    }
}
