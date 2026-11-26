using TAOM.Core.Validation;

namespace TAOM.Features.SeatOfSeeing.Domain;

/// <summary>What an order panel button, set or troop card is, by the view model its widget is bound to (Marshal's Word stage M7).</summary>
internal enum SeatPanelKind
{
    None,
    Card,
    OrderSet,
    OrderItem,
}

/// <summary>The topmost panel target under the pointer: its kind, and the view model it is bound to (null when the binding cannot be read).</summary>
internal readonly record struct SeatPanelHit(SeatPanelKind Kind, object? Target);

/// <summary>
/// A widget tree as the shield's rule reads it, so the rule runs in a test with no engine. The client adapter implements it over Gauntlet
/// widgets: <c>Shown</c> is the widget's own visible flag, <c>Contains</c> its measured area, <c>Classify</c> the panel kind of its view model.
/// </summary>
internal interface ISeatPanelTree<T> where T : class
{
    bool Shown(T node);

    bool Contains(T node, float x, float y);

    int ChildCount(T node);

    T Child(T node, int index);

    /// <summary>The panel kind of the node's view model, with that view model as <paramref name="target"/>; None for any other widget.</summary>
    SeatPanelKind Classify(T node, out object? target);
}

/// <summary>
/// The topmost panel target under a point (Seat build-spec 6.1). The walk is the engine's own hit walk (<c>EventManager.AnyWidgetsAt</c>)
/// without its two event flags: the order movies wrap the whole panel in <c>DoNotAcceptEvents</c> and <c>DoNotPassEventsToChildren</c>,
/// so the engine's walk never reaches a button. A later sibling draws above an earlier one and a child above its parent, so the walk
/// takes children last to first and a node only after its children. A hidden node hides everything below it. A child may lie outside its
/// parent's rectangle, so a parent's rectangle never prunes the walk.
/// </summary>
internal static class SeatPanelTarget
{
    internal static SeatPanelHit Topmost<T>(ISeatPanelTree<T> tree, T root, float x, float y) where T : class
    {
        if (root == null || !FiniteFloatValidator.IsFinite(x) || !FiniteFloatValidator.IsFinite(y) || !tree.Shown(root))
            return default;

        return Walk(tree, root, x, y, out var hit) ? hit : default;
    }

    private static bool Walk<T>(ISeatPanelTree<T> tree, T node, float x, float y, out SeatPanelHit hit) where T : class
    {
        for (var i = tree.ChildCount(node) - 1; i >= 0; i--)
        {
            var child = tree.Child(node, i);
            if (tree.Shown(child) && Walk(tree, child, x, y, out hit))
                return true;
        }

        if (tree.Contains(node, x, y))
        {
            var kind = tree.Classify(node, out var target);
            if (kind != SeatPanelKind.None)
            {
                hit = new SeatPanelHit(kind, target);
                return true;
            }
        }

        hit = default;
        return false;
    }
}
