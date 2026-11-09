using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RimshiSkeletons
{
    /// <summary>
    /// A skeleton out of maintenance (food need at zero, "starving") wears down: for each full day spent there, one random
    /// part that is not the brain or the waist takes MaintenanceExtension.wearDamage (2) Rimshi_MaintenanceWear damage, a worn part
    /// injury. One hit a day rather than a trickle, so the health tab shows a few clear wear marks instead of many specks.
    /// The clock (Gene_FuelReactor.wearTicks) only runs while the need is not frozen (soft cycle, cryptosleep), like
    /// vanilla malnutrition, and resets once the skeleton is maintained again. Skeleton injuries never heal on their own
    /// and do not bleed; repair kits and the repair bed fix them like any other injury.
    /// </summary>
    public static class SkeletonWear
    {
        private static readonly AccessTools.FieldRef<Need, Pawn> NeedPawn = AccessTools.FieldRefAccess<Need, Pawn>("pawn");
        private static readonly System.Func<Need, bool> IsFrozen = MakeIsFrozen();

        /// <summary>Need.NeedInterval runs every 150 ticks (Need_Food takes 150 ticks of fall each call).</summary>
        public const int IntervalTicks = 150;

        private static System.Func<Need, bool> MakeIsFrozen()
        {
            var getter = AccessTools.PropertyGetter(typeof(Need), "IsFrozen");
            if (getter == null)
            {
                return _ => false;
            }
            return AccessTools.MethodDelegate<System.Func<Need, bool>>(getter);
        }

        private static MaintenanceExtension Settings => RimshiDefOf.Rimshi_FuelReactor.GetModExtension<MaintenanceExtension>();

        public static float WearDamage => Settings?.wearDamage ?? 2f;

        public static int WearIntervalTicks => UnityEngine.Mathf.Max(IntervalTicks, (int)((Settings?.wearIntervalDays ?? 1f) * GenDate.TicksPerDay));

        /// <summary>Called after Need_Food.NeedInterval. Returns the part worn this call, or null.</summary>
        public static BodyPartRecord Update(Need_Food food)
        {
            Pawn pawn = food == null ? null : NeedPawn(food);
            if (pawn == null || pawn.Dead || !(pawn.genes?.GetGene(RimshiDefOf.Rimshi_FuelReactor) is Gene_FuelReactor gene) || !gene.Active)
            {
                return null;
            }
            bool starving = food.Starving;
            return Advance(pawn, gene, starving, starving && IsFrozen(food));
        }

        /// <summary>One need interval of the wear clock. Returns the part worn, or null.</summary>
        public static BodyPartRecord Advance(Pawn pawn, Gene_FuelReactor gene, bool starving, bool frozen)
        {
            if (!starving)
            {
                gene.wearTicks = 0;
                return null;
            }
            if (frozen)
            {
                return null;
            }
            gene.wearTicks += IntervalTicks;
            if (gene.wearTicks < WearIntervalTicks)
            {
                return null;
            }
            gene.wearTicks -= WearIntervalTicks;
            return Wear(pawn);
        }

        /// <summary>
        /// Body parts wear never picks by def: the waist (Matthew, 2026-11-09: a belt slot, not a moving part, and worn
        /// waists kept filling the log).
        /// </summary>
        public static readonly HashSet<string> ExcludedPartDefs = new HashSet<string> { "Waist" };

        /// <summary>
        /// Parts wear can reach: present, not the brain, not the waist, and not inside an added part (a prosthetic's own hand).
        /// </summary>
        public static List<BodyPartRecord> WearableParts(Pawn pawn)
        {
            HediffSet set = pawn.health.hediffSet;
            return set.GetNotMissingParts()
                .Where(p => !p.def.tags.Contains(BodyPartTagDefOf.ConsciousnessSource) && !ExcludedPartDefs.Contains(p.def.defName) && !set.AncestorHasDirectlyAddedParts(p))
                .ToList();
        }

        /// <summary>Wears one random part that is not the brain or the waist. Returns that part, or null when there is none.</summary>
        public static BodyPartRecord Wear(Pawn pawn)
        {
            if (!WearableParts(pawn).TryRandomElement(out BodyPartRecord part))
            {
                RimshiLog.Fail("maintenance", "wear", pawn.LabelShort + ": no part to wear");
                return null;
            }
            var dinfo = new DamageInfo(RimshiDefOf.Rimshi_MaintenanceWear, WearDamage, 0f, -1f, null, part, spawnFilth: false);
            dinfo.SetIgnoreArmor(true);
            dinfo.SetAllowDamagePropagation(false);
            pawn.TakeDamage(dinfo);
            float severity = pawn.health.hediffSet.hediffs.Where(h => h.def == RimshiDefOf.Rimshi_WornPart && h.Part == part).Sum(h => h.Severity);
            RimshiLog.Test("maintenance", "wear", pawn.LabelShort + ": out of maintenance, " + part.Label + " worn by " + WearDamage
                + " (worn total " + severity.ToString("0.##") + " of " + part.def.GetMaxHealth(pawn).ToString("0") + ")"
                + (pawn.Dead ? ", fell apart" : "") + (pawn.health.hediffSet.PartIsMissing(part) ? ", part worn away" : ""));
            if (pawn.Faction == Faction.OfPlayerSilentFail && !pawn.Dead && (pawn.Spawned || pawn.IsCaravanMember()))
            {
                Messages.Message(pawn.LabelShort + "'s " + part.Label + " is grinding itself apart without maintenance.", pawn, MessageTypeDefOf.NegativeHealthEvent);
            }
            return part;
        }
    }

    [HarmonyPatch(typeof(Need_Food), nameof(Need_Food.NeedInterval))]
    public static class Patch_MaintenanceWear
    {
        public static void Postfix(Need_Food __instance) => SkeletonWear.Update(__instance);
    }
}
