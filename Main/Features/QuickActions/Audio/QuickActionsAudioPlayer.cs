using System;
using TaleWorlds.Engine;
using TAOM.Core.Logging;

namespace TAOM.Features.QuickActions.Audio;

/// <summary>
/// Plays vanilla 2D UI sound events via <see cref="SoundEvent.PlaySound2D(string)"/>.
/// Confirmed via <c>ilspycmd</c> on v1.3.15 <c>TaleWorlds.Engine.dll</c>:
///   <c>public static bool PlaySound2D(string soundName)</c>
/// Vanilla event ID <c>event:/ui/transfer</c> is used by inventory transfers (sell/buy/move);
/// it's the closest semantic match for "bulk sell completed."
/// </summary>
public sealed class QuickActionsAudioPlayer : IQuickActionsAudioPlayer
{
    private const string SellEvent = "event:/ui/transfer";
    private const string UnequipEvent = "event:/ui/transfer";

    private readonly IModLogger _logger;

    public QuickActionsAudioPlayer(IModLogger logger)
    {
        _logger = logger;
    }

    public void PlaySellCompleted() => SafePlay(SellEvent);
    public void PlayUnequipCompleted() => SafePlay(UnequipEvent);

    private void SafePlay(string eventPath)
    {
        try
        {
            SoundEvent.PlaySound2D(eventPath);
        }
        catch (Exception ex)
        {
            // Sound failure must never crash a UI action; log and swallow.
            _logger.LogWarning($"[QuickActions] sound '{eventPath}' failed: {ex.Message}");
        }
    }
}
