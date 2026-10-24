using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace RimshiSkeletons
{
    /// <summary>
    /// Fills in the def links that name defs from DLCs and other mods, once all defs are loaded: thoughts the
    /// mechanical nature gene blocks, the vanilla hunger thought the fuel reactor blocks, and the immunity list. Looking
    /// the names up here means a missing DLC or mod just skips that name (it is listed in the log) instead of throwing
    /// a cross-reference error. Lists are the original's (race cannotReceiveThoughts and MechanicalNature_Hediff).
    /// </summary>
    [StaticConstructorOnStartup]
    public static class SkeletonDefs
    {
        public static readonly string[] BlockedThoughts =
        {
            "Naked", "Pain", "AteWithoutTable", "DeadMansApparel", "HumanLeatherApparelSad", "NeedOutdoors", "SoakingWet",
            "EnvironmentDark", "WrongApparelGender", "ProsthophileNoProsthetic",
            // Royalty
            "AteFoodInappropriateForTitle",
            // Ideology
            "NoRecentHumanMeat_Preferred", "NoRecentHumanMeat_RequiredStrong", "NoRecentHumanMeat_RequiredRavenous",
            "AteNonCannibalFood_Horrible", "AteNonCannibalFood_Know_Horrible", "AteNonCannibalFood_Abhorrent",
            "AteNonCannibalFood_Know_Abhorrent", "NeedNeuralSupercharge", "AgeReversalReceived", "AgeReversalDemanded",
            "BioSculpterDespised", "BiosculpterPleasure", "MasochistWearingCollar", "MasochistWearingBodyStrap",
            "SleptInRoomWithSlave",
            // Biotech
            "NoxiousHaze",
            // ReGrowth: Core
            "RG_Wet", "RG_ExtremelyWet", "RG_HotSpringWet",
        };

        public static readonly string[] Immunities =
        {
            "FoodPoisoning", "ToxicBuildup", "DrugOverdose", "HeartAttack", "Carcinoma", "GutWorms", "MuscleParasites",
            "WoundInfection", "Flu", "Plague", "Malaria", "SleepingSickness", "ChemicalBurn", "Frostbite",
            "ChemicalDamageModerate", "ChemicalDamageSevere", "Cirrhosis", "Scaria", "SandInEyes", "DirtInEyes", "MudInEyes",
            "GravelInEyes", "WaterInEyes",
        };

        static SkeletonDefs()
        {
            int thoughts = AddNullifyingGene(BlockedThoughts, RimshiDefOf.Rimshi_MechanicalNature, out List<string> missingThoughts);
            RimshiLog.Test("defs", "blocked thoughts", thoughts + " of " + BlockedThoughts.Length + " blocked"
                + (missingThoughts.Count > 0 ? "; not loaded: " + string.Join(", ", missingThoughts) : ""));

            int hunger = AddNullifyingGene(new[] { "NeedFood" }, RimshiDefOf.Rimshi_FuelReactor, out _);
            if (hunger == 1)
            {
                RimshiLog.Test("defs", "hunger thought", "NeedFood nullified by Rimshi_FuelReactor");
            }
            else
            {
                RimshiLog.Fail("defs", "hunger thought", "vanilla NeedFood thought not found");
            }

            GeneDef nature = RimshiDefOf.Rimshi_MechanicalNature;
            if (nature.makeImmuneTo == null)
            {
                nature.makeImmuneTo = new List<HediffDef>();
            }
            var missingHediffs = new List<string>();
            foreach (string name in Immunities)
            {
                HediffDef hediff = DefDatabase<HediffDef>.GetNamedSilentFail(name);
                if (hediff == null)
                {
                    missingHediffs.Add(name);
                }
                else if (!nature.makeImmuneTo.Contains(hediff))
                {
                    nature.makeImmuneTo.Add(hediff);
                }
            }
            RimshiLog.Test("defs", "immunities", nature.makeImmuneTo.Count + " of " + Immunities.Length + " immunities"
                + (missingHediffs.Count > 0 ? "; not loaded: " + string.Join(", ", missingHediffs) : ""));

            int fuels = 0;
            foreach (ThingDef def in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                if (def.HasModExtension<FuelExtension>())
                {
                    fuels++;
                    if (def.ingestible == null)
                    {
                        RimshiLog.Fail("defs", "fuel", def.defName + " is tagged as fuel but is not ingestible");
                    }
                    else
                    {
                        RimshiLog.Test("defs", "fuel", def.defName + ": nutrition " + def.GetStatValueAbstract(StatDefOf.Nutrition) + ", food type "
                            + def.ingestible.foodType + ", preferability " + def.ingestible.preferability
                            + (ThingDefOf.Human.race.CanEverEat(def) ? "" : "; another mod restricts it from humans (the diet patch lets skeletons eat it anyway)"));
                    }
                }
            }
            if (fuels == 0)
            {
                RimshiLog.Fail("defs", "fuel", "no ThingDef is tagged as fuel; skeletons cannot eat");
            }

            int backstories = DefDatabase<BackstoryDef>.AllDefsListForReading.Count(b => b.spawnCategories.Contains(SkeletonUtility.BackstoryCategory));
            RimshiLog.Test("defs", "backstories", backstories + " skeleton backstories");

            var spawning = new List<string>();
            foreach (FactionDef faction in DefDatabase<FactionDef>.AllDefsListForReading)
            {
                for (int i = 0; i < (faction.xenotypeSet?.Count ?? 0); i++)
                {
                    if (faction.xenotypeSet[i].xenotype == RimshiDefOf.Rimshi_Skeleton)
                    {
                        spawning.Add(faction.defName + " " + faction.xenotypeSet[i].chance.ToStringPercent("0.#"));
                    }
                }
            }
            RimshiLog.Test("defs", "spawning", spawning.Count > 0 ? string.Join(", ", spawning) : "no faction spawns skeletons");
        }

        private static int AddNullifyingGene(IEnumerable<string> thoughtNames, GeneDef gene, out List<string> missing)
        {
            missing = new List<string>();
            int added = 0;
            foreach (string name in thoughtNames)
            {
                ThoughtDef thought = DefDatabase<ThoughtDef>.GetNamedSilentFail(name);
                if (thought == null)
                {
                    missing.Add(name);
                    continue;
                }
                if (thought.nullifyingGenes == null)
                {
                    thought.nullifyingGenes = new List<GeneDef>();
                }
                if (!thought.nullifyingGenes.Contains(gene))
                {
                    thought.nullifyingGenes.Add(gene);
                }
                added++;
            }
            return added;
        }
    }
}
