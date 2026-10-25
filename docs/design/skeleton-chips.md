# Skeleton chips

Status: (draft) 2026-10-25. Groundwork only: gene kinds and the chip gene category are in; no chips or chip buildings yet.

## Goal

Matthew, 2026-10-25: buildings that modify skeletons the way the gene extractor, gene processor and gene assembler modify humans, using special chips instead of genepacks. Follow the genemodding pattern, as Vanilla Races Expanded - Android does, but as a separate system so skeleton content never mixes with regular gene modding.

## Vanilla Expanded Framework: not a dependency

Checked 2026-10-25 against VRE Android 1.6 (Workshop 2975771801, `vanillaracesexpanded.android`, decompiled `VREAndroids.dll`) and the VanillaExpandedFramework repository (`Source/VEF/Genes`).

- The android gene system lives entirely in VRE Android's own DLL: `AndroidGeneDef : GeneDef`, the Hardware and Subroutine gene categories, `Window_CreateAndroidBase : GeneCreationDialogBase` (its own gene editor), `Building_AndroidCreationStation`, `Building_AndroidBehavioristStation`, and the patches that keep android genes out of vanilla gene work (`GeneUtility_GenesInOrder_Patch`, `Dialog_CreateXenotype_DrawSearchRect_Patch`, `Building_GeneExtractor_CanAcceptPawn_Patch` and others).
- Of 301 decompiled files, one uses VEF: `GeneDefGenerator_ImpliedGeneDefs_Patch` copies `VEF.Genes.GeneExtension` onto implied skill genes for their background textures. The other VEF uses are XML: `GeneExtension` backgrounds on four gene defs and the PipeSystem neutroamine network. None of it is a reusable "second gene system".
- VEF's `Genes` module is gene cosmetics and behaviour flags (gene backgrounds, `hideGene`, `disableGeneExtraction`, custom blood, body graphics, egg laying). Nothing there touches genepacks, gene banks, the assembler or a separate gene pool.
- So taking the dependency would buy a gene icon background and a few flags we already cover (GeneProofing.cs does the gene lock), and cost a large hard requirement plus load order and version coupling.

Decision: no dependency. We copy the VRE Android pattern in our own code. If we ever want VEF's gene backgrounds for chips, add them as an optional `MayRequire="OskarPotocki.VanillaFactionsExpanded.Core"` mod extension on the chip base def, with our plain icon colour as the fallback.

## Pattern taken from VRE Android

| VRE Android | Rimshi skeletons |
| --- | --- |
| Hardware genes: fixed, part of being an android. | Core skeleton genes (frame, chassis, mechanical nature, persona, reactor, null chromosome, plating). Endogenes from the inheritable Skeleton xenotype. Never become chips. |
| Subroutine genes: programmable, cost power efficiency. | Chips. Xenogenes, so vanilla's split between endogenes and xenogenes keeps them apart from the core. |
| `AndroidGeneDef : GeneDef` marks its genes. | `SkeletonGeneExtension` (kind Core or Chip) on our abstract gene parents. An extension keeps every gene a plain `GeneDef`, so no XML class swap and no save change. |
| Own gene categories. | `Rimshi_Skeleton` (core) and `Rimshi_SkeletonChips` (chips). |
| Own editor window on `GeneCreationDialogBase`, with `GeneUtility.GenesInOrder` filtered by which window is open. | Same: our assembler window on `GeneCreationDialogBase`; outside it, chips are filtered out of vanilla gene lists. |
| Vanilla gene buildings refuse androids. | Already done: GeneProofing.cs refuses skeletons at the extractor, xenogerm implant, reimplant and absorb, and strips our genes from genepacks. |
| Modification at a station the pawn lies in. | Chips are items, so the flow is genepack-like (below). |

## Chip system

Vanilla's gene buildings are hard-wired to the vanilla types: `Building_GeneAssembler.GetGenepacks` asks each linked facility for `CompGenepackContainer` and the xenogerm dialog lists `Genepack`. That is what makes a clean split possible: if our types are siblings, not subclasses, vanilla never sees them.

