using System;
using System.Collections.Generic;
using System.Management;
using TAOM.Features.CrashReport.Domain;

namespace TAOM.Features.CrashReport.Collectors;

// Cached snapshot of WMI Win32_VideoController data, taken once on first request
// per process. WMI queries are slow (~50ms each) and the underlying hardware
// state doesn't change during a TAOM session, so re-running per crash is waste.
// Deep-review perf LOW (2026-05-25): the prior implementation ran two separate
// searchers on the same WMI class — consolidated to one query.
public sealed class GpuDisplayCollector
{
    private readonly object _lock = new();
    private GpuSnapshot? _cachedGpu;
    private DisplaySnapshot? _cachedDisplay;

    public GpuSnapshot CollectGpu()
    {
        EnsureSnapshot();
        return _cachedGpu ?? new GpuSnapshot(Array.Empty<GpuAdapterEntry>());
    }

    public DisplaySnapshot CollectDisplay()
    {
        EnsureSnapshot();
        return _cachedDisplay ?? new DisplaySnapshot(0, 0, 0, false, 0);
    }

    private void EnsureSnapshot()
    {
        if (_cachedGpu != null && _cachedDisplay != null) return;
        lock (_lock)
        {
            if (_cachedGpu != null && _cachedDisplay != null) return;
            QueryVideoControllers();
            QueryMonitorCount();
        }
    }

    private void QueryVideoControllers()
    {
        var gpuList = new List<GpuAdapterEntry>();
        int dispW = 0, dispH = 0, dispRefresh = 0;

        try
        {
            // Single searcher covers BOTH GPU adapter inventory AND display mode.
            // SELECT * is acceptable because Win32_VideoController is a small class
            // with ~30 fields per row and we hit it once per process.
            using var searcher = new ManagementObjectSearcher(
                "SELECT Name, DriverVersion, DriverDate, AdapterRAM, VideoProcessor, " +
                "CurrentHorizontalResolution, CurrentVerticalResolution, CurrentRefreshRate " +
                "FROM Win32_VideoController");

            foreach (ManagementObject obj in searcher.Get())
            {
                string name = obj["Name"]?.ToString() ?? "(unknown)";
                string? drv = obj["DriverVersion"]?.ToString();
                string? drvDate = ConvertWmiDate(obj["DriverDate"]?.ToString());
                long? ram = ParseLong(obj["AdapterRAM"]);
                string? vp = obj["VideoProcessor"]?.ToString();
                gpuList.Add(new GpuAdapterEntry(name, drv, drvDate, ram, vp));

                // First adapter with a non-zero resolution wins for display info.
                if (dispW == 0)
                {
                    int.TryParse(obj["CurrentHorizontalResolution"]?.ToString(), out var cw);
                    int.TryParse(obj["CurrentVerticalResolution"]?.ToString(), out var ch);
                    int.TryParse(obj["CurrentRefreshRate"]?.ToString(), out var cr);
                    if (cw > 0) { dispW = cw; dispH = ch; dispRefresh = cr; }
                }
            }
        }
        catch
        {
            // WMI can throw if the service is unhealthy; degrade gracefully.
        }

        _cachedGpu = new GpuSnapshot(gpuList);
        // Display snapshot finalised in QueryMonitorCount.
        _cachedDisplay = new DisplaySnapshot(dispW, dispH, dispRefresh, IsFullscreen: false, MonitorCount: 0);
    }

    private void QueryMonitorCount()
    {
        if (_cachedDisplay == null) return;
        int monitors = 0;
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_DesktopMonitor");
            foreach (var _ in searcher.Get()) monitors++;
        }
        catch { }
        // TaleWorlds.Engine.Screen has no public fullscreen flag in v1.4.5; left false.
        _cachedDisplay = _cachedDisplay with { MonitorCount = monitors };
    }

    private static string? ConvertWmiDate(string? wmi)
    {
        if (string.IsNullOrEmpty(wmi) || wmi.Length < 8) return wmi;
        try { return ManagementDateTimeConverter.ToDateTime(wmi).ToString("yyyy-MM-dd"); }
        catch { return wmi; }
    }

    private static long? ParseLong(object? v)
    {
        if (v == null) return null;
        try { return Convert.ToInt64(v); } catch { return null; }
    }
}
