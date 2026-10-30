using System;
using System.Linq;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.PlayerSwitcher;
using TAOM.Features.PlayerSwitcher.Domain;
using static TAOM.Adapters.CharacterCreationStageKind;

namespace TAOM.Features.FactionUI.Presets;

/// <summary>
/// What a pick on Kysaro's faction screen does (#704). Mike, 2026-10-01: "When I pick an existing
/// character like Thranduil from the UI, it should skip the character creation all of the way until the
/// career picker, user picks the career and it starts the game." So a living hero with a clan, whom
/// Player Switcher (#514) would hand over, is taken over: Player Switcher's own selection is set, which
/// skips the backstory menus to the career choice (Patch78) and makes the player that hero at the end
/// (its handover, priority 1100), and when the culture stage completes the stages the hero needs none of
/// are taken out of the list: the face generator before the narrative stage, and the banner, clan name,
/// review and options stages after it. The hero may be of another culture than the faction screen
/// (Haldir on Lothlorien's, #1067).
/// <para>
/// A clanless wanderer on a named card (Gimli, Aragorn) is adopted instead (#1067): Player Switcher
/// adopts him into the player's new clan, so the player still names that clan and designs its banner.
/// Only the face generator, review and options stages are taken out, and the check of the creation
/// clan (<see cref="IPlayerIdentityAdapter.StartupClanIsDisposable"/>) is not asked, because an
/// adoption leaves that clan with the player.
/// </para>
/// <para>
/// Every other pick is copied as before (<see cref="FactionPresetService"/>, Option A): a picker-only
/// legend, a troop, a wanderer template, Sauron and the Nine when Player Switcher turns them off, or
/// any hero while Player Switcher is off. A pick the handover would refuse at the end is copied too,
/// since by then the stages it skipped could not be shown again.
/// </para>
/// </summary>
public sealed class FactionPickService
{
    /// <summary>Vanilla's stages in the order vanilla adds them: the only list a pick changes, and
    /// the order it puts back (pinned against the engine by FactionUIBindingTests).</summary>
    internal static readonly CharacterCreationStageKind[] EngineOrder =
        { Culture, FaceGenerator, Narrative, BannerEditor, ClanNaming, Review, Options };

    private static readonly CharacterCreationStageKind[] TakeoverSkipped =
        { FaceGenerator, BannerEditor, ClanNaming, Review, Options };

    // The player names the adopted hero's new clan and designs its banner, so those two stay.
    private static readonly CharacterCreationStageKind[] AdoptionSkipped =
        { FaceGenerator, Review, Options };

    private readonly FactionPresetService _presets;
    // Lazy on purpose: only a takeover needs the hero picker, and its adapter takes UncapturableHeroes'
    // registry. Building the launcher (and so this service) should not build that graph for a plain pick
    // (FactionUIWiringTests pins the resolvable graph).
    private readonly Lazy<IHeroPickerService> _heroes;
    private readonly IPlayerSwitchPolicyProvider _policy;
    private readonly IPlayerSwitchSessionWriter _session;
    private readonly IPlayerIdentityAdapter _identity;
    private readonly IModLogger _logger;
    private HeroPickRow _takeover;
    // The set the stage list in hand had taken out (one of the two above), null while it holds all of them.
    private CharacterCreationStageKind[]? _skipped;

    public FactionPickService(
        FactionPresetService presets,
        Lazy<IHeroPickerService> heroes,
        IPlayerSwitchPolicyProvider policy,
        IPlayerSwitchSessionWriter session,
        IPlayerIdentityAdapter identity,
        IModLogger logger)
    {
        _presets = presets;
        _heroes = heroes;
        _policy = policy;
        _session = session;
        _identity = identity;
        _logger = logger;
    }

    /// <summary>The current pick is a hero the player takes over.</summary>
    public bool IsTakeover => !_takeover.IsEmpty;

    /// <summary>A card or a browse-list entry was picked on the faction whose culture is
    /// <paramref name="cultureId"/>. Never throws: a click on the faction screen must not.</summary>
    public void Pick(RosterEntry pick, string? cultureId)
    {
        try
        {
            Clear();
            // Both kinds of pick show their look the same way; only what happens at the end differs.
            if (pick.IsHero)
                _presets.SelectHero(pick.Source);
            else
                _presets.SelectTemplate(pick.Source);

            var row = TakeoverRow(pick, cultureId);
            if (row.IsEmpty)
                return;
            _takeover = row;
            _session.Select(row);
        }
        catch (Exception ex)
        {
            _logger.LogError($"[FactionUI] the pick of {pick.Name} failed and was dropped: {ex}");
            Clear();
        }
    }

