using System;

namespace MonoMod.Utils;

internal sealed class LazyDisposable : IDisposable
{
	public event Action? OnDispose;

	public LazyDisposable()
	{
	}

	public LazyDisposable(Action a)
		: this()
	{
		OnDispose += a;
	}

	public void Dispose()
	{
		this.OnDispose?.Invoke();
	}
}
internal sealed class LazyDisposable<T> : IDisposable
{
	private T? Instance;

	public event Action<T>? OnDispose;

	public LazyDisposable(T instance)
	{
		Instance = instance;
	}

	public LazyDisposable(T instance, Action<T> a)
		: this(instance)
	{
		OnDispose += a;
	}

	public void Dispose()
	{
		this.OnDispose?.Invoke(Instance);
		Instance = default(T);
	}
}
