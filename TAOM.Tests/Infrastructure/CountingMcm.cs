using System;
using System.Reflection;
using MCM.Abstractions;
using MCM.Abstractions.Base;
using NSubstitute;

namespace TAOM.Tests.Infrastructure;

/// <summary>
/// MCM's own provider is what every GlobalSettings&lt;T&gt;.Instance asks, through a static whose setter is internal
/// (MCM assigns it once at start-up). This installs a counting stand-in for one test, holding no settings until
/// Register, and puts back whatever was there. Shared by the hot-path settings gates (HotPathSettingsProvidersTests)
/// and the static readers they cannot construct (CreatureBanditTuningLiveTests).
/// </summary>
internal sealed class CountingMcm : IDisposable
{
    private static readonly MethodInfo SetInstance =
        typeof(BaseSettingsProvider).GetProperty(nameof(BaseSettingsProvider.Instance))!.GetSetMethod(nonPublic: true)!;

    private readonly BaseSettingsProvider? _previous = BaseSettingsProvider.Instance;
    private BaseSettings? _registered;

    public int Lookups { get; private set; }

    public CountingMcm()
    {
        var stand = Substitute.For<BaseSettingsProvider>();
        stand.GetSettings(Arg.Any<string>()).Returns(_ =>
        {
            Lookups++;
            return _registered;
        });
        SetInstance.Invoke(null, new object?[] { stand });
    }

    public void Register(BaseSettings settings) => _registered = settings;

    public void Dispose() => SetInstance.Invoke(null, new object?[] { _previous });
}
