# Maintenance and needs

Asked for by Matthew on 2026-10-25. Skeletons have no hunger. The food need becomes **maintenance**, met by stripping one industrial component about every two days. With Dubs Bad Hygiene loaded they also lose thirst and bladder, and hygiene becomes **self-cleaning**.

## Maintenance (food need)

The vanilla food need is kept rather than replaced, so food search, feeding, caravans, the ingest menu and food policies all keep working. Everything is keyed on the component maintenance gene, `Rimshi_FuelReactor` (defName and gene class keep their fuel reactor names because saves store both).

| Part | How |
| --- | --- |
| Label and tooltip | `Need.DrawOnGUI` and both `GetTipString`s are transpiled so every `Need.LabelCap` call asks `SkeletonNeeds.LabelCapFor` instead (a getter that small can be inlined past a plain patch). A tooltip postfix swaps the description for the Second Empire text. Data: `NeedRelabelExtension` on the gene (`Food` shown as "maintenance"). |
| What they consume | `ComponentIndustrial` gets `MaintenanceItemExtension`, `Nutrition` 0.7, and an ingestible block (food type Processed, one at a time, preferability MealAwful so every vanilla food policy except raw food allows it, the repair weld sound). `Patch_FoodIsSuitable` keeps it both ways: only maintenance pawns may consume tagged items, and they consume nothing else. Chemfuel and uranium are no longer edible. |
| Rate | `Patch_FoodFall` sets a fixed rate: one item's nutrition over `MaintenanceExtension.daysPerItem` (2) days, so 0.35 a day. Colonists eat at 30%, so a full bar lasts two days and one component (0.7) refills it. Metabolism, age, beds and hediffs no longer change it; the vanilla slowdown once hungry still applies. |
| Malnutrition | `Patch_NoMalnutrition` refuses any rise in malnutrition through `HealthUtility.AdjustSeverity` (the only way `Need_Food` adds it). Old malnutrition still wears off. |
| Mood | `Rimshi_MaintenanceNeed` with `ThoughtWorker_Maintenance`: "maintenance due" (-3) below 24%, "overdue" (-6) below 12%, then from "worn parts" (-10) at 0%, one stage worse per day out, down to "falling apart" (-22). It stands in for malnutrition's growing stages. |

Known side effects: components count toward the colony's food total and caravan food days, as chemfuel and uranium did before. Spacer components are not maintenance items. NPC skeletons are generated with ordinary travel food they cannot eat; without malnutrition this only costs them mood.

## Dubs Bad Hygiene (optional)

On the mechanical nature gene (all-skeleton rules live there), by name, so nothing errors without the mod:

- `removedNeeds` DBHThirst and Bladder. Dubs Bad Hygiene's own `ShouldHaveNeed` postfix decides its needs and would override a gene's `disablesNeeds`, so `Patch_ShouldHaveNeed` runs last and turns them off.
- `relabels` Hygiene as "self-cleaning" with the motor debris tooltip.
- `statFactorsIfLoaded` HygieneRateMultiplier x0.5 (Dubs Bad Hygiene's own stat, read by its hygiene fall rate), added to the gene at startup.

The gene's effect list gains a line for each need removed or relabelled, only when that need exists.

## Tests

Startup lines `defs | maintenance item` and `defs | need changes`; `diet | ate component`, `maintenance | no malnutrition`. Self-tests: eats components, maintenance rate, one component, maintenance label, no malnutrition, maintenance thought, dubs needs, human refuses components.
