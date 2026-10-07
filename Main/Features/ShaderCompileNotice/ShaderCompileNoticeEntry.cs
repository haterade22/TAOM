using System;
using System.Collections.Generic;
using TAOM.Core.Logging;
using TaleWorlds.Localization;
using TaleWorlds.ModuleManager;

namespace TAOM.Features.ShaderCompileNotice;

/// <summary>
/// Engine boundary for <see cref="ShaderCompileNotice"/>: resolves its texts and the active module
/// list on the game thread (the engine has loaded every module's language files before any
/// <c>OnSubModuleLoad</c>, Module.cs v1.5.4 lines 277 and 282), then starts it. Fail-open: a notice
/// must never be the thing that stops the mod loading.
/// </summary>
public static class ShaderCompileNoticeEntry
{
    public static void Start(IModLogger logger, bool isDedicatedServer)
    {
        try
        {
            var ids = new List<string>();
            var modules = ModuleHelper.GetActiveModules();
            if (modules != null)
            {
                foreach (var module in modules)
                {
                    if (module != null) ids.Add(module.Id);
                }
            }

            if (!ShaderCompileProgress.ShouldStart(isDedicatedServer, ids)) return;
            ShaderCompileNotice.Start(ResolveTexts(), logger);
        }
        catch (Exception e)
        {
            logger.LogWarning($"[ShaderNotice] not started: {e.GetType().Name}: {e.Message}");
        }
    }

    public static void Stop() => ShaderCompileNotice.Stop();

    private static ShaderCompileNoticeTexts ResolveTexts()
    {
        var withTotal = new TextObject("{=taom_shader_notice_title_total}Compiling shaders: {COUNT} of about {TOTAL}");
        withTotal.SetTextVariable("COUNT", ShaderCompileNoticeTexts.CountToken);
        withTotal.SetTextVariable("TOTAL", ShaderCompileNoticeTexts.TotalToken);
        var countOnly = new TextObject("{=taom_shader_notice_title_count}Compiling shaders: {COUNT}");
        countOnly.SetTextVariable("COUNT", ShaderCompileNoticeTexts.CountToken);
        var detail = new TextObject(
            "{=taom_shader_notice_detail}The game is rebuilding its shaders after a change to your mod list or to the game. It is not frozen.");
        return new ShaderCompileNoticeTexts(withTotal.ToString(), countOnly.ToString(), detail.ToString());
    }
}
