namespace System.Diagnostics.CodeAnalysis;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Property, Inherited = false, AllowMultiple = true)]
[ExcludeFromCodeCoverage]
[DebuggerNonUserCode]
internal sealed class _003C24b3ba8a_002D00b7_002D40fc_002Da603_002D2711fa115297_003EMemberNotNullAttribute : Attribute
{
	public string[] Members { get; }

	public _003C24b3ba8a_002D00b7_002D40fc_002Da603_002D2711fa115297_003EMemberNotNullAttribute(string member)
	{
		Members = new string[1] { member };
	}

	public _003C24b3ba8a_002D00b7_002D40fc_002Da603_002D2711fa115297_003EMemberNotNullAttribute(params string[] members)
	{
		Members = members;
	}
}
