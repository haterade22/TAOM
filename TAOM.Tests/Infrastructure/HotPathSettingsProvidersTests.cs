using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DryIoc;
using MCM.Abstractions;
using MCM.Abstractions.Base;
using MCM.Abstractions.Base.Global;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features;
using TAOM.Features.BattleLoadDiagnostics;
using TAOM.Features.BlowDiagnostics;
using TAOM.Features.CombatMechanics;
using TAOM.Features.CompanionTactics;
using TAOM.Features.CreatureBandits;
using TAOM.Features.CultureDoctrine;
using TAOM.Features.DreadAura;
using TAOM.Features.DreadAura.Domain;
using TAOM.Features.Elephant;
using TAOM.Features.MixedFormations;
using TAOM.Features.RaceAbilities;
using TAOM.Features.Refuge;
using TAOM.Features.SiegePropDiagnostics;
using TAOM.Features.SignatureStrikes;
using TAOM.Features.SignatureStrikes.Domain;
using TAOM.Features.SmartCavalryAI;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Infrastructure;

/// <summary>
/// Settings providers read on a hot path (per blow, per frame, per agent update) take the MCM settings
/// reference once, in a private lazy <c>Settings</c> accessor, and read through it; no other member may
/// resolve MCM's <c>Instance</c>, which walks MCM's settings containers on every call. The accessor caches
/// only a non-null instance, so a resolve before MCM is up cannot pin the defaults. Pattern:
/// BattleBalanceSettingsProvider (02157b18). The IL rule proves where MCM is resolved; the counting test
/// proves the result is kept, by running each provider against a stand-in for MCM's own provider.
/// </summary>
[TestClass]
public class HotPathSettingsProvidersTests
{
    private const BindingFlags Declared = BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic
                                          | BindingFlags.Instance | BindingFlags.Static;

    // MCM declares Instance on a generic base (GlobalSettings<T>), so match any declaring type the
    // settings class derives from.
    private static bool IsInstanceGetter(MethodBase m) =>
        m.Name == "get_Instance" && m.DeclaringType != null
        && (m.DeclaringType.IsAssignableFrom(typeof(TaomSettings))
            || m.DeclaringType.IsAssignableFrom(typeof(BlowDiagnosticsSettings)));

    private static bool CallsInstance(MethodBase m)
    {
        var il = m.GetMethodBody()?.GetILAsByteArray();
        return il != null && IlCallScanner.ExtractCalledMethods(m, il).Any(IsInstanceGetter);
    }

    [DataTestMethod]
    [DataRow(typeof(CombatMechanicsSettingsProvider))]
    [DataRow(typeof(BlowDiagnosticsSettingsProvider))]
    [DataRow(typeof(MixedFormationsSettingsProvider))]
    [DataRow(typeof(CompanionTacticsSettingsProvider))]
    [DataRow(typeof(DreadAuraSettingsProvider))]
    [DataRow(typeof(HowdahDiagnosticsSettingsProvider))]
    [DataRow(typeof(SmartCavalryAISettingsProvider))]
    [DataRow(typeof(CultureDoctrineSettingsProvider))]
    [DataRow(typeof(SiegePropDiagnosticsSettingsProvider))]
    [DataRow(typeof(RaceAbilitySettingsProvider))]
    [DataRow(typeof(RefugeSettingsProvider))]
    [DataRow(typeof(SignatureStrikesSettingsProvider))]
    // #746: static, read per hit against a creature bandit. It has no instance to construct, so the counting
    // test below cannot take it: CreatureBanditTuningLiveTests runs the same CountingMcm check on it.
    [DataRow(typeof(CreatureBanditTuning))]
    public void OnlyTheLazySettingsAccessor_ReadsTheMcmInstance(Type provider)
    {
        var accessor = provider.GetProperty("Settings", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)?.GetGetMethod(true);
        Assert.IsNotNull(accessor, provider.Name + ": the private lazy Settings accessor");
        Assert.IsTrue(CallsInstance(accessor), provider.Name + ": the lazy accessor takes the settings reference");

        var offenders = provider.GetMethods(Declared).Cast<MethodBase>()
            .Concat(provider.GetConstructors(Declared))
            .Where(m => m.Name != "get_Settings" && CallsInstance(m))
            .Select(m => m.Name)
            .ToList();
        Assert.AreEqual(0, offenders.Count,
            provider.Name + " resolves the MCM instance outside the lazy accessor: " + string.Join(", ", offenders));
    }

    // Caching the settings reference is safe only because MCM keeps ONE object per global settings id
    // for the whole process and copies resets, presets and menu edits into it. Per-save and
    // per-campaign settings get a new object on every game start or load, so a cached reference
    // would keep the first campaign's values. Changing either base class must fail here first.
    [DataTestMethod]
    [DataRow(typeof(TaomSettings))]
    [DataRow(typeof(BlowDiagnosticsSettings))]
    [DataRow(typeof(BattleLoadDiagnosticsSettings))]
    public void CachedSettingsClasses_AreMcmGlobalSettings(Type settings)
    {
        Assert.IsTrue(typeof(GlobalSettings).IsAssignableFrom(settings),
            settings.Name + " is no longer an MCM global setting: the cached providers would keep a stale object");
    }

