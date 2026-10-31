using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.Grammar;

namespace RimshiSkeletons
{
    /// <summary>One organ of the human body and the machine part a skeleton has in its place.</summary>
    public class SkeletonOrgan
    {
        public BodyPartDef part;
        public HediffDef hediff;
    }

    /// <summary>On Rimshi_MechanicalNature: the organs every skeleton has as machine parts (docs/design/skeleton-organs.md).</summary>
    public class SkeletonAnatomyExtension : DefModExtension
    {
        public List<SkeletonOrgan> organs = new List<SkeletonOrgan>();
    }

    /// <summary>
    /// A skeleton's own machine organ: an added part like a bionic, so it never bleeds, and removing it gives back the
    /// item. Hidden from the health tab like a human's natural organs; its part shows under the machine name instead.
    /// </summary>
    public class Hediff_Rimshi_SkeletonOrgan : Hediff_AddedPart
    {
        public override bool Visible => false;
    }

    /// <summary>Gives every skeleton its machine organs when the gene is added (generation, xenogerms, dev mode).</summary>
    public class Gene_MechanicalNature : Gene
    {
        public override void PostAdd()
        {
            base.PostAdd();
            SkeletonAnatomy.FitOrgans(pawn, "gene added");
        }
    }

    /// <summary>
    /// Skeleton anatomy (docs/design/skeleton-organs.md). Skeletons keep the human body (a xenotype cannot change it),
    /// but each organ listed in SkeletonAnatomyExtension carries a machine part hediff, and the part itself is shown
    /// under the machine name (left lung becomes left cooling fan) wherever a skeleton's body parts are named: the health
    /// tab, surgery menus and bills, and the combat log.
    /// </summary>
    public static class SkeletonAnatomy
    {
        private static Dictionary<BodyPartDef, HediffDef> organs;
        private static readonly Dictionary<BodyPartRecord, string[]> labels = new Dictionary<BodyPartRecord, string[]>();

        /// <summary>The pawn whose body parts are being named right now (set around each patched call site), or null.</summary>
        [System.ThreadStatic]
        public static Pawn context;

        public static Dictionary<BodyPartDef, HediffDef> Organs
        {
            get
            {
                if (organs == null)
                {
                    organs = new Dictionary<BodyPartDef, HediffDef>();
                    SkeletonAnatomyExtension ext = RimshiDefOf.Rimshi_MechanicalNature.GetModExtension<SkeletonAnatomyExtension>();
                    foreach (SkeletonOrgan organ in ext?.organs ?? Enumerable.Empty<SkeletonOrgan>())
                    {
                        if (organ.part != null && organ.hediff != null)
                        {
                            organs[organ.part] = organ.hediff;
                        }
                    }
                }
                return organs;
            }
        }

        /// <summary>
        /// Fits the machine part on every listed organ that is still there and has no added part of its own. Fitting a
        /// part clears what was on that organ, as installing any bionic does. Safe to run again: fitted organs are skipped.
        /// </summary>
        public static int FitOrgans(Pawn pawn, string why)
        {
            if (pawn?.health?.hediffSet == null || pawn.Dead)
            {
                return 0;
            }
            var fitted = new List<string>();
            foreach (BodyPartRecord part in pawn.health.hediffSet.GetNotMissingParts().ToList())
            {
                if (!Organs.TryGetValue(part.def, out HediffDef hediff) || pawn.health.hediffSet.PartOrAnyAncestorHasDirectlyAddedParts(part))
                {
                    continue;
                }
                pawn.health.AddHediff(HediffMaker.MakeHediff(hediff, pawn, part), part);
                fitted.Add(part.def.defName);
            }
            if (fitted.Count > 0)
            {
                RimshiLog.Test("anatomy", "organs fitted", pawn.LabelShort + " (" + why + "): " + string.Join(", ", fitted));
            }
            return fitted.Count;
        }

