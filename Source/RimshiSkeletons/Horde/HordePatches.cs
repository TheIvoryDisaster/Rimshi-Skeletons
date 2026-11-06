using HarmonyLib;
using RimWorld;
using Verse;

namespace RimshiSkeletons
{
    /// <summary>The Reprogram interaction only shows for prisoners that have a corrupted protocol.</summary>
    [HarmonyPatch(typeof(ITab_Pawn_Visitor), "DrawExclusiveInteractionRow")]
    public static class Patch_HideReprogramMode
    {
        public static bool Prepare() => PatchTarget.Exists(typeof(ITab_Pawn_Visitor), "DrawExclusiveInteractionRow")
            && AccessTools.PropertyGetter(typeof(ITab), "SelPawn") != null;

        public static bool Prefix(ITab_Pawn_Visitor __instance, PrisonerInteractionModeDef mode)
        {
            if (mode != RimshiDefOf.Rimshi_Reprogram)
            {
                return true;
            }
            var pawn = Traverse.Create(__instance).Property("SelPawn").GetValue<Pawn>();
            return ReprogramUtility.IsCorrupted(pawn);
        }
    }
}
