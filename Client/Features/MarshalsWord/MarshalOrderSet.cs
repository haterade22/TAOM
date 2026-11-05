using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade.View.VisualOrders.OrderSets;

namespace TAOM.Client.Features.MarshalsWord;

/// <summary>Marshal's Word's set in the order panel: a string id of its own, and the icon of the base game's toggle set, which is a brush layer the panel can draw.</summary>
internal sealed class MarshalOrderSet : GenericVisualOrderSet
{
    internal const string SetId = "taom_order_type_marshal";

    internal const string SetIcon = "order_type_toggle";

    public MarshalOrderSet()
        : base(SetId, new TextObject("{=taom_marshal_panel_set}Marshal's Word"), useActiveOrderForIconId: false, useActiveOrderForName: false)
    {
    }

    public override string IconId => SetIcon;
}
