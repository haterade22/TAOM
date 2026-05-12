using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Map.DistanceCache;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TAOM.Adapters;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.EditorCacheRebuild.Progress;

namespace TAOM.Features.EditorCacheRebuild;

public sealed class RuntimeCacheRebuildService : IRuntimeCacheRebuildService
{
    private readonly IDistanceCacheBuilderService _builderService;
    private readonly ICacheRebuildConfigProvider _configProvider;
    private readonly IPathService _pathService;
    private readonly IModLogger _logger;

    private int _runningFlag;

    public RuntimeCacheRebuildService(
        IDistanceCacheBuilderService builderService,
        ICacheRebuildConfigProvider configProvider,
        IPathService pathService,
        IModLogger logger)
    {
        _builderService = builderService;
        _configProvider = configProvider;
        _pathService = pathService;
        _logger = logger;
    }

    public bool IsRunning => Volatile.Read(ref _runningFlag) != 0;

    public bool Trigger()
    {
        var buildId = NewBuildId();
        var tag = $"[RuntimeCacheRebuild#{buildId}]";

        _logger.LogInfo($"{tag} ====================== TRIGGER REQUEST ======================");
        LogEnvironment(tag);

        if (Campaign.Current == null)
        {
            Notify("Cache rebuild requires an active campaign — load a save first.");
            _logger.LogWarning($"{tag} REJECTED: Campaign.Current is null. The button must be pressed while a campaign is active.");
            return false;
        }

        if (Campaign.Current.MapSceneWrapper == null)
        {
            Notify("Cache rebuild requires a loaded map scene — wait until the campaign is fully initialized.");
            _logger.LogWarning($"{tag} REJECTED: Campaign.Current.MapSceneWrapper is null (campaign loading not yet complete).");
            return false;
        }

        try
        {
            LogCampaignSnapshot(tag);
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"{tag} pre-flight campaign snapshot failed: {ex.GetType().Name}: {ex.Message} — continuing anyway");
        }

        if (Interlocked.CompareExchange(ref _runningFlag, 1, 0) != 0)
        {
            Notify("Cache rebuild already in progress — check log for status.");
            _logger.LogWarning($"{tag} REJECTED: another build is already running (IsRunning=true).");
            return false;
        }

        Notify("TAOM cache rebuild starting in background. This may take 10-30 minutes. Game stays playable but pathfinding queries during the rebuild may be inconsistent.");
        _logger.LogInfo($"{tag} ACCEPTED — spawning background task on threadpool. Watch this log for phase progress.");

