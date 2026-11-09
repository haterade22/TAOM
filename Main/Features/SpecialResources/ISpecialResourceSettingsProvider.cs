namespace TAOM.Features.SpecialResources;

/// <summary>The gold-only option (#1108): MCM over the compiled defaults.</summary>
public interface ISpecialResourceSettingsProvider
{
    /// <summary>When true, no troop costs a special resource and the Elite Emissary sells for gold.</summary>
    bool EliteTroopsCostGoldOnly { get; }

    /// <summary>The emissary's gold price for each unit of <c>merchant_cost</c>, always in range.</summary>
    int EmissaryGoldPerResource { get; }
}
