using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimshiSkeletons
{
    /// <summary>Marks a ThingDef as a maintenance item (the industrial component, added by Patches/Rimshi_ComponentMaintenance.xml).</summary>
    public class MaintenanceItemExtension : DefModExtension
    {
    }

    /// <summary>Settings for the component maintenance gene: one maintenance item lasts this many days.</summary>
    public class MaintenanceExtension : DefModExtension
    {
        public float daysPerItem = 2f;
    }

    /// <summary>
    /// Needs a gene changes, by NeedDef name so needs from mods that are not loaded are skipped: removed needs, needs shown
    /// under another label and description, and stat factors added to the gene only when the stat exists.
    /// </summary>
    public class NeedRelabelExtension : DefModExtension
    {
        public List<string> removedNeeds;
        public List<NeedRelabel> relabels;
        public List<OptionalStatFactor> statFactorsIfLoaded;
    }

    public class NeedRelabel
    {
        public string need;
        public string label;
        public string description;
    }

    public class OptionalStatFactor
    {
        public string stat;
        public float value = 1f;
    }

    /// <summary>
    /// Skeleton needs. The food need stays (so food search, caravans and the ingest menu work unchanged) but is shown as
    /// "maintenance", drains at a fixed rate so one component lasts MaintenanceExtension.daysPerItem days, and never
    /// causes malnutrition. Needs from other mods are removed or relabelled through NeedRelabelExtension (Dubs Bad
    /// Hygiene: no thirst or bladder, hygiene shown as self-cleaning).
    /// </summary>
    public static class SkeletonNeeds
    {
        private static readonly AccessTools.FieldRef<Need, Pawn> NeedPawn = AccessTools.FieldRefAccess<Need, Pawn>("pawn");

        private static Dictionary<NeedDef, List<KeyValuePair<GeneDef, NeedRelabel>>> relabels;
        private static Dictionary<NeedDef, List<GeneDef>> removed;
        private static float maintenancePerTick = -1f;

        public static ThingDef MaintenanceItem => DefDatabase<ThingDef>.AllDefsListForReading.FirstOrDefault(d => d.HasModExtension<MaintenanceItemExtension>());

        public static float DaysPerItem => RimshiDefOf.Rimshi_FuelReactor.GetModExtension<MaintenanceExtension>()?.daysPerItem ?? 2f;

        /// <summary>Maintenance lost per tick while fed: one item's nutrition over daysPerItem days.</summary>
        public static float MaintenancePerTick
        {
            get
            {
                if (maintenancePerTick < 0f)
                {
                    float nutrition = MaintenanceItem?.GetStatValueAbstract(StatDefOf.Nutrition) ?? 0f;
                    maintenancePerTick = nutrition > 0f ? nutrition / (DaysPerItem * GenDate.TicksPerDay) : 0f;
                }
                return maintenancePerTick;
            }
        }

        private static void EnsureLookups()
        {
            if (relabels != null)
            {
                return;
            }
            relabels = new Dictionary<NeedDef, List<KeyValuePair<GeneDef, NeedRelabel>>>();
            removed = new Dictionary<NeedDef, List<GeneDef>>();
            foreach (GeneDef gene in DefDatabase<GeneDef>.AllDefsListForReading)
            {
                NeedRelabelExtension ext = gene.GetModExtension<NeedRelabelExtension>();
                if (ext == null)
                {
                    continue;
                }
                foreach (NeedRelabel relabel in ext.relabels ?? Enumerable.Empty<NeedRelabel>())
                {
                    NeedDef need = DefDatabase<NeedDef>.GetNamedSilentFail(relabel.need);
                    if (need != null)
                    {
                        if (!relabels.TryGetValue(need, out var list))
                        {
                            relabels[need] = list = new List<KeyValuePair<GeneDef, NeedRelabel>>();
                        }
                        list.Add(new KeyValuePair<GeneDef, NeedRelabel>(gene, relabel));
                    }
                }
                foreach (string name in ext.removedNeeds ?? Enumerable.Empty<string>())
                {
                    NeedDef need = DefDatabase<NeedDef>.GetNamedSilentFail(name);
                    if (need != null)
                    {
                        if (!removed.TryGetValue(need, out var list))
                        {
                            removed[need] = list = new List<GeneDef>();
                        }
                        list.Add(gene);
                    }
                }
            }
        }

        /// <summary>The relabel a pawn's genes give a need, or null.</summary>
        public static NeedRelabel RelabelFor(Pawn pawn, NeedDef need)
        {
            EnsureLookups();
            if (pawn?.genes == null || need == null || !relabels.TryGetValue(need, out var list))
            {
                return null;
            }
            for (int i = 0; i < list.Count; i++)
            {
                if (pawn.genes.HasActiveGene(list[i].Key))
                {
                    return list[i].Value;
                }
            }
            return null;
        }

        public static NeedRelabel RelabelFor(Need need) => need == null ? null : RelabelFor(NeedPawn(need), need.def);

        /// <summary>Stands in for Need.LabelCap in the need bar and its tooltip (see Patch_NeedLabel).</summary>
        public static string LabelCapFor(Need need)
        {
            NeedRelabel relabel = RelabelFor(need);
            return relabel != null ? relabel.label.CapitalizeFirst() : need.LabelCap;
        }

        public static bool RemovesNeed(Pawn pawn, NeedDef need)
        {
            EnsureLookups();
            if (pawn?.genes == null || need == null || !removed.TryGetValue(need, out var genes))
            {
                return false;
            }
            for (int i = 0; i < genes.Count; i++)
            {
                if (pawn.genes.HasActiveGene(genes[i]))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// At startup (from SkeletonDefs): adds the optional stat factors whose stats are loaded, and an effect line on
        /// the gene for each need it removes or relabels. Returns a summary for the log.
        /// </summary>
        public static string ApplyOptionalDefs()
        {
            EnsureLookups();
            var done = new List<string>();
            foreach (GeneDef gene in DefDatabase<GeneDef>.AllDefsListForReading)
            {
                NeedRelabelExtension ext = gene.GetModExtension<NeedRelabelExtension>();
                if (ext == null)
                {
                    continue;
                }
                foreach (OptionalStatFactor factor in ext.statFactorsIfLoaded ?? Enumerable.Empty<OptionalStatFactor>())
                {
                    StatDef stat = DefDatabase<StatDef>.GetNamedSilentFail(factor.stat);
                    if (stat == null)
                    {
                        continue;
                    }
                    if (gene.statFactors == null)
                    {
                        gene.statFactors = new List<StatModifier>();
                    }
                    if (!gene.statFactors.Any(m => m.stat == stat))
                    {
                        gene.statFactors.Add(new StatModifier { stat = stat, value = factor.value });
                    }
                    done.Add(gene.defName + " " + stat.defName + " x" + factor.value);
                }
                var effects = new List<string>();
                foreach (string name in ext.removedNeeds ?? Enumerable.Empty<string>())
                {
                    NeedDef need = DefDatabase<NeedDef>.GetNamedSilentFail(name);
                    if (need != null)
                    {
                        effects.Add("No " + need.label + " need");
                        done.Add(gene.defName + " removes " + need.defName);
                    }
                }
                foreach (NeedRelabel relabel in ext.relabels ?? Enumerable.Empty<NeedRelabel>())
                {
                    NeedDef need = DefDatabase<NeedDef>.GetNamedSilentFail(relabel.need);
                    if (need != null)
                    {
                        effects.Add(need.LabelCap + " need is shown as " + relabel.label);
                        done.Add(gene.defName + " relabels " + need.defName);
                    }
                }
                if (effects.Count > 0)
                {
                    if (gene.customEffectDescriptions == null)
                    {
                        gene.customEffectDescriptions = new List<string>();
                    }
                    gene.customEffectDescriptions.InsertRange(0, effects.Where(e => !gene.customEffectDescriptions.Contains(e)));
                }
            }
            return string.Join(", ", done);
        }
    }

    /// <summary>
    /// Component diet, both ways: only pawns with the component maintenance gene may consume maintenance items, and those
    /// pawns may consume nothing else. FoodIsSuitable is what food search, feeding, caravans and the ingest menu ask. A
    /// maintenance pawn skips the race check, so a mod that restricts what humans eat cannot stop skeletons, who are human.
    /// </summary>
    [HarmonyPatch(typeof(FoodUtility), nameof(FoodUtility.FoodIsSuitable))]
    public static class Patch_FoodIsSuitable
    {
        public static void Postfix(Pawn p, ThingDef food, ref bool __result)
        {
            if (food == null)
            {
                return;
            }
            bool item = food.HasModExtension<MaintenanceItemExtension>();
            bool maintained = SkeletonUtility.HasFuelReactor(p);
            if (item && maintained)
            {
                __result = p.needs?.food != null && food.ingestible != null && food.ingestible.preferability != FoodPreferability.Undefined;
            }
            else if (item != maintained)
            {
                __result = false;
            }
        }
    }

    /// <summary>
    /// Maintenance drains at a fixed rate (one item per daysPerItem days while fed), whatever the pawn's metabolism, age,
    /// bed or hediffs; the vanilla slowdown once hungry still applies. A rate another rule set to 0 (a holding platform)
    /// stays 0.
    /// </summary>
    [HarmonyPatch(typeof(Need_Food), nameof(Need_Food.FoodFallPerTickAssumingCategory))]
    public static class Patch_FoodFall
    {
        public static void Postfix(Pawn ___pawn, HungerCategory hunger, ref float __result)
        {
            if (__result > 0f && SkeletonUtility.HasFuelReactor(___pawn))
            {
                __result = SkeletonNeeds.MaintenancePerTick * hunger.HungerMultiplier();
            }
        }
    }

    /// <summary>Skeletons never get malnutrition: Need_Food raises it through AdjustSeverity while starving. Lowering it still works, so old malnutrition wears off.</summary>
    [HarmonyPatch(typeof(HealthUtility), nameof(HealthUtility.AdjustSeverity))]
    public static class Patch_NoMalnutrition
    {
        private static readonly HashSet<int> logged = new HashSet<int>();

        public static bool Prefix(Pawn pawn, HediffDef hdDef, float sevOffset)
        {
            if (hdDef != HediffDefOf.Malnutrition || sevOffset <= 0f || !SkeletonUtility.HasFuelReactor(pawn))
            {
                return true;
            }
            if (logged.Add(pawn.thingIDNumber))
            {
                RimshiLog.Test("maintenance", "no malnutrition", pawn.LabelShort + ": out of maintenance, malnutrition refused");
            }
            return false;
        }
    }

    /// <summary>
    /// Food policies saved before components were edible never allow them (a saved filter does not pick up new foods),
    /// which left skeletons in older saves ignoring components when hungry. Once per save, when the mod first sees it
    /// (new or loaded), every policy that allows any food also allows the maintenance item, and a "Skeletons" policy
    /// allowing only the maintenance item is added, so players browsing policies see what skeletons eat. After that the
    /// policies are the player's: a disallowed component or a deleted Skeletons policy stays that way.
    /// </summary>
    public static class SkeletonFoodPolicies
    {
        public const string PolicyLabel = "Skeletons";

        public static FoodPolicy SkeletonPolicy => Current.Game?.foodRestrictionDatabase?.AllFoodRestrictions.FirstOrDefault(p => p.label == PolicyLabel);

        public static void Apply()
        {
            FoodRestrictionDatabase database = Current.Game?.foodRestrictionDatabase;
            ThingDef item = SkeletonNeeds.MaintenanceItem;
            if (database == null || item == null)
            {
                return;
            }
            var allowed = new List<string>();
            foreach (FoodPolicy policy in database.AllFoodRestrictions)
            {
                if (policy.filter.AllowedDefCount > 0 && !policy.filter.Allows(item))
                {
                    policy.filter.SetAllow(item, allow: true);
                    allowed.Add(policy.label);
                }
            }
            bool added = false;
            if (SkeletonPolicy == null)
            {
                FoodPolicy policy = database.MakeNewFoodRestriction();
                policy.label = PolicyLabel;
                policy.filter.SetDisallowAll();
                policy.filter.SetAllow(item, allow: true);
                added = true;
            }
            RimshiLog.Test("maintenance", "food policies", item.label + " allowed in " + (allowed.Count == 0 ? "no policies (already allowed)" : string.Join(", ", allowed))
                + (added ? "; added the " + PolicyLabel + " policy" : "; " + PolicyLabel + " policy already there"));
        }
    }

    /// <summary>
    /// Maintenance pawns keep one maintenance item in their inventory, as colonists pack a meal. Vanilla's food packing
    /// only takes stacks worth 0.8 nutrition (one component is 0.7) and only when the colony has plenty of human food, so
    /// when it gives nothing a skeleton without an item fetches one from within 30 cells.
    /// </summary>
    [HarmonyPatch(typeof(JobGiver_PackFood), "TryGiveJob")]
    public static class Patch_PackComponent
    {
        private const float SearchRadius = 30f;

        public static bool Prepare() => PatchTarget.Exists(typeof(JobGiver_PackFood), "TryGiveJob");

        public static bool Carries(Pawn pawn) => pawn.inventory.innerContainer.Any(t => t.def.HasModExtension<MaintenanceItemExtension>());

        public static void Postfix(Pawn pawn, ref Job __result)
        {
            if (__result != null || pawn.inventory == null || !pawn.Spawned || SkeletonNeeds.MaintenanceItem == null || !SkeletonUtility.HasFuelReactor(pawn) || Carries(pawn) || MassUtility.IsOverEncumbered(pawn))
            {
                return;
            }
            Thing item = GenClosest.ClosestThing_Global_Reachable(pawn.Position, pawn.Map, pawn.Map.listerThings.ThingsOfDef(SkeletonNeeds.MaintenanceItem),
                PathEndMode.ClosestTouch, TraverseParms.For(pawn), SearchRadius,
                t => !t.IsForbidden(pawn) && pawn.CanReserve(t) && pawn.WillEat(t) && MassUtility.CountToPickUpUntilOverEncumbered(pawn, t) > 0);
            if (item == null)
            {
                return;
            }
            __result = JobMaker.MakeJob(JobDefOf.TakeInventory, item);
            __result.count = 1;
            RimshiLog.Test("maintenance", "packed", pawn.LabelShort + " picking up 1 " + item.def.defName + " to carry, from " + item.Position);
        }
    }

    /// <summary>
    /// Shows a relabelled need under its new name: every Need.LabelCap call in the need bar and the need tooltips goes
    /// through SkeletonNeeds.LabelCapFor instead. A transpiler, because a getter this small can be inlined past a patch.
    /// </summary>
    [HarmonyPatch]
    public static class Patch_NeedLabel
    {
        private static readonly MethodInfo LabelCap = AccessTools.PropertyGetter(typeof(Need), nameof(Need.LabelCap));
        private static readonly MethodInfo LabelCapFor = AccessTools.Method(typeof(SkeletonNeeds), nameof(SkeletonNeeds.LabelCapFor));

        public static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(Need), nameof(Need.DrawOnGUI));
            yield return AccessTools.Method(typeof(Need), nameof(Need.GetTipString));
            yield return AccessTools.Method(typeof(Need_Food), nameof(Need_Food.GetTipString));
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original)
        {
            int swapped = 0;
            foreach (CodeInstruction code in instructions)
            {
                if (code.Calls(LabelCap))
                {
                    code.opcode = System.Reflection.Emit.OpCodes.Call;
                    code.operand = LabelCapFor;
                    swapped++;
                }
                yield return code;
            }
            if (swapped == 0)
            {
                RimshiLog.Fail("maintenance", "need label", original.DeclaringType?.Name + "." + original.Name + " has no LabelCap call; relabelled needs show their old name there");
            }
        }
    }

    /// <summary>Swaps a relabelled need's description in its tooltip. Need.GetTipString covers needs from mods that call base (Dubs Bad Hygiene's hygiene).</summary>
    [HarmonyPatch]
    public static class Patch_NeedTip
    {
        public static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(Need), nameof(Need.GetTipString));
            yield return AccessTools.Method(typeof(Need_Food), nameof(Need_Food.GetTipString));
        }

        public static void Postfix(Need __instance, ref string __result)
        {
            NeedRelabel relabel = SkeletonNeeds.RelabelFor(__instance);
            string description = __instance.def.description;
            if (relabel != null && __result != null && !description.NullOrEmpty())
            {
                __result = __result.Replace(description, relabel.description);
            }
        }
    }

    /// <summary>
    /// Needs a skeleton gene removes. Runs last, after Dubs Bad Hygiene's own ShouldHaveNeed postfix, which sets the
    /// result for its needs and so overrides a gene's disablesNeeds.
    /// </summary>
    [HarmonyPatch(typeof(Pawn_NeedsTracker), "ShouldHaveNeed")]
    public static class Patch_ShouldHaveNeed
    {
        public static bool Prepare() => PatchTarget.Exists(typeof(Pawn_NeedsTracker), "ShouldHaveNeed");

        [HarmonyPriority(Priority.Last)]
        public static void Postfix(Pawn ___pawn, NeedDef nd, ref bool __result)
        {
            if (__result && SkeletonNeeds.RemovesNeed(___pawn, nd))
            {
                __result = false;
            }
        }
    }

    /// <summary>
    /// Maintenance mood: stage 0 while maintenance is due, 1 while overdue, then from 2 on the first day out of
    /// maintenance items, one stage more per further day up to MaxStage. Stands in for malnutrition's growing stages.
    /// </summary>
    public class ThoughtWorker_Maintenance : ThoughtWorker
    {
        public const int MaxStage = 6;

        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            Need_Food food = p.needs?.food;
            if (food == null)
            {
                return ThoughtState.Inactive;
            }
            switch (food.CurCategory)
            {
                case HungerCategory.Hungry:
                    return ThoughtState.ActiveAtStage(0);
                case HungerCategory.UrgentlyHungry:
                    return ThoughtState.ActiveAtStage(1);
                case HungerCategory.Starving:
                    return ThoughtState.ActiveAtStage(Math.Min(MaxStage, 2 + food.TicksStarving / GenDate.TicksPerDay));
                default:
                    return ThoughtState.Inactive;
            }
        }
    }
}
