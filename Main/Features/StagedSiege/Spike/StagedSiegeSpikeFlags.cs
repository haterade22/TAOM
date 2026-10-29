using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Objects;

namespace TAOM.Features.StagedSiege.Spike;

/// <summary>Spawns the native sergeant flag poles, <c>flag_pole_big_sergeant_A</c> to <c>_E</c>.</summary>
internal static class StagedSiegeSpikeFlags
{
    internal static bool IsStageLetter(string text, char max, out char letter)
    {
        letter = text != null && text.Length == 1 ? char.ToUpperInvariant(text[0]) : '\0';
        return letter >= 'A' && letter <= max;
    }

    /// <summary>The created mission object (null on a client) and, through <paramref name="flag"/>, the flag when it is one.</summary>
    internal static MissionObject Spawn(Mission mission, char letter, Vec3 position, Vec2 facing, out FlagCapturePoint flag)
    {
        var forward = new Vec3(facing.x, facing.y, 0f);
        if (forward.LengthSquared < 0.0001f) forward = new Vec3(0f, 1f, 0f);
        forward.Normalize();
        var rotation = Mat3.Identity;
        rotation.f = forward;
        rotation.u = Vec3.Up;
        rotation.Orthonormalize();
        var frame = new MatrixFrame(rotation, position);
        var created = mission.CreateMissionObjectFromPrefab("flag_pole_big_sergeant_" + letter, frame, false, 0f, _ => { });
        flag = created as FlagCapturePoint;
        return created;
    }
}
