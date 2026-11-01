using System.Collections.Generic;
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
}
