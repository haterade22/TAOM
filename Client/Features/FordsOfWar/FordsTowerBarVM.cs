using System;
using TaleWorlds.Library;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Client.Features.FordsOfWar;

/// <summary>
/// One tower or keep as a bar: the fill is its hit points of <see cref="FullWidth"/> pixels, blue for the commander's side and red
/// for the enemy's, dimmer while the tower is locked behind one in front of it, and empty once it has fallen.
/// </summary>
public sealed class FordsTowerBarVM : FordsViewModel
{
    /// <summary>The width in pixels of a full bar; the prefab's track is this wide.</summary>
    public const float FullWidth = 44f;

    internal const string OwnColor = "#4F8FE0FF";
    private const string OwnLockedColor = "#2F527FFF";
    internal const string EnemyColor = "#D04A3AFF";
    private const string EnemyLockedColor = "#8A3329FF";
    private const string GoneColor = "#00000000";

    private string _label = string.Empty;
    private string _hitPointsText = string.Empty;
    private string _fillColor = GoneColor;
    private float _fillWidth;

    internal string TowerId { get; private set; } = string.Empty;

    internal bool IsOwn { get; private set; }

    internal bool IsDestroyed { get; private set; }

    internal bool IsLocked { get; private set; }

    [DataSourceProperty]
    public string Label
    {
        get => _label;
        private set => Set(ref _label, value, nameof(Label));
    }

    [DataSourceProperty]
    public string HitPointsText
    {
        get => _hitPointsText;
        private set => Set(ref _hitPointsText, value, nameof(HitPointsText));
    }

    [DataSourceProperty]
    public string FillColor
    {
        get => _fillColor;
        private set => Set(ref _fillColor, value, nameof(FillColor));
    }

    [DataSourceProperty]
    public float FillWidth
    {
        get => _fillWidth;
        private set => Set(ref _fillWidth, value, nameof(FillWidth));
    }

    internal void Update(MatchView.TowerView tower, MatchSide own, string label)
    {
        TowerId = tower.Id;
        Label = label;
        IsOwn = tower.Owner == own;
        IsDestroyed = tower.Destroyed;
        IsLocked = tower.Locked && !tower.Destroyed;
        HitPointsText = tower.Destroyed ? "0" : tower.HitPoints.ToString();
        FillWidth = tower.Destroyed || tower.MaxHitPoints <= 0
            ? 0f
            : Math.Max(1f, (float)Math.Round(FullWidth * Math.Min(1f, (float)tower.HitPoints / tower.MaxHitPoints)));
        FillColor = tower.Destroyed ? GoneColor : IsOwn ? (IsLocked ? OwnLockedColor : OwnColor) : (IsLocked ? EnemyLockedColor : EnemyColor);
    }
}
