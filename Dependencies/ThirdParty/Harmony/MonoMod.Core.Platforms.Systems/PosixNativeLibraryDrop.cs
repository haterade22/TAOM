using System;
using System.Buffers;
using System.IO;
using System.Text;
using MonoMod.Utils;

namespace MonoMod.Core.Platforms.Systems;

internal abstract class PosixNativeLibraryDrop
{
	protected abstract nint Mkstemp(Span<byte> template);

	protected abstract void CloseFileDescriptor(nint fd);

	public unsafe string DropLibrary(Stream sourceStream, ReadOnlySpan<byte> defaultTemplate)
	{
		byte[] array;
		int count;
		if (Switches.TryGetSwitchValue("HelperDropPath", out object value) && value is string path)
		{
			int num = defaultTemplate.LastIndexOf<byte>(47);
			Helpers.Assert(num >= 0, null, "endOfDefaultTemplateDir >= 0");
			ReadOnlySpan<byte> readOnlySpan = defaultTemplate.Slice(num);
			string fullPath = Path.GetFullPath(path);
			Directory.CreateDirectory(fullPath);
			int byteCount = Encoding.UTF8.GetByteCount(fullPath);
			array = ArrayPool<byte>.Shared.Rent(byteCount + readOnlySpan.Length + 1);
			array.AsSpan().Clear();
			int num2;
			fixed (char* chars = fullPath.AsSpan())
			{
				fixed (byte* bytes = array)
				{
					num2 = Encoding.UTF8.GetBytes(chars, fullPath.Length, bytes, array.Length);
				}
			}
			if (array[num2 - 1] == 47)
			{
				num2--;
			}
			readOnlySpan.CopyTo(array.AsSpan(num2));
			array[num2 + readOnlySpan.Length] = 0;
			count = num2 + readOnlySpan.Length;
		}
		else
		{
			array = ArrayPool<byte>.Shared.Rent(defaultTemplate.Length + 1);
			array.AsSpan().Clear();
			defaultTemplate.CopyTo(array);
			count = defaultTemplate.Length;
		}
		nint num3 = Mkstemp(array);
		string text = Encoding.UTF8.GetString(array, 0, count);
		ArrayPool<byte>.Shared.Return(array);
		if (PlatformDetection.Runtime == RuntimeKind.Mono && PlatformDetection.Corelib != CorelibKind.Core)
		{
			CloseFileDescriptor(num3);
			using FileStream destination = new FileStream(text, FileMode.Create, FileAccess.Write);
			sourceStream.CopyTo(destination);
		}
		else
		{
			try
			{
				using FileStream destination2 = new FileStream(num3, FileAccess.Write);
				sourceStream.CopyTo(destination2);
			}
			finally
			{
				CloseFileDescriptor(num3);
			}
		}
		return text;
	}
}
