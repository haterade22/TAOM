using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using TAOM.Core.Domain;
using TAOM.Features.Realms.Domain.Config;
using TAOM.Features.Realms.Domain.Persistence;
using TAOM.Features.Realms.Domain.Services;
using TAOM.Features.Realms.Hooks;

namespace TAOM.Features.Realms;

/// <summary>
/// Keeps the title ledger in step with the campaign and stores it in the save.
/// Titles are created on the first hourly tick of a campaign that has none, so TAOM's character
/// creation and starting changes are finished before the initial holders are recorded.
/// The campaign's coordinator, owned by the thin <see cref="RealmsCampaignBehavior"/> (ADR-002): it registers the
/// listeners in the behavior's name and reads and writes the behavior's save data.
/// </summary>
public sealed partial class RealmsCampaign
{
    private const string ChunkCountKey = "taom_realms_chunks";
    private const string ChunkKeyPrefix = "taom_realms_chunk_";
    private const string KingdomNamesKey = "taom_realms_kingdom_names";

    private readonly KingdomNames _kingdomNames = new();

    private readonly IRealmsPopupAdapter _popups;
    private readonly IRealmsSettingsProvider _settings;

    /// <summary>Told of each kingdom <see cref="KingdomNames"/> renames (the realm names on the map letter it anew).</summary>
    private readonly IKingdomRenameListener[] _renameListeners;
    private readonly Action<string> _renamed;

    /// <summary>The title styles option the lord party names were last built with; a change clears them.</summary>
    private bool _stylesOn = true;

    /// <summary>This peer may change the campaign: singleplayer or a co-op host (docs/features/coop-interop.md).</summary>
    private readonly Func<bool> _isAuthority;
    private readonly RealmsService _service = new();
    private readonly IRealmLeaders _leaders = new RealmLeaders();
    private RealmsConfig? _config;

    /// <summary>Save data this version could not read. It is written back unchanged and the ledger stays off.</summary>
    private List<string>? _unreadChunks;

    /// <summary>The save format the titles were read from; the loaded titles are brought up to date once the campaign is running.</summary>
    private int _savedVersion = RealmsSaveFormat.Version;
    private bool _needsUpgrade;

    public RealmsCampaign(IRealmsPopupAdapter popups, Func<bool> isAuthority, IRealmsSettingsProvider? settings = null,
        IEnumerable<IKingdomRenameListener>? renameListeners = null)
    {
        _popups = popups;
        _settings = settings ?? new RealmsSettingsProvider();
        _isAuthority = isAuthority;
        _renameListeners = renameListeners?.ToArray() ?? Array.Empty<IKingdomRenameListener>();
        _renamed = TellRenamed;
        _service.Clock = () => CampaignTime.Now.ToDays;
        LiegeArmyDiscount.Behavior = this;
    }

    /// <summary>The running campaign's titles, or null outside a campaign.</summary>
    public static RealmsCampaign? Current => RealmsCampaignBehavior.Current?.Realms;

    public RealmsService Service => _service;

    /// <summary>The "World/Realms" mod options.</summary>
    public IRealmsSettingsProvider Settings => _settings;

    public RealmsConfig Config
    {
        get
        {
            if (_config == null)
            {
                _config = RealmsConfigLoader.Load();
                _service.Config = _config;
            }

            // The mod options scale the file's prices and payments. A few assignments, so every read is current.
            _config.Claims.BuyPricePercent = _settings.ClaimPricePercent;
            _config.Claims.InfluencePercent = _settings.InfluenceCostPercent;
            _config.Feudal.InfluencePercent = _settings.InfluenceCostPercent;
            _config.Feudal.TributePercent = _settings.TributePercent;
            return _config;
        }
    }

    public bool IsDisabled => _unreadChunks != null;

