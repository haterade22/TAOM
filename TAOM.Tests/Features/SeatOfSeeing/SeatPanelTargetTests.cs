using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.SeatOfSeeing.Domain;

namespace TAOM.Tests.Features.SeatOfSeeing;

/// <summary>A widget tree as the shield's rule sees it: a rectangle, a shown flag, and the panel kind of the view model it is bound to.</summary>
internal sealed class PanelNode
{
    internal PanelNode(string name, float left, float top, float right, float bottom, SeatPanelKind kind = SeatPanelKind.None, object? target = null, bool shown = true)
    {
        Name = name;
        Left = left;
        Top = top;
        Right = right;
        Bottom = bottom;
        Kind = kind;
        Target = target;
        Shown = shown;
    }

    internal string Name { get; }
    internal float Left { get; }
    internal float Top { get; }
    internal float Right { get; }
    internal float Bottom { get; }
    internal SeatPanelKind Kind { get; }
    internal object? Target { get; }
    internal bool Shown { get; }
    internal List<PanelNode> Children { get; } = new();

    internal PanelNode With(params PanelNode[] children)
    {
        Children.AddRange(children);
        return this;
    }
}

internal sealed class PanelTree : ISeatPanelTree<PanelNode>
{
    internal int Classified;

    public bool Shown(PanelNode node) => node.Shown;

    public bool Contains(PanelNode node, float x, float y) => x >= node.Left && x <= node.Right && y >= node.Top && y <= node.Bottom;

    public int ChildCount(PanelNode node) => node.Children.Count;

    public PanelNode Child(PanelNode node, int index) => node.Children[index];

    public SeatPanelKind Classify(PanelNode node, out object? target)
    {
        Classified++;
        target = node.Target;
        return node.Kind;
    }
}

/// <summary>The topmost panel target under the pointer (Marshal's Word stage M7, Seat build-spec 6.1).</summary>
[TestClass]
public class SeatPanelTargetTests
{
    private static readonly object CardOne = new();
    private static readonly object CardTwo = new();
    private static readonly object SetOne = new();

    private static SeatPanelHit At(PanelNode root, float x, float y, PanelTree? tree = null) => SeatPanelTarget.Topmost(tree ?? new PanelTree(), root, x, y);

    [TestMethod]
    public void Pointer_OverACard_ReturnsItsKindAndViewModel()
    {
        var root = new PanelNode("root", 0, 0, 1000, 1000).With(new PanelNode("card", 10, 10, 100, 200, SeatPanelKind.Card, CardOne));

        var hit = At(root, 50, 50);

        Assert.AreEqual(SeatPanelKind.Card, hit.Kind);
        Assert.AreSame(CardOne, hit.Target);
    }

    [TestMethod]
    public void Pointer_OverNothingBoundToThePanel_ReturnsNone()
    {
        var root = new PanelNode("root", 0, 0, 1000, 1000).With(new PanelNode("card", 10, 10, 100, 200, SeatPanelKind.Card, CardOne));

        Assert.AreEqual(SeatPanelKind.None, At(root, 500, 500).Kind);
    }

    [TestMethod]
    public void WhereTwoTargetsOverlap_TheLaterSiblingWins()
    {
        var root = new PanelNode("root", 0, 0, 1000, 1000).With(
            new PanelNode("under", 0, 0, 100, 100, SeatPanelKind.Card, CardOne),
            new PanelNode("over", 50, 50, 150, 150, SeatPanelKind.Card, CardTwo));

        Assert.AreSame(CardTwo, At(root, 60, 60).Target);
        Assert.AreSame(CardOne, At(root, 10, 10).Target);
    }

    [TestMethod]
    public void AnUnboundChildOfATarget_DoesNotBlockIt()
    {
        var card = new PanelNode("card", 0, 0, 100, 100, SeatPanelKind.Card, CardOne).With(new PanelNode("label", 10, 10, 90, 40));
        var root = new PanelNode("root", 0, 0, 1000, 1000).With(card);

        Assert.AreSame(CardOne, At(root, 20, 20).Target);
    }

