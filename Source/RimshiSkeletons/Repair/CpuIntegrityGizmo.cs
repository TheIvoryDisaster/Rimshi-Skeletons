using System.Runtime.CompilerServices;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimshiSkeletons
{
    /// <summary>
    /// The skeleton's version of vanilla's deathrest gizmo beside the draft button: "CPU integrity" over the bar (with
    /// SoftCycle.TipString as its tooltip), and a line on the CPU's current state where vanilla counts deathrest buildings.
    /// Swapped in by Patch_SoftCycleGizmos. Text follows the no-spoiler rule: state only, no forecasts.
    /// </summary>
    public class GeneGizmo_Rimshi_CpuIntegrity : GeneGizmo_DeathrestCapacity
    {
        private static readonly ConditionalWeakTable<Gene_Deathrest, GeneGizmo_Rimshi_CpuIntegrity> cache = new ConditionalWeakTable<Gene_Deathrest, GeneGizmo_Rimshi_CpuIntegrity>();

        public GeneGizmo_Rimshi_CpuIntegrity(Gene_Deathrest gene) : base(gene)
        {
        }

        public static GeneGizmo_Rimshi_CpuIntegrity For(Gene_Deathrest gene) => cache.GetValue(gene, g => new GeneGizmo_Rimshi_CpuIntegrity(g));

        /// <summary>A word or two for the gizmo's bottom line.</summary>
        public static string StateLabel(Pawn pawn)
        {
            if (pawn.Deathresting)
            {
                return "Soft cycling";
            }
            if (StandingReset.InProgress(pawn))
            {
                return "Soft resetting";
            }
            if (RibCage.Suspended(pawn))
            {
                return "Suspended";
            }
            if (CpuOverload.Of(pawn) != null)
            {
                return "Overloaded";
            }
            return "Running";
        }

        public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
        {
            Rect rect = new Rect(topLeft.x, topLeft.y, GetWidth(maxWidth), 75f);
            Rect inner = rect.ContractedBy(6f);
            float row = inner.height / 3f;
            Widgets.DrawWindowBackground(rect);
            GUI.BeginGroup(inner);
            Widgets.Label(new Rect(0f, 0f, inner.width, row), "CPU integrity");
            Need_Deathrest need = gene.DeathrestNeed;
            if (need != null)
            {
                // Our own tooltip rather than the bar's, so no game version can slip vanilla's deathrest text in
                Rect tipRect = new Rect(0f, 0f, inner.width, row * 2f);
                need.DrawOnGUI(new Rect(0f, row, inner.width, row + 2f), int.MaxValue, 2f, drawArrows: false, doTooltip: false, tipRect, drawLabel: false);
                if (Mouse.IsOver(tipRect))
                {
                    Widgets.DrawHighlight(tipRect);
                    TooltipHandler.TipRegion(tipRect, SoftCycle.TipString(need, gene.pawn));
                }
            }
            Rect state = new Rect(0f, row * 2f, inner.width, Text.LineHeight);
            Text.Anchor = TextAnchor.UpperCenter;
            string label = StateLabel(gene.pawn);
            Widgets.Label(state, CpuOverload.Of(gene.pawn) != null && !gene.pawn.Deathresting ? label.Colorize(ColorLibrary.RedReadable) : label);
            Text.Anchor = TextAnchor.UpperLeft;
            if (Mouse.IsOver(state) && need != null)
            {
                Widgets.DrawHighlight(state);
                TooltipHandler.TipRegion(state, SoftCycle.StateLine(need, gene.pawn));
            }
            GUI.EndGroup();
            return new GizmoResult(GizmoState.Clear);
        }
    }
}
