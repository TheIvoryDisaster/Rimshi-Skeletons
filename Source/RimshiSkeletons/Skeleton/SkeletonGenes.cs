using RimWorld;
using Verse;

namespace RimshiSkeletons
{
    /// <summary>
    /// Null chromosome pattern: makes the pawn genderless, the way Rim-Hivers! - Biotech (VexedTrees) does it: gender
    /// None, and a Female body swapped for Male. Asexual and fertility 0 come from the GeneDef. Runs on PostAdd, which
    /// covers generation (genes are added after gender and body type are rolled), xenogerm implants and dev mode.
    /// </summary>
    public class Gene_NullChromosome : Gene
    {
        public override void PostAdd()
        {
            base.PostAdd();
            Gender was = pawn.gender;
            pawn.gender = Gender.None;
            if (pawn.story?.bodyType == BodyTypeDefOf.Female)
            {
                pawn.story.bodyType = BodyTypeDefOf.Male;
            }
            pawn.Drawer?.renderer?.SetAllGraphicsDirty();
            RimshiLog.Test("gene", "genderless", pawn.LabelShort + ": " + was + " to " + pawn.gender + ", body " + pawn.story?.bodyType?.defName);
        }

        public override void PostRemove()
        {
            base.PostRemove();
            if (pawn.gender != Gender.None)
            {
                return;
            }
            pawn.gender = Rand.Bool ? Gender.Male : Gender.Female;
            if (pawn.story != null && !pawn.DevelopmentalStage.Juvenile())
            {
                pawn.story.bodyType = PawnGenerator.GetBodyTypeFor(pawn);
            }
            pawn.Drawer?.renderer?.SetAllGraphicsDirty();
            RimshiLog.Test("gene", "gender restored", pawn.LabelShort + ": now " + pawn.gender);
        }
    }

    /// <summary>
    /// Skeleton frame: the heads come from forcedHeadTypes; the body texture is swapped by Patch_BodyGraphic. This keeps
    /// the body type on one the skeleton art covers.
    /// </summary>
    public class Gene_SkeletonFrame : Gene
    {
        public override void PostAdd()
        {
            base.PostAdd();
            SkeletonUtility.FixBodyType(pawn);
            pawn.Drawer?.renderer?.SetAllGraphicsDirty();
            RimshiLog.Test("gene", "frame", pawn.LabelShort + ": body " + pawn.story?.bodyType?.defName + ", head " + pawn.story?.headType?.defName);
        }
    }

    /// <summary>Fuel reactor: logs what a skeleton eats, so the diet can be checked in Player.log.</summary>
    public class Gene_FuelReactor : Gene
    {
        public override void Notify_IngestedThing(Thing thing, int numTaken)
        {
            base.Notify_IngestedThing(thing, numTaken);
            if (thing?.def?.ingestible == null || thing.def.IsDrug)
            {
                return;
            }
            if (thing.def.HasModExtension<FuelExtension>())
            {
                RimshiLog.Test("diet", "ate fuel", pawn.LabelShort + " ate " + numTaken + " " + thing.def.defName);
            }
            else
            {
                RimshiLog.Fail("diet", "ate fuel", pawn.LabelShort + " ate non-fuel " + thing.def.defName);
            }
        }
    }

    /// <summary>Marks a ThingDef as skeleton fuel (chemfuel and uranium, added by Patches/Rimshi_FuelEdible.xml).</summary>
    public class FuelExtension : DefModExtension
    {
    }

    /// <summary>Settings for the fuel reactor gene.</summary>
    public class FuelReactorExtension : DefModExtension
    {
        public float hungerRateFactor = 0.1f;
    }
}
