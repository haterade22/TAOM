using BehaviorTrees;

namespace BehaviorTreeWrapper.AbstractDecoratorsListeners;

public class BannerlordBTTickListener : BTListener
{
    public double SecondsTillEvent { get; }

    internal BannerlordBTTickListener(double secondsTillEvent, BehaviorTree tree, BTEventDecorator notifies)
        : base(tree, notifies)
    {
        SecondsTillEvent = secondsTillEvent;
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
