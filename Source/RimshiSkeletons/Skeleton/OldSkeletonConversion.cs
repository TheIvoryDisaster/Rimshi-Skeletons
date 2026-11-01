using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using LudeonTK;
using RimWorld;
using Verse;

namespace RimshiSkeletons
{
    /// <summary>
    /// Turns Rim-Skeletons (Continued) skeletons in a loaded save into ours: race Human, the skeleton xenotype, our
    /// backstories, heads and pawn kinds, and the old body's injuries and prosthetics moved onto the matching human
    /// parts. Runs with the "replace" mod setting, after the factions are swapped. It needs the old mod still loaded:
    /// without it RimWorld drops every pawn whose race is missing, and everything that pointed at them becomes null.
    /// A converted pawn no longer matches, so loading the save again converts nothing.
    /// </summary>
    public static class OldSkeletonConversion
    {
        public const string OldRaceName = "Skeleton_";
        public const string OldBodyName = "SkeletonBody";
        public const string OldHediffName = "MechanicalNature_Hediff";
        private const string OldHeadPrefix = "None_";
        private const string NewHeadPrefix = "Rimshi_Head_";

        /// <summary>The old pawn kinds and ours. The tribal, pirate and Empire skeleton kinds were not ported; they get FallbackKind.</summary>
        public static readonly Dictionary<string, string> OldKinds = new Dictionary<string, string>
        {
            { "Skeleton_Villager", "Rimshi_Villager" },
            { "Skeleton_Gunner", "Rimshi_Gunner" },
            { "Skeleton_Sniper", "Rimshi_Sniper" },
            { "Skeleton_Charger", "Rimshi_Duelist" },
            { "Skeleton_Grenadier_Destructive", "Rimshi_Grenadier" },
            { "Skeleton_Heavy", "Rimshi_Heavy" },
            { "SkeletonRaidLeader", "Rimshi_General" },
            { "Skeleton_Elite", "Rimshi_Elite" },
            { "Hydraulic_Knight_Skele", "Rimshi_HydraulicKnight" },
            { "SkeletonEmperor", "Rimshi_Emperor" },
            { "Town_Guard_Skele", "Rimshi_TownGuard" },
            { "Skeleton_Drifter", "Rimshi_Drifter" },
            { "Skeleton_Colonist", "Rimshi_Colonist" },
            { "Tribal_Skeleton", null },
            { "Pirate_Skele", null },
            { "Skele_Spacer_Reaper", null },
        };

        private const string FallbackKind = "Rimshi_Drifter";
        private const string FallbackPlayerKind = "Rimshi_Colonist";

        /// <summary>
        /// The old body's part defs (Bodies_Skele.xml) and the human part each one lands on. Sides and thumbs are matched
        /// by the parts' labels; the old body has one sound analyzer and one fluid reprocessor, which go to the left ear
        /// and the first kidney.
        /// </summary>
        public static readonly Dictionary<string, string> OldParts = new Dictionary<string, string>
        {
            { "MechanicalThorax", "Torso" },
            { "Reactor", "Heart" },
            { "FluidReprocessor", "Kidney" },
            { "MechanicalNeck", "Neck" },
            { "MechanicalHead", "Head" },
            { "MechanicalBrain", "Brain" },
            { "MechEye", "Eye" },
            { "HearingSensor", "Ear" },
            { "MechanicalJaw", "Jaw" },
            { "SmellSensor", "Nose" },
            { "MechShoulder", "Shoulder" },
            { "MechArm", "Arm" },
            { "MechanicalHand", "Hand" },
            { "MechanicalFinger", "Finger" },
            { "MechWaist", "Waist" },
            { "MechLeg", "Leg" },
            { "MechanicalFoot", "Foot" },
        };

        private static readonly FieldInfo CompsField = AccessTools.Field(typeof(ThingWithComps), "comps");
        private static readonly FieldInfo CompsByTypeField = AccessTools.Field(typeof(ThingWithComps), "compsByType");

