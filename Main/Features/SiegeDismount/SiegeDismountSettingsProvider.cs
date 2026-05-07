using TAOM.Features.SiegeDismount.Models;

namespace TAOM.Features.SiegeDismount;

public class SiegeDismountSettingsProvider : ISiegeDismountSettingsProvider
{
    public bool IsEnabled => TaomSettings.Instance?.EnableSiegeDismount ?? true;

    public SiegeMountBehaviorType MountBehavior =>
        (SiegeMountBehaviorType)(TaomSettings.Instance?.SiegeMountBehavior ?? (int)SiegeMountBehaviorType.AutoRemountAfter);

    public bool IsDebugMode => TaomSettings.Instance?.SiegeDismountDebug ?? false;
}
