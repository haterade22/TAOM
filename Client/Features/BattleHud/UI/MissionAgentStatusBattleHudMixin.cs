using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.ViewModels;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.ViewModelCollection;
using TAOM.Features.Stamina.Hooks;

namespace TAOM.Features.BattleHud.UI;

// Issue #1095: the circular HUD's health, horse and shield channels. It reads the numbers native
// AgentStatus.xml already binds on this VM (AgentHealth, HorseHealth, ShieldHealth, their maxima and
// ShowMountHealthBar / ShowShieldHealthBar), so no agent handle is held, and maps them through the
// unit-tested BattleHudStateMapper. Campaign-free: custom battles show health too. The career disk
// comes from MissionAgentStatusCareerMixin. The ammo capsule's count and alert are the VM's own
// AmmoCount and IsAmmoCountAlertEnabled; this adds only its icon and place (BattleHudAmmoMapper).
// HudColourMode carries the Accessibility colour option to the ring widget (BattleHudColourPalette).
// The stamina bar (#1497) reads the player's current soldier through StaminaHooks.TryRead each refresh.
[ViewModelMixin("Tick")]
internal class MissionAgentStatusBattleHudMixin : BaseViewModelMixin<MissionAgentStatusVM>
{
    private float _hudHealthFill;
    private float _hudHorseFill;
    private float _hudShieldFill;
    private bool _hudHasHorse;
    private bool _hudHasShield;
    private bool _hudShowAmmo;
    private bool _hudAmmoIsQuiver;
    private bool _hudAmmoIsJavelin;
    private float _hudAmmoTop = BattleHudGeometry.AmmoCapsuleTop(1);
    private float _hudAmmoNumberOffsetX;
    private int _hudColourMode;
    private bool _hudStaminaVisible;
    private float _hudStaminaHeight;
    private string _hudStaminaColour = BattleHudStaminaMapper.FreshColour;
    private bool _showStaminaBar = true;

    // The colour option is read again every this many refreshes (one a frame, so about half a second),
    // starting with the first. MCM's Instance walks its settings containers on each call, too much for
    // every frame; holding the instance instead would miss a change MCM makes by handing out a new one.
    private const int RefreshesPerSettingsRead = 30;
    private int _refreshesUntilSettingsRead;

    public MissionAgentStatusBattleHudMixin(MissionAgentStatusVM viewModel) : base(viewModel)
    {
    }

    public override void OnRefresh()
    {
        var vm = ViewModel;
        if (vm == null)
            return;

        if (--_refreshesUntilSettingsRead <= 0)
        {
            _refreshesUntilSettingsRead = RefreshesPerSettingsRead;
            HudColourMode = ColourModeIndex(TaomSettings.Instance);
            _showStaminaBar = TaomSettings.Instance?.ShowStaminaBar ?? true;
        }

        var state = BattleHudStateMapper.Map(new BattleHudInput(
            vm.AgentHealth, vm.AgentHealthMax,
            vm.ShowMountHealthBar, vm.HorseHealth, vm.HorseHealthMax,
            vm.ShowShieldHealthBar, vm.ShieldHealth, vm.ShieldHealthMax), null, null);

        HudHealthFill = state.Health.Fill01;
        HudHasHorse = state.Horse.IsVisible;
        HudHorseFill = state.Horse.Fill01;
        HudHasShield = state.Shield.IsVisible;
        HudShieldFill = state.Shield.Fill01;

        // The count and alert bind straight to the native VM; only the icon needs the wielded usage,
        // read fresh while native would show its counter. Native gates the counter twice: its own
        // ShowAmmoCount and its weapon column's IsAgentStatusPrioritized (a hidden counter maps to no icon).
        var showAmmo = vm.ShowAmmoCount && vm.IsAgentStatusPrioritized;
        var ammo = BattleHudAmmoMapper.Map(showAmmo, vm.AmmoCount,
            showAmmo ? WieldedUsageClass() : null, state.RingCount);
        HudShowAmmo = ammo.IsVisible;
        HudAmmoIsQuiver = ammo.Icon == BattleHudAmmoIcon.Quiver;
        HudAmmoIsJavelin = ammo.Icon == BattleHudAmmoIcon.Javelin;
        HudAmmoTop = ammo.Top;
        HudAmmoNumberOffsetX = ammo.NumberOffsetX;

        var hasStamina = StaminaHooks.TryRead(Mission.Current?.MainAgent, out var staminaShare, out var staminaBand);
        var stamina = BattleHudStaminaMapper.Map(_showStaminaBar, hasStamina, staminaShare, staminaBand);
        HudStaminaVisible = stamina.IsVisible;
        HudStaminaHeight = stamina.Height;
        HudStaminaColour = stamina.Colour;
    }

    /// <summary>The battle HUD colour option as the widget's mode index: no settings, no dropdown or an
    /// unknown index draw Default.</summary>
    internal static int ColourModeIndex(TaomSettings? settings)
        => (int)BattleHudColourPalette.Resolve(settings?.BattleHudColourMode?.SelectedIndex);

