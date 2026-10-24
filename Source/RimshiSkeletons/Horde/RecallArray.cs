using RimWorld;
using Verse;

namespace RimshiSkeletons
{
    public class CompProperties_RecallArray : CompProperties_UseEffect
    {
        public int minDelayTicks = 2500;
        public int maxDelayTicks = 15000;

        public CompProperties_RecallArray()
        {
            compClass = typeof(CompUseEffect_RecallArray);
        }
    }

    /// <summary>
    /// The recall array's use effect. A mechanitor broadcasts the recall codes (the same requirement as Biotech's
    /// mechband antenna); the array burns out, and the horde is queued on RimshiGameComponent to drop in around it after
    /// a delay. Only one horde can be on its way at a time.
    /// </summary>
    public class CompUseEffect_RecallArray : CompUseEffect
    {
        public CompProperties_RecallArray Props => (CompProperties_RecallArray)props;

        public override AcceptanceReport CanBeUsedBy(Pawn p)
        {
            if (!MechanitorUtility.IsMechanitor(p))
            {
                return "Only a mechanitor can broadcast the recall codes.";
            }
            if (RimshiGameComponent.Instance != null && RimshiGameComponent.Instance.HordePending)
            {
                return "A skeleton legion is already answering an earlier broadcast.";
            }
            return base.CanBeUsedBy(p);
        }

        public override void DoEffect(Pawn usedBy)
        {
            base.DoEffect(usedBy);
            Map map = parent.Map;
            IntVec3 cell = parent.Position;
            int delay = Rand.RangeInclusive(Props.minDelayTicks, Props.maxDelayTicks);
            RimshiGameComponent.Instance.ScheduleHorde(map, cell, delay);

            // The antenna's own call effect, when Biotech still defines it.
            EffecterDef effecter = DefDatabase<ThingDef>.GetNamedSilentFail(HordeDefs.AntennaDefName)?.GetCompProperties<CompProperties_Useable_CallBossgroup>()?.effecterDef;
            effecter?.Spawn(cell, map).Cleanup();
            FleckMaker.ThrowLightningGlow(parent.DrawPos, map, 3f);

            Find.LetterStack.ReceiveLetter("Recall codes broadcast",
                usedBy.LabelShort + " broadcast the old empire's recall codes and the array burned out. Something answered.\n\n"
                + "A skeleton legion will drop in around the array within a few hours. Its protocols are corrupted: every skeleton in it is berserk. "
                + "Skeletons with a failsafe core usually go down intact instead of being destroyed; capture them and interrogate them to reprogram them for the colony.",
                LetterDefOf.ThreatSmall, new TargetInfo(cell, map));
            RimshiLog.Test("horde", "array used", usedBy.LabelShort + " at " + cell + "; horde due in " + delay.ToStringTicksToPeriod() + ", broadcast " + (RimshiGameComponent.Instance.Summons + 1) + " in this game");

            for (int i = 0; i < 2; i++)
            {
                GenPlace.TryPlaceThing(ThingMaker.MakeThing(ThingDefOf.ChunkSlagSteel), cell, map, ThingPlaceMode.Near);
            }
            parent.Destroy(DestroyMode.Vanish);
        }
    }
}
