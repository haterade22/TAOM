using System.Linq;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Composition;
using TAOM.Core.Logging;
using TAOM.Features;
using TAOM.Features.BattleLoadDiagnostics;
using TAOM.Features.BlowDiagnostics;
using TAOM.Features.CoopInterop;
using TAOM.Features.CrashReport;
using TAOM.Features.ServerSettings;
using TAOM.Tests.Features.LoadTimeStamps;

namespace TAOM.Tests.Features.ServerSettings;

[TestClass]
public class ServerSettingsModuleTests
{
    private readonly ServerSettingsModule _sut = new ServerSettingsModule();
    private FakeServerSettingsFileStore _store = null!;
    private RecordingLogger _log = null!;
    private Container _container = null!;

    [TestInitialize]
    public void Setup()
    {
        ServerSettingsSource.ResetForTests();
        _store = FakeServerSettingsFileStore.WithText("{ \"CastleNotablesPerCastle\": 5, \"AiLordPartyScale\": 99 }");
        ServerSettingsSource.Store = _store;
        _log = new RecordingLogger();
        _container = new Container();
        _container.RegisterInstance<IModLogger>(_log);
    }

    [TestCleanup]
    public void Cleanup()
    {
        ServerSettingsSource.ResetForTests();
        _container.Dispose();
    }

    [TestMethod]
    public void Module_IsListedOnce()
    {
        Assert.AreEqual(1, FeatureModules.All.Count(m => m is ServerSettingsModule));
        Assert.AreEqual("ServerSettings", _sut.Id);
        Assert.IsNull(_sut.ParkedReason);
    }

    [TestMethod]
    public void Module_OnClient_LogsAndWritesNothing()
    {
        ServerSettingsSource.IsDedicatedServer = () => false;

        _sut.OnPhase(ApplyPhase.ProcessLoad, _container);

        Assert.AreEqual(0, _log.Lines.Count);
        Assert.AreEqual(0, _store.ReadCount);
        Assert.AreEqual(0, _store.MissingWrites.Count + _store.ChangedWrites.Count);
    }

    [TestMethod]
    public void Module_OnOtherPhases_DoesNothing()
    {
        ServerSettingsSource.IsDedicatedServer = () => true;

        _sut.OnPhase(ApplyPhase.MainMenu, _container);

        Assert.AreEqual(0, _store.ReadCount);
        Assert.AreEqual(0, _log.Lines.Count);
    }

    [TestMethod]
    public void Module_OnServer_LogsBootLine()
    {
        ServerSettingsSource.IsDedicatedServer = () => true;

        _sut.OnPhase(ApplyPhase.ProcessLoad, _container);

        var expected = "INFO [ServerSettings] TAOM server settings (MCM is not loaded on a dedicated server): loaded 1 key(s) from "
                       + _store.Where.Path + " (BANNERLORD_USER_DIR), 1 reverted, 0 unknown";
        Assert.IsTrue(_log.Lines.Contains(expected), string.Join("\n", _log.Lines));
        Assert.IsTrue(_log.Lines.Any(l => l.StartsWith("WARN ") && l.Contains("AiLordPartyScale")));
    }

    [TestMethod]
    public void Module_OnServer_LoaderThrow_BootLineSaysNoInstanceIsServed()
    {
        ServerSettingsSource.IsDedicatedServer = () => true;
        _store.OnTryRead = () => throw new System.InvalidOperationException("boom");

        _sut.OnPhase(ApplyPhase.ProcessLoad, _container);

        var expected = "INFO [ServerSettings] TAOM server settings (MCM is not loaded on a dedicated server): could not load the server settings from "
                       + _store.Where.Path + " (BANNERLORD_USER_DIR), no settings instance is served, so each provider's own fallback applies";
        Assert.IsTrue(_log.Lines.Contains(expected), string.Join("\n", _log.Lines));
        Assert.IsFalse(_log.Lines.Any(l => l.Contains("server default") && l.StartsWith("INFO ")), string.Join("\n", _log.Lines));
    }

