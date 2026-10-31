namespace TAOM.Features.FordsOfWar.Domain;

/// <summary>An opaque unit id handed out by the engine layer. Never an Agent.Index: indices recycle.</summary>
internal readonly record struct AgentKey(int Value);