- **Chip** (`Rimshi_SkeletonChip`): a `GeneSetHolderBase` subclass, the sibling of `Genepack`. It gets the gene view gizmo, description, stats and save code for free. Holds chip genes only. Not a `Genepack`, so the vanilla gene bank and assembler cannot load it.
- **Chip extractor**: a `Building_Enterable` like `Building_GeneExtractor`. A skeleton lies in it, one of its chip genes is copied into a new chip, and the skeleton gets a recalibration hediff (the analogue of genes regrowing) before it can be extracted again. Only chip genes are candidates; core genes never are. Accepts pawns with the mechanical nature gene.
- **Chip processor**: a facility linked to the chip assembler that raises its chip capacity, like the gene processor raises complexity. Uses our own stat (`Rimshi_ChipCapacityIncrease`), not `GeneticComplexityIncrease`, so it does nothing for the vanilla assembler.
- **Chip bank** (proposed): chip storage linked to the assembler, the analogue of the gene bank. Our own comp, not a `CompGenepackContainer` subclass, because `TryGetComp<CompGenepackContainer>` would find a subclass and hand our chips to vanilla. Could be folded into the processor if Matthew wants exactly three buildings.
- **Chip assembler**: works like the gene assembler. Our window lists chips from linked chip banks, checks capacity (sum of chip complexity against the assembler plus processors) and the skeleton's power budget (the metabolism stat), then takes work to produce a chipset.
- **Chipset** (the xenogerm analogue) and install: a crafter installs a chipset on a skeleton as a job, Crafting skill, the same model as skeleton repair. Installing removes the skeleton's current chip xenogenes and adds the new set. Core genes are never touched.

Separation rules, all keyed on `SkeletonChips.IsChip` / `SkeletonUtility.IsSkeletonGene`:

1. Chips never roll in gene sets (`canGenerateInGeneSet` false on the chip base) and never enter a genepack (existing GeneProofing strip).
2. Vanilla gene buildings already refuse skeletons, so chips cannot leave through them.
3. Chip buildings accept only pawns with the mechanical nature gene, the one gene that carries every rule for all skeletons (as the prosthetics lock does).
4. Chips only move through chip items and chip buildings.

## In place now

- `Source/RimshiSkeletons/Chips/SkeletonChips.cs`: `SkeletonGeneKind`, `SkeletonGeneExtension`, and `SkeletonChips` (`KindOf`, `IsCoreGene`, `IsChip`, `CanTakeChips`).
- `Defs/GeneDefs/Rimshi_ChipGenes.xml`: the `Rimshi_SkeletonChips` category and the abstract `Rimshi_SkeletonChipBase` parent. A chip is a `GeneDef` with `ParentName="Rimshi_SkeletonChipBase"`.
- `Rimshi_SkeletonGeneBase` and `Rimshi_PlatingBase` now mark their genes Core.
- `SkeletonUtility.IsSkeletonGene` matches by the extension (category kept as a fallback), so the gene lock covers chips too.
- Self-test `gene kinds`: every skeleton gene is marked core or chip and the chip category is loaded.

## Build order

1. First chips (a handful of skill and work chips, plus a couple of drawback chips that give back power), placed on skeletal imperium pawn kinds so chips exist in the world.
2. Chip item and chip extractor.
3. Chip bank, processor, assembler window, chipset and the install job.
4. Research: one project after the repair projects (BasicMechtech line, no mechanitor needed).
5. Filter chips out of `GeneUtility.GenesInOrder` and the xenotype editor outside our window.

## Open questions

- Should the vanilla custom xenotype editor offer chips? Core genes stay offered (Matthew's earlier call). Proposed: chips hidden there, so they only come from chip buildings.
- Exactly three buildings, or a separate chip bank as well?
- Where chips come from at first: skeleton faction loot and traders, quests, ancient sites, or craftable blank chips.
- Does a chipset replace all chips (like a xenogerm) or add to them up to a slot limit?
- Names and art for the buildings and the chip item.