    [TestMethod]
    public void Module_OnServer_WritesTheDefaultsReferenceEveryBoot_AndNoTemplateWhenTheFileExists()
    {
        ServerSettingsSource.IsDedicatedServer = () => true;

        _sut.OnPhase(ApplyPhase.ProcessLoad, _container);

        Assert.AreEqual(0, _store.MissingWrites.Count);
        Assert.AreEqual(1, _store.ChangedWrites.Count);
        Assert.AreEqual(_store.Where.DefaultsPath, _store.ChangedWrites[0].Path);
        Assert.AreEqual(ServerSettingsDefaultsRenderer.DefaultsReference(new ServerSettingsSet()), _store.ChangedWrites[0].Content);
    }

    [TestMethod]
    public void Module_OnServer_MissingFile_WritesTheTemplate_AndSaysSo()
    {
        ServerSettingsSource.IsDedicatedServer = () => true;
        _store.Read = new ServerFileRead(ServerFileState.Missing, null, null);

        _sut.OnPhase(ApplyPhase.ProcessLoad, _container);

        Assert.AreEqual(1, _store.MissingWrites.Count);
        Assert.AreEqual(_store.Where.Path, _store.MissingWrites[0].Path);
        Assert.AreEqual(ServerSettingsDefaultsRenderer.Template(), _store.MissingWrites[0].Content);
        Assert.IsTrue(_log.Lines.Any(l => l.StartsWith("INFO [ServerSettings]") && l.Contains("no file at " + _store.Where.Path)), string.Join("\n", _log.Lines));
    }

    [TestMethod]
    public void Module_OnServer_WriteFailure_WarnsAndContinues()
    {
        ServerSettingsSource.IsDedicatedServer = () => true;
        _store.WriteError = "disk full";

        _sut.OnPhase(ApplyPhase.ProcessLoad, _container);

        Assert.IsTrue(_log.Lines.Any(l => l.StartsWith("WARN ") && l.Contains("disk full")), string.Join("\n", _log.Lines));
        Assert.IsTrue(_log.Lines.Any(l => l.Contains("loaded 1 key(s)")));
    }

    [TestMethod]
    public void TaomSettingsPages_OnServer_ListsEveryPageOnce()
    {
        ServerSettingsSource.IsDedicatedServer = () => true;
        _store.Read = new ServerFileRead(ServerFileState.Loaded, "{}", null);

        var pages = TaomSettingsPages.All();

        CollectionAssert.AreEquivalent(
            new[] { typeof(TaomSettings), typeof(BattleLoadDiagnosticsSettings), typeof(BlowDiagnosticsSettings), typeof(CrashReportSettings) },
            pages.Select(p => p!.GetType()).ToArray());
        Assert.AreEqual(0, SettingsFingerprint.ComputeAcross(pages!).Unavailable);
    }

    [TestMethod]
    public void Fingerprint_OnServer_HashesFileValues()
    {
        // EnableTroopWeight is simulation-relevant; the fingerprint reads X.Instance, so the file value must reach the hash.
        var property = typeof(TaomSettings).GetProperty(nameof(TaomSettings.EnableTroopWeight))!;
        Assert.IsTrue(CoopSettingsRelevance.IsSimulationRelevant(property), "pick a setting that is in the fingerprint");

        string Hash() => SettingsFingerprint.ComputeAcross(
            TaomSettings.Instance!, BattleLoadDiagnosticsSettings.Instance!,
            BlowDiagnosticsSettings.Instance!, CrashReportSettings.Instance!).Global;

        ServerSettingsSource.IsDedicatedServer = () => true;
        _store.Read = new ServerFileRead(ServerFileState.Loaded, "{}", null);
        var defaults = Hash();

        ServerSettingsSource.ResetForTests();
        ServerSettingsSource.IsDedicatedServer = () => true;
        ServerSettingsSource.Store = FakeServerSettingsFileStore.WithText("{ \"EnableTroopWeight\": false }");
        var fromFile = Hash();

        Assert.AreNotEqual(defaults, fromFile);
    }
}
