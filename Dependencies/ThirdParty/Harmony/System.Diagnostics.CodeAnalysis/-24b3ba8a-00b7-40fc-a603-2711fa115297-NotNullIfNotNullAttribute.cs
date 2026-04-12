namespace System.Diagnostics.CodeAnalysis;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter | AttributeTargets.ReturnValue, AllowMultiple = true, Inherited = false)]
[ExcludeFromCodeCoverage]
[DebuggerNonUserCode]
internal sealed class _003C24b3ba8a_002D00b7_002D40fc_002Da603_002D2711fa115297_003ENotNullIfNotNullAttribute : Attribute
{
	public string ParameterName { get; }

	public _003C24b3ba8a_002D00b7_002D40fc_002Da603_002D2711fa115297_003ENotNullIfNotNullAttribute(string parameterName)
	{
		ParameterName = parameterName;
	}
}
