using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace RimshiSkeletons
{
    /// <summary>
    /// On a skeleton part hediff (KLR and industrial limbs): damage to the part it is installed on, or to anything
    /// attached below it (an arm's hand and fingers), is multiplied by damageFactor. 0.8 means 20% less damage.
    /// </summary>
    public class PartDamageExtension : DefModExtension
    {
        public float damageFactor = 1f;
    }

    /// <summary>On a skeleton part hediff (steady arms): chance per melee hit to knock the target's weapon out of its hands.</summary>
    public class DisarmExtension : DefModExtension
    {
        public float chance;
    }

    /// <summary>
    /// Install surgery for skeleton parts (docs/design/skeleton-parts.md): vanilla's artificial part install, offered only
    /// on skeletons. The prosthetics lock exempts recipes named Rimshi_, so this is the only gate.
    /// </summary>
    public class Recipe_Rimshi_InstallSkeletonPart : Recipe_InstallArtificialBodyPart
    {
        public override bool AvailableOnNow(Thing thing, BodyPartRecord part = null)
        {
            return thing is Pawn pawn && SkeletonUtility.HasMechanicalNature(pawn) && base.AvailableOnNow(thing, part);
        }

        public override void ApplyOnPawn(Pawn pawn, BodyPartRecord part, Pawn billDoer, List<Thing> ingredients, Bill bill)
        {
            base.ApplyOnPawn(pawn, part, billDoer, ingredients, bill);
            if (pawn.health.hediffSet.hediffs.Any(h => h.def == recipe.addsHediff && h.Part == part))
            {
                RimshiLog.Test("parts", "installed", pawn.LabelShort + ": " + recipe.addsHediff.defName + " on " + part.def.defName);
            }
        }
    }

    /// <summary>
    /// The AI core goes in place of a skeleton's brain. Installed again over an AI core that has injuries or other harm on
    /// it, it repairs all of it instead: the old core stays and the new one is used up. Losing the core (the brain part)
    /// still kills, as vanilla does for the brain.
    /// </summary>
    public class Recipe_Rimshi_InstallAICore : Recipe_Rimshi_InstallSkeletonPart
    {
        public override IEnumerable<BodyPartRecord> GetPartsToApplyOn(Pawn pawn, RecipeDef recipe)
        {
            foreach (BodyPartRecord part in base.GetPartsToApplyOn(pawn, recipe))
            {
                yield return part;
            }
            foreach (BodyPartRecord part in pawn.health.hediffSet.GetNotMissingParts())
            {
                if (HasCore(pawn, part, recipe.addsHediff) && Damage(pawn, part, recipe.addsHediff).Any())
                {
                    yield return part;
                }
            }
        }

        public override void ApplyOnPawn(Pawn pawn, BodyPartRecord part, Pawn billDoer, List<Thing> ingredients, Bill bill)
        {
            if (!HasCore(pawn, part, recipe.addsHediff))
            {
                base.ApplyOnPawn(pawn, part, billDoer, ingredients, bill);
                return;
            }
            if (billDoer != null && CheckSurgeryFail(billDoer, pawn, ingredients, part, bill))
            {
                return;
            }
            List<Hediff> damage = Damage(pawn, part, recipe.addsHediff).ToList();
            foreach (Hediff hediff in damage)
            {
                pawn.health.RemoveHediff(hediff);
            }
            RimshiLog.Test("parts", "ai core repaired", pawn.LabelShort + ": removed " + string.Join(", ", damage.Select(h => h.def.defName)));
        }

        private static bool HasCore(Pawn pawn, BodyPartRecord part, HediffDef core) =>
            pawn.health.hediffSet.hediffs.Any(h => h.def == core && h.Part == part);

        /// <summary>Anything harmful on the core's part: injuries, scars and other bad hediffs.</summary>
        private static IEnumerable<Hediff> Damage(Pawn pawn, BodyPartRecord part, HediffDef core) =>
            pawn.health.hediffSet.hediffs.Where(h => h.Part == part && h.def != core && (h is Hediff_Injury || h.def.isBad) && !(h is Hediff_MissingPart));
    }

    /// <summary>
    /// Part damage resistance: scales the damage an injury is made with when it lands on a part covered by a hediff with
    /// PartDamageExtension. FinalizeAndAddInjury(float) is where every damage worker turns damage into an injury on one part.
    /// </summary>
    [HarmonyPatch(typeof(DamageWorker_AddInjury), "FinalizeAndAddInjury",
        new[] { typeof(Pawn), typeof(float), typeof(DamageInfo), typeof(DamageWorker.DamageResult) })]
    public static class Patch_PartDamageResistance
    {
        private static bool logged;

        public static void Prefix(Pawn pawn, ref float totalDamage, DamageInfo dinfo)
        {
            BodyPartRecord hit = dinfo.HitPart;
            if (hit == null || pawn?.health?.hediffSet == null)
            {
                return;
            }
            List<Hediff> hediffs = pawn.health.hediffSet.hediffs;
            for (int i = 0; i < hediffs.Count; i++)
            {
                PartDamageExtension ext = hediffs[i].def.GetModExtension<PartDamageExtension>();
                if (ext != null && hediffs[i].Part != null && Covers(hediffs[i].Part, hit))
                {
                    float before = totalDamage;
                    totalDamage *= ext.damageFactor;
                    if (!logged)
                    {
                        logged = true;
                        RimshiLog.Test("parts", "damage reduced", pawn.LabelShort + ": " + hit.def.defName + " hit for " + before.ToString("0.#") + ", " + hediffs[i].def.defName + " made it " + totalDamage.ToString("0.#"));
                    }
                    return;
                }
            }
        }

        private static bool Covers(BodyPartRecord part, BodyPartRecord hit)
        {
            for (BodyPartRecord p = hit; p != null; p = p.parent)
            {
                if (p == part)
                {
                    return true;
                }
            }
            return false;
        }
    }

    /// <summary>Steady arm disarm: after a melee hit that did damage, a chance per steady arm to make the target drop its weapon.</summary>
    [HarmonyPatch(typeof(Verb_MeleeAttackDamage), "ApplyMeleeDamageToTarget")]
    public static class Patch_SteadyDisarm
    {
        public static void Postfix(Verb_MeleeAttackDamage __instance, LocalTargetInfo target, DamageWorker.DamageResult __result)
        {
            Pawn attacker = __instance.CasterPawn;
            if (attacker?.health?.hediffSet == null || __result == null || __result.totalDamageDealt <= 0f || !(target.Thing is Pawn victim))
            {
                return;
            }
            float chance = 0f;
            foreach (Hediff hediff in attacker.health.hediffSet.hediffs)
            {
                DisarmExtension ext = hediff.def.GetModExtension<DisarmExtension>();
                if (ext != null)
                {
                    chance = 1f - (1f - chance) * (1f - ext.chance);
                }
            }
            ThingWithComps weapon = victim.equipment?.Primary;
            if (chance <= 0f || weapon == null || victim.Dead || !victim.Spawned || !Rand.Chance(chance))
            {
                return;
            }
            if (victim.equipment.TryDropEquipment(weapon, out ThingWithComps dropped, victim.PositionHeld))
            {
                MoteMaker.ThrowText(victim.DrawPos, victim.Map, "Disarmed", 3.65f);
                RimshiLog.Test("parts", "disarm", attacker.LabelShort + " disarmed " + victim.LabelShort + " (" + dropped?.def.defName + ", chance " + chance.ToStringPercent() + ")");
            }
        }
    }

    /// <summary>Startup: where the parts research sits on its tab and whether it overlaps anything.</summary>
    [StaticConstructorOnStartup]
    public static class SkeletonPartDefs
    {
        static SkeletonPartDefs()
        {
            foreach (ResearchProjectDef research in new[] { RimshiDefOf.Rimshi_SkeletonFabrication, RimshiDefOf.Rimshi_AdvancedSkeletonParts })
            {
                var overlaps = DefDatabase<ResearchProjectDef>.AllDefsListForReading
                    .Where(r => r != research && r.tab == research.tab && System.Math.Abs(r.ResearchViewX - research.ResearchViewX) < 0.5f && System.Math.Abs(r.ResearchViewY - research.ResearchViewY) < 0.5f)
                    .Select(r => r.defName).ToList();
                string where = research.defName + " at (" + research.ResearchViewX + ", " + research.ResearchViewY + ") on " + research.tab?.defName;
                if (overlaps.Count == 0)
                {
                    RimshiLog.Test("parts", "research", where);
                }
                else
                {
                    RimshiLog.Fail("parts", "research", where + "; overlaps " + string.Join(", ", overlaps));
                }
            }
        }
    }
}
