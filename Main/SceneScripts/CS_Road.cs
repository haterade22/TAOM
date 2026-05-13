// Behavioral inspiration: Alliance mod's CS_Road (GPL v3, github.com/Byak0/Alliance).
// Implementation written from a behavioral spec at docs/scene-scripts/specs/cs-road.md;
// no Alliance source was copied. See docs/scene-scripts/ATTRIBUTION.md for the
// clean-room procedure.
using System.Collections.Generic;
using TaleWorlds.DotNet;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TAOM.Core.Validation;
using TAOM.SceneScripts.Roads;

namespace TAOM.SceneScripts;

/// <summary>
/// Map-authoring helper. Attach to any scene entity, point <see cref="PathName"/>
/// at a named Path entity, assign a <see cref="Material"/>, click GENERATE.
/// Produces a procedural quad-strip mesh along the path with adaptive sample
/// spacing controlled by <see cref="StepCurve"/>. The script's body is irreducibly
/// over the ADR-002 thin-entry-point line ceiling: the editor reflection contract
/// requires every exposed knob be a public instance field on this class (cannot
/// move to a service), and all four lifecycle methods must be overridden here.
/// All algorithmic logic lives in <see cref="Roads.RoadPathSampler"/>,
/// <see cref="Roads.RoadGeometryBuilder"/>, <see cref="Roads.RoadMeshAttacher"/>,
/// <see cref="Roads.StepCurveParser"/>, <see cref="Roads.StepCurveEvaluator"/>,
/// and <see cref="Roads.HexColorParser"/>.
/// </summary>
public class CS_Road : ScriptComponentBehavior
{
    private const float LiveRegenerationIntervalSeconds = 0.5f;
    // 0 = always-on tag; bit-mask tags (e.g. 1L<<44) are filtered by the engine
    // and silently swallow our diagnostics in editor + in-game log windows.
    private const ulong LogTag = 0uL;

    [EditableScriptComponentVariable(true)] public string PathName = "";
    [EditableScriptComponentVariable(true)] public float Width = 4f;
    [EditableScriptComponentVariable(true)] public float ElevationOffset = 0.1f;
    [EditableScriptComponentVariable(true)] public string StepCurve = "{0:1},{100:1}";
    [EditableScriptComponentVariable(true)] public Material? Material;
    [EditableScriptComponentVariable(true)] public string CustomColor = "#ffffffff";
    [EditableScriptComponentVariable(true)] public float RepeatU = 1f;
    [EditableScriptComponentVariable(true)] public float RepeatV = 1f;
    [EditableScriptComponentVariable(true)] public bool InvertU;
    [EditableScriptComponentVariable(true)] public bool InvertV;
    [EditableScriptComponentVariable(true)] public bool RotateUV;
    [EditableScriptComponentVariable(true)] public FlowAxis FlowDirection = FlowAxis.AlongU;
    [EditableScriptComponentVariable(true)] public bool FlipFaces;
    [EditableScriptComponentVariable(true)] public SimpleButton Generate = new();
    [EditableScriptComponentVariable(true)] public SimpleButton Readme = new();
    [EditableScriptComponentVariable(true)] public bool Live;

    private IReadOnlyList<StepKey> _parsedCurve = StepCurveParser.DefaultCurve;
    private MetaMesh? _lastGenerated;
    private float _liveAccumulator;

    protected override void OnInit()
    {
        ParseStepCurveWithWarning();
        GenerateMesh();
    }

    protected override void OnEditorInit()
    {
        ParseStepCurveWithWarning();
        GenerateMesh();
    }

    protected override void OnEditorVariableChanged(string variableName)
    {
        base.OnEditorVariableChanged(variableName);

        switch (variableName)
        {
            case nameof(Generate):
                LogInfo($"CS_Road on '{TryGetEntityName(GameEntity)}': Generate button clicked.");
                GenerateMesh();
                return;
            case nameof(Readme):
                LogReadme();
                return;
            case nameof(StepCurve):
                ParseStepCurveWithWarning();
                break;
        }

        if (Live) GenerateMesh();
    }

    protected override void OnEditorTick(float dt)
    {
        base.OnEditorTick(dt);
        if (!Live) return;

        _liveAccumulator += dt;
        if (_liveAccumulator < LiveRegenerationIntervalSeconds) return;

        _liveAccumulator = 0f;
        GenerateMesh();
    }

    protected override void OnRemoved(int removeReason)
    {
        var entity = GameEntity;
        if (entity.IsValid && _lastGenerated != null)
        {
            entity.RemoveMultiMesh(_lastGenerated);
        }
        _lastGenerated = null;
        base.OnRemoved(removeReason);
    }

    private void ParseStepCurveWithWarning()
    {
        bool ok = StepCurveParser.TryParse(StepCurve, out var parsed);
        _parsedCurve = parsed;
        if (!ok && !string.IsNullOrWhiteSpace(StepCurve))
        {
            LogWarn($"CS_Road: StepCurve '{StepCurve}' is malformed; falling back to constant step 1. Format: {{percent:step}},{{percent:step}},...");
        }
    }