    // A second (internal, test-only) constructor must not break DryIoc's single-public-constructor
    // selection at registration or resolve.
    [DataTestMethod]
    [DataRow(typeof(ICombatMechanicsSettingsProvider), typeof(CombatMechanicsSettingsProvider))]
    [DataRow(typeof(IBlowDiagnosticsSettingsProvider), typeof(BlowDiagnosticsSettingsProvider))]
    [DataRow(typeof(IMixedFormationsSettingsProvider), typeof(MixedFormationsSettingsProvider))]
    [DataRow(typeof(ICompanionTacticsSettingsProvider), typeof(CompanionTacticsSettingsProvider))]
    [DataRow(typeof(IDreadAuraSettingsProvider), typeof(DreadAuraSettingsProvider))]
    [DataRow(typeof(IHowdahDiagnosticsSettingsProvider), typeof(HowdahDiagnosticsSettingsProvider))]
    [DataRow(typeof(ISmartCavalryAISettingsProvider), typeof(SmartCavalryAISettingsProvider))]
    [DataRow(typeof(ICultureDoctrineSettingsProvider), typeof(CultureDoctrineSettingsProvider))]
    [DataRow(typeof(ISiegePropDiagnosticsSettingsProvider), typeof(SiegePropDiagnosticsSettingsProvider))]
    [DataRow(typeof(RaceAbilitySettingsProvider), typeof(RaceAbilitySettingsProvider))]
    [DataRow(typeof(IRefugeSettingsProvider), typeof(RefugeSettingsProvider))]
    [DataRow(typeof(ISignatureStrikesSettingsProvider), typeof(SignatureStrikesSettingsProvider))]
    public void Provider_ResolvesFromARealContainer(Type service, Type implementation)
    {
        using var container = new Container();
        foreach (var parameter in implementation.GetConstructors().Single().GetParameters())
            container.RegisterInstance(parameter.ParameterType,
                Substitute.For(new[] { parameter.ParameterType }, new object[0]));
        container.Register(service, implementation, Reuse.Singleton);

        Assert.IsInstanceOfType(container.Resolve(service), implementation);
    }

    private static ICombatMechanicsConfigProvider CombatConfig()
    {
        var config = Substitute.For<ICombatMechanicsConfigProvider>();
        config.GetConfig().Returns(new CombatMechanicsConfig());
        return config;
    }

    private static ISignatureStrikesConfigProvider SignatureConfig()
    {
        var config = Substitute.For<ISignatureStrikesConfigProvider>();
        config.GetConfig().Returns(new SignatureStrikesConfig());
        return config;
    }

    private static IDreadAuraConfigProvider DreadConfig()
    {
        var config = Substitute.For<IDreadAuraConfigProvider>();
        config.GetConfig().Returns(new DreadAuraConfig());
        return config;
    }

    // What each provider is probed with: how to build it as production does (its public constructor),
    // one bool getter that reads a single MCM setting, that setting, and the settings class MCM holds.
    private static readonly Dictionary<Type, (Func<object> Create, string Getter, string Setting, Func<BaseSettings> NewSettings)> Probes = new()
    {
        [typeof(CombatMechanicsSettingsProvider)] = (() => new CombatMechanicsSettingsProvider(CombatConfig()),
            nameof(ICombatMechanicsSettingsProvider.SkillCrushThroughEnabled), nameof(TaomSettings.EnableSkillCrushThrough), () => new TaomSettings()),
        [typeof(BlowDiagnosticsSettingsProvider)] = (() => new BlowDiagnosticsSettingsProvider(),
            nameof(IBlowDiagnosticsSettingsProvider.IsEnabled), nameof(BlowDiagnosticsSettings.EnableBlowDiagnostics), () => new BlowDiagnosticsSettings()),
        [typeof(MixedFormationsSettingsProvider)] = (() => new MixedFormationsSettingsProvider(),
            nameof(IMixedFormationsSettingsProvider.IsEnabled), nameof(TaomSettings.EnableMixedFormations), () => new TaomSettings()),
        [typeof(CompanionTacticsSettingsProvider)] = (() => new CompanionTacticsSettingsProvider(),
            nameof(ICompanionTacticsSettingsProvider.EnableCompanionRoleTooltips), nameof(TaomSettings.EnableCompanionRoleTooltips), () => new TaomSettings()),
        [typeof(DreadAuraSettingsProvider)] = (() => new DreadAuraSettingsProvider(DreadConfig()),
            nameof(IDreadAuraSettingsProvider.IsEnabled), nameof(TaomSettings.EnableDreadAura), () => new TaomSettings()),
        [typeof(HowdahDiagnosticsSettingsProvider)] = (() => new HowdahDiagnosticsSettingsProvider(),
            nameof(IHowdahDiagnosticsSettingsProvider.IsEnabled), nameof(TaomSettings.EnableHowdahDiagnostics), () => new TaomSettings()),
        [typeof(SmartCavalryAISettingsProvider)] = (() => new SmartCavalryAISettingsProvider(),
            nameof(ISmartCavalryAISettingsProvider.IsEnabled), nameof(TaomSettings.EnableSmartCavalryAI), () => new TaomSettings()),
        [typeof(CultureDoctrineSettingsProvider)] = (() => new CultureDoctrineSettingsProvider(Substitute.For<ICultureDoctrineConfigProvider>()),
            nameof(ICultureDoctrineSettingsProvider.IsDebug), nameof(TaomSettings.CultureDoctrineDebug), () => new TaomSettings()),
        [typeof(SiegePropDiagnosticsSettingsProvider)] = (() => new SiegePropDiagnosticsSettingsProvider(),
            nameof(ISiegePropDiagnosticsSettingsProvider.IsEnabled), nameof(TaomSettings.EnableSiegePropDiagnostics), () => new TaomSettings()),
        [typeof(RaceAbilitySettingsProvider)] = (() => new RaceAbilitySettingsProvider(),
            nameof(RaceAbilitySettingsProvider.Enabled), nameof(TaomSettings.EnableRaceAbilities), () => new TaomSettings()),
        // #745: read per hit on a refuge's defenders, real-time and auto-resolve.
        [typeof(RefugeSettingsProvider)] = (() => new RefugeSettingsProvider(),
            nameof(IRefugeSettingsProvider.Enabled), nameof(TaomSettings.EnableRefuges), () => new TaomSettings()),
        // #746: read on every melee hit a signature hero lands.
        [typeof(SignatureStrikesSettingsProvider)] = (() => new SignatureStrikesSettingsProvider(SignatureConfig()),
            nameof(ISignatureStrikesSettingsProvider.IsEnabled), nameof(TaomSettings.EnableSignatureStrikes), () => new TaomSettings()),
    };

