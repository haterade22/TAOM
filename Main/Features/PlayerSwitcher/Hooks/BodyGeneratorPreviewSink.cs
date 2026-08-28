using System;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade.GauntletUI.BodyGenerator;
using TaleWorlds.MountAndBlade.ViewModelCollection.FaceGenerator;
using TAOM.Core.Logging;
using TAOM.Features.PlayerSwitcher.Domain;

namespace TAOM.Features.PlayerSwitcher.Hooks;

/// <summary>
/// The only file in the feature that touches BodyGeneratorView or FaceGenVM.
/// </summary>
/// <remarks>
/// Holds the feature's second reflection site: BodyGeneratorView._dressedEquipment is
/// <c>private readonly Equipment</c>, so it can never be reassigned, only mutated slot by slot.
/// The view's own constructor does exactly that, including clearing EquipmentIndex 4 when the item
/// is a banner, and this mirrors it.
///
/// Every failure here is soft. A preview that cannot dress the lord is a cosmetic loss; it must
/// never stop the player finishing character creation.
/// </remarks>
public class BodyGeneratorPreviewSink : IHeroPreviewSink
{
    private static readonly FieldInfo? DressedEquipmentField = typeof(BodyGeneratorView).GetField(
        "_dressedEquipment", BindingFlags.Instance | BindingFlags.NonPublic);

    private readonly BodyGeneratorView _view;
    private readonly IPlayerSwitchSessionWriter _session;
    private readonly IModLogger _logger;

    private bool _snapshotTaken;
    private BodyProperties _defaultBody;
    private int _defaultRace;
    private int _defaultGender;
    private bool _defaultCanChangeRace;
    private bool _defaultCanChangeGender;
    private bool _defaultIsDressed;
    private Equipment? _defaultEquipment;

    public BodyGeneratorPreviewSink(BodyGeneratorView view, IPlayerSwitchSessionWriter session, IModLogger logger)
    {
        _view = view;
        _session = session;
        _logger = logger;
    }

    public void ApplyPreview(HeroPickRow row)
    {
        var vm = _view?.DataSource;
        if (vm == null || row.IsEmpty)
        {
            _logger.LogInfo($"[PS-DIAG] ApplyPreview bailed: dataSourceNull={vm == null} rowEmpty={row.IsEmpty}");
            return;
        }

        var hero = Campaign.Current?.CampaignObjectManager?.Find<Hero>(row.HeroId);
        if (hero == null)
        {
            _logger.LogInfo($"[PS-DIAG] ApplyPreview bailed: hero {row.HeroId} did not resolve");
            return;
        }

        _logger.LogInfo(
            $"[PS-DIAG] ApplyPreview enter hero={row.HeroId} targetRace={row.Race} " +
            $"bodyGenRace={_view.BodyGen?.Race} vmGender={vm.SelectedGender} canChangeRace={vm.CanChangeRace}");

        var applied = false;

        try
        {
            TakeSnapshotOnce(vm);

            // Suppress Patch9_RaceFilter for the duration. SetBodyProperties triggers
            // Refresh(clearProperties: true) whenever the race changes, and that patch rebuilds
            // the dropdown down to the culture's allowed races, which would snap a dwarf or a
            // Sauron preview straight back to the culture default.
            _session.SetPreviewActive(true);

            if (!BodyProperties.FromString(hero.BodyProperties.ToString(), out var props))
            {
                _logger.LogWarning($"Player Switcher: could not parse body properties for '{row.HeroId}'");
                return;
            }

            try
            {
                vm.SetBodyProperties(props, ignoreDebugValues: false, race: row.Race, gender: row.IsFemale ? 1 : 0);
            }
            catch (ArgumentOutOfRangeException ex)
            {
                // A voice preset outside the range this race defines. Cosmetic, and the face
                // itself still applied.
                _logger.LogWarning($"Player Switcher: preview voice preset out of range for '{row.HeroId}': {ex.Message}");
            }

            // After SetBodyProperties, never before: the setter itself needs the race to be
            // changeable while it applies.
            vm.CanChangeRace = false;
            vm.CanChangeGender = false;

            Dress(hero);
            applied = true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Player Switcher: preview failed for '{row.HeroId}', continuing undressed: {ex.Message}");
        }
        finally
        {
            _logger.LogInfo(
                $"[PS-DIAG] ApplyPreview exit hero={row.HeroId} applied={applied} " +
                $"bodyGenRaceAfter={_view.BodyGen?.Race} isDressed={_view.IsDressed}");

            // A preview that did NOT apply must not leave the race filter suppressed. Only the
            // success path earns the suppression, and only RestoreDefault lifts it afterwards.
            // Without this, the parse-failure return above and the catch beside it would leave
            // IsPreviewActive true, and Patch9_RaceFilter would silently stop applying the
            // culture race filter for the rest of this face generator visit, to a player who is
            // by then building their own face again.
            if (!applied)
                _session.SetPreviewActive(false);
        }
    }

