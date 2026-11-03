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
| Wear | See below: a full day at 0% wears a random non-brain part for 2. |
| Mood | `Rimshi_MaintenanceNeed` with `ThoughtWorker_Maintenance`: "maintenance due" (-3) below 24%, "overdue" (-6) below 12%, then from "worn parts" (-10) at 0%, one stage worse per day out, down to "falling apart" (-22). It stands in for malnutrition's growing stages. |

### Wear (2026-11-03)

Matthew: skeletons out of maintenance should take 2 damage a day to a random part that is not the brain. `SkeletonWear` (Skeleton/SkeletonWear.cs), via a postfix on `Need_Food.NeedInterval` (every 150 ticks):

| Choice | Why |
| --- | --- |
| One hit per full day at 0% (starving), the first one day after the bar empties | Matches the mood thought's daily stages and keeps the health tab readable (a few clear marks, not dozens of 0.3 specks). |
| Clock in `Gene_FuelReactor.wearTicks` (saved), counted in need intervals while starving and not frozen, reset when no longer starving | Like vanilla malnutrition: no wear in cryptosleep, while soft cycling (needs are frozen), or off-map where needs freeze. Old saves start at 0, so nobody takes a backlog of hits on load. |
| Part: any present part except consciousness sources (the brain) and parts inside an added part (a prosthetic's own fingers), uniform | Matthew's "random non-brain body part". Machine organs are added parts on the organ itself, so they can be worn. A part can be worn away (destroyed); torso, heart and the like can eventually kill. |
| `Rimshi_MaintenanceWear` damage (`DamageWorker_AddInjury`, not violence, no impact, no job interrupt), armour ignored, no propagation, no filth | Wear comes from inside, so apparel and the KLR arm do not stop it; the full 2 stays on the chosen part. Stats like `IncomingDamageFactor` from other mods still scale it. |
| `Rimshi_WornPart` injury ("worn", destroyed "worn away") | A plain `Hediff_Injury`: skeletons do not bleed it (#19), it never heals on its own (#58), repair kits and the repair bed fix it. |
| Message for player pawns on a map or in a caravan | The only warning; text says what is happening now, nothing about the schedule. |

Numbers are `wearDamage` and `wearIntervalDays` on the gene's `MaintenanceExtension`. Log line `maintenance | wear`; self-tests wear patch, wear clock, maintenance wear.

Found 2026-10-26 (Matthew: hungry skeletons ignored components): a food policy saved before components were edible never allows them, because a saved filter does not pick up new foods. Matthew chose to keep food policies working for skeletons rather than bypass them, so `SkeletonFoodPolicies.Apply` runs once per save, the first time the mod sees it (new or loaded game, flag `foodPoliciesSet` on `RimshiGameComponent`): every policy that allows any food also allows the maintenance item (raw food included; "nothing" policies stay empty), and a "Skeletons" policy allowing only the maintenance item is added so curious players see what skeletons eat. After that the policies are the player's, so a disallowed component or a deleted Skeletons policy stays that way. `Patch_PackComponent` postfixes `JobGiver_PackFood.TryGiveJob`: a maintenance pawn without a maintenance item in its inventory picks one up from within 30 cells, as colonists pack a meal (vanilla's packing needs a stack worth 0.8 nutrition and plenty of colony food, so it rarely fires for one 0.7 component).

Known side effects: components count toward the colony's food total and caravan food days, as chemfuel and uranium did before. Spacer components are not maintenance items. NPC skeletons are generated with ordinary travel food they cannot eat; without malnutrition this only costs them mood.

## Dubs Bad Hygiene (optional)

On the mechanical nature gene (all-skeleton rules live there), by name, so nothing errors without the mod:

- `removedNeeds` DBHThirst and Bladder. Dubs Bad Hygiene's own `ShouldHaveNeed` postfix decides its needs and would override a gene's `disablesNeeds`, so `Patch_ShouldHaveNeed` runs last and turns them off.
- `relabels` Hygiene as "self-cleaning" with the motor debris tooltip.
- `statFactorsIfLoaded` HygieneRateMultiplier x0.5 (Dubs Bad Hygiene's own stat, read by its hygiene fall rate), added to the gene at startup.

The gene's effect list gains a line for each need removed or relabelled, only when that need exists.

## Tests

Startup lines `defs | maintenance item` and `defs | need changes`; `diet | ate component`, `maintenance | no malnutrition`, `maintenance | wear`. Self-tests: eats components, maintenance rate, one component, maintenance label, no malnutrition, maintenance thought, dubs needs, human refuses components.

## Deathrest (repair bed as casket)

Replaced on 2026-10-26 by the CPU soft cycle (cpu-soft-cycle.md): no chemfuel, a powered 48 hour cycle. Kept below as the record of PR #23.

Asked for by Matthew on 2026-10-25, right after maintenance: skeletons deathrest like sanguophages, the skeleton repair bed is their casket, and one deathrest burns a modest amount of chemfuel (35, like a generator's tank). Extensions and capacity content come in a later milestone.

| Part | How |
| --- | --- |
| Gene | `Rimshi_Deathrest` with vanilla `Gene_Deathrest` and `enablesNeeds` Deathrest, in the xenotype. Vanilla deathrest rules apply: the need falls over 30 days, a full deathrest takes 4 days (less with efficiency buildings), deathrest exhaustion at 0, deathrest capacity 1, the wake gizmo and auto-wake. Skeletons in older saves get the gene on load (`SkeletonDeathrest.AddMissingGenes`, xenotype genes only). The need tooltip is relabelled to the reactor core text. |
| Bed | The repair bed gets a `CompRefuelable` (chemfuel, 35, `consumeFuelOnlyWhenUsed`, haulers top it up below half) and `CompRimshi_DeathrestFuel` (35 per deathrest). No `CompDeathrestBindable`: its rare tick rewrites the power output, which would break the bed's repair power draw, and nothing in the casket needs binding. Deathrest buildings in the room still link through vanilla's room scan. |
| Where | `Patch_DeathrestInRepairBed` postfixes `JobGiver_GetDeathrest.TryGiveJob`: the nearest roofed, fuelled, free repair bed replaces vanilla's bed or ground spot. No bed means no job, so the need runs down. The repair bed is medical, and vanilla lets pawns use medical beds only while seeking medical rest, so `Patch_DeathrestMedicalBed` counts a skeleton deathresting or on its way to deathrest (runs after the repair bed's own postfix). |
| Fuel | `Patch_DeathrestFuel` prefixes `Gene_Deathrest.TickDeathresting`: in a repair bed with fuel, it burns 35 / MinDeathrestTicks per tick; otherwise deathrest is paused (no progress, the need does not refill) with one message. Power is not needed; a powered bed also repairs the sleeper. |

Not done: no "start deathrest" right-click on the repair bed (vanilla's option rejects medical beds), the vanilla deathrest chamber room role and lines-to-casket overlays still look for the vanilla casket, and vanilla's sunlight checks still apply (the bed must be roofed).
