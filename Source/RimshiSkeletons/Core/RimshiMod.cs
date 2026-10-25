using HarmonyLib;
using UnityEngine;
using Verse;

namespace RimshiSkeletons
{
    public class RimshiMod : Mod
    {
        public static RimshiMod Instance;
        public static RimshiSettings Settings;

        public const string HarmonyId = "Matthew.RimshiSkeletons";

        public RimshiMod(ModContentPack content) : base(content)
        {
            Instance = this;
            Settings = GetSettings<RimshiSettings>();
            new Harmony(HarmonyId).PatchAll();
        }

        public override string SettingsCategory() => "Rimshi Skeletons";

        public override void DoSettingsWindowContents(Rect inRect)
        {
            var list = new Listing_Standard();
            list.Begin(inRect);
            list.CheckboxLabeled("Replace Rim-Skeletons (Continued) factions when a save loads", ref Settings.replaceOldFactions,
                "Needs Rim-Skeletons (Continued) to still be loaded. When a save loads, its skeletal imperium becomes this mod's "
                + "skeletal imperium and its skeleton player faction becomes this mod's, keeping their names, relations, settlements "
                + "and members. Converting the old skeleton pawns themselves comes in a later update.");
            list.Gap();
            list.Label("Development mode: Debug actions > Rimshi Skeletons has \"Add the skeletal imperium\" for a world made before this mod.");
            list.End();
        }
    }

    public class RimshiSettings : ModSettings
    {
        public bool replaceOldFactions;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref replaceOldFactions, "replaceOldFactions", false);
        }
    }
}
