using System;
using DryIoc;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace TAOM.Composition;

/// <summary>A Harmony patch category a module applies, and the phase it must apply in.</summary>
internal sealed class PatchCategoryDecl
{
    internal PatchCategoryDecl(string category, ApplyPhase phase)
    {
        if (string.IsNullOrWhiteSpace(category))
            throw new ArgumentException("A patch category needs a name.", nameof(category));
        Category = category;
        Phase = phase;
    }

    internal string Category { get; }

    internal ApplyPhase Phase { get; }
}

/// <summary>A campaign behavior a module adds at campaign start, and how to build it.</summary>
internal sealed class CampaignBehaviorDecl
{
    private CampaignBehaviorDecl(Type behaviorType, Func<IResolver, CampaignBehaviorBase> create)
    {
        BehaviorType = behaviorType;
        Create = create;
    }

    internal Type BehaviorType { get; }

    internal Func<IResolver, CampaignBehaviorBase> Create { get; }

    internal static CampaignBehaviorDecl Of<TBehavior>(Func<IResolver, TBehavior> create)
        where TBehavior : CampaignBehaviorBase =>
        new(typeof(TBehavior), create);
}

/// <summary>Which game starter a model is added to.</summary>
internal enum ModelTarget
{
    /// <summary>The CampaignGameStarter in OnGameStart, after SandBox's defaults.</summary>
    Campaign,

    /// <summary>The BasicGameStarter Custom Battle (and the editor's test battle) hands OnGameStart.</summary>
    CustomBattle,
}

/// <summary>
/// A game model a module adds, keyed by its engine slot. Added through the generic
/// <c>AddModel&lt;TSlot&gt;</c>, which chains the slot's previous model as BaseModel, exactly as the
/// hand-written AddModel calls in SubModule resolve today.
/// </summary>
internal sealed class GameModelDecl
{
    private GameModelDecl(Type slotType, Type modelType, ModelTarget target,
        Func<IResolver, GameModel> create, Action<IGameStarter, GameModel> add)
    {
        SlotType = slotType;
        ModelType = modelType;
        Target = target;
        Create = create;
        Add = add;
    }

    internal Type SlotType { get; }

    internal Type ModelType { get; }

    internal ModelTarget Target { get; }

    internal Func<IResolver, GameModel> Create { get; }

    internal Action<IGameStarter, GameModel> Add { get; }

    internal static GameModelDecl Of<TSlot, TModel>(ModelTarget target, Func<IResolver, TModel> create)
        where TSlot : GameModel
        where TModel : MBGameModel<TSlot> =>
        new(typeof(TSlot), typeof(TModel), target,
            create,
            (starter, model) => starter.AddModel<TSlot>((MBGameModel<TSlot>)model));
}

/// <summary>A mission behavior a module adds at every mission start, and how to build it.</summary>
internal sealed class MissionBehaviorDecl
{
    private MissionBehaviorDecl(Type behaviorType, Func<Mission, IResolver, MissionBehavior> create)
    {
        BehaviorType = behaviorType;
        Create = create;
    }

    internal Type BehaviorType { get; }

    internal Func<Mission, IResolver, MissionBehavior> Create { get; }

    internal static MissionBehaviorDecl Of<TBehavior>(Func<Mission, IResolver, TBehavior> create)
        where TBehavior : MissionBehavior =>
        new(typeof(TBehavior), create);
}
