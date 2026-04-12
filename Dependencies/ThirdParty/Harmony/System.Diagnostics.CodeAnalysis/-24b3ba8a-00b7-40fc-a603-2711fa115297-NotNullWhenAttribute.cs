namespace System.Diagnostics.CodeAnalysis;

[AttributeUsage(AttributeTargets.Parameter, Inherited = false)]
[ExcludeFromCodeCoverage]
[DebuggerNonUserCode]
internal sealed class _003C24b3ba8a_002D00b7_002D40fc_002Da603_002D2711fa115297_003ENotNullWhenAttribute : Attribute
{
	public bool ReturnValue { get; }

	public _003C24b3ba8a_002D00b7_002D40fc_002Da603_002D2711fa115297_003ENotNullWhenAttribute(bool returnValue)
	{
		ReturnValue = returnValue;
	}
}
