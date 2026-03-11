using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TAOM.Core.Logging;

namespace TAOM.Features.FactionMap;

public class CultureSettingService : ICultureSettingService
{
    private readonly IModLogger _logger;

    public CultureSettingService(IModLogger logger)
    {
        _logger = logger;
    }

    public void SetCultureOnCharacterCreation(CultureObject culture, object viewInstance, object? originalDataSource)
    {
        try
        {
            var activeState = TaleWorlds.Core.GameStateManager.Current?.ActiveState;
            if (activeState == null) { _logger.LogError("No active game state"); return; }

            var charCreationProp = AccessTools.Property(activeState.GetType(), "CharacterCreationManager");
            var charCreation = charCreationProp?.GetValue(activeState);
            if (charCreation == null) { _logger.LogError("CharacterCreationManager not found"); return; }

            var contentProp = AccessTools.Property(charCreation.GetType(), "CharacterCreationContent");
            object? content = contentProp?.GetValue(charCreation);
            if (content == null)
            {
                var contentField = AccessTools.Field(charCreation.GetType(), "_characterCreationContent");
                content = contentField?.GetValue(charCreation);
            }

            if (content != null)
            {
                var setCultureMethod = AccessTools.Method(content.GetType(), "SetSelectedCulture");
                if (setCultureMethod != null)
                {
                    var parameters = setCultureMethod.GetParameters();
                    if (parameters.Length == 2)
                        setCultureMethod.Invoke(content, new object[] { culture, charCreation });
                    else if (parameters.Length == 1)
                        setCultureMethod.Invoke(content, new object[] { culture });
                }
                else
                {
                    _logger.LogError("SetSelectedCulture method not found");
                }
            }

            if (originalDataSource != null)
            {
                var culturesField = AccessTools.Field(originalDataSource.GetType(), "_cultures");
                var culturesList = culturesField?.GetValue(originalDataSource) as System.Collections.IList;
                if (culturesList != null)
                {
                    foreach (var cultureVM in culturesList)
                    {
                        var cultureIdProp = AccessTools.Property(cultureVM.GetType(), "CultureID");
                        var cultureId = cultureIdProp?.GetValue(cultureVM) as string;
                        if (cultureId == culture.StringId)
                        {
                            var selectMethod = AccessTools.Method(cultureVM.GetType(), "ExecuteSelectCulture");
                            selectMethod?.Invoke(cultureVM, null);
                            break;
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"SetCultureOnCharacterCreation error: {ex.Message}");
        }
    }
}
