using TAOM.Features.LongDefeat.Domain;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.LongDefeat.Hooks;

/// <summary>
/// Which target of the boons a defender's body is: the lord's own body (whoever he was, so a soldier taken over after the lord's death becomes the player) takes the player
/// record only, every other defender the group its character puts it in. Reads an immutable character flag and one field of the mission, safe on any thread.
/// </summary>
internal static class LongDefeatTargets
{
    /// <summary>The body's target, or null when it has no character (a riderless horse, whose own Character is null).</summary>
    internal static BoonTarget? Of(Agent body, Mission mission)
    {
        if (ReferenceEquals(mission.MainAgent, body))
            return BoonTarget.Player;

        var character = body.Character;
        return character == null ? null : BoonTargets.OfCharacter(character.IsMounted, character.IsRanged);
    }
}
