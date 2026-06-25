using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using TAOM.Core.Logging;

namespace TAOM.Features.ShaderPrecompilation;

// File-backed IShaderPrecompileCrashGuard. The two files live in "Logs/" next to the taom_debug log +
// the battle-load stall marker, so a player who hits a scene crash finds everything in one folder.
// All I/O is best-effort and swallowed — a diagnostic must never break the walk. Touched only from the
// main thread (runner Begin / StartCurrentItem / TickEnding / Finish), so no locking is needed.
public sealed class ShaderPrecompileCrashGuard : IShaderPrecompileCrashGuard
{
    private const string InflightFileName = "shader-precompile-inflight.marker";
    private const string CrashedFileName  = "shader-precompile-crashed-scenes.txt";
    private const string CrashedFileHeader =
        "# Scenes that hard-crashed the shader pre-compile process while loading — auto-skipped on future\n" +
        "# walks so the walk can complete. Usually GPU/driver-specific. Delete this file to retry them.\n";

    private readonly IModLogger _logger;
    private readonly string _inflightPath;
    private readonly string _crashedPath;

    public ShaderPrecompileCrashGuard(IModLogger logger)
        : this(logger, Path.Combine("Logs", InflightFileName), Path.Combine("Logs", CrashedFileName)) { }

    // Test seam: inject temp paths so the file lifecycle is unit-tested. internal (not public) so DryIoc
    // sees a single public ctor and auto-resolves it; TAOM.Tests reaches it via InternalsVisibleTo.
    internal ShaderPrecompileCrashGuard(IModLogger logger, string inflightPath, string crashedPath)
    {
        _logger = logger;
        _inflightPath = inflightPath;
        _crashedPath = crashedPath;
    }

    public IReadOnlyCollection<string> ConsumeAndGetSkipSet()
    {
        // 1. A surviving inflight marker means the last walk crashed while loading that scene.
        var crashedScene = TryConsumeInflightScene();
        if (!string.IsNullOrEmpty(crashedScene))
        {
            AppendCrashedScene(crashedScene);
            _logger?.LogWarning($"[ShaderPrecompilation] scene '{crashedScene}' crashed the previous walk's process during load — recording it to the skip list");
            // This is a native GPU/driver AV we can only fix with the fault address. Tell the user (in the
            // log, which the crash bundle carries) exactly how to capture it while the Event Log entry is fresh.
            _logger?.LogWarning(
                "[ShaderPrecompilation] To help fix this crash: open Windows Event Viewer (Win+R -> eventvwr.msc) " +
                "-> Windows Logs -> Application, find the most recent 'Application Error' for Bannerlord.exe, and " +
                "send its 'Faulting module name' + 'Fault offset' (or right-click -> Save Selected Events) to the " +
                "TAOM author. Until then, the scene is auto-skipped (delete shader-precompile-crashed-scenes.txt to retry).");
        }

        // 2. Return the persistent skip set.
        var skip = ReadCrashedScenes();
        if (skip.Count > 0)
            _logger?.LogInfo($"[ShaderPrecompilation] {skip.Count} scene(s) on the crash skip list: {string.Join(", ", skip)} (delete {CrashedFileName} in the Logs folder to retry them)");
        return skip;
    }

    public void MarkLoading(string sceneId)
    {
        try
        {
            var dir = Path.GetDirectoryName(_inflightPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(_inflightPath, FormatInflight(sceneId, DateTime.UtcNow));
        }
        catch { /* a diagnostic must never break the walk */ }
    }

    public void ClearLoading()
    {
        try { if (File.Exists(_inflightPath)) File.Delete(_inflightPath); }
        catch { /* best-effort */ }
    }

    private string TryConsumeInflightScene()
    {
        try
        {
            if (!File.Exists(_inflightPath)) return null;
            var scene = ParseInflightScene(File.ReadAllText(_inflightPath));
            try { File.Delete(_inflightPath); } catch { /* leave it; it self-clears on the next walk */ }
            return scene;
        }
        catch { return null; }
    }

    private void AppendCrashedScene(string sceneId)
    {
        try
        {
            if (ReadCrashedScenes().Contains(sceneId)) return;  // already recorded — keep the list de-duped
            var dir = Path.GetDirectoryName(_crashedPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            if (!File.Exists(_crashedPath)) File.WriteAllText(_crashedPath, CrashedFileHeader);
            File.AppendAllText(_crashedPath, sceneId + "\n");
        }
        catch { /* best-effort */ }
    }

    private HashSet<string> ReadCrashedScenes()
    {
        try
        {
            return File.Exists(_crashedPath)
                ? ParseCrashedScenes(File.ReadAllText(_crashedPath))
                : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
        catch { return new HashSet<string>(StringComparer.OrdinalIgnoreCase); }
    }

    // ---- pure, unit-tested ---- //
    public static string FormatInflight(string sceneId, DateTime utc)
        => $"scene={sceneId ?? string.Empty}\nutc={utc.ToString("o", CultureInfo.InvariantCulture)}\n";

    // Returns the scene id from a `scene=` line, or null if absent/empty.
    public static string ParseInflightScene(string text)
    {
        foreach (var raw in (text ?? string.Empty).Split('\n'))
        {
            var line = raw.Trim();
            if (line.StartsWith("scene="))
            {
                var scene = line.Substring("scene=".Length).Trim();
                return scene.Length == 0 ? null : scene;
            }
        }
        return null;
    }

    // One scene id per line; ignores blanks + `#` comments; case-insensitive de-dupe.
    public static HashSet<string> ParseCrashedScenes(string text)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in (text ?? string.Empty).Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("#")) continue;
            set.Add(line);
        }
        return set;
    }
}