    /// <summary>"Custom Character", another faction, the faction screen shown again or declining: no pick
    /// stands. Player Switcher's selection goes too, whatever this service recorded: while the faction
    /// screen is the picker nothing else selects, and when it declines, the panel's own selection comes
    /// later, on the face generator, which clears it on construction anyway.</summary>
    public void Clear()
    {
        _takeover = default;
        _session.Clear();
        _presets.Clear();
    }

    /// <summary>A new character creation starts, or the game ended.</summary>
    public void ResetForNewCharacterCreation()
    {
        _takeover = default;
        _skipped = null;
        _session.Clear();
        _presets.ResetForNewCharacterCreation();
    }

    /// <summary>Character creation is finishing (handler priority 1060): a copied pick lands on the
    /// player; a takeover's is only forgotten, since Player Switcher's handover at 1100 makes the player
    /// that hero. Player Switcher's selection is left for that handover to read.</summary>
    public void OnCharacterCreationFinalize()
    {
        if (IsTakeover)
            _presets.ResetForNewCharacterCreation();
        else
            _presets.OnCharacterCreationFinalize();
    }

    /// <summary>The culture stage is completing (the faction was confirmed): a takeover goes on to the
    /// career choice, showing the hero; any other pick gets every stage back.</summary>
    public void OnCultureStageCompleted(ICharacterCreationStagesAdapter stages)
    {
        if (!IsTakeover)
        {
            RestoreStages(stages);
            return;
        }

        if (!TrySkipStages(stages, _takeover.HasClan ? TakeoverSkipped : AdoptionSkipped))
        {
            // The face generator shows the hero instead, and the handover still runs at the end.
            _logger.LogWarning($"[FactionUI] this character creation's stages are not vanilla's, so taking over {_takeover.Name} goes through all of them");
            return;
        }

        _presets.ApplyPendingLook();
        if (_takeover.HasClan)
            _logger.LogInfo($"[FactionUI] taking over {_takeover.Name} ({_takeover.HeroId}): straight to the career choice");
        else
            _logger.LogInfo($"[FactionUI] adopting {_takeover.Name} ({_takeover.HeroId}): the career choice, then the clan name and banner");
    }

    private bool TrySkipStages(ICharacterCreationStagesAdapter stages, CharacterCreationStageKind[] wanted)
    {
        if (stages.HoldsRemoved)
        {
            // Already short: the player came back to the faction screen and confirmed the same kind of pick.
            if (ReferenceEquals(_skipped, wanted))
                return true;

            // Short in another way (a takeover, then an adoption, or the reverse): whole again first.
            RestoreStages(stages);
        }

        // Only vanilla's own list, with the culture stage first: the engine opens the stage at the index
        // next, and on any other list the restore below could not rebuild the order by appending.
        if (stages.Count != EngineOrder.Length || stages.CurrentIndex != 1 || !EngineOrder.All(stages.Has))
            return false;

        foreach (var kind in wanted)
            stages.Remove(kind);
        _skipped = wanted;
        return true;
    }

    private void RestoreStages(ICharacterCreationStagesAdapter stages)
    {
        if (!stages.HoldsRemoved)
            return;

        // The engine only appends, so every stage after the culture stage that is still in the list comes
        // out too (the narrative stage, and after an adoption the banner and clan name), and all of them go
        // back in vanilla's order.
        _skipped = null;
        foreach (var kind in EngineOrder.Skip(1))
        {
            if (stages.Has(kind))
                stages.Remove(kind);
        }

        foreach (var kind in EngineOrder.Skip(1))
        {
            if (!stages.Append(kind))
                _logger.LogWarning($"[FactionUI] the {kind} stage could not be put back into character creation");
        }
    }

    private HeroPickRow TakeoverRow(RosterEntry pick, string? cultureId)
    {
        var heroId = pick.HeroId;
        if (string.IsNullOrEmpty(heroId) || string.IsNullOrEmpty(cultureId))
            return default;

        try
        {
            var row = _heroes.Value.FindTakeover(heroId!, cultureId!, _policy.Current);
            if (row.IsEmpty)
                return default;

            // The handover's own refusals, asked now: at the end the stages a takeover skips are gone.
            // An adoption (no clan) keeps the creation clan, so only a takeover asks whether it can go
            // (HeroSwitchService applies the same rule).
            if (!_identity.CanReassignPlayerClan || (row.HasClan && !_identity.StartupClanIsDisposable) || !_identity.IsSwitchable(heroId!))
            {
                _logger.LogWarning($"[FactionUI] {row.Name} cannot be taken over in this campaign; copying the look instead");
                return default;
            }

            return row;
        }
        catch (Exception ex)
        {
            _logger.LogError($"[FactionUI] could not check whether {pick.Name} can be taken over, copying instead: {ex}");
            return default;
        }
    }
}
