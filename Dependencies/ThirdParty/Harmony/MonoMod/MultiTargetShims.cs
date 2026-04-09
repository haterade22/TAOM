using Mono.Cecil;

namespace MonoMod;

internal static class MultiTargetShims
{
	public static TypeReference GetConstraintType(this GenericParameterConstraint constraint)
	{
		return constraint.ConstraintType;
	}
}
