using System.Runtime.InteropServices;

namespace MonoMod.Utils.Interop;

internal static class OSX
{
	public const string LibSystem = "libSystem";

	[DllImport("libSystem", CallingConvention = CallingConvention.Cdecl, EntryPoint = "uname", SetLastError = true)]
	public unsafe static extern int Uname(byte* buf);
}
