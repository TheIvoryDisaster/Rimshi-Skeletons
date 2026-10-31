using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimshiSkeletons
{
    /// <summary>
    /// A building linked to repair beds (through CompAffectedByFacilities) that changes a soft cycle in the bed
    /// (docs/design/soft-cycle-facilities.md). Like vanilla's deathrest buildings it must stand in the same room as the bed
    /// with a line of sight to it; the facility's own maxSimultaneous caps how many link to one bed.
    /// </summary>
    public class CompProperties_Rimshi_SoftCycleFacility : CompProperties
    {
        /// <summary>Multiplies the bed's cycle power draw.</summary>
        public float cyclePowerFactor = 1f;

        /// <summary>Added to the cycle's length factor while engaged: -0.15 makes the cycle 15% shorter.</summary>
        public float cycleTimeOffset;

        /// <summary>Fuel (from its CompRefuelable) burnt each time it joins a skeleton's cycle.</summary>
        public float fuelPerCycle;

        public CompProperties_Rimshi_SoftCycleFacility()
        {
            compClass = typeof(CompRimshi_SoftCycleFacility);
        }
    }

    /// <summary>
    /// Joins the soft cycle of a skeleton in a linked repair bed: on the cycle's first powered tick (or as soon as it is
    /// fuelled and linked mid-cycle) it burns fuelPerCycle and stays engaged with that skeleton until the cycle ends,
    /// finished or cut short. One skeleton at a time.
    /// </summary>
    public class CompRimshi_SoftCycleFacility : ThingComp
    {
        private Pawn engaged;

        public CompProperties_Rimshi_SoftCycleFacility Props => (CompProperties_Rimshi_SoftCycleFacility)props;

        public CompRefuelable Refuelable => parent.GetComp<CompRefuelable>();

        public Pawn Engaged => engaged;

        public bool Fuelled => Props.fuelPerCycle <= 0f || (Refuelable?.Fuel ?? 0f) >= Props.fuelPerCycle - 0.001f;

        /// <summary>Linked to the bed and in its room with a line of sight, checked again at use because rooms change.</summary>
        public bool Serves(Building_Bed bed)
        {
            if (bed == null || !parent.Spawned || !bed.Spawned || bed.Map != parent.Map)
            {
                return false;
            }
            List<Thing> linked = bed.TryGetComp<CompAffectedByFacilities>()?.LinkedFacilitiesListForReading;
            return linked != null && linked.Contains(parent) && SoftCycleFacilities.SameRoom(parent.Position, bed.Position, parent.Map);
        }

        /// <summary>True while the engaged skeleton is still soft cycling in a bed this facility serves.</summary>
        private bool StillCycling(Pawn pawn) => pawn != null && !pawn.Dead && pawn.Deathresting && Serves(pawn.CurrentBed());

        /// <summary>Engages with the skeleton's cycle if it isn't already, burning the fuel. False if it can't.</summary>
        public bool TryEngage(Pawn pawn, Building_Bed bed)
        {
            if (engaged == pawn)
            {
                return true;
            }
            if (engaged != null && StillCycling(engaged))
            {
                return false;
            }
            if (!Serves(bed) || !Fuelled)
            {
                return false;
            }
            if (Props.fuelPerCycle > 0f)
            {
                Refuelable.ConsumeFuel(Props.fuelPerCycle);
            }
            engaged = pawn;
            RimshiLog.Test("facility", "engaged", parent.LabelShort + " joined " + pawn.LabelShort + "'s soft cycle in " + bed.LabelShort + ", burnt " + Props.fuelPerCycle
                + " fuel, cycle length x" + SoftCycleFacilities.TimeFactor(SoftCycle.RepairBedOf(bed), pawn, readyOnly: false).ToString("0.00"));
            return true;
        }

        public void Release(Pawn pawn, string why)
        {
            if (engaged != null && engaged == pawn)
            {
                RimshiLog.Test("facility", "released", parent.LabelShort + " released " + pawn.LabelShort + ": " + why);
                engaged = null;
            }
        }

        public override void CompTickRare()
        {
            if (engaged != null && !StillCycling(engaged))
            {
                Release(engaged, "no longer soft cycling in a linked bed");
            }
        }

        public override void PostExposeData()
        {
            Scribe_References.Look(ref engaged, "rimshiEngaged");
        }

        public override string CompInspectStringExtra()
        {
            return engaged != null ? "Ringing with " + engaged.LabelShort + "'s soft cycle." : null;
        }
    }

    public static class SoftCycleFacilities
    {
        /// <summary>The shortest a cycle can get, however many facilities are engaged.</summary>
        public const float MinTimeFactor = 0.25f;

        public static bool SameRoom(IntVec3 a, IntVec3 b, Map map)
        {
            if (map == null)
            {
                return true;
            }
            Room roomA = a.GetRoom(map);
            return roomA != null && roomA == b.GetRoom(map);
        }

        public static IEnumerable<CompRimshi_SoftCycleFacility> Linked(CompRimshi_RepairBed bed)
        {
            List<Thing> linked = bed?.parent.TryGetComp<CompAffectedByFacilities>()?.LinkedFacilitiesListForReading;
            if (linked == null)
            {
                yield break;
            }
            foreach (Thing thing in linked)
            {
                CompRimshi_SoftCycleFacility comp = thing.TryGetComp<CompRimshi_SoftCycleFacility>();
                if (comp != null)
                {
                    yield return comp;
                }
            }
        }

        /// <summary>
        /// The cycle's length factor in this bed for this skeleton: 1 plus the time offsets of the facilities engaged with
        /// it, or with readyOnly, of those that would engage if it started now (fuelled and free, or already engaged).
        /// </summary>
        public static float TimeFactor(CompRimshi_RepairBed bed, Pawn pawn, bool readyOnly)
        {
            float factor = 1f;
            Building_Bed building = bed?.parent as Building_Bed;
            foreach (CompRimshi_SoftCycleFacility facility in Linked(bed))
            {
                bool counts = facility.Engaged == pawn || (readyOnly && facility.Engaged == null && facility.Fuelled);
                if (counts && facility.Serves(building))
                {
                    factor += facility.Props.cycleTimeOffset;
                }
            }
            return Mathf.Max(MinTimeFactor, factor);
        }

        /// <summary>Engages every linked facility that can join the skeleton's cycle; returns the cycle's length factor.</summary>
        public static float Engage(CompRimshi_RepairBed bed, Pawn pawn)
        {
            Building_Bed building = bed.parent as Building_Bed;
            foreach (CompRimshi_SoftCycleFacility facility in Linked(bed))
            {
                facility.TryEngage(pawn, building);
            }
            return TimeFactor(bed, pawn, readyOnly: false);
        }

        /// <summary>The cycle ended: every facility on the map engaged with the skeleton lets go.</summary>
        public static void ReleaseAll(Pawn pawn, string why)
        {
            Map map = pawn?.MapHeld;
            if (map == null)
            {
                return;
            }
            foreach (Building building in map.listerBuildings.allBuildingsColonist)
            {
                building.TryGetComp<CompRimshi_SoftCycleFacility>()?.Release(pawn, why);
            }
        }
    }

    /// <summary>
    /// Soft cycle facilities link only to beds in their own room, like vanilla's deathrest buildings (which bind to a
    /// deathrester in the same room with a line of sight, at any distance). The line of sight is vanilla's requiresLOS.
    /// </summary>
    [HarmonyPatch(typeof(CompAffectedByFacilities), nameof(CompAffectedByFacilities.CanPotentiallyLinkTo_Static),
        new[] { typeof(ThingDef), typeof(IntVec3), typeof(Rot4), typeof(ThingDef), typeof(IntVec3), typeof(Rot4), typeof(Map) })]
    public static class Patch_SoftCycleFacilitySameRoom
    {
        public static void Postfix(ThingDef facilityDef, IntVec3 facilityPos, ThingDef myDef, IntVec3 myPos, Map myMap, ref bool __result)
        {
            if (__result && facilityDef.HasComp(typeof(CompRimshi_SoftCycleFacility)) && !SoftCycleFacilities.SameRoom(facilityPos, myPos, myMap))
            {
                __result = false;
            }
        }
    }
}
