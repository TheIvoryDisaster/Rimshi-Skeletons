using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace RimshiSkeletons
{
    /// <summary>
    /// Modules: core genes every skeleton carries that the chip system and soft cycle facilities will later change
    /// (marked modifiable on SkeletonGeneExtension, see docs/design/skeleton-modules.md). The archaic speech module is
    /// stat factors only; the unshielded circuits module needs the EMP patches below.
    /// </summary>
    public static class SkeletonModules
    {
        private static readonly HashSet<int> loggedStun = new HashSet<int>();

        public static GeneDef UnshieldedCircuits => DefDatabase<GeneDef>.GetNamedSilentFail("Rimshi_UnshieldedCircuits");

        public static GeneDef ArchaicSpeech => DefDatabase<GeneDef>.GetNamedSilentFail("Rimshi_ArchaicSpeech");

        public static bool IsModule(GeneDef gene) => gene?.GetModExtension<SkeletonGeneExtension>()?.modifiable ?? false;

        /// <summary>
        /// True while the module is active, so a later chip or facility that suppresses the gene (vanilla's gene
        /// override, or removing it) turns EMP stuns off with no change here.
        /// </summary>
        public static bool EmpSusceptible(Pawn pawn)
        {
            GeneDef gene = UnshieldedCircuits;
            return gene != null && SkeletonUtility.HasActiveGene(pawn, gene);
        }

        public static void LogStun(Pawn pawn)
        {
            if (loggedStun.Add(pawn.thingIDNumber))
            {
                RimshiLog.Test("module", "emp stun", pawn.LabelShort + " stunned by EMP like a mechanoid");
            }
        }

        /// <summary>When a save loads: skeletons saved before the modules existed get any their xenotype now lists.</summary>
        public static void MigrateSave()
        {
            int added = 0;
            foreach (Pawn pawn in PawnsFinder.AllMapsWorldAndTemporary_AliveOrDead)
            {
                XenotypeDef xenotype = pawn?.genes?.Xenotype;
                if (xenotype?.genes == null || !SkeletonUtility.HasFrame(pawn))
                {
                    continue;
                }
                foreach (GeneDef gene in xenotype.genes.Where(IsModule))
                {
                    if (pawn.genes.GetGene(gene) == null)
                    {
                        pawn.genes.AddGene(gene, !xenotype.inheritable);
                        added++;
                    }
                }
            }
            if (added > 0)
            {
                RimshiLog.Test("module", "added on load", added + " modules added to skeletons from an older save");
            }
        }
    }

    /// <summary>
    /// Vanilla only lets EMP stun pawns whose race is not flesh (mechanoids). Skeletons are humans underneath, so the
    /// unshielded circuits module adds them, with the same downed, dead and awoken corpse exceptions vanilla checks first.
    /// </summary>
    [HarmonyPatch(typeof(StunHandler), "CanBeStunnedByDamage")]
    public static class Patch_EmpStunsSkeletons
    {
        public static bool Prepare() => PatchTarget.Exists(typeof(StunHandler), "CanBeStunnedByDamage");

        public static void Postfix(StunHandler __instance, DamageDef def, ref bool __result)
        {
            if (__result || def != DamageDefOf.EMP || !(__instance.parent is Pawn pawn) || pawn.Downed || pawn.Dead)
            {
                return;
            }
            if (ModsConfig.AnomalyActive && pawn.health.hediffSet.HasHediff(HediffDefOf.AwokenCorpse))
            {
                return;
            }
            if (SkeletonModules.EmpSusceptible(pawn))
            {
                __result = true;
                SkeletonModules.LogStun(pawn);
            }
        }
    }

    /// <summary>
    /// Mechanoids adapt to EMP: after one stun, more EMP does nothing until the adaptation wears off (the EMP def's
    /// stunAdaptationTicks). Skeletons with the module adapt the same way, so they cannot be stun-locked either.
    /// </summary>
    [HarmonyPatch(typeof(StunHandler), "CanAdaptToDamage")]
    public static class Patch_SkeletonsAdaptToEmp
    {
        public static bool Prepare() => PatchTarget.Exists(typeof(StunHandler), "CanAdaptToDamage");

        public static void Postfix(StunHandler __instance, DamageDef def, ref bool __result)
        {
            if (!__result && def == DamageDefOf.EMP && def.stunAdaptationTicks > 0 && __instance.parent is Pawn pawn
                && SkeletonModules.EmpSusceptible(pawn))
            {
                __result = true;
            }
        }
    }
}
