namespace System.Diagnostics.CodeAnalysis;

[AttributeUsage(AttributeTargets.Parameter, Inherited = false)]
[ExcludeFromCodeCoverage]
[DebuggerNonUserCode]
internal sealed class _003C1c2fb156_002De9ba_002D45cc_002Daf54_002Dd5335bdb59af_003ENotNullWhenAttribute : Attribute
{
	public bool ReturnValue { get; }

	public _003C1c2fb156_002De9ba_002D45cc_002Daf54_002Dd5335bdb59af_003ENotNullWhenAttribute(bool returnValue)
	{
		ReturnValue = returnValue;
	}
}
