using TaleWorlds.CampaignSystem;
using TAOM.Features.SiegeDismount.Models;

namespace TAOM.Adapters;

/// <summary>
/// Concrete <see cref="IPartyMountInventoryAdapter"/> backed by the main party's <c>ItemRoster</c>.
/// Uses the <c>AddToCounts(EquipmentElement, int)</c> overload so the inventory entry preserves
/// any <c>ItemModifier</c> (durability, quality prefix) on the captured mount/harness — the bare
/// <c>AddToCounts(ItemObject, int)</c> overload would drop the modifier. Reads
/// <c>Campaign.Current?.MainParty</c>, not <c>MobileParty.MainParty</c>, which is
/// <c>Campaign.Current.MainParty</c> and throws with no campaign (a Custom Battle).
/// </summary>
public class PartyMountInventoryAdapter : IPartyMountInventoryAdapter
{
    public void Deposit(IMountSnapshot snapshot)
    {
        var roster = Campaign.Current?.MainParty?.ItemRoster;
        if (roster == null || snapshot == null) return;
        if (snapshot is not MountSnapshot concrete) return;

        if (!concrete.Mount.IsEmpty)
            roster.AddToCounts(concrete.Mount, 1);
        if (!concrete.Harness.IsEmpty)
            roster.AddToCounts(concrete.Harness, 1);
    }

    public void Withdraw(IMountSnapshot snapshot)
    {
        var roster = Campaign.Current?.MainParty?.ItemRoster;
        if (roster == null || snapshot == null) return;
        if (snapshot is not MountSnapshot concrete) return;

        if (!concrete.Mount.IsEmpty)
            roster.AddToCounts(concrete.Mount, -1);
        if (!concrete.Harness.IsEmpty)
            roster.AddToCounts(concrete.Harness, -1);
    }
}
