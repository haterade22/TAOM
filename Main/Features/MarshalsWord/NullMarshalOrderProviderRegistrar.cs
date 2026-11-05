namespace TAOM.Features.MarshalsWord;

/// <summary>Core's default (ADR-013): registers nothing. TAOM.Client replaces it on a rendering client.</summary>
public sealed class NullMarshalOrderProviderRegistrar : IMarshalOrderProviderRegistrar
{
    public void Register()
    {
    }
}