        /// <summary>The old race, or null when Rim-Skeletons (Continued) is not loaded.</summary>
        public static ThingDef OldRace => DefDatabase<ThingDef>.GetNamedSilentFail(OldRaceName);

        /// <summary>Converts every old skeleton in the game (maps, caravans, world pawns, corpses) and sends a letter. Main thread only.</summary>
        public static void ConvertAll()
        {
            ThingDef oldRace = OldRace;
            if (oldRace == null)
            {
                RimshiLog.Test("migrate", "old mod missing", "Rim-Skeletons (Continued) is not loaded, so its skeletons are already gone from this save");
                return;
            }
            var lost = new List<string>();
            var converted = new List<string>();
            foreach (Pawn pawn in AllPawnsAndCorpsePawns())
            {
                if (!IsOldSkeleton(pawn, oldRace))
                {
                    continue;
                }
                try
                {
                    Convert(pawn, oldRace, lost);
                    converted.Add(pawn.LabelShort);
                }
                catch (Exception e)
                {
                    RimshiLog.Fail("migrate", "pawn", pawn.LabelShort + ": " + e);
                }
            }
            if (converted.Count == 0)
            {
                RimshiLog.Test("migrate", "none", "no Rim-Skeletons (Continued) skeletons in this save");
                return;
            }
            RimshiLog.Test("migrate", "summary", converted.Count + " converted, " + lost.Count + " health conditions could not carry over");
            string text = "Rimshi Skeletons converted " + converted.Count + " Rim-Skeletons (Continued) skeletons into skeleton-xenotype humans: "
                + string.Join(", ", converted) + ".\n\nYou can now save, quit and remove Rim-Skeletons (Continued). Its skeletons no longer need it.";
            if (lost.Count > 0)
            {
                text += "\n\nThese health conditions had no matching human body part and were removed:\n" + string.Join("\n", lost);
            }
            Find.LetterStack.ReceiveLetter("Skeletons converted", text, LetterDefOf.PositiveEvent);
        }

        [DebugAction("Rimshi Skeletons", "Convert old skeletons now", allowedGameStates = AllowedGameStates.Playing)]
        public static void ConvertAllDebug()
        {
            ConvertAll();
        }

        /// <summary>Old race, an old pawn kind, or the old hidden hediff (which also catches old skeletons that already load as humans).</summary>
        public static bool IsOldSkeleton(Pawn pawn, ThingDef oldRace)
        {
            if (pawn == null || pawn.Discarded)
            {
                return false;
            }
            return pawn.def == oldRace
                || (pawn.kindDef != null && OldKinds.ContainsKey(pawn.kindDef.defName))
                || (pawn.health?.hediffSet != null && pawn.health.hediffSet.hediffs.Any(h => h.def.defName == OldHediffName));
        }

        /// <summary>Converts one pawn. Lines it could not move onto the human body are added to lost.</summary>
        public static void Convert(Pawn pawn, ThingDef oldRace, List<string> lost)
        {
            ModContentPack oldMod = oldRace.modContentPack;
            var changes = new List<string>();
            RemoveOldHediffs(pawn, oldMod);
            if (pawn.def == oldRace)
            {
                ChangeRace(pawn, lost, changes);
            }
            if (pawn.genes != null && pawn.genes.Xenotype != RimshiDefOf.Rimshi_Skeleton)
            {
                changes.Add("xenotype " + pawn.genes.XenotypeLabel + " to skeleton");
                pawn.genes.SetXenotype(RimshiDefOf.Rimshi_Skeleton);
            }
            FixStory(pawn, oldMod, changes);
            FixKind(pawn, changes);
            RemoveOldMemories(pawn, oldMod);
            pawn.Notify_DisabledWorkTypesChanged();
            pawn.Drawer?.renderer?.SetAllGraphicsDirty();
            RimshiLog.Test("migrate", "pawn", pawn.LabelShort + ": " + string.Join(", ", changes));
        }