        Task.Run(() => RunBuild(buildId, tag));
        return true;
    }

    private void RunBuild(string buildId, string tag)
    {
        var overallSw = Stopwatch.StartNew();
        long memBefore = -1;
        try
        {
            memBefore = GC.GetTotalMemory(forceFullCollection: false);
            _logger.LogInfo($"{tag} ====================== BUILD STARTING ======================");
            _logger.LogInfo($"{tag} background thread id={Thread.CurrentThread.ManagedThreadId}, isThreadPool={Thread.CurrentThread.IsThreadPoolThread}, memBaseline={FormatBytes(memBefore)}");

            var config = _configProvider.GetConfig();
            _logger.LogInfo($"{tag} config: enabled={config.Enabled}, forceVanilla={config.ForceVanilla}, parallelism={config.Parallelism}, smokeTestPairs={config.SmokeTestPairs}, enableCheckpoint={config.EnableCheckpoint}, enableIncremental={config.EnableIncremental}");
            if (!config.Enabled || config.ForceVanilla)
            {
                _logger.LogWarning($"{tag} ABORT: feature disabled (enabled={config.Enabled}, forceVanilla={config.ForceVanilla}). Edit Main/_Module/ModuleData/configs/cache_rebuild_config.json to re-enable.");
                NotifyOnMainThread("Cache rebuild aborted: feature disabled in cache_rebuild_config.json.");
                return;
            }

            _logger.LogInfo($"{tag} step 1/5: constructing SandBoxNavigationCache (NavigationType.Default)");
            var ctorSw = Stopwatch.StartNew();
            var cache = new SandBoxNavigationCache(MobileParty.NavigationType.Default);
            ctorSw.Stop();
            _logger.LogInfo($"{tag} step 1/5 OK: cache constructed in {ctorSw.ElapsedMilliseconds}ms, type={cache.GetType().FullName}");

            _logger.LogInfo($"{tag} step 2/5: binding NavigationCacheAdapter (reflection probe of vanilla NavigationCache<Settlement>)");
            var adapterSw = Stopwatch.StartNew();
            var adapter = new NavigationCacheAdapter(cache, _logger);
            adapterSw.Stop();
            _logger.LogInfo($"{tag} step 2/5 OK: adapter bound in {adapterSw.ElapsedMilliseconds}ms, NavigationType={adapter.NavigationType}");

            try
            {
                var (sceneCrc, navMeshCrc) = adapter.GetSceneCrcValues();
                _logger.LogInfo($"{tag} scene CRCs: scene=0x{sceneCrc:X8}, navMesh=0x{navMeshCrc:X8}");
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"{tag} could not read scene CRCs (non-fatal): {ex.GetType().Name}: {ex.Message}");
            }

            try
            {
                var settlements = adapter.GetAllRegisteredSettlements();
                var fortifications = settlements.Count(s => s.IsFortification);
                var ports = settlements.Count(s => s.HasPort);
                _logger.LogInfo($"{tag} settlement census: total={settlements.Count}, fortifications={fortifications}, ports={ports}");
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"{tag} could not enumerate settlements (non-fatal at this point — Build will fail loudly): {ex.GetType().Name}: {ex.Message}");
            }

            var outputPath = ResolveCacheOutputPath(adapter.NavigationType.ToString());
            LogOutputDiagnostics(tag, outputPath);

            _logger.LogInfo($"{tag} step 3/5: handing off to CacheBuilderService.Build (Phase 0 + Phase 1 + Phase 2). Watch [CacheRebuild] tagged lines for per-phase progress.");
            var buildSw = Stopwatch.StartNew();
            var result = _builderService.Build(adapter, CancellationToken.None);
            buildSw.Stop();
            _logger.LogInfo($"{tag} step 3/5 OK: Build returned in {ProgressLogger.FormatDuration(buildSw.Elapsed)} (cancelled={result.Cancelled})");

            if (result.Cancelled)
            {
                _logger.LogWarning($"{tag} build returned CANCELLED — output file NOT written. Existing cache file at {outputPath} is unchanged.");
                NotifyOnMainThread("Cache rebuild cancelled. See log for details. Existing cache file unchanged.");
                return;
            }

            _logger.LogInfo($"{tag} step 4/5: serializing cache to disk (atomic write via .tmp + rename)");
            var serializeSw = Stopwatch.StartNew();
            WriteOutputAtomically(adapter, outputPath, tag);
            serializeSw.Stop();
            _logger.LogInfo($"{tag} step 4/5 OK: serialize completed in {serializeSw.ElapsedMilliseconds}ms");

            _logger.LogInfo($"{tag} step 5/5: post-write verification (re-deserialize round-trip)");
            VerifyOutputRoundTrip(outputPath, result.Phase1.PairsComputed, result.Phase2.NeighborPairsAdded, tag);

            overallSw.Stop();
            var memAfter = GC.GetTotalMemory(forceFullCollection: false);
            var summary = string.Format(
                "TAOM cache rebuild COMPLETE. Phase 1: {0} pairs in {1:F1}s. Phase 2: {2} neighbors in {3:F1}s. Smoke: {4}. Total wall: {5}. Output: {6}.",
                result.Phase1.PairsComputed, result.Phase1.ElapsedSeconds,
                result.Phase2.NeighborPairsAdded, result.Phase2.ElapsedSeconds,
                result.SmokeTest.Outcome,
                ProgressLogger.FormatDuration(overallSw.Elapsed),
                Path.GetFileName(outputPath));
            _logger.LogInfo($"{tag} ====================== BUILD COMPLETE ======================");
            _logger.LogInfo($"{tag} {summary}");
            _logger.LogInfo($"{tag} memory delta: before={FormatBytes(memBefore)}, after={FormatBytes(memAfter)}, peak-during={FormatBytes(memAfter - memBefore)}+");
            NotifyOnMainThread(summary + " Load the next save to use it.");
        }
        catch (Exception ex)
        {
            overallSw.Stop();
            _logger.LogError($"{tag} ====================== BUILD FAILED ======================");
            _logger.LogError($"{tag} EXCEPTION on background thread after {ProgressLogger.FormatDuration(overallSw.Elapsed)}: {ex.GetType().FullName}: {ex.Message}");
            _logger.LogError($"{tag} stack trace:\n{ex.StackTrace}");
            if (ex.InnerException != null)
                _logger.LogError($"{tag} inner exception: {ex.InnerException.GetType().FullName}: {ex.InnerException.Message}\n{ex.InnerException.StackTrace}");
            NotifyOnMainThread($"Cache rebuild FAILED: {ex.GetType().Name}: {ex.Message}. See log for full trace.");
        }
        finally
        {
            Volatile.Write(ref _runningFlag, 0);
            _logger.LogInfo($"{tag} runningFlag cleared — IsRunning is now false. New triggers will be accepted.");
        }
    }

    private void LogEnvironment(string tag)
    {
        _logger.LogInfo($"{tag} env: machineName={Environment.MachineName}, processorCount={Environment.ProcessorCount}, osVersion={Environment.OSVersion}, clr={Environment.Version}, is64Bit={Environment.Is64BitProcess}");
        _logger.LogInfo($"{tag} env: workingSet={FormatBytes(Environment.WorkingSet)}, gcServer={System.Runtime.GCSettings.IsServerGC}, latencyMode={System.Runtime.GCSettings.LatencyMode}");
        _logger.LogInfo($"{tag} env: moduleRoot={_pathService.ModuleRootPath}");
    }

    private void LogCampaignSnapshot(string tag)
    {
        var campaign = Campaign.Current;
        var settlementCount = Settlement.All?.Count ?? -1;
        var fortifications = Settlement.All?.Count(s => s.IsFortification) ?? -1;
        var towns = Settlement.All?.Count(s => s.IsTown) ?? -1;
        var castles = Settlement.All?.Count(s => s.IsCastle) ?? -1;
        var villages = Settlement.All?.Count(s => s.IsVillage) ?? -1;
        var startTime = campaign.Models?.CampaignTimeModel?.CampaignStartTime ?? CampaignTime.Zero;
        _logger.LogInfo($"{tag} campaign snapshot: gameId={campaign.UniqueGameId}, started={startTime}, current={CampaignTime.Now}, settlements={settlementCount}, fortifications={fortifications}, towns={towns}, castles={castles}, villages={villages}");

        try
        {
            var mapSceneType = campaign.MapSceneWrapper.GetType();
            _logger.LogInfo($"{tag} map scene wrapper: type={mapSceneType.FullName}");
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"{tag} could not introspect map scene wrapper: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private void LogOutputDiagnostics(string tag, string outputPath)
    {
        _logger.LogInfo($"{tag} resolved output path: {outputPath}");
        try
        {
            var directory = Path.GetDirectoryName(outputPath);
            var dirExists = !string.IsNullOrEmpty(directory) && Directory.Exists(directory);
            _logger.LogInfo($"{tag} output directory exists: {dirExists} ({directory})");

            var finalExists = File.Exists(outputPath);
            var prevPath = outputPath + ".prev";
            var tempPath = outputPath + ".tmp";
            var prevExists = File.Exists(prevPath);
            var tempExists = File.Exists(tempPath);

            if (finalExists)
            {
                var info = new FileInfo(outputPath);
                _logger.LogInfo($"{tag} existing cache file: size={FormatBytes(info.Length)} ({info.Length:N0} bytes), modified={info.LastWriteTime:u}");
            }
            else
            {
                _logger.LogInfo($"{tag} no existing cache file at output path — this will be a fresh write.");
            }

            // Interrupted-write diagnostic: if there's no final file but a .prev exists, a previous
            // atomic write was interrupted between the `final → .prev` and `.tmp → final` renames.
            // The .prev file is the last-known-good cache; the user may want to restore it manually
            // before triggering a fresh rebuild (or just let this rebuild produce a new final).
            if (!finalExists && prevExists)
            {
                var prevInfo = new FileInfo(prevPath);
                _logger.LogWarning(
                    $"{tag} INTERRUPTED-WRITE DETECTED: final cache file is MISSING but '{prevPath}' exists " +
                    $"(size={FormatBytes(prevInfo.Length)}, modified={prevInfo.LastWriteTime:u}). " +
                    $"A previous atomic write was interrupted between rename steps. " +
                    $"This rebuild will produce a new cache file. If you'd rather restore the prior cache, " +
                    $"cancel and rename '{prevPath}' → '{outputPath}' before retrying.");
            }

            if (tempExists)
            {
                var tempInfo = new FileInfo(tempPath);
                _logger.LogWarning(
                    $"{tag} STALE TEMP FILE DETECTED: '{tempPath}' exists (size={FormatBytes(tempInfo.Length)}, " +
                    $"modified={tempInfo.LastWriteTime:u}). A prior rebuild crashed during serialization. " +
                    $"This file will be deleted before the new rebuild writes.");
            }

            if (dirExists)
            {
                try
                {
                    var drive = new DriveInfo(Path.GetPathRoot(outputPath));
                    _logger.LogInfo($"{tag} target drive: {drive.Name}, free={FormatBytes(drive.AvailableFreeSpace)}, total={FormatBytes(drive.TotalSize)}");
                }
                catch (Exception ex)
                {
                    _logger.LogDebug($"{tag} could not query drive info: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"{tag} output diagnostics failed (non-fatal): {ex.GetType().Name}: {ex.Message}");
        }
    }

    private string ResolveCacheOutputPath(string navTypeName)
    {
        // _pathService.ModuleRootPath is .../Modules/TAOM. Distance cache lives in sibling
        // module TAOM_Map. Walk up one level then into TAOM_Map/ModuleData/DistanceCaches.
        var modulesDir = Path.GetFullPath(Path.Combine(_pathService.ModuleRootPath, ".."));
        return Path.Combine(modulesDir, "TAOM_Map", "ModuleData", "DistanceCaches", $"settlements_distance_cache_{navTypeName}.bin");
    }

    private void WriteOutputAtomically(INavigationCacheAdapter adapter, string finalPath, string tag)
    {
        var directory = Path.GetDirectoryName(finalPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            _logger.LogInfo($"{tag} creating missing output directory: {directory}");
            Directory.CreateDirectory(directory);
        }

        // Vanilla Serialize writes directly to the path. Write to a temp file then atomic-rename
        // so a crash mid-write can't corrupt the live cache file.
        var tempPath = finalPath + ".tmp";
        if (File.Exists(tempPath))
        {
            _logger.LogInfo($"{tag} removing stale temp file from prior aborted run: {tempPath}");
            File.Delete(tempPath);
        }

        _logger.LogInfo($"{tag} writing to temp file: {tempPath}");
        adapter.SerializeCache(tempPath);
        var tempSize = new FileInfo(tempPath).Length;
        _logger.LogInfo($"{tag} temp file written: {FormatBytes(tempSize)} ({tempSize:N0} bytes)");

        if (File.Exists(finalPath))
        {
            var backupPath = finalPath + ".prev";
            if (File.Exists(backupPath))
            {
                _logger.LogInfo($"{tag} removing prior .prev backup: {backupPath}");
                File.Delete(backupPath);
            }
            _logger.LogInfo($"{tag} renaming existing cache → .prev backup: {finalPath} → {backupPath}");
            File.Move(finalPath, backupPath);
        }

        _logger.LogInfo($"{tag} promoting temp file → final: {tempPath} → {finalPath}");
        File.Move(tempPath, finalPath);

        var finalSize = new FileInfo(finalPath).Length;
        _logger.LogInfo($"{tag} ATOMIC WRITE OK: {FormatBytes(finalSize)} live at {finalPath}");
    }

    // Allow up to 10% shortfall between expected and deserialized pair counts before warning.
    // Vanilla's distance dict is keyed on the inner cache element (port-vs-gate variant); for
    // NavigationType.Default we expect a 1:1 match with PairsComputed, but a small tolerance
    // absorbs any structural differences (e.g., zero-distance pairs that vanilla SetSettlement-
    // ToSettlementDistance silently dropped). A 90% shortfall would mean serialization truncated
    // mid-stream — a real corruption signal.
    private const double VerificationTolerance = 0.9;

    private void VerifyOutputRoundTrip(string outputPath, int expectedDistancePairs, int expectedNeighborPairs, string tag)
    {
        try
        {
            // Construct a fresh SandBoxNavigationCache and call Deserialize on the file we just wrote.
            // If the file is corrupt or format-invalid, vanilla Deserialize throws — we surface that.
            var verifyCache = new SandBoxNavigationCache(MobileParty.NavigationType.Default);
            var verifyAdapter = new NavigationCacheAdapter(verifyCache, logger: null);
            verifyAdapter.DeserializeCache(outputPath);
            var distanceCount = verifyAdapter.EnumerateExistingDistances().Count();
            var neighborCount = verifyAdapter.EnumerateExistingNeighbors().Count();

            var distanceMin = (int)(expectedDistancePairs * VerificationTolerance);
            var neighborMin = (int)(expectedNeighborPairs * VerificationTolerance);
            var distanceOk = expectedDistancePairs == 0 || distanceCount >= distanceMin;
            var neighborOk = expectedNeighborPairs == 0 || neighborCount >= neighborMin;

            if (distanceOk && neighborOk)
            {
                _logger.LogInfo($"{tag} round-trip OK: deserialized {distanceCount:N0} distance entries (expected ~{expectedDistancePairs:N0}) and {neighborCount:N0} neighbor entries (expected ~{expectedNeighborPairs:N0})");
            }
            else
            {
                _logger.LogError(
                    $"{tag} POST-WRITE VERIFICATION SHORTFALL: " +
                    $"distance={distanceCount:N0}/{expectedDistancePairs:N0} (min {distanceMin:N0}, ok={distanceOk}); " +
                    $"neighbor={neighborCount:N0}/{expectedNeighborPairs:N0} (min {neighborMin:N0}, ok={neighborOk}). " +
                    $"File MAY be truncated — consider restoring the .prev backup: '{outputPath}.prev' → '{outputPath}'.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"{tag} POST-WRITE VERIFICATION FAILED: {ex.GetType().Name}: {ex.Message}. File MAY be corrupt — keep the .prev backup as fallback.");
        }
    }

    private static string NewBuildId()
    {
        // Short 6-hex tag tying all log lines of one build together. Cheap to grep.
        var seed = Guid.NewGuid().GetHashCode() & 0xFFFFFF;
        return seed.ToString("X6");
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 0) return "n/a";
        if (bytes < 1024) return $"{bytes}B";
        if (bytes < 1024L * 1024) return $"{bytes / 1024.0:F1}KB";
        if (bytes < 1024L * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1}MB";
        return $"{bytes / (1024.0 * 1024 * 1024):F2}GB";
    }

    private static void Notify(string message)
    {
        SafeDisplay(message);
    }

    private static void NotifyOnMainThread(string message)
    {
        // InformationManager.DisplayMessage appends to a static queue that the UI thread
        // drains each frame. In practice this works from any thread. Worst case the message
        // is silently dropped — the log already captured the full result.
        SafeDisplay(message);
    }

    private static void SafeDisplay(string message)
    {
        try
        {
            InformationManager.DisplayMessage(new InformationMessage("[TAOM] " + message, Colors.Yellow));
        }
        catch
        {
            // suppress — caller already logged
        }
    }
}
