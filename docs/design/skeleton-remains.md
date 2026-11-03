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

## No rot

Asked for by Matthew on 2026-11-03: skeleton corpses must not rot or desiccate into organic skeletons, but may still deteriorate (outdoors, rain, fire, damage).

| Part | How |
| --- | --- |
| No rot | `CompRottable.Active` postfix returns false for a skeleton corpse: no rot progress, no rot damage, no rot stink, no rot line on the inspect pane, and the "time until rot" readings say never. Deterioration is a separate system (`SteadyEnvironmentEffects`) and still runs. |
| Held at zero | `CompRottable.RotProgress` setter prefix keeps a skeleton corpse's rot progress at 0, so `RotImmediately` (scenarios, ruins, other mods) cannot rot it either. |
| Old saves | A skeleton corpse saved while rotting or desiccated is made fresh in `CompRottable.PostExposeData` (post-load, graves and caravans too) and again on `Corpse.SpawnSetup`, which also refreshes its graphics. Logged once per corpse as `remains | rot cleared`. |

## Why the scrap bill seemed to ignore corpses (2026-11-03)

Matthew reported the scrap bill not taking skeleton corpses lying on the map. The 1.7 bill code (`WorkGiver_DoBill`, `ThingFilter`, `Bill.IsFixedOrAllowedIngredient`) checks out: the recipe's ingredient filter re-allows the colonist and stranger corpse special filters under Humanlike corpses, and our postfix keeps only skeleton corpses. In his save of that evening the only skeleton corpse was in a grave; the organic skeletons on the map were desiccated human corpses (Shek, hivers, baseliners and so on), which the bill rightly refuses. Two things that do stop it, both vanilla: hostile corpses outside the home area start forbidden (`Pawn.Kill` calls `SetForbiddenIfOutsideHomeArea`), and the bill's search radius. A diagnostic `WorkGiver_DoBill.TryFindBestBillIngredients` postfix now logs `remains | scrap skipped` with the reason, once per corpse, whenever a scrap bill finds nothing while a skeleton corpse lies on the map. The self-test now checks the ingredient filter as well as the bill (what the bill search really asks).

## Eating

| Part | How |
| --- | --- |
| Never ingestible | `Corpse.IngestibleNow` postfix returns false. Food searches, eat orders (right-click), animals grazing on corpses, caravan meals and food policies all ask this. |
| Predators | Hunting is allowed (Matthew): `FoodUtility` still picks skeletons as prey. After the kill, the predator hunt job goes straight to chewing the corpse without asking whether it can, so a `Toils_Ingest.ChewIngestible` postfix adds a fail condition when the target is a skeleton corpse; the job ends and the animal looks for food again. The same fail condition covers every job that chews (ingest jobs, modded ones). |
| Last guard | `Thing.Ingested` prefix: a skeleton corpse gives 0 nutrition and is left whole, without vanilla's "ingested IngestibleNow=false thing" error. Logged as `remains | ingest blocked`, which should never appear. |

## Not covered

- The corpse's info card still lists vanilla meat and leather amounts (display only).
