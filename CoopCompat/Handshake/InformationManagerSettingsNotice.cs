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

    public void LoadOrderDiffers(string hostFirst, string hostSecond)
    {
        var text = new TextObject("{=taom_coopcompat_load_order_differs}Your mod load order differs from the host's: on the host {FIRST} loads before {SECOND}, for you after it. A mod that loads later replaces items and troops from earlier ones, so you may see other gear or troops than the host. Load your mods in the host's order and restart the game.")
            .SetTextVariable("FIRST", hostFirst)
            .SetTextVariable("SECOND", hostSecond);
        InformationManager.DisplayMessage(new InformationMessage(text.ToString(), Colors.Yellow));
    }
}
