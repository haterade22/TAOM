using System;
using System.Runtime.InteropServices;
using TAOM.Core.Logging;
using TaleWorlds.ModuleManager;

namespace TAOM.Features.NativeSkinFixes
{
    internal static class NativeHookLoader
    {
        private const string DllName = "TAOM.NativeSkinFixes";

        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr LoadLibrary(string lpFileName);

        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool SetDllDirectory(string? lpPathName);

        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Ansi, ExactSpelling = true)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        public static IntPtr HooksModule { get; private set; }
        public static IntPtr NativeBase { get; private set; }

        private static IModLogger? _logger;

        public static bool EnsureLoaded(IModLogger logger)
        {
            _logger = logger;

            if (HooksModule != IntPtr.Zero)
                return true;

            NativeBase = GetModuleHandle("TaleWorlds.Native.dll");
            if (NativeBase == IntPtr.Zero)
            {
                logger.LogError("[NativeSkinFixes] TaleWorlds.Native.dll not found in process");
                return false;
            }

            string modulePath = ModuleHelper.GetModuleFullPath("TAOM");
            string binDir = System.IO.Path.GetFullPath(
                System.IO.Path.Combine(modulePath, "bin", "Win64_Shipping_Client")
            );
            SetDllDirectory(binDir);
            HooksModule = LoadLibrary(DllName + ".dll");
            SetDllDirectory(null);

            if (HooksModule == IntPtr.Zero)
            {
                int error = Marshal.GetLastWin32Error();
                logger.LogError($"[NativeSkinFixes] Failed to load {DllName}.dll from {binDir} (Win32 error {error})");
                return false;
            }

            logger.LogInfo($"[NativeSkinFixes] Loaded {DllName}.dll from {binDir}");
            return true;
        }

        public static IntPtr ResolveRva(long rva) =>
            new IntPtr(NativeBase.ToInt64() + rva);

        public static T? GetExport<T>(string name) where T : Delegate
        {
            if (HooksModule == IntPtr.Zero)
                return null;

            IntPtr proc = GetProcAddress(HooksModule, name);
            if (proc == IntPtr.Zero)
            {
                _logger?.LogError($"[NativeSkinFixes] Export '{name}' not found in {DllName}.dll");
                return null;
            }
            return Marshal.GetDelegateForFunctionPointer<T>(proc);
        }
    }
}
