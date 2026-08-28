using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TAOM.Core.Logging;
using TAOM.Features.PlayerSwitcher.Domain;
using TAOM.Features.PlayerSwitcher.Hooks;

namespace TAOM.Features.PlayerSwitcher.UI;

/// <summary>
/// Backs PreBuildCharacterSelection.xml. The prefab crossed over from LOTRAOM unchanged, so this
/// ViewModel is written to fit the file: three lists, three headers, and a per-row click.
/// </summary>
/// <remarks>
/// There is deliberately no confirm button, because the prefab has none. Clicking a lord selects
/// them and drives the live 3D preview; the player commits by pressing the face generator's own
/// Done, and abandons by clicking the selected lord again.
/// </remarks>
public class PlayerSwitcherVM : ViewModel
{
    private readonly IPlayerSwitchSessionWriter _session;
    private readonly IHeroPreviewSink _preview;
    private readonly IModLogger _logger;

    private string _kingdomMembersText = string.Empty;
    private string _clanMembersText = string.Empty;
    private string _companionsText = string.Empty;

    public PlayerSwitcherVM(
        HeroPickList picks,
        IPlayerSwitchSessionWriter session,
        IHeroPreviewSink preview,
        IModLogger logger)
    {
        _session = session;
        _preview = preview;
        _logger = logger;

        KingdomMembers = Build(picks.RulingHouse);
        ClanLeaders = Build(picks.ClanLeaders);
        Companions = Build(picks.Wanderers);

        RefreshValues();
    }

    [DataSourceProperty]
    public MBBindingList<HeroPickItemVM> KingdomMembers { get; }

    [DataSourceProperty]
    public MBBindingList<HeroPickItemVM> ClanLeaders { get; }

    [DataSourceProperty]
    public MBBindingList<HeroPickItemVM> Companions { get; }

    [DataSourceProperty]
    public string KingdomMembersText
    {
        get => _kingdomMembersText;
        set { if (value != _kingdomMembersText) { _kingdomMembersText = value; OnPropertyChangedWithValue(value, nameof(KingdomMembersText)); } }
    }

    [DataSourceProperty]
    public string ClanMembersText
    {
        get => _clanMembersText;
        set { if (value != _clanMembersText) { _clanMembersText = value; OnPropertyChangedWithValue(value, nameof(ClanMembersText)); } }
    }

    [DataSourceProperty]
    public string CompanionsText
    {
        get => _companionsText;
        set { if (value != _companionsText) { _companionsText = value; OnPropertyChangedWithValue(value, nameof(CompanionsText)); } }
    }

    /// <summary>
    /// Cascades into the rows. `TaleWorlds.Library.ViewModel.OnFinalize()` is an empty virtual, so
    /// without this the teardown's `ViewModel?.OnFinalize()` reaches a no-op stub and the rows are
    /// never finalized. That matters because the base row VM's own override
    /// (`ClanPartyMemberItemVM.OnFinalize`) is what nulls its `HeroViewModel`'s hero reference.
    /// </summary>
    public override void OnFinalize()
    {
        base.OnFinalize();

        foreach (var row in KingdomMembers) row.OnFinalize();
        foreach (var row in ClanLeaders) row.OnFinalize();
        foreach (var row in Companions) row.OnFinalize();
    }

    public override void RefreshValues()
    {
        base.RefreshValues();

        KingdomMembersText = Header("{=taom_ps_group_kingdom}Royal House", KingdomMembers.Count);
        ClanMembersText = Header("{=taom_ps_group_clans}Clan Leaders", ClanLeaders.Count);
        CompanionsText = Header("{=taom_ps_group_wanderers}Wanderers", Companions.Count);
    }

    private static string Header(string key, int count)
    {
        if (count > 0)
            return new TextObject(key).ToString();

        // An empty group keeps its header and says so, rather than binding null or vanishing and
        // leaving the player wondering whether the panel is broken. Only 20 of 39 cultures have
        // any wanderers, so this is the normal case rather than an edge one.
        return new TextObject(key) + " (" + new TextObject("{=taom_ps_none}none") + ")";
    }

    private MBBindingList<HeroPickItemVM> Build(IReadOnlyList<HeroPickRow> rows)
    {
        var list = new MBBindingList<HeroPickItemVM>();

        foreach (var row in rows)
        {
            var hero = Campaign.Current?.CampaignObjectManager?.Find<Hero>(row.HeroId);
            if (hero == null)
            {
                _logger.LogWarning($"Player Switcher: '{row.HeroId}' did not resolve to a hero, skipping the row");
                continue;
            }

            list.Add(new HeroPickItemVM(hero, row, OnRowClicked));
        }

        return list;
    }

    private void OnRowClicked(HeroPickItemVM clicked)
    {
        if (clicked == null)
        {
            _logger.LogInfo("[PS-DIAG] OnRowClicked fired with a null row");
            return;
        }

        // #514 diagnostic. The first in-game run showed a panel that renders correctly and does
        // nothing when clicked. This line is what distinguishes "the click never reaches the
        // ViewModel" from "the click lands and the preview fails silently".
        _logger.LogInfo(
            $"[PS-DIAG] OnRowClicked hero={clicked.Row.HeroId} name={clicked.Row.Name} " +
            $"race={clicked.Row.Race} wasSelected={clicked.IsSelected}");

        // Clicking the selected lord again is how the player takes their own character back,
        // since the prefab offers no separate button for it.
        var deselecting = clicked.IsSelected;

        ClearSelection();

        if (deselecting)
        {
            _session.Clear();
            _preview.RestoreDefault();
            return;
        }

        clicked.IsSelected = true;
        _session.Select(clicked.Row);
        _preview.ApplyPreview(clicked.Row);
    }

    private void ClearSelection()
    {
        foreach (var vm in KingdomMembers) vm.IsSelected = false;
        foreach (var vm in ClanLeaders) vm.IsSelected = false;
        foreach (var vm in Companions) vm.IsSelected = false;
    }
}
