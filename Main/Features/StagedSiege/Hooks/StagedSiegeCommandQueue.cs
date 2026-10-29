using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.StagedSiege.Domain;

namespace TAOM.Features.StagedSiege.Hooks;

/// <summary>What a console command asks the mission to do on its next tick.</summary>
internal enum StagedSiegeCommandKind
{
    ForceStage,
    OpenGates,
    ShowFlags,
}

/// <summary>One queued console request. Immutable, so it crosses from the console thread to the main thread safely.</summary>
internal sealed class StagedSiegeCommand
{
    private StagedSiegeCommand(StagedSiegeCommandKind kind, StageId? stage, IReadOnlyList<StageZone>? zones)
    {
        Kind = kind;
        Stage = stage;
        Zones = zones;
    }

    public StagedSiegeCommandKind Kind { get; }

    /// <summary>The target of a forced stage; null means capture through D.</summary>
    public StageId? Stage { get; }

    public IReadOnlyList<StageZone>? Zones { get; }

    public static StagedSiegeCommand ForceStage(StageId? target) => new(StagedSiegeCommandKind.ForceStage, target, null);

    public static StagedSiegeCommand OpenGates() => new(StagedSiegeCommandKind.OpenGates, null, null);

    public static StagedSiegeCommand ShowFlags(IReadOnlyList<StageZone> zones) => new(StagedSiegeCommandKind.ShowFlags, null, zones);
}

/// <summary>
/// The console's only way into a staged siege. A console command can run while the async AI tick is in flight (the engine
/// iterates <c>FormationAI._behaviors</c> there), so a command never writes engine AI, gate or mission-object state itself: it
/// enqueues, and the mission behavior drains this queue on the main thread at the start of <c>OnMissionTick</c>, before the
/// service ticks. It works with no service armed too, because recording an unrecorded scene needs open-gates and show-flags.
/// </summary>
internal sealed class StagedSiegeCommandQueue
{
    private readonly ConcurrentQueue<StagedSiegeCommand> _items = new();

    public bool IsEmpty => _items.IsEmpty;

    public void Enqueue(StagedSiegeCommand command) => _items.Enqueue(command);

    /// <summary>
    /// Applies what was queued when the drain began, in order, and returns how many commands ran. A command queued during the
    /// drain waits for the next one. Each command's result sentence goes to the log; a throwing command is logged and the rest
    /// still run.
    /// </summary>
    public int Drain(StagedSiegeService? service, IStagedSiegeMissionAdapter adapter, IModLogger logger)
    {
        var count = _items.Count;
        var ran = 0;
        for (var i = 0; i < count && _items.TryDequeue(out var command); i++)
        {
            ran++;
            try
            {
                logger.LogInfo($"{StagedSiegeService.Tag} console: {Apply(command, service, adapter)}");
            }
            catch (Exception ex)
            {
                logger.LogWarning($"{StagedSiegeService.Tag} console {command.Kind} failed: {ex.GetType().Name}: {ex.Message}");
            }
        }

        return ran;
    }

    private static string Apply(StagedSiegeCommand command, StagedSiegeService? service, IStagedSiegeMissionAdapter adapter)
    {
        switch (command.Kind)
        {
            case StagedSiegeCommandKind.ForceStage:
                return service == null
                    ? "The staged siege is not armed in this mission (taom.staged_siege_status says why)."
                    : service.ForceStage(command.Stage);
            case StagedSiegeCommandKind.OpenGates:
                var opened = adapter.OpenGates();
                return opened == 0 ? "No gate to open (this scene has none, or they are destroyed or disabled)." : $"Opened {opened} gate(s).";
            default:
                if (service != null && service.IsArmed)
                    return "The armed staged siege already placed its flags.";
                return $"Spawned {adapter.SpawnFlags(command.Zones!)} flag(s) of {command.Zones!.Count} recorded point(s).";
        }
    }
}
