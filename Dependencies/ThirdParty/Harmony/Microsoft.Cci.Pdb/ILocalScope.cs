namespace Microsoft.Cci.Pdb;

internal interface ILocalScope
{
	uint Offset { get; }

	uint Length { get; }
}
