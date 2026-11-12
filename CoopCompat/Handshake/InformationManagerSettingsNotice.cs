using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace TAOM.CoopCompat.Handshake;

/// <summary>The real notice: one line in the in-game message list. Keys are registered and translated separately.</summary>
public sealed class InformationManagerSettingsNotice : ISettingsNotice
{
    public void SettingsDiffer(IReadOnlyList<string> groups)
    {
        var text = new TextObject("{=taom_coopcompat_settings_differ}Your TAOM settings differ from the host's in: {GROUPS}. Co-op may behave differently for you.")
            .SetTextVariable("GROUPS", string.Join(", ", groups));
        InformationManager.DisplayMessage(new InformationMessage(text.ToString(), Colors.Yellow));
    }

    public void BuildMismatch()
    {
        var text = new TextObject("{=taom_coopcompat_build_mismatch}Your TAOM build is not the host's build. Co-op can desync or misbehave. Install the same TAOM download as the host.");
        InformationManager.DisplayMessage(new InformationMessage(text.ToString(), Colors.Red));
    }
}
