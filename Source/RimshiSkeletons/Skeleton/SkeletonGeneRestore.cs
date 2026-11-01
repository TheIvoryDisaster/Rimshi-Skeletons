using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace RimshiSkeletons
{
    /// <summary>
    /// Puts back skeleton core genes a skeleton's xenotype lists but the pawn has lost. Matthew's Clan Mana save lost
    /// Rimshi_MechanicalNature on every skeleton on the first load after it got its own gene class (PR #38): it was still
    /// in the autosave before and gone from every save after, so those skeletons had no machine organs, human lung names,
    /// blood, and organs that could be harvested. Runs on every load, so whatever stripped it is fixed on the next load,
    /// and the log names what came back. Modules and the deathrest gene have their own migrations (SkeletonModules,
    /// SoftCycle). Core genes cannot be removed in play (GeneProofing), so a missing one is always a loss.
    /// </summary>
    public static class SkeletonGeneRestore
    {
        public static void MigrateSave()
        {
            int pawns = 0;
            foreach (Pawn pawn in PawnsFinder.AllMapsWorldAndTemporary_AliveOrDead)
            {
                if (Restore(pawn, "older save").Count > 0)
                {
                    pawns++;
                }
            }
            if (pawns > 0)
            {
                RimshiLog.Test("gene", "restored on load", pawns + " skeletons got back core genes their xenotype lists");
            }
        }

        /// <summary>Adds every missing core gene of the pawn's skeleton xenotype; returns their defNames.</summary>
        public static List<string> Restore(Pawn pawn, string why)
        {
            var restored = new List<string>();
            XenotypeDef xenotype = pawn?.genes?.Xenotype;
            if (xenotype?.genes == null || !SkeletonUtility.IsSkeletonXenotype(xenotype))
            {
                return restored;
            }
            foreach (GeneDef gene in xenotype.genes)
            {
                if (!SkeletonUtility.IsSkeletonGene(gene) || SkeletonModules.IsModule(gene) || gene == SoftCycle.Gene || pawn.genes.GetGene(gene) != null)
                {
                    continue;
                }
                // Gene_MechanicalNature.PostAdd fits the machine organs.
                pawn.genes.AddGene(gene, !xenotype.inheritable);
                restored.Add(gene.defName);
            }
            if (restored.Contains(RimshiDefOf.Rimshi_MechanicalNature.defName) && pawn.health?.hediffSet != null)
            {
                // Blood lost while the gene was missing: a skeleton has none.
                Hediff bloodLoss = pawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.BloodLoss);
                if (bloodLoss != null)
                {
                    pawn.health.RemoveHediff(bloodLoss);
                }
            }
            if (restored.Count > 0)
            {
                RimshiLog.Test("gene", "core restored", pawn.LabelShort + " (" + why + "): " + string.Join(", ", restored));
            }
            return restored;
        }
    }

    /// <summary>
    /// Diagnostic: names whatever removes a core gene from a skeleton, since the save that lost mechanical nature left no
    /// log of it. The self-tests' gene removal check shows up here too.
    /// </summary>
    [HarmonyPatch(typeof(Pawn_GeneTracker), nameof(Pawn_GeneTracker.RemoveGene))]
    public static class Patch_LogCoreGeneRemoved
    {
        public static void Prefix(Pawn_GeneTracker __instance, Gene gene)
        {
            if (gene?.def == null || !SkeletonUtility.IsSkeletonGene(gene.def) || SkeletonModules.IsModule(gene.def)
                || !SkeletonUtility.IsSkeletonXenotype(__instance.Xenotype))
            {
                return;
            }
            string caller = string.Join(" < ", new System.Diagnostics.StackTrace(1).GetFrames()?.Skip(1).Take(4)
                .Select(f => f.GetMethod()).Where(m => m != null).Select(m => m.DeclaringType?.Name + "." + m.Name) ?? Enumerable.Empty<string>());
            RimshiLog.Test("gene", "core removed", __instance.pawn?.LabelShort + ": " + gene.def.defName + " removed by " + caller);
        }
    }
}
