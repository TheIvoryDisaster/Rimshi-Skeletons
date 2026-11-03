using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimshiSkeletons
{
    /// <summary>
    /// A dead skeleton is scrap, not meat. Skeleton corpses share Corpse_Human with every other human, so the ThingDef cannot
    /// tell them apart; everything here keys on the inner pawn's mechanical nature gene instead.
    /// Butchering: no butchery recipe (any recipe with the Butchery special product, vanilla or modded) accepts a skeleton
    /// corpse, and our own Rimshi_ScrapSkeletonRemains (crafting spot, machining bench) accepts nothing else. That recipe
    /// is a Butchery recipe too, so vanilla hands the corpse to Corpse.ButcherProducts, which yields scrap for a skeleton
    /// (as does any other mod that butchers one anyway): 1-5 each of steel, plasteel and uranium and at most one
    /// component. The vanilla products are never made, so there is no meat, no leather, no blood and no butchered
    /// humanlike thought.
    /// Rot: a skeleton corpse never rots or desiccates. Its CompRottable is inactive (no rot progress, no rot damage, no rot
    /// stink, no rot line on the inspect pane), any attempt to set rot progress (RotImmediately, mods) is held at zero, and
    /// a skeleton corpse that rotted before this fix is made fresh again on load and on spawn, so it stops being drawn as a
    /// rotting or desiccated organic body. Deterioration (outdoors, rain, fire, damage) is not rot and still happens.
    /// Eating: a skeleton corpse is never ingestible (Corpse.IngestibleNow), which takes it out of every food search,
    /// eat order and caravan meal. Predators may still hunt skeletons; once the prey is down, the chewing toil fails
    /// instead of eating, and Thing.Ingested is a last guard against anything else that tries.
    /// </summary>
    public static class SkeletonRemains
    {
        public const int MinScrap = 1;
        public const int MaxScrap = 5;
        public const float ComponentChance = 0.5f;

        private static readonly HashSet<string> loggedRefusals = new HashSet<string>();

        public static RecipeDef ScrapRecipe => RimshiDefOf.Rimshi_ScrapSkeletonRemains;

        public static bool IsSkeletonCorpse(Thing thing) => thing is Corpse corpse && SkeletonUtility.HasMechanicalNature(corpse.InnerPawn);

        public static bool IsButchery(RecipeDef recipe) =>
            recipe?.specialProducts != null && recipe.specialProducts.Contains(SpecialProductType.Butchery);

        /// <summary>What scrapping a skeleton corpse yields.</summary>
        public static IEnumerable<Thing> ScrapProducts(Corpse corpse)
        {
            // Apparel, weapons and inventory go on the floor as they would from a butchered human.
            if (corpse.SpawnedOrAnyParentSpawned && corpse.AnythingToStrip())
            {
                corpse.Strip();
            }
            var products = new List<Thing>();
            foreach (ThingDef def in new[] { ThingDefOf.Steel, ThingDefOf.Plasteel, ThingDefOf.Uranium })
            {
                Thing scrap = ThingMaker.MakeThing(def);
                scrap.stackCount = Rand.RangeInclusive(MinScrap, MaxScrap);
                products.Add(scrap);
            }
            if (Rand.Chance(ComponentChance))
            {
                products.Add(ThingMaker.MakeThing(ThingDefOf.ComponentIndustrial));
            }
            RimshiLog.Test("remains", "scrapped", corpse.InnerPawn?.LabelShort + " yielded "
                + string.Join(", ", products.ConvertAll(p => p.stackCount + " " + p.def.defName)));
            return products;
        }

        private static readonly AccessTools.FieldRef<CompRottable, float> rotProgressField =
            AccessTools.FieldRefAccess<CompRottable, float>("rotProgressInt");

        /// <summary>
        /// Makes a skeleton corpse that rotted before rot was disabled fresh again. Writes the field so it is safe while
        /// loading; when the corpse is spawned the graphics are refreshed too.
        /// </summary>
        public static void ClearRot(Corpse corpse, string when)
        {
            CompRottable rot = corpse?.GetComp<CompRottable>();
            if (rot == null || rotProgressField(rot) <= 0f || !IsSkeletonCorpse(corpse))
            {
                return;
            }
            RotStage stage = rot.Stage;
            rotProgressField(rot) = 0f;
            if (stage != RotStage.Fresh && corpse.Spawned)
            {
                corpse.RotStageChanged();
            }
            if (stage != RotStage.Fresh && FirstRefusal(corpse, "rot cleared"))
            {
                RimshiLog.Test("remains", "rot cleared", "the corpse of " + corpse.InnerPawn.LabelShort + " was " + stage + ", now fresh (" + when + ")");
            }
        }

        /// <summary>
        /// Why a scrap bill's ingredient search passed over a skeleton corpse on the map, in the order vanilla checks
        /// (WorkGiver_DoBill.TryFindBestIngredientsHelper), or null when nothing stood in the way.
        /// </summary>
        public static string ScrapSkipReason(Bill bill, Pawn pawn, Thing billGiver, Corpse corpse)
        {
            if (!bill.IsFixedOrAllowedIngredient(corpse) || !bill.recipe.ingredients.Any(i => i.filter.Allows(corpse)))
            {
                return "the bill's ingredient filter refuses it";
            }
            if (corpse.IsForbidden(pawn))
            {
                return "it is forbidden";
            }
            float radius = bill.ingredientSearchRadius;
            if ((corpse.Position - billGiver.Position).LengthHorizontalSquared >= radius * radius)
            {
                return "it is outside the bill's search radius";
            }
            if (!pawn.CanReserve(corpse))
            {
                return "someone else has it reserved";
            }
            if (!pawn.CanReach(corpse, PathEndMode.ClosestTouch, Danger.Deadly))
            {
                return "it cannot be reached";
            }
            return null;
        }

        /// <summary>True the first time a refusal of this kind is seen for this corpse, so each is logged once.</summary>
        public static bool FirstRefusal(Thing corpse, string key) => loggedRefusals.Add(key + corpse.thingIDNumber);
    }

    /// <summary>Butchery recipes refuse skeleton corpses; the scrap recipe takes nothing else.</summary>
    [HarmonyPatch(typeof(Bill), nameof(Bill.IsFixedOrAllowedIngredient), new[] { typeof(Thing) })]
    public static class Patch_BillIsFixedOrAllowedIngredient
    {
        // Arguments by position: Harmony matches names, and the vanilla parameter names are not ours to rely on.
        public static void Postfix(Bill __instance, Thing __0, ref bool __result)
        {
            Thing thing = __0;
            if (!__result || !(thing is Corpse))
            {
                return;
            }
            RecipeDef recipe = __instance.recipe;
            if (recipe == SkeletonRemains.ScrapRecipe)
            {
                __result = SkeletonRemains.IsSkeletonCorpse(thing);
            }
            else if (SkeletonRemains.IsButchery(recipe) && SkeletonRemains.IsSkeletonCorpse(thing))
            {
                __result = false;
                if (SkeletonRemains.FirstRefusal(thing, "butchery refused"))
                {
                    RimshiLog.Test("remains", "butchery refused", recipe.defName + " skipped the corpse of " + ((Corpse)thing).InnerPawn.LabelShort);
                }
            }
        }
    }

    /// <summary>A skeleton corpse yields scrap whoever takes it apart.</summary>
    [HarmonyPatch(typeof(Corpse), nameof(Corpse.ButcherProducts))]
    public static class Patch_CorpseButcherProducts
    {
        public static void Postfix(Corpse __instance, ref IEnumerable<Thing> __result)
        {
            if (SkeletonRemains.IsSkeletonCorpse(__instance))
            {
                __result = SkeletonRemains.ScrapProducts(__instance);
            }
        }
    }

    /// <summary>Nothing eats a skeleton corpse.</summary>
    [HarmonyPatch(typeof(Corpse), nameof(Corpse.IngestibleNow), MethodType.Getter)]
    public static class Patch_CorpseIngestibleNow
    {
        public static void Postfix(Corpse __instance, ref bool __result)
        {
            if (__result && SkeletonRemains.IsSkeletonCorpse(__instance))
            {
                __result = false;
            }
        }
    }

    /// <summary>
    /// A predator that brought down a skeleton goes to eat the corpse without asking whether it can; the chewing fails
    /// instead, so the hunt ends there.
    /// </summary>
    [HarmonyPatch(typeof(Toils_Ingest), nameof(Toils_Ingest.ChewIngestible), new[] { typeof(Pawn), typeof(float), typeof(TargetIndex), typeof(TargetIndex) })]
    public static class Patch_ChewIngestible
    {
        public static void Postfix(Toil __result, TargetIndex __2)
        {
            TargetIndex ingestibleInd = __2;
            Toil toil = __result;
            toil.FailOn(() =>
            {
                Thing food = toil.actor?.CurJob?.GetTarget(ingestibleInd).Thing;
                if (!SkeletonRemains.IsSkeletonCorpse(food))
                {
                    return false;
                }
                if (SkeletonRemains.FirstRefusal(food, "eating refused"))
                {
                    RimshiLog.Test("remains", "eating refused", toil.actor.LabelShort + " could not eat the corpse of " + ((Corpse)food).InnerPawn.LabelShort);
                }
                return true;
            });
        }
    }

    /// <summary>Last guard: anything that still ingests a skeleton corpse gets nothing and the corpse stays whole.</summary>
    [HarmonyPatch(typeof(Thing), nameof(Thing.Ingested))]
    public static class Patch_ThingIngested
    {
        public static bool Prefix(Thing __instance, Pawn __0, ref float __result)
        {
            Pawn ingester = __0;
            if (!SkeletonRemains.IsSkeletonCorpse(__instance))
            {
                return true;
            }
            if (SkeletonRemains.FirstRefusal(__instance, "ingest blocked"))
            {
                RimshiLog.Test("remains", "ingest blocked", ingester?.LabelShort + " tried to ingest the corpse of " + ((Corpse)__instance).InnerPawn.LabelShort);
            }
            __result = 0f;
            return false;
        }
    }

    /// <summary>A skeleton corpse does not rot: its rot comp is never active.</summary>
    [HarmonyPatch(typeof(CompRottable), nameof(CompRottable.Active), MethodType.Getter)]
    public static class Patch_CompRottableActive
    {
        public static void Postfix(CompRottable __instance, ref bool __result)
        {
            if (__result && SkeletonRemains.IsSkeletonCorpse(__instance.parent))
            {
                __result = false;
            }
        }
    }

    /// <summary>Nothing can push a skeleton corpse's rot progress up (RotImmediately, other mods).</summary>
    [HarmonyPatch(typeof(CompRottable), nameof(CompRottable.RotProgress), MethodType.Setter)]
    public static class Patch_CompRottableRotProgress
    {
        public static void Prefix(CompRottable __instance, ref float __0)
        {
            if (__0 > 0f && SkeletonRemains.IsSkeletonCorpse(__instance.parent))
            {
                __0 = 0f;
            }
        }
    }

    /// <summary>A skeleton corpse saved while rotting or desiccated loads fresh (graves and caravans included).</summary>
    [HarmonyPatch(typeof(CompRottable), nameof(CompRottable.PostExposeData))]
    public static class Patch_CompRottablePostExposeData
    {
        public static void Postfix(CompRottable __instance)
        {
            if (Scribe.mode == LoadSaveMode.PostLoadInit && __instance.parent is Corpse corpse)
            {
                SkeletonRemains.ClearRot(corpse, "loaded");
            }
        }
    }

    /// <summary>Second chance for the same, once the corpse is on a map, so the fresh body is drawn.</summary>
    [HarmonyPatch(typeof(Corpse), nameof(Corpse.SpawnSetup))]
    public static class Patch_CorpseSpawnSetup
    {
        public static void Postfix(Corpse __instance)
        {
            SkeletonRemains.ClearRot(__instance, "spawned");
        }
    }

    /// <summary>
    /// Diagnostics only: when a scrap bill finds nothing to scrap while a skeleton corpse lies on the map, log once per
    /// corpse and reason why the search passed it over (forbidden, filter, radius, reserved, unreachable).
    /// </summary>
    [HarmonyPatch(typeof(WorkGiver_DoBill), "TryFindBestBillIngredients")]
    public static class Patch_WorkGiverDoBillTryFindBestBillIngredients
    {
        public static void Postfix(Bill __0, Pawn __1, Thing __2, bool __result)
        {
            Bill bill = __0;
            Pawn pawn = __1;
            Thing billGiver = __2;
            if (__result || bill?.recipe != SkeletonRemains.ScrapRecipe || pawn?.Map == null || billGiver == null)
            {
                return;
            }
            foreach (Thing thing in pawn.Map.listerThings.ThingsInGroup(ThingRequestGroup.Corpse))
            {
                if (!(thing is Corpse corpse) || !corpse.Spawned || !SkeletonRemains.IsSkeletonCorpse(corpse))
                {
                    continue;
                }
                string reason = SkeletonRemains.ScrapSkipReason(bill, pawn, billGiver, corpse) ?? "no reason found";
                if (SkeletonRemains.FirstRefusal(corpse, "scrap skipped " + reason))
                {
                    RimshiLog.Test("remains", "scrap skipped", pawn.LabelShort + " passed over the corpse of " + corpse.InnerPawn.LabelShort + ": " + reason);
                }
            }
        }
    }
}
