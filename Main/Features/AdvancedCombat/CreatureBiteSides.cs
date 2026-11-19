using TAOM.Adapters;

namespace TAOM.Features.AdvancedCombat;

/// <summary>
/// Whether a creature bite spares its target (#594), shared by the spider and warg bites. A ridden mount is its
/// rider's side. The engine gives a mount no team, so a riderless mount's side is the one the adapter resolves
/// from its last rider; a riderless mount whose side is still unknown is spared, never bitten.
/// </summary>
internal static class CreatureBiteSides
{
    /// <param name="biterSide">The agent whose team stands for the biter (the spider's rider, or the spider), or
    /// null when the biter compares no team (a riderless warg).</param>
    /// <param name="target">The agent the bite reached.</param>
    public static bool Spares(IAgentAdapter? biterSide, IAgentAdapter target)
    {
        IAgentAdapter? rider = target.IsMount ? target.RiderAgent : null;
        if (target.IsMount && rider == null && !target.HasKnownSide) return true;
        if (biterSide == null) return false;
        return biterSide.IsSameTeam(rider ?? target);
    }
}
