using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BehaviorTrees.Nodes;

// TAOM-owned port of the BehaviorTrees library, originally shipped as a vendored binary at
// Main/_Module/bin/Win64_Shipping_Client/BehaviorTrees.dll (no upstream source repo).
// Decompiled via ilspycmd 2026-05-24 and inlined here so the whole stack ships as a single
// TAOM.dll — no separate libraries to keep in sync.
//
// Cleaned from the decompile: dropped ILSpy artifacts and rewrote C# 12 primary-constructor
// syntax to plain ctors (TAOM compiles at LangVersion=10). No behavior change.

namespace BehaviorTrees;

internal enum BTStatus
{
    Running,
    FinishedWithFalse,
    FinishedWithTrue,
    WaitingForEvent,
    ReceivedEvent,
    NotExecuted
}

public enum BTTaskStatus
{
    Running,
    FinishedWithFalse,
    FinishedWithTrue
}

public abstract class BehaviorTree
{
    public readonly int _rootEvaluationDelay = 2000;

    public bool IsRunning { get; private set; } = true;
    public bool ShouldRunNextTick { get; internal set; }

    internal BTNode CurrentNode { get; set; }
    internal BTNode RootNode { get; set; }
    internal BTNode? NodeReceivingEvent { get; set; }

    internal List<BTControlNode> ControlNodes { get; } = new List<BTControlNode>();

    public BehaviorTree(int rootEvaluationDelay = 2000)
    {
        _rootEvaluationDelay = rootEvaluationDelay;
    }

    public void RunTree()
    {
        if (!IsRunning)
            return;
        try
        {
            if (CurrentNode.Status == BTStatus.NotExecuted)
                CurrentNode.HandleExecute();
            if (CurrentNode.ShouldExitTree())
                return;
            do
            {
                CurrentNode = CurrentNode.HandleExecute();
                if (CurrentNode.ShouldExitTree())
                    return;
            } while (CurrentNode != RootNode);
            if (CurrentNode == RootNode && RootNode is BTControlNode controlNode)
            {
                controlNode.ResetChildren();
                CurrentNode.Status = BTStatus.NotExecuted;
            }
        }
        catch (Exception ex)
        {
            BTRegister.Logger?.LogMessage("Exception running the behavior tree: " + ex.Message);
            IsRunning = false;
        }
    }

    protected static BehaviorTreeBuilder<TTree> StartBuildingTree<TTree>(TTree tree) where TTree : BehaviorTree
    {
        return new BehaviorTreeBuilder<TTree>(tree);
    }

    public static BehaviorTree? BuildTree(object[] objects)
    {
        throw new NotImplementedException("Derived classes must implement the BuildTree method.");
    }
}

public class IncorrectParametersException : Exception
{
    public IncorrectParametersException(Type tree)
        : base("Can not create a tree " + tree.Name + ", received incorrect parameters")
    {
    }
}

public class BehaviorTreeBuilder<TTree> where TTree : BehaviorTree
{
    internal class AlreadyInRootException : Exception
    {
        public AlreadyInRootException(Type type) : base("Error building the tree " + type.Name + " in method, can not go Up, already in root.") { }
    }

    internal class MissingTreeBlackBoardException : Exception
    {
        public MissingTreeBlackBoardException(BehaviorTree treeBeingBuild, Type interfaceType, object node)
            : base("error creating tree " + treeBeingBuild.GetType().Name + ". The tree does not implement the interface " + interfaceType.Name + ", but " + node.GetType().Name + " does.") { }
    }

    internal class IncorrectPropertyException : Exception
    {
        public IncorrectPropertyException(BehaviorTree source, PropertyInfo property)
            : base("error creating tree " + source.GetType().Name + ". The property " + property.Name + " can not be assigned. Is it missing get or set methods?") { }
    }

    private readonly Stack<BTControlNode> _previousNodes;
    private BTControlNode _currentNode;

    private TTree TreeBeingBuild { get; }

    internal BehaviorTreeBuilder(TTree tree)
    {
        TreeBeingBuild = tree;
        _currentNode = new Sequence(TreeBeingBuild, typeof(TTree).Name);
        _previousNodes = new Stack<BTControlNode>();
        TreeBeingBuild.RootNode = _currentNode;
        TreeBeingBuild.CurrentNode = _currentNode;
        _currentNode.Parent = _currentNode;
    }

    private void SetINotifiable(object obj)
    {
        if (obj is IBTNotifiable notifiable)
        {
            notifiable.Tree = TreeBeingBuild;
            notifiable.CreateListener();
        }
    }

    private void SetDecorator<DDecorator>(DDecorator decorator) where DDecorator : AbstractDecorator
    {
        CopyAllInterfacesProperties(decorator);
        SetINotifiable(decorator);
        decorator.NodeBeingDecoracted = _currentNode;
    }

    public BehaviorTreeBuilder<TTree> AddSequence<DDecorator>(string name, DDecorator? decorator = null, int weight = 100) where DDecorator : AbstractDecorator
    {
        var sequence = new Sequence(TreeBeingBuild, name, new List<BTNode>(), decorator, weight);
        _currentNode.AddChild(sequence);
        _previousNodes.Push(_currentNode);
        sequence.Parent = _currentNode;
        _currentNode = sequence;
        if (decorator != null)
            SetDecorator(decorator);
        return this;
    }

    public BehaviorTreeBuilder<TTree> AddSequence(string name, int weight = 100)
        => AddSequence<BTEventDecorator>(name, null, weight);

