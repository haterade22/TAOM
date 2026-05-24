using System.Collections.Generic;
using System.Linq;

namespace BehaviorTrees.Nodes;

public abstract class BTNode
{
    public BTNode Parent;
    public int weight;

    protected BehaviorTree BaseTree { get; set; }
    public virtual AbstractDecorator? Decorator => null;
    internal BTStatus Status { get; set; } = BTStatus.NotExecuted;

    protected BTNode(int weight = 100)
    {
        this.weight = weight;
    }

    public abstract BTNode HandleExecute();

    public virtual bool ShouldExitTree() => false;

    public void AddDecoratorsListeners()
    {
        ((BTEventDecorator)Decorator).AddListener();
    }
}

public abstract class BTControlNode : BTNode
{
    protected int alreadyExecutedNodes = 0;
    protected List<BTNode> allChildren;
    protected List<BTNode> currentlyExecutableChildren = new List<BTNode>();
    protected bool hasRunChild = false;

    private readonly AbstractDecorator? _decorator;

    public string Name { get; internal set; }
    public override AbstractDecorator? Decorator => _decorator;
    protected bool IsWaitingASingleTime { get; set; } = false;

    protected BTControlNode(BehaviorTree tree, string name, AbstractDecorator? decorator = null, List<BTNode>? children = null, int weight = 100)
        : base(weight)
    {
        base.BaseTree = tree;
        Name = name;
        _decorator = decorator;
        allChildren = children ?? new List<BTNode>();
    }

    public override bool ShouldExitTree()
    {
        if (base.Status == BTStatus.WaitingForEvent)
            return true;
        if (IsWaitingASingleTime)
        {
            IsWaitingASingleTime = false;
            return true;
        }
        return false;
    }

    internal void ResetChildren()
    {
        foreach (var child in allChildren)
            child.Status = BTStatus.NotExecuted;
    }

    internal void AddChild(BTNode child) => allChildren.Add(child);

    public BTControlNode BuildNode(BTNode nextNode)
    {
        allChildren.Add(nextNode);
        return this;
    }

    protected void RemoveDecorator(BTEventDecorator decorator) => decorator.Remove();

    protected bool ExecutedAll() => alreadyExecutedNodes >= allChildren.Count;
}

public abstract class BTTask : BTNode
{
    private readonly BTListener? _isExecutedListener;

    protected BTTask(BTListener? listener = null, int weight = 100) : base(weight)
    {
        _isExecutedListener = listener;
    }

    public abstract BTTaskStatus Execute();

    public sealed override BTNode HandleExecute()
    {
        if (Decorator != null && !Decorator.Evaluate())
        {
            if (Decorator is BTEventDecorator)
                base.Status = BTStatus.WaitingForEvent;
            else
                base.Status = BTStatus.FinishedWithFalse;
            base.Status = BTStatus.Running;
            return this;
        }
        switch (Execute())
        {
            case BTTaskStatus.Running:
                base.Status = BTStatus.Running;
                break;
            case BTTaskStatus.FinishedWithFalse:
                base.Status = BTStatus.FinishedWithFalse;
                break;
            case BTTaskStatus.FinishedWithTrue:
                base.Status = BTStatus.FinishedWithTrue;
                break;
        }
        return Parent;
    }
}

public class Selector : BTControlNode
{
    private BTNode? _lastChild;
    protected List<BTNode> childrenWithTasks = new List<BTNode>();

    public Selector(BehaviorTree tree, string name, List<BTNode>? children = null, AbstractDecorator? decorator = null, int weight = 100)
        : base(tree, name, decorator, children, weight)
    {
    }

    private bool Prepare()
    {
        alreadyExecutedNodes = 0;
        currentlyExecutableChildren = new List<BTNode>();
        childrenWithTasks = new List<BTNode>();
        foreach (var child in allChildren)
        {
            if (child.Decorator == null)
                currentlyExecutableChildren.Add(child);
            else if (child.Decorator.Evaluate())
                currentlyExecutableChildren.Add(child);
            else if (child.Decorator is BTEventDecorator)
                childrenWithTasks.Add(child);
            else
                alreadyExecutedNodes++;
        }
        IsWaitingASingleTime = false;
        if (currentlyExecutableChildren.Count == 0 && childrenWithTasks.Count == 0)
        {
            base.Status = BTStatus.FinishedWithFalse;
            return false;
        }
        base.Status = BTStatus.Running;
        return true;
    }

