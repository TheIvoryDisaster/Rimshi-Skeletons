using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace RimshiSkeletons
{
    /// <summary>
    /// Pawns with the mechanical nature gene (every skeleton) have no blood. Vanilla gates all bleeding on
    /// Pawn_HealthTracker.CanBleed (injury and missing part bleed rates, the bleed total, blood loss, blood drops and
    /// smears while bleeding, the blood drop when hit, hemogen extraction), so turning that off covers them all, the same
    /// way vanilla treats mechs. A side effect, also as for mechs: vanilla never offers to tend an injury on a pawn that
    /// cannot bleed (Hediff.TendableNow), so doctors leave skeleton injuries to the repair job.
    /// When hit, a skeleton leaks ancient oil (Rimshi_Filth_AncientOil) where a human would drop blood, and the red hit
    /// spray is played in black (Rimshi_Damage_HitAncientOil). Anything that calls DropBloodFilth on a skeleton (vanilla
    /// surgery, other mods) gets oil too. Sanguophages get nothing from biting one.
    /// </summary>
    public static class SkeletonBlood
    {
        private static readonly HashSet<int> loggedOil = new HashSet<int>();
        private static readonly HashSet<int> loggedHit = new HashSet<int>();

        public static bool HasNoBlood(Pawn pawn) => SkeletonUtility.HasMechanicalNature(pawn);

        /// <summary>Leaks ancient oil where the pawn stands (or where whatever holds it stands).</summary>
        public static void DropOil(Pawn pawn)
        {
            if ((!pawn.Spawned && !(pawn.ParentHolder is Pawn_CarryTracker)) || !pawn.SpawnedOrAnyParentSpawned)
            {
                return;
            }
            FilthMaker.TryMakeFilth(pawn.PositionHeld, pawn.MapHeld, RimshiDefOf.Rimshi_Filth_AncientOil, pawn.LabelIndefinite());
            if (loggedOil.Add(pawn.thingIDNumber))
            {
                RimshiLog.Test("blood", "oil dropped", pawn.LabelShort + " leaked ancient oil at " + pawn.PositionHeld);
            }
        }

        public static void LogHit(Pawn pawn)
        {
            if (loggedHit.Add(pawn.thingIDNumber))
            {
                RimshiLog.Test("blood", "oil hit effect", pawn.LabelShort + ": black hit spray instead of blood");
            }
        }
    }

    /// <summary>No bleeding at all for skeletons; see SkeletonBlood.</summary>
    [HarmonyPatch(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.CanBleed), MethodType.Getter)]
    public static class Patch_CanBleed
    {
        public static void Postfix(Pawn ___pawn, ref bool __result)
        {
            if (__result && SkeletonBlood.HasNoBlood(___pawn))
            {
                __result = false;
            }
        }
    }

    /// <summary>
    /// Vanilla drops blood on half of all hits that make blood, but only for pawns that can bleed. This does the same for
    /// skeletons, with oil.
    /// </summary>
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.PostApplyDamage))]
    public static class Patch_PostApplyDamage
    {
        public static void Postfix(Pawn __instance, DamageInfo dinfo, float totalDamageDealt)
        {
            if (dinfo.Def != null && dinfo.Def.makesBlood && !dinfo.InstantPermanentInjury && totalDamageDealt > 0f
                && SkeletonBlood.HasNoBlood(__instance) && Rand.Chance(0.5f))
            {
                SkeletonBlood.DropOil(__instance);
            }
        }
    }

    /// <summary>Any blood drop asked for on a skeleton (surgery, other mods) becomes oil.</summary>
    [HarmonyPatch(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.DropBloodFilth))]
    public static class Patch_DropBloodFilth
    {
        public static bool Prefix(Pawn ___pawn)
        {
            if (!SkeletonBlood.HasNoBlood(___pawn))
            {
                return true;
            }
            SkeletonBlood.DropOil(___pawn);
            return false;
        }
    }

    /// <summary>No blood smears from a crawling skeleton.</summary>
    [HarmonyPatch(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.DropBloodSmear))]
    public static class Patch_DropBloodSmear
    {
        public static bool Prefix(Pawn ___pawn) => !SkeletonBlood.HasNoBlood(___pawn);
    }

    /// <summary>
    /// The hit spray comes from the race's flesh type (Damage_HitFlesh for humans, red), which every skeleton shares with
    /// humans. When that effect fires on a skeleton, the black oil version plays instead. Other effects are untouched.
    /// </summary>
    [HarmonyPatch(typeof(Effecter), nameof(Effecter.Trigger))]
    public static class Patch_EffecterTrigger
    {
        public static bool Prefix(Effecter __instance, TargetInfo A, TargetInfo B, int overrideSpawnTick, ref Effecter __result)
        {
            if (!(A.Thing is Pawn pawn) || __instance.def == null || __instance.def != pawn.RaceProps.FleshType.damageEffecter
                || !SkeletonBlood.HasNoBlood(pawn))
            {
                return true;
            }
            Effecter oil = RimshiDefOf.Rimshi_Damage_HitAncientOil.Spawn();
            oil.Trigger(A, B, overrideSpawnTick);
            oil.Cleanup();
            SkeletonBlood.LogHit(pawn);
            __result = __instance;
            return false;
        }
    }

    /// <summary>Colonist sanguophages never pick a skeleton prisoner to feed on.</summary>
    [HarmonyPatch(typeof(JobGiver_GetHemogen), nameof(JobGiver_GetHemogen.CanFeedOnPrisoner))]
    public static class Patch_CanFeedOnPrisoner
    {
        public static void Postfix(Pawn prisoner, ref AcceptanceReport __result)
        {
            if (__result.Accepted && SkeletonBlood.HasNoBlood(prisoner))
            {
                __result = false;
            }
        }
    }

    /// <summary>A bite on a skeleton (the bloodfeed ability, other mods) draws nothing: no hemogen, no blood loss, no blood.</summary>
    [HarmonyPatch(typeof(SanguophageUtility), nameof(SanguophageUtility.DoBite))]
    public static class Patch_DoBite
    {
        public static bool Prefix(Pawn biter, Pawn victim)
        {
            if (!SkeletonBlood.HasNoBlood(victim))
            {
                return true;
            }
            RimshiLog.Test("blood", "bite refused", biter?.LabelShort + " bit " + victim.LabelShort + " and got no hemogen");
            return false;
        }
    }
}
