namespace TAOM.Features.Refuge;

/// <summary>MCM boundary; values validated at this surface (Config Providers MUST Validate).
/// The source module declared an EnableRefuge toggle and never read it; this one is real.</summary>
public interface IRefugeSettingsProvider
{
    /// <summary>Master toggle. Off = no founding, no menus; standing refuges persist and can be
    /// entered/dismantled so a toggle never strands a garrison.</summary>
    bool Enabled { get; }

    /// <summary>Gold to found a refuge (default 2000).</summary>
    int FoundCost { get; }

    /// <summary>Gold to upgrade to a stronghold (default 5000).</summary>
    int StrongholdUpgradeCost { get; }

    /// <summary>Hours to raise a refuge after founding (default 6).</summary>
    float BuildHours { get; }

    /// <summary>Hard ceiling on simultaneous refuges; the live limit also scales with clan tier
    /// (default 3).</summary>
    int MaxRefugesCap { get; }

    /// <summary>Manage/interaction radius around a refuge (default 4).</summary>
    float ManageRange { get; }

    /// <summary>Founding keep-out radius from towns and castles (default 16).</summary>
    float MinTownDistance { get; }

    /// <summary>Stronghold-upgrade keep-out radius (default 26).</summary>
    float StrongholdMinTownDistance { get; }

    /// <summary>Defender damage reduction for a refuge (default 0.20).</summary>
    float RefugeDefenseBonus { get; }

    /// <summary>Defender damage reduction for a stronghold (default 0.35).</summary>
    float StrongholdDefenseBonus { get; }

    /// <summary>Base militia rallying to an attacked refuge (default 6).</summary>
    int MilitiaBase { get; }

    /// <summary>Militia ceiling (default 40).</summary>
    int MilitiaMax { get; }

    /// <summary>Optional hostile raids on refuges (default OFF, experimental as in the source).</summary>
    bool EnableRaids { get; }

    /// <summary>Raid search radius around a refuge (default 6).</summary>
    float RaidRange { get; }

    /// <summary>Named building mesh placed beside the command tent in the FALLBACK layout (source
    /// default "empire_street_tent_02"); empty disables it. Source-parity knob; not yet surfaced
    /// in MCM (TaomSettings is single-owner), so the provider returns the source default.</summary>
    string BuildingMesh { get; }

    /// <summary>Scale for <see cref="BuildingMesh"/> (source default 0.4).</summary>
    float BuildingScale { get; }
}
