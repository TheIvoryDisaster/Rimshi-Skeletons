using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RimshiSkeletons
{
    /// <summary>
    /// The horde's breaking point (Matthew, 2026-11-09: the first real horde was far too strong). Vanilla has no
    /// "the colony is losing" signal short of the game-over check (every colonist down), so the trigger is ours: once at
    /// least half the free colonists on a map are downed, every free horde skeleton there gets the recall frenzy for good.
    /// The frenzy keeps it berserk, and berserk horde skeletons hunt each other (closest reachable legion skeleton first,
    /// shooting or charging) and only fall back to vanilla berserk, which can still hit colonists, once none of the
    /// legion is left standing within reach.
    /// </summary>
    public static class HordeInfighting
    {
        public const int CheckInterval = 250;

        /// <summary>Fraction of the map's free colonists that must be downed for the horde to turn on itself.</summary>
        public const float DownedFraction = 0.5f;

        /// <summary>How many of the nearest candidates get a (costly) reachability check each time a skeleton picks a target.</summary>
        public const int MaxReachChecks = 12;

        private static readonly IntRange MeleeExpiry = new IntRange(420, 900);
        private static readonly IntRange ShootExpiry = new IntRange(300, 600);
        private static readonly IntRange MoveExpiry = new IntRange(180, 300);

        private static readonly Dictionary<int, int> lastLog = new Dictionary<int, int>();
        private const int LogInterval = 2500;

        /// <summary>The losing rule on its own: at least one colonist down and at least half of them.</summary>
        public static bool ColonyLosing(int downed, int total) => total > 0 && downed > 0 && downed >= total * DownedFraction;

        public static bool IsFrenzied(Pawn pawn) => pawn?.health?.hediffSet != null && pawn.health.hediffSet.HasHediff(RimshiDefOf.Rimshi_HordeFrenzy);

        /// <summary>A legion skeleton running loose on a map: spawned, alive, not anyone's prisoner.</summary>
        public static bool IsFreeLegionPawn(Pawn pawn, Faction legion)
        {
            return pawn != null && legion != null && pawn.Faction == legion && pawn.Spawned && !pawn.Dead && !pawn.IsPrisoner;
        }

        /// <summary>Every few seconds per map: if the colony there is losing, the free horde skeletons on it get the frenzy.</summary>
        public static void CheckMaps()
        {
            Faction legion = Find.FactionManager.FirstFactionOfDef(RimshiDefOf.Rimshi_RecallLegion);
            if (legion == null)
            {
                return;
            }
            foreach (Map map in Find.Maps)
            {
                CheckMap(map, legion);
            }
        }

        public static void CheckMap(Map map, Faction legion)
        {
            List<Pawn> loose = map.mapPawns.SpawnedPawnsInFaction(legion).Where(p => IsFreeLegionPawn(p, legion) && !IsFrenzied(p)).ToList();
            if (loose.Count == 0)
            {
                return;
            }
            List<Pawn> colonists = map.mapPawns.FreeColonistsSpawned.ToList();
            int downed = colonists.Count(p => p.Downed);
            if (!ColonyLosing(downed, colonists.Count))
            {
                return;
            }
            bool first = !map.mapPawns.SpawnedPawnsInFaction(legion).Any(IsFrenzied);
            foreach (Pawn pawn in loose)
            {
                pawn.health.AddHediff(RimshiDefOf.Rimshi_HordeFrenzy);
            }
            RimshiLog.Test("horde", "infighting", loose.Count + " horde skeletons on " + map + " turned on each other: " + downed + " of " + colonists.Count + " free colonists downed");
            if (first)
            {
                Find.LetterStack.ReceiveLetter("Legion in disarray",
                    "Something in the recall signal has given way. The skeletons of the legion no longer know friend from foe, and are turning their weapons on each other.",
                    LetterDefOf.PositiveEvent, new LookTargets(loose));
            }
        }

        /// <summary>
        /// For a frenzied skeleton: the closest reachable other skeleton of its legion that is still up, shot at or charged.
        /// Null when there is none, so vanilla berserk takes over.
        /// </summary>
        public static Job InfightJob(Pawn pawn)
        {
            if (!pawn.Spawned || pawn.Downed || pawn.Faction == null || pawn.WorkTagIsDisabled(WorkTags.Violent))
            {
                return null;
            }
            Faction legion = pawn.Faction;
            var candidates = new List<Pawn>();
            foreach (Pawn other in pawn.Map.mapPawns.SpawnedPawnsInFaction(legion))
            {
                if (other != pawn && !other.Downed && IsFreeLegionPawn(other, legion))
                {
                    candidates.Add(other);
                }
            }
            Pawn target = ClosestReachable(pawn, candidates);
            if (target == null)
            {
                return null;
            }
            Job job = AttackJob(pawn, target, out string how);
            if (job != null && ShouldLog(pawn))
            {
                RimshiLog.Test("horde", "infight", pawn.LabelShort + " " + how + " " + target.LabelShort + " " + pawn.Position.DistanceTo(target.Position).ToString("0") + " cells away");
            }
            return job;
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
    /// Recall frenzy: keeps a free horde skeleton berserk (berserk pawns are hostile to everyone, their own faction
    /// included). Removed if the skeleton is captured or joins the colony.
    /// </summary>
    public class HediffComp_HordeFrenzy : HediffComp
    {
        private bool failLogged;

        public override void CompPostPostAdd(DamageInfo? dinfo)
        {
            base.CompPostPostAdd(dinfo);
            Refresh();
        }

        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            base.CompPostTickInterval(ref severityAdjustment, delta);
            if (Pawn.IsHashIntervalTick(HordeInfighting.CheckInterval, delta))
            {
                Refresh();
            }
        }

        /// <summary>Captured or recruited: the frenzy is gone (removed by the health tracker, not mid-tick by us).</summary>
        public override bool CompShouldRemove => Pawn.IsPrisoner || (Pawn.Faction != null && Pawn.Faction.IsPlayer);

        public void Refresh()
        {
            Pawn pawn = Pawn;
            if (pawn.Dead || CompShouldRemove)
            {
                return;
            }
            if (!pawn.Spawned || pawn.Downed || pawn.InMentalState)
            {
                return;
            }
            if (pawn.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.Berserk, "recall frenzy", forced: true, forceWake: true, transitionSilently: true))
            {
                RimshiLog.Test("horde", "frenzy", pawn.LabelShort + " (" + pawn.kindDef.defName + ") went berserk at " + pawn.Position);
            }
            else if (!failLogged)
            {
                failLogged = true;
                RimshiLog.Fail("horde", "frenzy", pawn.LabelShort + ": the game would not start Berserk (awake " + pawn.Awake() + ", job " + (pawn.CurJobDef?.defName ?? "none") + ")");
            }
        }
    }

    /// <summary>Berserk frenzied horde skeletons go for their own legion first; anyone else gets the vanilla berserk job.</summary>
    [HarmonyPatch(typeof(JobGiver_Berserk), "TryGiveJob")]
    public static class Patch_HordeInfightJob
    {
        public static bool Prepare() => PatchTarget.Exists(typeof(JobGiver_Berserk), "TryGiveJob");

        public static bool Prefix(Pawn pawn, ref Job __result)
        {
            if (!HordeInfighting.IsFrenzied(pawn))
            {
                return true;
            }
            Job job = HordeInfighting.InfightJob(pawn);
            if (job == null)
            {
                return true;
            }
            __result = job;
            return false;
        }
    }
}
