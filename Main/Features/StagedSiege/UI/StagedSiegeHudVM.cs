using TaleWorlds.Library;
using TaleWorlds.Localization;
using TAOM.Features.StagedSiege.Domain;

namespace TAOM.Features.StagedSiege.UI;

/// <summary>
/// View model for the staged siege HUD (StagedSiegeHud.xml): a strip of the points, the objective, the capture progress and a
/// marker over the current point. Wording lives here, the reading in <see cref="StagedSiegeHudModel"/>; the mission view calls
/// <see cref="Refresh"/> about four times a second and <see cref="PlaceMarker"/> every frame.
/// </summary>
public sealed class StagedSiegeHudVM : ViewModel
{
    private bool _isVisible;
    private string _stripText = string.Empty;
    private string _objectiveText = string.Empty;
    private string _progressText = string.Empty;
    private bool _isMarkerVisible;
    private string _markerText = string.Empty;
    private float _markerOffsetX;
    private float _markerOffsetY;
    private readonly StagedSiegeHudRecorder _recorder = new();

    [DataSourceProperty]
    public bool IsVisible
    {
        get => _isVisible;
        set
        {
            if (_isVisible != value)
            {
                _isVisible = value;
                OnPropertyChangedWithValue(value, nameof(IsVisible));
            }
        }
    }

    [DataSourceProperty]
    public string StripText
    {
        get => _stripText;
        set
        {
            if (_stripText != value)
            {
                _stripText = value;
                OnPropertyChangedWithValue(value, nameof(StripText));
            }
        }
    }

    [DataSourceProperty]
    public string ObjectiveText
    {
        get => _objectiveText;
        set
        {
            if (_objectiveText != value)
            {
                _objectiveText = value;
                OnPropertyChangedWithValue(value, nameof(ObjectiveText));
            }
        }
    }

    [DataSourceProperty]
    public string ProgressText
    {
        get => _progressText;
        set
        {
            if (_progressText != value)
            {
                _progressText = value;
                OnPropertyChangedWithValue(value, nameof(ProgressText));
            }
        }
    }

    [DataSourceProperty]
    public bool IsMarkerVisible
    {
        get => _isMarkerVisible;
        set
        {
            if (_isMarkerVisible != value)
            {
                _isMarkerVisible = value;
                OnPropertyChangedWithValue(value, nameof(IsMarkerVisible));
            }
        }
    }

    [DataSourceProperty]
    public string MarkerText
    {
        get => _markerText;
        set
        {
            if (_markerText != value)
            {
                _markerText = value;
                OnPropertyChangedWithValue(value, nameof(MarkerText));
            }
        }
    }

    [DataSourceProperty]
    public float MarkerOffsetX
    {
        get => _markerOffsetX;
        set
        {
            if (_markerOffsetX != value)
            {
                _markerOffsetX = value;
                OnPropertyChangedWithValue(value, nameof(MarkerOffsetX));
            }
        }
    }

    [DataSourceProperty]
    public float MarkerOffsetY
    {
        get => _markerOffsetY;
        set
        {
            if (_markerOffsetY != value)
            {
                _markerOffsetY = value;
                OnPropertyChangedWithValue(value, nameof(MarkerOffsetY));
            }
        }
    }

    public void Refresh(StageSnapshot snapshot)
    {
        var model = StagedSiegeHudModel.From(snapshot);
        var letter = model.PointLetter.ToString();
        StripText = model.Strip;
        MarkerText = letter;
        ObjectiveText = ObjectiveLine(model.ObjectiveKind, letter);
        ProgressText = snapshot.Finished ? string.Empty : ProgressLine(model);
        IsVisible = true;
        _recorder.Shown(snapshot.MissionToken, letter, model.ObjectiveKind);
    }

    /// <summary>The lord's hall fight: point E is the objective, and the progress line goes once it is taken.</summary>
    public void Refresh(HallSnapshot snapshot)
    {
        var model = StagedSiegeHudModel.FromHall(snapshot);
        StripText = model.Strip;
        MarkerText = model.PointLetter.ToString();
        ObjectiveText = ObjectiveLine(model.ObjectiveKind, MarkerText);
        ProgressText = snapshot.Finished ? string.Empty : ProgressLine(model);
        IsVisible = true;
        _recorder.Shown(snapshot.MissionToken, MarkerText, model.ObjectiveKind);
    }

    public void PlaceMarker(bool visible, float offsetX, float offsetY)
    {
        IsMarkerVisible = visible;
        MarkerOffsetX = offsetX;
        MarkerOffsetY = offsetY;
        _recorder.Marker(visible, MarkerText);
    }

    private static string ObjectiveLine(HudObjectiveKind kind, string letter)
    {
        switch (kind)
        {
            case HudObjectiveKind.Take:
                return new TextObject("{=taom_staged_siege_take}Take point {POINT}").SetTextVariable("POINT", letter).ToString();
            case HudObjectiveKind.Hold:
                return new TextObject("{=taom_staged_siege_hold}Hold point {POINT}").SetTextVariable("POINT", letter).ToString();
            case HudObjectiveKind.TakeHall:
                return new TextObject("{=taom_staged_siege_take_hall}Take the lord's hall").ToString();
            case HudObjectiveKind.HallTaken:
                return new TextObject("{=taom_staged_siege_hall_taken}The lord's hall is taken.").ToString();
            case HudObjectiveKind.Hall:
                return new TextObject("{=taom_staged_siege_hall}The defenders fall back to the lord's hall").ToString();
            default:
                return new TextObject("{=taom_staged_siege_fallen}The last point has fallen").ToString();
        }
    }

    // One whole sentence per status, so a translator can reorder the percentage and the state.
    private static string ProgressLine(StagedSiegeHudModel model)
    {
        var line = ProgressSentence(model.CaptureStatus);
        line.SetTextVariable("PERCENT", model.ProgressPercent);
        return line.ToString();
    }

    private static TextObject ProgressSentence(CaptureStatus status)
    {
        switch (status)
        {
            case CaptureStatus.Rising:
                return new TextObject("{=taom_staged_siege_progress_rising}Capture {PERCENT}%: attackers gaining");
            case CaptureStatus.Falling:
                return new TextObject("{=taom_staged_siege_progress_falling}Capture {PERCENT}%: defenders pushing back");
            case CaptureStatus.Contested:
                return new TextObject("{=taom_staged_siege_progress_contested}Capture {PERCENT}%: contested");
            default:
                return new TextObject("{=taom_staged_siege_progress_idle}Capture {PERCENT}%: quiet");
        }
    }
}
