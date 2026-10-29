using TAOM.Features.StagedSiege.Domain;

namespace TAOM.Tests.Features.StagedSiege;

/// <summary>Shared builders for the staged siege tests.</summary>
internal static class StagedSiegeFixtures
{
    internal static StageZone Zone(StageId id, float x, float y, float z = 10f, float radius = 8f) =>
        new(id, new StagePoint(x, y, z), radius);

    internal static StageFrame Frame(float x, float y, float z, float fx, float fy) =>
        new(new StagePoint(x, y, z), new StageFacing(fx, fy));

    /// <summary>Four points on a line along +Y, 100 m apart, with distinct frames for the detachment and B, C, D.</summary>
    internal static ScenePlan LinePlan(string scene = "test_scene") => new(
        scene,
        new[]
        {
            Zone(StageId.A, 0f, 0f),
            Zone(StageId.B, 0f, 100f),
            Zone(StageId.C, 0f, 200f),
            Zone(StageId.D, 0f, 300f),
        },
        Frame(1f, 2f, 3f, 0f, -1f),
        Frame(10f, 110f, 11f, 0f, 1f),
        Frame(20f, 210f, 12f, 1f, 0f),
        Frame(30f, 310f, 13f, -1f, 0f));
}
