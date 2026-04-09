namespace System.Diagnostics.CodeAnalysis;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter | AttributeTargets.ReturnValue, AllowMultiple = true, Inherited = false)]
[ExcludeFromCodeCoverage]
[DebuggerNonUserCode]
internal sealed class _003C1c2fb156_002De9ba_002D45cc_002Daf54_002Dd5335bdb59af_003ENotNullIfNotNullAttribute : Attribute
{
	public string ParameterName { get; }

	public _003C1c2fb156_002De9ba_002D45cc_002Daf54_002Dd5335bdb59af_003ENotNullIfNotNullAttribute(string parameterName)
	{
		ParameterName = parameterName;
	}
}
