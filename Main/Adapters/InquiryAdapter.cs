using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace TAOM.Adapters;

public sealed class InquiryAdapter : IInquiryAdapter
{
    public void ShowTwoOptionInquiry(
        string titleKey, string titleFallback,
        string bodyKey, string bodyFallback,
        string optionAKey, string optionAFallback,
        string optionBKey, string optionBFallback,
        System.Action onOptionA, System.Action onOptionB)
    {
        var title = new TextObject("{=" + titleKey + "}" + titleFallback).ToString();
        var body = new TextObject("{=" + bodyKey + "}" + bodyFallback).ToString();
        var optionA = new TextObject("{=" + optionAKey + "}" + optionAFallback).ToString();
        var optionB = new TextObject("{=" + optionBKey + "}" + optionBFallback).ToString();

        InformationManager.ShowInquiry(new InquiryData(
            title,
            body,
            true,
            true,
            optionA,
            optionB,
            onOptionA,
            onOptionB),
            true,
            false);
    }

    public void ShowMessage(string key, string fallback, string variableName, string variableValue)
    {
        var text = new TextObject("{=" + key + "}" + fallback);
        if (!string.IsNullOrEmpty(variableName))
            text.SetTextVariable(variableName, variableValue ?? string.Empty);
        InformationManager.DisplayMessage(new InformationMessage(text.ToString()));
    }
}
