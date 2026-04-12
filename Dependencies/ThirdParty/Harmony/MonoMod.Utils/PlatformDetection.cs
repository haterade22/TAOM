using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using MonoMod.Utils.Interop;

namespace MonoMod.Utils;

internal static class PlatformDetection
{
	private static int platInitState;

	private static OSKind os;

	private static ArchitectureKind arch;

	private static int runtimeInitState;

	private static RuntimeKind runtime;

	private static CorelibKind corelib;

	private static Version? runtimeVersion;

	public static OSKind OS
	{
		get
		{
			EnsurePlatformInfoInitialized();
			return os;
		}
	}

	public static ArchitectureKind Architecture
	{
		get
		{
			EnsurePlatformInfoInitialized();
			return arch;
		}
	}

	public static RuntimeKind Runtime
	{
		get
		{
			EnsureRuntimeInitialized();
			return runtime;
		}
	}

	public static CorelibKind Corelib
	{
		get
		{
			EnsureRuntimeInitialized();
			return corelib;
		}
	}

	public static Version RuntimeVersion
	{
		get
		{
			EnsureRuntimeInitialized();
			return runtimeVersion;
		}
	}

	private static void EnsurePlatformInfoInitialized()
	{
		if (platInitState == 0)
		{
			(os, arch) = DetectPlatformInfo();
			Thread.MemoryBarrier();
			Interlocked.Exchange(ref platInitState, 1);
		}
	}

	private static (OSKind OS, ArchitectureKind Arch) DetectPlatformInfo()
	{
		OSKind oSKind = OSKind.Unknown;
		ArchitectureKind architectureKind = ArchitectureKind.Unknown;
		PropertyInfo property = typeof(Environment).GetProperty("Platform", BindingFlags.Static | BindingFlags.NonPublic);
		string self = ((!(property != null)) ? Environment.OSVersion.Platform.ToString() : property.GetValue(null, null)?.ToString())?.ToUpperInvariant() ?? "";
		if (self.Contains("WIN", StringComparison.Ordinal))
		{
			oSKind = OSKind.Windows;
		}
		else if (self.Contains("MAC", StringComparison.Ordinal) || self.Contains("OSX", StringComparison.Ordinal))
		{
			oSKind = OSKind.OSX;
		}
		else if (self.Contains("LIN", StringComparison.Ordinal))
		{
			oSKind = OSKind.Linux;
		}
		else if (self.Contains("BSD", StringComparison.Ordinal))
		{
			oSKind = OSKind.BSD;
		}
		else if (self.Contains("UNIX", StringComparison.Ordinal))
		{
			oSKind = OSKind.Posix;
		}
		if (oSKind == OSKind.Windows)
		{
			DetectInfoWindows(ref oSKind, ref architectureKind);
		}
		else if ((oSKind & OSKind.Posix) != OSKind.Unknown)
		{
			DetectInfoPosix(ref oSKind, ref architectureKind);
		}
		if (oSKind != OSKind.Unknown)
		{
			if (oSKind == OSKind.Linux && Directory.Exists("/data") && File.Exists("/system/build.prop"))
			{
				oSKind = OSKind.Android;
			}
			else if (oSKind == OSKind.Posix && Directory.Exists("/Applications") && Directory.Exists("/System") && Directory.Exists("/User") && !Directory.Exists("/Users"))
			{
				oSKind = OSKind.IOS;
			}
			else if (oSKind == OSKind.Windows && CheckWine())
			{
				oSKind = OSKind.Wine;
			}
		}
		bool isEnabled;
		MMDbgLog.DebugLogInfoStringHandler message = new MMDbgLog.DebugLogInfoStringHandler(16, 2, out isEnabled);
		if (isEnabled)
		{
			message.AppendLiteral("Platform info: ");
			message.AppendFormatted(oSKind);
			message.AppendLiteral(" ");
			message.AppendFormatted(architectureKind);
		}
		MMDbgLog.Info(ref message);
		return (OS: oSKind, Arch: architectureKind);
	}

