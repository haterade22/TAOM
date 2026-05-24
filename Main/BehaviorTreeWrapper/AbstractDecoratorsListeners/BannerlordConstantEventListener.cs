using BehaviorTrees;

namespace BehaviorTreeWrapper.AbstractDecoratorsListeners;

public abstract class BannerlordConstantEventListener : ConstantEventListener
{
    private readonly SubscriptionPossibilities _subscribesTo;

    public BannerlordConstantEventListener(SubscriptionPossibilities subscribesTo)
    {
        _subscribesTo = subscribesTo;
    }

    public abstract override void Notify(object[] data);

    public override void CreateListener()
    {
        base.Listener = new BannerlordBTListener(_subscribesTo, base.Tree, this);
        base.Listener.Subscribe();
    }
}
