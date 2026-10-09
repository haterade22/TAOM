// Adapted from yotthani's VanillaTuning (MIT, (c) 2026 yotthani), frame-buffer-guard and frame-buffer-watch.
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace TAOM.Adapters;

/// <summary>Engine side of the skeleton buffer watch: the one warning, localized through TAOM's {=KEY} strings.</summary>
public sealed class SkeletonBufferEngineAdapter : ISkeletonBufferEngineAdapter
{
    /// <summary>One whole sentence with one variable, so each language can inflect it. Called on the main thread from the mission tick.</summary>
    public void ShowWarning(int percent)
    {
        var text = new TextObject("{=taom_skeleton_buffer_warning}This mission uses {PERCENT} percent of the engine's skeleton buffer, and the game freezes for good when it is full. Lower the battle size.");
        text.SetTextVariable("PERCENT", percent.ToString(System.Globalization.CultureInfo.InvariantCulture));
        InformationManager.DisplayMessage(new InformationMessage(text.ToString(), Color.FromUint(0xFFE8A0A0)));
    }
}
