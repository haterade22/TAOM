using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Library;
using TaleWorlds.ModuleManager;
using TaleWorlds.MountAndBlade;
using TaleWorlds.CampaignSystem;
using TAOM.Core.Logging;

namespace TAOM.Features.MissionDiagnostic;

public sealed class MissionDiagnosticService : IMissionDiagnosticService
{
    private readonly IModLogger _logger;
    private readonly HashSet<string> _seenActionSets = new HashSet<string>(StringComparer.Ordinal);
    private bool _sessionLogged;

    public MissionDiagnosticService(IModLogger logger)
    {
        _logger = logger;
    }

    public void LogSessionSnapshot()
    {
        if (_sessionLogged) return;
        _sessionLogged = true;

        try
        {
            _logger.LogInfo("[MissionDiag] === Session snapshot ===");
            _logger.LogInfo($"[MissionDiag] OS: {Environment.OSVersion} (64-bit: {Environment.Is64BitOperatingSystem}), CLR: {Environment.Version}, machine: {Environment.MachineName}, cores: {Environment.ProcessorCount}");
            var nativeVer = ModuleHelper.GetModuleInfo("Native")?.Version.ToString() ?? "unknown";
            _logger.LogInfo($"[MissionDiag] Bannerlord (Native) version: {nativeVer}");

            var modules = ModuleHelper.GetActiveModules()?.ToList() ?? new List<ModuleInfo>();
            _logger.LogInfo($"[MissionDiag] Active modules ({modules.Count}):");
            foreach (var mod in modules)
            {
                var ver = mod.Version.ToString();
                _logger.LogInfo($"[MissionDiag]   {mod.Id} v{ver}");
            }

            // Dump TAOM-internal assembly references too — version drift on Harmony / MCM / ButterLib is a common cause of weird runtime bugs.
            var loaded = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => a.GetName().Name is var n && n != null && (n.StartsWith("Bannerlord") || n.StartsWith("MCM") || n.StartsWith("BUTR") || n.StartsWith("0Harmony") || n.StartsWith("HarmonyLib")))
                .Select(a => a.GetName())
                .OrderBy(n => n.Name)
                .ToList();
            _logger.LogInfo($"[MissionDiag] Mod-stack assemblies ({loaded.Count}):");
            foreach (var n in loaded)
                _logger.LogInfo($"[MissionDiag]   {n.Name} v{n.Version}");

            // Save-game context if a campaign is active. Each subfield is independently
            // guarded — OnGameStart runs before CampaignTime model is ready, so reading
            // CampaignTime.Now there NREs even when Campaign.Current is non-null.
            if (Campaign.Current != null && Campaign.Current.GameStarted)
            {
                string heroLine = "<no hero>";
                try
                {
                    var hero = Campaign.Current.MainParty?.LeaderHero;
                    if (hero != null)
                        heroLine = $"MainHero='{hero.Name}', culture='{hero.Culture?.StringId}', kingdom='{hero.Clan?.Kingdom?.StringId ?? "none"}'";
                }
                catch (Exception heroEx) { heroLine = $"<hero read failed: {heroEx.GetType().Name}>"; }

                string timeLine = "<time unavailable>";
                try { timeLine = $"now={CampaignTime.Now}"; }
                catch (Exception timeEx) { timeLine = $"<time read failed: {timeEx.GetType().Name}>"; }

                _logger.LogInfo($"[MissionDiag] Campaign: {timeLine}, {heroLine}");
            }
            else
            {
                _logger.LogInfo("[MissionDiag] Campaign: not active or not started yet at snapshot time");
            }
            _logger.LogInfo("[MissionDiag] === /Session snapshot ===");
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"[MissionDiag] LogSessionSnapshot failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    public void LogMissionStartSnapshot(
        string sceneName,
        IReadOnlyList<MissionBehavior> behaviors,
        IReadOnlyList<MissionLogic> missionLogics)
    {
        try
        {
            var nullCount = 0;
            var nullIndices = new List<int>();
            for (int i = 0; i < missionLogics.Count; i++)
            {
                if (missionLogics[i] == null)
                {
                    nullCount++;
                    nullIndices.Add(i);
                }
            }

            _logger.LogInfo($"[MissionDiag] === Mission start: scene='{sceneName}' behaviors={behaviors.Count} missionLogics={missionLogics.Count} nullSlots={nullCount} ===");

            // Dump every MissionBehavior with its classification. The line that
            // immediately precedes a null in MissionLogics is the offender (or one
            // of multiple — log them all).
            var lastLogicNonMissionLogic = new List<string>();
            for (int i = 0; i < behaviors.Count; i++)
            {
                var b = behaviors[i];
                if (b == null)
                {
                    _logger.LogError($"[MissionDiag]   [{i}] NULL MissionBehavior — someone added null directly");
                    continue;
                }
                var type = b.GetType();
                var isMissionLogic = b is MissionLogic;
                var behaviorType = b.BehaviorType;
                var asm = type.Assembly.GetName().Name;
                var suspect = behaviorType == MissionBehaviorType.Logic && !isMissionLogic;

                var line = $"[MissionDiag]   [{i}] {type.FullName}  BehaviorType={behaviorType}  IsMissionLogic={isMissionLogic}  asm={asm}";
                if (suspect)
                {
                    _logger.LogError(line + "  ← OFFENDER (BehaviorType=Logic but !MissionLogic — null-cast bug)");
                    lastLogicNonMissionLogic.Add($"{type.FullName} (asm={asm})");
                }
                else
                {
                    _logger.LogDebug(line);
                }
            }

            if (nullCount > 0)
            {
                _logger.LogError($"[MissionDiag] NULL ENTRIES in MissionLogics at indices: [{string.Join(", ", nullIndices)}]");
                if (lastLogicNonMissionLogic.Count > 0)
                {
                    _logger.LogError($"[MissionDiag] ROOT CAUSE: {lastLogicNonMissionLogic.Count} BehaviorType=Logic + !MissionLogic class(es) found:");
                    foreach (var name in lastLogicNonMissionLogic)
                        _logger.LogError($"[MissionDiag]   - {name}");
                    _logger.LogError("[MissionDiag] Will NRE in Mission.CheckMissionEnded every tick.");
                }
                else
                {
                    _logger.LogError("[MissionDiag] No BehaviorType=Logic+!MissionLogic suspects found in behaviors list — null may have been added directly via AddMissionBehavior(null).");
                }
            }

            _logger.LogInfo("[MissionDiag] === /Mission start ===");
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"[MissionDiag] LogMissionStartSnapshot failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    public void LogActionSetSeen(string actionSetName, string raceName, string agentName)
    {
        if (string.IsNullOrEmpty(actionSetName)) return;
        // Dedup by (actionSet, race) so we log once per unique combo per mission —
        // a `as_human_warrior` used by elf agents is the diagnostic signal.
        var key = $"{actionSetName}|{raceName}";
        if (!_seenActionSets.Add(key)) return;
        _logger.LogInfo($"[MissionDiag] ActionSet '{actionSetName}' used by race='{raceName}' (first agent: '{agentName}')");
    }

    public void ResetForNewMission()
    {
        _seenActionSets.Clear();
    }
}
