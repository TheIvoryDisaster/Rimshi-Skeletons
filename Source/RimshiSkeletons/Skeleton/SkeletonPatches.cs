using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimshiSkeletons
{
    /// <summary>Draws the skeleton body texture instead of the human one for pawns with the skeleton frame gene.</summary>
    [HarmonyPatch(typeof(PawnRenderNode_Body), nameof(PawnRenderNode_Body.GraphicFor))]
    public static class Patch_BodyGraphic
    {
        private static readonly HashSet<int> logged = new HashSet<int>();

        public static void Postfix(Pawn pawn, ref Graphic __result)
        {
            if (__result == null || !SkeletonUtility.HasFrame(pawn) || pawn.Drawer.renderer.CurRotDrawMode == RotDrawMode.Dessicated)
            {
                return;
            }
            string path = SkeletonUtility.BodyGraphicPathFor(pawn.story?.bodyType);
            if (path == null)
            {
                return;
            }
            __result = GraphicDatabase.Get<Graphic_Multi>(path, __result.Shader, Vector2.one, __result.color);
            if (logged.Add(pawn.thingIDNumber))
            {
                RimshiLog.Test("render", "body", pawn.LabelShort + ": " + path + ", head " + pawn.story?.headType?.defName + ", skin " + pawn.story?.SkinColor.ToString()
                    + ", gender " + pawn.gender + ", xenotype " + pawn.genes?.XenotypeLabel);
            }
        }
    }

    /// <summary>
    /// Fuel diet, both ways: only pawns with the fuel reactor gene may eat things tagged as fuel (chemfuel, uranium), and
    /// those pawns may eat nothing else. FoodIsSuitable is what food search, feeding, caravans and the ingest menu ask.
    /// </summary>
    [HarmonyPatch(typeof(FoodUtility), nameof(FoodUtility.FoodIsSuitable))]
    public static class Patch_FoodIsSuitable
    {
        public static void Postfix(Pawn p, ThingDef food, ref bool __result)
        {
            if (!__result || food == null)
            {
                return;
            }
            bool fuel = food.HasModExtension<FuelExtension>();
            if (fuel != SkeletonUtility.HasFuelReactor(p))
            {
                __result = false;
            }
        }
    }

    /// <summary>Skeletons burn fuel slowly: hunger rate times the fuel reactor's hungerRateFactor.</summary>
    [HarmonyPatch(typeof(Need_Food), nameof(Need_Food.FoodFallPerTickAssumingCategory))]
    public static class Patch_FoodFall
    {
        public static void Postfix(Pawn ___pawn, ref float __result)
        {
            if (SkeletonUtility.HasFuelReactor(___pawn))
            {
                __result *= RimshiDefOf.Rimshi_FuelReactor.GetModExtension<FuelReactorExtension>()?.hungerRateFactor ?? 1f;
            }
        }
    }

    /// <summary>Genderless pawns are generated without family, as Rim-Hivers does (the original's relation chances were all 0).</summary>
    [HarmonyPatch(typeof(PawnGenerator), "GeneratePawnRelations")]
    public static class Patch_GeneratePawnRelations
    {
        public static bool Prefix(Pawn pawn)
        {
            if (!SkeletonUtility.HasNullChromosome(pawn))
            {
                return true;
            }
            RimshiLog.Test("gene", "no relations", pawn.LabelShort + ": relation generation skipped");
            return false;
        }
    }
}
