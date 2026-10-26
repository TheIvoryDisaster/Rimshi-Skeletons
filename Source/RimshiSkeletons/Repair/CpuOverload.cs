using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RimshiSkeletons
{
    /// <summary>
    /// CPU overload (docs/design/cpu-soft-cycle.md): what a skeleton gets when its CPU integrity runs out or a soft cycle is
    /// cancelled, until a soft cycle finishes. Severity counts days overloaded; it does not advance while the skeleton is
    /// soft cycling or suspended in a rib cage. Days 0 to 2 sluggish, 2 to 4 steady, then lucid paranoia: quicker and
    /// sharper, a mood penalty growing a point a day, and only violent mental breaks.
    /// </summary>
    public static class CpuOverload
    {
        public const float SteadyDays = 2f;
        public const float MadnessDays = 4f;
        public const int MaxMadnessPenalty = 30;

        public const int SluggishStage = 0;
        public const int SteadyStage = 1;
        public const int MadnessStage = 2;

        public static Hediff Of(Pawn pawn) => pawn?.health?.hediffSet?.GetFirstHediffOfDef(RimshiDefOf.Rimshi_CpuOverload);

        public static bool Mad(Pawn pawn) => Of(pawn)?.CurStageIndex == MadnessStage;

        /// <summary>Whole days spent in the madness stage (0 on the first day).</summary>
        public static int MadnessDay(Hediff overload) => overload == null ? 0 : Mathf.Max(0, Mathf.FloorToInt(overload.Severity - MadnessDays));

        public static int MadnessPenalty(Hediff overload) => Mathf.Min(MaxMadnessPenalty, MadnessDay(overload) + 1);

        /// <summary>Starts an overload if the skeleton has none (an ongoing one keeps its clock).</summary>
        public static void Begin(Pawn pawn, string reason)
        {
            if (pawn?.health == null || pawn.Dead || Of(pawn) != null || !SkeletonUtility.HasFrame(pawn))
            {
                return;
            }
            pawn.health.AddHediff(RimshiDefOf.Rimshi_CpuOverload);
            RimshiLog.Test("overload", "begin", pawn.LabelShort + ": " + reason);
            if (PawnUtility.ShouldSendNotificationAbout(pawn))
            {
                Messages.Message(pawn.LabelShort + "'s CPU is overloading (" + reason + "). A soft cycle will clear it.", pawn, MessageTypeDefOf.NegativeEvent);
            }
        }

        /// <summary>Clears the overload; true if there was one.</summary>
        public static bool End(Pawn pawn)
        {
            Hediff overload = Of(pawn);
            if (overload == null)
            {
                return false;
            }
            RimshiLog.Test("overload", "end", pawn.LabelShort + " cleared after " + overload.Severity.ToString("0.0") + " days");
            pawn.health.RemoveHediff(overload);
            return true;
        }

        public static bool IsViolent(MentalBreakDef def) => def?.mentalState != null && def.mentalState.IsAggro;
    }

    public class HediffCompProperties_Rimshi_CpuOverload : HediffCompProperties
    {
        public HediffCompProperties_Rimshi_CpuOverload()
        {
            compClass = typeof(HediffComp_Rimshi_CpuOverload);
        }
    }

    /// <summary>Advances the overload a day per day, except while soft cycling or suspended, and announces each new stage.</summary>
    public class HediffComp_Rimshi_CpuOverload : HediffComp
    {
        private int lastStage = -1;

        public bool Holding => Pawn.Deathresting || StandingReset.InProgress(Pawn) || RibCage.Suspended(Pawn);

        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            if (!Holding)
            {
                severityAdjustment += (float)delta / GenDate.TicksPerDay;
            }
            int stage = parent.CurStageIndex;
            if (stage == lastStage)
            {
                return;
            }
            if (lastStage >= 0)
            {
                RimshiLog.Test("overload", "stage", Pawn.LabelShort + " now " + parent.CurStage.label + " after " + parent.Severity.ToString("0.0") + " days");
                if (PawnUtility.ShouldSendNotificationAbout(Pawn))
                {
                    string text = stage == CpuOverload.SteadyStage
                        ? Pawn.LabelShort + "'s overloaded CPU has steadied. It will not last."
                        : Pawn.LabelShort + "'s overloaded CPU has turned lucid and paranoid: quicker and sharper, but growing unhappier every day, and any mental break will be violent.";
                    Messages.Message(text, Pawn, stage == CpuOverload.MadnessStage ? MessageTypeDefOf.NegativeEvent : MessageTypeDefOf.NeutralEvent);
                }
            }
            lastStage = stage;
        }

        public override string CompLabelInBracketsExtra => Holding ? "holding" : null;

        public override string CompTipStringExtra
        {
            get
            {
                string text = "Overloaded for " + parent.Severity.ToString("0.0") + " days.";
                if (Holding)
                {
                    text += " The overload holds while soft cycling or suspended in a rib cage.";
                }
                else if (parent.Severity < CpuOverload.SteadyDays)
                {
                    text += " Steadies in " + (CpuOverload.SteadyDays - parent.Severity).ToString("0.0") + " days.";
                }
                else if (parent.Severity < CpuOverload.MadnessDays)
                {
                    text += " Turns paranoid in " + (CpuOverload.MadnessDays - parent.Severity).ToString("0.0") + " days.";
                }
                else
                {
                    text += " Mood penalty " + (-CpuOverload.MadnessPenalty(parent)) + "; any mental break will be violent.";
                }
                return text + " A finished soft cycle clears it.";
            }
        }

        public override void CompExposeData()
        {
            Scribe_Values.Look(ref lastStage, "lastStage", -1);
        }
    }

    /// <summary>The sluggish first two days: -8 mood.</summary>
    public class ThoughtWorker_Rimshi_CpuSluggish : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn p) => CpuOverload.Of(p)?.CurStageIndex == CpuOverload.SluggishStage ? ThoughtState.ActiveDefault : ThoughtState.Inactive;
    }

    /// <summary>The paranoid stage: active with a penalty that grows each day (Thought_Rimshi_CpuMadness).</summary>
    public class ThoughtWorker_Rimshi_CpuMadness : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn p) => CpuOverload.Mad(p) ? ThoughtState.ActiveDefault : ThoughtState.Inactive;
    }

    /// <summary>
    /// -1 mood on the first paranoid day and one more each day (up to -30). Its label is one of the thought def's stage
    /// labels, a new one each day, cycling through them.
    /// </summary>
    public class Thought_Rimshi_CpuMadness : Thought_Situational
    {
        private int Day => CpuOverload.MadnessDay(CpuOverload.Of(pawn));

        private ThoughtStage Line => def.stages[Day % def.stages.Count];

        protected override float BaseMoodOffset => -CpuOverload.MadnessPenalty(CpuOverload.Of(pawn));

        public override string LabelCap => Line.LabelCap;

        public override string Description => Line.description + "\n\nCPU overload, day " + (Day + 1) + " of paranoia.";
    }

    /// <summary>A paranoid overloaded skeleton only has violent mental breaks; if none fits the break's intensity, any violent one that can happen.</summary>
    [HarmonyPatch(typeof(MentalBreaker), "CurrentPossibleMoodBreaks", MethodType.Getter)]
    public static class Patch_CpuMadnessBreaks
    {
        private static readonly HashSet<int> logged = new HashSet<int>();

        public static bool Prepare() => AccessTools.PropertyGetter(typeof(MentalBreaker), "CurrentPossibleMoodBreaks") != null;

        public static void Postfix(Pawn ___pawn, ref IEnumerable<MentalBreakDef> __result)
        {
            if (!CpuOverload.Mad(___pawn))
            {
                return;
            }
            List<MentalBreakDef> violent = __result.Where(CpuOverload.IsViolent).ToList();
            if (violent.Count == 0)
            {
                violent = DefDatabase<MentalBreakDef>.AllDefsListForReading.Where(d => CpuOverload.IsViolent(d) && !d.anomalousBreak && d.Worker.BreakCanOccur(___pawn)).ToList();
            }
            if (violent.Count == 0)
            {
                return;
            }
            __result = violent;
            if (logged.Add(___pawn.thingIDNumber))
            {
                RimshiLog.Test("overload", "violent breaks", ___pawn.LabelShort + " can only break violently: " + string.Join(", ", violent.Select(d => d.defName)));
            }
        }
    }
}
