using TAOM.Adapters;
using TAOM.Features.WarOfTheRingMomentum;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace TAOM.Features.WarChronicle.Events;

/// <summary>
/// Production <see cref="IChroniclePresenter"/>: the story event window (<see cref="IEventWindowAdapter"/>, the prefab
/// the Realms coronation shows): "War Chronicle" over the event title, its line as the story, the event's <c>image</c>
/// from GUI/EventArt as the illustration and, under the story, what the resolution applied
/// (<see cref="ChronicleEffectFormatter"/> over <see cref="ChronicleAnnouncement.Outcome"/>). Where no window can open (a client without TAOM.Client) it falls back to a plain
/// one-button <c>InformationManager.ShowInquiry</c> (the <c>InquiryData</c> shape TAOM already uses in CareerQuest and
/// FieldCommission). Never a scene notification, which NREs for custom races. The map is free when
/// <see cref="MapPopupGate.IsMapFree"/> says so and no event window is showing or waiting to open.
/// <c>{TARGET}</c> is the target settlement's name and <c>{DAYS}</c> the window length.
/// </summary>
public sealed class ChroniclePresenter : IChroniclePresenter
{
    internal const string Category = "{=taom_event_category_chronicle}War Chronicle";
    internal const string ForDays = "{=taom_event_effect_days}For {DAYS} days";

    private readonly IEventWindowAdapter _windows;
    private readonly IWarChronicleSettingsProvider _settings;
    private readonly IMomentumConfigProvider _momentum;
    private readonly IKingdomJoinAdapter _kingdoms;

    public ChroniclePresenter(IEventWindowAdapter windows, IWarChronicleSettingsProvider settings, IMomentumConfigProvider momentum,
        IKingdomJoinAdapter kingdoms)
    {
        _windows = windows;
        _settings = settings;
        _momentum = momentum;
        _kingdoms = kingdoms;
    }

    public bool IsMapFree() => MapPopupGate.IsMapFree() && !_windows.IsOpen;

    public void Show(ChronicleAnnouncement announcement)
    {
        var title = Text(announcement.Title, announcement).ToString();
        var body = announcement.Text == null ? string.Empty : Text(announcement.Text, announcement).ToString();
        var button = new TextObject("{=taom_wotr_continue}Continue").ToString();
        // The award lasted what the momentum config said when it was made (MomentumAwardService); the config loads once.
        var momentumDays = (int)System.Math.Round(_momentum.GetConfig().DurationsHours.Chronicle / 24d);
        var effects = ChronicleEffectFormatter.Format(announcement.Outcome, _settings.WarEffectStrength, momentumDays,
            raw => new TextObject(raw).ToString(),
            days => new TextObject(ForDays).SetTextVariable("DAYS", days).ToString(),
            _kingdoms.GetKingdomName);
        var category = new TextObject(Category).ToString();
        if (_windows.Show(new EventWindow(title, body, announcement.Image, button, category: category, effects: effects)))
            return;

        // SingleQueryPopUpVM null-checks the affirmative action: the button only closes the popup.
        InformationManager.ShowInquiry(
            new InquiryData(title, body, true, false, button, null, null, null),
            true,
            false);
    }

    private static TextObject Text(string text, ChronicleAnnouncement announcement)
    {
        // A fresh TextObject per call: SetTextVariable on a shared one would leak into its other users.
        var result = new TextObject(text);
        var target = announcement.TargetSettlementId == null ? null : Settlement.Find(announcement.TargetSettlementId);
        result.SetTextVariable("TARGET", target?.Name ?? TextObject.GetEmpty());
        result.SetTextVariable("DAYS", announcement.Days);
        return result;
    }
}
