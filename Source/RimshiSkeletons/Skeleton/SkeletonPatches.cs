using System.Collections.Generic;
using System.Linq;
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

    /// <summary>Genderless pawns are generated without family, as Rim-Hivers does (the original's relation chances were all 0).</summary>
    [HarmonyPatch(typeof(PawnGenerator), "GeneratePawnRelations")]
    public static class Patch_GeneratePawnRelations
    {
        public static bool Prepare() => PatchTarget.Exists(typeof(PawnGenerator), "GeneratePawnRelations");

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

    /// <summary>Remembers which pawns were newly generated (not taken from the world pawns), for Patch_ClearNewRelations.</summary>
    [HarmonyPatch(typeof(PawnGenerator), "TryGenerateNewPawnInternal")]
    public static class Patch_NoteNewPawn
    {
        public static readonly HashSet<int> NewPawns = new HashSet<int>();

        public static bool Prepare() => PatchTarget.Exists(typeof(PawnGenerator), "TryGenerateNewPawnInternal");

        public static void Postfix(Pawn __result)
        {
            if (__result != null)
            {
                // Pawns a generation attempt threw away never reach Patch_ClearNewRelations; keep the set small.
                if (NewPawns.Count > 1000)
                {
                    NewPawns.Clear();
                }
                NewPawns.Add(__result.thingIDNumber);
            }
        }
    }

    /// <summary>
    /// Some relations reach a newly generated genderless pawn by another route than GeneratePawnRelations (another mod,
    /// or a later generation step). Once generation is finished, a new genderless pawn drops every relation it got, and
    /// the log names each one so the route can be found. Pawns that came back from the world pawns keep theirs.
    /// </summary>
    [HarmonyPatch(typeof(PawnGenerator), nameof(PawnGenerator.GeneratePawn), new[] { typeof(PawnGenerationRequest) })]
    public static class Patch_ClearNewRelations
    {
        [HarmonyPriority(Priority.Last)]
        public static void Postfix(Pawn __result)
        {
            if (__result == null || !Patch_NoteNewPawn.NewPawns.Remove(__result.thingIDNumber) || !SkeletonUtility.HasNullChromosome(__result)
                || __result.relations == null || __result.relations.DirectRelations.Count == 0)
            {
                return;
            }
            var removed = new List<string>();
            foreach (DirectPawnRelation relation in __result.relations.DirectRelations.ToList())
            {
                if (__result.relations.TryRemoveDirectRelation(relation.def, relation.otherPawn))
                {
                    removed.Add(relation.def.defName + " " + relation.otherPawn?.LabelShort);
                }
            }
            RimshiLog.Test("gene", "relations cleared", __result.LabelShort + ": " + string.Join(", ", removed));
        }
    }

    /// <summary>
    /// Skeletons always get skeleton backstories, in any faction (the original's pawn kinds all overrode the backstory
    /// filters). The xenotype is picked before the backstory but the genes are added after it, so the xenotype is noted
    /// here for Patch_SkeletonBackstories.
    /// </summary>
    [HarmonyPatch(typeof(PawnBioAndNameGenerator), nameof(PawnBioAndNameGenerator.GiveAppropriateBioAndNameTo))]
    public static class Patch_NoteBioXenotype
    {
        public static XenotypeDef Current;

        public static void Prefix(XenotypeDef xenotype)
        {
            Current = xenotype;
        }

        public static void Finalizer()
        {
            Current = null;
        }
    }

    [HarmonyPatch(typeof(PawnBioAndNameGenerator), "GetBackstoryCategoryFiltersFor")]
    public static class Patch_SkeletonBackstories
    {
        public static bool Prepare() => PatchTarget.Exists(typeof(PawnBioAndNameGenerator), "GetBackstoryCategoryFiltersFor");

        private static readonly List<BackstoryCategoryFilter> Filters = new List<BackstoryCategoryFilter>
        {
            new BackstoryCategoryFilter { categories = new List<string> { SkeletonUtility.BackstoryCategory } },
        };

        public static void Postfix(Pawn pawn, ref List<BackstoryCategoryFilter> __result)
        {
            if (pawn.DevelopmentalStage.Adult() && SkeletonUtility.IsSkeletonXenotype(Patch_NoteBioXenotype.Current))
            {
                __result = Filters;
            }
        }
    }

    /// <summary>
    /// Patches that name a private method by string skip themselves, with a failed test line, when the method is not
    /// there (renamed in another game version), instead of stopping every other patch from applying.
    /// </summary>
    public static class PatchTarget
    {
        public static bool Exists(System.Type type, string method)
        {
            if (AccessTools.Method(type, method) != null)
            {
                return true;
            }
            RimshiLog.Fail("patch", "missing", type.Name + "." + method + " not found; that patch is off");
            return false;
        }
    }
}
