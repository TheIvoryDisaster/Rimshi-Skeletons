using HarmonyLib;
using Verse;

namespace RimshiSkeletons
{
    public class RimshiMod : Mod
    {
        public static RimshiMod Instance;

        public RimshiMod(ModContentPack content) : base(content)
        {
            Instance = this;
            new Harmony("Matthew.RimshiSkeletons").PatchAll();
        }
    }
}
