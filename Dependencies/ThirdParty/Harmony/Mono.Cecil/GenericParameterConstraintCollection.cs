using Mono.Collections.Generic;

namespace Mono.Cecil;

internal class GenericParameterConstraintCollection : Collection<GenericParameterConstraint>
{
	private readonly GenericParameter generic_parameter;

	internal GenericParameterConstraintCollection(GenericParameter genericParameter)
	{
		generic_parameter = genericParameter;
	}

	internal GenericParameterConstraintCollection(GenericParameter genericParameter, int length)
		: base(length)
	{
		generic_parameter = genericParameter;
	}

	protected override void OnAdd(GenericParameterConstraint item, int index)
	{
		item.generic_parameter = generic_parameter;
	}

	protected override void OnInsert(GenericParameterConstraint item, int index)
	{
		item.generic_parameter = generic_parameter;
	}

	protected override void OnSet(GenericParameterConstraint item, int index)
	{
		item.generic_parameter = generic_parameter;
	}

	protected override void OnRemove(GenericParameterConstraint item, int index)
	{
		item.generic_parameter = null;
	}
}
