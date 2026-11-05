using MCM.Abstractions.Base.Global;
using System.Linq;
using MCM.Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features;
using TAOM.Features.CrashReport.Collectors;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.CrashReport;

[TestClass]
public class McmSettingsCollectorTests
{
    [TestMethod]
    public void Dropdown_RendersTheSelectedValue_NotTheTypeName()
    {
        var dropdown = new Dropdown<string>(new[] { "Never", "Taken-over lords only", "Always" }, 2);

        Assert.AreEqual("Always", McmSettingsCollector.StringifyValue(dropdown));
    }

    [TestMethod]
    public void Dropdown_WithAnOutOfRangeSelection_DoesNotThrow()
    {
        var dropdown = new Dropdown<string>(new[] { "A", "B" }, 0) { SelectedIndex = 5 };

        StringAssert.StartsWith(McmSettingsCollector.StringifyValue(dropdown), "(");
    }

    // A third-party class (Diplomacy.Settings) declares no Instance. Reading the inherited GlobalSettings<T>.Instance
    // would run that mod's constructor inside crash handling, so the class is skipped by design (#982).
    [TestMethod]
    public void ResolveInstance_ClassWithoutItsOwnInstance_IsSkipped_AndRunsNoThirdPartyCode()
    {
        using var mcm = new CountingMcm();
        mcm.Register(new InheritingSettings());

        InheritingSettings.Constructed = false;
        var resolved = McmSettingsCollector.ResolveInstance(typeof(InheritingSettings));

        Assert.IsNull(resolved);
        Assert.IsFalse(InheritingSettings.Constructed, "the inherited MCM getter (and so new T()) must not run");
    }

    [TestMethod]
    public void ExtractSettingEntries_DoesNotListMcmDiscoveryType()
    {
        using var mcm = new CountingMcm();
        var settings = new TaomSettings();
        mcm.Register(settings);

        var entries = McmSettingsCollector.ExtractSettingEntries(settings, typeof(TaomSettings));

        Assert.IsTrue(entries.Count > 0);
        Assert.IsFalse(entries.Any(e => e.PropertyPath == "DiscoveryType"));
    }

    [TestMethod]
    public void ResolveInstance_ClassThatHidesInstance_TakesTheHide_NotTheBase()
    {
        // ResolveInstance is the DeclaredOnly second guard, not the ownership gate: Collect() skips
        // foreign assemblies before it gets here.
        using var mcm = new CountingMcm();
        mcm.Register(new HidingSettings());

        Assert.AreSame(HidingSettings.Known, McmSettingsCollector.ResolveInstance(typeof(HidingSettings)));
    }

    [TestMethod]
    public void ResolveInstance_TaomSettings_ResolvesTheHide_WithoutThrowing()
    {
        using var mcm = new CountingMcm();
        var settings = new TaomSettings();
        mcm.Register(settings);

        Assert.AreSame(settings, McmSettingsCollector.ResolveInstance(typeof(TaomSettings)));
    }

    // TAOM.Tests is a foreign assembly here. DeclaredOnly alone let a class that declares its own Instance
    // through, so its code ran in crash handling (#982 finding 8). The assembly check must skip it unread.
    [TestMethod]
    public void Collect_SkipsAForeignClassThatDeclaresItsOwnInstance_AndRunsNoCode()
    {
        using var mcm = new CountingMcm();
        mcm.Register(new TaomSettings());
        mcm.Register(new ForeignDeclaringSettings());

        // Touch the class first so its static constructor has already run; only the flags below are meaningful.
        // StaticInit therefore proves nothing on its own and is not asserted. Instance and the constructor are.
        ForeignDeclaringSettings.InstanceRead = false;
        ForeignDeclaringSettings.Constructed = false;
        var snapshot = new McmSettingsCollector().Collect();

        Assert.IsFalse(snapshot.Providers.Any(p => p.ProviderId == "Taom.Test.Foreign"), "a foreign class must not be listed");
        Assert.IsFalse(ForeignDeclaringSettings.InstanceRead, "the foreign Instance getter must not run");
        Assert.IsFalse(ForeignDeclaringSettings.Constructed, "the foreign constructor must not run");
    }

    [TestMethod]
    public void Collect_ListsTaomSettings()
    {
        using var mcm = new CountingMcm();
        mcm.Register(new TaomSettings());

        var snapshot = new McmSettingsCollector().Collect();

        var taom = snapshot.Providers.SingleOrDefault(p => p.ProviderId == "TAOM");
        Assert.IsNotNull(taom);
        Assert.IsTrue(taom.Settings.Count > 0);
    }

    private sealed class ForeignDeclaringSettings : AttributeGlobalSettings<ForeignDeclaringSettings>
    {
        public static bool InstanceRead;
        public static bool Constructed;
        public static bool StaticInit;
        private static readonly ForeignDeclaringSettings Known;

        static ForeignDeclaringSettings() { StaticInit = true; Known = new ForeignDeclaringSettings(); }

        public ForeignDeclaringSettings() { Constructed = true; }

        public static new ForeignDeclaringSettings? Instance { get { InstanceRead = true; return Known; } }

        public override string Id => "Taom.Test.Foreign";
        public override string DisplayName => "Foreign";
        public override string FolderName => "Taom.Test.Foreign";
        public override string FormatType => "json2";
    }

    private sealed class InheritingSettings: AttributeGlobalSettings<InheritingSettings>
    {
        public static bool Constructed;

        public InheritingSettings() { Constructed = true; }

        public override string Id => "Taom.Test.Inheriting";
        public override string DisplayName => "Inheriting";
        public override string FolderName => "Taom.Test.Inheriting";
        public override string FormatType => "json2";
    }

    private sealed class HidingSettings : AttributeGlobalSettings<HidingSettings>
    {
        public static readonly HidingSettings Known = new();
        public static new HidingSettings? Instance => Known;

        public override string Id => "Taom.Test.Hiding";
        public override string DisplayName => "Hiding";
        public override string FolderName => "Taom.Test.Hiding";
        public override string FormatType => "json2";
    }
}
