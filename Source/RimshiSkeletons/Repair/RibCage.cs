using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RimshiSkeletons
{
    public class CompProperties_Rimshi_RibCage : CompProperties
    {
        /// <summary>The ribs in front of the hanging skeleton, drawn over it (Graphic_Single, same draw size as the building).</summary>
        public string frontTexPath;

        public CompProperties_Rimshi_RibCage()
        {
            compClass = typeof(CompRimshi_RibCage);
        }
    }

    /// <summary>
    /// The rib cage (docs/design/cpu-soft-cycle.md): a medical, unassignable, skeleton-only bed that holds a skeleton upright
    /// in stasis. A skeleton in one has its needs frozen, so CPU integrity and any overload stop moving. Rescuers bring
    /// downed skeletons here when no repair bed is free (it ranks just below the repair bed). Draws the front ribs over
    /// whoever hangs in it.
    /// </summary>
    public class CompRimshi_RibCage : ThingComp
    {
        private Graphic front;

        public CompProperties_Rimshi_RibCage Props => (CompProperties_Rimshi_RibCage)props;

        private Building_Bed Bed => parent as Building_Bed;

        public override void PostDraw()
        {
            base.PostDraw();
            if (Props.frontTexPath.NullOrEmpty())
            {
                return;
            }
            if (front == null)
            {
                front = GraphicDatabase.Get<Graphic_Single>(Props.frontTexPath, ShaderDatabase.Cutout, parent.def.graphicData.drawSize, Color.white);
            }
            Vector3 loc = parent.DrawPos + parent.def.graphicData.drawOffset;
            loc.y = AltitudeLayer.PawnState.AltitudeFor();
            front.Draw(loc, Rot4.North, parent);
        }

        public override string CompInspectStringExtra()
        {
            List<string> held = Bed?.CurOccupants.Select(p => p.LabelShort).ToList();
            return held != null && held.Count > 0 ? "Suspended: " + string.Join(", ", held) : null;
        }
    }

    public static class RibCage
    {
        public static bool IsRibCage(ThingDef def) => def != null && def.HasComp(typeof(CompRimshi_RibCage));

        /// <summary>True while the pawn lies (hangs) in a rib cage.</summary>
        public static bool Suspended(Pawn pawn)
        {
            if (pawn == null || !pawn.Spawned || !pawn.InBed())
            {
                return false;
            }
            return IsRibCage(pawn.CurrentBed()?.def);
        }

        public static Building_Bed CageAt(IntVec3 cell, Map map)
        {
            if (map == null || !cell.InBounds(map))
            {
                return null;
            }
            return cell.GetThingList(map).OfType<Building_Bed>().FirstOrDefault(b => IsRibCage(b.def));
        }

        public static bool CanHangIn(Pawn pawn, Building_Bed cage, out string reason)
        {
            reason = null;
            if (!SkeletonUtility.HasFrame(pawn))
            {
                reason = "only skeletons";
                return false;
            }
            if (cage.IsForbidden(pawn) || cage.IsBurning() || cage.Faction != Faction.OfPlayer)
            {
                reason = "not usable";
                return false;
            }
            if (!cage.CurOccupants.Contains(pawn) && !cage.AnyUnoccupiedSleepingSlot)
            {
                reason = "occupied";
                return false;
            }
            if (!pawn.CanReach(cage, PathEndMode.OnCell, Danger.Deadly) || !(pawn.HasReserved(cage) || pawn.CanReserve(cage, cage.SleepingSlotsCount)))
            {
                reason = "unreachable or reserved";
                return false;
            }
            return true;
        }
    }

    /// <summary>Walks into the rib cage and hangs there until given another order. Needs freeze while it hangs.</summary>
    public class JobDriver_Rimshi_HangInRibCage : JobDriver
    {
        private Building_Bed Cage => job.GetTarget(TargetIndex.A).Thing as Building_Bed;

        public override bool TryMakePreToilReservations(bool errorOnFailed) => pawn.Reserve(Cage, job, Cage.SleepingSlotsCount, 0, null, errorOnFailed);

        protected override IEnumerable<Toil> MakeNewToils()
        {
            yield return Toils_Bed.GotoBed(TargetIndex.A);
            Toil hang = Toils_LayDown.LayDown(TargetIndex.A, hasBed: true, lookForOtherJobs: false, canSleep: false, gainRestAndHealth: false);
            hang.initAction = (System.Action)System.Delegate.Combine(hang.initAction, (System.Action)delegate
            {
                RimshiLog.Test("ribcage", "hang", pawn.LabelShort + " hanging in " + Cage.LabelShort + " at " + Cage.Position + ", needs frozen " + RibCage.Suspended(pawn));
            });
            yield return hang;
        }
    }

    /// <summary>
    /// Freezes every need of a pawn suspended in a rib cage (Need_Food and others call this base getter too), and every
    /// need but CPU integrity of a skeleton in a soft cycle, in a repair bed or standing (Matthew, 2026-10-28: nothing
    /// like maintenance, hygiene or recreation drops while it cycles). Integrity is raised by the cycle itself.
    /// </summary>
    [HarmonyPatch(typeof(Need), "IsFrozen", MethodType.Getter)]
    public static class Patch_RibCageStasis
    {
        public static void Postfix(Need __instance, Pawn ___pawn, ref bool __result)
        {
            if (__result)
            {
                return;
            }
            if (RibCage.Suspended(___pawn) || (!(__instance is Need_Deathrest) && ___pawn != null && SoftCycle.Cycling(___pawn) && SkeletonUtility.HasFrame(___pawn)))
            {
                __result = true;
            }
        }
    }

    /// <summary>Rib cages are always medical, so they are never assigned to anyone: the medical toggle is taken away.</summary>
    [HarmonyPatch(typeof(Building_Bed), nameof(Building_Bed.GetGizmos))]
    public static class Patch_RibCageGizmos
    {
        public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, Building_Bed __instance)
        {
            bool cage = RibCage.IsRibCage(__instance.def);
            string medical = cage ? "CommandBedSetAsMedicalLabel".Translate().ToString() : null;
            foreach (Gizmo gizmo in __result)
            {
                if (cage && gizmo is Command_Toggle toggle && toggle.defaultLabel == medical)
                {
                    continue;
                }
                yield return gizmo;
            }
        }
    }

    /// <summary>
    /// Right-click a rib cage with a skeleton selected: hang in it, or soft reset in it. Vanilla's "use medical bed" is dropped.
    /// Right-click a repair bed: soft cycle in it (greyed out with the reason when it can't take the skeleton now).
    /// </summary>
    [HarmonyPatch(typeof(Building_Bed), nameof(Building_Bed.GetFloatMenuOptions))]
    public static class Patch_RibCageFloatMenu
    {
        public static IEnumerable<FloatMenuOption> Postfix(IEnumerable<FloatMenuOption> __result, Building_Bed __instance, Pawn myPawn)
        {
            if (!RibCage.IsRibCage(__instance.def))
            {
                foreach (FloatMenuOption option in __result)
                {
                    yield return option;
                }
                FloatMenuOption cycle = SoftCycle.RepairBedFloatOption(myPawn, __instance);
                if (cycle != null)
                {
                    yield return cycle;
                }
                yield break;
            }
            string useMedical = "UseMedicalBed".Translate().ToString();
            foreach (FloatMenuOption option in __result)
            {
                if (option.Label == null || !option.Label.StartsWith(useMedical))
                {
                    yield return option;
                }
            }
            if (!SkeletonUtility.HasFrame(myPawn) || !myPawn.IsColonistPlayerControlled || myPawn.Downed)
            {
                yield break;
            }
            if (!RibCage.CanHangIn(myPawn, __instance, out string reason))
            {
                yield return new FloatMenuOption("Hang in rib cage (" + reason + ")", null);
                yield break;
            }
            if (!RibCage.Suspended(myPawn))
            {
                yield return FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption("Hang in rib cage", delegate
                {
                    myPawn.jobs.TryTakeOrderedJob(JobMaker.MakeJob(RimshiDefOf.Rimshi_HangInRibCage, __instance), JobTag.Misc);
                }), myPawn, __instance);
            }
            if (SoftCycle.IsSkeletonCycler(myPawn) && !SoftCycle.Cycling(myPawn))
            {
                yield return FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption("Soft cycle in rib cage", delegate
                {
                    StandingReset.Order(myPawn, __instance);
                }), myPawn, __instance);
            }
        }
    }
}
