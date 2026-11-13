using System.Collections.Generic;
using System.Linq;
using LudeonTK;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace RimshiSkeletons
{
    /// <summary>
    /// The recall horde: generates old-empire skeletons in the hidden, permanently hostile recalled legion faction, marks
    /// about two in three with a corrupted protocol (cannot be recruited, only reprogrammed) and about one in ten with a
    /// failsafe core (rarely killed when downed), and drops them in around a cell as a raid under an assault lord, so they
    /// are hostile in their pods and get the normal raid AI and mechanics.
    /// </summary>
    public static class SkeletonHorde
    {
        public const float MinPoints = 1000f;
        public const float PointsPerSummon = 0.2f;
        public const int MaxSummonBonus = 5;
        /// <summary>Hordes were cut by a fifth after the first real one overran the colony (Matthew, 2026-11-09).</summary>
        public const float SizeFactor = 0.8f;
        public const int MinPawns = 5;
        public const int MaxPawns = 32;
        public const float FailsafeFraction = 0.1f;

        /// <summary>Chance that a horde skeleton has no corrupted protocol and can be recruited the normal way (at least one per horde).</summary>
        public const float RecruitableChance = 1f / 3f;

        private static readonly List<(string kind, float weight)> Kinds = new List<(string, float)>
        {
            ("Rimshi_HordeLegionary", 0.5f),
            ("Rimshi_HordeBladeguard", 0.35f),
            ("Rimshi_HordeCenturion", 0.15f),
        };

        /// <summary>Threat points for the next horde: the storyteller's points (at least MinPoints) times SizeFactor, more for each earlier summon.</summary>
        public static float PointsFor(Map map, int earlierSummons)
        {
            float points = Mathf.Max(MinPoints, StorytellerUtility.DefaultThreatPointsNow(map));
            return points * SizeFactor * (1f + PointsPerSummon * Mathf.Min(earlierSummons, MaxSummonBonus));
        }

        public static List<Pawn> Generate(float points)
        {
            Faction legion = LegionFaction();
            var pawns = new List<Pawn>();
            float spent = 0f;
            while ((spent < points || pawns.Count < MinPawns) && pawns.Count < MaxPawns)
            {
                PawnKindDef kind = DefDatabase<PawnKindDef>.GetNamed(Kinds.RandomElementByWeight(k => k.weight).kind);
                pawns.Add(GeneratePawn(kind, legion, corrupted: !Rand.Chance(RecruitableChance)));
                spent += kind.combatPower;
            }
            if (pawns.TrueForAll(ReprogramUtility.IsCorrupted))
            {
                MakeRecruitable(pawns.RandomElement());
            }
            int failsafes = Mathf.Max(1, GenMath.RoundRandom(pawns.Count * FailsafeFraction));
            foreach (Pawn pawn in pawns.InRandomOrder().Take(failsafes).ToList())
            {
                pawn.health.AddHediff(RimshiDefOf.Rimshi_FailsafeCore);
            }
            return pawns;
        }

        /// <summary>
        /// One horde skeleton of a kind in the legion faction. Corrupted ones get the protocol (no recruiting, reprogram
        /// only); the rest can be recruited the normal way and are never unwaveringly loyal.
        /// </summary>
        public static Pawn GeneratePawn(PawnKindDef kind, Faction faction, bool corrupted)
        {
            Pawn pawn = PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind, faction, PawnGenerationContext.NonPlayer,
                forceGenerateNewPawn: true, mustBeCapableOfViolence: true));
            if (corrupted)
            {
                pawn.health.AddHediff(RimshiDefOf.Rimshi_CorruptedProtocol);
            }
            else if (pawn.guest != null)
            {
                pawn.guest.Recruitable = true;
            }
            return pawn;
        }

        private static void MakeRecruitable(Pawn pawn)
        {
            Hediff protocol = ReprogramUtility.Protocol(pawn);
            if (protocol != null)
            {
                pawn.health.RemoveHediff(protocol);
            }
            if (pawn.guest != null)
            {
                pawn.guest.Recruitable = true;
            }
        }

        /// <summary>The recalled legion: the world's one, or a new one for saves from before it existed.</summary>
        public static Faction LegionFaction()
        {
            FactionDef def = RimshiDefOf.Rimshi_RecallLegion;
            Faction faction = Find.FactionManager.FirstFactionOfDef(def);
            if (faction == null)
            {
                faction = FactionGenerator.NewGeneratedFaction(new FactionGeneratorParms(def));
                Find.FactionManager.Add(faction);
                RimshiLog.Test("horde", "legion added", faction.Name + " created (hidden " + def.hidden + ", hostile to player " + faction.HostileTo(Faction.OfPlayer) + ")");
            }
            return faction;
        }

        /// <summary>A raid lord for the horde: assault the colony, no kidnapping, stealing or fleeing.</summary>
        public static Lord MakeLord(Faction faction, Map map, IEnumerable<Pawn> pawns)
        {
            return LordMaker.MakeNewLord(faction, new LordJob_AssaultColony(faction, canKidnap: false, canTimeoutOrFlee: false, sappers: false,
                useAvoidGridSmart: false, canSteal: false), map, pawns);
        }

        public static IEnumerable<PawnKindDef> AllKinds => Kinds.Select(k => DefDatabase<PawnKindDef>.GetNamed(k.kind));

        /// <summary>Generates the horde and drops it in around <paramref name="center"/> (anywhere on the map if that is not usable).</summary>
        public static List<Pawn> Arrive(Map map, IntVec3 center, float points)
        {
            if (!center.IsValid || !center.InBounds(map))
            {
                center = DropCellFinder.RandomDropSpot(map);
            }
            List<Pawn> pawns = Generate(points);
            Faction legion = pawns[0].Faction;
            DropPodUtility.DropThingsNear(center, map, pawns.Cast<Thing>(), canRoofPunch: true, forbid: false);
            Lord lord = MakeLord(legion, map, pawns);

            int failsafes = pawns.Count(p => p.health.hediffSet.HasHediff(RimshiDefOf.Rimshi_FailsafeCore));
            Find.LetterStack.ReceiveLetter("Skeleton legion",
                "A skeleton legion of the old empire has answered the recall codes. " + pawns.Count + " ancient skeletons are dropping in, hostile and armed with spacer weapons.\n\n"
                + "Most of their protocols are corrupted: captured, those can only be brought over by setting them to \"interrogate and reprogram\". A few carry failsafe cores and will usually go down intact.",
                LetterDefOf.ThreatBig, new LookTargets(pawns));

            string weapons = string.Join(", ", pawns.GroupBy(p => p.equipment?.Primary?.def.defName ?? "none").Select(g => g.Key + " x" + g.Count()));
            string kinds = string.Join(", ", pawns.GroupBy(p => p.kindDef.defName).Select(g => g.Key + " x" + g.Count()));
            int recruitable = pawns.Count(p => !ReprogramUtility.IsCorrupted(p));
            RimshiLog.Test("horde", "arrived", pawns.Count + " skeletons of " + legion.Name + " (hostile " + legion.HostileTo(Faction.OfPlayer) + ", lord " + lord.LordJob.GetType().Name + ") for "
                + points.ToString("0") + " points at " + center + ", " + failsafes + " with failsafe cores, " + recruitable + " recruitable; "
                + kinds + "; weapons " + weapons + "; shield belts " + pawns.Count(HasShieldBelt));
            return pawns;
        }

        /// <summary>
        /// Horde skeletons from before the legion faction (factionless, berserk) join the legion on load: berserk ends and
        /// the free ones on each map get an assault lord. Prisoners and the colony's own pawns are left alone.
        /// </summary>
        public static void MigrateSave()
        {
            foreach (Map map in Find.Maps)
            {
                List<Pawn> free = map.mapPawns.AllPawnsSpawned.Where(p => p.Faction == null && !p.IsPrisoner && !p.Dead && ReprogramUtility.IsCorrupted(p)).ToList();
                if (free.Count == 0)
                {
                    continue;
                }
                Faction legion = LegionFaction();
                foreach (Pawn pawn in free)
                {
                    if (pawn.InMentalState)
                    {
                        pawn.MentalState.RecoverFromState();
                    }
                    pawn.SetFaction(legion);
                }
                MakeLord(legion, map, free.Where(p => !p.Downed));
                RimshiLog.Test("horde", "migrated", free.Count + " factionless horde skeletons on " + map + " joined " + legion.Name);
            }
        }

        public static bool HasShieldBelt(Pawn pawn) => pawn.apparel != null && pawn.apparel.WornApparel.Any(a => a.def == ThingDefOf.Apparel_ShieldBelt);

        [DebugAction("Rimshi Skeletons", "Summon skeleton horde now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void DebugSummon()
        {
            Map map = Find.CurrentMap;
            Arrive(map, UI.MouseCell(), PointsFor(map, RimshiGameComponent.Instance?.Summons ?? 0));
        }
    }

    /// <summary>A horde on its way: where it lands and when.</summary>
    public class PendingHorde : IExposable
    {
        public Map map;
        public IntVec3 cell;
        public int arriveTick;

        public void ExposeData()
        {
            Scribe_References.Look(ref map, "map");
            Scribe_Values.Look(ref cell, "cell");
            Scribe_Values.Look(ref arriveTick, "arriveTick");
        }
    }
}
