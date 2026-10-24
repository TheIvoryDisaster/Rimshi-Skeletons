using System.Collections.Generic;
using LudeonTK;
using RimWorld;
using Verse;

namespace RimshiSkeletons
{
    /// <summary>
    /// Faction upkeep: replacing the old mod's factions in a loaded save (mod setting, off by default), and a debug action
    /// that adds the skeletal imperium to a world generated before this mod was added.
    /// </summary>
    public static class SkeletonFactions
    {
        /// <summary>Rim-Skeletons (Continued) FactionDef names and the FactionDef of ours that replaces each.</summary>
        public static readonly Dictionary<string, string> OldToNew = new Dictionary<string, string>
        {
            { "Archaic_SkeletonEmpire", "Rimshi_SkeletalImperium" },
            { "PlayerColonySkeleton", "Rimshi_PlayerSkeletons" },
        };

        /// <summary>
        /// Swaps the def of every faction that uses one of the old FactionDefs. The faction object stays, so its name,
        /// goodwill, settlements, quests, leader and members carry over; only what the def decides (pawn groups, traders,
        /// icon, which xenotypes it spawns) becomes ours. Saved under our def, so the save no longer needs the old mod for it.
        /// </summary>
        public static void ReplaceOldFactions()
        {
            int replaced = 0;
            foreach (Faction faction in Find.FactionManager.AllFactionsListForReading)
            {
                if (faction.def == null || !OldToNew.TryGetValue(faction.def.defName, out string newName))
                {
                    continue;
                }
                FactionDef newDef = DefDatabase<FactionDef>.GetNamedSilentFail(newName);
                if (newDef == null)
                {
                    RimshiLog.Fail("faction", "replace", faction.Name + ": " + newName + " not found");
                    continue;
                }
                string oldName = faction.def.defName;
                faction.def = newDef;
                replaced++;
                RimshiLog.Test("faction", "replace", faction.Name + ": " + oldName + " to " + newName);
            }
            if (replaced == 0)
            {
                RimshiLog.Test("faction", "replace none", "no Rim-Skeletons (Continued) factions in this save");
            }
        }

        [DebugAction("Rimshi Skeletons", "Add the skeletal imperium", allowedGameStates = AllowedGameStates.Playing)]
        public static void AddImperium()
        {
            Faction faction = FactionGenerator.NewGeneratedFaction(new FactionGeneratorParms(RimshiDefOf.Rimshi_SkeletalImperium));
            Find.FactionManager.Add(faction);
            RimshiLog.Test("faction", "added", faction.Name + " (" + faction.def.defName + ")");
            Messages.Message("Added " + faction.Name + " to the world.", MessageTypeDefOf.NeutralEvent, false);
        }
    }
}
