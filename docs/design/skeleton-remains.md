# Skeleton remains

Asked for by Matthew on 2026-10-27 (items 4 and 5 of that list). A dead skeleton is scrap, not meat: it cannot be butchered or eaten, and a new bill takes it apart for metal instead.

Skeleton corpses are `Corpse_Human` like every other human (the xenotype sits on the vanilla Human race), so no def can tell them apart. Everything keys on the inner pawn's mechanical nature gene (`SkeletonRemains.IsSkeletonCorpse`), in `Source/RimshiSkeletons/Skeleton/SkeletonRemains.cs`.

## Butchering and scrapping

| Part | How |
| --- | --- |
| No butchering | `Bill.IsFixedOrAllowedIngredient(Thing)` postfix: a recipe with the `Butchery` special product (butcher spot, butcher table, any mod's butchery) refuses a skeleton corpse. Bill workers use this check to pick ingredients, so the corpse is never hauled to a butcher. |
| Scrap recipe | `Rimshi_ScrapSkeletonRemains` ("scrap skeleton remains") at the crafting spot and machining table, no research. 450 work, Crafting skill, general labour speed. Its filter offers every humanlike corpse (the UI cannot single out skeletons); the same postfix lets it take only skeleton corpses. Colonist corpses are off by default, as for butchery. Rotten corpses are allowed. |
| Yield | The scrap recipe is a `Butchery` recipe, so vanilla asks `Corpse.ButcherProducts`. A postfix replaces the result for a skeleton corpse: 1-5 each of steel, plasteel and uranium (uniform, independently) and a 50% chance of one industrial component (`SkeletonRemains.MinScrap`, `MaxScrap`, `ComponentChance`). Skill and efficiency stats do not change it. Apparel, weapons and inventory are stripped onto the floor first. Any other mod that butchers a skeleton gets the same scrap. |
| Side effects skipped | The vanilla products enumerator never runs, so there is no meat, leather, blood filth, or butchered humanlike thought and history event, for the scrap bill as well. |

## Eating

| Part | How |
| --- | --- |
| Never ingestible | `Corpse.IngestibleNow` postfix returns false. Food searches, eat orders (right-click), animals grazing on corpses, caravan meals and food policies all ask this. |
| Predators | Hunting is allowed (Matthew): `FoodUtility` still picks skeletons as prey. After the kill, the predator hunt job goes straight to chewing the corpse without asking whether it can, so a `Toils_Ingest.ChewIngestible` postfix adds a fail condition when the target is a skeleton corpse; the job ends and the animal looks for food again. The same fail condition covers every job that chews (ingest jobs, modded ones). |
| Last guard | `Thing.Ingested` prefix: a skeleton corpse gives 0 nutrition and is left whole, without vanilla's "ingested IngestibleNow=false thing" error. Logged as `remains | ingest blocked`, which should never appear. |

## Not covered

- The corpse's info card still lists vanilla meat and leather amounts (display only).
- Skeleton corpses still rot like human ones; out of scope here.
