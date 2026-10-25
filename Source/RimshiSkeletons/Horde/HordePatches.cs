using HarmonyLib;
using RimWorld;
using Verse;

namespace RimshiSkeletons
{
    /// <summary>
    /// Berserk pawns are hostile to everyone, so a berserk horde would tear itself apart on landing. Free horde skeletons
    /// (corrupted protocol, not the colony's prisoners) are not hostile to each other; everyone else is still fair game.
    /// </summary>
    [HarmonyPatch(typeof(GenHostility), nameof(GenHostility.HostileTo), new[] { typeof(Thing), typeof(Thing) })]
    public static class Patch_HordeNotHostileToItself
    {
        public static void Postfix(Thing a, Thing b, ref bool __result)
        {
            if (__result && a is Pawn pa && b is Pawn pb && IsFreeHordePawn(pa) && IsFreeHordePawn(pb))
            {
                __result = false;
            }
        }

        private static bool IsFreeHordePawn(Pawn pawn) => !pawn.IsPrisonerOfColony && ReprogramUtility.IsCorrupted(pawn);
    }

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
