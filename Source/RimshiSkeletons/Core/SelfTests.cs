using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using LudeonTK;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RimshiSkeletons
{
    /// <summary>
    /// In-game self-tests. They generate a skeleton and a baseliner off-map (never spawned, discarded afterwards), check
    /// what the mod promises, and write one line per check:
    ///   [Rimshi][T] selftest | name | detail          (pass)
    ///   [Rimshi][T] selftest | name | FAILED: detail  (fail)
    /// then a summary line. They run once per session when a game is loaded or started with Development mode on, and on
    /// demand from the debug actions menu (Rimshi Skeletons > Run self-tests). Every check name needs a row in
    /// tools/rimshi_testlog.py; tests/test_checklist.py fails CI if one is missing.
    /// </summary>
    public static class SelfTests
    {
        private static bool ranThisSession;
        private static int passed;
        private static int failed;

        public static void RunIfDevMode()
        {
            if (Prefs.DevMode && !ranThisSession)
            {
                Run();
            }
        }

        [DebugAction("Rimshi Skeletons", "Run self-tests", allowedGameStates = AllowedGameStates.Playing)]
        public static void Run()
        {
            ranThisSession = true;
            passed = 0;
            failed = 0;
            var pawns = new List<Pawn>();
            try
            {
                Pawn skeleton = Generate(RimshiDefOf.Rimshi_Skeleton);
                Pawn human = Generate(XenotypeDefOf.Baseliner);
                Check("generate", skeleton != null && human != null, "skeleton " + skeleton?.LabelShort + ", baseliner " + human?.LabelShort);
                if (skeleton == null || human == null)
                {
                    return;
                }
                pawns.Add(skeleton);
                pawns.Add(human);
                SkeletonChecks(skeleton, human);
                HumanChecks(human);
                RepairChecks(skeleton, human);
                GeneProofChecks(skeleton, human);
                ProstheticsChecks(skeleton, human);
                PartsChecks(skeleton, human);
                BloodChecks(skeleton, human);
                DrugChecks(skeleton, human);
                FactionChecks(pawns);
                ReproductionChecks(skeleton, human);
                HordeChecks(pawns, human);
                ConversionChecks(pawns);
                SoftCycleChecks(skeleton, human);
                // Last: it takes the null chromosome off the skeleton, and every check above needs it.
                GeneRemovalCheck(skeleton);
            }
            catch (Exception e)
            {
                Check("no exceptions", false, e.GetType().Name + ": " + e.Message);
            }
            finally
            {
                foreach (Pawn pawn in pawns)
                {
                    Discard(pawn);
                }
                string summary = passed + " passed, " + failed + " failed";
                if (failed == 0)
                {
                    RimshiLog.Test("selftest", "summary", summary);
                }
                else
                {
                    RimshiLog.Fail("selftest", "summary", summary);
                }
            }
        }

        private static void SkeletonChecks(Pawn s, Pawn human)
        {
            Check("xenotype", s.genes.Xenotype == RimshiDefOf.Rimshi_Skeleton, "xenotype " + s.genes.XenotypeLabel);
            Check("genderless", s.gender == Gender.None, "gender " + s.gender);
            Check("asexual", s.story.traits.HasTrait(TraitDefOf.Asexual), "traits " + string.Join(", ", s.story.traits.allTraits.ConvertAll(t => t.def.defName)));
            Check("body type", SkeletonUtility.IsSkeletonBodyType(s.story.bodyType), "body " + s.story.bodyType?.defName);
            Check("head", s.story.headType != null && s.story.headType.defName.StartsWith("Rimshi_Head_"), "head " + s.story.headType?.defName);
            Check("plating", s.story.skinColorOverride.HasValue, "skin " + s.story.SkinColor);
            Check("bald", s.story.hairDef == null || s.story.hairDef == HairDefOf.Bald, "hair " + s.story.hairDef?.defName);

            string bodyPath = SkeletonUtility.BodyGraphicPathFor(s.story.bodyType);
            Check("body texture", bodyPath != null && ContentFinder<Texture2D>.Get(bodyPath + "_south", false) != null, "path " + bodyPath);
            string headPath = s.story.headType?.graphicPath;
            Check("head texture", headPath != null && ContentFinder<Texture2D>.Get(headPath + "_south", false) != null, "path " + headPath);

            Check("no rest need", s.needs.rest == null, "rest need " + (s.needs.rest == null ? "absent" : "present"));
            Check("no relations", s.relations == null || s.relations.DirectRelations.Count == 0, "direct relations " + (s.relations?.DirectRelations.Count ?? 0));
            Check("fertility", s.GetStatValue(StatDefOf.Fertility) == 0f, "fertility " + s.GetStatValue(StatDefOf.Fertility));
            Check("toxic resistance", s.GetStatValue(StatDefOf.ToxicResistance) >= 1f, "toxic resistance " + s.GetStatValue(StatDefOf.ToxicResistance));
            Check("psychically deaf", s.GetStatValue(StatDefOf.PsychicSensitivity) == 0f, "psychic sensitivity " + s.GetStatValue(StatDefOf.PsychicSensitivity));
            HediffDef flu = DefDatabase<HediffDef>.GetNamedSilentFail("Flu");
            Check("immunity", flu != null && s.health.immunity.AnyGeneMakesFullyImmuneTo(flu), "immune to flu");

            ThingDef component = ThingDefOf.ComponentIndustrial;
            Check("eats components", s.FoodIsSuitable(component), "component suitable " + s.FoodIsSuitable(component));
            Check("refuses meals", !s.FoodIsSuitable(ThingDefOf.MealSimple), "simple meal suitable " + s.FoodIsSuitable(ThingDefOf.MealSimple));

            float perDay = s.needs.food.FoodFallPerTickAssumingCategory(HungerCategory.Fed) * GenDate.TicksPerDay;
            float nutrition = component.GetStatValueAbstract(StatDefOf.Nutrition);
            float days = perDay > 0f ? nutrition / perDay : -1f;
            Check("maintenance rate", Mathf.Abs(days - SkeletonNeeds.DaysPerItem) < 0.05f, "one component lasts " + days.ToString("0.00") + " days");
            int toEat = FoodUtility.WillIngestStackCountOf(s, component, nutrition);
            Check("one component", toEat == 1, "takes " + toEat + " at once");

            NeedRelabel food = SkeletonNeeds.RelabelFor(s, NeedDefOf.Food);
            Check("maintenance label", SkeletonNeeds.LabelCapFor(s.needs.food) == "Maintenance" && food != null && food.description.Contains("Second Empire")
                && SkeletonNeeds.LabelCapFor(human.needs.food) == human.needs.food.LabelCap, "skeleton " + SkeletonNeeds.LabelCapFor(s.needs.food) + ", human "
                + SkeletonNeeds.LabelCapFor(human.needs.food));
            HealthUtility.AdjustSeverity(s, HediffDefOf.Malnutrition, 0.5f);
            Check("no malnutrition", !s.health.hediffSet.HasHediff(HediffDefOf.Malnutrition), "malnutrition " + s.health.hediffSet.HasHediff(HediffDefOf.Malnutrition));

            Check("hunger thought", ThoughtUtility.ThoughtNullified(s, NeedFood), "NeedFood nullified");
            ThoughtDef maintenance = DefDatabase<ThoughtDef>.GetNamedSilentFail("Rimshi_MaintenanceNeed");
            Check("maintenance thought", maintenance != null && ThoughtUtility.CanGetThought(s, maintenance), "Rimshi_MaintenanceNeed can apply");
            DubsNeedChecks(s, human);
            DeathrestChecks(s, human);
            MethodBase packFood = AccessTools.Method(typeof(JobGiver_PackFood), "TryGiveJob");
            Patches packInfo = packFood == null ? null : Harmony.GetPatchInfo(packFood);
            Check("component patches", packInfo != null && packInfo.Owners.Contains(RimshiMod.HarmonyId), packFood == null ? "JobGiver_PackFood.TryGiveJob missing" : "packing patch applied " + (packInfo != null && packInfo.Owners.Contains(RimshiMod.HarmonyId)));
            // Apply runs before the self-tests, so a missing policy means the player deleted it.
            FoodPolicy skeletonPolicy = SkeletonFoodPolicies.SkeletonPolicy;
            bool deleted = skeletonPolicy == null && (RimshiGameComponent.Instance?.FoodPoliciesSet ?? false);
            Check("skeleton food policy", deleted || (skeletonPolicy != null && skeletonPolicy.filter.Allows(component) && skeletonPolicy.filter.AllowedDefCount == 1),
                skeletonPolicy == null ? (deleted ? "deleted by the player" : "no " + SkeletonFoodPolicies.PolicyLabel + " policy")
                    : SkeletonFoodPolicies.PolicyLabel + " policy allows " + string.Join(", ", skeletonPolicy.filter.AllowedThingDefs.Select(d => d.defName)));
            Check("blocked thought", ThoughtUtility.ThoughtNullified(s, ThoughtDefOf.Naked), "Naked nullified");

            Check("backstory", s.story.Adulthood != null && s.story.Adulthood.spawnCategories.Contains(SkeletonUtility.BackstoryCategory),
                "backstories " + s.story.Childhood?.defName + ", " + s.story.Adulthood?.defName);
        }

        /// <summary>Deathrest underneath the CPU soft cycle: skeletons have the vanilla deathrest need (CPU integrity) and gene.</summary>
        private static void DeathrestChecks(Pawn s, Pawn human)
        {
            NeedDef deathrest = DefDatabase<NeedDef>.GetNamedSilentFail("Deathrest");
            Check("deathrest", s.CanDeathrest() && deathrest != null && s.needs.TryGetNeed(deathrest) != null && !human.CanDeathrest()
                && ContentFinder<Texture2D>.Get(RimshiDefOf.Rimshi_Deathrest.iconPath, false) != null,
                "skeleton deathrests " + s.CanDeathrest() + ", need " + (deathrest != null && s.needs.TryGetNeed(deathrest) != null) + ", human deathrests " + human.CanDeathrest());
            NeedRelabel integrity = SkeletonNeeds.RelabelFor(s, deathrest);
            Check("deathrest label", integrity != null && integrity.label == "CPU integrity" && integrity.description.Contains("soft cycle"), "need shown as " + integrity?.label);
        }

        /// <summary>
        /// CPU soft cycle (docs/design/cpu-soft-cycle.md): bed power and length, the integrity curve, completing and
        /// cancelling, the overload stages and the rib cage. Changes the skeleton's memories, skills and hediffs, so it runs
        /// after the other skeleton checks.
        /// </summary>
        private static void SoftCycleChecks(Pawn s, Pawn human)
        {
            ThingDef bed = RimshiDefOf.Rimshi_SkeletonRepairBed;
            CompProperties_Rimshi_RepairBed bedProps = bed.GetCompProperties<CompProperties_Rimshi_RepairBed>();
            Check("soft cycle bed", bedProps != null && bedProps.cycleWattsPerSkillLevel == 15f && bed.GetCompProperties<CompProperties_Refuelable>() == null,
                "cycle draw " + bedProps?.cycleWattsPerSkillLevel + " W per skill level (" + SoftCycle.SkillLevels(s) * (bedProps?.cycleWattsPerSkillLevel ?? 0f) + " W for the test skeleton), chemfuel tank "
                + (bed.GetCompProperties<CompProperties_Refuelable>() != null));
            Gene_Deathrest gene = SoftCycle.GeneOf(s);
            Check("soft cycle length", gene != null && Mathf.Abs(SoftCycle.BedTicksTotal(gene) - SoftCycle.BedCycleTicks) < 60, "bed cycle " + (gene == null ? "no gene" : SoftCycle.BedTicksTotal(gene).ToStringTicksToPeriod()));

            SimpleCurve curve = SoftCycle.StandingCurve;
            bool rising = true;
            for (int day = 1; day <= 5; day++)
            {
                rising &= curve.Evaluate(day) > curve.Evaluate(day - 1) && (day < 2 || curve.Evaluate(day) - curve.Evaluate(day - 1) >= curve.Evaluate(day - 1) - curve.Evaluate(day - 2));
            }
            Check("standing curve", rising && curve.Evaluate(3f) <= 0.2f && curve.Evaluate(5f) >= 1f, "after 3 days " + curve.Evaluate(3f).ToStringPercent() + ", after 4 " + curve.Evaluate(4f).ToStringPercent());

            Need_Deathrest need = SoftCycle.Integrity(s);
            if (need != null)
            {
                need.CurLevel = 0.5f;
                SoftCycle.Raise(s, 0f, 0.5f);
                float half = need.CurLevel;
                SoftCycle.Raise(s, 0.5f, 1f);
                Check("integrity raise", Mathf.Abs(half - 0.75f) < 0.01f && need.CurLevel >= 0.999f, "50% integrity, half a cycle " + half.ToStringPercent() + ", whole cycle " + need.CurLevel.ToStringPercent());
            }

            if (need != null)
            {
                need.CurLevel = 0.4f;
            }
            SoftCycle.Cancel(s, SoftCycleKind.RepairBed, "self-test");
            float keptLevel = need?.CurLevel ?? -1f;
            bool bedOverload = CpuOverload.Of(s) != null;
            Check("bed wake keeps", Mathf.Approximately(keptLevel, 0.4f) && !bedOverload, "bed cycle cut short: integrity " + keptLevel.ToStringPercent() + ", overload " + bedOverload);
            SoftCycle.Cancel(s, SoftCycleKind.Standing, "self-test");
            Hediff overload = CpuOverload.Of(s);
            Check("cancel overloads", overload != null && need != null && need.CurLevel <= 0f, "standing cycle cut short: integrity " + need?.CurLevelPercentage.ToStringPercent() + ", overload " + (overload != null));
            Hediff resetting = s.health.AddHediff(RimshiDefOf.Rimshi_SoftResetting);
            bool foodHeld = false, integrityHeld = true;
            if (s.needs?.food != null)
            {
                Patch_RibCageStasis.Postfix(s.needs.food, s, ref foodHeld);
            }
            if (need != null)
            {
                integrityHeld = false;
                Patch_RibCageStasis.Postfix(need, s, ref integrityHeld);
            }
            s.health.RemoveHediff(resetting);
            Check("cycle needs held", foodHeld && !integrityHeld, "while soft cycling: maintenance frozen " + foodHeld + ", CPU integrity frozen " + integrityHeld);
            ThoughtDef exhaustion = DefDatabase<ThoughtDef>.AllDefsListForReading.FirstOrDefault(t => t.workerClass == typeof(ThoughtWorker_DeathrestExhaustion));
            Check("no exhaustion thought", exhaustion != null && need != null && need.CurLevel <= 0f && !exhaustion.Worker.CurrentState(s).Active,
                "vanilla thought " + (exhaustion?.defName ?? "missing") + " active " + exhaustion?.Worker.CurrentState(s).Active + " at integrity " + need?.CurLevelPercentage.ToStringPercent());
            Gene_Deathrest cpuGene = SoftCycle.GeneOf(s);
            List<Gizmo> gizmos = cpuGene == null ? new List<Gizmo>() : Patch_SoftCycleGizmos.Postfix(new Gizmo[] { new GeneGizmo_DeathrestCapacity(cpuGene) }, cpuGene).ToList();
            Check("cpu gizmo", gizmos.OfType<GeneGizmo_Rimshi_CpuIntegrity>().Count() == 1 && !gizmos.Any(g => g.GetType() == typeof(GeneGizmo_DeathrestCapacity)),
                "gizmos " + string.Join(", ", gizmos.Select(g => g.GetType().Name)));
            string tip = need?.GetTipString() ?? "";
            Check("cpu tip", tip.Contains("CPU integrity") && tip.IndexOf("deathrest", System.StringComparison.OrdinalIgnoreCase) < 0,
                "integrity tooltip: " + tip.Replace("\n", " / "));
            MethodBase[] wakeOnly = { AccessTools.Method(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.TryTakeOrderedJob)), AccessTools.PropertySetter(typeof(Pawn_DraftController), nameof(Pawn_DraftController.Drafted)), AccessTools.Method(typeof(Pawn_DraftController), "GetGizmos") };
            List<string> unguarded = wakeOnly.Where(m => m == null || !(Harmony.GetPatchInfo(m)?.Owners.Contains(RimshiMod.HarmonyId) ?? false)).Select(m => m == null ? "missing method" : m.DeclaringType.Name + "." + m.Name).ToList();
            Check("wake only", unguarded.Count == 0, unguarded.Count == 0 ? "orders, drafting and the draft button are guarded during a standing soft cycle" : "not patched: " + string.Join(", ", unguarded));
            ThingDef ribCage = DefDatabase<ThingDef>.GetNamedSilentFail("Rimshi_RibCage");
            Check("rib cage draw", ribCage != null && !ribCage.graphicData.drawRotated && !ribCage.building.bed_UseSheetColor,
                "rib cage drawRotated " + ribCage?.graphicData.drawRotated + ", sheet colour " + ribCage?.building.bed_UseSheetColor);
            if (overload != null)
            {
                float moving = OverloadFactor(overload, PawnCapacityDefOf.Moving);
                overload.Severity = 3f;
                int steady = overload.CurStageIndex;
                overload.Severity = 5.5f;
                bool mad = CpuOverload.Mad(s);
                float fast = OverloadFactor(overload, PawnCapacityDefOf.Moving);
                Check("overload stages", Mathf.Approximately(moving, 0.8f) && steady == CpuOverload.SteadyStage && mad && Mathf.Approximately(fast, 1.1f) && CpuOverload.MadnessPenalty(overload) == 2,
                    "moving x" + moving + " sluggish, stage " + steady + " at day 3, paranoid " + mad + " moving x" + fast + ", penalty -" + CpuOverload.MadnessPenalty(overload) + " on day 5.5");
                ThoughtDef madness = DefDatabase<ThoughtDef>.GetNamedSilentFail("Rimshi_CpuMadness");
                Check("madness thought", madness != null && madness.stages.Count == 4 && madness.Worker.CurrentState(s).Active && ThoughtUtility.CanGetThought(s, madness)
                    && !madness.Worker.CurrentState(human).Active, "paranoid thought active " + madness?.Worker.CurrentState(s).Active);
            }
            MentalBreakDef berserk = DefDatabase<MentalBreakDef>.GetNamedSilentFail("Berserk");
            MentalBreakDef sad = DefDatabase<MentalBreakDef>.GetNamedSilentFail("Wander_Sad");
            MethodBase breaks = AccessTools.PropertyGetter(typeof(MentalBreaker), "CurrentPossibleMoodBreaks");
            Check("violent breaks", CpuOverload.IsViolent(berserk) && !CpuOverload.IsViolent(sad) && breaks != null && (Harmony.GetPatchInfo(breaks)?.Owners.Contains(RimshiMod.HarmonyId) ?? false),
                "berserk violent " + CpuOverload.IsViolent(berserk) + ", sad wander violent " + CpuOverload.IsViolent(sad));

            s.needs.mood?.thoughts.memories.TryGainMemory(ThoughtDefOf.AteWithoutTable);
            SoftCycle.Complete(s, SoftCycleKind.RepairBed);
            List<Thought_Memory> memories = s.needs.mood?.thoughts.memories.Memories;
            Check("cycle complete", CpuOverload.Of(s) == null && need != null && need.CurLevel >= 0.999f && memories != null && memories.Count == 1 && memories[0].def == RimshiDefOf.Rimshi_SoftCycleHoned,
                "overload " + (CpuOverload.Of(s) != null) + ", integrity " + need?.CurLevelPercentage.ToStringPercent() + ", memories " + string.Join(", ", memories?.Select(m => m.def.defName) ?? Enumerable.Empty<string>()));

            ThingDef cage = RimshiDefOf.Rimshi_RibCage;
            ResearchProjectDef mechtech = DefDatabase<ResearchProjectDef>.GetNamedSilentFail("BasicMechtech");
            Check("rib cage", cage.IsBed && !cage.rotatable && cage.size == new IntVec2(1, 2) && cage.building.bed_defaultMedical && cage.minifiedDef == null
                && cage.researchPrerequisites != null && cage.researchPrerequisites.Contains(mechtech)
                && cage.CostList.Any(c => c.thingDef == ThingDefOf.Steel && c.count == 80) && cage.CostList.Any(c => c.thingDef == ThingDefOf.ComponentIndustrial && c.count == 3)
                && RestUtility.CanUseBedEver(s, cage) && !RestUtility.CanUseBedEver(human, cage),
                "1x2 unrotatable medical bed, 80 steel + 3 components, basic mechtech; skeleton can use " + RestUtility.CanUseBedEver(s, cage) + ", human " + RestUtility.CanUseBedEver(human, cage));
            CompProperties_Rimshi_RibCage cageProps = cage.GetCompProperties<CompProperties_Rimshi_RibCage>();
            Check("rib cage art", ContentFinder<Texture2D>.Get(cage.graphicData.texPath, false) != null && cageProps != null && ContentFinder<Texture2D>.Get(cageProps.frontTexPath, false) != null,
                cage.graphicData.texPath + " and " + cageProps?.frontTexPath);
            MethodBase frozen = AccessTools.PropertyGetter(typeof(Need), "IsFrozen");
            var medical = (List<ThingDef>)AccessTools.Field(typeof(RestUtility), "bedDefsBestToWorst_Medical")?.GetValue(null) ?? new List<ThingDef>();
            int bedRank = medical.IndexOf(bed);
            int cageRank = medical.IndexOf(cage);
            int hospitalRank = medical.IndexOf(DefDatabase<ThingDef>.GetNamed("HospitalBed"));
            Check("rib cage stasis", frozen != null && (Harmony.GetPatchInfo(frozen)?.Owners.Contains(RimshiMod.HarmonyId) ?? false) && bedRank >= 0 && cageRank > bedRank && cageRank < hospitalRank,
                "need freeze patched, rescue order repair bed " + bedRank + ", rib cage " + cageRank + ", hospital bed " + hospitalRank);
        }

        /// <summary>The capacity post factor an overload stage gives.</summary>
        private static float OverloadFactor(Hediff overload, PawnCapacityDef capacity)
        {
            PawnCapacityModifier mod = overload.CurStage?.capMods?.FirstOrDefault(c => c.capacity == capacity);
            return mod?.postFactor ?? 1f;
        }

        /// <summary>Dubs Bad Hygiene (optional): skeletons lose thirst and bladder, and hygiene is self-cleaning at half rate.</summary>
        private static void DubsNeedChecks(Pawn s, Pawn human)
        {
            NeedDef thirst = DefDatabase<NeedDef>.GetNamedSilentFail("DBHThirst");
            NeedDef bladder = DefDatabase<NeedDef>.GetNamedSilentFail("Bladder");
            NeedDef hygiene = DefDatabase<NeedDef>.GetNamedSilentFail("Hygiene");
            StatDef hygieneRate = DefDatabase<StatDef>.GetNamedSilentFail("HygieneRateMultiplier");
            if (thirst == null && bladder == null && hygiene == null)
            {
                Check("dubs needs", true, "Dubs Bad Hygiene not loaded");
                return;
            }
            bool removed = (thirst == null || (SkeletonNeeds.RemovesNeed(s, thirst) && s.needs.TryGetNeed(thirst) == null && !SkeletonNeeds.RemovesNeed(human, thirst)))
                && (bladder == null || (SkeletonNeeds.RemovesNeed(s, bladder) && s.needs.TryGetNeed(bladder) == null));
            NeedRelabel selfCleaning = hygiene == null ? null : SkeletonNeeds.RelabelFor(s, hygiene);
            float rate = hygieneRate == null ? -1f : s.GetStatValue(hygieneRate);
            Check("dubs needs", removed && (hygiene == null || (selfCleaning?.label == "self-cleaning" && SkeletonNeeds.RelabelFor(human, hygiene) == null))
                && (hygieneRate == null || Mathf.Approximately(rate, 0.5f)),
                "thirst and bladder removed " + removed + ", hygiene shown as " + (selfCleaning?.label ?? "hygiene") + ", hygiene rate " + rate);
        }

        private static void FactionChecks(List<Pawn> pawns)
        {
            FactionDef imperium = RimshiDefOf.Rimshi_SkeletalImperium;
            Check("faction def", imperium.pawnGroupMakers != null && imperium.pawnGroupMakers.Count > 0 && RimshiDefOf.Rimshi_PlayerSkeletons.isPlayer,
                imperium.defName + " with " + (imperium.pawnGroupMakers?.Count ?? 0) + " pawn groups, player faction " + RimshiDefOf.Rimshi_PlayerSkeletons.defName);
            PawnKindDef guardKind = DefDatabase<PawnKindDef>.GetNamed("Rimshi_TownGuard");
            Pawn guard = PawnGenerator.GeneratePawn(new PawnGenerationRequest(guardKind, null, forceGenerateNewPawn: true, forceNoGear: true));
            pawns.Add(guard);
            Check("faction kind", guard.genes.Xenotype == RimshiDefOf.Rimshi_Skeleton, guardKind.defName + ": xenotype " + guard.genes.XenotypeLabel + ", name " + guard.Name?.ToStringFull);
            Faction inWorld = Find.FactionManager.FirstFactionOfDef(imperium);
            RimshiLog.Test("faction", "in world", inWorld != null ? inWorld.Name : "none (world made without this mod; see Debug actions > Rimshi Skeletons)");
        }

        private static void ReproductionChecks(Pawn s, Pawn human)
        {
            AcceptanceReport child = PregnancyUtility.CanEverProduceChild(s, human);
            Check("cannot reproduce", !child.Accepted && PregnancyUtility.PregnancyChanceForPawn(s) == 0f && PregnancyUtility.PregnancyChanceImplantEmbryo(s) == 0f,
                "child with baseliner " + (child.Accepted ? "allowed" : child.Reason) + ", pregnancy chance " + PregnancyUtility.PregnancyChanceForPawn(s));
            Hediff pregnancy = HediffMaker.MakeHediff(HediffDefOf.PregnantHuman, s);
            s.health.AddHediff(pregnancy);
            bool pregnant = s.health.hediffSet.HasHediff(HediffDefOf.PregnantHuman);
            if (pregnant)
            {
                s.health.RemoveHediff(pregnancy);
            }
            Check("pregnancy refused", !pregnant, "pregnancy hediff " + (pregnant ? "was added" : "refused"));
        }

        private static void HordeChecks(List<Pawn> pawns, Pawn human)
        {
            ThingDef array = RimshiDefOf.Rimshi_RecallArray;
            ResearchProjectDef research = RimshiDefOf.Rimshi_SkeletonRecall;
            Check("recall array", array.HasComp(typeof(CompUseEffect_RecallArray)) && array.HasComp(typeof(CompUsable)) && array.researchPrerequisites != null
                && array.researchPrerequisites.Contains(research) && research.baseCost == 2000f && !research.requiresMechanitor
                && research.prerequisites != null && research.prerequisites.Any(r => r.defName == "BasicMechtech"),
                array.defName + " needs " + research.defName + " (" + research.baseCost + ", after " + string.Join(", ", research.prerequisites?.ConvertAll(r => r.defName) ?? new List<string>())
                + ", needs mechanitor " + research.requiresMechanitor + ")");

            var horde = new List<Pawn>();
            foreach (PawnKindDef kind in SkeletonHorde.AllKinds)
            {
                horde.Add(SkeletonHorde.GeneratePawn(kind));
            }
            pawns.AddRange(horde);
            Check("horde factionless", horde.TrueForAll(p => p.Faction == null && p.genes.Xenotype == RimshiDefOf.Rimshi_Skeleton),
                string.Join(", ", horde.ConvertAll(p => p.kindDef.defName + " faction " + (p.Faction?.Name ?? "none") + " " + p.genes.XenotypeLabel)));
            Check("horde protocol", horde.TrueForAll(p => ReprogramUtility.IsCorrupted(p) && !p.guest.Recruitable),
                string.Join(", ", horde.ConvertAll(p => p.LabelShort + " corrupted " + ReprogramUtility.IsCorrupted(p) + " recruitable " + p.guest.Recruitable)));
            Check("horde gear", horde.TrueForAll(HordeGearOk), string.Join(", ", horde.ConvertAll(p => p.kindDef.defName + ": " + (p.equipment?.Primary?.def.defName ?? "no weapon")
                + (SkeletonHorde.HasShieldBelt(p) ? " + shield belt" : ""))));
            Check("horde peace", !horde[0].HostileTo(horde[1]) && !horde[1].HostileTo(horde[2]), "horde skeletons not hostile to each other");

            Pawn failsafe = horde[0];
            failsafe.health.AddHediff(RimshiDefOf.Rimshi_FailsafeCore);
            float chance = failsafe.health.overrideDeathOnDownedChance;
            Check("failsafe", Mathf.Approximately(chance, RimshiDefOf.Rimshi_FailsafeCore.CompProps<HediffCompProperties_FailsafeCore>().deathOnDownedChance),
                "death on downed chance " + chance.ToStringPercent());

            Pawn prisoner = horde[1];
            Hediff protocol = ReprogramUtility.Protocol(prisoner);
            float before = protocol.Severity;
            ReprogramUtility.DoSession(human, prisoner);
            Check("reprogram session", protocol.Severity < before && ReprogramUtility.IsCorrupted(prisoner),
                "corruption " + before.ToStringPercent() + " to " + protocol.Severity.ToStringPercent() + " (warden Intellectual " + human.skills.GetSkill(SkillDefOf.Intellectual).Level + ")");
        }

        /// <summary>Gunners carry a spacer gun; bladeguards a melee weapon and a shield belt.</summary>
        private static bool HordeGearOk(Pawn p)
        {
            ThingWithComps weapon = p.equipment?.Primary;
            if (weapon == null)
            {
                return false;
            }
            if (p.kindDef.defName == "Rimshi_HordeBladeguard")
            {
                return weapon.def.IsMeleeWeapon && SkeletonHorde.HasShieldBelt(p);
            }
            return weapon.def.weaponTags != null && weapon.def.weaponTags.Contains("SpacerGun");
        }

        /// <summary>
        /// Only with Rim-Skeletons (Continued) loaded: every old body part lands on a human part, and an old skeleton
        /// generated off the map converts into one of ours.
        /// </summary>
        private static void ConversionChecks(List<Pawn> pawns)
        {
            ThingDef oldRace = OldSkeletonConversion.OldRace;
            BodyDef oldBody = DefDatabase<BodyDef>.GetNamedSilentFail(OldSkeletonConversion.OldBodyName);
            if (oldRace == null || oldBody == null)
            {
                return;
            }
            BodyDef human = ThingDefOf.Human.race.body;
            var unmapped = oldBody.AllParts.Where(p => OldSkeletonConversion.MapPart(p, oldBody, human) == null).Select(p => p.def.defName).ToList();
            var targets = oldBody.AllParts.Select(p => OldSkeletonConversion.MapPart(p, oldBody, human)).Where(p => p != null).ToList();
            Check("convert parts", unmapped.Count == 0 && targets.Distinct().Count() == targets.Count,
                oldBody.AllParts.Count + " old parts, unmapped: " + (unmapped.Count == 0 ? "none" : string.Join(", ", unmapped))
                + ", shared targets " + (targets.Count - targets.Distinct().Count()));

            PawnKindDef oldKind = DefDatabase<PawnKindDef>.GetNamedSilentFail("Skeleton_Colonist");
            if (oldKind == null)
            {
                return;
            }
            Pawn old = PawnGenerator.GeneratePawn(new PawnGenerationRequest(oldKind, null, forceGenerateNewPawn: true, forceNoGear: true));
            pawns.Add(old);
            var lost = new List<string>();
            OldSkeletonConversion.Convert(old, oldRace, lost);
            Check("convert race", old.def == ThingDefOf.Human && old.RaceProps.body == human, "race " + old.def.defName + ", body " + old.RaceProps.body.defName);
            Check("convert xenotype", old.genes.Xenotype == RimshiDefOf.Rimshi_Skeleton && old.gender == Gender.None, "xenotype " + old.genes.XenotypeLabel + ", gender " + old.gender);
            Check("convert story", old.story.headType.defName.StartsWith("Rimshi_Head_") && old.story.Adulthood != null
                && old.story.Adulthood.spawnCategories.Contains(SkeletonUtility.BackstoryCategory),
                "head " + old.story.headType.defName + ", backstories " + old.story.Childhood?.defName + ", " + old.story.Adulthood?.defName);
            Check("convert kind", old.kindDef.defName == "Rimshi_Colonist", "kind " + old.kindDef.defName);
            Check("convert health", !old.health.hediffSet.hediffs.Any(h => h.def.defName == OldSkeletonConversion.OldHediffName)
                && old.health.hediffSet.hediffs.All(h => h.Part == null || h.Part.body == human) && lost.Count == 0,
                old.health.hediffSet.hediffs.Count + " health conditions, lost " + lost.Count);
            Check("convert again", !OldSkeletonConversion.IsOldSkeleton(old, oldRace), "converted pawn no longer matches");
        }

        private static void HumanChecks(Pawn h)
        {
            Check("human refuses components", !h.FoodIsSuitable(ThingDefOf.ComponentIndustrial), "component suitable " + h.FoodIsSuitable(ThingDefOf.ComponentIndustrial));
            Check("human eats meals", h.FoodIsSuitable(ThingDefOf.MealSimple), "simple meal suitable " + h.FoodIsSuitable(ThingDefOf.MealSimple));
            Check("human unchanged", h.gender != Gender.None && !ThoughtUtility.ThoughtNullified(h, NeedFood),
                "gender " + h.gender + ", hunger thought nullified " + ThoughtUtility.ThoughtNullified(h, NeedFood));
        }

        private static void RepairChecks(Pawn s, Pawn h)
        {
            ThingDef kit = RimshiDefOf.Rimshi_RepairKit;
            ThingDef bed = RimshiDefOf.Rimshi_SkeletonRepairBed;
            ThingDef hospitalBed = DefDatabase<ThingDef>.GetNamed("HospitalBed");
            ResearchProjectDef kitResearch = RimshiDefOf.Rimshi_SkeletonRepairKits;
            ResearchProjectDef bedResearch = RimshiDefOf.Rimshi_SkeletonRepairBeds;
            ResearchProjectDef mechtech = DefDatabase<ResearchProjectDef>.GetNamedSilentFail("BasicMechtech");
            Check("repair research", mechtech != null && kitResearch.prerequisites.Contains(mechtech) && bedResearch.prerequisites.Contains(mechtech),
                kitResearch.defName + " and " + bedResearch.defName + " need " + mechtech?.defName);
            RecipeDef kitRecipe = DefDatabase<RecipeDef>.AllDefs.FirstOrDefault(r => r.ProducedThingDef == kit);
            List<ResearchProjectDef> kitNeeds = kitRecipe == null ? new List<ResearchProjectDef>()
                : (kitRecipe.researchPrerequisites ?? new List<ResearchProjectDef>()).Concat(kitRecipe.researchPrerequisite != null ? new[] { kitRecipe.researchPrerequisite } : new ResearchProjectDef[0]).ToList();
            Check("kit gated", kitRecipe != null && kitNeeds.Contains(kitResearch),
                "recipe " + kitRecipe?.defName + " needs " + string.Join(", ", kitNeeds.ConvertAll(r => r.defName)));
            Check("bed gated", bed.researchPrerequisites != null && bed.researchPrerequisites.Contains(bedResearch)
                && bed.CostList.Any(c => c.thingDef == DefDatabase<ThingDef>.GetNamed("AIPersonaCore")), "bed needs " + string.Join(", ", bed.researchPrerequisites?.ConvertAll(r => r.defName) ?? new List<string>()));
            Check("kit not medicine", !kit.IsMedicine, "repair kit is medicine " + kit.IsMedicine + "; vanilla tending never uses it");
            Check("no healing", s.GetStatValue(StatDefOf.InjuryHealingFactor) == 0f && h.GetStatValue(StatDefOf.InjuryHealingFactor) > 0f,
                "injury healing: skeleton " + s.GetStatValue(StatDefOf.InjuryHealingFactor).ToStringPercent() + ", human " + h.GetStatValue(StatDefOf.InjuryHealingFactor).ToStringPercent());
            WorkGiverDef repairGiver = DefDatabase<WorkGiverDef>.GetNamedSilentFail("Rimshi_RepairSkeleton");
            Check("repair work", repairGiver != null && repairGiver.workType.defName == "Smithing" && repairGiver.workType.relevantSkills.Contains(SkillDefOf.Crafting),
                "work giver in " + repairGiver?.workType?.defName);
            BodyPartRecord torso = s.RaceProps.body.corePart;
            Hediff cut = HediffMaker.MakeHediff(HediffDefOf.Cut, s, torso);
            cut.Severity = 4f;
            s.health.AddHediff(cut, torso);
            Hediff humanCut = HediffMaker.MakeHediff(HediffDefOf.Cut, h, h.RaceProps.body.corePart);
            humanCut.Severity = 4f;
            h.health.AddHediff(humanCut, h.RaceProps.body.corePart);
            Check("needs repair", SkeletonRepair.NeedsRepair(s) && !SkeletonRepair.NeedsRepair(h) && SkeletonRepair.KitsFor(s) == 1,
                "cut skeleton needs repair " + SkeletonRepair.NeedsRepair(s) + " (" + SkeletonRepair.KitsFor(s) + " kit), cut human " + SkeletonRepair.NeedsRepair(h));
            Check("no tending", !cut.TendableNow() && !s.health.HasHediffsNeedingTend() && humanCut.TendableNow(),
                "skeleton cut tendable " + cut.TendableNow() + ", skeleton needs tending " + s.health.HasHediffsNeedingTend() + ", human cut tendable " + humanCut.TendableNow());
            s.health.RemoveHediff(cut);
            h.health.RemoveHediff(humanCut);
            Check("bed users", RestUtility.CanUseBedEver(s, bed) && !RestUtility.CanUseBedEver(h, bed) && RestUtility.CanUseBedEver(s, hospitalBed),
                "skeleton " + RestUtility.CanUseBedEver(s, bed) + ", human " + RestUtility.CanUseBedEver(h, bed) + ", skeleton in hospital bed " + RestUtility.CanUseBedEver(s, hospitalBed));
        }

        private static void GeneProofChecks(Pawn s, Pawn human)
        {
            List<GeneDef> ours = DefDatabase<GeneDef>.AllDefsListForReading.FindAll(SkeletonUtility.IsSkeletonGene);
            List<GeneDef> rollable = ours.FindAll(g => g.canGenerateInGeneSet);
            Check("gene set flag", ours.Count > 0 && rollable.Count == 0, ours.Count + " skeleton genes"
                + (rollable.Count > 0 ? "; can roll in gene sets: " + string.Join(", ", rollable.ConvertAll(g => g.defName)) : ", none can roll in gene sets"));

            List<GeneDef> unmarked = ours.FindAll(g => SkeletonChips.KindOf(g) == null);
            bool chipCategory = DefDatabase<GeneCategoryDef>.GetNamedSilentFail(SkeletonChips.ChipCategory) != null;
            Check("gene kinds", chipCategory && unmarked.Count == 0, ours.FindAll(SkeletonChips.IsCoreGene).Count + " core genes, "
                + ours.FindAll(SkeletonChips.IsChip).Count + " chips, chip category " + (chipCategory ? "loaded" : "missing")
                + (unmarked.Count > 0 ? "; not marked core or chip: " + string.Join(", ", unmarked.ConvertAll(g => g.defName)) : ""));

            int leaked = 0;
            for (int i = 0; i < 200; i++)
            {
                if (GeneUtility.GenerateGeneSet().GenesListForReading.Any(SkeletonUtility.IsSkeletonGene))
                {
                    leaked++;
                }
            }
            Check("gene sets clean", leaked == 0, leaked + " of 200 random gene sets held a skeleton gene");

            GeneDef neversleep = DefDatabase<GeneDef>.GetNamed("Neversleep");
            var pack = (Genepack)ThingMaker.MakeThing(ThingDefOf.Genepack);
            pack.Initialize(new List<GeneDef> { RimshiDefOf.Rimshi_FuelReactor, neversleep });
            List<GeneDef> packed = pack.GeneSet.GenesListForReading;
            Check("genepack strip", !packed.Any(SkeletonUtility.IsSkeletonGene) && packed.Contains(neversleep),
                "genepack holds " + string.Join(", ", packed.ConvertAll(g => g.defName)));
            pack.Destroy();

            Check("gene lock", GeneProofing.Refuses(s) && !GeneProofing.Refuses(human), "skeleton refused " + GeneProofing.Refuses(s) + ", baseliner refused " + GeneProofing.Refuses(human));

            RecipeDef implant = DefDatabase<RecipeDef>.GetNamedSilentFail("ImplantXenogerm");
            Check("implant bill", implant != null && !implant.Worker.AvailableOnNow(s), "implant xenogerm bill on skeleton available " + implant?.Worker.AvailableOnNow(s));

            var unpatched = new List<string>();
            foreach (MethodBase method in GeneProofTargets)
            {
                Patches info = method == null ? null : Harmony.GetPatchInfo(method);
                if (info == null || !info.Owners.Contains(RimshiMod.HarmonyId))
                {
                    unpatched.Add(method == null ? "missing method" : method.DeclaringType.Name + "." + method.Name);
                }
            }
            Check("gene lock patched", unpatched.Count == 0, unpatched.Count == 0 ? GeneProofTargets.Length + " gene lock patches applied" : "not patched: " + string.Join(", ", unpatched));
        }

        private static readonly MethodBase[] GeneProofTargets =
        {
            AccessTools.Method(typeof(Building_GeneExtractor), nameof(Building_GeneExtractor.CanAcceptPawn)),
            AccessTools.Method(typeof(Xenogerm), nameof(Xenogerm.ValidateTarget)),
            AccessTools.Method(typeof(Recipe_ImplantXenogerm), nameof(Recipe_ImplantXenogerm.AvailableOnNow)),
            // Valid is overloaded (the base class adds a GlobalTargetInfo one), so a name-only lookup is ambiguous.
            AccessTools.Method(typeof(CompAbilityEffect_ReimplantXenogerm), nameof(CompAbilityEffect_ReimplantXenogerm.Valid),
                new[] { typeof(LocalTargetInfo), typeof(bool) }),
            AccessTools.Method(typeof(JobDriver_AbsorbXenogerm), nameof(JobDriver_AbsorbXenogerm.TryMakePreToilReservations)),
            AccessTools.Method(typeof(GeneUtility), nameof(GeneUtility.ImplantXenogermItem)),
            AccessTools.Method(typeof(GeneUtility), nameof(GeneUtility.ReimplantXenogerm)),
            AccessTools.Method(typeof(Genepack), nameof(Genepack.Initialize)),
        };

        private static void ProstheticsChecks(Pawn s, Pawn human)
        {
            // A throwaway kind that always rolls bionics, so the baseliner shows the generator still works for others.
            var bionicKind = new PawnKindDef
            {
                defName = "Rimshi_SelfTestBionics",
                race = ThingDefOf.Human,
                techHediffsChance = 1f,
                techHediffsMaxAmount = 3,
                techHediffsMoney = new FloatRange(10000f, 10000f),
                techHediffsTags = new List<string> { "Simple", "Advanced" },
            };
            int skeletonParts = AddedParts(s);
            int humanParts = AddedParts(human);
            PawnKindDef skeletonKind = s.kindDef;
            PawnKindDef humanKind = human.kindDef;
            try
            {
                s.kindDef = bionicKind;
                human.kindDef = bionicKind;
                PawnTechHediffsGenerator.GenerateTechHediffsFor(s);
                PawnTechHediffsGenerator.GenerateTechHediffsFor(human);
            }
            finally
            {
                s.kindDef = skeletonKind;
                human.kindDef = humanKind;
            }
            int skeletonAdded = AddedParts(s) - skeletonParts;
            int humanAdded = AddedParts(human) - humanParts;
            Check("no spawn bionics", skeletonAdded == 0 && humanAdded > 0, "bionics generated: skeleton " + skeletonAdded + ", baseliner " + humanAdded);

            var installs = DefDatabase<RecipeDef>.AllDefsListForReading.FindAll(ProstheticsLock.IsInstallRecipe);
            var offered = installs.FindAll(r => r.Worker.AvailableOnNow(s));
            RecipeDef arm = DefDatabase<RecipeDef>.GetNamedSilentFail("InstallBionicArm");
            RecipeDef heart = DefDatabase<RecipeDef>.GetNamedSilentFail("InstallNaturalHeart");
            bool controls = arm != null && heart != null && installs.Contains(arm) && installs.Contains(heart)
                && arm.Worker.AvailableOnNow(human) && heart.Worker.AvailableOnNow(human);
            Check("no install bills", installs.Count > 0 && offered.Count == 0 && controls, installs.Count + " install surgeries, "
                + offered.Count + " offered on the skeleton" + (offered.Count > 0 ? " (" + string.Join(", ", offered.Take(5).Select(r => r.defName)) + ")" : "")
                + "; bionic arm and natural heart offered on the baseliner " + controls);

            RecipeDef removal = DefDatabase<RecipeDef>.GetNamedSilentFail("RemoveBodyPart");
            Check("removal allowed", removal != null && !ProstheticsLock.IsInstallRecipe(removal) && removal.Worker.AvailableOnNow(s),
                "remove body part offered on the skeleton " + removal?.Worker.AvailableOnNow(s));

            Check("self implants", ProstheticsLock.AllowsSelfImplant(DefDatabase<HediffDef>.GetNamedSilentFail("MechlinkImplant"))
                && !ProstheticsLock.AllowsSelfImplant(HediffDefOf.PsychicAmplifier), "mechlink allowed, psylink neuroformer refused");

            if (ModsConfig.RoyaltyActive)
            {
                int skeletonCasts = s.abilities.abilities.Count;
                s.ChangePsylinkLevel(1, sendLetter: false);
                human.ChangePsylinkLevel(1, sendLetter: false);
                Check("no psylink", s.GetMainPsylinkSource() == null && s.abilities.abilities.Count == skeletonCasts && human.GetMainPsylinkSource() != null,
                    "skeleton psylink " + (s.GetMainPsylinkSource() != null) + ", new abilities " + (s.abilities.abilities.Count - skeletonCasts)
                    + "; baseliner psylink " + (human.GetMainPsylinkSource() != null));
            }

            var unpatched = new List<string>();
            foreach (MethodBase method in ProstheticsTargets)
            {
                Patches info = method == null ? null : Harmony.GetPatchInfo(method);
                if (info == null || !info.Owners.Contains(RimshiMod.HarmonyId))
                {
                    unpatched.Add(method == null ? "missing method" : method.DeclaringType.Name + "." + method.Name);
                }
            }
            Check("prosthetics patched", unpatched.Count == 0, unpatched.Count == 0 ? ProstheticsTargets.Length + " prosthetics lock patches applied" : "not patched: " + string.Join(", ", unpatched));
        }

        private static void BloodChecks(Pawn s, Pawn h)
        {
            Check("no bleeding", !s.health.CanBleed && h.health.CanBleed, "skeleton can bleed " + s.health.CanBleed + ", baseliner " + h.health.CanBleed);

            BodyPartRecord torso = s.RaceProps.body.corePart;
            Hediff cut = HediffMaker.MakeHediff(HediffDefOf.Cut, s, torso);
            cut.Severity = 6f;
            s.health.AddHediff(cut, torso);
            Hediff humanCut = HediffMaker.MakeHediff(HediffDefOf.Cut, h, h.RaceProps.body.corePart);
            humanCut.Severity = 6f;
            h.health.AddHediff(humanCut, h.RaceProps.body.corePart);
            Check("cut no blood loss", s.health.hediffSet.BleedRateTotal == 0f && !cut.Bleeding && h.health.hediffSet.BleedRateTotal > 0f,
                "bleed rate: cut skeleton " + s.health.hediffSet.BleedRateTotal.ToStringPercent() + ", cut baseliner " + h.health.hediffSet.BleedRateTotal.ToStringPercent());
            s.health.RemoveHediff(cut);
            h.health.RemoveHediff(humanCut);

            ThingDef oil = RimshiDefOf.Rimshi_Filth_AncientOil;
            Check("ancient oil", oil != null && oil.filth != null && oil.label == "ancient oil" && oil.graphicData.color.r < 0.2f
                && RimshiDefOf.Rimshi_Damage_HitAncientOil != null && h.RaceProps.FleshType.damageEffecter != null,
                "filth " + oil?.defName + " colour " + oil?.graphicData?.color + ", hit effect " + RimshiDefOf.Rimshi_Damage_HitAncientOil?.defName
                + " replaces " + h.RaceProps.FleshType.damageEffecter?.defName);

            var unpatched = new List<string>();
            foreach (MethodBase method in BloodTargets)
            {
                Patches info = method == null ? null : Harmony.GetPatchInfo(method);
                if (info == null || !info.Owners.Contains(RimshiMod.HarmonyId))
                {
                    unpatched.Add(method == null ? "missing method" : method.DeclaringType.Name + "." + method.Name);
                }
            }
            Check("blood patched", unpatched.Count == 0, unpatched.Count == 0 ? BloodTargets.Length + " blood patches applied" : "not patched: " + string.Join(", ", unpatched));
        }

        private static void DrugChecks(Pawn s, Pawn h)
        {
            ThingDef beer = ThingDefOf.Beer;
            ThingDef penoxycyline = DefDatabase<ThingDef>.GetNamedSilentFail("Penoxycyline");
            Check("drug refused", !s.CanTakeDrug(beer) && (h.CanTakeDrug(beer) || h.IsTeetotaler()) && penoxycyline != null
                && !s.DrugIsSuitable(penoxycyline) && h.DrugIsSuitable(penoxycyline),
                "beer: skeleton " + s.CanTakeDrug(beer) + ", baseliner " + h.CanTakeDrug(beer) + " (teetotaler " + h.IsTeetotaler()
                + "); penoxycyline suitable: skeleton " + (penoxycyline != null && s.DrugIsSuitable(penoxycyline)) + ", baseliner " + (penoxycyline != null && h.DrugIsSuitable(penoxycyline)));

            // The test pawns have no faction, so they have no drug policy tracker; give them one on a policy that schedules
            // every drug, then one that carries every drug (a policy entry can't do both).
            int skeletonAllowed = PolicyAllowedDrugs(s);
            int humanAllowed = PolicyAllowedDrugs(h);
            Check("drug policy", skeletonAllowed == 0 && humanAllowed > 0,
                "policy allowing every drug: skeleton schedules or carries " + skeletonAllowed + ", baseliner " + humanAllowed);

            var administer = DefDatabase<RecipeDef>.AllDefsListForReading.FindAll(r => r.Worker is Recipe_AdministerIngestible && r.ingredients.Count > 0
                && r.ingredients[0].filter.AllowedThingDefs.Any(DrugLock.IsBlockedDrug));
            var offered = administer.FindAll(r => r.Worker.AvailableOnNow(s));
            int humanOffered = administer.Count(r => r.Worker.AvailableOnNow(h));
            Check("no administer bills", administer.Count > 0 && offered.Count == 0 && humanOffered > 0, administer.Count + " administer drug bills, "
                + offered.Count + " offered on the skeleton" + (offered.Count > 0 ? " (" + string.Join(", ", offered.Take(5).Select(r => r.defName)) + ")" : "")
                + ", " + humanOffered + " on the baseliner");

            var binges = DefDatabase<MentalStateDef>.AllDefsListForReading.FindAll(d => d.stateClass != null && typeof(MentalState_BingingDrug).IsAssignableFrom(d.stateClass));
            var possible = binges.FindAll(d => d.Worker.StateCanOccur(s));
            Check("no drug binge", binges.Count > 0 && possible.Count == 0, binges.Count + " drug binge states, possible on the skeleton: "
                + (possible.Count == 0 ? "none" : string.Join(", ", possible.Select(d => d.defName))));

            var unpatched = new List<string>();
            foreach (MethodBase method in DrugTargets)
            {
                Patches info = method == null ? null : Harmony.GetPatchInfo(method);
                if (info == null || !info.Owners.Contains(RimshiMod.HarmonyId))
                {
                    unpatched.Add(method == null ? "missing method" : method.DeclaringType.Name + "." + method.Name);
                }
            }
            Check("drugs patched", unpatched.Count == 0, unpatched.Count == 0 ? DrugTargets.Length + " drug lock patches applied" : "not patched: " + string.Join(", ", unpatched));
        }

        private static readonly MethodBase[] DrugTargets =
        {
            AccessTools.Method(typeof(PawnUtility), nameof(PawnUtility.CanTakeDrug)),
            AccessTools.Method(typeof(FoodUtility), nameof(FoodUtility.DrugIsSuitable)),
            AccessTools.Method(typeof(Pawn_DrugPolicyTracker), nameof(Pawn_DrugPolicyTracker.AllowedToTakeScheduledEver)),
            AccessTools.Method(typeof(Pawn_DrugPolicyTracker), nameof(Pawn_DrugPolicyTracker.AllowedToTakeToInventory)),
            AccessTools.Method(typeof(JoyGiver_Ingest), "CanIngestForJoy"),
            AccessTools.Method(typeof(JoyGiver_SocialRelax), "TryFindIngestibleToNurse"),
            AccessTools.Method(typeof(JobGiver_TakeCombatEnhancingDrug), "TryGiveJob"),
            AccessTools.Method(typeof(JobGiver_SatisfyChemicalNeed), "TryGiveJob"),
            AccessTools.Method(typeof(JobGiver_TakeDrugsForDrugPolicy), "TryGiveJob"),
            AccessTools.Method(typeof(JobGiver_BingeDrug), "BestIngestTarget"),
            AccessTools.Method(typeof(MentalStateWorker), nameof(MentalStateWorker.StateCanOccur)),
            AccessTools.Method(typeof(Recipe_AdministerIngestible), nameof(Recipe_AdministerIngestible.AvailableOnNow)),
            AccessTools.Method(typeof(PawnInventoryGenerator), "GiveCombatEnhancingDrugs"),
            AccessTools.Method(typeof(CompDrug), nameof(CompDrug.PostIngested)),
            AccessTools.Method(typeof(IngestionOutcomeDoer), nameof(IngestionOutcomeDoer.DoIngestionOutcome)),
            AccessTools.Method(typeof(DrugPolicyUIUtility), nameof(DrugPolicyUIUtility.DoAssignDrugPolicyButtons)),
        };

        private static readonly MethodBase[] BloodTargets =
        {
            AccessTools.PropertyGetter(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.CanBleed)),
            AccessTools.Method(typeof(Pawn), nameof(Pawn.PostApplyDamage)),
            AccessTools.Method(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.DropBloodFilth)),
            AccessTools.Method(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.DropBloodSmear)),
            AccessTools.Method(typeof(Effecter), nameof(Effecter.Trigger)),
            AccessTools.Method(typeof(JobGiver_GetHemogen), nameof(JobGiver_GetHemogen.CanFeedOnPrisoner)),
            AccessTools.Method(typeof(SanguophageUtility), nameof(SanguophageUtility.DoBite)),
        };

        private static int AddedParts(Pawn pawn) =>
            pawn.health.hediffSet.hediffs.Count(h => h.def.countsAsAddedPartOrImplant || h is Hediff_AddedPart || h is Hediff_Implant);

        private static readonly MethodBase[] ProstheticsTargets =
        {
            AccessTools.Method(typeof(PawnTechHediffsGenerator), nameof(PawnTechHediffsGenerator.GenerateTechHediffsFor)),
            AccessTools.Method(typeof(Recipe_Surgery), nameof(Recipe_Surgery.AvailableOnNow)),
            AccessTools.Method(typeof(Recipe_InstallArtificialBodyPart), nameof(Recipe_InstallArtificialBodyPart.ApplyOnPawn)),
            AccessTools.Method(typeof(Recipe_InstallImplant), nameof(Recipe_InstallImplant.ApplyOnPawn)),
            AccessTools.Method(typeof(Recipe_InstallNaturalBodyPart), nameof(Recipe_InstallNaturalBodyPart.ApplyOnPawn)),
            AccessTools.Method(typeof(CompUseEffect_InstallImplant), nameof(CompUseEffect_InstallImplant.CanBeUsedBy)),
            AccessTools.Method(typeof(CompPsylinkable), nameof(CompPsylinkable.CanPsylink)),
            AccessTools.Method(typeof(Hediff_Psylink), nameof(Hediff_Psylink.ChangeLevel), new[] { typeof(int), typeof(bool) }),
        };

        private static int PolicyAllowedDrugs(Pawn pawn)
        {
            if (pawn.drugs == null)
            {
                pawn.drugs = new Pawn_DrugPolicyTracker(pawn);
            }
            var policy = new DrugPolicy(-1, "Rimshi self-test");
            pawn.drugs.CurrentPolicy = policy;
            var allowed = new HashSet<ThingDef>();
            for (int i = 0; i < policy.Count; i++)
            {
                policy[i].allowScheduled = true;
            }
            for (int i = 0; i < policy.Count; i++)
            {
                if (DrugLock.IsBlockedDrug(policy[i].drug) && pawn.drugs.AllowedToTakeScheduledEver(policy[i].drug))
                {
                    allowed.Add(policy[i].drug);
                }
                policy[i].allowScheduled = false;
                policy[i].takeToInventory = 1;
            }
            for (int i = 0; i < policy.Count; i++)
            {
                if (DrugLock.IsBlockedDrug(policy[i].drug) && pawn.drugs.AllowedToTakeToInventory(policy[i].drug))
                {
                    allowed.Add(policy[i].drug);
                }
            }
            return allowed.Count;
        }

        private static void GeneRemovalCheck(Pawn s)
        {
            Gene gene = s.genes.GetGene(RimshiDefOf.Rimshi_NullChromosome);
            if (gene != null)
            {
                s.genes.RemoveGene(gene);
            }
            Check("gene removal", gene != null && s.gender != Gender.None, "gender after removing null chromosome " + s.gender);
        }

        private static Pawn Generate(XenotypeDef xenotype)
        {
            return PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Colonist, null, forceGenerateNewPawn: true,
                forcedXenotype: xenotype, forceNoGear: true));
        }

        private static ThoughtDef NeedFood => DefDatabase<ThoughtDef>.GetNamed("NeedFood");

        private static void Discard(Pawn pawn)
        {
            try
            {
                if (!pawn.Discarded)
                {
                    Find.WorldPawns.PassToWorld(pawn, PawnDiscardDecideMode.Discard);
                }
            }
            catch (Exception e)
            {
                Log.Warning("[Rimshi] self-test could not discard " + pawn.LabelShort + ": " + e.Message);
            }
        }

        /// <summary>
        /// Skeleton parts (Parts/SkeletonParts.cs): install bills only on skeletons, a KLR arm installs and takes 20% less
        /// damage, and a second AI core repairs a damaged one. Runs on the throwaway skeleton, which is discarded after.
        /// </summary>
        private static void PartsChecks(Pawn s, Pawn human)
        {
            List<RecipeDef> installs = DefDatabase<RecipeDef>.AllDefsListForReading.Where(r => r.Worker is Recipe_Rimshi_InstallSkeletonPart).ToList();
            List<string> wrong = installs.Where(r => !r.Worker.AvailableOnNow(s) || r.Worker.AvailableOnNow(human) || !r.Worker.GetPartsToApplyOn(s, r).Any()).Select(r => r.defName).ToList();
            Check("part bills", installs.Count >= 13 && wrong.Count == 0, installs.Count + " skeleton part installs, skeleton only" + (wrong.Count > 0 ? "; wrong: " + string.Join(", ", wrong) : ""));

            RecipeDef klr = DefDatabase<RecipeDef>.GetNamed("Rimshi_Install_KLRArm");
            BodyPartRecord shoulder = klr.Worker.GetPartsToApplyOn(s, klr).FirstOrDefault();
            if (shoulder == null)
            {
                Check("part install", false, "no shoulder to install a KLR arm on");
                return;
            }
            klr.Worker.ApplyOnPawn(s, shoulder, null, new List<Thing>(), null);
            bool installed = s.health.hediffSet.hediffs.Any(h => h.def == klr.addsHediff && h.Part == shoulder);
            Check("part install", installed, "KLR arm on " + shoulder.Label + ": " + installed);

            // Other mods and stats (IncomingDamageFactor) can scale the hit before it reaches the part, so the check is on the
            // reduction itself: the arm takes 20% off whatever arrives, and the injury is what is left.
            Patch_PartDamageResistance.lastBefore = Patch_PartDamageResistance.lastAfter = -1f;
            var dinfo = new DamageInfo(DamageDefOf.Bullet, 10f, 99f, -1f, null, shoulder);
            s.TakeDamage(dinfo);
            float severity = s.health.hediffSet.hediffs.OfType<Hediff_Injury>().Where(h => h.Part == shoulder).Sum(h => h.Severity);
            float before = Patch_PartDamageResistance.lastBefore;
            float after = Patch_PartDamageResistance.lastAfter;
            Check("part armour", before > 0f && Mathf.Abs(after - before * 0.8f) < 0.05f && Mathf.Abs(severity - after) < 0.05f,
                "10 bullet damage on the KLR arm arrived as " + before.ToString("0.##") + ", reduced to " + after.ToString("0.##")
                + " (expected " + (before * 0.8f).ToString("0.##") + "), injury " + severity.ToString("0.##"));

            RecipeDef core = DefDatabase<RecipeDef>.GetNamed("Rimshi_Install_AICore");
            BodyPartRecord brain = s.health.hediffSet.GetBrain();
            core.Worker.ApplyOnPawn(s, brain, null, new List<Thing>(), null);
            bool hasCore = s.health.hediffSet.hediffs.Any(h => h.def == core.addsHediff && h.Part == brain);
            s.TakeDamage(new DamageInfo(DamageDefOf.Bullet, 1f, 99f, -1f, null, brain));
            bool offered = core.Worker.GetPartsToApplyOn(s, core).Contains(brain);
            core.Worker.ApplyOnPawn(s, brain, null, new List<Thing>(), null);
            int left = s.health.hediffSet.hediffs.Count(h => h.Part == brain && h is Hediff_Injury);
            Check("ai core repair", hasCore && offered && left == 0 && !s.Dead, "core installed " + hasCore + ", repair offered when damaged " + offered + ", injuries left " + left + ", alive " + !s.Dead);
        }

        private static void Check(string name, bool ok, string detail)
        {
            if (ok)
            {
                passed++;
                RimshiLog.Test("selftest", name, detail);
            }
            else
            {
                failed++;
                RimshiLog.Fail("selftest", name, detail);
            }
        }
    }

    /// <summary>
    /// When a save loads: replaces the old mod's factions and converts its skeletons if the mod setting is on. When a game
    /// loads or starts: runs the self-tests once per session with Development mode on. Also keeps the recall horde
    /// schedule: hordes on their way and how many times the recall codes have been broadcast in this game (each horde is
    /// larger).
    /// </summary>
    public class RimshiGameComponent : GameComponent
    {
        public static RimshiGameComponent Instance;

        private List<PendingHorde> pendingHordes = new List<PendingHorde>();
        private int summons;
        private bool foodPoliciesSet;

        public RimshiGameComponent(Game game)
        {
            Instance = this;
        }

        public int Summons => summons;

        public bool FoodPoliciesSet => foodPoliciesSet;

        public bool HordePending => pendingHordes.Count > 0;

        public void ScheduleHorde(Map map, IntVec3 cell, int delayTicks)
        {
            pendingHordes.Add(new PendingHorde { map = map, cell = cell, arriveTick = Find.TickManager.TicksGame + delayTicks });
        }

        public override void GameComponentTick()
        {
            if (pendingHordes.Count == 0 || Find.TickManager.TicksGame % 60 != 0)
            {
                return;
            }
            for (int i = pendingHordes.Count - 1; i >= 0; i--)
            {
                PendingHorde horde = pendingHordes[i];
                if (horde.map == null || !Find.Maps.Contains(horde.map))
                {
                    pendingHordes.RemoveAt(i);
                    RimshiLog.Test("horde", "cancelled", "the map the horde was called to is gone");
                    continue;
                }
                if (Find.TickManager.TicksGame < horde.arriveTick)
                {
                    continue;
                }
                pendingHordes.RemoveAt(i);
                SkeletonHorde.Arrive(horde.map, horde.cell, SkeletonHorde.PointsFor(horde.map, summons));
                summons++;
            }
        }

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref pendingHordes, "pendingHordes", LookMode.Deep);
            Scribe_Values.Look(ref summons, "hordeSummons");
            Scribe_Values.Look(ref foodPoliciesSet, "foodPoliciesSet");
            if (Scribe.mode == LoadSaveMode.PostLoadInit && pendingHordes == null)
            {
                pendingHordes = new List<PendingHorde>();
            }
        }

        // Both run inside the loading long event, off the main thread; textures can only be read on the main thread, so
        // the tests wait until loading is finished.
        public override void LoadedGame()
        {
            if (RimshiMod.Settings.replaceOldFactions)
            {
                SkeletonFactions.ReplaceOldFactions();
                // Pawns leave and re-enter the map while their race changes, which needs the main thread.
                LongEventHandler.ExecuteWhenFinished(OldSkeletonConversion.ConvertAll);
            }
            LongEventHandler.ExecuteWhenFinished(SoftCycle.MigrateSave);
            LongEventHandler.ExecuteWhenFinished(SetFoodPoliciesOnce);
            LongEventHandler.ExecuteWhenFinished(SelfTests.RunIfDevMode);
        }

        public override void StartedNewGame()
        {
            LongEventHandler.ExecuteWhenFinished(SetFoodPoliciesOnce);
            LongEventHandler.ExecuteWhenFinished(SelfTests.RunIfDevMode);
        }

        /// <summary>Components go into the save's food policies the first time the mod sees it; after that they are the player's.</summary>
        private void SetFoodPoliciesOnce()
        {
            if (foodPoliciesSet)
            {
                return;
            }
            SkeletonFoodPolicies.Apply();
            foodPoliciesSet = true;
        }
    }
}
