using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RimshiSkeletons
{
    public enum SoftCycleKind
    {
        /// <summary>48 hours in a powered skeleton repair bed (vanilla deathrest underneath).</summary>
        RepairBed,

        /// <summary>5 days standing still, or hanging in a rib cage, with no repair bed (StandingReset.cs).</summary>
        Standing
    }

    /// <summary>
    /// The CPU soft cycle (docs/design/cpu-soft-cycle.md). Skeletons keep Biotech's deathrest gene and need, but the need is
    /// their CPU integrity: it falls over 30 days, a soft cycle brings it back to full, and at 0% the CPU overloads
    /// (CpuOverload.cs) instead of vanilla's deathrest exhaustion. A cycle in a repair bed takes 48 hours of uninterrupted
    /// power; losing power or stopping it early cancels it, leaving 0% integrity and an overload. Finishing a cycle clears
    /// the overload but costs the skeleton its short-term memories and some skill.
    /// </summary>
    public static class SoftCycle
    {
        public const int BedCycleTicks = 2 * GenDate.TicksPerDay;

        /// <summary>
        /// How much faster than vanilla deathrest the bed cycle runs: it adds extra deathrest ticks rather than patching the
        /// tiny MinDeathrestTicks getter, which the JIT may inline. Deathrest buildings that speed deathrest still count.
        /// </summary>
        public const float BedCycleSpeed = (float)Gene_Deathrest.BaseDeathrestTicksWithoutInterruptedHediff / BedCycleTicks;

        /// <summary>Real time left in a bed cycle.</summary>
        public static int BedTicksLeft(Gene_Deathrest gene) => Mathf.Max(0, Mathf.RoundToInt((gene.MinDeathrestTicks - gene.deathrestTicks) / BedCycleSpeed));

        /// <summary>Real time a bed cycle has run.</summary>
        public static int BedTicksDone(Gene_Deathrest gene) => Mathf.RoundToInt(gene.deathrestTicks / BedCycleSpeed);

        public static int BedTicksTotal(Gene_Deathrest gene) => Mathf.RoundToInt(gene.MinDeathrestTicks / BedCycleSpeed);
        public const int StandingResetTicks = 5 * GenDate.TicksPerDay;
        public const float IntegrityFallPerDay = 1f / 30f;

        /// <summary>Need intervals per day (vanilla runs needs every 150 ticks).</summary>
        public const float NeedIntervalsPerDay = GenDate.TicksPerDay / 150f;

        /// <summary>Share of the standing reset done, by days into it: slow for three days, then faster through the last.</summary>
        public static readonly SimpleCurve StandingCurve = new SimpleCurve
        {
            new CurvePoint(0f, 0f),
            new CurvePoint(1f, 0.04f),
            new CurvePoint(2f, 0.09f),
            new CurvePoint(3f, 0.16f),
            new CurvePoint(4f, 0.4f),
            new CurvePoint(5f, 1f)
        };

        /// <summary>Share of a skill's total XP lost from each skill a finished cycle wipes (vanilla's deathrest damage range).</summary>
        public static readonly FloatRange SkillLossRange = new FloatRange(0.03f, 0.06f);

        public static GeneDef Gene => RimshiDefOf.Rimshi_Deathrest;

        /// <summary>Set by whoever stops a bed cycle just before calling Wake, for the message and log.</summary>
        public static string pendingCancelReason;

        /// <summary>True for a skeleton (frame gene) that deathrests (any Gene_Deathrest, ours or vanilla's).</summary>
        public static bool IsSkeletonCycler(Pawn pawn) => SkeletonUtility.HasFrame(pawn) && pawn.CanDeathrest();

        public static Need_Deathrest Integrity(Pawn pawn) => pawn?.needs?.TryGetNeed<Need_Deathrest>();

        public static Gene_Deathrest GeneOf(Pawn pawn) => pawn?.genes?.GetFirstGeneOfType<Gene_Deathrest>();

        public static CompRimshi_RepairBed RepairBedOf(Building_Bed bed) => bed?.TryGetComp<CompRimshi_RepairBed>();

        /// <summary>True while the skeleton runs either kind of cycle.</summary>
        public static bool Cycling(Pawn pawn) => pawn.Deathresting || StandingReset.InProgress(pawn);

        /// <summary>Sum of the skeleton's skill levels, what the repair bed's cycle power draw is scaled by.</summary>
        public static int SkillLevels(Pawn pawn)
        {
            int total = 0;
            if (pawn?.skills == null)
            {
                return 0;
            }
            foreach (SkillRecord skill in pawn.skills.skills)
            {
                if (!skill.TotallyDisabled)
                {
                    total += skill.Level;
                }
            }
            return total;
        }

        /// <summary>Raises integrity as a cycle's share done goes from before to after, reaching 100% exactly when the cycle ends.</summary>
        public static void Raise(Pawn pawn, float before, float after)
        {
            Need_Deathrest need = Integrity(pawn);
            if (need == null || after <= before)
            {
                return;
            }
            if (after >= 1f || before >= 1f)
            {
                need.CurLevel = need.MaxLevel;
                return;
            }
            need.CurLevel += (need.MaxLevel - need.CurLevel) * (after - before) / (1f - before);
        }

        /// <summary>
        /// A repair bed the skeleton can start a soft cycle in now: ours, powered with enough to spare for the cycle, roofed
        /// (vanilla wakes deathresters in sunlight), free, not forbidden, reachable and reservable. reason says why not.
        /// </summary>
        public static bool CanCycleIn(Pawn pawn, Building_Bed bed, out string reason)
        {
            reason = null;
            CompRimshi_RepairBed comp = RepairBedOf(bed);
            if (comp == null || !bed.Spawned || bed.Map != pawn.Map || bed.IsForbidden(pawn) || bed.IsBurning())
            {
                reason = "no usable repair bed";
                return false;
            }
            if (bed.Faction != pawn.Faction && (pawn.HostFaction == null || bed.Faction != pawn.HostFaction))
            {
                reason = "not our bed";
                return false;
            }
            if (bed.ForPrisoners != pawn.IsPrisoner || bed.ForSlaves != pawn.IsSlave)
            {
                reason = "wrong bed type";
                return false;
            }
            if (!bed.CurOccupants.Contains(pawn) && !bed.AnyUnoccupiedSleepingSlot)
            {
                reason = "occupied";
                return false;
            }
            if (bed.OccupiedRect().Any(c => c.GetRoof(bed.Map) == null))
            {
                reason = "not roofed";
                return false;
            }
            if (!comp.Powered)
            {
                reason = "unpowered";
                return false;
            }
            if (!comp.CanPowerCycle(pawn))
            {
                reason = "not enough power (needs " + comp.CycleWatts(pawn).ToString("F0") + " W more)";
                return false;
            }
            if (!pawn.CanReach(bed, PathEndMode.OnCell, Danger.Some) || !(pawn.HasReserved(bed) || pawn.CanReserve(bed, bed.SleepingSlotsCount)))
            {
                reason = "unreachable or reserved";
                return false;
            }
            return true;
        }

        public static bool CanCycleIn(Pawn pawn, Building_Bed bed) => CanCycleIn(pawn, bed, out _);

        /// <summary>The nearest repair bed the skeleton can cycle in; reason says why there is none.</summary>
        public static Building_Bed BestBedFor(Pawn pawn, out string reason)
        {
            reason = "no repair bed";
            if (!pawn.Spawned)
            {
                return null;
            }
            if (pawn.CurrentBed() is Building_Bed current && CanCycleIn(pawn, current))
            {
                return current;
            }
            Building_Bed best = null;
            float bestDistance = float.MaxValue;
            foreach (Building_Bed bed in pawn.Map.listerBuildings.allBuildingsColonist.OfType<Building_Bed>())
            {
                if (RepairBedOf(bed) == null)
                {
                    continue;
                }
                if (!CanCycleIn(pawn, bed, out string why))
                {
                    reason = why;
                    continue;
                }
                float distance = bed.Position.DistanceToSquared(pawn.Position);
                if (distance < bestDistance)
                {
                    best = bed;
                    bestDistance = distance;
                }
            }
            return best;
        }

        public static Building_Bed BestBedFor(Pawn pawn) => BestBedFor(pawn, out _);

        /// <summary>A finished cycle: full integrity, overload cleared, short-term memories gone, some skill lost, and the cycle's thought.</summary>
        public static void Complete(Pawn pawn, SoftCycleKind kind)
        {
            Need_Deathrest need = Integrity(pawn);
            if (need != null)
            {
                need.CurLevel = need.MaxLevel;
            }
            bool wasOverloaded = CpuOverload.End(pawn);
            int forgotten = ForgetShortTermMemories(pawn);
            List<string> lost = LoseSkills(pawn, kind == SoftCycleKind.RepairBed ? 1 : 2);
            ThoughtDef thought = kind == SoftCycleKind.RepairBed ? RimshiDefOf.Rimshi_SoftCycleHoned : RimshiDefOf.Rimshi_SoftResetForeign;
            pawn.needs?.mood?.thoughts?.memories?.TryGainMemory(thought);
            string detail = pawn.LabelShort + ": integrity 100%, overload cleared " + wasOverloaded + ", " + forgotten + " memories forgotten, skill lost: "
                + (lost.Count == 0 ? "none" : string.Join(", ", lost)) + ", thought " + thought.defName;
            if (kind == SoftCycleKind.RepairBed)
            {
                RimshiLog.Test("softcycle", "complete bed", detail);
            }
            else
            {
                RimshiLog.Test("softcycle", "complete standing", detail);
            }
            if (PawnUtility.ShouldSendNotificationAbout(pawn))
            {
                string text = pawn.LabelShort + " finished a CPU soft " + (kind == SoftCycleKind.RepairBed ? "cycle" : "reset") + ". CPU integrity is back to 100%"
                    + (wasOverloaded ? " and the overload has cleared" : "") + ". Short-term memories were flushed"
                    + (lost.Count > 0 ? "; skill lost: " + string.Join(", ", lost) : "") + ".";
                Messages.Message(text, pawn, MessageTypeDefOf.PositiveEvent);
            }
        }

        /// <summary>A cycle cut short: integrity drops to 0% and the CPU overloads.</summary>
        public static void Cancel(Pawn pawn, SoftCycleKind kind, string reason)
        {
            Need_Deathrest need = Integrity(pawn);
            if (need != null)
            {
                need.CurLevel = 0f;
            }
            CpuOverload.Begin(pawn, "soft " + (kind == SoftCycleKind.RepairBed ? "cycle" : "reset") + " cancelled: " + reason);
            if (kind == SoftCycleKind.RepairBed)
            {
                RimshiLog.Test("softcycle", "cancelled bed", pawn.LabelShort + ": " + reason + ", integrity 0%");
            }
            else
            {
                RimshiLog.Test("softcycle", "cancelled standing", pawn.LabelShort + ": " + reason + ", integrity 0%");
            }
            if (PawnUtility.ShouldSendNotificationAbout(pawn))
            {
                Messages.Message(pawn.LabelShort + "'s CPU soft " + (kind == SoftCycleKind.RepairBed ? "cycle" : "reset") + " was cancelled (" + reason
                    + "). CPU integrity dropped to 0% and the CPU is overloading.", pawn, MessageTypeDefOf.NegativeEvent);
            }
        }

        /// <summary>Flushes the memory buffer: every mood memory goes, good and bad.</summary>
        private static int ForgetShortTermMemories(Pawn pawn)
        {
            MemoryThoughtHandler memories = pawn.needs?.mood?.thoughts?.memories;
            if (memories == null)
            {
                return 0;
            }
            List<Thought_Memory> all = memories.Memories.ToList();
            foreach (Thought_Memory memory in all)
            {
                memories.RemoveMemory(memory);
            }
            return all.Count;
        }

        /// <summary>Takes 3 to 6% of the total XP from up to count skills, picked weighted by level like vanilla's deathrest damage.</summary>
        private static List<string> LoseSkills(Pawn pawn, int count)
        {
            var lost = new List<string>();
            if (pawn.skills == null)
            {
                return lost;
            }
            List<SkillRecord> candidates = pawn.skills.skills.Where(s => !s.TotallyDisabled && s.XpTotalEarned > 0f).ToList();
            for (int i = 0; i < count && candidates.TryRandomElementByWeight(s => s.GetLevel() * 10f + 1f, out SkillRecord skill); i++)
            {
                float xp = skill.XpTotalEarned * SkillLossRange.RandomInRange;
                skill.Learn(-xp, direct: true);
                lost.Add(skill.def.label + " -" + ((int)xp) + " XP");
                candidates.Remove(skill);
            }
            return lost;
        }

        /// <summary>
        /// When a save loads: skeletons saved before the deathrest gene existed get it if their xenotype has it, and
        /// vanilla's deathrest exhaustion or interrupted deathrest left on a skeleton becomes a CPU overload.
        /// </summary>
        public static void MigrateSave()
        {
            int added = 0;
            int converted = 0;
            foreach (Pawn pawn in PawnsFinder.AllMapsWorldAndTemporary_AliveOrDead)
            {
                XenotypeDef xenotype = pawn?.genes?.Xenotype;
                if (xenotype?.genes != null && xenotype.genes.Contains(Gene) && !pawn.genes.HasActiveGene(Gene) && pawn.genes.GetGene(Gene) == null)
                {
                    pawn.genes.AddGene(Gene, !xenotype.inheritable);
                    added++;
                }
                if (pawn?.health == null || !SkeletonUtility.HasFrame(pawn))
                {
                    continue;
                }
                bool exhausted = false;
                foreach (HediffDef old in new[] { HediffDefOf.DeathrestExhaustion, HediffDefOf.InterruptedDeathrest })
                {
                    Hediff hediff = old == null ? null : pawn.health.hediffSet.GetFirstHediffOfDef(old);
                    if (hediff != null)
                    {
                        pawn.health.RemoveHediff(hediff);
                        exhausted |= old == HediffDefOf.DeathrestExhaustion;
                    }
                }
                if (exhausted && !pawn.Dead)
                {
                    CpuOverload.Begin(pawn, "deathrest exhaustion from an older save");
                    converted++;
                }
            }
            if (added > 0)
            {
                RimshiLog.Test("deathrest", "gene added", added + " skeletons from an older save got the deathrest gene");
            }
            if (converted > 0)
            {
                RimshiLog.Test("softcycle", "migrated", converted + " skeletons had deathrest exhaustion, now CPU overload");
            }
        }

        private static readonly CachedTexture StopTex = new CachedTexture("UI/Gizmos/Wake");
        private static readonly CachedTexture CycleTex = new CachedTexture("UI/Icons/Genes/Gene_Deathrest");
        private static readonly CachedTexture ResetTex = new CachedTexture("UI/Gizmos/DeathrestAutoWake");

        /// <summary>The skeleton's soft cycle buttons, in place of vanilla's wake and auto-wake.</summary>
        public static IEnumerable<Gizmo> Gizmos(Pawn pawn, Gene_Deathrest gene)
        {
            bool controlled = pawn.IsColonistPlayerControlled || pawn.IsPrisonerOfColony;
            if (pawn.Deathresting)
            {
                if (controlled)
                {
                    var stop = new Command_Action
                    {
                        defaultLabel = "Stop soft cycle",
                        defaultDesc = pawn.LabelShort + " has been soft cycling for " + BedTicksDone(gene).ToStringTicksToPeriod() + " of "
                            + BedTicksTotal(gene).ToStringTicksToPeriod() + ".\n\nStopping before the cycle finishes cancels it: CPU integrity drops to 0% and the CPU overloads.",
                        icon = StopTex.Texture,
                        action = delegate
                        {
                            if (gene.DeathrestPercent >= 1f)
                            {
                                gene.Wake();
                                return;
                            }
                            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("Stop " + pawn.LabelShort + "'s soft cycle? It is not finished: CPU integrity will drop to 0% and "
                                + pawn.LabelShort + " will overload.", delegate
                                {
                                    pendingCancelReason = "stopped by the player";
                                    gene.Wake();
                                }, destructive: true));
                        }
                    };
                    if (SanguophageUtility.ShouldBeDeathrestingOrInComaInsteadOfDead(pawn))
                    {
                        stop.Disable("WakeDisabledCriticalCondition".Translate(pawn.Named("PAWN")));
                    }
                    yield return stop;
                }
            }
            else if (StandingReset.InProgress(pawn))
            {
                if (controlled)
                {
                    yield return new Command_Action
                    {
                        defaultLabel = "Stop soft reset",
                        defaultDesc = "Stopping before the reset finishes cancels it: CPU integrity drops to 0% and the CPU overloads.",
                        icon = StopTex.Texture,
                        action = delegate
                        {
                            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("Stop " + pawn.LabelShort + "'s soft reset? CPU integrity will drop to 0% and "
                                + pawn.LabelShort + " will overload.", delegate { StandingReset.Cancel(pawn, "stopped by the player"); }, destructive: true));
                        }
                    };
                }
            }
            else if (pawn.IsColonistPlayerControlled && pawn.Spawned)
            {
                Building_Bed bed = BestBedFor(pawn, out string reason);
                var cycle = new Command_Action
                {
                    defaultLabel = "Soft cycle",
                    defaultDesc = "Go to the nearest powered skeleton repair bed and run a CPU soft cycle: 48 hours that must not lose power. The bed draws "
                        + WattsPerLevel + " W for each of " + pawn.LabelShort + "'s skill levels (" + SkillLevels(pawn) + ") while it runs. Finishing restores CPU integrity to 100% and clears any overload, "
                        + "but flushes short-term memories and wears away a little of one skill. Stopping early or losing power cancels it with 0% integrity and an overload.",
                    icon = CycleTex.Texture,
                    action = delegate
                    {
                        pawn.jobs.TryTakeOrderedJob(JobMaker.MakeJob(JobDefOf.Deathrest, bed), JobTag.Misc);
                    }
                };
                if (bed == null)
                {
                    cycle.Disable(reason.CapitalizeFirst() + ".");
                }
                yield return cycle;
                yield return StandingReset.BeginGizmo(pawn, ResetTex.Texture);
            }
            if (DebugSettings.ShowDevGizmos)
            {
                yield return new Command_Action
                {
                    defaultLabel = "DEV: Set CPU integrity",
                    action = delegate
                    {
                        var options = new List<FloatMenuOption>();
                        foreach (float level in new[] { 0f, 0.05f, 0.5f, 1f })
                        {
                            options.Add(new FloatMenuOption(level.ToStringPercent(), delegate
                            {
                                Need_Deathrest need = Integrity(pawn);
                                if (need != null)
                                {
                                    need.CurLevel = level;
                                }
                                if (level <= 0f)
                                {
                                    CpuOverload.Begin(pawn, "dev action");
                                }
                            }));
                        }
                        Find.WindowStack.Add(new FloatMenu(options));
                    }
                };
            }
        }

        /// <summary>Watts per skill level, for the gizmo text (the repair bed def holds the real value).</summary>
        private static float WattsPerLevel => RimshiDefOf.Rimshi_SkeletonRepairBed.GetCompProperties<CompProperties_Rimshi_RepairBed>()?.cycleWattsPerSkillLevel ?? 0f;

        /// <summary>The integrity tooltip, replacing vanilla's deathrest one for skeletons.</summary>
        public static string TipString(Need_Deathrest need, Pawn pawn)
        {
            NeedRelabel relabel = SkeletonNeeds.RelabelFor(pawn, need.def);
            string label = relabel?.label.CapitalizeFirst() ?? need.LabelCap;
            string text = (label + ": " + need.CurLevelPercentage.ToStringPercent()).Colorize(ColoredText.TipSectionTitleColor) + "\n";
            Gene_Deathrest gene = GeneOf(pawn);
            if (pawn.Deathresting && gene != null)
            {
                text += "Soft cycling: " + gene.DeathrestPercent.ToStringPercent("F0") + " done, " + BedTicksLeft(gene).ToStringTicksToPeriod() + " to go.";
            }
            else if (StandingReset.InProgress(pawn))
            {
                text += "Soft resetting without a repair bed: " + StandingReset.FractionDone(pawn).ToStringPercent("F0") + " done.";
            }
            else if (RibCage.Suspended(pawn))
            {
                text += "Suspended in a rib cage: CPU integrity is holding.";
            }
            else if (need.CurLevel > 0f)
            {
                text += "The CPU will overload in " + "PeriodDays".Translate((need.CurLevel / IntegrityFallPerDay).ToString("F1")) + " without a soft cycle.";
            }
            else
            {
                text += ("CPU overloaded. " + pawn.LabelShort + " needs a soft cycle.").Colorize(ColorLibrary.RedReadable);
            }
            return text + "\n\n" + (relabel?.description ?? need.def.description);
        }
    }

    /// <summary>
    /// Skeletons soft cycle only in a powered repair bed: the vanilla deathrest job giver's bed (or bare ground) is swapped
    /// for the nearest one, and with none there is no job, so integrity runs down to an overload unless the player orders a
    /// standing reset.
    /// </summary>
    [HarmonyPatch(typeof(JobGiver_GetDeathrest), "TryGiveJob")]
    public static class Patch_DeathrestInRepairBed
    {
        private static readonly HashSet<int> loggedNoBed = new HashSet<int>();

        public static bool Prepare() => PatchTarget.Exists(typeof(JobGiver_GetDeathrest), "TryGiveJob");

        public static void Postfix(Pawn pawn, ref Job __result)
        {
            if (__result == null || !SoftCycle.IsSkeletonCycler(pawn))
            {
                return;
            }
            Building_Bed bed = SoftCycle.BestBedFor(pawn, out string reason);
            if (bed == null || RibCage.Suspended(pawn) || StandingReset.InProgress(pawn))
            {
                __result = null;
                if (bed == null && loggedNoBed.Add(pawn.thingIDNumber))
                {
                    RimshiLog.Test("softcycle", "no bed", pawn.LabelShort + " is due a soft cycle but no repair bed is usable (" + reason + ")");
                }
                return;
            }
            loggedNoBed.Remove(pawn.thingIDNumber);
            if (__result.targetA.Thing != bed)
            {
                __result = JobMaker.MakeJob(JobDefOf.Deathrest, bed);
            }
            RimshiLog.Test("softcycle", "start", pawn.LabelShort + " going to soft cycle in " + bed.LabelShort + " at " + bed.Position + ", "
                + SoftCycle.RepairBedOf(bed).CycleWatts(pawn).ToString("F0") + " W for " + SoftCycle.SkillLevels(pawn) + " skill levels");
        }
    }

    /// <summary>
    /// The repair bed and rib cage are medical beds, and vanilla only lets a pawn use a medical bed while it should seek
    /// medical rest. A skeleton on its way to or in a soft cycle, or ordered into a rib cage, counts.
    /// </summary>
    [HarmonyPatch(typeof(HealthAIUtility), nameof(HealthAIUtility.ShouldSeekMedicalRest))]
    public static class Patch_DeathrestMedicalBed
    {
        [HarmonyPriority(Priority.Low)]
        public static void Postfix(Pawn pawn, ref bool __result)
        {
            if (__result)
            {
                return;
            }
            JobDef job = pawn.CurJobDef;
            if ((pawn.Deathresting || job == JobDefOf.Deathrest || job == RimshiDefOf.Rimshi_SoftReset || job == RimshiDefOf.Rimshi_HangInRibCage) && SkeletonUtility.HasFrame(pawn))
            {
                __result = true;
            }
        }
    }

    /// <summary>
    /// The bed cycle's tick. In a powered repair bed it raises integrity in step with the cycle; in an unpowered one the
    /// cycle is cancelled. Anywhere else (carried, rescued to a rib cage, an older save's deathrest on the floor) it is held,
    /// neither progressing nor cancelled. A finished cycle always ends by itself.
    /// </summary>
    [HarmonyPatch(typeof(Gene_Deathrest), nameof(Gene_Deathrest.TickDeathresting))]
    public static class Patch_SoftCycleTick
    {
        private static readonly HashSet<int> loggedCycle = new HashSet<int>();
        private static readonly HashSet<int> loggedHeld = new HashSet<int>();

        public static void Prefix(Gene_Deathrest __instance, ref bool paused, int delta)
        {
            Pawn pawn = __instance.pawn;
            if (paused || !SkeletonUtility.HasFrame(pawn))
            {
                return;
            }
            __instance.autoWake = true;
            CompRimshi_RepairBed bed = SoftCycle.RepairBedOf(pawn.CurrentBed());
            if (bed == null)
            {
                paused = true;
                loggedCycle.Remove(pawn.thingIDNumber);
                if (loggedHeld.Add(pawn.thingIDNumber))
                {
                    RimshiLog.Test("softcycle", "held", pawn.LabelShort + " is soft cycling outside a repair bed" + (RibCage.Suspended(pawn) ? " (in a rib cage)" : "") + ", cycle held");
                }
                return;
            }
            loggedHeld.Remove(pawn.thingIDNumber);
            if (!bed.Powered)
            {
                paused = true;
                loggedCycle.Remove(pawn.thingIDNumber);
                SoftCycle.pendingCancelReason = bed.parent.LabelShort + " lost power";
                __instance.Wake();
                return;
            }
            // Vanilla adds delta after this; the extra makes the cycle run BedCycleSpeed times as fast.
            int total = __instance.MinDeathrestTicks;
            int extra = Mathf.RoundToInt(delta * (SoftCycle.BedCycleSpeed - 1f));
            float before = Mathf.Clamp01((float)__instance.deathrestTicks / total);
            __instance.deathrestTicks += extra;
            float after = Mathf.Clamp01((float)(__instance.deathrestTicks + delta) / total);
            SoftCycle.Raise(pawn, before, after);
            if (loggedCycle.Add(pawn.thingIDNumber))
            {
                RimshiLog.Test("softcycle", "cycling", pawn.LabelShort + " soft cycling in " + bed.parent.LabelShort + ", " + SoftCycle.BedTicksTotal(__instance).ToStringTicksToPeriod() + ", drawing "
                    + bed.CycleWatts(pawn).ToString("F0") + " W, integrity " + SoftCycle.Integrity(pawn)?.CurLevelPercentage.ToStringPercent());
            }
        }
    }

    /// <summary>
    /// Waking a skeleton: a finished cycle completes (vanilla still applies any deathrest building bonuses); an unfinished
    /// one is cancelled instead of vanilla's interrupted deathrest.
    /// </summary>
    [HarmonyPatch(typeof(Gene_Deathrest), nameof(Gene_Deathrest.Wake))]
    public static class Patch_SoftCycleWake
    {
        public static bool Prefix(Gene_Deathrest __instance)
        {
            Pawn pawn = __instance.pawn;
            if (!SkeletonUtility.HasFrame(pawn))
            {
                return true;
            }
            string reason = SoftCycle.pendingCancelReason ?? "woken early";
            SoftCycle.pendingCancelReason = null;
            if (__instance.DeathrestPercent >= 1f)
            {
                SoftCycle.Complete(pawn, SoftCycleKind.RepairBed);
                return true;
            }
            __instance.deathrestTicks = 0;
            __instance.adjustedDeathrestTicks = 0f;
            Hediff deathrest = pawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.Deathrest);
            if (deathrest != null)
            {
                pawn.health.RemoveHediff(deathrest);
            }
            SoftCycle.Cancel(pawn, SoftCycleKind.RepairBed, reason);
            return false;
        }
    }

    /// <summary>
    /// CPU integrity for skeletons: falls 1/30 a day like vanilla deathrest, holds while frozen, cycling or suspended in a
    /// rib cage (cycles raise it themselves), and at 0% overloads the CPU instead of vanilla's deathrest exhaustion.
    /// </summary>
    [HarmonyPatch(typeof(Need_Deathrest), nameof(Need_Deathrest.NeedInterval))]
    public static class Patch_CpuIntegrity
    {
        private static readonly AccessTools.FieldRef<Need, Pawn> NeedPawn = AccessTools.FieldRefAccess<Need, Pawn>("pawn");
        private static readonly System.Func<Need, bool> IsFrozen = AccessTools.MethodDelegate<System.Func<Need, bool>>(AccessTools.PropertyGetter(typeof(Need), "IsFrozen"));

        public static bool Prefix(Need_Deathrest __instance)
        {
            Pawn pawn = NeedPawn(__instance);
            if (!SkeletonUtility.HasFrame(pawn))
            {
                return true;
            }
            if (IsFrozen(__instance) || SoftCycle.Cycling(pawn) || RibCage.Suspended(pawn))
            {
                return false;
            }
            __instance.CurLevel -= SoftCycle.IntegrityFallPerDay / SoftCycle.NeedIntervalsPerDay;
            if (__instance.CurLevel <= 0f)
            {
                CpuOverload.Begin(pawn, "CPU integrity ran out");
            }
            return false;
        }
    }

    /// <summary>The skeleton's integrity tooltip (vanilla's speaks of deathrest and exhaustion).</summary>
    [HarmonyPatch(typeof(Need_Deathrest), nameof(Need_Deathrest.GetTipString))]
    public static class Patch_CpuIntegrityTip
    {
        private static readonly AccessTools.FieldRef<Need, Pawn> NeedPawn = AccessTools.FieldRefAccess<Need, Pawn>("pawn");

        public static bool Prefix(Need_Deathrest __instance, ref string __result)
        {
            Pawn pawn = NeedPawn(__instance);
            if (!SkeletonUtility.HasFrame(pawn))
            {
                return true;
            }
            __result = SoftCycle.TipString(__instance, pawn);
            return false;
        }
    }

    /// <summary>Skeletons get the soft cycle buttons in place of vanilla's wake and auto-wake (a finished cycle always ends by itself).</summary>
    [HarmonyPatch(typeof(Gene_Deathrest), nameof(Gene_Deathrest.GetGizmos))]
    public static class Patch_SoftCycleGizmos
    {
        public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, Gene_Deathrest __instance)
        {
            Pawn pawn = __instance.pawn;
            bool skeleton = SkeletonUtility.HasFrame(pawn);
            string wake = skeleton ? "Wake".Translate().CapitalizeFirst().ToString() : null;
            string autoWake = skeleton ? "AutoWake".Translate().CapitalizeFirst().ToString() : null;
            foreach (Gizmo gizmo in __result)
            {
                if (skeleton && gizmo is Command command && (command.defaultLabel == wake || command.defaultLabel == autoWake))
                {
                    continue;
                }
                yield return gizmo;
            }
            if (skeleton && __instance.Active)
            {
                foreach (Gizmo gizmo in SoftCycle.Gizmos(pawn, __instance))
                {
                    yield return gizmo;
                }
            }
        }
    }

    /// <summary>Job report and health tab label: "soft cycling" for a skeleton instead of "deathresting".</summary>
    [HarmonyPatch(typeof(SanguophageUtility), nameof(SanguophageUtility.DeathrestJobReport))]
    public static class Patch_SoftCycleReport
    {
        public static bool Prefix(Pawn pawn, ref string __result)
        {
            Gene_Deathrest gene = SkeletonUtility.HasFrame(pawn) ? SoftCycle.GeneOf(pawn) : null;
            if (gene == null)
            {
                return true;
            }
            __result = SoftCycle.RepairBedOf(pawn.CurrentBed()) == null ? "soft cycle held (not in a repair bed)" : "soft cycling: " + gene.DeathrestPercent.ToStringPercent("F0");
            return false;
        }
    }

    [HarmonyPatch(typeof(Hediff), nameof(Hediff.LabelBase), MethodType.Getter)]
    public static class Patch_SoftCycleHediffLabel
    {
        public static void Postfix(Hediff __instance, ref string __result)
        {
            if (__instance.def == HediffDefOf.Deathrest && SkeletonUtility.HasFrame(__instance.pawn))
            {
                __result = "CPU soft cycle";
            }
        }
    }
}
