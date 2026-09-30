using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TAOM.Features.RealmBorders.Domain;

namespace TAOM.Adapters;

/// <summary>The realm borders' messages, localized through TAOM's {=KEY} strings and shown in the message log.</summary>
public sealed class RealmNoticeAdapter : IRealmNoticeAdapter
{
    public void ShowEnteringRealm(string realmName)
    {
        var text = new TextObject("{=taom_realm_borders_entering}You enter the lands of {REALM}.");
        text.SetTextVariable("REALM", realmName);
        InformationManager.DisplayMessage(new InformationMessage(text.ToString(), Color.FromUint(0xFFE8D8A8)));
    }

    /// <summary>One whole sentence per mode, never a fragment slotted into a frame, so each language can inflect it.</summary>
    public void ShowMapMode(MapMode mode)
    {
        var text = mode switch
        {
            MapMode.Alignment => new TextObject("{=taom_realm_borders_mode_alignment}The map shows the Free Peoples and the Shadow."),
            MapMode.War => new TextObject("{=taom_realm_borders_mode_war}The map shows your allies and your enemies."),
            _ => new TextObject("{=taom_realm_borders_mode_political}The map shows the realms."),
        };
        InformationManager.DisplayMessage(new InformationMessage(text.ToString()));
    }
}
