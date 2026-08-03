namespace TAOM.Features.CoopInterop;

/// <summary>
/// Whether this process is a Bannerlord DEDICATED SERVER — a headless host with no local player.
///
/// The distinction matters because <c>Hero.MainHero</c> exists there anyway: it is the world-gen
/// hero the campaign was created around, idle and unplayed. Anything that credits, charges or
/// rewards "the player" by reading <c>Hero.MainHero</c> therefore acts on a hero nobody controls.
/// Field report 2026-08-03 §6 caught this as dozens of <c>[SpecRes] PRISONERS: +N</c> lines on a
/// server while the remote players who actually fought those battles earned nothing.
///
/// It is deliberately NOT derivable from co-op role: a CLIENT-HOSTED session also reports
/// <c>IsServer</c>, but that host is a real player at a real keyboard and must keep earning normally.
/// </summary>
public interface IDedicatedServerProvider
{
    bool IsDedicatedServer { get; }
}
