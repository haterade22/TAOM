namespace MonoMod.Core.Utils;

internal interface IInitialize
{
	void Initialize();
}
internal interface IInitialize<T>
{
	void Initialize(T value);
}