    public BehaviorTreeBuilder<TTree> AddSelector<DDecorator>(string name, DDecorator? decorator = null, int weight = 100) where DDecorator : AbstractDecorator
    {
        var selector = new Selector(TreeBeingBuild, name, new List<BTNode>(), decorator, weight);
        _currentNode.AddChild(selector);
        _previousNodes.Push(_currentNode);
        selector.Parent = _currentNode;
        _currentNode = selector;
        if (decorator != null)
            SetDecorator(decorator);
        return this;
    }

    public BehaviorTreeBuilder<TTree> AddSelector(string name, int weight = 100)
        => AddSelector<BTEventDecorator>(name, null, weight);

    public BehaviorTreeBuilder<TTree> Up()
    {
        if (_previousNodes.Count == 0)
            throw new AlreadyInRootException(typeof(TTree));
        _currentNode = _previousNodes.Pop();
        return this;
    }

    public BehaviorTreeBuilder<TTree> AddTask<TTask>(TTask task) where TTask : BTTask
    {
        CopyAllInterfacesProperties(task);
        _currentNode.AddChild(task);
        task.Parent = _currentNode;
        return this;
    }

    private void CopyAllInterfacesProperties<TAssignableObject>(TAssignableObject node)
    {
        var nodeInterfaces = typeof(TAssignableObject).GetInterfaces();
        var treeInterfaces = typeof(TTree).GetInterfaces();
        foreach (var type in nodeInterfaces)
        {
            if (typeof(IBTBlackboard).IsAssignableFrom(type) && type != typeof(IBTBlackboard))
            {
                if (!treeInterfaces.Contains(type))
                    throw new MissingTreeBlackBoardException(TreeBeingBuild, type, node);
                CopyInterfaceProperties(TreeBeingBuild, node, type);
            }
        }
    }

    private static void CopyInterfaceProperties(BehaviorTree source, object target, Type interfaceType)
    {
        foreach (var property in interfaceType.GetProperties())
        {
            if (!property.CanRead || !property.CanWrite)
                throw new IncorrectPropertyException(source, property);
            var value = property.GetValue(source);
            property.SetValue(target, value);
        }
    }

    public BehaviorTreeBuilder<TTree> AddConstantEventListener<CEL>(CEL listener) where CEL : ConstantEventListener
    {
        CopyAllInterfacesProperties(listener);
        SetINotifiable(listener);
        return this;
    }

    public TTree? Finish() => TreeBeingBuild;
}

public interface IBTBlackboard
{
}

public class BTBlackboardValue<TValue>
{
    private TValue _value;

    public BTBlackboardValue(TValue value)
    {
        _value = value;
    }

    public TValue GetValue() => _value;
    public void SetValue(TValue value) => _value = value;
}

public abstract class BTListener
{
    public BehaviorTree Tree { get; private set; }
    public IBTNotifiable Notifies { get; private set; }

    protected BTListener(BehaviorTree tree, IBTNotifiable notifies)
    {
        Notifies = notifies;
        Tree = tree;
    }

    public virtual void Subscribe() { }
    public abstract void UnSubscribe();
    public void Notify(object[] data) => Notifies.HandleNotification(data);
}

public static class BTRegister
{
    private static readonly Dictionary<string, Func<object[], BehaviorTree?>> RegisteredBuilders
        = new Dictionary<string, Func<object[], BehaviorTree?>>();

    public static ILogger? Logger { get; private set; }

    public static void AddLogger(ILogger logger) => Logger = logger;

    public static void RegisterClass(string className, Func<object[], BehaviorTree?> factory)
    {
        if (!RegisteredBuilders.ContainsKey(className))
            RegisteredBuilders[className] = factory;
    }

    public static BehaviorTree? Build(string className, params object[] args)
    {
        if (RegisteredBuilders.TryGetValue(className, out var factory))
            return factory(args);
        throw new ArgumentException("Class '" + className + "' is not registered.");
    }
}

public abstract class ConstantEventListener : IBTNotifiable
{
    public BTListener Listener { get; set; }
    public BehaviorTree Tree { get; set; }

    public abstract void CreateListener();
    public void HandleNotification(object[] data) => Notify(data);
    public abstract void Notify(object[] data);
}

public abstract class AbstractDecorator
{
    public BTNode NodeBeingDecoracted { get; internal set; }

    protected internal AbstractDecorator() { }

    public abstract bool Evaluate();

    internal abstract bool HandleEvaluation();
}

public interface IBTNotifiable
{
    BTListener Listener { get; set; }
    BehaviorTree Tree { get; set; }
    void HandleNotification(object[] data);
    void CreateListener();
}

public abstract class BTEventDecorator : AbstractDecorator, IBTNotifiable
{
    public BTListener Listener { get; set; }
    public BehaviorTree Tree { get; set; }

    public abstract void Notify(object[] data);

    public void HandleNotification(object[] data)
    {
        Notify(data);
        NodeBeingDecoracted.Parent.Status = BTStatus.ReceivedEvent;
        Tree.NodeReceivingEvent = NodeBeingDecoracted;
        Tree.ShouldRunNextTick = true;
    }

    public void AddListener()
    {
        NodeBeingDecoracted.Parent.Status = BTStatus.WaitingForEvent;
        Listener.Subscribe();
    }

    public void Remove() => Listener.UnSubscribe();

    internal override bool HandleEvaluation()
    {
        if (Evaluate())
            return true;
        AddListener();
        return false;
    }

    protected BTEventDecorator() { }

    public abstract void CreateListener();
}

public abstract class BTReturnFalseDecorator : AbstractDecorator
{
    internal override bool HandleEvaluation() => Evaluate();
}

public interface ILogger
{
    void LogMessage(string message);
}
