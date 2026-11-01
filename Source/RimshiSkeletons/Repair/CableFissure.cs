using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RimshiSkeletons
{
    /// <summary>
    /// The cable fissure's tuning (Rimshi_CableFissure, docs/design/soft-cycle-facilities.md). A tuning is made with a bill
    /// at the fissure (4 components, 100 steel, 40 stone blocks, 250 research points of work at research speed), drawing
    /// untunedWatts until it is done and nothing once tuned. Tuned, the fissure joins a soft cycle in a linked repair bed,
    /// and when that cycle finishes the skeleton is overclocked (Overclock) and the fissure needs tuning again.
    /// </summary>
    public class CompProperties_Rimshi_FissureTuning : CompProperties
    {
        public float untunedWatts = 250f;

        /// <summary>The tuning bill, put on a newly built fissure so it gets tuned without the player adding it.</summary>
        public RecipeDef recipe;

        public CompProperties_Rimshi_FissureTuning()
        {
            compClass = typeof(CompRimshi_FissureTuning);
        }
    }

    public class CompRimshi_FissureTuning : ThingComp, ISoftCycleCharge
    {
        private bool tuned;

        public CompProperties_Rimshi_FissureTuning Props => (CompProperties_Rimshi_FissureTuning)props;

        public CompPowerTrader PowerTrader => parent.GetComp<CompPowerTrader>();

        public bool Charged => tuned;

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            if (!respawningAfterLoad && Props.recipe != null && parent is Building_WorkTable table && table.billStack != null && table.billStack.Count == 0)
            {
                Bill bill = Props.recipe.MakeNewBill();
                if (bill is Bill_Production production)
                {
                    production.repeatMode = BillRepeatModeDefOf.Forever;
                }
                table.billStack.AddBill(bill);
            }
            UpdatePowerDraw();
        }

        public override void CompTickRare()
        {
            UpdatePowerDraw();
        }

        public override void ReceiveCompSignal(string signal)
        {
            UpdatePowerDraw();
        }

        /// <summary>250 W until tuned, nothing once tuned (Matthew's spec).</summary>
        private void UpdatePowerDraw()
        {
            CompPowerTrader trader = PowerTrader;
            if (trader != null && trader.PowerOn)
            {
                trader.PowerOutput = tuned ? 0f : -Props.untunedWatts;
            }
        }

        /// <summary>The tuning bill finished.</summary>
        public void Tune(Pawn tuner)
        {
            tuned = true;
            UpdatePowerDraw();
            RimshiLog.Test("fissure", "tuned", parent.LabelShort + " tuned by " + (tuner?.LabelShort ?? "a dev gizmo") + ", now drawing 0 W");
        }

        /// <summary>A cycle it joined finished: the overclock goes into the skeleton and the tuning is spent.</summary>
        public void Discharge(Pawn pawn)
        {
            if (!tuned)
            {
                return;
            }
            tuned = false;
            UpdatePowerDraw();
            Overclock.Grant(pawn, parent);
        }

        public override void PostExposeData()
        {
            Scribe_Values.Look(ref tuned, "rimshiTuned");
        }

        public override string CompInspectStringExtra()
        {
            return tuned ? "Tuned. The cables hold still, waiting." : "Untuned.";
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (!Prefs.DevMode || tuned)
            {
                yield break;
            }
            yield return new Command_Action
            {
                defaultLabel = "DEV: Tune now",
                action = () => Tune(null)
            };
        }
    }

    /// <summary>Tunes the fissure the bill was done at.</summary>
    public class RecipeWorker_Rimshi_TuneFissure : RecipeWorker
    {
        public override void Notify_IterationCompleted(Pawn billDoer, List<Thing> ingredients)
        {
            base.Notify_IterationCompleted(billDoer, ingredients);
            Thing bench = billDoer?.CurJob?.GetTarget(TargetIndex.A).Thing;
            CompRimshi_FissureTuning tuning = bench?.TryGetComp<CompRimshi_FissureTuning>();
            if (tuning == null)
            {
                RimshiLog.Fail("fissure", "tuned", "tuning bill finished by " + billDoer?.LabelShort + " but no cable fissure was its target (" + bench + ")");
                return;
            }
            tuning.Tune(billDoer);
        }
    }

    /// <summary>The research-type bill giver for the fissure: a tuned fissure has nothing to do.</summary>
    public class WorkGiver_Rimshi_TuneFissure : WorkGiver_DoBill
    {
        public override Job JobOnThing(Pawn pawn, Thing thing, bool forced = false)
        {
            if (thing.TryGetComp<CompRimshi_FissureTuning>()?.Charged ?? false)
            {
                return null;
            }
            return base.JobOnThing(pawn, thing, forced);
        }
    }

    /// <summary>
    /// The half-made tuning. Vanilla's unfinished thing names and describes itself from its recipe's first product, and
    /// the tuning bill has none, so it uses its own def's label and description.
    /// </summary>
    public class UnfinishedThing_Rimshi_Tuning : UnfinishedThing
    {
        public override string LabelNoCount => def.label;

        public override string DescriptionDetailed => def.description;

        public override string DescriptionFlavor => def.description;

        public override IEnumerable<StatDrawEntry> SpecialDisplayStats()
        {
            yield break;
        }
    }

    /// <summary>
    /// What the cable fissure leaves in a skeleton when a soft cycle it joined finishes: Rimshi_Overclocked, +20 carrying
    /// capacity and the one-use heave (Rimshi_Heave). Heaving disarms one adjacent enemy and throws it carrying capacity / 10
    /// cells straight away from the skeleton, through other pawns, 2 to 5 blunt damage on a different body part for each
    /// cell; each cell a wall (or closed door, or the map edge) stops it short of hits for double. Then the overclock is spent.
    /// </summary>
    public static class Overclock
    {
        public const int MinHit = 2;

        public const int MaxHit = 5;

        public const float CellsPerCarryingCapacity = 0.1f;

        public static HediffDef Overclocked => DefDatabase<HediffDef>.GetNamedSilentFail("Rimshi_Overclocked");

        public static bool Holds(Pawn pawn) => Overclocked != null && (pawn?.health?.hediffSet?.HasHediff(Overclocked) ?? false);

        public static int Distance(Pawn pawn) => Mathf.Max(1, Mathf.FloorToInt(pawn.GetStatValue(StatDefOf.CarryingCapacity) * CellsPerCarryingCapacity));

        public static void Grant(Pawn pawn, Thing fissure)
        {
            if (pawn?.health == null || Overclocked == null || !SkeletonUtility.HasFrame(pawn))
            {
                return;
            }
            Hediff old = pawn.health.hediffSet.GetFirstHediffOfDef(Overclocked);
            if (old != null)
            {
                pawn.health.RemoveHediff(old);
            }
            pawn.health.AddHediff(Overclocked);
            RimshiLog.Test("fissure", "overclocked", pawn.LabelShort + " took the overclock from " + fissure.LabelShort + ": carrying capacity "
                + pawn.GetStatValue(StatDefOf.CarryingCapacity).ToString("0") + ", heave distance " + Distance(pawn) + " cells" + (old != null ? " (replaced an unspent one)" : ""));
            if (PawnUtility.ShouldSendNotificationAbout(pawn))
            {
                Messages.Message(pawn.LabelShort + " rises from its soft cycle with its cables drawn tight.", pawn, MessageTypeDefOf.PositiveEvent);
            }
        }

        /// <summary>True if the cell stops a heaved pawn: out of the map, a wall or other impassable thing, or a closed door.</summary>
        public static bool Blocks(IntVec3 cell, Map map)
        {
            if (!cell.InBounds(map) || !cell.Walkable(map))
            {
                return true;
            }
            Building_Door door = cell.GetDoor(map);
            return door != null && !door.Open;
        }

        /// <summary>
        /// The cells a pawn heaved from `from` in direction `step` passes through, up to `distance`; blocked is how many it
        /// fell short by. Other pawns don't stop it. A diagonal step between two blocked cells is blocked too.
        /// </summary>
        public static List<IntVec3> Path(IntVec3 from, IntVec3 step, int distance, Map map, out int blocked)
        {
            var path = new List<IntVec3>();
            IntVec3 cur = from;
            for (int i = 0; i < distance; i++)
            {
                IntVec3 next = cur + step;
                bool corner = step.x != 0 && step.z != 0 && Blocks(new IntVec3(cur.x + step.x, 0, cur.z), map) && Blocks(new IntVec3(cur.x, 0, cur.z + step.z), map);
                if (corner || Blocks(next, map))
                {
                    blocked = distance - i;
                    return path;
                }
                path.Add(next);
                cur = next;
            }
            blocked = 0;
            return path;
        }

        public static bool CanHeave(Pawn caster, Pawn target)
        {
            return target != null && target != caster && target.Spawned && !target.Dead && caster.Spawned && target.Map == caster.Map
                && target.HostileTo(caster) && target.Position.AdjacentTo8Way(caster.Position);
        }

        public static void Heave(Pawn caster, Pawn target)
        {
            if (!CanHeave(caster, target))
            {
                return;
            }
            Map map = caster.Map;
            IntVec3 start = target.Position;
            IntVec3 step = start - caster.Position;
            int distance = Distance(caster);

            string disarmed = "nothing";
            ThingWithComps weapon = target.equipment?.Primary;
            if (weapon != null && target.equipment.TryDropEquipment(weapon, out ThingWithComps dropped, start, forbid: false))
            {
                disarmed = dropped?.LabelShort ?? weapon.LabelShort;
            }

            List<IntVec3> path = Path(start, step, distance, map, out int blocked);
            foreach (IntVec3 cell in path)
            {
                FleckMaker.ThrowDustPuff(cell, map, 1.2f);
            }
            if (path.Count > 0)
            {
                target.Position = path[path.Count - 1];
                target.Notify_Teleported(endCurrentJob: true, resetTweenedPos: true);
            }
            if (blocked > 0)
            {
                FleckMaker.ThrowMicroSparks(target.DrawPos, map);
            }

            // One hit for every cell moved, a double hit for every cell a wall stopped it short of, each on a body part
            // not yet hit if there is one left.
            var hit = new HashSet<BodyPartRecord>();
            var hits = new List<string>();
            float total = 0f;
            int count = path.Count + blocked;
            for (int i = 0; i < count && !target.Dead; i++)
            {
                float amount = Rand.RangeInclusive(MinHit, MaxHit) * (i < path.Count ? 1 : 2);
                BodyPartRecord part = PickPart(target, hit);
                if (part == null)
                {
                    break;
                }
                hit.Add(part);
                target.TakeDamage(new DamageInfo(DamageDefOf.Blunt, amount, 0f, -1f, caster, part));
                total += amount;
                hits.Add(part.Label + " " + amount.ToString("0"));
            }

            RimshiLog.Test("fissure", "heave", caster.LabelShort + " heaved " + target.LabelShort + ": disarmed " + disarmed + ", distance " + distance + ", moved " + path.Count
                + ", blocked " + blocked + ", " + hits.Count + " hits on " + hit.Count + " parts (" + string.Join(", ", hits) + "), total " + total.ToString("0") + (target.Dead ? ", killed" : ""));

            if (caster.health.hediffSet.GetFirstHediffOfDef(Overclocked) is Hediff_Rimshi_Overclocked held)
            {
                held.spent = true;
            }
        }

        private static BodyPartRecord PickPart(Pawn target, HashSet<BodyPartRecord> hit)
        {
            List<BodyPartRecord> parts = target.health.hediffSet.GetNotMissingParts(BodyPartHeight.Undefined, BodyPartDepth.Outside).ToList();
            if (parts.Count == 0)
            {
                return null;
            }
            List<BodyPartRecord> fresh = parts.Where(p => !hit.Contains(p)).ToList();
            List<BodyPartRecord> pool = fresh.Count > 0 ? fresh : parts;
            return pool.RandomElementByWeightWithFallback(p => Mathf.Max(0.01f, p.coverageAbsWithChildren));
        }
    }

    /// <summary>The overclock. Gives the heave (vanilla HediffComp_GiveAbility); once heaved it is spent and goes on its next tick.</summary>
    public class Hediff_Rimshi_Overclocked : HediffWithComps
    {
        public bool spent;

        public override bool ShouldRemove => spent || base.ShouldRemove;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref spent, "rimshiSpent");
        }
    }

    public class CompProperties_Rimshi_Heave : CompProperties_AbilityEffect
    {
        public CompProperties_Rimshi_Heave()
        {
            compClass = typeof(CompAbilityEffect_Rimshi_Heave);
        }
    }

    public class CompAbilityEffect_Rimshi_Heave : CompAbilityEffect
    {
        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Overclock.Heave(parent.pawn, target.Pawn);
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            if (!Overclock.CanHeave(parent.pawn, target.Pawn))
            {
                if (throwMessages)
                {
                    Messages.Message("Only an enemy within arm's reach.", target.ToTargetInfo(parent.pawn.Map), MessageTypeDefOf.RejectInput, false);
                }
                return false;
            }
            return base.Valid(target, throwMessages);
        }

        public override bool GizmoDisabled(out string reason)
        {
            if (!Overclock.Holds(parent.pawn))
            {
                reason = "Nothing is wound up.";
                return true;
            }
            return base.GizmoDisabled(out reason);
        }
    }
}