        private static IEnumerable<Pawn> AllPawnsAndCorpsePawns()
        {
            var pawns = new HashSet<Pawn>(PawnsFinder.AllMapsWorldAndTemporary_AliveOrDead);
            var corpses = new List<Corpse>();
            foreach (Map map in Find.Maps)
            {
                corpses.Clear();
                ThingOwnerUtility.GetAllThingsRecursively(map, ThingRequest.ForGroup(ThingRequestGroup.Corpse), corpses, allowUnreal: true, null, alsoGetSpawnedThings: true);
                foreach (Corpse corpse in corpses)
                {
                    if (corpse.InnerPawn != null)
                    {
                        pawns.Add(corpse.InnerPawn);
                    }
                }
            }
            return pawns.ToList();
        }

        private static void ChangeRace(Pawn pawn, List<string> lost, List<string> changes)
        {
            ThingDef oldRace = pawn.def;
            BodyDef oldBody = pawn.RaceProps.body;
            float oldLifeExpectancy = pawn.RaceProps.lifeExpectancy;
            Corpse corpse = pawn.ParentHolder as Corpse;
            Map map = pawn.Spawned ? pawn.Map : null;
            IntVec3 position = pawn.Position;
            Rot4 rotation = pawn.Rotation;
            // Map lists are kept per ThingDef, so a spawned pawn leaves the map while its def changes.
            if (map != null)
            {
                pawn.DeSpawn();
            }
            SwapDef(pawn, ThingDefOf.Human);
            pawn.verbTracker = new VerbTracker(pawn);
            int moved = MoveHediffs(pawn, oldBody, lost);
            changes.Add("race " + oldRace.defName + " to Human, " + moved + " health conditions moved");

            // The old race lived to 2500; keep the same share of a human life so a 1200-year-old skeleton is not 1200 as a human.
            float humanLifeExpectancy = pawn.RaceProps.lifeExpectancy;
            long startBio = pawn.ageTracker.AgeBiologicalTicks;
            if (oldLifeExpectancy > humanLifeExpectancy)
            {
                long before = pawn.ageTracker.AgeBiologicalTicks;
                long after = (long)(before * (double)(humanLifeExpectancy / oldLifeExpectancy));
                long adult = pawn.ageTracker.AdultMinAgeTicks;
                pawn.ageTracker.AgeBiologicalTicks = Math.Max(after, Math.Min(before, adult));
            }
            // Every skeleton is at least 120 biologically (SkeletonAge); the old chronological age is kept.
            SkeletonAge.RaiseBiologicalAge(pawn);
            if (pawn.ageTracker.AgeBiologicalTicks != startBio)
            {
                changes.Add("biological age " + startBio / GenDate.TicksPerYear + " to " + pawn.ageTracker.AgeBiologicalYears);
            }
            if (map != null)
            {
                GenSpawn.Spawn(pawn, position, map, rotation);
            }
            if (corpse != null)
            {
                SwapCorpseDef(corpse);
            }
        }

        private static void SwapCorpseDef(Corpse corpse)
        {
            ThingDef corpseDef = ThingDefOf.Human.race.corpseDef;
            if (corpseDef == null || corpse.def == corpseDef)
            {
                return;
            }
            Map map = corpse.Spawned ? corpse.Map : null;
            IntVec3 position = corpse.Position;
            Rot4 rotation = corpse.Rotation;
            if (map != null)
            {
                corpse.DeSpawn();
            }
            SwapDef(corpse, corpseDef);
            if (map != null)
            {
                GenSpawn.Spawn(corpse, position, map, rotation);
            }
        }

