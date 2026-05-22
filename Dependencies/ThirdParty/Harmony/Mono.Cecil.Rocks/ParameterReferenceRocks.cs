namespace Mono.Cecil.Rocks;

internal static class ParameterReferenceRocks
{
	public static int GetSequence(this ParameterReference self)
	{
		return self.Index + 1;
	}
}
