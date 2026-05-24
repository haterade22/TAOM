using BehaviorTrees;

namespace BehaviorTreeWrapper.AbstractDecoratorsListeners;

public class BannerlordBTListener : BTListener
{
    public SubscriptionPossibilities SubscribesTo { get; set; }

    internal BannerlordBTListener(SubscriptionPossibilities subscribesTo, BehaviorTree tree, IBTNotifiable notifies)
        : base(tree, notifies)
    {
        SubscribesTo = subscribesTo;
    }

    public override void Subscribe()
    {
        base.Subscribe();
        BehaviorTreeBannerlordWrapper.Instance.Subscribe(this);
    }

    public override void UnSubscribe()
    {
        BehaviorTreeBannerlordWrapper.Instance.UnSubscribe(this);
    }
}
