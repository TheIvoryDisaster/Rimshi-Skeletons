using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace RimshiSkeletons
{
    /// <summary>
    /// A skeleton's genetics screen calls its genes cores (subsets of the Kenshi AI core) and its xenogenes chips. While
    /// GeneUIUtility.DrawGenesInfo or the View genes window draws for a skeleton, the active language's entry for each
    /// vanilla key in Swaps is pointed at our Rimshi_ entry (Languages/English/Keyed/Rimshi_Cores.xml), and put back when
    /// the screen is done. Swapping the data instead of patching Translator matters on 1.7: its JIT inlines the tiny
    /// Translator.TryTranslate into Translate, so a TryTranslate prefix never ran there. Gene tooltips are built while
    /// the screen draws, so they are covered too. A language without our key keeps the vanilla text. The character
    /// card's "Click to view genes" tooltip is built later, outside the screen, so it is swapped by its pawn argument
    /// instead (Patch_ViewGenesDesc).
    /// </summary>
    public static class CoreTerms
    {
        public const string ViewGenesDescKey = "ViewGenesDesc";
        public const string ViewCoresDescKey = "Rimshi_ViewCoresDesc";

        public static readonly Dictionary<string, string> Swaps = new Dictionary<string, string>
        {
            { "Endogenes", "Rimshi_Cores" },
            { "EndogenesDesc", "Rimshi_CoresDesc" },
            { "Xenogenes", "Rimshi_Chips" },
            { "XenogenesDesc", "Rimshi_ChipsDesc" },
            { "NoXenogermImplanted", "Rimshi_NoChips" },
            { "OverriddenByGene", "Rimshi_OverriddenByCore" },
            { "OverriddenByIdenticalGene", "Rimshi_OverriddenByIdenticalCore" },
            { "ViewGenes", "Rimshi_ViewCores" },
        };

        private static int depth;
        private static bool logged;
        private static LoadedLanguage swappedLanguage;
        private static readonly Dictionary<string, LoadedLanguage.KeyedReplacement> originals = new Dictionary<string, LoadedLanguage.KeyedReplacement>();

        public static bool Active => depth > 0;

        public static bool AppliesTo(Thing target) => target is Pawn pawn && SkeletonUtility.CarriesSkeletonGenes(pawn);

        /// <summary>Starts core wording if the target is a skeleton; returns whether it did, for End. Self-tests pass log false.</summary>
        public static bool Begin(Thing target, bool log = true)
        {
            if (!AppliesTo(target))
            {
                return false;
            }
            if (depth++ == 0)
            {
                SwapIn();
            }
            if (log && !logged)
            {
                logged = true;
                RimshiLog.Test("cores", "screen", target.LabelShort + "'s genetics screen reads " + "Endogenes".Translate() + " / " + "Xenogenes".Translate());
            }
            return true;
        }

        public static void End(bool began)
        {
            if (began && depth > 0 && --depth == 0)
            {
                SwapOut();
            }
        }

        /// <summary>Points each vanilla key at our entry in the active language, remembering what it held.</summary>
        private static void SwapIn()
        {
            LoadedLanguage lang = LanguageDatabase.activeLanguage;
            if (lang == null)
            {
                return;
            }
            lang.HaveTextForKey(ViewGenesDescKey); // loads the language data if it is not loaded yet
            swappedLanguage = lang;
            originals.Clear();
            Dictionary<string, LoadedLanguage.KeyedReplacement> keyed = lang.keyedReplacements;
            foreach (KeyValuePair<string, string> swap in Swaps)
            {
                if (keyed.TryGetValue(swap.Value, out LoadedLanguage.KeyedReplacement ours) && !ours.isPlaceholder)
                {
                    keyed.TryGetValue(swap.Key, out LoadedLanguage.KeyedReplacement vanilla);
                    originals[swap.Key] = vanilla;
                    keyed[swap.Key] = ours;
                }
            }
        }

        private static void SwapOut()
        {
            if (swappedLanguage != null)
            {
                Dictionary<string, LoadedLanguage.KeyedReplacement> keyed = swappedLanguage.keyedReplacements;
                foreach (KeyValuePair<string, LoadedLanguage.KeyedReplacement> original in originals)
                {
                    if (original.Value != null)
                    {
                        keyed[original.Key] = original.Value;
                    }
                    else
                    {
                        keyed.Remove(original.Key);
                    }
                }
            }
            originals.Clear();
            swappedLanguage = null;
        }
    }

    /// <summary>The Genes tab and the View genes window both draw through here.</summary>
    [HarmonyPatch(typeof(GeneUIUtility), nameof(GeneUIUtility.DrawGenesInfo))]
    public static class Patch_CoreTermsGenesInfo
    {
        public static void Prefix(Thing target, out bool __state) => __state = CoreTerms.Begin(target);

        public static void Finalizer(bool __state) => CoreTerms.End(__state);
    }

    /// <summary>The View genes window title is drawn before DrawGenesInfo.</summary>
    [HarmonyPatch(typeof(Dialog_ViewGenes), nameof(Dialog_ViewGenes.DoWindowContents))]
    public static class Patch_CoreTermsViewGenes
    {
        public static void Prefix(Pawn ___target, out bool __state) => __state = CoreTerms.Begin(___target);

        public static void Finalizer(bool __state) => CoreTerms.End(__state);
    }

    /// <summary>Character card xenotype tooltip: "Click to view {PAWN}'s genes" reads cores for a skeleton.</summary>
    [HarmonyPatch(typeof(TranslatorFormattedStringExtensions), nameof(TranslatorFormattedStringExtensions.Translate),
        new[] { typeof(string), typeof(NamedArgument) })]
    public static class Patch_ViewGenesDesc
    {
        public static void Prefix(ref string key, NamedArgument arg1)
        {
            if (key == CoreTerms.ViewGenesDescKey && CoreTerms.AppliesTo(arg1.arg as Pawn) && CoreTerms.ViewCoresDescKey.CanTranslate())
            {
                key = CoreTerms.ViewCoresDescKey;
            }
        }
    }
}
