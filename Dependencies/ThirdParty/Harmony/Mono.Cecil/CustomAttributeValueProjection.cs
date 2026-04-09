using System;

namespace Mono.Cecil;

internal sealed class CustomAttributeValueProjection
{
	public readonly AttributeTargets Targets;

	public readonly CustomAttributeValueTreatment Treatment;

	public CustomAttributeValueProjection(AttributeTargets targets, CustomAttributeValueTreatment treatment)
	{
		Targets = targets;
		Treatment = treatment;
	}
}