    /// <summary>Registers the campaign listeners with <paramref name="owner"/>, the campaign behavior, as their owner.</summary>
    internal void RegisterEvents(object owner)
    {
        CampaignEvents.HourlyTickEvent.AddNonSerializedListener(owner, OnHourlyTick);
        CampaignEvents.DailyTickEvent.AddNonSerializedListener(owner, OnDailyTick);
        CampaignEvents.WeeklyTickEvent.AddNonSerializedListener(owner, OnWeeklyTick);
        CampaignEvents.HeroKilledEvent.AddNonSerializedListener(owner, OnHeroKilled);
        CampaignEvents.OnSettlementOwnerChangedEvent.AddNonSerializedListener(owner, OnSettlementOwnerChanged);
        CampaignEvents.OnClanLeaderChangedEvent.AddNonSerializedListener(owner, (_, _) => ResolvePending());
        CampaignEvents.RulingClanChanged.AddNonSerializedListener(owner, OnRulingClanChanged);
        CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(owner, _ => OnSessionLaunched());
    }

    private void OnSessionLaunched()
    {
        LiegeArmyDiscount.Patch();
        Upgrade();
        if (_service.IsInitialized && _isAuthority())
        {
            ApplyFormerBannersOnLoad();
            ApplyCrownedBannersOnLoad();
        }

        UpdateKingdomNames();

        // Party names may have been built before the saved titles were read.
        _stylesOn = _settings.TitleStyles;
        PartyNames.Refresh();
    }

    private void OnHourlyTick()
    {
        // Every peer builds its own party names, so this runs on a co-op client too.
        if (_stylesOn != _settings.TitleStyles)
        {
            _stylesOn = !_stylesOn;
            PartyNames.Refresh();
        }

        MayWrite();
    }

    /// <summary>
    /// Brings titles made by an older version up to date, once per session after the campaign's heroes and realms exist:
    /// a version 1 save's vacant throne, unions, starting claims and offices added to the config since. These are not news.
    /// </summary>
    private void Upgrade()
    {
        if (!_needsUpgrade || !_service.IsInitialized || IsDisabled || !_isAuthority())
            return;
        _needsUpgrade = false;
        var warnings = new List<string>();
        var quiet = new List<TitleEvent>(_service.Migrate(_savedVersion, _leaders));
        var added = _service.AddMissingTitles(warnings);
        var claims = _service.ApplyStartingClaims(id => Hero.Find(id)?.IsAlive == true);
        quiet.AddRange(_service.EnsureOffices(_claimWorld));
        foreach (var warning in warnings)
            Debug.Print($"[TAOM.Realms] {warning}");
        foreach (var e in quiet)
            Debug.Print($"[TAOM.Realms] update {e.Kind}/{e.Reason} {e.TitleId}: {e.PreviousHolderId ?? "-"} -> {e.HeroId ?? "-"}");
        if (added > 0 || claims > 0)
            Debug.Print($"[TAOM.Realms] added {added} titles (titular realms and unions) and {claims} starting claims.");
        _savedVersion = RealmsSaveFormat.Version;
        ForgetBonds();
        PartyNames.Refresh();
    }

    /// <summary>The ledger is ready and this peer may change it and the campaign. A co-op client keeps the ledger it loaded, read only.</summary>
    private bool MayWrite() => _isAuthority() && EnsureInitialized();

    /// <summary>Creates the titles if this campaign has none yet. Returns false while the ledger is off, and on a co-op client without titles.</summary>
    public bool EnsureInitialized()
    {
        if (IsDisabled)
            return false;
        if (_service.IsInitialized)
            return true;
        if (!_isAuthority())
            return false;

        var warnings = _service.Initialize(WorldSnapshotReader.Read(), Config);
        foreach (var warning in warnings)
            Debug.Print($"[TAOM.Realms] {warning}");
        Debug.Print($"[TAOM.Realms] created {_service.Catalog.Count} titles.");
        if (_service.IsInitialized)
        {
            // The starting grants are part of the new titles, not news.
            foreach (var e in GrantToChildren())
                Debug.Print($"[TAOM.Realms] start grant {e.TitleId}: {e.PreviousHolderId} -> {e.HeroId}");
            _needsUpgrade = true;
            Upgrade(); // starting claims and offices
            UpdateKingdomNames();
        }

        PartyNames.Refresh();
        return _service.IsInitialized;
    }

    private void OnDailyTick()
    {
        if (!_service.IsInitialized || IsDisabled || !_isAuthority())
            return;
        ResolvePending();
        if (_settings.FamilyGrants)
            Report(GrantToChildren()); // children who came of age, and saves made before grants existed
        UpdateKingdomNames(); // a ruler changed in a way no title event shows (a new kingdom, a clan leaving)
    }

