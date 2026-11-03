using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace RimshiSkeletons
{
    /// <summary>
    /// Soft cycle mending (Matthew, 2026-11-03; docs/design/cpu-soft-cycle.md): while a skeleton soft cycles, in a repair
    /// bed or without one, its injuries mend just enough that it can get up when the cycle ends, no more. "Get up" means
    /// out of pain shock and able to move. Leg injuries mend first, then the pelvis and spine, then whatever hurts most.
    /// The pace is spread over the rest of the cycle, so the last of it lands as the cycle finishes. A skeleton missing
    /// both legs gets none of it.
    ///
    /// The amount needed is found by trying it: injury severities are lowered in place (the raw field, so nothing is
    /// notified), the health caches are dirtied and the skeleton's pain and moving are read, then everything is put back.
    /// States the cycle itself causes or ends (deathrest, the soft reset, CPU overload, being powered down) are taken out
    /// of the list for the trial, so they never count against getting up.
    /// </summary>
    public static class SoftCycleMending
    {
        /// <summary>Ticks between mending steps while a cycle runs.</summary>
        public const int Interval = 250;

        private const int SearchSteps = 12;

        private static readonly AccessTools.FieldRef<Hediff, float> SeverityField = AccessTools.FieldRefAccess<Hediff, float>("severityInt");

        private static readonly HashSet<int> loggedMending = new HashSet<int>();
        private static readonly HashSet<int> loggedSkipped = new HashSet<int>();

        /// <summary>Every leg the body has is gone (prosthetic or skeleton limbs count as legs).</summary>
        public static bool HasNoLegs(Pawn pawn)
        {
            List<BodyPartRecord> legs = pawn?.RaceProps?.body?.GetPartsWithTag(BodyPartTagDefOf.MovingLimbCore);
            return legs != null && legs.Count > 0 && legs.All(leg => pawn.health.hediffSet.PartIsMissing(leg));
        }

        private static bool HasTagUp(BodyPartRecord part, params BodyPartTagDef[] tags)
        {
            for (BodyPartRecord p = part; p != null; p = p.parent)
            {
                if (p.def.tags != null && tags.Any(t => t != null && p.def.tags.Contains(t)))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>0 for a leg (or a part of one), 1 for the pelvis or spine, 2 for anything else.</summary>
        public static int Group(BodyPartRecord part)
        {
            if (HasTagUp(part, BodyPartTagDefOf.MovingLimbCore, BodyPartTagDefOf.MovingLimbSegment, BodyPartTagDefOf.MovingLimbDigit))
            {
                return 0;
            }
            if (part != null && part.def.tags != null && (part.def.tags.Contains(BodyPartTagDefOf.Pelvis) || part.def.tags.Contains(BodyPartTagDefOf.Spine)))
            {
                return 1;
            }
            return 2;
        }

        /// <summary>The skeleton's injuries in mending order: legs, then pelvis and spine, then the rest; fresh before old; most painful first.</summary>
        public static List<Hediff_Injury> Order(Pawn pawn)
        {
            var injuries = new List<Hediff_Injury>();
            pawn.health.hediffSet.GetHediffs(ref injuries);
            return injuries.Where(i => i.Severity > 0f)
                .OrderBy(i => Group(i.Part))
                .ThenBy(i => i.IsPermanent() ? 1 : 0)
                .ThenByDescending(i => i.PainOffset)
                .ThenByDescending(i => i.Severity)
                .ToList();
        }

        private static bool LiftedByCycle(HediffDef def)
        {
            return def == HediffDefOf.Deathrest || def == HediffDefOf.InterruptedDeathrest || def == HediffDefOf.Anesthetic
                || def == RimshiDefOf.Rimshi_SoftResetting || def == RimshiDefOf.Rimshi_CpuOverload || def == RimshiDefOf.Rimshi_MaintenanceMode;
        }

        private static bool CanGetUp(Pawn pawn) => !pawn.health.InPainShock && pawn.health.capacities.CapableOf(PawnCapacityDefOf.Moving);

        private static void Refresh(Pawn pawn)
        {
            pawn.health.hediffSet.DirtyCache();
            pawn.health.capacities.Notify_CapacityLevelsDirty();
        }

        private static void Lower(List<Hediff_Injury> order, float[] saved, float amount)
        {
            for (int i = 0; i < order.Count; i++)
            {
                float take = amount < saved[i] ? amount : saved[i];
                SeverityField(order[i]) = saved[i] - take;
                amount -= take;
            }
        }

        /// <summary>
        /// Injury severity that still has to mend, in this order, before the skeleton could get up once its cycle ends: 0 if
        /// it already could, -1 if mending every injury would still not do it (a missing spine, a ruined CPU).
        /// </summary>
        public static float Needed(Pawn pawn, List<Hediff_Injury> order)
        {
            List<Hediff> hediffs = pawn.health.hediffSet.hediffs;
            var lifted = new List<KeyValuePair<int, Hediff>>();
            float[] saved = order.Select(i => SeverityField(i)).ToArray();
            try
            {
                for (int i = hediffs.Count - 1; i >= 0; i--)
                {
                    if (LiftedByCycle(hediffs[i].def))
                    {
                        lifted.Add(new KeyValuePair<int, Hediff>(i, hediffs[i]));
                        hediffs.RemoveAt(i);
                    }
                }
                Refresh(pawn);
                if (CanGetUp(pawn))
                {
                    return 0f;
                }
                float total = saved.Sum();
                Lower(order, saved, total);
                Refresh(pawn);
                if (!CanGetUp(pawn))
                {
                    return -1f;
                }
                float low = 0f, high = total;
                for (int step = 0; step < SearchSteps; step++)
                {
                    float mid = (low + high) / 2f;
                    Lower(order, saved, mid);
                    Refresh(pawn);
                    if (CanGetUp(pawn))
                    {
                        high = mid;
                    }
                    else
                    {
                        low = mid;
                    }
                }
                return high;
            }
            finally
            {
                for (int i = 0; i < order.Count; i++)
                {
                    SeverityField(order[i]) = saved[i];
                }
                for (int i = lifted.Count - 1; i >= 0; i--)
                {
                    hediffs.Insert(lifted[i].Key, lifted[i].Value);
                }
                Refresh(pawn);
            }
        }

        /// <summary>Mends amount of injury severity, in mending order. Returns what was mended.</summary>
        private static float Mend(List<Hediff_Injury> order, float amount)
        {
            float mended = 0f;
            foreach (Hediff_Injury injury in order)
            {
                if (amount <= 0f)
                {
                    break;
                }
                float take = amount < injury.Severity ? amount : injury.Severity;
                injury.Heal(take);
                amount -= take;
                mended += take;
            }
            return mended;
        }

        /// <summary>
        /// One mending step of a running cycle, ticksLeft before it ends: mends the share of what is still needed that this
        /// step's interval is of the time left (all of it once the cycle is about to end). Returns what was mended.
        /// </summary>
        public static float Step(Pawn pawn, int ticksLeft, int interval = Interval)
        {
            if (pawn?.health == null || pawn.Dead || !SkeletonUtility.HasFrame(pawn))
            {
                return 0f;
            }
            if (HasNoLegs(pawn))
            {
                if (loggedSkipped.Add(pawn.thingIDNumber))
                {
                    RimshiLog.Test("softcycle", "mend skipped", pawn.LabelShort + " has no legs left, the cycle mends nothing");
                }
                return 0f;
            }
            List<Hediff_Injury> order = Order(pawn);
            if (order.Count == 0)
            {
                return 0f;
            }
            float needed = Needed(pawn, order);
            if (needed < 0f)
            {
                if (loggedSkipped.Add(pawn.thingIDNumber))
                {
                    RimshiLog.Test("softcycle", "mend skipped", pawn.LabelShort + " could not get up even fully mended, the cycle mends nothing");
                }
                return 0f;
            }
            if (needed <= 0f)
            {
                return 0f;
            }
            float amount = ticksLeft <= interval ? needed : needed * interval / ticksLeft;
            float mended = Mend(order, amount);
            if (loggedMending.Add(pawn.thingIDNumber))
            {
                RimshiLog.Test("softcycle", "mending", pawn.LabelShort + " needs " + needed.ToString("0.##") + " injury mended to get up, " + ticksLeft.ToStringTicksToPeriod()
                    + " of cycle left, first " + (order[0].Part?.Label ?? "body") + " (" + mended.ToString("0.###") + " this step)");
            }
            return mended;
        }

        /// <summary>A finished cycle: mends whatever is still needed, so the skeleton can get up now.</summary>
        public static void Finish(Pawn pawn)
        {
            float mended = Step(pawn, 0);
            bool wasMending = loggedMending.Remove(pawn.thingIDNumber);
            loggedSkipped.Remove(pawn.thingIDNumber);
            if (wasMending || mended > 0f)
            {
                RimshiLog.Test("softcycle", "mended", pawn.LabelShort + " mended enough to get up as the cycle ended (last " + mended.ToString("0.##") + "), downed " + pawn.Downed);
            }
        }

        /// <summary>A cut-short cycle forgets its mending log, so the next one logs again.</summary>
        public static void Forget(Pawn pawn)
        {
            loggedMending.Remove(pawn.thingIDNumber);
            loggedSkipped.Remove(pawn.thingIDNumber);
        }
    }
}
