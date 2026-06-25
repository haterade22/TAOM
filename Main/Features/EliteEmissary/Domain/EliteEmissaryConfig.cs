using System;
using System.Collections.Generic;

namespace TAOM.Features.EliteEmissary.Domain;

/// <summary>Loaded + validated <c>elite_emissary_config.xml</c>: the key-settlement set and the
/// per-culture ordered offer lists. Immutable; built once by the config provider.</summary>
public sealed class EliteEmissaryConfig
{
    public bool Enabled { get; }

    /// <summary>Settlement StringIds where the emissary appears (case-sensitive membership).</summary>
    public ISet<string> KeySettlementIds { get; }

    /// <summary>cultureId → ordered troop ids offered for that owner culture.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> CultureOffers { get; }

    public EliteEmissaryConfig(bool enabled, ISet<string> keySettlementIds,
        IReadOnlyDictionary<string, IReadOnlyList<string>> cultureOffers)
    {
        Enabled = enabled;
        KeySettlementIds = keySettlementIds ?? new HashSet<string>();
        CultureOffers = cultureOffers ?? new Dictionary<string, IReadOnlyList<string>>();
    }

    /// <summary>Empty/inert config — feature does nothing. Used on missing/malformed file.</summary>
    public static readonly EliteEmissaryConfig Empty = new(
        enabled: false,
        keySettlementIds: new HashSet<string>(),
        cultureOffers: new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal));
}
