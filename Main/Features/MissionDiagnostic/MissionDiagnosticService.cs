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
            // The time half routinely fails HERE and that is expected: the snapshot runs before
            // Campaign.Models is built, so CampaignTime.ToString() hits GetYear, which divides by
            // a still-zero TimeTicksPerYear. The hero half survives on its own, and
            // LogMissionStartSnapshot repeats the line once models are up -- that is where the
            // date actually lands. On a save-load Campaign.GameStarted is ALREADY true here, so
            // the guard below does not protect this call (see CampaignContextFormatter).
            if (Campaign.Current != null && Campaign.Current.GameStarted)
                LogCampaignContext();
            else
                _logger.LogInfo("[MissionDiag] Campaign: not active or not started yet at snapshot time");
            _logger.LogInfo("[MissionDiag] === /Session snapshot ===");
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"[MissionDiag] LogSessionSnapshot failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private void LogCampaignContext() =>
        _logger.LogInfo($"[MissionDiag] Campaign: {CampaignContextFormatter.Describe(ReadCampaignTime, ReadMainHero)}");

    // Both readers are handed to the formatter as delegates so it owns the guarding; they must
    // stay thin enough that the only thing they can do is throw, which the formatter names.
    private static string ReadCampaignTime() => $"now={CampaignTime.Now}";

    private static string ReadMainHero()
    {
        var hero = Campaign.Current?.MainParty?.LeaderHero;
        return hero == null
            ? null
            : $"MainHero='{hero.Name}', culture='{hero.Culture?.StringId}', kingdom='{hero.Clan?.Kingdom?.StringId ?? "none"}'";
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

            // Repeated here deliberately. A crash bundle is correlated by save + in-game day, and
            // the session snapshot cannot read the date (see LogSessionSnapshot). Every mission
            // logs this block, so every crash that happens in a mission now carries the date.
            if (Campaign.Current != null && Campaign.Current.GameStarted)
                LogCampaignContext();

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

    public void LogActionSetSeen(string actionSetName, string raceName, bool isFemale, string agentName, string characterId, string monsterId)
    {
        if (string.IsNullOrEmpty(actionSetName)) return;
        // Dedup by (actionSet, race, sex) so we log once per unique combo per mission —
        // a `as_human_warrior` used by elf agents is the diagnostic signal. Sex is part of the
        // key because the whole female-vs-male action-set divergence lives in
        // ActionSetCode.GenerateActionSetNameWithSuffix; folding the sexes together hid it, and
        // player reports of race-specific breakage that only affects one sex are what we are
        // chasing (crash bundle d7d9f7d3 follow-up, 2026-08-06).
        var key = $"{actionSetName}|{raceName}|{isFemale}";
        if (!_seenActionSets.Add(key)) return;
        _logger.LogInfo(
            $"[MissionDiag] ActionSet '{actionSetName}' used by race='{raceName}' female={isFemale} " +
            $"monster='{monsterId ?? "<null>"}' (first agent: '{agentName}' char='{characterId ?? "<none>"}')");
    }

    public void ResetForNewMission()
    {
        _seenActionSets.Clear();
    }
}
