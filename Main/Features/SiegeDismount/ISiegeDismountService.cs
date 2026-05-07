namespace TAOM.Features.SiegeDismount;

public interface ISiegeDismountService
{
    /// <summary>
    /// Called when a mission begins. The behavior reads mission state and passes primitives in
    /// so the service is testable without a live <c>Mission</c>.
    /// </summary>
    void OnMissionStart(bool isSiegeBattle, string? sceneName);

    /// <summary>
    /// Called when a mission ends. Restores the mount if auto-remount was previously elected.
    /// </summary>
    void OnMissionEnd();
}