    private void GenerateMesh()
    {
        var entity = GameEntity;
        var entityName = TryGetEntityName(entity);
        LogInfo($"CS_Road on '{entityName}': GenerateMesh start.");

        if (!entity.IsValid)
        {
            LogWarn($"CS_Road on '{entityName}': GameEntity is not valid. Skipping.");
            return;
        }

        if (!FiniteFloatValidator.IsFinite(Width) || Width <= 0f)
        {
            LogWarn($"CS_Road on '{entityName}': Width must be a finite value > 0; got {Width}. Skipping.");
            return;
        }
        if (!FiniteFloatValidator.IsFinite(ElevationOffset))
        {
            LogWarn($"CS_Road on '{entityName}': ElevationOffset must be finite; got {ElevationOffset}. Skipping.");
            return;
        }
        if (!FiniteFloatValidator.IsFinite(RepeatU) || !FiniteFloatValidator.IsFinite(RepeatV))
        {
            LogWarn($"CS_Road on '{entityName}': RepeatU/RepeatV must be finite; got {RepeatU}/{RepeatV}. Skipping.");
            return;
        }
        if (Material == null)
        {
            LogWarn($"CS_Road on '{entityName}': Material is not set. Skipping.");
            return;
        }

        var scene = Scene;
        if (scene == null)
        {
            LogWarn($"CS_Road on '{entityName}': Scene is null (editor context not yet ready?). Skipping.");
            return;
        }

        if (string.IsNullOrWhiteSpace(PathName))
        {
            LogWarn($"CS_Road on '{entityName}': PathName is empty. Skipping.");
            return;
        }

        var path = scene.GetPathWithName(PathName);
        if (path == null)
        {
            LogWarn($"CS_Road on '{entityName}': no path named '{PathName}' found in scene. Skipping.");
            return;
        }

        float totalDistance = path.TotalDistance;
        if (!FiniteFloatValidator.IsFinite(totalDistance) || totalDistance <= 0f)
        {
            LogWarn($"CS_Road on '{entityName}': path '{PathName}' has TotalDistance={totalDistance}. Skipping.");
            return;
        }

        var samples = SamplePath(path, totalDistance);
        if (samples.Count < 2)
        {
            LogWarn($"CS_Road on '{entityName}': path '{PathName}' produced only {samples.Count} sample(s) (StepCurve='{StepCurve}', totalDistance={totalDistance:F2}m). Skipping.");
            return;
        }

        var triangles = RoadGeometryBuilder.Build(
            samples, halfWidth: Width, elevationOffset: ElevationOffset,
            repeatU: RepeatU, repeatV: RepeatV,
            invertU: InvertU, invertV: InvertV, rotateUV: RotateUV,
            flowDirection: FlowDirection, flipFaces: FlipFaces);
        if (triangles.Count == 0)
        {
            LogWarn($"CS_Road on '{entityName}': geometry builder returned 0 triangles from {samples.Count} samples. Skipping.");
            return;
        }

        var color = HexColorParser.ToPackedArgb(CustomColor);
        _lastGenerated = RoadMeshAttacher.BuildAndAttach(entity, Material, triangles, color, _lastGenerated);
        LogInfo($"CS_Road on '{entityName}': generated mesh from path '{PathName}' (totalDistance={totalDistance:F2}m, {samples.Count} samples, {triangles.Count} triangles, material='{Material.Name}').");
    }

    private List<RoadSampleFrame> SamplePath(Path path, float totalDistance)
    {
        var distances = RoadPathSampler.SampleDistances(_parsedCurve, totalDistance, StepCurveParser.MinStep);
        var samples = new List<RoadSampleFrame>(distances.Count);
        for (int i = 0; i < distances.Count; i++)
        {
            var d = distances[i];
            var frame = path.GetFrameForDistance(d);
            samples.Add(new RoadSampleFrame(frame.origin, frame.rotation.s, d));
        }
        return samples;
    }

    private static void LogReadme()
    {
        Debug.Print(
            "CS_Road StepCurve format: {percent:step},{percent:step},... " +
            "percent ∈ [0,100], step is world-units between samples at that point. " +
            "Example: \"{0:0.5},{50:2},{100:0.5}\" = dense at start, sparse middle, dense at end. " +
            "Braces optional. Pairs that fail to parse are skipped; if no pair parses, falls back to {0:1},{100:1}.",
            0, Debug.DebugColor.White, LogTag);
    }

    private static void LogWarn(string message)
    {
        Debug.Print($"TAOM: {message}", 0, Debug.DebugColor.Yellow, LogTag);
    }

    private static void LogInfo(string message)
    {
        Debug.Print($"TAOM: {message}", 0, Debug.DebugColor.White, LogTag);
    }

    private static string TryGetEntityName(WeakGameEntity entity)
    {
        try { return entity.Name ?? "(unnamed)"; }
        catch { return "(?)"; }
    }
}
