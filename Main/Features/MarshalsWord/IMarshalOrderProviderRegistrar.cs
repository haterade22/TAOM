namespace TAOM.Features.MarshalsWord;

/// <summary>Registers Marshal's Word's order provider with the engine once per process; the provider derives from a client-only type, so TAOM.Client holds it (ADR-013).</summary>
public interface IMarshalOrderProviderRegistrar
{
    void Register();
}
