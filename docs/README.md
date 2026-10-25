# Rimshi Skeletons docs

The design and project documents. These files are the source of truth; edit them here.

## Design (docs/design)

| File | What it holds |
| --- | --- |
| xenotype-port.md | What Rim-Skeletons (Continued) does, how each part maps onto Biotech genes and patches, soft dependencies, assets, open questions and build order. |
| recall-horde.md | The skeleton recall array (built from the mechband antenna), the berserk factionless horde it calls, failsafe cores, reprogramming captured skeletons, and the reproduction block. |
| kenshi-lore-and-repair.md | Kenshi lore and how it themes the mod, skeleton repair kits (Crafting-based repair work), and the skeleton repair bed. |
| skeleton-chips.md | Skeleton chips: the genepack-like system for modding skeletons (chip extractor, processor, assembler), kept apart from Biotech's gene trade, and why it does not use Vanilla Expanded Framework. |

## Project (docs/project)

| File | What it holds |
| --- | --- |
| test-checklist.md | What has been seen working in game, from the `[Rimshi][T]` log lines. Updated with tools/rimshi_testlog.py. |
| changelog.md | Dated changes, newest first. Rows arrive as fragment files in changelog/ and CI folds them in on main. |

## Conventions

- Every change gets a dated changelog row, as its own fragment file in `project/changelog/` (see the README there). Never edit the changelog table on a branch.
- No em or en dashes anywhere.
- Drafts for Matthew's review are marked "(draft)" or "Proposed:".
- Every def name starts with `Rimshi_`. C# lives in the `RimshiSkeletons` namespace.
- Vanilla behaviour is checked against the decompiled source on Matthew's PC (`<save data folder>\Decompiled\Assembly-CSharp`), not guessed. Other mods we patch are decompiled the same way first.
- Content that needs another mod has a fallback or the mod is a declared requirement (see Soft dependencies in design/xenotype-port.md).
