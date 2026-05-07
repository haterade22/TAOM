using TAOM.Features.SiegeDismount.Models;

namespace TAOM.Features.SiegeDismount;

public interface ISiegeDismountSettingsProvider
{
    bool IsEnabled { get; }
    SiegeMountBehaviorType MountBehavior { get; }
    bool IsDebugMode { get; }
}
