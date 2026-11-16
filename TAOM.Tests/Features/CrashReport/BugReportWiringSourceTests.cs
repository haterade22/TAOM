using System.IO;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.CrashReport.BugReport;
using TAOM.Features.CrashReport.UI;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.CrashReport;

/// <summary>
/// Wiring of the player's bug report: the entry points call the tested pieces (BugReportFiles, BugReportSources,
/// BugReportStartup). Source pins, comments stripped, each read from the one method that must hold the call.
/// </summary>
[TestClass]
public class BugReportWiringSourceTests
{
    private static string Method(string path, string signaturePattern)
    {
        var src = RepoPaths.ReadSource(path, stripComments: true);
        var body = Regex.Match(src, signaturePattern + @"[\s\S]*?\n    \}").Value;
        Assert.IsTrue(body.Length > 0, $"{signaturePattern} not found in {path}");
        return body;
    }

    [TestMethod]
    public void OnSubModuleUnloaded_WritesTheLineTheNextStartLooksFor()
    {
        var body = Method("Main/SubModule.cs", @"protected override void OnSubModuleUnloaded\(\)");

        StringAssert.Contains(body, $"LogInfo(\"{BugReportFiles.CleanExitLine}\")");
    }

    [TestMethod]
    public void MainMenu_ChecksThePreviousSession_AndAStallNoticeUsesTheTurn()
    {
        var src = RepoPaths.ReadSource("Main/SubModule.cs", stripComments: true);

        // #1297: the offer has a try of its own, so a throw in the stall marker resolve does not skip it for the launch.
        StringAssert.Matches(src, new Regex(
            @"if \(stale != null\)\s*\{\s*stallShown = true;\s*Features\.BattleLoadDiagnostics\.StallReportNotifier\.Notify\(stale\);\s*\}\s*\}\s*catch[^\n]*\n\s*" +
            @"try\s*\{\s*Features\.CrashReport\.BugReport\.BugReportStartup\.CheckPreviousSession\(\s*IoC\.Resolve<IModLogger>\(\),\s*stallNoticeShown: stallShown\);\s*\}"));
    }

    [TestMethod]
    public void CrashReportSettings_MakeBugReportButton_RunsTheReport()
    {
        var src = RepoPaths.ReadSource("Main/Features/CrashReport/CrashReportSettings.cs", stripComments: true);

        StringAssert.Matches(src, new Regex(
            @"\[SettingPropertyButton\(""(\{=[A-Za-z0-9_]+\})?Make Bug Report""[\s\S]*?public System\.Action MakeBugReportAction \{ get; set; \} = static \(\) => (BugReport\.)?BugReportStartup\.MakeAndShow\("));
    }

    [TestMethod]
    public void WriteBugReport_CollectsTheSourcesAndWritesTheReportZip()
    {
        var body = Method("Main/Features/CrashReport/CrashReportService.cs", @"public string\? WriteBugReport\(");

        var collect = body.IndexOf("BugReportSources.Collect(");
        var write = body.IndexOf("_bundleWriter.WriteReport(");
        Assert.IsTrue(collect > 0 && write > collect, body);
    }

    [TestMethod]
    public void LogTailCollector_TakesOnlyTheMainRglLog()
    {
        var body = Method("Main/Features/CrashReport/Collectors/LogTailCollector.cs", @"private static string\? FindLatestRglLog\(\)");

        StringAssert.Contains(body, "BugReportFiles.IsMainRglLog(");
    }

    [TestMethod]
    public void CrashNotifier_RelativeLogPath_IsShownAbsolute()
    {
        var relative = Path.Combine("Logs", "taom_debug_2026-11-13_23-12-05.log");

        var message = CrashNotifier.BuildMessage("NullReferenceException", null, relative);

        StringAssert.Contains(message, Path.GetFullPath(relative));
    }
}