        /// <summary>
        /// Gives a thing a new def and keeps its comps (their state), dropping Humanoid Alien Races' own comps and adding
        /// any the new def has that the thing lacks. The comps are saved inside the thing, so the next load reads them
        /// back under the new def.
        /// </summary>
        private static void SwapDef(ThingWithComps thing, ThingDef newDef)
        {
            thing.def = newDef;
            if (!(CompsField?.GetValue(thing) is List<ThingComp> comps))
            {
                thing.InitializeComps();
                return;
            }
            comps.RemoveAll(c => c.GetType().Namespace?.StartsWith("AlienRace") == true);
            if (newDef.comps != null)
            {
                foreach (CompProperties props in newDef.comps)
                {
                    if (comps.Any(c => c.GetType() == props.compClass))
                    {
                        continue;
                    }
                    var comp = (ThingComp)Activator.CreateInstance(props.compClass);
                    comp.parent = thing;
                    comps.Add(comp);
                    comp.Initialize(props);
                }
            }
            CompsByTypeField?.SetValue(thing, comps.GroupBy(c => c.GetType()).ToDictionary(g => g.Key, g => g.ToArray()));
        }

        /// <summary>
        /// Moves every health condition from the old body onto the human body (the def has already changed). The
        /// conditions are moved as they are, not re-added, so their severity, age and comps stay as they were.
        /// </summary>
        private static int MoveHediffs(Pawn pawn, BodyDef oldBody, List<string> lost)
        {
            HediffSet set = pawn.health.hediffSet;
            List<Hediff> hediffs = set.hediffs.ToList();
            set.hediffs.Clear();
            set.DirtyCache();
            int moved = 0;
            foreach (Hediff hediff in hediffs)
            {
                if (hediff.Part != null)
                {
                    BodyPartRecord part = MapPart(hediff.Part, oldBody, pawn.RaceProps.body);
                    if (part == null || set.PartIsMissing(part))
                    {
                        lost.Add(pawn.LabelShort + ": " + hediff.LabelCap + " (" + hediff.Part.Label + ")");
                        RimshiLog.Test("migrate", "lost", pawn.LabelShort + ": " + hediff.def.defName + " on " + hediff.Part.def.defName);
                        continue;
                    }
                    hediff.Part = part;
                }
                set.hediffs.Add(hediff);
                set.DirtyCache();
                moved++;
            }
            pawn.health.capacities?.Notify_CapacityLevelsDirty();
            pawn.health.summaryHealth?.Notify_HealthChanged();
            return moved;
        }

        /// <summary>The human part an old body part lands on: same mapped def, same side, thumb to thumb, then in order.</summary>
        public static BodyPartRecord MapPart(BodyPartRecord old, BodyDef oldBody, BodyDef newBody)
        {
            string target = OldParts.TryGetValue(old.def.defName, out string mapped) ? mapped : old.def.defName;
            List<BodyPartRecord> candidates = newBody.AllParts.Where(p => p.def.defName == target).ToList();
            if (candidates.Count <= 1)
            {
                return candidates.FirstOrDefault();
            }
            string side = Side(old);
            bool thumb = IsThumb(old);
            List<BodyPartRecord> narrowed = candidates.Where(p => side == null || Side(p) == side).ToList();
            if (narrowed.Count > 0)
            {
                candidates = narrowed;
            }
            narrowed = candidates.Where(p => IsThumb(p) == thumb).ToList();
            if (narrowed.Count > 0)
            {
                candidates = narrowed;
            }
            int index = oldBody.AllParts.Where(p => p.def == old.def && Side(p) == side && IsThumb(p) == thumb).ToList().IndexOf(old);
            return candidates[Math.Min(Math.Max(index, 0), candidates.Count - 1)];
        }

        private static string Side(BodyPartRecord part)
        {
            string label = part.customLabel ?? "";
            return label.Contains("left") ? "left" : label.Contains("right") ? "right" : null;
        }

        private static bool IsThumb(BodyPartRecord part) => part.customLabel != null && part.customLabel.Contains("thumb");