    // The IL rule above proves WHERE MCM is resolved, not THAT the result is kept: an accessor written
    // `_settings ?? Instance` passes it and walks MCM's containers on every read, and one that reads
    // MCM once and keeps a null would pin the defaults. This runs each provider, built through its
    // public constructor, against a counting stand-in for MCM's own provider (the object every
    // GlobalSettings<T>.Instance asks): it must keep asking while MCM has nothing, take the settings
    // object the moment MCM has one, never ask again, and still see edits made to that object.
    [DataTestMethod]
    [DataRow(typeof(CombatMechanicsSettingsProvider))]
    [DataRow(typeof(BlowDiagnosticsSettingsProvider))]
    [DataRow(typeof(MixedFormationsSettingsProvider))]
    [DataRow(typeof(CompanionTacticsSettingsProvider))]
    [DataRow(typeof(DreadAuraSettingsProvider))]
    [DataRow(typeof(HowdahDiagnosticsSettingsProvider))]
    [DataRow(typeof(SmartCavalryAISettingsProvider))]
    [DataRow(typeof(CultureDoctrineSettingsProvider))]
    [DataRow(typeof(SiegePropDiagnosticsSettingsProvider))]
    [DataRow(typeof(RaceAbilitySettingsProvider))]
    [DataRow(typeof(RefugeSettingsProvider))]
    [DataRow(typeof(SignatureStrikesSettingsProvider))]
    public void Provider_AsksMcmUntilItHasTheSettings_ThenNeverAgain_AndReadsThrough(Type provider)
    {
        var probe = Probes[provider];
        using var mcm = new CountingMcm();
        var sut = probe.Create();
        var getter = provider.GetProperty(probe.Getter)!;
        bool Read() => (bool)getter.GetValue(sut)!;

        // MCM is not up: the compiled fallback applies, and every read asks again.
        bool fallback = Read();
        Assert.IsTrue(mcm.Lookups >= 1, "a read resolves the settings through MCM");
        int whileDown = mcm.Lookups;
        Read();
        Assert.IsTrue(mcm.Lookups > whileDown,
            provider.Name + ": a read while MCM has nothing must ask again, or a null is pinned for the session");

        // MCM comes up holding a value that differs from the fallback: the next read takes it.
        var settings = probe.NewSettings();
        SetSetting(settings, probe.Setting, !fallback);
        mcm.Register(settings);
        Assert.AreEqual(!fallback, Read(), provider.Name + ": the first read after MCM is up must use its settings, not the fallback");
        int resolved = mcm.Lookups;

        // Kept: further reads never ask MCM.
        for (int i = 0; i < 5; i++)
            Read();
        Assert.AreEqual(resolved, mcm.Lookups,
            provider.Name + ": once resolved, the settings object must be kept; every read walks MCM's containers otherwise");

        // Read through: an edit to that same object shows on the next read, with no new lookup.
        SetSetting(settings, probe.Setting, fallback);
        Assert.AreEqual(fallback, Read(), provider.Name + ": an edit made to MCM's object must show on the next read");
        Assert.AreEqual(resolved, mcm.Lookups, provider.Name + ": reading an edit must not look the object up again");
    }

    private static void SetSetting(BaseSettings settings, string property, bool value)
        => settings.GetType().GetProperty(property)!.SetValue(settings, value);
}
