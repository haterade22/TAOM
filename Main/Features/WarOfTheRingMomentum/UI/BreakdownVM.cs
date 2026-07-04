using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TAOM.Features.WarOfTheRingMomentum.Domain;

namespace TAOM.Features.WarOfTheRingMomentum.UI;

/// <summary>
/// One three-column row (Free value | label | Evil value). Momentum rows carry
/// accumulating tooltips listing each contributing event; stats rows are plain.
/// Values render at internal scale (raw ÷ 100).
/// </summary>
public class BreakdownVM : ViewModel
{
    [DataSourceProperty] public string Text { get; set; }
    [DataSourceProperty] public string Number1 { get; set; }
    [DataSourceProperty] public string Number2 { get; set; }
    [DataSourceProperty] public BasicTooltipViewModel ValueFaction1 { get; set; }
    [DataSourceProperty] public BasicTooltipViewModel ValueFaction2 { get; set; }

    /// <summary>Momentum row: per-side event lists → summed values + tooltip breakdown.</summary>
    public BreakdownVM(string label, IReadOnlyList<MomentumEvent> freeEvents, IReadOnlyList<MomentumEvent> evilEvents)
    {
        Text = label;

        var freeExplained = BuildExplained(freeEvents);
        var evilExplained = BuildExplained(evilEvents);
        Number1 = ((int)freeExplained.ResultNumber).ToString();
        Number2 = ((int)evilExplained.ResultNumber).ToString();
        ValueFaction1 = new BasicTooltipViewModel(() => GetExplainerTooltip(freeEvents));
        ValueFaction2 = new BasicTooltipViewModel(() => GetExplainerTooltip(evilEvents));
    }

    /// <summary>Stats row: plain numbers, no tooltip.</summary>
    public BreakdownVM(string label, string freeValue, string evilValue)
    {
        Text = label;
        Number1 = freeValue;
        Number2 = evilValue;
    }

    private static ExplainedNumber BuildExplained(IReadOnlyList<MomentumEvent> events)
    {
        var explained = new ExplainedNumber(0f, includeDescriptions: true);
        foreach (var ev in events)
            explained.Add(ev.Value / (float)MomentumWarState.MomentumScale, new TextObject(ev.Description));
        return explained;
    }

    private static List<TooltipProperty> GetExplainerTooltip(IReadOnlyList<MomentumEvent> events)
    {
        var explained = BuildExplained(events);
        return CampaignUIHelper.GetTooltipForAccumulatingPropertyWithResult(
            new TextObject("{=taom_wotr_momentum}Momentum").ToString(),
            explained.ResultNumber, ref explained);
    }
}