        private static void RemoveOldHediffs(Pawn pawn, ModContentPack oldMod)
        {
            if (pawn.health?.hediffSet == null)
            {
                return;
            }
            foreach (Hediff hediff in pawn.health.hediffSet.hediffs.ToList())
            {
                if (hediff.def.defName == OldHediffName || (oldMod != null && hediff.def.modContentPack == oldMod))
                {
                    pawn.health.RemoveHediff(hediff);
                }
            }
        }

        private static void RemoveOldMemories(Pawn pawn, ModContentPack oldMod)
        {
            MemoryThoughtHandler memories = pawn.needs?.mood?.thoughts?.memories;
            if (memories == null || oldMod == null)
            {
                return;
            }
            foreach (Thought_Memory memory in memories.Memories.ToList())
            {
                if (memory.def.modContentPack == oldMod)
                {
                    memories.RemoveMemory(memory);
                }
            }
        }

        /// <summary>Old backstories to ours with the same title, old heads to ours by name, and the xenotype's hair and beard.</summary>
        private static void FixStory(Pawn pawn, ModContentPack oldMod, List<string> changes)
        {
            Pawn_StoryTracker story = pawn.story;
            if (story == null)
            {
                return;
            }
            BackstoryDef childhood = OurBackstory(story.Childhood, BackstorySlot.Childhood, oldMod);
            if (childhood != story.Childhood)
            {
                changes.Add("childhood " + story.Childhood?.defName + " to " + childhood?.defName);
                story.Childhood = childhood;
            }
            BackstoryDef adulthood = OurBackstory(story.Adulthood, BackstorySlot.Adulthood, oldMod);
            if (adulthood != story.Adulthood)
            {
                changes.Add("adulthood " + story.Adulthood?.defName + " to " + adulthood?.defName);
                story.Adulthood = adulthood;
            }

            List<HeadTypeDef> heads = RimshiDefOf.Rimshi_SkeletonFrame.forcedHeadTypes;
            if (heads != null && heads.Count > 0 && !heads.Contains(story.headType))
            {
                HeadTypeDef head = story.headType != null && story.headType.defName.StartsWith(OldHeadPrefix)
                    ? DefDatabase<HeadTypeDef>.GetNamedSilentFail(NewHeadPrefix + story.headType.defName.Substring(OldHeadPrefix.Length))
                    : null;
                head = head ?? heads.RandomElement();
                changes.Add("head " + story.headType?.defName + " to " + head.defName);
                story.headType = head;
            }
            story.hairDef = HairDefOf.Bald;
            if (pawn.style != null)
            {
                pawn.style.beardDef = BeardDefOf.NoBeard;
            }
        }

        private static BackstoryDef OurBackstory(BackstoryDef old, BackstorySlot slot, ModContentPack oldMod)
        {
            if (old == null || oldMod == null || old.modContentPack != oldMod)
            {
                return old;
            }
            List<BackstoryDef> ours = DefDatabase<BackstoryDef>.AllDefsListForReading
                .Where(b => b.slot == slot && b.spawnCategories != null && b.spawnCategories.Contains(SkeletonUtility.BackstoryCategory)).ToList();
            return ours.FirstOrDefault(b => b.title == old.title) ?? ours.RandomElementWithFallback(old);
        }

        private static void FixKind(Pawn pawn, List<string> changes)
        {
            if (pawn.kindDef == null || !OldKinds.TryGetValue(pawn.kindDef.defName, out string newName))
            {
                return;
            }
            newName = newName ?? (pawn.Faction != null && pawn.Faction.IsPlayer ? FallbackPlayerKind : FallbackKind);
            PawnKindDef kind = DefDatabase<PawnKindDef>.GetNamedSilentFail(newName);
            if (kind == null)
            {
                RimshiLog.Fail("migrate", "pawn", pawn.LabelShort + ": pawn kind " + newName + " not found");
                return;
            }
            changes.Add("kind " + pawn.kindDef.defName + " to " + kind.defName);
            pawn.kindDef = kind;
        }
    }
}
