# Temporarily power down

Matthew's ask, 2026-11-01: on a skeleton, Anesthetize should read "Temporarily power down", and the operation's bill should describe putting the unit in maintenance mode for hardware modifications.

Code: `Source/RimshiSkeletons/Skeleton/SkeletonPowerDown.cs`. Defs: `Defs/RecipeDefs/Rimshi_PowerDown.xml`, `Defs/HediffDefs/Rimshi_MaintenanceMode.xml`.

- **The operation.** `Rimshi_PowerDown` ("temporarily power down") is a Human recipe whose worker only offers it on skeletons (mechanical nature). Vanilla's `Anesthetize` is hidden on skeletons (an `AvailableOnNow` postfix), so a skeleton's operations list has power down where a human's has Anesthetize. Like Anesthetize it takes no work and has the same success chance; unlike it, it uses no medicine, since skeletons take none.
- **Maintenance mode.** Vanilla puts the patient under as any operation with `anesthetize` starts (`HealthUtility.TryAnesthetize`), and surgical inspection and some quests add anaesthetic directly. On a skeleton, every anaesthetic it would get is swapped for `Rimshi_MaintenanceMode` where it is added (both `AddHediff` overloads). So skeletons are powered down for any surgery, not only this one. The hediff copies Anesthetic's severity, duration and capacity stages without the vomiting, confusion and metabolism; stages are "powered down", "booting" and "reinitializing".
- Player text follows the no-spoiler rule: the description says what state the unit is in and why, nothing about how long it lasts.

## Tests

Self-test `power down`: offered on the skeleton only, Anesthetize on the baseliner only, and `TryAnesthetize` on the skeleton gives maintenance mode and no anaesthetic. Play lines: `powerdown | powered down`, `powerdown | anesthetic swapped`.
