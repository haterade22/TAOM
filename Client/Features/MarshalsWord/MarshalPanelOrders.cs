using System.Collections.Generic;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.ViewModelCollection.Order.Visual;
using TAOM.Adapters;
using TAOM.Features.MarshalsWord;
using TAOM.Features.MarshalsWord.Domain;

namespace TAOM.Client.Features.MarshalsWord;

/// <summary>One entry of Marshal's Word's panel set, with TAOM's own string id and one of the base game's icons, whose click stores the key's press for the selected companies.</summary>
internal abstract class MarshalPanelOrder : VisualOrder
{
    private readonly string _iconId;
    private readonly PanelAction _action;

    protected MarshalPanelOrder(string stringId, string iconId, PanelAction action)
        : base(stringId)
    {
        _iconId = iconId;
        _action = action;
    }

    public override bool IsTargeted() => false;

    public override void ExecuteOrder(OrderController orderController, VisualOrderExecutionParameters executionParameters)
    {
        if (orderController != MarshalHooks.PlayerController)
            return;
        var selected = orderController.SelectedFormations;
        var companies = new List<int>(selected.Count);
        for (var i = 0; i < selected.Count; i++)
            companies.Add(MarshalOrderAdapter.CompanyOf(selected[i]));
        MarshalHooks.PanelPress(_action, companies);
    }

    protected override string GetIconId() => _iconId;
}

/// <summary>Cycles the selected archers' fire: at will, on the word, Massed (key 543).</summary>
internal sealed class MarshalFireModeOrder : MarshalPanelOrder
{
    public MarshalFireModeOrder()
        : base("taom_marshal_fire", "order_toggle_fire", PanelAction.Fire)
    {
    }

    public override TextObject GetName(OrderController orderController) => new("{=taom_marshal_panel_fire}Archers' fire");

    protected override bool? OnGetFormationHasOrder(Formation formation) => MarshalFireTable.ModeOf(MarshalOrderAdapter.CompanyOf(formation)) != FireMode.Free;
}

/// <summary>Gives the word to the selected archers who hold a drawn bow (key 544).</summary>
internal sealed class MarshalWordOrder : MarshalPanelOrder
{
    public MarshalWordOrder()
        : base("taom_marshal_word", "order_movement_skirmish", PanelAction.Word)
    {
    }

    public override TextObject GetName(OrderController orderController) => new("{=taom_marshal_panel_word}Give the word");

    protected override bool? OnGetFormationHasOrder(Formation formation) => null;
}

/// <summary>Toggles hold the line for the selected companies (key 545).</summary>
internal sealed class MarshalHoldLineOrder : MarshalPanelOrder
{
    public MarshalHoldLineOrder()
        : base("taom_marshal_hold", "order_form_close", PanelAction.Hold)
    {
    }

    public override TextObject GetName(OrderController orderController) => new("{=taom_marshal_panel_hold}Hold the line");

    protected override bool? OnGetFormationHasOrder(Formation formation) => MarshalFireTable.HoldOf(MarshalOrderAdapter.CompanyOf(formation));
}