    // The same read native UpdateWeaponStatuses makes: the primary wielded slot's current usage, so a
    // javelin held for melee is not a javelin to throw. Nothing is cached across refreshes.
    private static WeaponClass? WieldedUsageClass()
    {
        var agent = Mission.Current?.MainAgent;
        if (agent == null || !agent.IsActive())
            return null;
        var index = agent.GetPrimaryWieldedItemIndex();
        if (index <= EquipmentIndex.None || index >= EquipmentIndex.NumAllWeaponSlots)
            return null;
        var weapon = agent.Equipment[index];
        if (weapon.IsEmpty)
            return null;
        return weapon.CurrentUsageItem?.WeaponClass;
    }

    [DataSourceProperty]
    public bool HudShowAmmo
    {
        get => _hudShowAmmo;
        set { if (_hudShowAmmo != value) { _hudShowAmmo = value; OnPropertyChanged(nameof(HudShowAmmo)); } }
    }

    [DataSourceProperty]
    public bool HudAmmoIsQuiver
    {
        get => _hudAmmoIsQuiver;
        set { if (_hudAmmoIsQuiver != value) { _hudAmmoIsQuiver = value; OnPropertyChanged(nameof(HudAmmoIsQuiver)); } }
    }

    [DataSourceProperty]
    public bool HudAmmoIsJavelin
    {
        get => _hudAmmoIsJavelin;
        set { if (_hudAmmoIsJavelin != value) { _hudAmmoIsJavelin = value; OnPropertyChanged(nameof(HudAmmoIsJavelin)); } }
    }

    [DataSourceProperty]
    public float HudAmmoTop
    {
        get => _hudAmmoTop;
        set { if (_hudAmmoTop != value) { _hudAmmoTop = value; OnPropertyChanged(nameof(HudAmmoTop)); } }
    }

    [DataSourceProperty]
    public float HudAmmoNumberOffsetX
    {
        get => _hudAmmoNumberOffsetX;
        set { if (_hudAmmoNumberOffsetX != value) { _hudAmmoNumberOffsetX = value; OnPropertyChanged(nameof(HudAmmoNumberOffsetX)); } }
    }

    [DataSourceProperty]
    public float HudHealthFill
    {
        get => _hudHealthFill;
        set { if (_hudHealthFill != value) { _hudHealthFill = value; OnPropertyChanged(nameof(HudHealthFill)); } }
    }

    [DataSourceProperty]
    public float HudHorseFill
    {
        get => _hudHorseFill;
        set { if (_hudHorseFill != value) { _hudHorseFill = value; OnPropertyChanged(nameof(HudHorseFill)); } }
    }

    [DataSourceProperty]
    public float HudShieldFill
    {
        get => _hudShieldFill;
        set { if (_hudShieldFill != value) { _hudShieldFill = value; OnPropertyChanged(nameof(HudShieldFill)); } }
    }

    [DataSourceProperty]
    public bool HudHasHorse
    {
        get => _hudHasHorse;
        set { if (_hudHasHorse != value) { _hudHasHorse = value; OnPropertyChanged(nameof(HudHasHorse)); } }
    }

    /// <summary>A <see cref="BattleHudColourMode"/> index for the ring widget's ColourMode.</summary>
    [DataSourceProperty]
    public int HudColourMode
    {
        get => _hudColourMode;
        set { if (_hudColourMode != value) { _hudColourMode = value; OnPropertyChanged(nameof(HudColourMode)); } }
    }

    [DataSourceProperty]
    public bool HudStaminaVisible
    {
        get => _hudStaminaVisible;
        set { if (_hudStaminaVisible != value) { _hudStaminaVisible = value; OnPropertyChanged(nameof(HudStaminaVisible)); } }
    }

    /// <summary>
    /// Pixels of the stamina fill, 0 to BattleHudStaminaMapper.TrackHeight. A float, because the widget writes its
    /// SuggestedHeight back here on every change and an int setter throws on that write (GauntletFloatWriteBackGateTests).
    /// </summary>
    [DataSourceProperty]
    public float HudStaminaHeight
    {
        get => _hudStaminaHeight;
        set { if (_hudStaminaHeight != value) { _hudStaminaHeight = value; OnPropertyChanged(nameof(HudStaminaHeight)); } }
    }

    [DataSourceProperty]
    public string HudStaminaColour
    {
        get => _hudStaminaColour;
        set { if (_hudStaminaColour != value) { _hudStaminaColour = value; OnPropertyChanged(nameof(HudStaminaColour)); } }
    }

    [DataSourceProperty]
    public bool HudHasShield
    {
        get => _hudHasShield;
        set { if (_hudHasShield != value) { _hudHasShield = value; OnPropertyChanged(nameof(HudHasShield)); } }
    }
}
