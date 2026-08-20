using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using TAOM.Features.CrashReport.Domain;

namespace TAOM.Features.CrashReport.Rendering;

// Sectioned, greppable plain-text rendering. Format is stable — section headers
// use `--- <Name> ---` so post-mortem scripts can split a report into sections.
public sealed class PlainTextCrashReportRenderer : ICrashReportRenderer
{
    private const string SectionRule = "===================================================================";
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public string Render(ExceptionContext c)
    {
        var sb = new StringBuilder(64 * 1024);

        sb.AppendLine(SectionRule);
        sb.AppendLine($"TAOM CrashReport — captured {c.CapturedAtUtc:yyyy-MM-dd HH:mm:ss.fff} UTC");
        sb.AppendLine($"Signature: {c.CrashSignature}");
        sb.AppendLine(SectionRule);
        sb.AppendLine();

        RenderIdentity(sb, c.Identity);
        RenderException(sb, c.Exception);
        RenderStackFrames(sb, c.StackFrames);
        RenderHarmony(sb, c.Harmony);
        RenderModules(sb, c.Modules);
        RenderAssemblies(sb, c.Assemblies);
        RenderCampaign(sb, c.Campaign);
        RenderMission(sb, c.Mission);
        RenderTaomState(sb, c.Taom);
        RenderMcm(sb, c.Mcm);
        RenderProcess(sb, c.Process);
        RenderGpu(sb, c.Gpu);
        RenderDisplay(sb, c.Display);
        RenderOs(sb, c.Os);
        RenderAppDomain(sb, c.AppDomain, c.EnvVars);
        RenderPerformance(sb, c.Performance);
        RenderLogs(sb, c.Logs);
        RenderCollectorFailures(sb, c.CollectorFailures);

        sb.AppendLine(SectionRule);
        return sb.ToString();
    }

    private static void Section(StringBuilder sb, string name)
    {
        sb.AppendLine();
        sb.AppendLine($"--- {name} ---");
    }

    private static void RenderIdentity(StringBuilder sb, IdentitySnapshot id)
    {
        Section(sb, "Identity");
        sb.AppendLine($"Bannerlord: {id.BannerlordVersion} (exe {id.BannerlordExeFileVersion})");
        sb.AppendLine($"TAOM:       {id.TaomVersion} (dll sha1 {id.TaomDllSha1})");
        sb.AppendLine($"Origin:     {id.OriginatingPatchTarget}");
        sb.AppendLine($"Language:   {id.LanguageCode}");
    }

    private static void RenderException(StringBuilder sb, ExceptionFrame? ex)
    {
        Section(sb, "Exception");
        if (ex == null) { sb.AppendLine("(no exception captured)"); return; }
        RenderExceptionFrame(sb, ex, depth: 0);
    }

