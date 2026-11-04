using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Features.LongDefeat;

/// <summary>
/// What starts one Long Defeat run: the fortress row's id, who asked for it ("menu", "console" or "autorun", for the log) and the
/// run's options. The launcher refuses an id the config does not hold. <paramref name="RecordsFolder"/> is null for a player's run
/// (their own folder) and the autorun's out folder for an unattended one, so a test run never touches the player's best.
/// </summary>
internal sealed record LongDefeatSetup(string FortressId, string Trigger, RunOptions Options, string? RecordsFolder = null);
