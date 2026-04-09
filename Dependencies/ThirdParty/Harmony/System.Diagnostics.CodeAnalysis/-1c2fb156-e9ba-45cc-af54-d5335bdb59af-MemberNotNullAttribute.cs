namespace System.Diagnostics.CodeAnalysis;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Property, Inherited = false, AllowMultiple = true)]
[ExcludeFromCodeCoverage]
[DebuggerNonUserCode]
internal sealed class _003C1c2fb156_002De9ba_002D45cc_002Daf54_002Dd5335bdb59af_003EMemberNotNullAttribute : Attribute
{
	public string[] Members { get; }

	public _003C1c2fb156_002De9ba_002D45cc_002Daf54_002Dd5335bdb59af_003EMemberNotNullAttribute(string member)
	{
		Members = new string[1] { member };
	}

	public _003C1c2fb156_002De9ba_002D45cc_002Daf54_002Dd5335bdb59af_003EMemberNotNullAttribute(params string[] members)
	{
		Members = members;
	}
}
