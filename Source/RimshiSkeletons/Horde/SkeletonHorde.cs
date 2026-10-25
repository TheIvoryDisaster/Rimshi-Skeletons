using System.Collections.Generic;
using System.Linq;
using LudeonTK;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimshiSkeletons
{
    /// <summary>
    /// The recall horde: generates factionless old-empire skeletons, marks every one with a corrupted protocol (berserk)
    /// and about one in ten with a failsafe core (rarely killed when downed), and drops them in around a cell.
    /// </summary>
    public static class SkeletonHorde
    {
        public const float MinPoints = 1000f;
        public const float PointsPerSummon = 0.2f;
        public const int MaxSummonBonus = 5;
        public const int MinPawns = 6;
        public const int MaxPawns = 40;
        public const float FailsafeFraction = 0.1f;

        private static readonly List<(string kind, float weight)> Kinds = new List<(string, float)>
        {
            ("Rimshi_HordeLegionary", 0.5f),
            ("Rimshi_HordeBladeguard", 0.35f),
            ("Rimshi_HordeCenturion", 0.15f),
        };

        /// <summary>Threat points for the next horde: the storyteller's points (at least MinPoints), more for each earlier summon.</summary>
        public static float PointsFor(Map map, int earlierSummons)
        {
            float points = Mathf.Max(MinPoints, StorytellerUtility.DefaultThreatPointsNow(map));
            return points * (1f + PointsPerSummon * Mathf.Min(earlierSummons, MaxSummonBonus));
        }

        public static List<Pawn> Generate(float points)
        {
            var pawns = new List<Pawn>();
            float spent = 0f;
            while ((spent < points || pawns.Count < MinPawns) && pawns.Count < MaxPawns)
            {
                PawnKindDef kind = DefDatabase<PawnKindDef>.GetNamed(Kinds.RandomElementByWeight(k => k.weight).kind);
                pawns.Add(GeneratePawn(kind));
                spent += kind.combatPower;
            }
            int failsafes = Mathf.Max(1, GenMath.RoundRandom(pawns.Count * FailsafeFraction));
            foreach (Pawn pawn in pawns.InRandomOrder().Take(failsafes).ToList())
            {
                pawn.health.AddHediff(RimshiDefOf.Rimshi_FailsafeCore);
            }
            return pawns;
        }

        /// <summary>One factionless horde skeleton of a kind, with its corrupted protocol.</summary>
        public static Pawn GeneratePawn(PawnKindDef kind)
        {
            Pawn pawn = PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind, null, PawnGenerationContext.NonPlayer,
                forceGenerateNewPawn: true, mustBeCapableOfViolence: true));
            pawn.health.AddHediff(RimshiDefOf.Rimshi_CorruptedProtocol);
            return pawn;
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
            DropPodUtility.DropThingsNear(center, map, pawns.Cast<Thing>(), canRoofPunch: true, forbid: false);

            int failsafes = pawns.Count(p => p.health.hediffSet.HasHediff(RimshiDefOf.Rimshi_FailsafeCore));
            Find.LetterStack.ReceiveLetter("Skeleton legion",
                "A skeleton legion of the old empire has answered the recall codes. " + pawns.Count + " ancient skeletons are dropping in, berserk and armed with spacer weapons.\n\n"
                + "Their protocols are corrupted and they belong to no faction. A few carry failsafe cores and will usually go down intact: capture them and set them to \"interrogate and reprogram\" to bring them over to the colony.",
                LetterDefOf.ThreatBig, new LookTargets(pawns));

            string weapons = string.Join(", ", pawns.GroupBy(p => p.equipment?.Primary?.def.defName ?? "none").Select(g => g.Key + " x" + g.Count()));
            string kinds = string.Join(", ", pawns.GroupBy(p => p.kindDef.defName).Select(g => g.Key + " x" + g.Count()));
            RimshiLog.Test("horde", "arrived", pawns.Count + " skeletons for " + points.ToString("0") + " points at " + center + ", " + failsafes + " with failsafe cores; "
                + kinds + "; weapons " + weapons + "; shield belts " + pawns.Count(HasShieldBelt));
            return pawns;
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
