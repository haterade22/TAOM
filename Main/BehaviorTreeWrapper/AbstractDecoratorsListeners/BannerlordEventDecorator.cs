using BehaviorTrees;

namespace BehaviorTreeWrapper.AbstractDecoratorsListeners;

public abstract class BannerlordEventDecorator : BTEventDecorator
{
    private readonly SubscriptionPossibilities _subscribesTo;

    protected BannerlordEventDecorator(SubscriptionPossibilities subscribesTo)
    {
        _subscribesTo = subscribesTo;
    }

    public sealed override void CreateListener()
    {
        base.Listener = new BannerlordBTListener(_subscribesTo, base.Tree, this);
    }
}

public abstract class BannerlordNoWaitDecorator : BTReturnFalseDecorator
{
}

public abstract class BannerlordTickTimedDecorator : BTEventDecorator
{
    private readonly double _secondsTillEvent;

    protected BannerlordTickTimedDecorator(double secondsTillEvent = 0.66667)
    {
        _secondsTillEvent = secondsTillEvent;
    }

    public sealed override void CreateListener()
    {
        base.Listener = new BannerlordBTTickListener(_secondsTillEvent, base.Tree, this);
    }
}
