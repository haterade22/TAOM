using System;
using System.Collections.Generic;
using TAOM.Adapters;
using TAOM.Features.MarshalsWord.Domain;

namespace TAOM.Features.MarshalsWord;

/// <summary>What the patches tell the live session, so that a patch holds only this interface.</summary>
internal interface IMarshalHookSink
{
    /// <summary>The capture prefix offers a player order, and true means it was queued and vanilla must not run it.</summary>
    bool TryCapture(MarshalOrderPayload payload);

    /// <summary>The array takeover laid out a click or a drag, reported by the commit and not by the preview.</summary>
    void ArraySolved(bool click, int companies);

    /// <summary>The array takeover threw; it latched itself off for the mission and vanilla ran.</summary>
    void ArrayFault(Exception exception);

    /// <summary>An array commit of two or more companies asks for a march.</summary>
    void MarchRequested(IReadOnlyList<MarchTarget> targets);
}
