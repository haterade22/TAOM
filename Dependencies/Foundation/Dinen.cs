using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using HarmonyLib;
using TaleWorlds.Engine;
using TaleWorlds.Library;

namespace TAOM.Dependencies.Foundation;

/// <summary>
/// Dinen silences the Modding Kit's assert and warning boxes from inside the editor process, so the Kit loads without
/// a click on each one. It runs only when this DLL loads from Win64_Shipping_wEditor. It answers the boxes
/// TaleWorlds.Native.dll opens through its <c>MessageBoxA</c> import (<see cref="DinenPolicy.AnswerFor"/>), mutes
/// <c>Debug.Assert</c> and <c>Debug.FailedAssert</c> (the editor build keeps its Assert calls), turns off the dumps the engine writes for warnings, and returns -1 from
/// <c>GetActionCodeWithName</c> while no action codes are loaded. Each muted line goes once to diag.log.
/// Ported from QuietEditor (MIT, docs/reference/provenance-register.md); see docs/features/dinen.md.
/// </summary>
public static class Dinen
{
    private const string Tag = "Dinen";
    private const string HarmonyId = "TAOM.Dependencies.Foundation.Dinen";
    private const uint PageReadWrite = 0x04;

    private static readonly MessageBoxA Hook = AnswerMessageBox;
    private static readonly LogRepeatLimiter Limiter = new(512);
    private static MessageBoxA? _original;
    private static Func<int>? _actionCodeCount;
    private static int _installed;

    [UnmanagedFunctionPointer(CallingConvention.Winapi, CharSet = CharSet.Ansi)]
    private delegate int MessageBoxA(IntPtr owner, string text, string caption, uint style);

    /// <summary>True once <see cref="InstallIfEditor"/> has run in the Modding Kit.</summary>
    public static bool IsActive => Volatile.Read(ref _installed) == 1;

    /// <summary>
    /// Installs the hook and the three patches, once per process, when this DLL runs in the Modding Kit. Never throws:
    /// the Harmony work sits in a NoInlining helper, so a failed JIT of it is caught here too.
    /// </summary>
    public static void InstallIfEditor()
    {
        try
        {
            if (!IncompatibleModDetector.IsEditorBin(typeof(Dinen).Assembly.Location)
                || Interlocked.Exchange(ref _installed, 1) == 1)
                return;
            Install();
        }
        catch (Exception ex)
        {
            DiagLog.LogCaught(Tag, "InstallIfEditor", ex);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Install()
    {
        try
        {
            Note($"MessageBoxA hooked in {HookMessageBox()} import slot(s) of TaleWorlds.Native.dll");
        }
        catch (Exception ex)
        {
            DiagLog.LogCaught(Tag, "HookMessageBox", ex);
        }

        var harmony = new Harmony(HarmonyId);
        Patch(harmony, AccessTools.Method(typeof(Debug), nameof(Debug.Assert)), nameof(AssertPrefix));
        Patch(harmony, AccessTools.Method(typeof(Debug), nameof(Debug.FailedAssert)), nameof(FailedAssertPrefix));
        Patch(harmony, AccessTools.Method(AccessTools.TypeByName("ManagedCallbacks.ScriptingInterfaceOfIMBAnimation"),
            "GetActionCodeWithName"), nameof(ActionCodePrefix));
    }

    /// <summary>Runs the engine command that stops dumps for warnings. Call it from OnSubModuleLoad, after the engine is up.</summary>
    public static void StopWarningDumps()
    {
        if (!IsActive)
            return;
        try
        {
            var reply = (Utilities.ExecuteCommandLineCommand("debug.donot_create_dumps_for_warnings") ?? string.Empty).Trim();
            Note($"debug.donot_create_dumps_for_warnings: {reply}");
        }
        catch (Exception ex)
        {
            DiagLog.LogCaught(Tag, "StopWarningDumps", ex);
        }
    }

    private static int HookMessageBox()
    {
        var image = GetModuleHandle("TaleWorlds.Native.dll");
        if (image == IntPtr.Zero)
            return 0;

        var slots = DinenPolicy.FindImportSlots(
            offset => Marshal.ReadInt32(image, offset),
            offset => Marshal.ReadInt64(image, offset),
            offset => Marshal.PtrToStringAnsi(image + offset) ?? string.Empty,
            "user32.dll", "MessageBoxA");
        foreach (var offset in slots)
        {
            var slot = image + offset;
            _original ??= (MessageBoxA)Marshal.GetDelegateForFunctionPointer(Marshal.ReadIntPtr(slot), typeof(MessageBoxA));
            if (!VirtualProtect(slot, (UIntPtr)8u, PageReadWrite, out var previous))
                throw new InvalidOperationException($"VirtualProtect failed with error {Marshal.GetLastWin32Error()}");
            Marshal.WriteIntPtr(slot, Marshal.GetFunctionPointerForDelegate(Hook));
            VirtualProtect(slot, (UIntPtr)8u, previous, out _);
        }
        return slots.Count;
    }

    private static int AnswerMessageBox(IntPtr owner, string text, string caption, uint style)
    {
        var answer = DinenPolicy.ShowBox;
        try
        {
            answer = DinenPolicy.AnswerFor(caption, style);
            if (answer != DinenPolicy.ShowBox)
                Note($"answered {answer} to '{caption}': {text}");
        }
        catch (Exception ex)
        {
            DiagLog.LogCaught(Tag, "AnswerMessageBox", ex);
        }
        return answer != DinenPolicy.ShowBox ? answer : _original!(owner, text, caption, style);
    }

    private static bool AssertPrefix(bool condition, string message, string callerFile, string callerMethod, int callerLine)
    {
        if (!condition)
            Note($"muted Assert: {message} ({callerFile}:{callerLine} {callerMethod})");
        return false;
    }

    private static bool FailedAssertPrefix(string message, string callerFile, string callerMethod, int callerLine)
    {
        Note($"muted FailedAssert: {message} ({callerFile}:{callerLine} {callerMethod})");
        return false;
    }

    private static bool ActionCodePrefix(object __instance, string name, ref int __result)
    {
        try
        {
            _actionCodeCount ??= AccessTools.MethodDelegate<Func<int>>(
                AccessTools.Method(__instance.GetType(), "GetNumActionCodes"), __instance);
            if (_actionCodeCount() > 0)
                return true;
        }
        catch (Exception ex)
        {
            DiagLog.LogCaught(Tag, "ActionCodePrefix", ex);
            return true;
        }
        Note($"GetActionCodeWithName('{name}') before the action codes loaded: returned -1");
        __result = -1;
        return false;
    }

    private static void Patch(Harmony harmony, System.Reflection.MethodBase? target, string prefix)
    {
        try
        {
            if (target == null)
                throw new MissingMethodException($"no target for {prefix}");
            harmony.Patch(target, prefix: new HarmonyMethod(typeof(Dinen), prefix));
        }
        catch (Exception ex)
        {
            DiagLog.LogCaught(Tag, prefix, ex);
        }
    }

    private static void Note(string line)
    {
        if (Limiter.ShouldWrite(line))
            Limiter.WriteFinished(line, DiagLog.TryLog(Tag, line));
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string name);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool VirtualProtect(IntPtr address, UIntPtr size, uint protection, out uint previous);
}
