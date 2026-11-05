using SandBox.Missions.MissionLogics.Hideout;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.VisualOrders;
using TaleWorlds.MountAndBlade.View.VisualOrders.OrderSets;
using TaleWorlds.MountAndBlade.ViewModelCollection.Order.Visual;
using TAOM.Features.MarshalsWord;
using TAOM.Features.MarshalsWord.Domain;

namespace TAOM.Client.Features.MarshalsWord;

/// <summary>The base game's order list with one more set holding Marshal's Word's three orders, offered only in a live Marshal's Word mission that is not a hideout.</summary>
internal sealed class MarshalVisualOrderProvider : DefaultVisualOrderProvider
{
    // TAOM loads after Sandbox, so this provider answers before Sandbox's hideout one, whose set leaves Advance out on purpose (Main/_Module/SubModule.xml:24).
    public override bool IsAvailable() =>
        base.IsAvailable() && MarshalHooks.IsLive && !Mission.Current.HasMissionBehavior<HideoutMissionController>();

    public override MBReadOnlyList<VisualOrderSet> GetOrders()
    {
        var sets = new MBList<VisualOrderSet>(base.GetOrders());
        var lastIsReturn = sets.Count > 0 && sets[sets.Count - 1] is SingleVisualOrderSet { Order: ReturnVisualOrder };
        sets.Insert(MarshalPanelRules.SetIndex(BannerlordConfig.OrderLayoutType, sets.Count, lastIsReturn), NewSet());
        return sets;
    }

    private static MarshalOrderSet NewSet()
    {
        var set = new MarshalOrderSet();
        set.AddOrder(new MarshalFireModeOrder());
        set.AddOrder(new MarshalWordOrder());
        set.AddOrder(new MarshalHoldLineOrder());
        set.AddOrder(new ReturnVisualOrder());
        return set;
    }
}
