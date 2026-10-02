using TaleWorlds.Library;
using TAOM.Features.FactionUI.FactionScreen;

namespace TAOM.Features.FactionUI.UI.FactionScreen;

/// <summary>The selected faction's text, benefits and map pin (#704, Kysaro's).</summary>
public sealed class FactionDetailVM : ViewModel
{
    public FactionDetailVM(FactionInfo info)
    {
        Description = info.Description;
        DifficultyText = info.DifficultyText;
        Benefits = new MBBindingList<BenefitItemVM>();
        foreach (var benefit in info.Benefits)
            Benefits.Add(new BenefitItemVM(benefit.Key, benefit.Value));
        Strengths = new MBBindingList<TextItemVM>();
        foreach (var strength in info.Strengths)
            Strengths.Add(new TextItemVM(strength));
        Weaknesses = new MBBindingList<TextItemVM>();
        foreach (var weakness in info.Weaknesses)
            Weaknesses.Add(new TextItemVM(weakness));

        MapPinMarginLeft = (float)(info.MapX * 472.0 - 14.0);
        MapPinMarginTop = (float)(info.MapY * 222.0 - 49.0);
        MapPinMarginLeftBig = (float)(info.MapX * 952.0 - 20.0);
        MapPinMarginTopBig = (float)(info.MapY * 452.0 - 70.0);
        TerritorySprite = info.TerritorySprite;
        HasTerritory = !string.IsNullOrEmpty(info.TerritorySprite);
    }

    [DataSourceProperty] public string Description { get; }

    [DataSourceProperty] public string DifficultyText { get; }

    [DataSourceProperty] public MBBindingList<BenefitItemVM> Benefits { get; }

    [DataSourceProperty] public MBBindingList<TextItemVM> Strengths { get; }

    [DataSourceProperty] public MBBindingList<TextItemVM> Weaknesses { get; }

    [DataSourceProperty] public bool HasStrengths => Strengths.Count > 0;

    [DataSourceProperty] public bool HasWeaknesses => Weaknesses.Count > 0;

    [DataSourceProperty] public float MapPinMarginLeft { get; }

    [DataSourceProperty] public float MapPinMarginTop { get; }

    [DataSourceProperty] public float MapPinMarginLeftBig { get; }

    [DataSourceProperty] public float MapPinMarginTopBig { get; }

    [DataSourceProperty] public string TerritorySprite { get; }

    [DataSourceProperty] public bool HasTerritory { get; }
}