	private unsafe static int PosixUname(OSKind os, byte* buf)
	{
		if (os != OSKind.OSX)
		{
			return Libc(buf);
		}
		return Osx(buf);
		unsafe static int Libc(byte* buf2)
		{
			return Unix.Uname(buf2);
		}
		unsafe static int Osx(byte* buf2)
		{
			return OSX.Uname(buf2);
		}
	}

	private unsafe static string GetCString(ReadOnlySpan<byte> buffer, out int nullByte)
	{
		fixed (byte* ptr = buffer)
		{
			return Marshal.PtrToStringAnsi((IntPtr)ptr, nullByte = buffer.IndexOf<byte>(0));
		}
	}

	private unsafe static void DetectInfoPosix(ref OSKind os, ref ArchitectureKind arch)
	{
		bool isEnabled;
		try
		{
			Span<byte> span = new byte[3078];
			fixed (byte* buf = span)
			{
				if (PosixUname(os, buf) < 0)
				{
					string message = new Win32Exception(Marshal.GetLastWin32Error()).Message;
					MMDbgLog.DebugLogErrorStringHandler message2 = new MMDbgLog.DebugLogErrorStringHandler(24, 1, out isEnabled);
					if (isEnabled)
					{
						message2.AppendLiteral("uname() syscall failed! ");
						message2.AppendFormatted(message);
					}
					MMDbgLog.Error(ref message2);
					return;
				}
			}
			int nullByte;
			string text = GetCString(span, out nullByte).ToUpperInvariant();
			span = span.Slice(nullByte);
			MMDbgLog.DebugLogTraceStringHandler message3 = new MMDbgLog.DebugLogTraceStringHandler(22, 1, out isEnabled);
			if (isEnabled)
			{
				message3.AppendLiteral("uname() call returned ");
				message3.AppendFormatted(text);
			}
			MMDbgLog.Trace(ref message3);
			if (text.Contains("LINUX", StringComparison.Ordinal))
			{
				os = OSKind.Linux;
			}
			else if (text.Contains("DARWIN", StringComparison.Ordinal))
			{
				os = OSKind.OSX;
			}
			else if (text.Contains("BSD", StringComparison.Ordinal))
			{
				os = OSKind.BSD;
			}
			string self = GetMachineNamePosix(os, span).ToUpperInvariant();
			if (self.Contains("X86_64", StringComparison.Ordinal) || self.Contains("AMD64", StringComparison.Ordinal))
			{
				arch = ArchitectureKind.x86_64;
			}
			else if (self.Contains("X86", StringComparison.Ordinal) || self.Contains("I686", StringComparison.Ordinal))
			{
				arch = ArchitectureKind.x86;
			}
			else if (self.Contains("AARCH64", StringComparison.Ordinal) || self.Contains("ARM64", StringComparison.Ordinal))
			{
				arch = ArchitectureKind.Arm64;
			}
			else if (self.Contains("ARM", StringComparison.Ordinal))
			{
				arch = ArchitectureKind.Arm;
			}
			MMDbgLog.DebugLogTraceStringHandler message4 = new MMDbgLog.DebugLogTraceStringHandler(37, 2, out isEnabled);
			if (isEnabled)
			{
				message4.AppendLiteral("uname() detected architecture info: ");
				message4.AppendFormatted(os);
				message4.AppendLiteral(" ");
				message4.AppendFormatted(arch);
			}
			MMDbgLog.Trace(ref message4);
		}
		catch (Exception value)
		{
			MMDbgLog.DebugLogErrorStringHandler message5 = new MMDbgLog.DebugLogErrorStringHandler(49, 1, out isEnabled);
			if (isEnabled)
			{
				message5.AppendLiteral("Error trying to detect info on POSIX-like system ");
				message5.AppendFormatted(value);
			}
			MMDbgLog.Error(ref message5);
		}
	}

