using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace RimshiSkeletons
{
    public class CompProperties_Rimshi_RepairBed : CompProperties
    {
        /// <summary>Injury severity repaired per day, scaled by the pawn's health scale like vanilla healing.</summary>
        public float repairPerDay = 40f;

        /// <summary>Once fresh injuries are gone, permanent ones (scars, old wounds) are repaired too.</summary>
        public bool repairsScars = true;

        /// <summary>Power drawn with no skeleton being repaired or cycling.</summary>
        public float standbyWatts = 50f;

        /// <summary>Power drawn while repairing at least one skeleton.</summary>
        public float repairWatts = 400f;

        /// <summary>Extra power drawn while a skeleton soft cycles, per level of all its skills added up.</summary>
        public float cycleWattsPerSkillLevel = 15f;

        public CompProperties_Rimshi_RepairBed()
        {
            compClass = typeof(CompRimshi_RepairBed);
        }
    }

    /// <summary>
    /// A building linked to repair beds (through CompAffectedByFacilities) that changes the soft cycle's power draw.
    /// Nothing uses it yet; it is the hook for the soft cycle buildings to come.
    /// </summary>
    public class CompProperties_Rimshi_SoftCycleFacility : CompProperties
    {
        public float cyclePowerFactor = 1f;

        public CompProperties_Rimshi_SoftCycleFacility()
        {
            compClass = typeof(ThingComp);
        }
    }

    /// <summary>
    /// While powered, repairs every skeleton lying in the bed, one injury at a time, worst first (the same idea as
    /// Biotech's MechRepairUtility). Lost parts are not regrown. Runs on the rare tick (every 250 ticks), and sets the
    /// bed's power draw: standby, more while repairing, and a skill-scaled draw for each skeleton soft cycling in it.
    /// </summary>
    public class CompRimshi_RepairBed : ThingComp
    {
        private const int Interval = 250;
        private readonly HashSet<int> loggedRepair = new HashSet<int>();

        public CompProperties_Rimshi_RepairBed Props => (CompProperties_Rimshi_RepairBed)props;

        public CompPowerTrader PowerTrader => parent.GetComp<CompPowerTrader>();

        public bool Powered => PowerTrader?.PowerOn ?? true;

        private Building_Bed Bed => parent as Building_Bed;

        /// <summary>Product of the cycle power factors of linked soft cycle facilities.</summary>
        public float FacilityFactor
        {
            get
            {
                float factor = 1f;
                List<Thing> linked = parent.TryGetComp<CompAffectedByFacilities>()?.LinkedFacilitiesListForReading;
                if (linked != null)
                {
                    foreach (Thing facility in linked)
                    {
                        CompProperties_Rimshi_SoftCycleFacility props = facility.def.GetCompProperties<CompProperties_Rimshi_SoftCycleFacility>();
                        if (props != null)
                        {
                            factor *= props.cyclePowerFactor;
                        }
                    }
                }
                return factor;
            }
        }

        /// <summary>Extra watts the bed draws while this skeleton soft cycles in it.</summary>
        public float CycleWatts(Pawn pawn) => SoftCycle.SkillLevels(pawn) * Props.cycleWattsPerSkillLevel * FacilityFactor;

        /// <summary>
        /// The bed's grid can carry the skeleton's cycle: its spare generation covers the extra draw, or its batteries hold
        /// enough to make up the shortfall for the full 48 hours.
        /// </summary>
        public bool CanPowerCycle(Pawn pawn)
        {
            CompPowerTrader trader = PowerTrader;
            if (trader == null)
            {
                return true;
            }
            PowerNet net = trader.PowerNet;
            if (net == null)
            {
                return false;
            }
            float extra = CycleWatts(pawn);
            if (pawn.Deathresting && Bed != null && Bed.CurOccupants.Contains(pawn))
            {
                extra = 0f;
            }
            float spare = net.CurrentEnergyGainRate() / CompPower.WattsToWattDaysPerTick;
            if (spare >= extra)
            {
                return true;
            }
            return net.CurrentStoredEnergy() >= (extra - spare) * SoftCycle.BedCycleTicks / GenDate.TicksPerDay;
        }

        public override void CompTickRare()
        {
            if (Bed == null)
            {
                return;
            }
            UpdatePowerDraw();
            if (!Powered)
            {
                return;
            }
            foreach (Pawn pawn in Bed.CurOccupants.ToList())
            {
                if (SkeletonUtility.HasFrame(pawn))
                {
                    Repair(pawn);
                }
            }
        }

        private void UpdatePowerDraw()
        {
            CompPowerTrader trader = PowerTrader;
            if (trader == null)
            {
                return;
            }
            float watts = Props.standbyWatts;
            bool repairing = false;
            foreach (Pawn pawn in Bed.CurOccupants)
            {
                if (!SkeletonUtility.HasFrame(pawn))
                {
                    continue;
                }
                repairing |= WorstInjury(pawn) != null;
                if (pawn.Deathresting)
                {
                    watts += CycleWatts(pawn);
                }
            }
            if (repairing)
            {
                watts += Props.repairWatts;
            }
            trader.PowerOutput = -watts;
        }

        private Hediff_Injury WorstInjury(Pawn pawn)
        {
            var injuries = new List<Hediff_Injury>();
            pawn.health.hediffSet.GetHediffs(ref injuries);
            Hediff_Injury fresh = injuries.Where(i => !i.IsPermanent()).OrderByDescending(i => i.Severity).FirstOrDefault();
            if (fresh != null || !Props.repairsScars)
            {
                return fresh;
            }
            return injuries.OrderByDescending(i => i.Severity).FirstOrDefault();
        }

        private void Repair(Pawn pawn)
        {
            Hediff_Injury injury = WorstInjury(pawn);
            if (injury == null)
            {
                return;
            }
            float amount = Props.repairPerDay * Interval / GenDate.TicksPerDay * pawn.HealthScale;
            string label = injury.LabelCap + " on " + (injury.Part?.Label ?? "body");
            injury.Heal(amount);
            if (loggedRepair.Add(pawn.thingIDNumber))
            {
                RimshiLog.Test("bed", "repair", pawn.LabelShort + ": " + label + " -" + amount.ToString("0.000") + " per rare tick");
            }
            if (WorstInjury(pawn) == null)
            {
                loggedRepair.Remove(pawn.thingIDNumber);
                RimshiLog.Test("bed", "done", pawn.LabelShort + " fully repaired in " + parent.LabelShort);
                if (pawn.Faction == Faction.OfPlayer)
                {
                    Messages.Message(pawn.LabelShort + " is fully repaired.", pawn, MessageTypeDefOf.PositiveEvent);
                }
            }
        }

        public override string CompInspectStringExtra()
        {
            if (!Powered)
            {
                return "Unpowered: not repairing or soft cycling.";
            }
            var lines = new List<string>();
            List<string> repairing = Bed?.CurOccupants.Where(p => SkeletonUtility.HasFrame(p) && WorstInjury(p) != null).Select(p => p.LabelShort).ToList();
            if (repairing != null && repairing.Count > 0)
            {
                lines.Add("Repairing: " + string.Join(", ", repairing));
            }
            foreach (Pawn pawn in Bed?.CurOccupants ?? Enumerable.Empty<Pawn>())
            {
                Gene_Deathrest gene = pawn.Deathresting ? SoftCycle.GeneOf(pawn) : null;
                if (gene != null && SkeletonUtility.HasFrame(pawn))
                {
                    lines.Add("Soft cycling: " + pawn.LabelShort + " " + gene.DeathrestPercent.ToStringPercent("F0") + ", " + CycleWatts(pawn).ToString("F0") + " W");
                }
            }
            return lines.Count > 0 ? string.Join("\n", lines) : null;
        }
    }

    /// <summary>Only skeletons can use a repair bed or rib cage, the way vanilla keeps everyone but deathresters out of the deathrest casket.</summary>
    [HarmonyPatch(typeof(RestUtility), nameof(RestUtility.CanUseBedEver))]
    public static class Patch_RepairBedOnlySkeletons
    {
        private static readonly HashSet<int> logged = new HashSet<int>();

        public static void Postfix(Pawn p, ThingDef bedDef, ref bool __result)
        {
            if (__result && (bedDef.HasComp(typeof(CompRimshi_RepairBed)) || RibCage.IsRibCage(bedDef)) && !SkeletonUtility.HasFrame(p))
            {
                __result = false;
                if (logged.Count < 200 && logged.Add(p.thingIDNumber))
                {
                    RimshiLog.Test("bed", "refused", p.LabelShort + " is not a skeleton");
                }
            }
        }
    }
}