        /// <summary>Older saves: skeletons made before machine organs existed get them on load.</summary>
        public static void MigrateSave()
        {
            int pawns = 0;
            foreach (Pawn pawn in PawnsFinder.AllMapsWorldAndTemporary_Alive)
            {
                if (SkeletonUtility.HasMechanicalNature(pawn) && FitOrgans(pawn, "older save") > 0)
                {
                    pawns++;
                }
            }
            if (pawns > 0)
            {
                RimshiLog.Test("anatomy", "save migrated", pawns + " skeletons given machine organs");
            }
        }

        private static string[] LabelsFor(BodyPartRecord part)
        {
            if (!labels.TryGetValue(part, out string[] set))
            {
                // label, labelCap, labelShort, labelShortCap; the side ("left") is kept from the human label.
                HediffDef organ = Organs[part.def];
                string label = part.Label.Contains(part.def.label) ? part.Label.Replace(part.def.label, organ.label) : organ.label;
                set = new[] { label, label.CapitalizeFirst(), organ.label, organ.label.CapitalizeFirst() };
                labels[part] = set;
            }
            return set;
        }

        private static bool Relabels(BodyPartRecord part) =>
            part != null && context != null && Organs.ContainsKey(part.def) && SkeletonUtility.HasMechanicalNature(context);

        public static string Label(BodyPartRecord part) => Relabels(part) ? LabelsFor(part)[0] : part.Label;

        public static string LabelCap(BodyPartRecord part) => Relabels(part) ? LabelsFor(part)[1] : part.LabelCap;

        public static string LabelShort(BodyPartRecord part) => Relabels(part) ? LabelsFor(part)[2] : part.LabelShort;

        public static string LabelShortCap(BodyPartRecord part) => Relabels(part) ? LabelsFor(part)[3] : part.LabelShortCap;

        /// <summary>A part's label as it reads for a pawn, for the self-tests.</summary>
        public static string LabelFor(Pawn pawn, BodyPartRecord part)
        {
            Pawn was = context;
            context = pawn;
            try
            {
                return Label(part);
            }
            finally
            {
                context = was;
            }
        }
    }

    /// <summary>New pawns: a backstop in case anything in generation clears hediffs after the gene was added.</summary>
    [HarmonyPatch(typeof(PawnGenerator), nameof(PawnGenerator.GeneratePawn), new[] { typeof(PawnGenerationRequest) })]
    public static class Patch_GenerateOrgans
    {
        public static void Postfix(Pawn __result)
        {
            if (SkeletonUtility.HasMechanicalNature(__result))
            {
                SkeletonAnatomy.FitOrgans(__result, "generated");
            }
        }
    }

    /// <summary>
    /// Swaps every BodyPartRecord label call in the call sites below for SkeletonAnatomy's, which name a skeleton's organs
    /// by their machine part. A transpiler, because getters this small can be inlined past a patch. The sites count their
    /// swaps for the self-test.
    /// </summary>
    [HarmonyPatch]
    public static class Patch_OrganLabels
    {
        public static readonly Dictionary<string, int> swaps = new Dictionary<string, int>();

        private static readonly Dictionary<MethodInfo, MethodInfo> getters = new Dictionary<MethodInfo, MethodInfo>
        {
            { AccessTools.PropertyGetter(typeof(BodyPartRecord), nameof(BodyPartRecord.Label)), AccessTools.Method(typeof(SkeletonAnatomy), nameof(SkeletonAnatomy.Label)) },
            { AccessTools.PropertyGetter(typeof(BodyPartRecord), nameof(BodyPartRecord.LabelCap)), AccessTools.Method(typeof(SkeletonAnatomy), nameof(SkeletonAnatomy.LabelCap)) },
            { AccessTools.PropertyGetter(typeof(BodyPartRecord), nameof(BodyPartRecord.LabelShort)), AccessTools.Method(typeof(SkeletonAnatomy), nameof(SkeletonAnatomy.LabelShort)) },
            { AccessTools.PropertyGetter(typeof(BodyPartRecord), nameof(BodyPartRecord.LabelShortCap)), AccessTools.Method(typeof(SkeletonAnatomy), nameof(SkeletonAnatomy.LabelShortCap)) },
        };