	private unsafe static string GetMachineNamePosix(OSKind os, Span<byte> unameBuffer)
	{
		string text = null;
		bool isEnabled;
		int i;
		if (os == OSKind.Linux)
		{
			if (DynDll.OpenLibrary("libc").TryGetExport("getauxval", out var functionPtr))
			{
				IntPtr intPtr = ((delegate* unmanaged[Cdecl]<IntPtr, IntPtr>)(void*)functionPtr)((IntPtr)15);
				if (intPtr != (IntPtr)0)
				{
					text = Marshal.PtrToStringAnsi(intPtr);
					MMDbgLog.DebugLogTraceStringHandler message = new MMDbgLog.DebugLogTraceStringHandler(35, 1, out isEnabled);
					if (isEnabled)
					{
						message.AppendLiteral("Got architecture from getauxval(): ");
						message.AppendFormatted(text);
					}
					MMDbgLog.Trace(ref message);
				}
			}
			if (text == null)
			{
				try
				{
					Span<Unix.LinuxAuxvEntry> span = MemoryMarshal.Cast<byte, Unix.LinuxAuxvEntry>(Helpers.ReadAllBytes("/proc/self/auxv").AsSpan());
					text = string.Empty;
					Span<Unix.LinuxAuxvEntry> span2 = span;
					for (i = 0; i < span2.Length; i++)
					{
						Unix.LinuxAuxvEntry linuxAuxvEntry = span2[i];
						if (linuxAuxvEntry.Key == 15)
						{
							text = Marshal.PtrToStringAnsi(linuxAuxvEntry.Value) ?? string.Empty;
							break;
						}
					}
					if (text.Length == 0)
					{
						MMDbgLog.DebugLogWarningStringHandler message2 = new MMDbgLog.DebugLogWarningStringHandler(56, 1, out isEnabled);
						if (isEnabled)
						{
							message2.AppendLiteral("Auxv table did not inlcude useful AT_PLATFORM (0x");
							message2.AppendFormatted(15, "x");
							message2.AppendLiteral(") entry");
						}
						MMDbgLog.Warning(ref message2);
						Span<Unix.LinuxAuxvEntry> span3 = span;
						for (i = 0; i < span3.Length; i++)
						{
							Unix.LinuxAuxvEntry linuxAuxvEntry2 = span3[i];
							MMDbgLog.DebugLogTraceStringHandler message3 = new MMDbgLog.DebugLogTraceStringHandler(3, 2, out isEnabled);
							if (isEnabled)
							{
								message3.AppendFormatted(linuxAuxvEntry2.Key, "x16");
								message3.AppendLiteral(" = ");
								message3.AppendFormatted(linuxAuxvEntry2.Value, "x16");
							}
							MMDbgLog.Trace(ref message3);
						}
						text = null;
					}
					else
					{
						MMDbgLog.DebugLogTraceStringHandler message4 = new MMDbgLog.DebugLogTraceStringHandler(43, 1, out isEnabled);
						if (isEnabled)
						{
							message4.AppendLiteral("Got architecture name ");
							message4.AppendFormatted(text);
							message4.AppendLiteral(" from /proc/self/auxv");
						}
						MMDbgLog.Trace(ref message4);
					}
				}
				catch (UnauthorizedAccessException value)
				{
					MMDbgLog.Warning("Could not read /proc/self/auxv, and libc does not have getauxval");
					MMDbgLog.Warning("Falling back to parsing out of uname() result...");
					MMDbgLog.DebugLogWarningStringHandler message5 = new MMDbgLog.DebugLogWarningStringHandler(0, 1, out isEnabled);
					if (isEnabled)
					{
						message5.AppendFormatted(value);
					}
					MMDbgLog.Warning(ref message5);
				}
			}
		}
		if (text == null)
		{
			for (int j = 0; j < 4; j++)
			{
				if (j != 0)
				{
					int num = unameBuffer.IndexOf<byte>(0);
					unameBuffer = unameBuffer.Slice(num);
					if (j == 1 && num < 5 && unameBuffer.Length >= 2 && unameBuffer[1] != 0)
					{
						num = unameBuffer.Slice(1).IndexOf<byte>(0);
						unameBuffer = unameBuffer.Slice(num + 1);
					}
				}
				int k;
				for (k = 0; k < unameBuffer.Length && unameBuffer[k] == 0; k++)
				{
				}
				unameBuffer = unameBuffer.Slice(k);
			}
			text = GetCString(unameBuffer, out i);
			MMDbgLog.DebugLogTraceStringHandler message6 = new MMDbgLog.DebugLogTraceStringHandler(35, 1, out isEnabled);
			if (isEnabled)
			{
				message6.AppendLiteral("Got architecture name ");
				message6.AppendFormatted(text);
				message6.AppendLiteral(" from uname()");
			}
			MMDbgLog.Trace(ref message6);
		}
		return text;
	}

