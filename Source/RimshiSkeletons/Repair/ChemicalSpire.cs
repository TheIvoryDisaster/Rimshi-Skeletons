using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace RimshiSkeletons
{
    /// <summary>
    /// The chemical spire's battery (Rimshi_ChemicalSpire, docs/design/soft-cycle-facilities.md): filled with chemfuel,
    /// then charged at chargeWatts for chargeTicks without a break (a power cut starts the charge over, the chemfuel stays).
    /// A full charge never drains. The soft cycle facility comp only lets it join a cycle while charged, and when that
    /// cycle finishes the charge (and the chemfuel) goes into the skeleton as a held storm (SpireStorm).
    /// </summary>
    public class CompProperties_Rimshi_SpireCharge : CompProperties
    {
        public float chargeWatts = 100f;

        public int chargeTicks = 36 * GenDate.TicksPerHour;

        public CompProperties_Rimshi_SpireCharge()
        {
            compClass = typeof(CompRimshi_SpireCharge);
        }
    }

    public class CompRimshi_SpireCharge : ThingComp, ISoftCycleCharge
    {
        private bool charged;
        private int chargedTicks;

        public CompProperties_Rimshi_SpireCharge Props => (CompProperties_Rimshi_SpireCharge)props;

        public CompRefuelable Refuelable => parent.GetComp<CompRefuelable>();

        public CompPowerTrader PowerTrader => parent.GetComp<CompPowerTrader>();

        public bool Filled => Refuelable == null || Refuelable.IsFull;

        /// <summary>Full of chemfuel and holding its charge.</summary>
        public bool Charged => charged && Filled;

        public bool Charging => Filled && !charged;

        public float ChargeFraction => charged ? 1f : Mathf.Clamp01((float)chargedTicks / Props.chargeTicks);

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            UpdatePowerDraw();
        }

        public override void CompTickRare()
        {
            if (charged && !Filled)
            {
                // Something emptied the cistern (a dev tool, another mod): the charge goes with the chemfuel.
                charged = false;
                chargedTicks = 0;
            }
            if (Charging)
            {
                if (PowerTrader?.PowerOn ?? true)
                {
                    chargedTicks += GenTicks.TickRareInterval;
                    if (chargedTicks >= Props.chargeTicks)
                    {
                        charged = true;
                        chargedTicks = 0;
                        RimshiLog.Test("spire", "charged", parent.LabelShort + " fully charged (" + Props.chargeTicks.ToStringTicksToPeriod() + " at " + Props.chargeWatts + " W)");
                    }
                }
                else
                {
                    LoseCharge("unpowered");
                }
            }
            UpdatePowerDraw();
        }

        public override void ReceiveCompSignal(string signal)
        {
            if (signal == "PowerTurnedOff" || signal == "FlickedOff")
            {
                LoseCharge(signal);
            }
            UpdatePowerDraw();
        }

        /// <summary>The charge has to run straight through: a break while charging starts it over. A full charge is kept.</summary>
        private void LoseCharge(string why)
        {
            if (!Charging || chargedTicks <= 0)
            {
                return;
            }
            RimshiLog.Test("spire", "charge lost", parent.LabelShort + " lost " + ChargeFraction.ToStringPercent() + " of its charge (" + why + ")");
            chargedTicks = 0;
            if (parent.Spawned && parent.Faction == Faction.OfPlayer)
            {
                Messages.Message("The " + parent.LabelShort + " lost the storm it was drawing in when its power failed.", parent, MessageTypeDefOf.NegativeEvent);
            }
        }

        /// <summary>Draws power only while charging; empty or charged it takes nothing.</summary>
        private void UpdatePowerDraw()
        {
            CompPowerTrader trader = PowerTrader;
            if (trader != null)
            {
                trader.PowerOutput = Charging ? -Props.chargeWatts : 0f;
            }
        }

        /// <summary>A cycle it joined finished: the storm goes into the skeleton and the spire is spent, chemfuel and all.</summary>
        public void Discharge(Pawn pawn)
        {
            if (!Charged)
            {
                return;
            }
            charged = false;
            chargedTicks = 0;
            Refuelable?.ConsumeFuel(Refuelable.Fuel);
            UpdatePowerDraw();
            SpireStorm.Grant(pawn, parent);
        }

        public override void PostExposeData()
        {
            Scribe_Values.Look(ref charged, "rimshiCharged");
            Scribe_Values.Look(ref chargedTicks, "rimshiChargedTicks");
        }

        public override string CompInspectStringExtra()
        {
            if (Charged)
            {
                return "The storm is held.";
            }
            if (!Filled)
            {
                return "Its cistern waits to be filled.";
            }
            if (!(PowerTrader?.PowerOn ?? true))
            {
                return "Waiting for power.";
            }
            return "Drawing a storm in (" + ChargeFraction.ToStringPercent() + ").";
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (!Prefs.DevMode)
            {
                yield break;
            }
            yield return new Command_Action
            {
                defaultLabel = "DEV: Charge fully",
                action = () =>
                {
                    Refuelable?.Refuel(Refuelable.Props.fuelCapacity);
                    charged = true;
                    chargedTicks = 0;
                    UpdatePowerDraw();
                    RimshiLog.Test("spire", "charged", parent.LabelShort + " charged by a dev gizmo");
                }
            };
        }
    }

    /// <summary>
    /// The storm a chemical spire leaves in a skeleton when a soft cycle it joined finishes. Rimshi_HeldTempest gives
    /// x1.1 move speed and the one-use burst; once the burst is loosed it becomes Rimshi_TempestWake. Either one shields
    /// the unshielded circuits from EMP stun (SkeletonModules.EmpSusceptible). Both end when the next soft cycle finishes.
    /// </summary>
    public static class SpireStorm
    {
        /// <summary>Damage at the most: 40 (Matthew's maximum); 30 counts as high.</summary>
        public const float MaxDamage = 40f;

        public const float MinDamage = 5f;

        /// <summary>Burst damage per skill level, all skills summed: 120 levels (10 in each of 12 skills) give 30, 160 give 40.</summary>
        public const float DamagePerSkillLevel = 0.25f;

        /// <summary>The burst's damage is split into hits of at most this much, so it scorches rather than takes off a head.</summary>
        public const float DamagePerHit = 10f;

        public static HediffDef HeldTempest => DefDatabase<HediffDef>.GetNamedSilentFail("Rimshi_HeldTempest");

        public static HediffDef TempestWake => DefDatabase<HediffDef>.GetNamedSilentFail("Rimshi_TempestWake");

        public static bool EmpProof(Pawn pawn)
        {
            HediffSet set = pawn?.health?.hediffSet;
            return set != null && ((HeldTempest != null && set.HasHediff(HeldTempest)) || (TempestWake != null && set.HasHediff(TempestWake)));
        }

        public static bool HoldsBurst(Pawn pawn) => HeldTempest != null && (pawn?.health?.hediffSet?.HasHediff(HeldTempest) ?? false);

        public static float Damage(Pawn pawn) => Mathf.Clamp(SoftCycle.SkillLevels(pawn) * DamagePerSkillLevel, MinDamage, MaxDamage);

        public static void Grant(Pawn pawn, Thing spire)
        {
            if (pawn?.health == null || HeldTempest == null || !SkeletonUtility.HasFrame(pawn))
            {
                return;
            }
            End(pawn, "a new storm replaces it");
            pawn.health.AddHediff(HeldTempest);
            RimshiLog.Test("spire", "storm granted", pawn.LabelShort + " took the storm from " + spire.LabelShort + ": EMP-proof, x1.1 move speed, one burst of "
                + Damage(pawn).ToString("0") + " damage (" + SoftCycle.SkillLevels(pawn) + " skill levels)");
            if (PawnUtility.ShouldSendNotificationAbout(pawn))
            {
                Messages.Message(pawn.LabelShort + " rises from its soft cycle humming with something the " + spire.LabelShort + " was holding.", pawn, MessageTypeDefOf.PositiveEvent);
            }
        }

        /// <summary>The next soft cycle finished (or a new storm came): whatever is left of the old one goes.</summary>
        public static void End(Pawn pawn, string why)
        {
            if (pawn?.health == null)
            {
                return;
            }
            foreach (HediffDef def in new[] { HeldTempest, TempestWake })
            {
                Hediff hediff = def == null ? null : pawn.health.hediffSet.GetFirstHediffOfDef(def);
                if (hediff != null)
                {
                    pawn.health.RemoveHediff(hediff);
                    RimshiLog.Test("spire", "storm ended", pawn.LabelShort + "'s " + def.defName + " ended: " + why);
                }
            }
        }

        /// <summary>Hostile mechanoids and skeletons within the radius, in line of sight of the caster (Matthew: hostiles only).</summary>
        public static List<Pawn> BurstTargets(Pawn caster, float radius)
        {
            Map map = caster.Map;
            return map.mapPawns.AllPawnsSpawned
                .Where(p => p != caster && !p.Dead && p.HostileTo(caster) && (p.RaceProps.IsMechanoid || SkeletonUtility.HasFrame(p))
                    && p.Position.InHorDistOf(caster.Position, radius) && GenSight.LineOfSight(caster.Position, p.Position, map))
                .ToList();
        }

        /// <summary>
        /// Looses the burst: every hostile mechanoid and skeleton in range takes an EMP hit of the damage (a stun,
        /// unless it is shielded or adapted) and that much burn damage in hits of up to 10. Then the held tempest settles
        /// into its wake, which keeps the EMP shield.
        /// </summary>
        public static void Burst(Pawn caster, float radius)
        {
            Map map = caster.Map;
            if (map == null)
            {
                return;
            }
            float damage = Damage(caster);
            DamageDef emp = DamageDefOf.EMP;
            if (emp.explosionCellFleck != null)
            {
                foreach (IntVec3 cell in GenRadial.RadialCellsAround(caster.Position, radius, true))
                {
                    if (cell.InBounds(map) && Rand.Chance(0.35f))
                    {
                        FleckMaker.Static(cell, map, emp.explosionCellFleck);
                    }
                }
            }
            emp.soundExplosion?.PlayOneShot(new TargetInfo(caster.Position, map));
            List<Pawn> targets = BurstTargets(caster, radius);
            int stunned = 0;
            foreach (Pawn target in targets)
            {
                bool wasStunned = target.stances?.stunner?.Stunned ?? false;
                target.TakeDamage(new DamageInfo(emp, damage, 0f, -1f, caster));
                for (float left = damage; left > 0.01f && !target.Dead; left -= DamagePerHit)
                {
                    target.TakeDamage(new DamageInfo(DamageDefOf.Burn, Mathf.Min(DamagePerHit, left), 0.3f, -1f, caster));
                }
                FleckMaker.ThrowMicroSparks(target.DrawPos, map);
                if (!wasStunned && (target.stances?.stunner?.Stunned ?? false))
                {
                    stunned++;
                }
            }
            RimshiLog.Test("spire", "burst", caster.LabelShort + " loosed the storm: " + damage.ToString("0") + " damage (" + SoftCycle.SkillLevels(caster)
                + " skill levels), radius " + radius + ", hit " + targets.Count + " (" + string.Join(", ", targets.Select(t => t.LabelShort)) + "), stunned " + stunned);
            if (caster.health.hediffSet.GetFirstHediffOfDef(HeldTempest) is Hediff_Rimshi_HeldTempest held)
            {
                held.spent = true;
            }
            if (TempestWake != null && !caster.health.hediffSet.HasHediff(TempestWake))
            {
                caster.health.AddHediff(TempestWake);
            }
        }
    }

    /// <summary>
    /// The held tempest. Gives the burst ability (vanilla HediffComp_GiveAbility, removed with the hediff); once the burst
    /// is loosed it is marked spent and the health tracker removes it on its next tick, outside the ability's own cast.
    /// </summary>
    public class Hediff_Rimshi_HeldTempest : HediffWithComps
    {
        public bool spent;

        public override bool ShouldRemove => spent || base.ShouldRemove;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref spent, "rimshiSpent");
        }
    }

    public class CompProperties_Rimshi_TempestBurst : CompProperties_AbilityEffect
    {
        public float radius = 10f;

        public CompProperties_Rimshi_TempestBurst()
        {
            compClass = typeof(CompAbilityEffect_Rimshi_TempestBurst);
        }
    }

    public class CompAbilityEffect_Rimshi_TempestBurst : CompAbilityEffect
    {
        public new CompProperties_Rimshi_TempestBurst Props => (CompProperties_Rimshi_TempestBurst)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            SpireStorm.Burst(parent.pawn, Props.radius);
        }

        public override bool GizmoDisabled(out string reason)
        {
            if (!SpireStorm.HoldsBurst(parent.pawn))
            {
                reason = "Nothing is held.";
                return true;
            }
            return base.GizmoDisabled(out reason);
        }
    }
}
