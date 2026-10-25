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
        }

        public override string SettingsCategory() => "Rimshi Skeletons";

        public override void DoSettingsWindowContents(Rect inRect)
        {
            var list = new Listing_Standard();
            list.Begin(inRect);
            list.CheckboxLabeled("Convert Rim-Skeletons (Continued) factions and skeletons when a save loads", ref Settings.replaceOldFactions,
                "Needs Rim-Skeletons (Continued) to still be loaded. When a save loads, its skeletal imperium becomes this mod's "
                + "skeletal imperium and its skeleton player faction becomes this mod's, keeping their names, relations, settlements "
                + "and members. Every old skeleton (colonists, prisoners, visitors, world pawns and corpses) becomes a human with the "
                + "skeleton xenotype, keeping its name, skills, traits, relations, gear and injuries. A letter lists who was converted. "
                + "Then save, quit and remove Rim-Skeletons (Continued).");
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

    /// <summary>
    /// Harmony patches go in after defs and DefOfs load, not in the mod constructor. Patching a type runs its static
    /// constructor, and PregnancyUtility's builds a dictionary keyed on PawnRelationDefOf: patched too early, that
    /// constructor throws and PregnancyUtility stays broken for the whole session, for every mod and for loading saves.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class RimshiHarmony
    {
        static RimshiHarmony()
        {
            new Harmony(RimshiMod.HarmonyId).PatchAll();
        }
    }
}
