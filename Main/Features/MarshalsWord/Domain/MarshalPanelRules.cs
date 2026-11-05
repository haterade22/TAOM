using System;

namespace TAOM.Features.MarshalsWord.Domain;

/// <summary>What a click on one of Marshal's Word's panel entries asks for (build-spec 10): the same three presses the keys store.</summary>
internal enum PanelAction
{
    Fire,
    Word,
    Hold,
}

/// <summary>Where Marshal's Word's panel set goes in the base game's list of sets (build-spec 10).</summary>
internal static class MarshalPanelRules
{
    /// <summary><c>BannerlordConfig.OrderLayoutType</c> of the legacy layout: <c>DefaultVisualOrderProvider.GetOrders</c> builds the legacy sets when it is 1.</summary>
    internal const int LegacyLayout = 1;

    /// <summary>The index the new set goes at: before the legacy layout's trailing Return, which keeps F9 (GauntletOrderUIHandler.cs:489), and last in every other list.</summary>
    internal static int SetIndex(int layoutType, int setCount, bool lastIsReturn) =>
        layoutType == LegacyLayout && lastIsReturn && setCount > 0 ? setCount - 1 : Math.Max(0, setCount);
}
