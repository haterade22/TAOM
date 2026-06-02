namespace TAOM.Features.CareerSystem.Abilities;

// Boundary for Mission.Current?.CurrentTime so the activation controller can be tested
// without a live mission. Returns 0 when no mission is active.
public interface IMissionTimeProvider
{
    float CurrentTime { get; }
}
