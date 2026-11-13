using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimshiSkeletons
{
    /// <summary>
    /// Being worked on does not wake a soft cycling skeleton (Matthew, 2026-11-09: only real damage should). Jobs done to
    /// a pawn (repairing it with kits, vanilla tending and similar) make it wait through PawnUtility.ForceWait, which
    /// starts a Wait or Wait_MaintainPosture job on it and so ended the standing reset's job (cancelling the reset) or the
    /// repair bed's deathrest job. A skeleton mid-cycle ignores those waits and stays in its cycle; the job working on it
    /// carries on regardless. Damage, downing and mental breaks still end a cycle as before.
    /// Patched at Pawn_JobTracker.StartJob rather than ForceWait, which is small enough for 1.7's JIT to inline.
    /// </summary>
    public static class SoftCycleUndisturbed
    {
        /// <summary>The skeleton is in a soft cycle whose job a forced wait would end.</summary>
        public static bool Holds(Pawn pawn)
        {
            if (pawn == null || !SkeletonUtility.HasFrame(pawn))
            {
                return false;
            }
            JobDef current = pawn.CurJobDef;
            if (current == RimshiDefOf.Rimshi_SoftReset)
            {
                return StandingReset.InProgress(pawn);
            }
            return current == JobDefOf.Deathrest && pawn.Deathresting;
        }

        /// <summary>The waiting jobs other pawns' work hands to the pawn they work on.</summary>
        public static bool IsForcedWait(Job job) => job != null && (job.def == JobDefOf.Wait || job.def == JobDefOf.Wait_MaintainPosture);

        /// <summary>True when the job should be ignored, leaving the cycle running.</summary>
        public static bool Ignore(Pawn pawn, Job newJob) => IsForcedWait(newJob) && Holds(pawn);
    }

    [HarmonyPatch(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.StartJob))]
    public static class Patch_SoftCycleIgnoresWaits
    {
        public static bool Prefix(Job newJob, Pawn ___pawn)
        {
            if (!SoftCycleUndisturbed.Ignore(___pawn, newJob))
            {
                return true;
            }
            RimshiLog.Test("softcycle", "wait ignored", ___pawn.LabelShort + " kept soft cycling instead of starting " + newJob.def.defName
                + " (" + (___pawn.CurJobDef == JobDefOf.Deathrest ? "repair bed" : "standing") + ")");
            return false;
        }
    }
}
