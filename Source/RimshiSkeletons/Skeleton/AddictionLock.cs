using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace RimshiSkeletons
{
    /// <summary>
    /// Pawns with the mechanical nature gene (every skeleton) never get an addiction or a chemical dependency, whatever the
    /// source: a drug forced past the drug lock, another mod's hediff giver, an event, dev mode, or a chemical dependency gene
    /// in a custom xenotype (the gene stays but its deficiency hediff never appears, so it can never wear the skeleton down).
    /// Generated skeletons get no starting addictions or tolerances, and ones that already have an addiction (older saves,
    /// a gene added later) lose it on load or when the gene is added. Addictions named Rimshi_ are exempt, like our drugs.
    /// </summary>
    public static class AddictionLock
    {
        private static readonly HashSet<string> loggedRefusals = new HashSet<string>();

        public static bool Immune(Pawn pawn) => SkeletonUtility.HasMechanicalNature(pawn);

        /// <summary>Addictions (vanilla Hediff_Addiction and anything that causes a chemical need) and Biotech gene dependencies.</summary>
        public static bool IsAddiction(Hediff hediff)
        {
            HediffDef def = hediff?.def;
            if (def == null || def.defName.StartsWith("Rimshi_", StringComparison.Ordinal))
            {
                return false;
            }
            return hediff is Hediff_Addiction || hediff is Hediff_ChemicalDependency || def.chemicalNeed != null
                || (def.hediffClass != null && (typeof(Hediff_Addiction).IsAssignableFrom(def.hediffClass) || typeof(Hediff_ChemicalDependency).IsAssignableFrom(def.hediffClass)));
        }

        public static void NoteRefused(Pawn pawn, Hediff hediff)
        {
            if (loggedRefusals.Add(hediff.def.defName))
            {
                RimshiLog.Test("addiction", "refused", pawn.LabelShort + ": " + hediff.def.defName + " not added");
            }
        }

        /// <summary>Removes every addiction and dependency the pawn already has. Returns how many were removed.</summary>
        public static int Clear(Pawn pawn, string why)
        {
            if (pawn?.health?.hediffSet == null || !Immune(pawn))
            {
                return 0;
            }
            List<Hediff> found = pawn.health.hediffSet.hediffs.Where(IsAddiction).ToList();
            foreach (Hediff hediff in found)
            {
                pawn.health.RemoveHediff(hediff);
            }
            if (found.Count > 0)
            {
                RimshiLog.Test("addiction", "cleared", pawn.LabelShort + " (" + why + "): " + string.Join(", ", found.Select(h => h.def.defName)));
            }
            return found.Count;
        }

        /// <summary>Older saves: skeletons that picked up an addiction before this existed lose it on load.</summary>
        public static void MigrateSave()
        {
            int removed = 0;
            foreach (Pawn pawn in PawnsFinder.AllMapsWorldAndTemporary_Alive)
            {
                removed += Clear(pawn, "older save");
            }
            if (removed > 0)
            {
                RimshiLog.Test("addiction", "save migrated", removed + " addictions removed from skeletons");
            }
        }
    }

    /// <summary>
    /// Every addiction reaches a pawn through this overload (the HediffDef one makes the hediff and calls it), so refusing
    /// here covers drugs, hediff givers, other mods and dev mode alike. It is far too large to be inlined on 1.7.
    /// </summary>
    [HarmonyPatch(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.AddHediff),
        new[] { typeof(Hediff), typeof(BodyPartRecord), typeof(DamageInfo?), typeof(DamageWorker.DamageResult) })]
    public static class Patch_NoSkeletonAddiction
    {
        public static bool Prefix(Pawn ___pawn, Hediff hediff)
        {
            if (!AddictionLock.IsAddiction(hediff) || !AddictionLock.Immune(___pawn))
            {
                return true;
            }
            AddictionLock.NoteRefused(___pawn, hediff);
            return false;
        }
    }

    /// <summary>Generation: no starting addictions or tolerances (also stops the generator retrying a refused addiction).</summary>
    [HarmonyPatch(typeof(PawnAddictionHediffsGenerator), nameof(PawnAddictionHediffsGenerator.GenerateAddictionsAndTolerancesFor))]
    public static class Patch_NoGeneratedAddictions
    {
        public static bool Prefix(Pawn pawn) => !AddictionLock.Immune(pawn);
    }

    /// <summary>
    /// A chemical dependency gene on a skeleton has no deficiency hediff, so any vanilla code that expects one (the gene's
    /// reset when the chemical is ingested) must not throw for it.
    /// </summary>
    [HarmonyPatch]
    public static class Patch_DependencyGeneWithoutHediff
    {
        public static IEnumerable<System.Reflection.MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(Gene_ChemicalDependency), nameof(Gene_ChemicalDependency.Reset));
            yield return AccessTools.Method(typeof(Gene_ChemicalDependency), nameof(Gene_ChemicalDependency.Notify_IngestedThing));
        }

        public static Exception Finalizer(Exception __exception, Gene_ChemicalDependency __instance)
        {
            if (__exception is NullReferenceException && AddictionLock.Immune(__instance?.pawn))
            {
                return null;
            }
            return __exception;
        }
    }
}
