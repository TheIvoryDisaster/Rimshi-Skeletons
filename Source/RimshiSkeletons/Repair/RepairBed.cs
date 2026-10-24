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

        public CompProperties_Rimshi_RepairBed()
        {
            compClass = typeof(CompRimshi_RepairBed);
        }
    }

    /// <summary>
    /// While powered, repairs every skeleton lying in the bed, one injury at a time, worst first (the same idea as
    /// Biotech's MechRepairUtility). Lost parts are not regrown. Runs on the rare tick (every 250 ticks).
    /// </summary>
    public class CompRimshi_RepairBed : ThingComp
    {
        private const int Interval = 250;
        private readonly HashSet<int> loggedRepair = new HashSet<int>();

        public CompProperties_Rimshi_RepairBed Props => (CompProperties_Rimshi_RepairBed)props;

        private bool Powered => parent.GetComp<CompPowerTrader>()?.PowerOn ?? true;

        private Building_Bed Bed => parent as Building_Bed;

        public override void CompTickRare()
        {
            if (!Powered || Bed == null)
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
                return "Unpowered: not repairing.";
            }
            List<string> repairing = Bed?.CurOccupants.Where(p => SkeletonUtility.HasFrame(p) && WorstInjury(p) != null).Select(p => p.LabelShort).ToList();
            return repairing != null && repairing.Count > 0 ? "Repairing: " + string.Join(", ", repairing) : null;
        }
    }

    /// <summary>Only skeletons can use a repair bed, the way vanilla keeps everyone but deathresters out of the deathrest casket.</summary>
    [HarmonyPatch(typeof(RestUtility), nameof(RestUtility.CanUseBedEver))]
    public static class Patch_RepairBedOnlySkeletons
    {
        private static readonly HashSet<int> logged = new HashSet<int>();

        public static void Postfix(Pawn p, ThingDef bedDef, ref bool __result)
        {
            if (__result && bedDef.HasComp(typeof(CompRimshi_RepairBed)) && !SkeletonUtility.HasFrame(p))
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
