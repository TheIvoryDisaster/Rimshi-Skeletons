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
    /// Skeleton repair, modelled on Biotech's mech repair (WorkGiver_RepairMech, JobDriver_RepairMech, which only take
    /// mechanitors and mechs). Skeletons have InjuryHealingFactor 0 from the frame gene, so they never heal on their own;
    /// a crafter carrying repair kits works on one and its worst injury mends every second while the work goes on, using
    /// up a kit per KitSeverity healed. Scars are left to the repair bed. Medicine never works on a skeleton: nothing on
    /// one is tendable (Patch_SkeletonNoTending), so doctors, self-tend and drafted tend all leave skeletons alone and
    /// the repair job, with kits, is the only treatment.
    /// </summary>
    public static class SkeletonRepair
    {
        public const int Interval = 60;

        /// <summary>Injury severity repaired per kit.</summary>
        public const float KitSeverity = 20f;

        /// <summary>Kit use, in severity, of treating one tendable condition (a leak, or a mod's tend-only hediff).</summary>
        public const float TreatCost = 5f;

        /// <summary>Severity repaired per second of work: base plus per Crafting level (1.0 at Crafting 10).</summary>
        public const float RepairBase = 0.4f;
        public const float RepairPerCrafting = 0.06f;

        public static float RepairPerInterval(Pawn repairer)
        {
            int crafting = repairer.skills?.GetSkill(SkillDefOf.Crafting)?.Level ?? 0;
            return (RepairBase + RepairPerCrafting * crafting) * repairer.GetStatValue(StatDefOf.GeneralLaborSpeed);
        }

        public static Hediff_Injury WorstFreshInjury(Pawn pawn)
        {
            var injuries = new List<Hediff_Injury>();
            pawn.health.hediffSet.GetHediffs(ref injuries, i => !i.IsPermanent());
            return injuries.OrderByDescending(i => i.Severity).FirstOrDefault();
        }

        public static float FreshSeverity(Pawn pawn)
        {
            var injuries = new List<Hediff_Injury>();
            pawn.health.hediffSet.GetHediffs(ref injuries, i => !i.IsPermanent());
            return injuries.Sum(i => i.Severity);
        }

        public static bool HasScars(Pawn pawn)
        {
            var injuries = new List<Hediff_Injury>();
            pawn.health.hediffSet.GetHediffs(ref injuries, i => i.IsPermanent());
            return injuries.Count > 0;
        }

        /// <summary>Repair quality for treating tendable conditions: the same curve as the repair speed, capped at 1.</summary>
        public static float TreatQuality(Pawn repairer) => Mathf.Clamp01(RepairPerInterval(repairer));

        /// <summary>
        /// What vanilla would tend on a skeleton if medicine worked on it: bleeding injuries and tendable non-injury
        /// conditions, the same tests as Hediff.TendableNow apart from the skeleton block.
        /// </summary>
        public static List<Hediff> NeedingTreatment(Pawn pawn)
        {
            return pawn.health.hediffSet.hediffs.FindAll(h => h.def.tendable && h.Severity > 0f && h.Visible && !h.IsPermanent() && !h.FullyImmune()
                && (!(h is Hediff_Injury) || h.BleedRate > 0f) && (h.TryGetComp<HediffComp_TendDuration>()?.AllowTend ?? true));
        }

        public static bool NeedsRepair(Pawn pawn) => SkeletonUtility.HasFrame(pawn) && (WorstFreshInjury(pawn) != null || NeedingTreatment(pawn).Count > 0);

        public static int KitsFor(Pawn patient) => Mathf.Max(1, Mathf.CeilToInt((FreshSeverity(patient) + NeedingTreatment(patient).Count * TreatCost) / KitSeverity));

        /// <summary>A powered repair bed on the pawn's map that it could lie in now (or is lying in).</summary>
        public static bool RepairBedAvailable(Pawn pawn)
        {
            if (!pawn.Spawned)
            {
                return false;
            }
            foreach (Building building in pawn.Map.listerBuildings.allBuildingsColonist)
            {
                if (building is Building_Bed bed && bed.TryGetComp<CompRimshi_RepairBed>() is CompRimshi_RepairBed comp && comp.Powered
                    && (bed.CurOccupants.Contains(pawn) || (bed.AnyUnoccupiedSleepingSlot && (bed.Medical || bed.OwnersForReading.Contains(pawn) || bed.OwnersForReading.Count == 0)))
                    && !bed.IsForbidden(pawn) && pawn.CanReach(bed, PathEndMode.OnCell, Danger.Some))
                {
                    return true;
                }
            }
            return false;
        }
    }

    public class WorkGiver_Rimshi_RepairSkeleton : WorkGiver_Scanner
    {
        public override PathEndMode PathEndMode => PathEndMode.Touch;

        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn) => pawn.Map.mapPawns.AllPawnsSpawned.Where(SkeletonRepair.NeedsRepair);

        public override bool ShouldSkip(Pawn pawn, bool forced = false) => !pawn.Map.mapPawns.AllPawnsSpawned.Any(SkeletonRepair.NeedsRepair);

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            if (!(t is Pawn patient) || patient == pawn || !SkeletonRepair.NeedsRepair(patient))
            {
                return false;
            }
            if (patient.Faction != pawn.Faction && !(patient.IsPrisonerOfColony || patient.IsSlaveOfColony))
            {
                return false;
            }
            if (patient.InAggroMentalState || patient.IsForbidden(pawn) || !pawn.CanReserve(patient, 1, -1, null, forced))
            {
                return false;
            }
            if (FindKit(pawn) == null)
            {
                JobFailReason.Is("No skeleton repair kits");
                return false;
            }
            return true;
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            Thing kit = FindKit(pawn);
            if (kit == null)
            {
                return null;
            }
            Job job = JobMaker.MakeJob(RimshiDefOf.Rimshi_RepairSkeleton, t, kit);
            job.count = Mathf.Min(SkeletonRepair.KitsFor((Pawn)t), kit.stackCount);
            return job;
        }

        private static Thing FindKit(Pawn pawn)
        {
            return GenClosest.ClosestThingReachable(pawn.Position, pawn.Map, ThingRequest.ForDef(RimshiDefOf.Rimshi_RepairKit), PathEndMode.ClosestTouch,
                TraverseParms.For(pawn), 9999f, k => !k.IsForbidden(pawn) && pawn.CanReserve(k));
        }
    }

    /// <summary>Fetch kits (B), go to the skeleton (A), and repair it while it waits, as the mechanitor does with a mech.</summary>
    public class JobDriver_Rimshi_RepairSkeleton : JobDriver
    {
        private float healedSinceKit;
        private int ticks;

        public const int SessionTicks = 2500;

        private Pawn Patient => (Pawn)job.GetTarget(TargetIndex.A).Thing;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref healedSinceKit, "healedSinceKit");
            Scribe_Values.Look(ref ticks, "ticks");
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(Patient, job, 1, -1, null, errorOnFailed) && pawn.Reserve(job.GetTarget(TargetIndex.B), job, 1, job.count, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            this.FailOn(() => Patient.InAggroMentalState);
            AddFinishAction(condition =>
            {
                Thing carried = pawn.carryTracker?.CarriedThing;
                if (carried != null && carried.def == RimshiDefOf.Rimshi_RepairKit)
                {
                    pawn.carryTracker.TryDropCarriedThing(pawn.Position, ThingPlaceMode.Near, out _);
                }
                Pawn patient = Patient;
                if (patient?.jobs?.curJob != null && (patient.CurJobDef == JobDefOf.Wait || patient.CurJobDef == JobDefOf.Wait_MaintainPosture)
                    && patient.CurJob.targetA.Thing == pawn)
                {
                    patient.jobs.EndCurrentJob(JobCondition.InterruptForced);
                }
            });
            yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.ClosestTouch).FailOnDespawnedNullOrForbidden(TargetIndex.B);
            yield return Toils_Haul.StartCarryThing(TargetIndex.B, subtractNumTakenFromJobCount: true);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            // One session; the work giver starts another if there is still damage. The skeleton is made to wait (lying
            // down stays lying down) and is released in the finish action.
            Toil repair = Toils_General.WaitWith(TargetIndex.A, SessionTicks, maintainPosture: true);
            // The mechanitor's repair sparks and sound (JobDriver_RepairMech).
            repair.WithEffect(EffecterDefOf.MechRepairing, TargetIndex.A);
            repair.PlaySustainerOrSound(SoundDefOf.RepairMech_Touch);
            repair.activeSkill = () => SkillDefOf.Crafting;
            repair.tickAction = () =>
            {
                pawn.skills?.Learn(SkillDefOf.Crafting, 0.08f);
                if (++ticks % SkeletonRepair.Interval == 0)
                {
                    RepairStep();
                }
            };
            yield return repair;
        }

        private void RepairStep()
        {
            Thing kits = pawn.carryTracker.CarriedThing;
            if (kits == null || kits.def != RimshiDefOf.Rimshi_RepairKit)
            {
                Finish(!SkeletonRepair.NeedsRepair(Patient));
                return;
            }
            Treat();
            Hediff_Injury injury = SkeletonRepair.WorstFreshInjury(Patient);
            if (injury == null)
            {
                Finish(true);
                return;
            }
            float amount = Mathf.Min(SkeletonRepair.RepairPerInterval(pawn), injury.Severity);
            if (ticks == SkeletonRepair.Interval)
            {
                RimshiLog.Test("repair", "start", pawn.LabelShort + " repairing " + Patient.LabelShort + ": " + injury.LabelCap + " on " + (injury.Part?.Label ?? "body")
                    + ", " + amount.ToString("0.00") + " per second, carrying " + kits.stackCount + " kits");
            }
            injury.Heal(amount);
            healedSinceKit += amount;
            UseKits(kits);
            if (!SkeletonRepair.NeedsRepair(Patient))
            {
                Finish(true);
            }
        }

        /// <summary>Seals leaks and treats tend-only conditions with the kits, which is what tending does for flesh.</summary>
        private void Treat()
        {
            List<Hediff> treat = SkeletonRepair.NeedingTreatment(Patient);
            if (treat.Count == 0)
            {
                return;
            }
            float quality = SkeletonRepair.TreatQuality(pawn);
            for (int i = 0; i < treat.Count; i++)
            {
                treat[i].Tended(quality, 1f, i);
                healedSinceKit += SkeletonRepair.TreatCost;
            }
            RimshiLog.Test("repair", "treated", pawn.LabelShort + " treated " + string.Join(", ", treat.ConvertAll(h => h.LabelCap.ToString())) + " on " + Patient.LabelShort
                + " with repair kits, quality " + quality.ToStringPercent());
            UseKits(pawn.carryTracker.CarriedThing);
        }

        private void UseKits(Thing kits)
        {
            while (healedSinceKit >= SkeletonRepair.KitSeverity && kits != null && !kits.Destroyed)
            {
                healedSinceKit -= SkeletonRepair.KitSeverity;
                kits.SplitOff(1).Destroy();
                RimshiLog.Test("repair", "kit used", pawn.LabelShort + " used a repair kit on " + Patient.LabelShort);
            }
        }

        private void Finish(bool repaired)
        {
            if (repaired)
            {
                // A kit that has been started is used up.
                if (healedSinceKit > 0f && pawn.carryTracker.CarriedThing is Thing kits && kits.def == RimshiDefOf.Rimshi_RepairKit)
                {
                    kits.SplitOff(1).Destroy();
                }
                RimshiLog.Test("repair", "done", pawn.LabelShort + " finished repairing " + Patient.LabelShort);
                if (Patient.Faction == Faction.OfPlayer)
                {
                    Messages.Message(Patient.LabelShort + " is repaired.", Patient, MessageTypeDefOf.PositiveEvent);
                }
            }
            EndJobWith(repaired ? JobCondition.Succeeded : JobCondition.Incompletable);
        }
    }

    /// <summary>
    /// With healing at 0, a tended skeleton would rest in a bed forever waiting to heal. It still goes to bed whenever vanilla
    /// says it urgently should (downed, needing tending to stop bleeding, surgery due); otherwise only when a powered repair bed is free,
    /// for fresh injuries any time and for scars during its Sleep hours.
    /// </summary>
    [HarmonyPatch(typeof(HealthAIUtility), nameof(HealthAIUtility.ShouldSeekMedicalRest))]
    public static class Patch_SkeletonMedicalRest
    {
        public static void Postfix(Pawn pawn, ref bool __result)
        {
            if (!SkeletonUtility.HasFrame(pawn) || HealthAIUtility.ShouldSeekMedicalRestUrgent(pawn))
            {
                return;
            }
            bool fresh = SkeletonRepair.WorstFreshInjury(pawn) != null;
            bool scarsTonight = SkeletonRepair.HasScars(pawn) && pawn.timetable?.CurrentAssignment == TimeAssignmentDefOf.Sleep;
            __result = (fresh || scarsTonight) && SkeletonRepair.RepairBedAvailable(pawn);
        }
    }

    /// <summary>
    /// Medicine does nothing for a skeleton: nothing on one is tendable, so no doctor, self-tend, drafted tend or other
    /// mod's tend AI picks it up with herbal or any other medicine, and it raises no "needs tending" alert. Repair kits,
    /// through the repair job, are the only treatment (it seals leaks and treats tend-only conditions itself). Runs only
    /// when vanilla said yes, so the usual false answers cost nothing.
    /// </summary>
    [HarmonyPatch(typeof(Hediff), nameof(Hediff.TendableNow))]
    public static class Patch_SkeletonNoTending
    {
        public static void Postfix(Hediff __instance, ref bool __result)
        {
            if (__result && __instance.pawn != null && SkeletonUtility.HasFrame(__instance.pawn))
            {
                __result = false;
            }
        }
    }
}
