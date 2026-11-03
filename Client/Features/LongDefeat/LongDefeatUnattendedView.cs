using System;
using TaleWorlds.MountAndBlade.View.MissionViews;
using TAOM.Features.LongDefeat.Spike;

namespace TAOM.Client.Features.LongDefeat;

/// <summary>A lost focus opens the Escape menu, which pauses the mission until someone closes it, so an armed run says no, as <c>FordsCommandView</c> does.</summary>
internal sealed class LongDefeatUnattendedView : MissionView
{
    public override bool IsOpeningEscapeMenuOnFocusChangeAllowed() =>
        string.IsNullOrEmpty(Environment.GetEnvironmentVariable(LongDefeatSpikeOptions.EnvVar)) && base.IsOpeningEscapeMenuOnFocusChangeAllowed();
}
