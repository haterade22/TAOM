namespace TAOM.Features.MountDespawn;

public class MountDespawnSettingsProvider : IMountDespawnSettingsProvider
{
    public bool IsEnabled => TaomSettings.Instance?.EnableDeadMountDespawn ?? true;

    public float DespawnDelaySeconds =>
        TaomSettings.Instance?.DeadMountDespawnDelaySeconds ?? DeadMountDespawnService.DefaultDelaySeconds;
}
