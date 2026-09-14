using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade.GauntletUI.Widgets.Nameplate;

namespace TAOM.Features.SettlementNameplateRelation;

/// <summary>
/// Issue #591. The container under a settlement plate's capsule button (Id
/// "SettlementNameplateLayout" in the three nameplate prefabs). Vanilla signals relation and
/// visibility by writing <c>Color</c>, <c>AlphaFactor</c> and <c>ColorFactor</c> on the
/// <see cref="SettlementNameplateItemWidget"/>; TAOM's restyled prefab draws the bar, text and
/// diamond on descendants the engine never propagates those values to. This widget consumes them:
/// <c>RelationType="@Relation"</c> picks a palette entry, blended by the MCM tint strength (#596),
/// and <see cref="SettlementPlatePresenter"/> mirrors the item's alpha every late update.
/// Engine-constructed (settings arrive through the static <see cref="Settings"/>), auto-registered
/// by simple type name (hence the Taom prefix). Never throws: a broken tint is cosmetic.
/// </summary>
public class TaomSettlementPlateWidget : Widget
{
    /// <summary>Parents to walk for the item widget (the prefabs nest it two up; a prefab test pins that).</summary>
    internal const int MaxAncestorDepth = 4;
    private const int MaxResolveAttempts = 120;

    /// <summary>Captured once by <c>NameplateRelationIoC.InitializeWidgetStatics</c>; null means defaults.</summary>
    public static INameplateRelationSettingsProvider? Settings { get; set; }

    private int _relationType = -1;
    private bool _paletteDirty;
    private int _resolveAttempts;
    private float _appliedTint = float.NaN;
    private SettlementNameplateItemWidget? _item;
    private Widget? _bar;
    private Widget? _frame;
    private TextWidget? _text;
    private MaskedTextureWidget? _banner;
    private Widget? _ring;
    private float _lastAlpha = float.NaN;
    private float _lastColorFactor = float.NaN;

    private NameplatePaletteEntry _neutral = NameplateRelationPalette.DefaultNeutral;
    private NameplatePaletteEntry _sameFaction = NameplateRelationPalette.DefaultSameFaction;
    private NameplatePaletteEntry _enemy = NameplateRelationPalette.DefaultEnemy;
    private NameplatePaletteEntry _ally = NameplateRelationPalette.DefaultAlly;

    public TaomSettlementPlateWidget(UIContext context) : base(context)
    {
    }

    [Editor(false)]
    public int RelationType { get => _relationType; set => SetRelation(value); }

    [Editor(false)]
    public Widget? BarBackgroundWidget { get => _bar; set => SetReference(ref _bar, value, nameof(BarBackgroundWidget)); }

    [Editor(false)]
    public TextWidget? NameTextWidget { get => _text; set => SetReference(ref _text, value, nameof(NameTextWidget)); }

    [Editor(false)]
    public Widget? FrameWidget { get => _frame; set => SetReference(ref _frame, value, nameof(FrameWidget)); }

    [Editor(false)]
    public MaskedTextureWidget? BannerWidget { get => _banner; set => SetReference(ref _banner, value, nameof(BannerWidget)); }

    [Editor(false)]
    public Widget? TrackedRingWidget { get => _ring; set => SetReference(ref _ring, value, nameof(TrackedRingWidget)); }