        /// <summary>Site names, as the self-test and swaps use them.</summary>
        public static string SiteName(MethodBase method) => method.DeclaringType?.Name + "." + method.Name;

        public static IEnumerable<MethodBase> TargetMethods()
        {
            foreach (MethodBase method in Patch_OrganLabelPawn.TargetMethods())
            {
                yield return method;
            }
            // Combat log: part rules are built while a battle log entry makes its grammar request (Patch_OrganLabelLog).
            yield return AccessTools.EnumeratorMoveNext(AccessTools.Method(typeof(GrammarUtility), nameof(GrammarUtility.RulesForBodyPartRecord)));
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original)
        {
            int swapped = 0;
            foreach (CodeInstruction code in instructions)
            {
                if ((code.opcode == System.Reflection.Emit.OpCodes.Call || code.opcode == System.Reflection.Emit.OpCodes.Callvirt)
                    && code.operand is MethodInfo called && getters.TryGetValue(called, out MethodInfo ours))
                {
                    code.opcode = System.Reflection.Emit.OpCodes.Call;
                    code.operand = ours;
                    swapped++;
                }
                yield return code;
            }
            swaps[SiteName(original)] = swapped;
        }
    }

    /// <summary>Sets the pawn whose parts are named for the health tab and surgery call sites, from their Pawn argument or bill.</summary>
    [HarmonyPatch]
    public static class Patch_OrganLabelPawn
    {
        public static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(HealthCardUtility), "DrawHediffRow");
            yield return AccessTools.Method(typeof(HealthCardUtility), "GetTooltip", new[] { typeof(Pawn), typeof(BodyPartRecord) });
            yield return AccessTools.Method(typeof(HealthCardUtility), "GenerateSurgeryOption");
            yield return AccessTools.PropertyGetter(typeof(Bill_Medical), nameof(Bill_Medical.Label));
        }

        public static void Prefix(object __instance, object[] __args, out Pawn __state)
        {
            __state = SkeletonAnatomy.context;
            Pawn pawn = (__instance as Bill_Medical)?.GiverPawn;
            for (int i = 0; pawn == null && i < __args.Length; i++)
            {
                pawn = __args[i] as Pawn;
            }
            SkeletonAnatomy.context = pawn;
        }

        public static void Finalizer(Pawn __state)
        {
            SkeletonAnatomy.context = __state;
        }
    }

    /// <summary>Sets the damaged pawn while a battle log entry builds its text, so its part rules use the machine names.</summary>
    [HarmonyPatch]
    public static class Patch_OrganLabelLog
    {
        private static readonly Dictionary<System.Type, FieldInfo> recipients = new Dictionary<System.Type, FieldInfo>();

        public static IEnumerable<MethodBase> TargetMethods()
        {
            foreach (System.Type type in new[] { typeof(BattleLogEntry_DamageTaken), typeof(BattleLogEntry_MeleeCombat), typeof(BattleLogEntry_RangedImpact), typeof(BattleLogEntry_ExplosionImpact) })
            {
                recipients[type] = AccessTools.Field(type, "recipientPawn");
                yield return AccessTools.Method(type, "GenerateGrammarRequest");
            }
        }

        public static void Prefix(LogEntry __instance, out Pawn __state)
        {
            __state = SkeletonAnatomy.context;
            SkeletonAnatomy.context = recipients.TryGetValue(__instance.GetType(), out FieldInfo field) ? field?.GetValue(__instance) as Pawn : null;
        }

        public static void Finalizer(Pawn __state)
        {
            SkeletonAnatomy.context = __state;
        }
    }
}
