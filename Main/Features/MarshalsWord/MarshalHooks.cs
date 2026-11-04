using System;
using System.Collections.Generic;
using TaleWorlds.MountAndBlade;
using TAOM.Adapters;
using TAOM.Features.MarshalsWord.Domain;

namespace TAOM.Features.MarshalsWord;

/// <summary>
/// The statics the Marshal patches read (build-spec 2): every field is volatile, because the session, the order adapter and the array prefix
/// write them on the main thread and the patches read them on the main thread, the AI thread and the workers.
/// </summary>
internal static class MarshalHooks
{
    private static volatile bool _isLive;
    private static volatile bool _queueArmed;
    private static volatile bool _keepWidthsHeld;
    private static volatile bool _keepArray;
    private static volatile bool _arriveTogether;
    private static volatile bool _arrayFaulted;
    private static volatile OrderController? _playerController;
    private static volatile IMarshalHookSink? _sink;
    private static volatile MarshalPaceTable? _paces;

    /// <summary>False until the session begins and again from the first teardown path or fault, and the pace postfix tests the table instead.</summary>
    internal static bool IsLive => _isLive;

    /// <summary>Key 540 is down and the order panel is open (written once a frame by the session).</summary>
    internal static bool QueueArmed => _queueArmed;

    /// <summary>
    /// Key 542 is down and the order panel is open. Written by the screen tick, and borrowed for the length of one queued order, which carries
    /// the key as it stood when the player gave it (<c>MarshalOrderAdapter.Issue</c>, main thread only, restored in a finally).
    /// </summary>
    internal static bool KeepWidthsHeld
    {
        get => _keepWidthsHeld;
        set => _keepWidthsHeld = value;
    }

    /// <summary>The <c>MarshalKeepArray</c> setting, copied once a frame while live. False after the array takeover has thrown.</summary>
    internal static bool KeepArray => _keepArray && !_arrayFaulted;

    /// <summary>The <c>MarshalArriveTogether</c> setting, copied once a frame while live.</summary>
    internal static bool ArriveTogether => _arriveTogether;

    internal static OrderController? PlayerController
    {
        get => _playerController;
        set => _playerController = value;
    }

    internal static MarshalPaceTable? Paces
    {
        get => _paces;
        set => _paces = value;
    }

    internal static void Begin(IMarshalHookSink sink)
    {
        _arrayFaulted = false;
        _paces = null;
        _sink = sink;
        _isLive = true;
    }

    /// <summary>Written together once a frame by the session, from the keys and the settings.</summary>
    internal static void Arm(bool queue, bool keepWidths, bool keepArray, bool arriveTogether)
    {
        _queueArmed = queue;
        _keepWidthsHeld = keepWidths;
        _keepArray = keepArray;
        _arriveTogether = arriveTogether;
    }

    /// <summary>The array prefix threw: the takeover is off for the rest of the mission and vanilla lays out every order.</summary>
    internal static void LatchArrayOff(Exception exception)
    {
        _arrayFaulted = true;
        _sink?.ArrayFault(exception);
    }

    /// <summary>Offers an order to the session. False (vanilla runs) with no live session.</summary>
    internal static bool TryCapture(MarshalOrderPayload payload) => _sink?.TryCapture(payload) ?? false;

    internal static void ReportArray(bool click, int companies) => _sink?.ArraySolved(click, companies);

    internal static void RequestMarch(IReadOnlyList<MarchTarget> targets) => _sink?.MarchRequested(targets);

    /// <summary>True when this sink is the live session. A session that finishes late must not clear the next mission's hooks.</summary>
    internal static bool IsOwnedBy(IMarshalHookSink sink) => ReferenceEquals(_sink, sink);

    /// <summary>The one safe-to-repeat clear. Makes no native call.</summary>
    internal static void Clear()
    {
        _isLive = false;
        _queueArmed = false;
        _keepWidthsHeld = false;
        _keepArray = false;
        _arriveTogether = false;
        _playerController = null;
        _sink = null;
        _paces = null;
    }
}
