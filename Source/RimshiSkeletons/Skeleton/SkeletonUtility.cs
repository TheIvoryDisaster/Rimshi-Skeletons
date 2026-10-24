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
