using System.Collections.Generic;
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
        System.Action onOptionA, System.Action onOptionB,
        string bodyVariableName = null, string bodyVariableValue = null,
        IReadOnlyDictionary<string, string> bodyVariables = null,
        bool prioritize = false)
    {
        var title = new TextObject("{=" + titleKey + "}" + titleFallback).ToString();

        var bodyText = new TextObject("{=" + bodyKey + "}" + bodyFallback);
        if (!string.IsNullOrEmpty(bodyVariableName))
            bodyText.SetTextVariable(bodyVariableName, bodyVariableValue ?? string.Empty);
        if (bodyVariables != null)
        {
            foreach (var pair in bodyVariables)
            {
                if (!string.IsNullOrEmpty(pair.Key))
                    bodyText.SetTextVariable(pair.Key, pair.Value ?? string.Empty);
            }
        }
        var body = bodyText.ToString();
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
            pauseGameActiveState: true,
            prioritize: prioritize);
    }

    public void ShowMessage(
        string key, string fallback,
        string variableName, string variableValue,
        string secondVariableName = null, string secondVariableValue = null)
    {
        var text = new TextObject("{=" + key + "}" + fallback);
        if (!string.IsNullOrEmpty(variableName))
            text.SetTextVariable(variableName, variableValue ?? string.Empty);
        if (!string.IsNullOrEmpty(secondVariableName))
            text.SetTextVariable(secondVariableName, secondVariableValue ?? string.Empty);
        InformationManager.DisplayMessage(new InformationMessage(text.ToString()));
    }
}
