using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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

        /// <summary>
        /// How fast the skeleton's bed cycle runs now: BedCycleSpeed, faster still for each soft cycle facility (tuning
        /// fork) engaged with it, whose time offsets shorten the cycle (SoftCycleFacility.cs).
        /// </summary>
        public static float BedSpeed(Pawn pawn) => BedCycleSpeed / SoftCycleFacilities.TimeFactor(RepairBedOf(pawn?.CurrentBed()), pawn, readyOnly: false);

        /// <summary>Real time left in a bed cycle, at the current speed.</summary>
        public static int BedTicksLeft(Gene_Deathrest gene) => Mathf.Max(0, Mathf.RoundToInt((gene.MinDeathrestTicks - gene.deathrestTicks) / BedSpeed(gene.pawn)));

        /// <summary>Real time a bed cycle has run, as if it had run at the current speed throughout.</summary>
        public static int BedTicksDone(Gene_Deathrest gene) => Mathf.RoundToInt(gene.deathrestTicks / BedSpeed(gene.pawn));

        public static int BedTicksTotal(Gene_Deathrest gene) => Mathf.RoundToInt(gene.MinDeathrestTicks / BedSpeed(gene.pawn));
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
            if (!comp.CanPowerCycle(pawn, out string powerReason))
            {
                reason = powerReason;
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
            // Whatever is still needed to get up mends now (SoftCycleMending.cs).
            SoftCycleMending.Finish(pawn);
            Need_Deathrest need = Integrity(pawn);
            if (need != null)
            {
                need.CurLevel = need.MaxLevel;
            }
            bool wasOverloaded = CpuOverload.End(pawn);
            // A storm held since the last cycle ends with this one (a spire that joined it gives a new one after this).
            SpireStorm.End(pawn, "a soft cycle finished");
            int forgotten = ForgetShortTermMemories(pawn);
            var lostSkills = new List<string>();
            List<string> lost = LoseSkills(pawn, kind == SoftCycleKind.RepairBed ? 1 : 2, lostSkills);
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
                string text = pawn.LabelShort + " finished a CPU soft " + (kind == SoftCycleKind.RepairBed ? "cycle" : "reset") + ". Its thoughts run clean again"
                    + (lostSkills.Count > 0 ? ", though some of what it knew of " + string.Join(" and ", lostSkills) + " has slipped away" : "") + ".";
                Messages.Message(text, pawn, MessageTypeDefOf.PositiveEvent);
            }
        }

        /// <summary>
        /// A cycle cut short. In a repair bed (woken or power lost) the skeleton keeps the integrity the cycle has raised so
        /// far but gets none of a finished cycle's effects (Matthew, 2026-10-27). Without a bed, integrity drops to 0% and the
        /// CPU overloads.
        /// </summary>
        public static void Cancel(Pawn pawn, SoftCycleKind kind, string reason)
        {
            SoftCycleMending.Forget(pawn);
            Need_Deathrest need = Integrity(pawn);
            if (kind == SoftCycleKind.RepairBed)
            {
                RimshiLog.Test("softcycle", "cancelled bed", pawn.LabelShort + ": " + reason + ", integrity kept at " + need?.CurLevelPercentage.ToStringPercent());
            }
            else
            {
                if (need != null)
                {
                    need.CurLevel = 0f;
                }
                CpuOverload.Begin(pawn, "soft reset cancelled: " + reason);
                RimshiLog.Test("softcycle", "cancelled standing", pawn.LabelShort + ": " + reason + ", integrity 0%");
            }
            if (PawnUtility.ShouldSendNotificationAbout(pawn))
            {
                Messages.Message(pawn.LabelShort + "'s CPU soft " + (kind == SoftCycleKind.RepairBed ? "cycle" : "reset") + " was cut short (" + reason + ").", pawn, MessageTypeDefOf.NegativeEvent);
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
        private static List<string> LoseSkills(Pawn pawn, int count, List<string> lostLabels = null)
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
                lostLabels?.Add(skill.def.label);
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

        /// <summary>The standing wake button's warning: there is a price, without saying what it is.</summary>
        public const string WakeWarning = "Waking a skeleton before its soft cycle is finished tears the CPU out of it half done, and it will pay for that.";

        /// <summary>The bed wake button's note: a bed cycle cut short costs nothing but the rest of the cycle.</summary>
        public const string BedWakeWarning = "Waking it now leaves the soft cycle unfinished.";

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
                        defaultLabel = "Wake up",
                        defaultDesc = pawn.LabelShort + " has been soft cycling for " + BedTicksDone(gene).ToStringTicksToPeriod() + ". " + BedWakeWarning,
                        icon = StopTex.Texture,
                        action = delegate
                        {
                            if (gene.DeathrestPercent >= 1f)
                            {
                                gene.Wake();
                                return;
                            }
                            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("Wake " + pawn.LabelShort + "? Its soft cycle is not finished.", delegate
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
                        defaultLabel = "Wake up",
                        defaultDesc = WakeWarning + (StandingReset.Of(pawn)?.lying == true ? ""
                            : " Nothing else will disturb " + pawn.LabelShort + " while it stands here: it ignores orders and cannot be drafted until it wakes."),
                        icon = StopTex.Texture,
                        action = delegate
                        {
                            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("Wake " + pawn.LabelShort + "? Its soft cycle is not finished.", delegate { StandingReset.Cancel(pawn, "stopped by the player"); }, destructive: true));
                        }
                    };
                }
            }
            else if (pawn.IsColonistPlayerControlled && pawn.Spawned)
            {
                yield return pawn.Downed ? LyingCycleGizmo(pawn) : CycleGizmo(pawn);
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

        /// <summary>
        /// The one "Soft cycle" button: target a skeleton repair bed for the bed cycle, or a rib cage or any spot the
        /// skeleton can stand on for the slow soft reset (StandingReset).
        /// </summary>
        public static Command CycleGizmo(Pawn pawn)
        {
            var command = new Command_Target
            {
                defaultLabel = "Soft cycle",
                defaultDesc = "Have " + pawn.LabelShort + " reset its CPU and memory buffer. Choose a powered skeleton repair bed, a rib cage, or any spot where it can stand perfectly still. "
                    + "Without a repair bed, a soft reset is slow and rough. Either way, it is best not interrupted.",
                icon = CycleTex.Texture,
                targetingParams = new TargetingParameters
                {
                    canTargetLocations = true,
                    canTargetBuildings = true,
                    canTargetPawns = false,
                    // Any repair bed can be clicked: if it can't take the skeleton now, OrderCycle says why instead
                    // of the click silently doing nothing.
                    validator = t => RepairBedAt(t.Cell, pawn.Map) != null || ValidTarget(pawn, t.Cell, out _)
                },
                action = target => OrderCycle(pawn, target)
            };
            if (pawn.Downed || pawn.InMentalState)
            {
                command.Disable(pawn.LabelShort + " cannot start a soft cycle now.");
            }
            return command;
        }

        /// <summary>
        /// The "Soft cycle" button on a downed skeleton: it soft resets where it lies, on the ground, in a rib cage or in any
        /// bed it was carried to (StandingReset.BeginLying). Not in a powered repair bed, which is already seeing to it.
        /// </summary>
        public static Command LyingCycleGizmo(Pawn pawn)
        {
            var command = new Command_Action
            {
                defaultLabel = "Soft cycle",
                defaultDesc = "Have " + pawn.LabelShort + " reset its CPU and memory buffer where it lies. Without a repair bed, a soft reset is slow and rough. It is best not interrupted.",
                icon = CycleTex.Texture,
                action = delegate { StandingReset.BeginLying(pawn); }
            };
            CompRimshi_RepairBed bed = RepairBedOf(pawn.CurrentBed());
            if (bed != null && bed.Powered)
            {
                command.Disable(pawn.LabelShort + " is lying in a powered repair bed.");
            }
            else if (pawn.InMentalState)
            {
                command.Disable(pawn.LabelShort + " cannot start a soft cycle now.");
            }
            return command;
        }

        /// <summary>
        /// The right-click "Soft cycle" option on a repair bed for a selected skeleton, greyed out with the reason when the
        /// bed can't take it now. Null when the option doesn't apply (not a repair bed, not a player's skeleton, already cycling).
        /// </summary>
        public static FloatMenuOption RepairBedFloatOption(Pawn pawn, Building_Bed bed)
        {
            if (RepairBedOf(bed) == null || pawn == null || !IsSkeletonCycler(pawn) || !pawn.IsColonistPlayerControlled || pawn.Downed || Cycling(pawn))
            {
                return null;
            }
            if (!CanCycleIn(pawn, bed, out string reason))
            {
                return new FloatMenuOption("Soft cycle (" + reason + ")", null);
            }
            return FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption("Soft cycle", delegate { OrderCycle(pawn, bed); }), pawn, bed);
        }

        /// <summary>A repair bed on the cell, if any.</summary>
        public static Building_Bed RepairBedAt(IntVec3 cell, Map map)
        {
            if (map == null || !cell.InBounds(map))
            {
                return null;
            }
            return cell.GetThingList(map).OfType<Building_Bed>().FirstOrDefault(b => RepairBedOf(b) != null);
        }

        /// <summary>A repair bed the skeleton can cycle in, a rib cage it can hang in, or a cell it can stand on.</summary>
        public static bool ValidTarget(Pawn pawn, LocalTargetInfo target, out string reason)
        {
            Building_Bed bed = RepairBedAt(target.Cell, pawn.Map);
            if (bed != null)
            {
                return CanCycleIn(pawn, bed, out reason);
            }
            return StandingReset.ValidTarget(pawn, target, out reason);
        }

        public static void OrderCycle(Pawn pawn, LocalTargetInfo target)
        {
            Building_Bed bed = RepairBedAt(target.Cell, pawn.Map);
            if (bed == null)
            {
                StandingReset.Order(pawn, target);
                return;
            }
            if (!CanCycleIn(pawn, bed, out string reason))
            {
                Messages.Message("Cannot soft cycle there: " + reason + ".", bed, MessageTypeDefOf.RejectInput, false);
                RimshiLog.Test("softcycle", "bed refused", pawn.LabelShort + " can't soft cycle in " + bed.LabelShort + " at " + bed.Position + ": " + reason);
                return;
            }
            pawn.jobs.TryTakeOrderedJob(JobMaker.MakeJob(JobDefOf.Deathrest, bed), JobTag.Misc);
            RimshiLog.Test("softcycle", "bed ordered", pawn.LabelShort + " ordered to soft cycle in " + bed.LabelShort + " at " + bed.Position);
        }

        /// <summary>
        /// One line on where the skeleton's CPU is right now, in lore voice. Never a countdown or a forecast (no-spoiler
        /// rule, docs/design/cpu-soft-cycle.md).
        /// </summary>
        public static string StateLine(Need_Deathrest need, Pawn pawn)
        {
            Gene_Deathrest gene = GeneOf(pawn);
            if (pawn.Deathresting && gene != null)
            {
                return "Soft cycling: " + gene.DeathrestPercent.ToStringPercent("F0") + ".";
            }
            if (StandingReset.InProgress(pawn))
            {
                return "Soft resetting: " + StandingReset.FractionDone(pawn).ToStringPercent("F0") + ".";
            }
            if (RibCage.Suspended(pawn))
            {
                return "Suspended in a rib cage. Nothing in its mind is moving.";
            }
            if (CpuOverload.Of(pawn) != null || need.CurLevel <= 0f)
            {
                return "Overloaded.".Colorize(ColorLibrary.RedReadable);
            }
            if (need.CurLevelPercentage > 0.7f)
            {
                return "Its processes run clean.";
            }
            if (need.CurLevelPercentage > 0.3f)
            {
                return "Stray memories are piling up in its buffer.";
            }
            return "Its buffer is crowded. Old memories surface at odd moments, uninvited.";
        }

        /// <summary>Lore for the integrity tooltip if the gene's relabel is missing (say, a custom xenotype without it).</summary>
        public const string FallbackDescription = "A skeleton's mind runs on a CPU and a memory buffer that were never built to carry centuries. Everything it lives through leaves something behind in the buffer, and a soft cycle is how a skeleton clears it.";

        /// <summary>
        /// The integrity tooltip, replacing vanilla's deathrest one for skeletons: never vanilla's deathrest text, its
        /// countdown or its "comes from gene" line.
        /// </summary>
        public static string TipString(Need_Deathrest need, Pawn pawn)
        {
            NeedRelabel relabel = SkeletonNeeds.RelabelFor(pawn, need.def);
            string text = ("CPU integrity: " + need.CurLevelPercentage.ToStringPercent()).Colorize(ColoredText.TipSectionTitleColor) + "\n";
            text += StateLine(need, pawn);
            return text + "\n\n" + (relabel?.description ?? FallbackDescription);
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
        private static readonly HashSet<int> loggedGraceHeld = new HashSet<int>();
        private static readonly Dictionary<int, float> remainder = new Dictionary<int, float>();

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
            if (!bed.Powered && SoftCycleGravship.HoldsUnpoweredBed(pawn, bed.parent))
            {
                // Just landed (or still aboard): the ship's power nets come back a moment after the bed does.
                paused = true;
                if (loggedGraceHeld.Add(pawn.thingIDNumber))
                {
                    RimshiLog.Test("softcycle", "gravship power wait", pawn.LabelShort + "'s " + bed.parent.LabelShort + " has no power yet after a grav ship landing, cycle held");
                }
                return;
            }
            loggedGraceHeld.Remove(pawn.thingIDNumber);
            if (!bed.Powered)
            {
                paused = true;
                loggedCycle.Remove(pawn.thingIDNumber);
                SoftCycle.pendingCancelReason = bed.parent.LabelShort + " lost power";
                __instance.Wake();
                return;
            }
            // Vanilla adds delta after this; the extra makes the cycle run BedCycleSpeed times as fast, and faster with
            // engaged tuning forks. Their speeds aren't whole ticks, so the fraction carries over to the next tick.
            float speed = SoftCycle.BedCycleSpeed / SoftCycleFacilities.Engage(bed, pawn);
            int total = __instance.MinDeathrestTicks;
            remainder.TryGetValue(pawn.thingIDNumber, out float carried);
            float exact = delta * (speed - 1f) + carried;
            int extra = Mathf.FloorToInt(exact);
            remainder[pawn.thingIDNumber] = exact - extra;
            float before = Mathf.Clamp01((float)__instance.deathrestTicks / total);
            __instance.deathrestTicks += extra;
            float after = Mathf.Clamp01((float)(__instance.deathrestTicks + delta) / total);
            SoftCycle.Raise(pawn, before, after);
            if (pawn.IsHashIntervalTick(SoftCycleMending.Interval, delta))
            {
                SoftCycleMending.Step(pawn, SoftCycle.BedTicksLeft(__instance));
            }
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
                SoftCycleFacilities.ReleaseAll(pawn, "cycle finished", finished: true);
                return true;
            }
            SoftCycleFacilities.ReleaseAll(pawn, "cycle cut short: " + reason);
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
    /// rib cage (cycles raise it themselves), and at 0% overloads the CPU instead of vanilla's deathrest exhaustion. A downed
    /// skeleton at 0% begins a lying soft reset (StandingReset.TryAutoLying).
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
                StandingReset.TryAutoLying(pawn);
            }
            return false;
        }
    }

    /// <summary>The skeleton's integrity tooltip (vanilla's speaks of deathrest and exhaustion).</summary>
    [HarmonyPatch]
    public static class Patch_CpuIntegrityTip
    {
        internal static readonly AccessTools.FieldRef<Need, Pawn> NeedPawn = AccessTools.FieldRefAccess<Need, Pawn>("pawn");

        // Only where Need_Deathrest overrides it; otherwise Patch_CpuIntegrityTipBase catches it on Need.
        public static bool Prepare() => TargetMethod() != null;

        public static MethodBase TargetMethod() => AccessTools.DeclaredMethod(typeof(Need_Deathrest), nameof(Need.GetTipString));

        public static bool Prefix(Need __instance, ref string __result) => Swap(__instance, ref __result);

        /// <summary>
        /// Runs after every other mod's prefix and postfix: one that rebuilds the tooltip after ours (the 2026-11-01
        /// self-test still read vanilla's deathrest text) would otherwise put the deathrest text back.
        /// </summary>
        [HarmonyPriority(Priority.Last)]
        public static void Postfix(Need __instance, ref string __result) => Swap(__instance, ref __result);

        internal static bool Swap(Need need, ref string result)
        {
            Pawn pawn = NeedPawn(need);
            if (!(need is Need_Deathrest deathrest) || !SkeletonUtility.HasFrame(pawn))
            {
                return true;
            }
            result = SoftCycle.TipString(deathrest, pawn);
            return false;
        }
    }

    /// <summary>
    /// The same swap on the base Need.GetTipString, for game versions where Need_Deathrest has no override of its own
    /// (that base adds vanilla's "comes from gene: deathrest" line). Harmless where it does override: never reached then.
    /// </summary>
    [HarmonyPatch(typeof(Need), nameof(Need.GetTipString))]
    public static class Patch_CpuIntegrityTipBase
    {
        public static bool Prefix(Need __instance, ref string __result) => Patch_CpuIntegrityTip.Swap(__instance, ref __result);

        [HarmonyPriority(Priority.Last)]
        public static void Postfix(Need __instance, ref string __result) => Patch_CpuIntegrityTip.Swap(__instance, ref __result);
    }

    /// <summary>
    /// Skeletons get the soft cycle buttons in place of vanilla's wake and auto-wake (a finished cycle always ends by
    /// itself), and the CPU integrity gizmo in place of the deathrest one.
    /// </summary>
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
                if (skeleton && gizmo is GeneGizmo_DeathrestCapacity)
                {
                    // "Deathrest" and the deathrest building count become "CPU integrity" and the CPU's state
                    yield return GeneGizmo_Rimshi_CpuIntegrity.For(__instance);
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
