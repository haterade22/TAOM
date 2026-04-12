using System;
using System.ComponentModel;
using System.Reflection;
using MonoMod.Core.Platforms;
using MonoMod.Utils;

namespace MonoMod.Core;

[CLSCompliant(true)]
internal static class DetourFactory
{
	private static object currentLock = new object();

	private static IDetourFactory? lazyDefault;

	private static IDetourFactory? lazyCurrent;

	public unsafe static IDetourFactory Default => Helpers.GetOrInitWithLock(ref lazyDefault, currentLock, (delegate*<IDetourFactory>)(&CreateDefault));

	public unsafe static IDetourFactory Current => Helpers.GetOrInitWithLock(ref lazyCurrent, currentLock, (delegate*<IDetourFactory>)(&CreateCurrent));

	private static IDetourFactory CreateDefault()
	{
		return new PlatformTripleDetourFactory(PlatformTriple.Current);
	}

	private static IDetourFactory CreateCurrent()
	{
		return Default;
	}

	[EditorBrowsable(EditorBrowsableState.Never)]
	public static void SetCurrentFactory(Func<IDetourFactory, IDetourFactory> creator)
	{
		Helpers.ThrowIfArgumentNull(creator, "creator");
		lock (currentLock)
		{
			lazyCurrent = creator(Current);
		}
	}

	public static ICoreDetour CreateDetour(this IDetourFactory factory, MethodBase source, MethodBase target, bool applyByDefault = true)
	{
		Helpers.ThrowIfArgumentNull(factory, "factory");
		return factory.CreateDetour(new CreateDetourRequest(source, target)
		{
			ApplyByDefault = applyByDefault
		});
	}

	public static ICoreNativeDetour CreateNativeDetour(this IDetourFactory factory, IntPtr source, IntPtr target, bool applyByDefault = true)
	{
		Helpers.ThrowIfArgumentNull(factory, "factory");
		return factory.CreateNativeDetour(new CreateNativeDetourRequest(source, target)
		{
			ApplyByDefault = applyByDefault
		});
	}
}
