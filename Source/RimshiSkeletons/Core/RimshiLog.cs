using System;
using System.IO;
using Verse;

namespace RimshiSkeletons
{
    /// <summary>
    /// Test logging (same shape as Tithe's). Every test-worthy event writes one line that the test checklist reads back
    /// from Player.log:
    ///   [Rimshi][T] area | key | detail
    /// area groups the checklist (build, spawn, gene, need, surgery, compat...); key is usually a def name.
    /// A failure writes the same line as a warning with "FAILED:" at the start of the detail.
    /// </summary>
    public static class RimshiLog
    {
        public const string Prefix = "[Rimshi][T] ";

        public static void Test(string area, string key, string detail = "")
        {
            Log.Message(Prefix + area + " | " + key + " | " + Clean(detail));
        }

        public static void Fail(string area, string key, string detail)
        {
            Log.Warning(Prefix + area + " | " + key + " | FAILED: " + Clean(detail));
        }

        private static string Clean(string detail) => (detail ?? "").Replace("\n", " / ").Replace("|", "/");
    }

    /// <summary>Logs which build is running once defs are loaded, so every log can be matched to a build.</summary>
    [StaticConstructorOnStartup]
    public static class RimshiBuild
    {
        public const string Version = BuildInfo.Version;

        static RimshiBuild()
        {
            string stamp = "?";
            try
            {
                stamp = File.GetLastWriteTimeUtc(typeof(RimshiBuild).Assembly.Location).ToString("yyyy-MM-dd HH:mm") + " UTC";
            }
            catch (Exception)
            {
            }
            RimshiLog.Test("build", Version, "version " + Version + ", build " + BuildInfo.Number + " (" + BuildInfo.BuiltBy + "), dll written " + stamp
                + "; biotech " + ModsConfig.BiotechActive + ", royalty " + ModsConfig.RoyaltyActive + ", ideology " + ModsConfig.IdeologyActive
                + ", odyssey " + ModsConfig.OdysseyActive);
        }
    }
}
