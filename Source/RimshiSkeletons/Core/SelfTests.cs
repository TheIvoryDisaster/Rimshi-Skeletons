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
                GeneRemovalCheck(skeleton);
                FactionChecks(pawns);
                ReproductionChecks(skeleton, human);
                HordeChecks(pawns, human);
                ConversionChecks(pawns);
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

            Check("eats fuel", s.FoodIsSuitable(ThingDefOf.Chemfuel) && s.FoodIsSuitable(ThingDefOf.Uranium), "chemfuel and uranium suitable");
            Check("refuses meals", !s.FoodIsSuitable(ThingDefOf.MealSimple), "simple meal suitable " + s.FoodIsSuitable(ThingDefOf.MealSimple));

            float skeletonRate = s.needs.food.FoodFallPerTickAssumingCategory(HungerCategory.Fed);
            float humanRate = human.needs.food.FoodFallPerTickAssumingCategory(HungerCategory.Fed);
            float ratio = humanRate > 0f ? skeletonRate / humanRate : -1f;
            Check("hunger rate", ratio > 0f && ratio < 0.25f, "skeleton/human hunger " + ratio.ToString("0.000"));

            Check("hunger thought", ThoughtUtility.ThoughtNullified(s, NeedFood), "NeedFood nullified");
            ThoughtDef fuelNeed = DefDatabase<ThoughtDef>.GetNamedSilentFail("Rimshi_FuelNeed");
            Check("fuel thought", fuelNeed != null && ThoughtUtility.CanGetThought(s, fuelNeed), "Rimshi_FuelNeed can apply");
            Check("blocked thought", ThoughtUtility.ThoughtNullified(s, ThoughtDefOf.Naked), "Naked nullified");

            Check("backstory", s.story.Adulthood != null && s.story.Adulthood.spawnCategories.Contains(SkeletonUtility.BackstoryCategory),
                "backstories " + s.story.Childhood?.defName + ", " + s.story.Adulthood?.defName);
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
            Check("human refuses fuel", !h.FoodIsSuitable(ThingDefOf.Chemfuel), "chemfuel suitable " + h.FoodIsSuitable(ThingDefOf.Chemfuel));
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
            Check("kit gated", kitRecipe != null && kitRecipe.researchPrerequisite == kitResearch,
                "recipe " + kitRecipe?.defName + " needs " + kitRecipe?.researchPrerequisite?.defName);
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
            AccessTools.Method(typeof(CompAbilityEffect_ReimplantXenogerm), nameof(CompAbilityEffect_ReimplantXenogerm.Valid)),
            AccessTools.Method(typeof(JobDriver_AbsorbXenogerm), nameof(JobDriver_AbsorbXenogerm.TryMakePreToilReservations)),
            AccessTools.Method(typeof(GeneUtility), nameof(GeneUtility.ImplantXenogermItem)),
            AccessTools.Method(typeof(GeneUtility), nameof(GeneUtility.ReimplantXenogerm)),
            AccessTools.Method(typeof(Genepack), nameof(Genepack.Initialize)),
        };

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

        public RimshiGameComponent(Game game)
        {
            Instance = this;
        }

        public int Summons => summons;

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
            LongEventHandler.ExecuteWhenFinished(SelfTests.RunIfDevMode);
        }

        public override void StartedNewGame()
        {
            LongEventHandler.ExecuteWhenFinished(SelfTests.RunIfDevMode);
        }
    }
}
