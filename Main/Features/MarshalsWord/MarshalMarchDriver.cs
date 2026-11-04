using System;
using System.Collections.Generic;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.MarshalsWord.Domain;

namespace TAOM.Features.MarshalsWord;

/// <summary>
/// Runs the march of build-spec 6.4 on the slow pass: it adopts a march the array commit asked for, publishes the paces, and writes the one
/// verdict line (6.5) when the march ends. <see cref="Request"/> and <see cref="Pass"/> both run on the main thread, because the array commit is
/// reached from the screen tick or from <c>OnMissionTick</c>.
/// </summary>
internal sealed class MarshalMarchDriver
{
    private readonly IMarshalOrderAdapter _orders;
    private readonly IOutcomeLog _outcomes;
    private readonly string _key;
    private IReadOnlyList<MarchTarget>? _request;
    private IReadOnlyList<MarchTarget> _targets = Array.Empty<MarchTarget>();
    private MarshalMarch? _march;
    private double _nextPass = double.NegativeInfinity;

    internal MarshalMarchDriver(IMarshalOrderAdapter orders, IOutcomeLog outcomes, string missionKey)
    {
        _orders = orders;
        _outcomes = outcomes;
        _key = missionKey;
    }

    internal bool IsMarching => _march != null;

    /// <summary>An array commit of two or more companies. Stored for the next pass: the commit can run in the screen tick.</summary>
    internal void Request(IReadOnlyList<MarchTarget> targets)
    {
        if (targets.Count >= 2)
            _request = targets;
    }

    /// <summary>One slow pass, where <paramref name="allowed"/> is the Arrive Together setting and off stops a march in progress and drops a pending request.</summary>
    internal void Pass(bool allowed)
    {
        var now = _orders.Now;
        if (now < _nextPass)
            return;
        _nextPass = now + MarshalTuning.SlowPassSeconds;

        var request = _request;
        _request = null;
        if (!allowed)
        {
            if (_march != null)
                End("setting-off");
            return;
        }

        if (request != null)
            Adopt(request);
        if (_march == null)
            return;

        var pass = _march.Update(now, _orders.ReadMarch(_targets));
        if (!pass.Ended)
        {
            _orders.PublishPaces(pass.Paces);
            return;
        }

        Conclude(pass.Reason);
    }

    /// <summary>The player's team changed, so each company number names a formation of the old team.</summary>
    internal void OnPlayerTeamChanged()
    {
        if (_march != null)
            End("team-changed");
        else
            Stop();
    }

    private void Adopt(IReadOnlyList<MarchTarget> request)
    {
        if (_march != null)
            End("replaced");
        _targets = request;
        _march = new MarshalMarch();
        _outcomes.Pass(MarshalChecks.MarchId, _key, "mission=" + _key + " companies=" + request.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    private void Conclude(MarchEnd reason)
    {
        if (reason != MarchEnd.Arrived)
        {
            End(reason.ToString().ToLowerInvariant());
            return;
        }

        var march = _march!;
        if (march.MembersChanged)
        {
            End("company-left");
            return;
        }

        Stop();
        var spread = march.Spread();
        var longest = march.LongestToSlack;
        var shortest = march.ShortestToSlack;
        var detail = "mission=" + _key + " spread=" + spread.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)
            + " longest=" + longest.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)
            + " shortest=" + shortest.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
        if (!MarshalChecks.SpreadProvesPace(longest, shortest))
        {
            _outcomes.Skip(MarshalChecks.SpreadId, _key + ":no-test", detail + " reason=no-test");
            return;
        }

        if (MarshalChecks.SpreadPasses(spread, longest, shortest))
            _outcomes.Pass(MarshalChecks.SpreadId, _key, detail);
        else
            _outcomes.Fail(MarshalChecks.SpreadId, _key, detail);
    }

    /// <summary>Clears the paces and forgets the march, with no log line.</summary>
    private void Stop()
    {
        _march = null;
        _request = null;
        _orders.PublishPaces(Array.Empty<MarchPace>());
    }

    private void End(string why)
    {
        Stop();
        _outcomes.Skip(MarshalChecks.SpreadId, _key + ":" + why, "mission=" + _key + " reason=" + why);
    }
}
