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

    private static readonly FieldInfo? ParamsField = typeof(FaceGenVM).GetField(
        "_faceGenerationParams", BindingFlags.Instance | BindingFlags.NonPublic);

    private static readonly FieldInfo? RefreshEnabledField = typeof(FaceGenVM).GetField(
        "_characterRefreshEnabled", BindingFlags.Instance | BindingFlags.NonPublic);

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
            return;

        var hero = Campaign.Current?.CampaignObjectManager?.Find<Hero>(row.HeroId);
        if (hero == null)
        {
            _logger.LogWarning($"Player Switcher: '{row.HeroId}' did not resolve to a hero; preview skipped");
            return;
        }

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
            catch (ArgumentOutOfRangeException)
            {
                // NOT cosmetic, and NOT fixable by simply retrying. This is the defect that made
                // the whole preview look broken in game.
                //
                // FaceGenVM.Refresh calls UpdateVoiceIndiciesFromCurrentParameters, whose
                // GetVoiceUIIndex loops `for (i = 0; i < _faceGenerationParams.CurrentVoice; i++)`
                // over _isVoiceTypeUsableForOnlyNpc. That list is rebuilt for the TARGET race,
                // while CurrentVoice was decoded from the LORD's body-properties key. A lord whose
                // key encodes a voice index the target race does not define runs off the end and
                // throws, aborting Refresh before UpdateFace runs. UpdateFace calls
                // BodyGenerator.RefreshFace, the only assignment of BodyGenerator.Race outside the
                // constructor, so the race silently never commits: the face changes, the body does
                // not.
                //
                // Re-calling SetBodyProperties does NOT help, and a review caught that: the second
                // call takes the UpdateFacegen branch, and UpdateFacegen does
                // `SoundPreset.Value = GetVoiceUIIndex()` unconditionally, throwing at the same
                // index for the same reason.
                //
                // The real gap is that SetBodyProperties never adjusts the decoded params to the
                // race it just switched to. The engine's own BodyGenerator.InitBodyGenerator does,
                // via FaceGenerationParams.SetRaceGenderAndAdjustParams, which clamps CurrentVoice
                // (and hair, beard, textures, tattoo, eyebrow) to that race's limits. So apply the
                // engine's own adjustment to the live params and drive Refresh once more. Refresh
                // is called directly rather than through SetBodyProperties on purpose: going back
                // through SetBodyProperties would re-decode the key and undo the clamp.
                if (!TryAdjustParamsToRace(vm, row))
                    _logger.LogWarning(
                        $"Player Switcher: '{row.HeroId}' carries indices its race does not define, " +
                        "and the params could not be adjusted; the preview may show the wrong race.");
            }

            // After SetBodyProperties, never before: the setter itself needs the race to be
            // changeable while it applies.
            vm.CanChangeRace = false;
            vm.CanChangeGender = false;

            Dress(hero);

            // `no exception escaped` is NOT success. BodyGenerator.Race is assigned only by
            // RefreshFace, so if anything aborted the apply before UpdateFace ran, the race never
            // committed even though every catch block stayed quiet. Ask the engine what it
            // actually holds rather than inferring it from control flow.
            //
            // This matters beyond reporting: the finally below uses `applied` to decide whether to
            // clear IsPreviewActive, and that flag suppresses Patch9_RaceFilter. Treating a failed
            // apply as success would leave the culture race filter suppressed for the rest of the
            // visit.
            applied = _view.BodyGen != null && _view.BodyGen.Race == row.Race;

            if (!applied)
                _logger.LogWarning(
                    $"Player Switcher: preview for '{row.HeroId}' did not commit the race " +
                    $"(wanted {row.Race}, engine holds {_view.BodyGen?.Race}).");
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Player Switcher: preview failed for '{row.HeroId}', continuing undressed: {ex.Message}");
        }
        finally
        {

            // A preview that did NOT apply must not leave the race filter suppressed. Only the
            // success path earns the suppression, and only RestoreDefault lifts it afterwards.
            // Without this, the parse-failure return above and the catch beside it would leave
            // IsPreviewActive true, and Patch9_RaceFilter would silently stop applying the
            // culture race filter for the rest of this face generator visit, to a player who is
            // by then building their own face again.
            if (!applied)
            {
                _session.SetPreviewActive(false);

                // Undo a half-applied preview rather than leaving it staged.
                //
                // FaceGenVM.SetBodyProperties assigns BodyGenerator.CurrentBodyProperties near its
                // top, unconditionally, while Race and IsFemale are only written later by
                // RefreshFace. So a preview that fails partway leaves the lord's body key paired
                // with the player's race, and vanilla persists exactly that trio the next time it
                // calls SaveCurrentCharacter from Done() or GoToIndex(). Re-entering the face
                // generator would then build a BodyGenerator from the mutated character, and the
                // fresh sink would snapshot the LORD as the player's own default, which is not
                // recoverable from inside this feature.
                //
                // Restoring here keeps the damage window to the width of this method.
                if (_snapshotTaken)
                    RestoreDefault();
            }
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

        // Each step is isolated. Previously a throw in the first call skipped everything after it,
        // including the equipment restore and the save, so a deselect could leave the player still
        // wearing the lord's gear with only a log line to show for it.
        TryRestoreStep("body properties", () =>
            vm.SetBodyProperties(_defaultBody, ignoreDebugValues: false, race: _defaultRace, gender: _defaultGender));

        TryRestoreStep("race and gender controls", () =>
        {
            vm.CanChangeRace = _defaultCanChangeRace;
            vm.CanChangeGender = _defaultCanChangeGender;
        });

        TryRestoreStep("equipment", () =>
        {
            if (_defaultEquipment != null)
                CopyInto(_defaultEquipment);
            _view.IsDressed = _defaultIsDressed;
        });

        // Write the player's own body back into the character object.
        //
        // The preview mutates the live BodyGenerator, and vanilla calls SaveCurrentCharacter() from
        // both IFaceGeneratorHandler.Done() and GoToIndex(), persisting whatever is currently
        // previewed into CharacterObject.PlayerCharacter. Without this, previewing a lord and then
        // abandoning the selection would leave the player wearing that lord's face on the character
        // they built. It runs even if a step above failed, because a partial restore that is never
        // saved is the worse outcome.
        TryRestoreStep("save", () => _view.BodyGen?.SaveCurrentCharacter());

        // Re-arm the snapshot. The player is now back on their own character, and anything they
        // edit from here is the new thing worth restoring to. Without this, a second preview later
        // in the same visit would restore to the ORIGINAL snapshot and silently discard every edit
        // made between the two previews.
        _snapshotTaken = false;
    }

    private void TryRestoreStep(string what, Action step)
    {
        try
        {
            step();
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Player Switcher: could not restore {what} for the player's own character: {ex.Message}");
        }
    }


    /// <summary>
    /// Applies the engine's own post-decode adjustment that FaceGenVM.SetBodyProperties omits.
    ///
    /// Two reflection sites, both narrow and both fail-soft: the private FaceGenerationParams
    /// struct field, and the private bool that gates Refresh. Refresh early-returns unless that
    /// bool is set, and the aborted call left it false.
    /// </summary>
    private bool TryAdjustParamsToRace(FaceGenVM vm, HeroPickRow row)
    {
        if (ParamsField == null || RefreshEnabledField == null)
            return false;

        try
        {
            // FaceGenerationParams is a struct, so this boxes a copy, adjusts it, and writes back.
            var boxed = ParamsField.GetValue(vm);
            if (boxed == null)
                return false;

            var age = (float)(boxed.GetType().GetField("CurrentAge")?.GetValue(boxed) ?? 0f);

            var adjust = boxed.GetType().GetMethod(
                "SetRaceGenderAndAdjustParams",
                BindingFlags.Instance | BindingFlags.Public);
            if (adjust == null)
                return false;

            adjust.Invoke(boxed, new object[] { row.Race, row.IsFemale ? 1 : 0, (int)age });
            ParamsField.SetValue(vm, boxed);

            // Refresh bails out unless this is true, and the throw left it false.
            RefreshEnabledField.SetValue(vm, true);
            vm.Refresh(clearProperties: true);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Player Switcher: could not adjust face params for '{row.HeroId}': {ex.Message}");
            return false;
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
