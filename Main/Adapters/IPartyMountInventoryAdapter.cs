using TAOM.Features.SiegeDismount.Models;

namespace TAOM.Adapters;

/// <summary>
/// Adds and removes mount items from the player main party's <c>ItemRoster</c>.
/// Operates on opaque <see cref="IMountSnapshot"/> tokens so services never see <c>ItemObject</c> (ADR-007).
/// </summary>
public interface IPartyMountInventoryAdapter
{
    void Deposit(IMountSnapshot snapshot);
    void Withdraw(IMountSnapshot snapshot);
}