    /// <summary>
    /// Moves a titular realm's lands to it or back (<see cref="RealmsService.SyncTitularLands"/>: Khazad-dûm formed, or
    /// fallen vacant), then names each kingdom after the higher title its ruler holds, or gives it its own name back
    /// (<see cref="KingdomNames"/>).
    /// </summary>
    private void UpdateKingdomNames()
    {
        if (!_service.IsInitialized || IsDisabled || !_isAuthority())
            return;
        var moved = _service.SyncTitularLands();
        if (moved > 0)
        {
            ForgetBonds();
            Debug.Print($"[TAOM.Realms] {moved} titles moved to or from a titular realm.");
        }
        _kingdomNames.Update(_service, Config, _renamed);
    }

    private void TellRenamed(string kingdomId)
    {
        foreach (var listener in _renameListeners)
        {
            try
            {
                listener.OnKingdomRenamed(kingdomId);
            }
            catch (Exception ex)
            {
                // A listener that throws must not stop the other kingdoms being renamed.
                Debug.Print($"[TAOM.Realms] {listener.GetType().Name} failed on the rename of {kingdomId}: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// The weekly work: what titles do to their holders, the offices, then the AI's claims and new titles. One sweep
    /// (<see cref="ClaimWorld.BeginSweep"/>), and the time each part took goes to the log, so a slow week shows where.
    /// </summary>
    private void OnWeeklyTick()
    {
        if (!_service.IsInitialized || IsDisabled || !_isAuthority())
            return;
        var clock = System.Diagnostics.Stopwatch.StartNew();
        long consequences, offices, claims;
        IReadOnlyList<TitleEvent> events;
        _claimWorld.BeginSweep();
        try
        {
            ForgetBonds();
            WeeklyConsequences();
            consequences = clock.ElapsedMilliseconds;
            WeeklyOffices();
            offices = clock.ElapsedMilliseconds;
            events = WeeklyAiClaims();
            claims = clock.ElapsedMilliseconds;
        }
        finally
        {
            _claimWorld.EndSweep();
        }

        Report(events, everyParty: false);
        Debug.Print($"[TAOM.Realms] weekly tick {clock.ElapsedMilliseconds} ms: consequences {consequences}, offices {offices - consequences}, "
                    + $"AI titles and claims {claims - offices}, report {clock.ElapsedMilliseconds - claims} ({events.Count} events).");
    }

    /// <summary>AI clan leaders hand lesser titles to their grown children. The player's clan grants nothing by itself.</summary>
    private IReadOnlyList<TitleEvent> GrantToChildren()
    {
        var heads = new List<ClanHead>();
        var leaders = new List<Hero>();
        foreach (var clan in Clan.All)
        {
            var leader = clan?.Leader;
            if (clan == null || clan.IsEliminated || clan == Clan.PlayerClan || leader == null || !leader.IsAlive || leader == Hero.MainHero)
                continue;
            var ruled = clan.Kingdom != null && clan.Kingdom.RulingClan == clan ? clan.Kingdom.StringId : null;
            heads.Add(new ClanHead(leader.StringId, clan.HomeSettlement?.StringId, ruled));
            leaders.Add(leader);
        }

        return _service.GrantToChildren(heads, FamilySnapshotReader.ReadHouseholds(leaders), Config.KingdomCapitals);
    }

    private void OnHeroKilled(Hero victim, Hero killer, KillCharacterAction.KillCharacterActionDetail detail, bool showNotification)
    {
        if (victim == null || !MayWrite())
            return;
        Report(_service.OnHeroDied(victim.StringId, FamilySnapshotReader.Read(victim), _claimWorld));
        ResolvePending();
    }

    private void OnSettlementOwnerChanged(Settlement settlement, bool openToClaim, Hero newOwner, Hero oldOwner,
        Hero capturerHero, ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail detail)
    {
        if (settlement == null || !MayWrite())
            return;
        var day = CampaignTime.Now.ToDays;
        // A fief the old owner granted to a member of their clan leaves with the fief on a lawful hand-over.
        var oldClan = oldOwner?.Clan?.Heroes;
        var oldClanIds = oldClan != null ? new HashSet<string>(oldClan.Where(h => h?.StringId != null).Select(h => h.StringId)) : null;
        Report(_service.OnFiefOwnerChanged(settlement.StringId, newOwner?.StringId, oldOwner?.StringId, KindOf(detail), day, oldClanIds));
    }

    /// <summary>The game passes the clan that ruled before; the new one is already the kingdom's ruling clan.</summary>
    private void OnRulingClanChanged(Kingdom kingdom, Clan formerRulingClan)
    {
        if (kingdom == null || !MayWrite())
            return;
        Report(_service.OnRealmRulerChanged(kingdom.StringId, kingdom.RulingClan?.Leader?.StringId));
        ResolvePending();
        Report(_service.UpdateOffices(_claimWorld, null)); // a Steward's office follows the new ruler at once
        ApplyFormerBanners(kingdom, formerRulingClan);
        UpdateKingdomNames();
    }

    private void ResolvePending()
    {
        if (_service.IsInitialized && !IsDisabled && _isAuthority())
            Report(_service.ResolvePending(_leaders, _claimWorld));
    }

    internal static TransferKind KindOf(ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail detail)
    {
        switch (detail)
        {
            case ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail.BySiege:
            case ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail.ByRebellion:
                return TransferKind.Conquest;
            case ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail.ByKingDecision:
            case ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail.ByGift:
            case ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail.ByBarter:
                return TransferKind.Transfer;
            default:
                return TransferKind.Other;
        }
    }

    private void Report(IReadOnlyList<TitleEvent> events) => Report(events, everyParty: true);

    /// <param name="everyParty">
    /// Clear every lord party's name; false clears only those of the events' holders, for events that record every
    /// change of holder they made (the AI's claims and new titles).
    /// </param>
    private void Report(IReadOnlyList<TitleEvent> events, bool everyParty)
    {
        if (events.Count == 0)
            return;
        TitleEventReporter.Report(events, _service.Catalog, Config);
        ForgetBonds();
        // A holder changed, so a party's style may have.
        if (everyParty)
            PartyNames.Refresh();
        else
            PartyNames.Refresh(events);
        UpdateKingdomNames();
    }

    internal void SyncData(IDataStore dataStore)
    {
        if (dataStore.IsSaving)
            Save(dataStore);
        else
            Load(dataStore);
    }

    private void Save(IDataStore dataStore)
    {
        var chunks = _unreadChunks
            ?? (_service.IsInitialized
                ? new List<string>(SaveChunks.Split(RealmsSaveFormat.Write(_service.Catalog, _service.Ledger)))
                : new List<string>());
        var count = chunks.Count;
        dataStore.SyncData(ChunkCountKey, ref count);
        for (var i = 0; i < count; i++)
        {
            var chunk = chunks[i];
            dataStore.SyncData(ChunkKeyPrefix + i, ref chunk);
        }

        var names = _kingdomNames.Save();
        dataStore.SyncData(KingdomNamesKey, ref names);
    }

    private void Load(IDataStore dataStore)
    {
        var names = string.Empty;
        dataStore.SyncData(KingdomNamesKey, ref names);
        _kingdomNames.Load(names);

        var count = 0;
        dataStore.SyncData(ChunkCountKey, ref count);
        if (count <= 0)
            return; // a campaign without titles yet: they are created on the first hourly tick

        var chunks = new List<string>(count);
        for (var i = 0; i < count; i++)
        {
            var chunk = string.Empty;
            dataStore.SyncData(ChunkKeyPrefix + i, ref chunk);
            chunks.Add(chunk ?? string.Empty);
        }

        SaveReadResult read;
        try
        {
            read = RealmsSaveFormat.Read(SaveChunks.Join(chunks));
        }
        catch (Exception ex)
        {
            read = new SaveReadResult(null, null, ex.Message, new string[0]);
        }

        foreach (var warning in read.Warnings)
            Debug.Print($"[TAOM.Realms] save: {warning}");
        if (read.Succeeded && read.Catalog != null && read.Ledger != null)
        {
            _ = Config; // the service reads its settings (chronicle length, offices) from here
            _service.Load(read.Catalog, read.Ledger);
            _savedVersion = read.Version;
            _needsUpgrade = true;
            return;
        }

        _unreadChunks = chunks;
        Debug.Print($"[TAOM.Realms] title data in this save could not be read ({read.Error}); titles are off and the data is kept as it is.");
    }
}
