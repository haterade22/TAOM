using TaleWorlds.Library;

namespace TAOM.Adapters;

public interface IEditorSceneAdapter
{
    bool TryGetPath(
        int fromFace,
        int toFace,
        Vec2 fromPos,
        Vec2 toPos,
        float agentRadius,
        int[] excludedFaceIds,
        int regionSwitchCostTo0,
        int regionSwitchCostTo1,
        NavigationPath outPath);

    bool TryGetPathDistance(
        int fromFace,
        int toFace,
        Vec2 fromPos,
        Vec2 toPos,
        float agentRadius,
        float distanceLimit,
        int[] excludedFaceIds,
        int regionSwitchCostTo0,
        int regionSwitchCostTo1,
        out float distance);
}
