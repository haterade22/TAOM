namespace TAOM.Features.SiegePropDiagnostics.Models;

/// <summary>
/// Which family of in-mission resupply prop a snapshot describes. The two behave differently
/// enough that the diagnosis must branch on it:
///
/// <list type="bullet">
/// <item><see cref="RockPile"/> (<c>StonePile</c>) hands out a concrete item, so it needs a
/// resolvable <c>GivenItemID</c> and <c>ammopickup</c>-tagged standing points, and it runs out
/// of ammo.</item>
/// <item><see cref="AmmoBarrel"/> (<c>ArrowBarrel</c>/<c>JavelinBarrel</c>) hands out no item at
/// all — it tops the agent's own slots up — so it has no item id, no ammo counter, and vanilla
/// deliberately tags none of its points <c>ammopickup</c>.</item>
/// </list>
/// </summary>
public enum SiegePropKind
{
    Other = 0,
    RockPile = 1,
    AmmoBarrel = 2,
}
