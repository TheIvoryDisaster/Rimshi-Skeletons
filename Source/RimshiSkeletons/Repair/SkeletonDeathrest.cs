using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimshiSkeletons
{
    /// <summary>Repair bed settings for deathrest: chemfuel burned over one full deathrest (the bed's CompRefuelable holds it).</summary>
    public class CompProperties_Rimshi_DeathrestFuel : CompProperties
    {
        public float fuelPerDeathrest = 35f;

        public CompProperties_Rimshi_DeathrestFuel()
        {
            compClass = typeof(CompRimshi_DeathrestFuel);
        }
    }

    /// <summary>
    /// Makes the skeleton repair bed the skeletons' deathrest casket. A skeleton deathrests only in a repair bed with fuel in
    /// it; the bed burns fuelPerDeathrest chemfuel over one full deathrest (vanilla's MinDeathrestTicks), and deathrest
    /// pauses while the tank is empty. Power is not needed: that is only for repairs.
    /// </summary>
    public class CompRimshi_DeathrestFuel : ThingComp
    {
        public CompProperties_Rimshi_DeathrestFuel Props => (CompProperties_Rimshi_DeathrestFuel)props;

        public CompRefuelable Refuelable => parent.GetComp<CompRefuelable>();

        public bool HasFuel => Refuelable?.HasFuel ?? false;

        public void Burn(Gene_Deathrest gene, int delta)
        {
            Refuelable?.ConsumeFuel(Props.fuelPerDeathrest * delta / gene.MinDeathrestTicks);
        }

        public override string CompInspectStringExtra()
        {
            List<string> resting = (parent as Building_Bed)?.CurOccupants.Where(p => p.Deathresting).Select(p => p.LabelShort).ToList();
            if (resting == null || resting.Count == 0)
            {
                return null;
            }
            return (HasFuel ? "Deathresting: " : "Out of chemfuel, deathrest paused: ") + string.Join(", ", resting);
        }
    }

    public static class SkeletonDeathrest
    {
        public static GeneDef Gene => RimshiDefOf.Rimshi_Deathrest;

        /// <summary>True for a skeleton (frame gene) that deathrests (any Gene_Deathrest, ours or vanilla's).</summary>
        public static bool IsSkeletonDeathrester(Pawn pawn) => SkeletonUtility.HasFrame(pawn) && pawn.CanDeathrest();

        public static CompRimshi_DeathrestFuel FuelOf(Building_Bed bed) => bed?.TryGetComp<CompRimshi_DeathrestFuel>();

        /// <summary>
        /// A repair bed the skeleton can start deathresting in now: ours, fuelled, roofed (vanilla wakes deathresters in
        /// sunlight), free, not forbidden, reachable and reservable.
        /// </summary>
        public static bool CanDeathrestIn(Pawn pawn, Building_Bed bed)
        {
            CompRimshi_DeathrestFuel fuel = FuelOf(bed);
            if (fuel == null || !fuel.HasFuel || !bed.Spawned || bed.Map != pawn.Map || bed.IsForbidden(pawn) || bed.IsBurning())
            {
                return false;
            }
            if (bed.Faction != pawn.Faction && (pawn.HostFaction == null || bed.Faction != pawn.HostFaction))
            {
                return false;
            }
            if (bed.ForPrisoners != pawn.IsPrisoner || bed.ForSlaves != pawn.IsSlave)
            {
                return false;
            }
            if (!bed.CurOccupants.Contains(pawn) && !bed.AnyUnoccupiedSleepingSlot)
            {
                return false;
            }
            if (bed.OccupiedRect().Any(c => c.GetRoof(bed.Map) == null))
            {
                return false;
            }
            return pawn.CanReach(bed, PathEndMode.OnCell, Danger.Some) && (pawn.HasReserved(bed) || pawn.CanReserve(bed, bed.SleepingSlotsCount));
        }

        public static Building_Bed BestBedFor(Pawn pawn)
        {
            if (!pawn.Spawned)
            {
                return null;
            }
            if (pawn.CurrentBed() is Building_Bed current && CanDeathrestIn(pawn, current))
            {
                return current;
            }
            return pawn.Map.listerBuildings.allBuildingsColonist.OfType<Building_Bed>()
                .Where(b => CanDeathrestIn(pawn, b))
                .OrderBy(b => b.Position.DistanceToSquared(pawn.Position))
                .FirstOrDefault();
        }

        /// <summary>Skeletons saved before the deathrest gene existed get it when the save loads, if their xenotype has it.</summary>
        public static void AddMissingGenes()
        {
            int added = 0;
            foreach (Pawn pawn in PawnsFinder.AllMapsWorldAndTemporary_AliveOrDead)
            {
                XenotypeDef xenotype = pawn?.genes?.Xenotype;
                if (xenotype?.genes == null || !xenotype.genes.Contains(Gene) || pawn.genes.HasActiveGene(Gene) || pawn.genes.GetGene(Gene) != null)
                {
                    continue;
                }
                pawn.genes.AddGene(Gene, !xenotype.inheritable);
                added++;
            }
            if (added > 0)
            {
                RimshiLog.Test("deathrest", "gene added", added + " skeletons from an older save got the deathrest gene");
            }
        }
    }

    /// <summary>
    /// Skeletons deathrest only in a fuelled repair bed: the vanilla job giver's bed (or bare ground) is swapped for the
    /// nearest one, and with none there is no deathrest job, so the need runs down to deathrest exhaustion.
    /// </summary>
    [HarmonyPatch(typeof(JobGiver_GetDeathrest), "TryGiveJob")]
    public static class Patch_DeathrestInRepairBed
    {
        private static readonly HashSet<int> loggedNoBed = new HashSet<int>();

        public static bool Prepare() => PatchTarget.Exists(typeof(JobGiver_GetDeathrest), "TryGiveJob");

        public static void Postfix(Pawn pawn, ref Job __result)
        {
            if (__result == null || !SkeletonDeathrest.IsSkeletonDeathrester(pawn))
            {
                return;
            }
            Building_Bed bed = SkeletonDeathrest.BestBedFor(pawn);
            if (bed == null)
            {
                __result = null;
                if (loggedNoBed.Add(pawn.thingIDNumber))
                {
                    RimshiLog.Test("deathrest", "no bed", pawn.LabelShort + " needs to deathrest but no roofed, fuelled repair bed is free");
                }
                return;
            }
            loggedNoBed.Remove(pawn.thingIDNumber);
            if (__result.targetA.Thing != bed)
            {
                __result = JobMaker.MakeJob(JobDefOf.Deathrest, bed);
            }
            RimshiLog.Test("deathrest", "start", pawn.LabelShort + " going to deathrest in " + bed.LabelShort + " at " + bed.Position + ", chemfuel " + SkeletonDeathrest.FuelOf(bed).Refuelable.Fuel.ToString("0.#"));
        }
    }

    /// <summary>
    /// The repair bed is a medical bed (so injured skeletons find it), and vanilla only lets a pawn use a medical bed while
    /// it should seek medical rest. A skeleton on its way to deathrest or deathresting counts, so the bed stays usable.
    /// </summary>
    [HarmonyPatch(typeof(HealthAIUtility), nameof(HealthAIUtility.ShouldSeekMedicalRest))]
    public static class Patch_DeathrestMedicalBed
    {
        [HarmonyPriority(Priority.Low)]
        public static void Postfix(Pawn pawn, ref bool __result)
        {
            if (!__result && (pawn.Deathresting || pawn.CurJobDef == JobDefOf.Deathrest) && SkeletonDeathrest.IsSkeletonDeathrester(pawn))
            {
                __result = true;
            }
        }
    }

    /// <summary>Deathrest burns the bed's chemfuel, and pauses for a skeleton out of a fuelled repair bed.</summary>
    [HarmonyPatch(typeof(Gene_Deathrest), nameof(Gene_Deathrest.TickDeathresting))]
    public static class Patch_DeathrestFuel
    {
        private static readonly HashSet<int> loggedBurn = new HashSet<int>();
        private static readonly HashSet<int> loggedPause = new HashSet<int>();

        public static void Prefix(Gene_Deathrest __instance, ref bool paused, int delta)
        {
            Pawn pawn = __instance.pawn;
            if (paused || !SkeletonUtility.HasFrame(pawn))
            {
                return;
            }
            CompRimshi_DeathrestFuel fuel = SkeletonDeathrest.FuelOf(pawn.CurrentBed());
            if (fuel != null && fuel.HasFuel)
            {
                fuel.Burn(__instance, delta);
                loggedPause.Remove(pawn.thingIDNumber);
                if (loggedBurn.Add(pawn.thingIDNumber))
                {
                    RimshiLog.Test("deathrest", "fuel burned", pawn.LabelShort + " deathresting in " + fuel.parent.LabelShort + ", " + fuel.Props.fuelPerDeathrest
                        + " chemfuel over " + __instance.MinDeathrestTicks.ToStringTicksToPeriod() + ", " + fuel.Refuelable.Fuel.ToString("0.#") + " left");
                }
                return;
            }
            paused = true;
            loggedBurn.Remove(pawn.thingIDNumber);
            if (loggedPause.Add(pawn.thingIDNumber))
            {
                RimshiLog.Test("deathrest", "paused", pawn.LabelShort + ": " + (fuel == null ? "not in a repair bed" : fuel.parent.LabelShort + " is out of chemfuel") + ", deathrest paused");
                if (PawnUtility.ShouldSendNotificationAbout(pawn))
                {
                    Messages.Message(pawn.LabelShort + "'s deathrest is paused: " + (fuel == null ? "skeletons can only deathrest in a skeleton repair bed." : "the repair bed is out of chemfuel."),
                        pawn, MessageTypeDefOf.NegativeEvent);
                }
            }
        }
    }
}
