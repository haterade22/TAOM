using System;
using System.Linq;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
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
                // Vanilla generates the player clan name inside SetSelectedCulture from
                // CharacterObject.PlayerCharacter.Culture (== Hero.MainHero.Culture). Assign the chosen
                // culture first so the generated family name comes from the selected culture's
                // <clan_names>, not the stale default culture. Vanilla's culture stage sets this on
                // click (before name gen); our faction-map flow calls SetSelectedCulture first, so we
                // must do it explicitly here.
                if (Hero.MainHero != null)
                    Hero.MainHero.Culture = culture;

                var setCultureMethod = AccessTools.Method(content.GetType(), "SetSelectedCulture");
                if (setCultureMethod != null)
                {
                    var parameters = setCultureMethod.GetParameters();
                    if (parameters.Length == 2)
                        setCultureMethod.Invoke(content, new object[] { culture, charCreation });
                    else if (parameters.Length == 1)
                        setCultureMethod.Invoke(content, new object[] { culture });

                    // Vanilla Helpers.FactionHelper.GenerateClanNameforPlayer() (invoked inside
                    // SetSelectedCulture) hardcodes the family name "dey Corvand" for the vlandia
                    // culture id. TAOM repurposes vlandia as Rohan, so replace that placeholder with a
                    // name generated from the culture's own <clan_names> (Harolding, Earfening, …).
                    // Only vlandia needs this — it is the sole culture vanilla special-cases; every
                    // other reused vanilla id already generates from its clan list. Guarded so an empty
                    // clan list keeps the vanilla fallback rather than throwing (GenerateClanName
                    // iterates a null array when the list is empty).
                    if (culture.StringId == "vlandia" && Hero.MainHero != null && Clan.PlayerClan != null
                        && culture.ClanNameList != null && culture.ClanNameList.Count > 0)
                    {
                        // GenerateClanName's vlandia special-case unconditionally derefs the settlement's
                        // Name (engine NameGenerator.cs), so a null settlement NREs. Pass a real
                        // culture-appropriate settlement. The ORIGIN_SETTLEMENT text variable is unused by
                        // TAOM's <clan_names>, so the choice is cosmetic — any non-null settlement avoids
                        // the crash; first-of-culture is thematically apt.
                        var originSettlement = Settlement.All.FirstOrDefault(s => s.Culture == culture)
                                               ?? Settlement.All.FirstOrDefault();
                        if (originSettlement != null)
                        {
                            var clanName = NameGenerator.Current.GenerateClanName(culture, originSettlement);
                            if (clanName != null)
                                Clan.PlayerClan.ChangeClanName(clanName, clanName);
                        }
                    }
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
