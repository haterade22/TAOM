using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.WandererAllegiance;

namespace TAOM.Features.NamedCompanions.Oaths.Hooks;

/// <summary>
/// Thin dialogue registration (ADR-002) for Oaths of Service (#904): the hire terms, the extra topics and the
/// backstory intro (#1061): <see cref="OathHireDialog"/>, <see cref="OathTopicDialog"/> and <see cref="OathIntroDialog"/>.
/// When the session launches the game data is loaded, so this class first resolves the config's item and text ids, then registers all of them
/// (they read the resolved config). Its SyncData is empty: the ledger's behavior owns the save record.
/// </summary>
public sealed class OathsOfServiceDialogBehavior : CampaignBehaviorBase
{
    private readonly OathHireDialog _hire;
    private readonly OathTopicDialog _topics;
    private readonly OathIntroDialog _intro;
    private readonly INamedCompanionConfigProvider _config;
    private readonly IItemPoolAdapter _items;
    private readonly INamedCompanionAdapter _adapter;
    private readonly IModLogger _logger;

    public OathsOfServiceDialogBehavior(
        HireTermsService hire, OathTopicService topics, INamedCompanionConfigProvider config, INamedCompanionAdapter adapter,
        ICoopSessionProvider coop, IItemPoolAdapter items, IArmourItemCatalogAdapter catalog,
        WandererAllegianceService allegiance, IModLogger logger)
    {
        _config = config;
        _items = items;
        _adapter = adapter;
        _logger = logger;
        _hire = new OathHireDialog(hire, adapter, coop, catalog.GetName, logger);
        _topics = new OathTopicDialog(topics, config, coop, catalog.GetName, logger);
        _intro = new OathIntroDialog(topics, coop, allegiance, logger);
    }

    public override void RegisterEvents()
    {
        CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
    }

    public override void SyncData(IDataStore dataStore) { }

    private void OnSessionLaunched(CampaignGameStarter starter)
    {
        try
        {
            OathConfigValidator.ResolveIds(_config.GetCompanions(), _items.ItemExists, TextExists, _adapter.SettlementExists, _logger);
        }
        catch (Exception ex)
        {
            _logger.LogError($"[Oaths] could not resolve the config ids: {ex}");
        }
        _hire.Register(starter);
        _topics.Register(starter);
        _intro.Register(starter);
    }

    private static bool TextExists(string id) => !string.IsNullOrEmpty(id) && GameTexts.TryGetText(id, out _);
}
