using System.Linq;
using RimWorld;
using Verse;

namespace RimshiSkeletons
{
    /// <summary>
    /// Startup checks and fallbacks for the recall horde: the array really did inherit the mechband antenna's graphic,
    /// where the research sits on its tab (and whether it overlaps anything), and the bladeguards' melee fallback when
    /// Royalty (plasmaswords, monoswords, zeushammers) is not loaded.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class HordeDefs
    {
        public const string AntennaDefName = "BurnoutMechlinkBooster";
        public const string RoyaltyMeleeTag = "UltratechMelee";
        public const string FallbackMeleeTag = "MedievalMeleeAdvanced";

        static HordeDefs()
        {
            ThingDef antenna = DefDatabase<ThingDef>.GetNamedSilentFail(AntennaDefName);
            ThingDef array = RimshiDefOf.Rimshi_RecallArray;
            string arrayPath = array.graphicData?.texPath;
            if (antenna != null && arrayPath != null && arrayPath == antenna.graphicData?.texPath)
            {
                RimshiLog.Test("horde", "array graphic", array.defName + " uses " + arrayPath + ", size " + array.size + ", research " + string.Join(", ", array.researchPrerequisites?.Select(r => r.defName) ?? Enumerable.Empty<string>()));
            }
            else
            {
                RimshiLog.Fail("horde", "array graphic", array.defName + " graphic " + (arrayPath ?? "none") + ", antenna " + (antenna == null ? "not found" : antenna.graphicData?.texPath));
            }

            ResearchProjectDef research = RimshiDefOf.Rimshi_SkeletonRecall;
            ResearchProjectDef basic = research.prerequisites?.FirstOrDefault();
            var overlaps = DefDatabase<ResearchProjectDef>.AllDefsListForReading
                .Where(r => r != research && r.tab == research.tab && System.Math.Abs(r.ResearchViewX - research.ResearchViewX) < 0.5f && System.Math.Abs(r.ResearchViewY - research.ResearchViewY) < 0.5f)
                .Select(r => r.defName).ToList();
            string where = research.defName + " at (" + research.ResearchViewX + ", " + research.ResearchViewY + ") on " + research.tab?.defName + ", cost " + research.baseCost
                + ", needs " + (basic?.defName ?? "nothing") + (basic != null ? " at (" + basic.ResearchViewX + ", " + basic.ResearchViewY + ") on " + basic.tab?.defName : "");
            if (overlaps.Count == 0)
            {
                RimshiLog.Test("horde", "research", where);
            }
            else
            {
                RimshiLog.Fail("horde", "research", where + "; overlaps " + string.Join(", ", overlaps));
            }

            PawnKindDef bladeguard = DefDatabase<PawnKindDef>.GetNamed("Rimshi_HordeBladeguard");
            bool royalMelee = DefDatabase<ThingDef>.AllDefsListForReading.Any(d => d.weaponTags != null && d.weaponTags.Contains(RoyaltyMeleeTag));
            if (!royalMelee && !bladeguard.weaponTags.Contains(FallbackMeleeTag))
            {
                bladeguard.weaponTags.Add(FallbackMeleeTag);
            }
            RimshiLog.Test("horde", "melee", "bladeguard weapon tags " + string.Join(", ", bladeguard.weaponTags) + (royalMelee ? "" : " (no " + RoyaltyMeleeTag + " weapons loaded)"));
        }
    }
}
