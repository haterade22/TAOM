using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.FactionPower;

namespace TAOM.Features.CampaignStartOptions;

/// <summary>
/// Starts the per-session state at <c>SubModule.OnGameStart</c>: pins a brand-new campaign's resolved values
/// into the campaign's own option data (the engine saves them with it), then refreshes the store the
/// readers use. A loaded save is never pinned. Kept apart from the store because the defaults it needs read
/// the Troop Weight provider, which read the store.
/// </summary>
public sealed class CampaignStartSession : ICampaignStartSession
{
    private readonly IAdvancedStartDataAdapter _data;
    private readonly CampaignStartOptionsStore _store;
    private readonly ICampaignStartDefaults _defaults;
    private readonly IModLogger _logger;
    private readonly IFactionPowerSettings _factionPower;

    public CampaignStartSession(
        IAdvancedStartDataAdapter data, CampaignStartOptionsStore store, ICampaignStartDefaults defaults, IModLogger logger,
        IFactionPowerSettings factionPower)
    {
        _data = data;
        _store = store;
        _defaults = defaults;
        _logger = logger;
        _factionPower = factionPower;
    }

    public void Begin()
    {
        _store.Reload();
        _factionPower.Reload();
        if (_data.IsAvailable && _data.IsNewCampaign)
        {
            Pin();
            _store.Reload();
        }
        else if (_store.IsPinned)
        {
            var days = _store.WarPhaseDays;
            _logger.LogInfo(
                $"CampaignStartOptions.Loaded: warOfTheRing={_store.WarOfTheRing} days={(days.HasValue ? $"{days.Value.Phase1Day}/{days.Value.Phase2Day}" : "")} "
                + $"troopWeight={_store.TroopWeight} warCanBeWon={_store.MomentumVictory} eyeHunters={_store.EyeOfSauron} "
                + $"largeAiArmies={_store.AiPartyScaling} alignment={_store.AlignmentPreset}");
        }
    }

    private void Pin()
    {
        var screen = ReadScreen();
        var resolved = CampaignStartOptionsResolver.Resolve(screen, _defaults.Current());
        if (resolved.Warning != null)
            _logger.LogWarning($"CampaignStartOptions: {resolved.Warning}");

        const string category = CampaignStartOptionKeys.PinnedCategory;
        _data.Add(CampaignStartOptionKeys.VersionKey, category, CampaignStartOptionKeys.CurrentVersion);
        _data.Add(CampaignStartOptionKeys.WarOfTheRingKey, category, resolved.WarOfTheRing);
        _data.Add(CampaignStartOptionKeys.Day1Key, category, resolved.Phase1Day);
        _data.Add(CampaignStartOptionKeys.Day2Key, category, resolved.Phase2Day);
        _data.Add(CampaignStartOptionKeys.TroopWeightKey, category, resolved.TroopWeight);
        _data.Add(CampaignStartOptionKeys.VictoryKey, category, resolved.MomentumVictory);
        _data.Add(CampaignStartOptionKeys.EyeOfSauronKey, category, resolved.EyeOfSauron);
        _data.Add(CampaignStartOptionKeys.AiPartyScalingKey, category, resolved.AiPartyScaling);
        _data.Add(CampaignStartOptionKeys.AlignmentKey, category, resolved.AlignmentPreset);

        _logger.LogInfo(
            $"CampaignStartOptions.Pinned: warOfTheRing={resolved.WarOfTheRing} days={resolved.Phase1Day}/{resolved.Phase2Day} "
            + $"troopWeight={resolved.TroopWeight} "
            + $"warCanBeWon={resolved.MomentumVictory} eyeHunters={resolved.EyeOfSauron} largeAiArmies={resolved.AiPartyScaling} "
            + $"alignment={resolved.AlignmentPreset} "
            + $"(scenario={screen.Scenario ?? "default"}, start={screen.StartDate ?? "default"})");
    }

    private ScreenValues ReadScreen()
    {
        var screen = new ScreenValues();
        if (_data.TryGet<string>(CampaignStartOptionKeys.ScenarioOption, out var scenario)) screen.Scenario = scenario;
        if (_data.TryGet<string>(CampaignStartOptionKeys.StartDateOption, out var start)) screen.StartDate = start;
        if (_data.TryGet<int>(CampaignStartOptionKeys.CustomDay1Option, out var day1)) screen.CustomDay1 = day1;
        if (_data.TryGet<int>(CampaignStartOptionKeys.CustomDay2Option, out var day2)) screen.CustomDay2 = day2;
        if (_data.TryGet<bool>(CampaignStartOptionKeys.TroopWeightOption, out var weight)) screen.TroopWeight = weight;
        if (_data.TryGet<bool>(CampaignStartOptionKeys.WarCanBeWonOption, out var victory)) screen.WarCanBeWon = victory;
        if (_data.TryGet<bool>(CampaignStartOptionKeys.EyeHuntersOption, out var eye)) screen.EyeHunters = eye;
        if (_data.TryGet<bool>(CampaignStartOptionKeys.LargeAiArmiesOption, out var armies)) screen.LargeAiArmies = armies;
        if (_data.TryGet<string>(CampaignStartOptionKeys.AlignmentPresetOption, out var alignment)) screen.AlignmentPreset = alignment;
        return screen;
    }
}
