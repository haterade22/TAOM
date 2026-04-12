namespace Mono.Cecil;

internal interface IModifierType
{
	TypeReference ModifierType { get; }

	TypeReference ElementType { get; }
}
