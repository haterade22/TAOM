using System;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Features.CareerSystem.UI;

// Per-entry VM rendered in the career-switch picker panel. Each instance corresponds to one
// eligible target career the player can switch INTO. ExecuteChoose() is wired to the "Choose"
// button in the prefab and forwards to the screen's switch-service callback.
public class CareerSwitchTargetVM : ViewModel
{
    private readonly CareerDefinition _career;
    private readonly Action<string> _onChoose;

    public CareerSwitchTargetVM(CareerDefinition career, AbilityTemplateData abilityTemplate, Action<string> onChoose)
    {
        _career = career;
        _onChoose = onChoose;

        Name = new TextObject(career.DisplayName).ToString();
        Description = new TextObject(career.Description).ToString();
        PortraitSprite = $"CareerSystem\\Portraits\\{career.PortraitSprite}";
        AbilityName = abilityTemplate != null
            ? new TextObject(abilityTemplate.DisplayName).ToString()
            : new TextObject(career.AbilityTemplateId).ToString();
        ChooseLabel = new TextObject("{=taom_career_switch_choose}Choose").ToString();
    }

    public string CareerId => _career.Id;

    [DataSourceProperty]
    public string Name { get; }

    [DataSourceProperty]
    public string Description { get; }

    [DataSourceProperty]
    public string PortraitSprite { get; }

    [DataSourceProperty]
    public string AbilityName { get; }

    [DataSourceProperty]
    public string ChooseLabel { get; }

    // Bound to Command.Click="ExecuteChoose" on the Choose button.
    public void ExecuteChoose()
    {
        _onChoose?.Invoke(_career.Id);
    }
}