    private static void RenderExceptionFrame(StringBuilder sb, ExceptionFrame ex, int depth)
    {
        var indent = new string(' ', depth * 2);
        if (depth > 0) sb.AppendLine($"{indent}--- Inner Exception (depth {depth}) ---");
        sb.AppendLine($"{indent}Type:       {ex.Type}");
        sb.AppendLine($"{indent}Message:    {ex.Message}");
        sb.AppendLine($"{indent}Source:     {ex.Source ?? "(null)"}");
        sb.AppendLine($"{indent}HResult:    0x{ex.HResult:X8}");
        sb.AppendLine($"{indent}TargetSite: {ex.TargetSiteFullName ?? "(null)"}");
        if (ex.Data.Count > 0)
        {
            sb.AppendLine($"{indent}Data:");
            foreach (var d in ex.Data) sb.AppendLine($"{indent}  {d.Key} = {d.Value}");
        }
        if (!string.IsNullOrEmpty(ex.StackTrace))
        {
            sb.AppendLine($"{indent}StackTrace:");
            foreach (var line in ex.StackTrace!.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                sb.AppendLine($"{indent}  {line.TrimStart()}");
        }
        if (ex.InnerException != null) RenderExceptionFrame(sb, ex.InnerException, depth + 1);
    }

    private static void RenderStackFrames(StringBuilder sb, IReadOnlyList<StackFrameSnapshot> frames)
    {
        Section(sb, $"Stack Frames ({frames.Count})");
        if (frames.Count == 0) { sb.AppendLine("(stack trace unavailable)"); return; }
        foreach (var f in frames)
        {
            sb.AppendLine($"[{f.Index:D3}] {f.MethodFullName ?? "<unknown>"}  asm={f.DeclaringAssemblyName ?? "?"}");
            if (!string.IsNullOrEmpty(f.FileName))
                sb.AppendLine($"      file={f.FileName} line={f.FileLine} IL=0x{f.IlOffset:X}");
        }
    }

    private static void RenderHarmony(StringBuilder sb, HarmonyCorrelationSnapshot h)
    {
        Section(sb, "Harmony Correlation");
        sb.AppendLine($"Total patched methods in process: {h.TotalPatchedMethods}");
        sb.AppendLine($"Top owners by patch count:");
        foreach (var o in h.AllOwnersByPatchCount.Take(20))
            sb.AppendLine($"  {o.Owner.PadRight(60)} {o.PatchCount,4}");
        if (h.AllOwnersByPatchCount.Count > 20) sb.AppendLine($"  ... +{h.AllOwnersByPatchCount.Count - 20} more");

        sb.AppendLine();
        sb.AppendLine("Patches on throwing call stack:");
        foreach (var frame in h.PatchesPerStackFrame)
        {
            sb.AppendLine($"  Frame [{frame.FrameIndex:D3}] {frame.MethodFullName}:");
            if (frame.Patches.Count == 0) { sb.AppendLine("    (no patches)"); continue; }
            foreach (var p in frame.Patches)
                sb.AppendLine($"    {p.PatchType,-10} pri={p.Priority,-4} {p.Owner}  {p.PatchMethodFullName}");
        }
    }

    private static void RenderModules(StringBuilder sb, ModuleInventorySnapshot mods)
    {
        Section(sb, $"Modules ({mods.Modules.Count})");
        foreach (var m in mods.Modules)
        {
            var officialTag = m.IsOfficial ? "official" : "mod     ";
            var inversionTag = m.HasDependencyOrderInversion ? "  ⚠ DEP-ORDER-INVERSION" : "";
            sb.AppendLine($"[{m.LoadIndex:D2}] {m.Id.PadRight(40)} v{m.Version.PadRight(15)} {officialTag}  sha1={m.MainDllSha1}{inversionTag}");
            if (m.DeclaredDependencies.Count > 0)
                sb.AppendLine($"     deps: {string.Join(", ", m.DeclaredDependencies)}");
            if (!string.IsNullOrEmpty(m.MainDllPath))
                sb.AppendLine($"     path: {m.MainDllPath}");
            if (m.XmlFilePaths.Count > 0)
                sb.AppendLine($"     xml ({m.XmlFilePaths.Count}): {string.Join(", ", m.XmlFilePaths)}");
        }
    }

    private static void RenderAssemblies(StringBuilder sb, AssemblyInventorySnapshot a)
    {
        Section(sb, $"Assemblies ({a.Assemblies.Count})");
        foreach (var asm in a.Assemblies)
        {
            var gacTag = asm.IsInGac ? " (GAC)" : "";
            sb.AppendLine($"  {asm.Name.PadRight(55)} v{asm.Version}{gacTag}");
            if (!string.IsNullOrEmpty(asm.Location))
                sb.AppendLine($"    {asm.Location}");
        }
    }

    private static void RenderCampaign(StringBuilder sb, CampaignSnapshot? c)
    {
        Section(sb, "Campaign");
        if (c == null) { sb.AppendLine("(no active campaign at crash time)"); return; }
        sb.AppendLine($"UniqueGameId: {c.UniqueGameId ?? "?"}");
        sb.AppendLine($"Time:         {c.CampaignTimeNow ?? "?"}  (day {c.DayOfSeason}/{c.Season}, year {c.Year})");
        sb.AppendLine($"Autosave:     {c.AutosaveEnabled?.ToString() ?? "?"}   last save: {c.LastSaveTimestampUtc ?? "?"}");
        if (c.PlayerHero != null)
        {
            var h = c.PlayerHero;
            sb.AppendLine($"Hero:         {h.Name} (lvl {h.Level}, age {h.Age}, race={h.Race ?? "?"})");
            sb.AppendLine($"              culture={h.CultureId ?? "?"}  clan={h.ClanId ?? "?"}  kingdom={h.KingdomId ?? "?"}");
            sb.AppendLine($"              gold={h.Gold}  renown={h.Renown}  influence={h.Influence}");
        }
        if (c.PlayerParty != null)
        {
            var p = c.PlayerParty;
            sb.AppendLine($"Party:        {p.MemberCount} total, {p.WoundedCount} wounded, {p.PrisonerCount} prisoners");
            sb.AppendLine($"              morale={p.Morale} food={p.Food}");
            if (p.TroopHistogram.Count > 0)
                sb.AppendLine($"              tiers: {string.Join("  ", p.TroopHistogram.Select(t => $"T{t.Tier}={t.Count}"))}");
        }
        if (c.CurrentLocation != null)
        {
            var loc = c.CurrentLocation;
            if (loc.IsOnMap)
                sb.AppendLine($"Location:     open map @ ({loc.MapPositionX.ToString("F1", Inv)}, {loc.MapPositionY.ToString("F1", Inv)})");
            else
                sb.AppendLine($"Location:     {loc.SettlementId} \"{loc.SettlementName}\" culture={loc.SettlementCulture ?? "?"}");
        }
        if (c.RecentEvents.Count > 0)
        {
            sb.AppendLine($"Recent CampaignEvents (last {c.RecentEvents.Count}):");
            foreach (var e in c.RecentEvents) sb.AppendLine($"  {e.CapturedAtUtc}  {e.EventName}");
        }
    }

    private static void RenderMission(StringBuilder sb, MissionSnapshot? m)
    {
        Section(sb, "Mission");
        if (m == null) { sb.AppendLine("(no active mission at crash time)"); return; }
        sb.AppendLine($"Scene:    {m.SceneName ?? "?"}");
        sb.AppendLine($"Mode:     {m.MissionMode ?? "?"}");
        sb.AppendLine($"TimeOfDay: {m.TimeOfDay?.ToString("F2", Inv) ?? "?"}  weather={m.Weather ?? "?"}");
        sb.AppendLine($"Teams ({m.Teams.Count}):");
        foreach (var t in m.Teams)
            sb.AppendLine($"  [{t.Index}] side={t.Side} alive={t.AgentsAlive}/{t.AgentsTotal}{(t.IsPlayerTeam ? "  ← player" : "")}");
        if (m.PlayerFormations.Count > 0)
        {
            sb.AppendLine($"Player Formations ({m.PlayerFormations.Count}):");
            foreach (var f in m.PlayerFormations)
                sb.AppendLine($"  {f.Class.PadRight(15)} alive={f.CountAlive}/{f.CountTotal}  movement={f.MovementOrder ?? "?"}  facing={f.FacingOrder ?? "?"}  arr={f.ArrangementOrder ?? "?"}");
        }
        if (m.PlayerAgent != null)
        {
            var p = m.PlayerAgent;
            sb.AppendLine($"Player Agent: health={p.Health.ToString("F0", Inv)}/{p.MaxHealth.ToString("F0", Inv)}  pos=({p.PositionX.ToString("F1", Inv)},{p.PositionY.ToString("F1", Inv)},{p.PositionZ.ToString("F1", Inv)})  mounted={p.IsMounted}  wielded={p.WieldedItemId ?? "(none)"}");
        }
    }

    private static void RenderTaomState(StringBuilder sb, TaomStateSnapshot t)
    {
        Section(sb, "TAOM State");
        if (t.Career != null)
            sb.AppendLine($"Career:        {t.Career.SelectedCareerId ?? "(none)"} (level {t.Career.CareerLevel}) passives=[{string.Join(", ", t.Career.ActivePassives)}]");
        else
            sb.AppendLine("Career:        (no selection)");

        if (t.SpecialResources.Count > 0)
        {
            sb.AppendLine($"Special Resources ({t.SpecialResources.Count}):");
            foreach (var r in t.SpecialResources) sb.AppendLine($"  {r.KingdomId}  {r.ResourceId} = {r.Balance}");
        }
        if (t.CulturalFeats != null)
            sb.AppendLine($"Cultural Feats: player culture={t.CulturalFeats.PlayerCultureId ?? "?"}  active=[{string.Join(", ", t.CulturalFeats.ActiveFeats)}]");
        if (t.RevoltTuning != null)
        {
            var r = t.RevoltTuning;
            sb.AppendLine($"Revolt Tuning: threshold={r.LoyaltyThresholdToRevolt.ToString("F2", Inv)} cooldown={r.CooldownDaysBetweenRevolts.ToString("F1", Inv)} diffCulture={r.DifferentCultureLoyaltyPenalty.ToString("F2", Inv)} occupier={r.OccupierKingdomLoyaltyPenalty.ToString("F2", Inv)}");
        }
        sb.AppendLine($"Messengers in flight: {t.ActiveMessengersCount?.ToString() ?? "?"}");
    }

    private static void RenderMcm(StringBuilder sb, McmSettingsSnapshot m)
    {
        Section(sb, $"MCM Settings ({m.Providers.Count} provider(s))");
        if (m.Providers.Count == 0) { sb.AppendLine("(no MCM providers reachable)"); return; }
        foreach (var p in m.Providers)
        {
            sb.AppendLine($"  [{p.AssemblyName}] {p.DisplayName} ({p.ProviderId}):");
            foreach (var s in p.Settings)
                sb.AppendLine($"    {s.PropertyPath.PadRight(50)} = {s.ValueAsString}  ({s.TypeName})");
        }
    }

    private static void RenderProcess(StringBuilder sb, ProcessSnapshot p)
    {
        Section(sb, "Process");
        sb.AppendLine($"WorkingSet:    {Bytes(p.WorkingSetBytes)}");
        sb.AppendLine($"PrivateMemory: {Bytes(p.PrivateMemoryBytes)}");
        sb.AppendLine($"GC.Total:      {Bytes(p.GcTotalMemoryBytes)}   gen0={p.Gen0CollectionCount} gen1={p.Gen1CollectionCount} gen2={p.Gen2CollectionCount}");
        sb.AppendLine($"Threads:       process={p.ProcessThreadCount} managed={p.ManagedThreadCount}");
        sb.AppendLine($"Uptime:        {TimeSpan.FromSeconds(p.UptimeSeconds)}");
        var t = p.ThrowingThread;
        sb.AppendLine($"Throwing thread: id={t.ManagedThreadId} name=\"{t.Name ?? ""}\" bg={t.IsBackground} apt={t.ApartmentState}");
    }

    private static void RenderGpu(StringBuilder sb, GpuSnapshot g)
    {
        Section(sb, $"GPU ({g.Adapters.Count} adapter(s))");
        if (g.Adapters.Count == 0) { sb.AppendLine("(WMI unreachable)"); return; }
        foreach (var a in g.Adapters)
        {
            sb.AppendLine($"  {a.Name}");
            sb.AppendLine($"    driver={a.DriverVersion ?? "?"} date={a.DriverDate ?? "?"} vram={(a.AdapterRamBytes.HasValue ? Bytes(a.AdapterRamBytes.Value) : "?")}  videoProc={a.VideoProcessor ?? "?"}");
        }
    }

    private static void RenderDisplay(StringBuilder sb, DisplaySnapshot d)
    {
        Section(sb, "Display");
        sb.AppendLine($"{d.ResolutionWidth}x{d.ResolutionHeight} @ {d.RefreshRate}Hz  fullscreen={d.IsFullscreen}  monitors={d.MonitorCount}");
    }

    private static void RenderOs(StringBuilder sb, OsSnapshot o)
    {
        Section(sb, "OS");
        sb.AppendLine($"OS:      {o.OsDescription} ({o.OsVersion}, 64-bit={o.Is64BitOs}, {o.ArchitectureLabel}, {o.LogicalProcessorCount} cores)");
        sb.AppendLine($"Locale:  {o.CurrentCulture}  UI={o.CurrentUiCulture}");
        sb.AppendLine($"CLR:     {o.ClrVersion}");
    }

    private static void RenderAppDomain(StringBuilder sb, AppDomainSnapshot d, IReadOnlyList<EnvVarEntry> env)
    {
        Section(sb, "AppDomain & Environment");
        sb.AppendLine($"FriendlyName: {d.FriendlyName}");
        sb.AppendLine($"BaseDir:      {d.BaseDirectory}");
        sb.AppendLine($"RelativeSearchPath: {d.RelativeSearchPath ?? "(none)"}");
        sb.AppendLine($"FullyTrusted: {d.IsFullyTrusted}");
        sb.AppendLine("Env vars (filtered):");
        if (env.Count == 0) { sb.AppendLine("  (none matching filter)"); }
        else foreach (var e in env) sb.AppendLine($"  {e.Name} = {e.Value}");
    }

    private static void RenderPerformance(StringBuilder sb, FrameTimingSnapshot p)
    {
        Section(sb, "Performance");
        sb.AppendLine($"Samples:      {p.SampleCount}");
        sb.AppendLine($"Avg frame ms: {p.AverageFrameTimeMs.ToString("F2", Inv)}");
        sb.AppendLine($"FPS at crash: {p.FpsAtCrash.ToString("F1", Inv)}");
        if (p.RecentFrameTimesMs.Count > 0)
            sb.AppendLine($"Recent ms:    {string.Join(", ", p.RecentFrameTimesMs.Select(x => x.ToString("F1", Inv)))}");
    }

    private static void RenderLogs(StringBuilder sb, LogTailSnapshot l)
    {
        Section(sb, "Logs");
        sb.AppendLine($"TAOM debug log: {l.TaomDebugLogPath ?? "?"}");
        if (l.TaomDebugLogTail.Count > 0)
        {
            sb.AppendLine($"TAOM tail (last {l.TaomDebugLogTail.Count}):");
            foreach (var line in l.TaomDebugLogTail) sb.AppendLine($"  {line}");
        }
        sb.AppendLine($"Dependencies diag log: {l.DiagLogPath ?? "?"}");
        if (l.DiagLogTail.Count > 0)
        {
            sb.AppendLine($"diag tail (last {l.DiagLogTail.Count}):");
            foreach (var line in l.DiagLogTail) sb.AppendLine($"  {line}");
        }
        sb.AppendLine($"RGL log: {l.RglLogPath ?? "?"}");
        if (l.RglLogTail.Count > 0)
        {
            sb.AppendLine($"RGL tail (last {l.RglLogTail.Count}):");
            foreach (var line in l.RglLogTail) sb.AppendLine($"  {line}");
        }
    }

    private static void RenderCollectorFailures(StringBuilder sb, IReadOnlyList<CollectorFailure> fails)
    {
        Section(sb, "Collector failures");
        if (fails.Count == 0) { sb.AppendLine("(none — full report)"); return; }
        foreach (var f in fails)
            sb.AppendLine($"  {f.Collector}: {f.ExceptionType}: {f.Message}");
    }

    private static string Bytes(long b)
    {
        if (b < 1024) return $"{b} B";
        double v = b;
        string[] u = { "KB", "MB", "GB", "TB" };
        int i = -1;
        do { v /= 1024.0; i++; } while (v >= 1024 && i < u.Length - 1);
        return $"{v.ToString("F2", Inv)} {u[i]}";
    }
}