	private unsafe static void DetectInfoWindows(ref OSKind os, ref ArchitectureKind arch)
	{
		MonoMod.Utils.Interop.Windows.SYSTEM_INFO sYSTEM_INFO = default(MonoMod.Utils.Interop.Windows.SYSTEM_INFO);
		MonoMod.Utils.Interop.Windows.GetSystemInfo(&sYSTEM_INFO);
		ushort wProcessorArchitecture = sYSTEM_INFO.Anonymous.Anonymous.wProcessorArchitecture;
		arch = wProcessorArchitecture switch
		{
			9 => ArchitectureKind.x86_64, 
			6 => throw new PlatformNotSupportedException("You're running .NET on an Itanium device!?!?"), 
			0 => ArchitectureKind.x86, 
			5 => ArchitectureKind.Arm, 
			12 => ArchitectureKind.Arm64, 
			_ => throw new PlatformNotSupportedException($"Unknown Windows processor architecture {wProcessorArchitecture}"), 
		};
	}

	private unsafe static bool CheckWine()
	{
		if (Switches.TryGetSwitchEnabled("RunningOnWine", out var isEnabled))
		{
			return isEnabled;
		}
		string text = Environment.GetEnvironmentVariable("XL_WINEONLINUX")?.ToUpperInvariant();
		if (text == "TRUE")
		{
			return true;
		}
		if (text == "FALSE")
		{
			return false;
		}
		fixed (char* lpModuleName = "ntdll.dll".AsSpan())
		{
			MonoMod.Utils.Interop.Windows.HMODULE moduleHandleW = MonoMod.Utils.Interop.Windows.GetModuleHandleW((ushort*)lpModuleName);
			if (moduleHandleW != MonoMod.Utils.Interop.Windows.HMODULE.NULL && moduleHandleW != MonoMod.Utils.Interop.Windows.HMODULE.INVALID_VALUE)
			{
				fixed (byte* lpProcName = "wineGetVersion"u8)
				{
					if (MonoMod.Utils.Interop.Windows.GetProcAddress(moduleHandleW, (sbyte*)lpProcName) != IntPtr.Zero)
					{
						return true;
					}
				}
			}
		}
		return false;
	}

	[MemberNotNull("runtimeVersion")]
	private static void EnsureRuntimeInitialized()
	{
		if (runtimeInitState != 0)
		{
			if ((object)runtimeVersion == null)
			{
				throw new InvalidOperationException("Despite runtimeInitState being set, runtimeVersion was somehow null");
			}
		}
		else
		{
			(runtime, corelib, runtimeVersion) = DetermineRuntimeInfo();
			Thread.MemoryBarrier();
			Interlocked.Exchange(ref runtimeInitState, 1);
		}
	}

