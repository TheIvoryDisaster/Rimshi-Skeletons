using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace RimshiSkeletons
{
    /// <summary>
    /// Keeps skeleton genes out of Biotech's gene trade, both ways: skeletons cannot give genes (gene extractor) or take
    /// them (xenogerm implant, reimplant ability, absorbing a xenogerm), and skeleton genes never turn up in a genepack, so
    /// the gene assembler can never use them. Every patch only changes the answer for pawns carrying a skeleton gene (or
    /// genepacks holding one), so other xenotypes and other mods' gene work are untouched. Gene packs from traders and
    /// quests are kept clean by canGenerateInGeneSet false on the genes (Defs/GeneDefs/Rimshi_Genes.xml).
    /// The custom xenotype editor (scenario start and dev mode) still offers the genes on purpose.
    /// </summary>
    public static class GeneProofing
    {
        public const string RefuseExtract = "Skeletons have no genes to extract: their frame is built, not grown.";
        public const string RefuseImplant = "Skeletons cannot take xenogenes: there is nothing living in their frame for them to work on.";

        /// <summary>Gene buildings and processes refuse pawns carrying any skeleton gene.</summary>
        public static bool Refuses(Pawn pawn) => SkeletonUtility.CarriesSkeletonGenes(pawn);

        /// <summary>Removes skeleton genes from a list headed for a genepack. Returns how many were removed.</summary>
        public static int StripSkeletonGenes(List<GeneDef> genes)
        {
            return genes?.RemoveAll(SkeletonUtility.IsSkeletonGene) ?? 0;
        }

        public static void Reject(string text, LookTargets target, bool show)
        {
            if (show)
            {
                Messages.Message(text, target, MessageTypeDefOf.RejectInput, false);
            }
        }
    }

    /// <summary>Gene extractor: skeletons cannot be put in (also stops the carry and enter work givers and the menu).</summary>
    [HarmonyPatch(typeof(Building_GeneExtractor), nameof(Building_GeneExtractor.CanAcceptPawn))]
    public static class Patch_GeneExtractorRefuses
    {
        public static void Postfix(Pawn pawn, ref AcceptanceReport __result)
        {
            if (__result.Accepted && GeneProofing.Refuses(pawn))
            {
                __result = GeneProofing.RefuseExtract;
            }
        }
    }

    /// <summary>Xenogerm item: skeletons are not a valid implant target (the gizmo, the menu and SetTargetPawn ask this).</summary>
    [HarmonyPatch(typeof(Xenogerm), nameof(Xenogerm.ValidateTarget))]
    public static class Patch_XenogermTarget
    {
        public static void Postfix(LocalTargetInfo target, bool showMessages, ref bool __result)
        {
            if (__result && GeneProofing.Refuses(target.Pawn))
            {
                __result = false;
                GeneProofing.Reject(GeneProofing.RefuseImplant, target.Pawn, showMessages);
                RimshiLog.Test("geneproof", "implant refused", target.Pawn.LabelShort + ": xenogerm target refused");
            }
        }
    }

    /// <summary>Implant xenogerm bill: not available on skeletons (health tab, and bills already queued).</summary>
    [HarmonyPatch(typeof(Recipe_ImplantXenogerm), nameof(Recipe_ImplantXenogerm.AvailableOnNow))]
    public static class Patch_ImplantXenogermBill
    {
        public static void Postfix(Thing thing, ref bool __result)
        {
            if (__result && thing is Pawn pawn && GeneProofing.Refuses(pawn))
            {
                __result = false;
            }
        }
    }

    /// <summary>Xenogerm reimplanter ability (sanguophages and others): skeletons are not a valid target.</summary>
    [HarmonyPatch(typeof(CompAbilityEffect_ReimplantXenogerm), nameof(CompAbilityEffect_ReimplantXenogerm.Valid),
        new[] { typeof(LocalTargetInfo), typeof(bool) })]
    public static class Patch_ReimplantTarget
    {
        public static void Postfix(LocalTargetInfo target, bool throwMessages, ref bool __result)
        {
            if (__result && GeneProofing.Refuses(target.Pawn))
            {
                __result = false;
                GeneProofing.Reject(GeneProofing.RefuseImplant, target.Pawn, throwMessages);
                RimshiLog.Test("geneproof", "reimplant refused", target.Pawn.LabelShort + ": reimplant target refused");
            }
        }
    }

    /// <summary>
    /// Absorbing a dying pawn's xenogerm: a skeleton cannot take the job (the original blocked the AbsorbXenogerm job for
    /// its race the same way). Failing the reservations ends the job before it starts.
    /// </summary>
    [HarmonyPatch(typeof(JobDriver_AbsorbXenogerm), nameof(JobDriver_AbsorbXenogerm.TryMakePreToilReservations))]
    public static class Patch_AbsorbXenogerm
    {
        public static bool Prefix(JobDriver_AbsorbXenogerm __instance, bool errorOnFailed, ref bool __result)
        {
            Pawn pawn = __instance.pawn;
            if (!GeneProofing.Refuses(pawn))
            {
                return true;
            }
            __result = false;
            GeneProofing.Reject(GeneProofing.RefuseImplant, pawn, pawn.Faction == Faction.OfPlayer);
            RimshiLog.Test("geneproof", "absorb refused", pawn.LabelShort + ": absorb xenogerm job refused");
            return false;
        }
    }

    /// <summary>
    /// Last guard, where xenogenes are actually written: a xenogerm item or a reimplant reaching a skeleton anyway (another
    /// mod's menu or job that skips the checks above) does nothing, and the log names the route as a failure.
    /// A skeleton is never a reimplant donor either, so no one can be given the skeleton xenotype this way.
    /// </summary>
    [HarmonyPatch(typeof(GeneUtility), nameof(GeneUtility.ImplantXenogermItem))]
    public static class Patch_ImplantXenogermItem
    {
        public static bool Prefix(Pawn pawn)
        {
            if (!GeneProofing.Refuses(pawn))
            {
                return true;
            }
            RimshiLog.Fail("geneproof", "implant blocked", pawn.LabelShort + ": a xenogerm reached a skeleton past the target checks and was not implanted");
            return false;
        }
    }

    [HarmonyPatch(typeof(GeneUtility), nameof(GeneUtility.ReimplantXenogerm))]
    public static class Patch_ReimplantXenogerm
    {
        public static bool Prefix(Pawn caster, Pawn recipient)
        {
            if (!GeneProofing.Refuses(caster) && !GeneProofing.Refuses(recipient))
            {
                return true;
            }
            RimshiLog.Fail("geneproof", "reimplant blocked", caster?.LabelShort + " to " + recipient?.LabelShort
                + ": a reimplant involving a skeleton got past the target checks and was not done");
            return false;
        }
    }

    /// <summary>
    /// Genepacks never hold skeleton genes, whatever made them (extractor, trader stock, quest reward, another mod). Gene
    /// sets already leave them out; this catches the rest and logs it. A pack that would end up empty is left alone and
    /// logged, since an empty genepack breaks the gene bank and assembler.
    /// </summary>
    [HarmonyPatch(typeof(Genepack), nameof(Genepack.Initialize))]
    public static class Patch_GenepackGenes
    {
        public static void Prefix(ref List<GeneDef> genes)
        {
            if (genes == null || !genes.Any(SkeletonUtility.IsSkeletonGene))
            {
                return;
            }
            var kept = new List<GeneDef>(genes);
            int removed = GeneProofing.StripSkeletonGenes(kept);
            if (kept.Count == 0)
            {
                RimshiLog.Fail("geneproof", "genepack", "a genepack of only skeleton genes was made (" + string.Join(", ", genes.ConvertAll(g => g.defName)) + "); left as is");
                return;
            }
            genes = kept;
            RimshiLog.Test("geneproof", "genepack", removed + " skeleton gene(s) kept out of a genepack");
        }
    }
}
