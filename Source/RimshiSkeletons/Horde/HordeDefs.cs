using System.Linq;
using RimWorld;
using Verse;

namespace RimshiSkeletons
{
    /// <summary>
    /// Startup checks and fallbacks for the recall horde: the array has its own tower graphic at its 3x5 footprint,
    /// where the research sits on its tab (and whether it overlaps anything), and the bladeguards' melee fallback when
    /// Royalty (plasmaswords, monoswords, zeushammers) is not loaded.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class HordeDefs
    {
        public const string AntennaDefName = "BurnoutMechlinkBooster";
        public const string ArrayTexPath = "Rimshi/Buildings/RecallArray";
        public const string RoyaltyMeleeTag = "UltratechMelee";
        public const string FallbackMeleeTag = "MedievalMeleeAdvanced";

        static HordeDefs()
        {
            ThingDef array = RimshiDefOf.Rimshi_RecallArray;
            string arrayPath = array.graphicData?.texPath;
            bool tower = arrayPath == ArrayTexPath && array.size == new IntVec2(3, 5) && !array.rotatable
                && array.graphicData.drawSize == new UnityEngine.Vector2(3f, 5f) && array.passability == Traversability.Impassable;
            string detail = array.defName + " uses " + (arrayPath ?? "no graphic") + ", size " + array.size + ", draw size " + array.graphicData?.drawSize
                + ", rotatable " + array.rotatable + ", " + array.passability + ", research "
                + string.Join(", ", array.researchPrerequisites?.Select(r => r.defName) ?? Enumerable.Empty<string>());
            if (tower)
            {
                RimshiLog.Test("horde", "array graphic", detail);
            }
            else
            {
                RimshiLog.Fail("horde", "array graphic", detail);
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
