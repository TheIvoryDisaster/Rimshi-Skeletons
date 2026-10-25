using System.Collections.Generic;
using RimWorld;
using Verse;

namespace RimshiSkeletons
{
    [DefOf]
    public static class RimshiDefOf
    {
        public static XenotypeDef Rimshi_Skeleton;

        public static GeneDef Rimshi_SkeletonFrame;
        public static GeneDef Rimshi_MechanicalNature;
        public static GeneDef Rimshi_FuelReactor;
        public static GeneDef Rimshi_NullChromosome;

        public static FactionDef Rimshi_SkeletalImperium;
        public static FactionDef Rimshi_PlayerSkeletons;

        public static ThingDef Rimshi_RecallArray;
        public static ResearchProjectDef Rimshi_SkeletonRecall;
        public static HediffDef Rimshi_CorruptedProtocol;
        public static HediffDef Rimshi_FailsafeCore;
        public static PrisonerInteractionModeDef Rimshi_Reprogram;
        public static JobDef Rimshi_ReprogramSkeleton;

        static RimshiDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(RimshiDefOf));
        }
    }

    public static class SkeletonUtility
    {
        public const string BodyPath = "Rimshi/Body/Naked_";

        /// <summary>Backstory spawn category of the skeleton backstories (Defs/BackstoryDefs).</summary>
        public const string BackstoryCategory = "Rimshi_Skeleton";

        /// <summary>True for our xenotype and any xenotype carrying the skeleton frame gene.</summary>
        public static bool IsSkeletonXenotype(XenotypeDef xenotype) =>
            xenotype != null && (xenotype == RimshiDefOf.Rimshi_Skeleton || (xenotype.genes != null && xenotype.genes.Contains(RimshiDefOf.Rimshi_SkeletonFrame)));

        public static bool HasActiveGene(Pawn pawn, GeneDef gene) => pawn?.genes != null && pawn.genes.HasActiveGene(gene);

        public static bool HasFrame(Pawn pawn) => HasActiveGene(pawn, RimshiDefOf.Rimshi_SkeletonFrame);

        public static bool HasFuelReactor(Pawn pawn) => HasActiveGene(pawn, RimshiDefOf.Rimshi_FuelReactor);

        public static bool HasNullChromosome(Pawn pawn) => HasActiveGene(pawn, RimshiDefOf.Rimshi_NullChromosome);

        /// <summary>Gene display category of every skeleton gene (Defs/GeneDefs/Rimshi_Genes.xml).</summary>
        public const string GeneCategory = "Rimshi_Skeleton";

        /// <summary>True for our genes: everything in the skeleton gene category, plating colours included.</summary>
        public static bool IsSkeletonGene(GeneDef gene) => gene?.displayCategory?.defName == GeneCategory;

        /// <summary>True if the pawn has any skeleton gene, active or not, or the skeleton xenotype.</summary>
        public static bool CarriesSkeletonGenes(Pawn pawn)
        {
            if (pawn?.genes == null)
            {
                return false;
            }
            if (IsSkeletonXenotype(pawn.genes.Xenotype))
            {
                return true;
            }
            List<Gene> genes = pawn.genes.GenesListForReading;
            for (int i = 0; i < genes.Count; i++)
            {
                if (IsSkeletonGene(genes[i].def))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Skeleton art exists for Male, Thin and Hulk only (as in the original). Children and babies keep vanilla.</summary>
        public static bool IsSkeletonBodyType(BodyTypeDef bodyType) =>
            bodyType == BodyTypeDefOf.Male || bodyType == BodyTypeDefOf.Thin || bodyType == BodyTypeDefOf.Hulk;

        /// <summary>The skeleton body texture for a body type; Female and Fat fall back to Male, children get none.</summary>
        public static string BodyGraphicPathFor(BodyTypeDef bodyType)
        {
            if (bodyType == null || bodyType == BodyTypeDefOf.Child || bodyType == BodyTypeDefOf.Baby)
            {
                return null;
            }
            return BodyPath + (IsSkeletonBodyType(bodyType) ? bodyType.defName : BodyTypeDefOf.Male.defName);
        }

        /// <summary>Swaps an adult body type the skeleton art does not cover (Female, Fat) for Male.</summary>
        public static void FixBodyType(Pawn pawn)
        {
            if (pawn.story?.bodyType == null || pawn.DevelopmentalStage.Juvenile() || IsSkeletonBodyType(pawn.story.bodyType))
            {
                return;
            }
            pawn.story.bodyType = BodyTypeDefOf.Male;
        }
    }
}
