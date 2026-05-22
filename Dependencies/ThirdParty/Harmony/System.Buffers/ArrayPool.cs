namespace System.Buffers;

internal abstract class ArrayPool<T>
{
	private static readonly TlsOverPerCoreLockedStacksArrayPool<T> s_shared = new TlsOverPerCoreLockedStacksArrayPool<T>();

	public static ArrayPool<T> Shared => s_shared;

	public static ArrayPool<T> Create()
	{
		return new ConfigurableArrayPool<T>();
	}

	public static ArrayPool<T> Create(int maxArrayLength, int maxArraysPerBucket)
	{
		return new ConfigurableArrayPool<T>(maxArrayLength, maxArraysPerBucket);
	}

	public abstract T[] Rent(int minimumLength);

	public abstract void Return(T[] array, bool clearArray = false);
}
