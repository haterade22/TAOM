using System.Collections.Generic;

namespace TAOM.Features.TrainingGrounds.Waves.Domain;

/// <summary>Chooses the spawn frame of the next fighter: one at least 12 m from the player, cycling by turn.</summary>
internal static class SpawnPicker
{
    /// <summary>The square of vanilla's minimum spawn distance (12 m).</summary>
    private const float MinDistanceSquared = 144f;

    /// <summary>
    /// The index of the frame, cycling by <paramref name="turn"/> among the frames whose squared distance from the player is at least
    /// 144; when none is that far, the farthest one. -1 for no frames. The far test is a positive requirement, so a NaN position
    /// (the player's or a frame's) never passes it; with a NaN player position the fallback keeps the first frame.
    /// </summary>
    public static int Pick(IReadOnlyList<(float X, float Y)> frames, float px, float py, int turn)
    {
        if (frames.Count == 0)
            return -1;

        var far = new List<int>();
        var farthest = 0;
        var farthestSquared = -1f;
        for (var i = 0; i < frames.Count; i++)
        {
            var dx = frames[i].X - px;
            var dy = frames[i].Y - py;
            var squared = dx * dx + dy * dy;
            if (squared >= MinDistanceSquared)
                far.Add(i);
            if (squared > farthestSquared)
            {
                farthestSquared = squared;
                farthest = i;
            }
        }

        return far.Count > 0 ? far[((turn % far.Count) + far.Count) % far.Count] : farthest;
    }
}