    [TestMethod]
    public void AChildThatSharesItsParentsViewModel_IsTheSameTarget()
    {
        var card = new PanelNode("card", 0, 0, 100, 100, SeatPanelKind.Card, CardOne).With(new PanelNode("icon", 10, 10, 90, 40, SeatPanelKind.Card, CardOne));
        var root = new PanelNode("root", 0, 0, 1000, 1000).With(card);

        var hit = At(root, 20, 20);

        Assert.AreEqual(SeatPanelKind.Card, hit.Kind);
        Assert.AreSame(CardOne, hit.Target);
    }

    [TestMethod]
    public void AHiddenNode_AndEverythingBelowIt_IsSkipped()
    {
        var hidden = new PanelNode("hiddenBar", 0, 0, 500, 500, SeatPanelKind.OrderSet, SetOne, shown: false)
            .With(new PanelNode("item", 0, 0, 500, 500, SeatPanelKind.OrderItem, CardTwo));
        var root = new PanelNode("root", 0, 0, 1000, 1000).With(hidden);

        Assert.AreEqual(SeatPanelKind.None, At(root, 100, 100).Kind);
    }

    [TestMethod]
    public void AHiddenOverlay_LetsTheTargetUnderItAnswer()
    {
        var root = new PanelNode("root", 0, 0, 1000, 1000).With(
            new PanelNode("card", 0, 0, 100, 100, SeatPanelKind.Card, CardOne),
            new PanelNode("overlay", 0, 0, 100, 100, SeatPanelKind.Card, CardTwo, shown: false));

        Assert.AreSame(CardOne, At(root, 20, 20).Target);
    }

    [TestMethod]
    public void AChildOutsideItsParentsRectangle_StillAnswers()
    {
        var bar = new PanelNode("bar", 0, 0, 100, 100).With(new PanelNode("item", 200, 0, 260, 60, SeatPanelKind.OrderItem, CardTwo));
        var root = new PanelNode("root", 0, 0, 1000, 1000).With(bar);

        Assert.AreSame(CardTwo, At(root, 220, 20).Target);
    }

    [TestMethod]
    public void AHiddenRoot_AnswersNone()
    {
        var root = new PanelNode("root", 0, 0, 1000, 1000, shown: false).With(new PanelNode("card", 0, 0, 100, 100, SeatPanelKind.Card, CardOne));

        Assert.AreEqual(SeatPanelKind.None, At(root, 20, 20).Kind);
    }

    [DataTestMethod]
    [DataRow(float.NaN, 10f)]
    [DataRow(10f, float.NaN)]
    [DataRow(float.PositiveInfinity, 10f)]
    public void ANonFinitePointer_AnswersNone_AndReadsNothing(float x, float y)
    {
        var tree = new PanelTree();
        var root = new PanelNode("root", 0, 0, 1000, 1000).With(new PanelNode("card", 0, 0, 100, 100, SeatPanelKind.Card, CardOne));

        Assert.AreEqual(SeatPanelKind.None, At(root, x, y, tree).Kind);
        Assert.AreEqual(0, tree.Classified);
    }

    [TestMethod]
    public void OnlyNodesUnderThePointer_AreClassified()
    {
        var tree = new PanelTree();
        var root = new PanelNode("root", 0, 0, 1000, 1000).With(
            new PanelNode("far", 500, 500, 600, 600, SeatPanelKind.Card, CardTwo),
            new PanelNode("near", 0, 0, 100, 100, SeatPanelKind.Card, CardOne));

        At(root, 20, 20, tree);

        Assert.AreEqual(1, tree.Classified, "the near card answers first; the far card and the root are never read");
    }

    [TestMethod]
    public void ANullRoot_AnswersNone()
    {
        Assert.AreEqual(SeatPanelKind.None, SeatPanelTarget.Topmost<PanelNode>(new PanelTree(), null!, 1, 1).Kind);
    }
}