    public void RestoreDefault()
    {
        var vm = _view?.DataSource;
        if (vm == null || !_snapshotTaken)
        {
            _session.SetPreviewActive(false);
            return;
        }

        // Cleared FIRST, not in a finally. SetBodyProperties below triggers
        // Refresh(clearProperties: true), and that refresh is the only thing that rebuilds the
        // culture-filtered race selector. Clearing afterwards meant the one refresh that would
        // have restored the filter was itself suppressed, so deselecting a lord of another race
        // left the unfiltered vanilla selector in place for the rest of the visit.
        _session.SetPreviewActive(false);

        try
        {
            vm.SetBodyProperties(_defaultBody, ignoreDebugValues: false, race: _defaultRace, gender: _defaultGender);
            vm.CanChangeRace = _defaultCanChangeRace;
            vm.CanChangeGender = _defaultCanChangeGender;

            if (_defaultEquipment != null)
                CopyInto(_defaultEquipment);

            _view.IsDressed = _defaultIsDressed;

            // Write the player's own body back into the character object.
            //
            // The preview mutates the live BodyGenerator, and vanilla calls
            // BodyGenerator.SaveCurrentCharacter() from both IFaceGeneratorHandler.Done() and
            // GoToIndex(), which persists whatever is currently previewed into
            // CharacterObject.PlayerCharacter via UpdatePlayerCharacterBodyProperties. Without
            // this line, previewing a lord and then abandoning the selection would leave the
            // player wearing that lord's face on the character they built.
            _view.BodyGen?.SaveCurrentCharacter();
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Player Switcher: could not restore the player's own preview: {ex.Message}");
        }
    }

    private void TakeSnapshotOnce(FaceGenVM vm)
    {
        if (_snapshotTaken)
            return;

        // BodyGenerator.CurrentBodyProperties and .Race are public fields on the view's own
        // generator, which is the authoritative pair the view renders from.
        _defaultBody = _view.BodyGen.CurrentBodyProperties;
        _defaultRace = _view.BodyGen.Race;
        _defaultGender = vm.SelectedGender;
        _defaultCanChangeRace = vm.CanChangeRace;
        _defaultCanChangeGender = vm.CanChangeGender;
        _defaultIsDressed = _view.IsDressed;

        var live = ReadDressedEquipment();
        _defaultEquipment = live?.Clone(false);

        _snapshotTaken = true;
    }

    private void Dress(Hero hero)
    {
        var battle = hero.BattleEquipment;
        if (battle == null)
            return;

        if (!CopyInto(battle))
            return;

        _view.IsDressed = true;
    }

    /// <summary>
    /// Copies a source Equipment slot by slot into the view's own instance. The field is readonly,
    /// so replacing it is impossible and mutation is the only route.
    /// </summary>
    private bool CopyInto(Equipment source)
    {
        var target = ReadDressedEquipment();
        if (target == null)
            return false;

        for (var slot = EquipmentIndex.WeaponItemBeginSlot; slot < EquipmentIndex.NumEquipmentSetSlots; slot++)
        {
            var element = source[slot];

            // Mirrors what BodyGeneratorView's constructor does for its own equipment: a banner in
            // the extra weapon slot is cleared rather than rendered.
            if (!element.IsEmpty && element.Item != null && element.Item.IsBannerItem)
            {
                target[slot] = EquipmentElement.Invalid;
                continue;
            }

            target[slot] = element;
        }

        return true;
    }

    private Equipment? ReadDressedEquipment()
    {
        if (DressedEquipmentField == null)
        {
            _logger.LogWarning("Player Switcher: BodyGeneratorView._dressedEquipment not found; previews will be undressed");
            return null;
        }

        return DressedEquipmentField.GetValue(_view) as Equipment;
    }
}
