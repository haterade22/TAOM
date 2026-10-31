namespace TAOM.Features.FordsOfWar.Diagnostics;

/// <summary>The match numbers the verdict rules compare against, read once from the config and the layout.</summary>
internal sealed record FordsMatchFacts(float FireRange, float AttackRange, float CaptureRadius, int Lanes, int MaxLivePerSide, int MaxAlivePerSide,
    float BaseDistance = 0f, int AgentLimit = 0);
