using Verse;

namespace RimshiSkeletons
{
    /// <summary>
    /// The two halves of a skeleton's genes. Core genes (frame, chassis, mechanical nature, reactor, plating) are the
    /// fixed hardware every skeleton is built with: endogenes from the xenotype, never moved. Chips are the swappable half:
    /// they will be xenogenes that only the chip buildings extract, store, assemble and install, the way Biotech moves
    /// genes with genepacks. See docs/design/skeleton-chips.md.
    /// </summary>
    public enum SkeletonGeneKind
    {
        Core,
        Chip
    }

    /// <summary>
    /// Marks one of our gene defs as core or chip. Set on the abstract parents (Rimshi_SkeletonGeneBase,
    /// Rimshi_PlatingBase, Rimshi_SkeletonChipBase), so every gene we add picks it up by inheriting from one of them.
    /// </summary>
    public class SkeletonGeneExtension : DefModExtension
    {
        public SkeletonGeneKind kind = SkeletonGeneKind.Core;

        /// <summary>
        /// A module: a core gene the chip system and soft cycle facilities are meant to change later (suppress, swap or
        /// soften), unlike the fixed hardware. Set by Rimshi_SkeletonModuleBase. See docs/design/skeleton-modules.md.
        /// </summary>
        public bool modifiable;
    }

    public static class SkeletonChips
    {
        /// <summary>Gene display category for chips (Defs/GeneDefs/Rimshi_ChipGenes.xml).</summary>
        public const string ChipCategory = "Rimshi_SkeletonChips";

        public static SkeletonGeneKind? KindOf(GeneDef gene) => gene?.GetModExtension<SkeletonGeneExtension>()?.kind;

        public static bool IsCoreGene(GeneDef gene) => KindOf(gene) == SkeletonGeneKind.Core;

        public static bool IsChip(GeneDef gene) => KindOf(gene) == SkeletonGeneKind.Chip;

        /// <summary>
        /// Who the chip buildings will accept: pawns with the mechanical nature gene, the one gene that carries every
        /// rule holding for all skeletons. A custom xenotype without it is not a skeleton for chips either.
        /// </summary>
        public static bool CanTakeChips(Pawn pawn) => SkeletonUtility.HasActiveGene(pawn, RimshiDefOf.Rimshi_MechanicalNature);
    }
}