    public override BTNode HandleExecute()
    {
        switch (base.Status)
        {
            case BTStatus.NotExecuted:
                if (Prepare())
                    return this;
                return Parent;
            case BTStatus.WaitingForEvent:
                return this;
            case BTStatus.ReceivedEvent:
                if (BaseTree.NodeReceivingEvent.Decorator.Evaluate())
                {
                    base.Status = BTStatus.Running;
                    RemoveDecorators();
                    childrenWithTasks.Remove(BaseTree.NodeReceivingEvent);
                    currentlyExecutableChildren.Add(BaseTree.NodeReceivingEvent);
                    _lastChild = BaseTree.NodeReceivingEvent;
                    return BaseTree.NodeReceivingEvent;
                }
                BaseTree.NodeReceivingEvent = null;
                base.Status = BTStatus.WaitingForEvent;
                return this;
            case BTStatus.Running:
                if (alreadyExecutedNodes >= allChildren.Count)
                {
                    base.Status = BTStatus.FinishedWithFalse;
                    ResetChildren();
                    return Parent;
                }
                if (currentlyExecutableChildren.Count == 0)
                {
                    foreach (var task in childrenWithTasks)
                    {
                        if (task.Decorator.Evaluate())
                        {
                            _lastChild = task;
                            currentlyExecutableChildren.Add(task);
                            base.Status = BTStatus.Running;
                            return task;
                        }
                        task.AddDecoratorsListeners();
                        base.Status = BTStatus.WaitingForEvent;
                    }
                    return this;
                }
                if (_lastChild != null)
                    return HandleChild();
                _lastChild = currentlyExecutableChildren.First();
                hasRunChild = true;
                return _lastChild;
            default:
                BTRegister.Logger?.LogMessage($"Error, the Selector {Name} is in a {base.Status} state, this should never happen!");
                ResetChildren();
                return Parent;
        }
    }

    private BTNode HandleChild()
    {
        switch (_lastChild.Status)
        {
            case BTStatus.FinishedWithTrue:
                IsWaitingASingleTime = false;
                base.Status = BTStatus.FinishedWithTrue;
                ResetChildren();
                _lastChild = null;
                return Parent;
            case BTStatus.FinishedWithFalse:
                IsWaitingASingleTime = false;
                alreadyExecutedNodes++;
                currentlyExecutableChildren.Remove(_lastChild);
                _lastChild = null;
                return this;
            case BTStatus.Running:
                base.Status = BTStatus.Running;
                if (hasRunChild)
                {
                    hasRunChild = false;
                    IsWaitingASingleTime = true;
                    return this;
                }
                hasRunChild = true;
                return _lastChild;
            default:
                base.Status = BTStatus.Running;
                return this;
        }
    }

    internal void RemoveDecorators()
    {
        foreach (var child in childrenWithTasks)
        {
            var decorator = child.Decorator;
            if (decorator is BTEventDecorator eventDecorator)
                RemoveDecorator(eventDecorator);
        }
    }
}

public class Sequence : BTControlNode
{
    public Sequence(BehaviorTree tree, string name, List<BTNode>? children = null, AbstractDecorator? decorator = null, int weight = 100)
        : base(tree, name, decorator, children, weight)
    {
    }

    private void Prepare()
    {
        alreadyExecutedNodes = 0;
        IsWaitingASingleTime = false;
    }

    public override BTNode HandleExecute()
    {
        switch (base.Status)
        {
            case BTStatus.NotExecuted:
                Prepare();
                base.Status = BTStatus.Running;
                return this;
            case BTStatus.WaitingForEvent:
                return this;
            case BTStatus.ReceivedEvent:
                if (BaseTree.NodeReceivingEvent.Decorator.Evaluate())
                {
                    base.Status = BTStatus.Running;
                    RemoveDecorator(BaseTree.NodeReceivingEvent.Decorator as BTEventDecorator);
                    return BaseTree.NodeReceivingEvent;
                }
                BaseTree.NodeReceivingEvent = null;
                base.Status = BTStatus.WaitingForEvent;
                return this;
            case BTStatus.Running:
                return HandleChild(allChildren[alreadyExecutedNodes]);
            default:
                BTRegister.Logger?.LogMessage($"Error, the Sequence {Name} is in a {base.Status} state, this should never happen!");
                ResetChildren();
                return Parent;
        }
    }

    private BTNode? HandleDecorators(BTNode currentChild)
    {
        var decorator = currentChild.Decorator;
        if (decorator != null && !decorator.Evaluate())
        {
            if (decorator is BTEventDecorator)
            {
                AddDecoratorsListeners();
                base.Status = BTStatus.WaitingForEvent;
                return this;
            }
            base.Status = BTStatus.FinishedWithFalse;
            ResetChildren();
            return Parent;
        }
        return null;
    }

    private BTNode HandleChild(BTNode currentChild)
    {
        switch (currentChild.Status)
        {
            case BTStatus.NotExecuted:
            {
                var nextNode = HandleDecorators(currentChild);
                if (nextNode == null)
                {
                    hasRunChild = true;
                    base.Status = BTStatus.Running;
                    return currentChild;
                }
                return nextNode;
            }
            case BTStatus.Running:
                base.Status = BTStatus.Running;
                if (hasRunChild)
                {
                    hasRunChild = false;
                    IsWaitingASingleTime = true;
                    return this;
                }
                hasRunChild = true;
                return currentChild;
            case BTStatus.FinishedWithTrue:
                IsWaitingASingleTime = false;
                alreadyExecutedNodes++;
                if (alreadyExecutedNodes >= allChildren.Count)
                {
                    base.Status = BTStatus.FinishedWithTrue;
                    ResetChildren();
                    return Parent;
                }
                return this;
            case BTStatus.FinishedWithFalse:
                IsWaitingASingleTime = false;
                base.Status = BTStatus.FinishedWithFalse;
                ResetChildren();
                return Parent;
            case BTStatus.WaitingForEvent:
                if (currentChild.Decorator.Evaluate())
                {
                    base.Status = BTStatus.Running;
                    return currentChild;
                }
                return this;
            default:
                ResetChildren();
                return Parent;
        }
    }
}
