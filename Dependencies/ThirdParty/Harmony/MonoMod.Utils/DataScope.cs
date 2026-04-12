using System;

namespace MonoMod.Utils;

internal readonly struct DataScope : IDisposable
{
	private readonly ScopeHandlerBase? handler;

	private readonly object? data;

	public object? Data => data;

	public DataScope(ScopeHandlerBase handler, object? data)
	{
		this.handler = handler;
		this.data = data;
	}

	public void Dispose()
	{
		if (handler != null)
		{
			handler.EndScope(data);
		}
	}
}
internal readonly struct DataScope<T> : IDisposable
{
	private readonly ScopeHandlerBase<T>? handler;

	private readonly T data;

	public T Data => data;

	public DataScope(ScopeHandlerBase<T> handler, T data)
	{
		this.handler = handler;
		this.data = data;
	}

	public void Dispose()
	{
		if (handler != null)
		{
			handler.EndScope(data);
		}
	}
}
