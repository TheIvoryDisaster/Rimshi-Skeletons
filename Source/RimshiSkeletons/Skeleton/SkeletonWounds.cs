using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimshiSkeletons
{
    /// <summary>
    /// Skeletons share the human flesh type, so their injuries would draw the red flesh wound overlays. Every wound overlay
    /// comes from FleshTypeDef.ChooseWoundOverlay; for a skeleton's injury or missing part this swaps the red texture for
    /// one of vanilla's grey and black mechanoid wounds (Things/Pawn/Wounds/WoundMechA..C), keeping the original wound's
    /// placement, scale and flags. Overlays for added parts (bionic eyes) and scarification are left alone.
    /// </summary>
    public static class SkeletonWounds
    {
        public static readonly string[] MechWoundTextures =
        {
            "Things/Pawn/Wounds/WoundMechA",
            "Things/Pawn/Wounds/WoundMechB",
            "Things/Pawn/Wounds/WoundMechC",
        };

        private static readonly Dictionary<(FleshTypeDef.ResolvedWound, int), FleshTypeDef.ResolvedWound> swapped =
            new Dictionary<(FleshTypeDef.ResolvedWound, int), FleshTypeDef.ResolvedWound>();
        private static readonly HashSet<int> logged = new HashSet<int>();

        public static bool Recolors(Hediff hediff) =>
            (hediff is Hediff_Injury || hediff is Hediff_MissingPart) && SkeletonUtility.HasMechanicalNature(hediff.pawn);

        /// <summary>The grey and black version of a flesh wound overlay; the same hediff always gets the same texture.</summary>
        public static FleshTypeDef.ResolvedWound MechVersion(FleshTypeDef.ResolvedWound original, Hediff hediff)
        {
            int index = Mathf.Abs(hediff.loadID) % MechWoundTextures.Length;
            if (!swapped.TryGetValue((original, index), out FleshTypeDef.ResolvedWound mech))
            {
                FleshTypeDef.Wound from = original.wound;
                var wound = new FleshTypeDef.Wound
                {
                    texture = MechWoundTextures[index],
                    scale = from.scale,
                    drawOffsetSouth = from.drawOffsetSouth,
                    drawOffsetEastWest = from.drawOffsetEastWest,
                    color = from.color,
                    flipOnWoundAnchorTag = from.flipOnWoundAnchorTag,
                    flipOnRotation = from.flipOnRotation,
                    onlyOnPart = from.onlyOnPart,
                    displayOverApparel = from.displayOverApparel,
                    displayPermanent = from.displayPermanent,
                    missingBodyPartFresh = from.missingBodyPartFresh,
                };
                mech = wound.Resolve();
                swapped[(original, index)] = mech;
            }
            return mech;
        }

        /// <summary>Logged once per pawn, and only for pawns on a map, so the off-map self-test pawns don't count.</summary>
        public static void LogSwap(Hediff hediff)
        {
            if (hediff.pawn.Spawned && logged.Add(hediff.pawn.thingIDNumber))
            {
                RimshiLog.Test("wounds", "mech wound drawn", hediff.pawn.LabelShort + ": " + hediff.def.defName
                    + " drawn as a grey and black wound instead of a red one");
            }
        }
    }

    /// <summary>Grey and black wound overlays on skeletons; see SkeletonWounds.</summary>
    [HarmonyPatch(typeof(FleshTypeDef), nameof(FleshTypeDef.ChooseWoundOverlay))]
    public static class Patch_ChooseWoundOverlay
    {
        public static void Postfix(Hediff hediff, ref FleshTypeDef.ResolvedWound __result)
        {
            if (__result?.wound == null || hediff == null || !SkeletonWounds.Recolors(hediff))
            {
                return;
            }
            __result = SkeletonWounds.MechVersion(__result, hediff);
            SkeletonWounds.LogSwap(hediff);
        }
    }
}