    [Editor(false)] public Color NeutralBarColor { get => _neutral.Bar; set => SetEntry(ref _neutral, value, _neutral.Text, _neutral.Frame); }
    [Editor(false)] public Color NeutralTextColor { get => _neutral.Text; set => SetEntry(ref _neutral, _neutral.Bar, value, _neutral.Frame); }
    [Editor(false)] public Color NeutralFrameColor { get => _neutral.Frame; set => SetEntry(ref _neutral, _neutral.Bar, _neutral.Text, value); }
    [Editor(false)] public Color SameFactionBarColor { get => _sameFaction.Bar; set => SetEntry(ref _sameFaction, value, _sameFaction.Text, _sameFaction.Frame); }
    [Editor(false)] public Color SameFactionTextColor { get => _sameFaction.Text; set => SetEntry(ref _sameFaction, _sameFaction.Bar, value, _sameFaction.Frame); }
    [Editor(false)] public Color SameFactionFrameColor { get => _sameFaction.Frame; set => SetEntry(ref _sameFaction, _sameFaction.Bar, _sameFaction.Text, value); }
    [Editor(false)] public Color EnemyBarColor { get => _enemy.Bar; set => SetEntry(ref _enemy, value, _enemy.Text, _enemy.Frame); }
    [Editor(false)] public Color EnemyTextColor { get => _enemy.Text; set => SetEntry(ref _enemy, _enemy.Bar, value, _enemy.Frame); }
    [Editor(false)] public Color EnemyFrameColor { get => _enemy.Frame; set => SetEntry(ref _enemy, _enemy.Bar, _enemy.Text, value); }
    [Editor(false)] public Color AllyBarColor { get => _ally.Bar; set => SetEntry(ref _ally, value, _ally.Text, _ally.Frame); }
    [Editor(false)] public Color AllyTextColor { get => _ally.Text; set => SetEntry(ref _ally, _ally.Bar, value, _ally.Frame); }
    [Editor(false)] public Color AllyFrameColor { get => _ally.Frame; set => SetEntry(ref _ally, _ally.Bar, _ally.Text, value); }

    protected override void OnLateUpdate(float dt)
    {
        base.OnLateUpdate(dt);
        try
        {
            ResolveReferences();
            ApplyPaletteIfDirty();
            if (_item != null)
                SettlementPlatePresenter.MirrorAlpha(_item, Settings, _relationType, _bar, _frame, _text, _banner, _ring, ref _lastAlpha, ref _lastColorFactor);
        }
        catch
        {
            // Shield-excluded UI layer: swallowing IS the crash guard.
        }
    }

    private void SetRelation(int value)
    {
        if (_relationType == value) return;
        _relationType = value;
        OnPropertyChanged(value, nameof(RelationType));
        _paletteDirty = true;
    }

    private void SetReference<T>(ref T? field, T? value, string name) where T : Widget
    {
        if (ReferenceEquals(field, value)) return;
        field = value;
        if (value != null) OnPropertyChanged(value, name);
        _paletteDirty = true;
    }

    private void SetEntry(ref NameplatePaletteEntry field, Color bar, Color text, Color frame)
    {
        if (field.Bar == bar && field.Text == text && field.Frame == frame) return;
        field = new NameplatePaletteEntry(bar, text, frame);
        _paletteDirty = true;
    }

    /// <summary>XML paths deliver every reference; on a null miss, fall back to an Id search for a bounded number of frames.</summary>
    private void ResolveReferences()
    {
        if (_resolveAttempts >= MaxResolveAttempts) return;
        if (_item != null && _bar != null && _text != null && _frame != null && _banner != null && _ring != null) return;
        _resolveAttempts++;

        if (_item == null) _item = SettlementPlatePresenter.FindItemAncestor(this, MaxAncestorDepth);
        if (_bar == null) BarBackgroundWidget = FindChild("SettlementBarBackgroundWidget", true);
        if (_text == null) NameTextWidget = FindChild("SettlementNameTextWidget", true) as TextWidget;
        if (_frame == null) FrameWidget = FindChild("SettlementBannerBorderWidget", true);
        if (_banner == null) BannerWidget = FindChild("SettlementBannerWidget", true) as MaskedTextureWidget;
        if (_ring == null) TrackedRingWidget = FindChild("TrackedRingWidget", true);
    }

    /// <summary>A slider move (or the toggle) repaints on the next frame: one float compare per plate.</summary>
    private void ApplyPaletteIfDirty()
    {
        var settings = Settings;
        var tint = NameplateRelationPalette.EffectiveStrength(settings?.ColorsEnabled ?? true, settings?.TintStrength ?? 1f);
        if (tint != _appliedTint) _paletteDirty = true;
        if (!_paletteDirty) return;
        if (_bar == null && _text == null && _frame == null) return;

        var entry = NameplateRelationPalette.Select(_relationType, _neutral, _sameFaction, _enemy, _ally);
        SettlementPlatePresenter.ApplyPalette(_bar, _frame, _text, NameplateRelationPalette.Blend(entry, tint));
        _appliedTint = tint;
        _paletteDirty = false;
    }
}
