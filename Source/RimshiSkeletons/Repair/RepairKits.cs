using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace RimshiSkeletons
{
    /// <summary>
    /// Repair kits are the only medicine that works on skeletons, and they work on nothing else
    /// (docs/design/kenshi-lore-and-repair.md, section 2). Every tend job (work givers, drafted tend, fetching more
    /// medicine mid-job) picks its medicine through HealthAIUtility.FindBestMedicine, whose validator asks
    /// MedicalCareUtility.AllowsMedicine; that has no patient, so FindBestMedicine notes the patient for it.
    /// </summary>
    public static class RepairKits
    {
        public static bool IsKit(ThingDef def) => def != null && def == RimshiDefOf.Rimshi_RepairKit;

        /// <summary>True when this medicine may be used on this patient: kits on skeletons, other medicine on everyone else.</summary>
        public static bool Suits(Pawn patient, ThingDef medicine) => IsKit(medicine) == SkeletonUtility.HasFrame(patient);

        private static readonly HashSet<string> logged = new HashSet<string>();

        public static void LogSkipped(Pawn patient, ThingDef medicine)
        {
            if (logged.Count < 200 && logged.Add(patient.ThingID + medicine.defName))
            {
                RimshiLog.Test("kit", "filter", "skipped " + medicine.defName + " for " + patient.LabelShort + (SkeletonUtility.HasFrame(patient) ? " (skeleton)" : " (not a skeleton)"));
            }
        }
    }

    [HarmonyPatch(typeof(HealthAIUtility), nameof(HealthAIUtility.FindBestMedicine))]
    public static class Patch_FindBestMedicine
    {
        public static Pawn Patient;

        public static void Prefix(Pawn patient)
        {
            Patient = patient;
        }

        /// <summary>With no player settings and onlyUseInventory, vanilla skips AllowsMedicine, so the result is checked too.</summary>
        public static void Postfix(Pawn patient, ref Thing __result)
        {
            if (__result != null && !RepairKits.Suits(patient, __result.def))
            {
                RepairKits.LogSkipped(patient, __result.def);
                __result = null;
            }
        }

        public static void Finalizer()
        {
            Patient = null;
        }
    }

    [HarmonyPatch(typeof(MedicalCareUtility), nameof(MedicalCareUtility.AllowsMedicine))]
    public static class Patch_AllowsMedicine
    {
        public static void Postfix(ThingDef meds, ref bool __result)
        {
            Pawn patient = Patch_FindBestMedicine.Patient;
            if (__result && patient != null && !RepairKits.Suits(patient, meds))
            {
                RepairKits.LogSkipped(patient, meds);
                __result = false;
            }
        }
    }
}
