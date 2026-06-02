using TaleWorlds.MountAndBlade;

namespace TAOM.Features.CareerSystem.Abilities;

public class MissionTimeProvider : IMissionTimeProvider
{
    public float CurrentTime => Mission.Current?.CurrentTime ?? 0f;
}
