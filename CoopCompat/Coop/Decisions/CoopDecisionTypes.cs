using System;
using GameInterface.Services.Kingdoms;
using TaleWorlds.CampaignSystem.Election;

namespace TAOM.CoopCompat.Decisions;

/// <summary>
/// Coop's hook for kingdom decision types a mod adds (#1346). Coop sends a registered type to every
/// client so players can vote on it; an unregistered type is decided by the server AI. Coop refuses a
/// join when the host and the player registered different types, so every peer registers the same ones
/// before it hosts or joins.
/// </summary>
public static class CoopDecisionTypes
{
    public static void Register<TDecision>(Func<TDecision, byte[]> write, Func<byte[], TDecision> read)
        where TDecision : KingdomDecision =>
        KingdomDecisionTypeRegistry.Register(write, read);
}
