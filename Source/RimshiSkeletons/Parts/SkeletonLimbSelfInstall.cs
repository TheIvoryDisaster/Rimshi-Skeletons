using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace RimshiSkeletons
{
    /// <summary>
    /// Skeleton limbs install themselves like a mechlink (docs/design/skeleton-parts.md): a skeleton colonist right-clicks
    /// the limb ("Install ...", vanilla CompUsable + UseItem job), picks which side when more than one fits, and fits it to
    /// itself, no surgery bill, doctor or repair kit. The install surgery stays as the other route (a skeleton with no arm
    /// left to work with, prisoners, downed skeletons). Organs and the AI core have no use comp and still need surgery.
    /// The part rules are the install recipe's own: the limb goes wherever its Rimshi_Install_ recipe would offer it, and the
    /// recipe worker does the install (old part dropped, hediff added, "parts | installed" logged).
    /// </summary>
    public class CompProperties_UseEffectInstallSkeletonLimb : CompProperties_UseEffect
    {
        /// <summary>The limb hediff. Left empty, the hediff with the item's own defName (every skeleton part is named so).</summary>
        public HediffDef hediffDef;

        public CompProperties_UseEffectInstallSkeletonLimb()
        {
            compClass = typeof(CompUseEffect_InstallSkeletonLimb);
        }
    }

    public class CompUseEffect_InstallSkeletonLimb : CompUseEffect
    {
        public const string RefuseNotSkeleton = "only fits a skeleton";
        public const string RefuseNoPlace = "nowhere to fit it";

        /// <summary>The side each pawn picked in the float menu, until it installs. Not saved: after a load it picks again.</summary>
        private readonly Dictionary<Pawn, BodyPartRecord> chosen = new Dictionary<Pawn, BodyPartRecord>();

        private static readonly Dictionary<HediffDef, RecipeDef> recipes = new Dictionary<HediffDef, RecipeDef>();

        public CompProperties_UseEffectInstallSkeletonLimb Props => (CompProperties_UseEffectInstallSkeletonLimb)props;

        public HediffDef Hediff => Props.hediffDef ?? DefDatabase<HediffDef>.GetNamedSilentFail(parent.def.defName);

        /// <summary>The skeleton part install surgery that adds this limb; its worker decides where the limb fits.</summary>
        public RecipeDef Recipe => RecipeFor(Hediff);

        public static RecipeDef RecipeFor(HediffDef hediff)
        {
            if (hediff == null)
            {
                return null;
            }
            if (!recipes.TryGetValue(hediff, out RecipeDef recipe))
            {
                recipe = DefDatabase<RecipeDef>.AllDefsListForReading.FirstOrDefault(r => r.addsHediff == hediff && r.Worker is Recipe_Rimshi_InstallSkeletonPart);
                recipes[hediff] = recipe;
            }
            return recipe;
        }

        /// <summary>Where the limb could go on this pawn, best first: missing limbs, then the most worn ones.</summary>
        public List<BodyPartRecord> PartsFor(Pawn pawn)
        {
            RecipeDef recipe = Recipe;
            if (recipe == null || pawn?.health?.hediffSet == null || !recipe.Worker.AvailableOnNow(pawn))
            {
                return new List<BodyPartRecord>();
            }
            HediffSet set = pawn.health.hediffSet;
            return recipe.Worker.GetPartsToApplyOn(pawn, recipe)
                .OrderByDescending(p => set.PartIsMissing(p))
                .ThenBy(p => PawnCapacityUtility.CalculatePartEfficiency(set, p))
                .ToList();
        }

        public override AcceptanceReport CanBeUsedBy(Pawn p)
        {
            if (!SkeletonUtility.HasMechanicalNature(p))
            {
                return RefuseNotSkeleton;
            }
            if (!p.IsFreeColonist || p.HasExtraHomeFaction())
            {
                return "InstallImplantNotAllowedForNonColonists".Translate();
            }
            if (PartsFor(p).Count == 0)
            {
                return RefuseNoPlace;
            }
            return true;
        }

        /// <summary>
        /// Picking "Install ..." (float menu or use gizmo): with more than one place to fit it, a second menu asks which.
        /// Returning true tells CompUsable this comp started the job itself.
        /// </summary>
        public override bool SelectedUseOption(Pawn p)
        {
            List<BodyPartRecord> parts = PartsFor(p);
            CompUsable usable = parent.GetComp<CompUsable>();
            if (parts.Count <= 1 || usable == null)
            {
                chosen.Remove(p);
                return false;
            }
            var options = new List<FloatMenuOption>();
            foreach (BodyPartRecord part in parts)
            {
                BodyPartRecord target = part;
                options.Add(new FloatMenuOption(part.LabelCap, () =>
                {
                    chosen[p] = target;
                    usable.TryStartUseJob(p, LocalTargetInfo.Invalid, usable.Props.ignoreOtherReservations);
                }));
            }
            Find.WindowStack.Add(new FloatMenu(options));
            return true;
        }

        public override void DoEffect(Pawn user)
        {
            base.DoEffect(user);
            List<BodyPartRecord> parts = PartsFor(user);
            BodyPartRecord part = chosen.TryGetValue(user, out BodyPartRecord picked) && parts.Contains(picked) ? picked : parts.FirstOrDefault();
            chosen.Remove(user);
            if (part == null)
            {
                RimshiLog.Fail("parts", "self installed", user.LabelShort + ": nowhere to fit " + parent.def.defName);
                return;
            }
            Recipe.Worker.ApplyOnPawn(user, part, null, new List<Thing>(), null);
            bool installed = user.health.hediffSet.hediffs.Any(h => h.def == Hediff && h.Part == part);
            if (installed)
            {
                RimshiLog.Test("parts", "self installed", user.LabelShort + ": " + parent.def.defName + " on " + part.def.defName + (picked == part ? " (picked)" : " (auto)"));
            }
            else
            {
                RimshiLog.Fail("parts", "self installed", user.LabelShort + ": " + parent.def.defName + " did not take on " + part.def.defName);
            }
        }
    }
}