	private static (RuntimeKind Rt, CorelibKind Cor, Version Ver) DetermineRuntimeInfo()
	{
		Version version = null;
		bool flag = Type.GetType("Mono.Runtime") != null || Type.GetType("Mono.RuntimeStructs") != null;
		bool flag2 = typeof(object).Assembly.GetName().Name == "System.Private.CoreLib";
		CorelibKind corelibKind = (flag2 ? CorelibKind.Core : CorelibKind.Framework);
		RuntimeKind runtimeKind = (flag ? RuntimeKind.Mono : ((!flag2 || flag) ? RuntimeKind.Framework : RuntimeKind.CoreCLR));
		bool isEnabled;
		MMDbgLog.DebugLogTraceStringHandler message = new MMDbgLog.DebugLogTraceStringHandler(21, 2, out isEnabled);
		if (isEnabled)
		{
			message.AppendLiteral("IsMono: ");
			message.AppendFormatted(flag);
			message.AppendLiteral(", IsCoreBcl: ");
			message.AppendFormatted(flag2);
		}
		MMDbgLog.Trace(ref message);
		Version version2 = Environment.Version;
		MMDbgLog.DebugLogTraceStringHandler message2 = new MMDbgLog.DebugLogTraceStringHandler(25, 1, out isEnabled);
		if (isEnabled)
		{
			message2.AppendLiteral("Returned system version: ");
			message2.AppendFormatted(version2);
		}
		MMDbgLog.Trace(ref message2);
		Type type = Type.GetType("System.Runtime.InteropServices.RuntimeInformation");
		if ((object)type == null)
		{
			type = Type.GetType("System.Runtime.InteropServices.RuntimeInformation, System.Runtime.InteropServices.RuntimeInformation");
		}
		string text = (string)type?.GetProperty("FrameworkDescription")?.GetValue(null, null);
		MMDbgLog.DebugLogTraceStringHandler message3 = new MMDbgLog.DebugLogTraceStringHandler(22, 1, out isEnabled);
		if (isEnabled)
		{
			message3.AppendLiteral("FrameworkDescription: ");
			message3.AppendFormatted(text ?? "(null)");
		}
		MMDbgLog.Trace(ref message3);
		if (text != null)
		{
			int length;
			if (text.StartsWith("Mono ", StringComparison.Ordinal))
			{
				runtimeKind = RuntimeKind.Mono;
				length = "Mono ".Length;
			}
			else if (text.StartsWith(".NET Core ", StringComparison.Ordinal))
			{
				runtimeKind = RuntimeKind.CoreCLR;
				length = ".NET Core ".Length;
			}
			else if (text.StartsWith(".NET Framework ", StringComparison.Ordinal))
			{
				runtimeKind = RuntimeKind.Framework;
				length = ".NET Framework ".Length;
			}
			else if (text.StartsWith(".NET ", StringComparison.Ordinal))
			{
				runtimeKind = (flag ? RuntimeKind.Mono : RuntimeKind.CoreCLR);
				length = ".NET ".Length;
			}
			else
			{
				runtimeKind = RuntimeKind.Unknown;
				length = text.Length;
			}
			int num = text.IndexOfAny(new char[2] { ' ', '-' }, length);
			if (num < 0)
			{
				num = text.Length;
			}
			string version3 = text.Substring(length, num - length);
			try
			{
				version = new Version(version3);
			}
			catch (Exception value)
			{
				MMDbgLog.DebugLogErrorStringHandler message4 = new MMDbgLog.DebugLogErrorStringHandler(61, 2, out isEnabled);
				if (isEnabled)
				{
					message4.AppendLiteral("Invalid version string pulled from FrameworkDescription ('");
					message4.AppendFormatted(text);
					message4.AppendLiteral("') ");
					message4.AppendFormatted(value);
				}
				MMDbgLog.Error(ref message4);
			}
		}
		if (runtimeKind == RuntimeKind.Framework && (object)version == null)
		{
			version = version2;
		}
		MMDbgLog.DebugLogInfoStringHandler message5 = new MMDbgLog.DebugLogInfoStringHandler(34, 3, out isEnabled);
		if (isEnabled)
		{
			message5.AppendLiteral("Detected runtime: ");
			message5.AppendFormatted(runtimeKind);
			message5.AppendLiteral(" ");
			message5.AppendFormatted(version?.ToString() ?? "(null)");
			message5.AppendLiteral(" using ");
			message5.AppendFormatted(corelibKind);
			message5.AppendLiteral(" corelib");
		}
		MMDbgLog.Info(ref message5);
		return (Rt: runtimeKind, Cor: corelibKind, Ver: version ?? new Version(0, 0));
	}
}
