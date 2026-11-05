using TAOM.Features.MarshalsWord;
using TaleWorlds.MountAndBlade.ViewModelCollection.Order.Visual;

namespace TAOM.Client.Features.MarshalsWord;

/// <summary>Registers Marshal's Word's order provider with the engine (ADR-013); core's null registrar registers nothing.</summary>
internal sealed class MarshalOrderProviderRegistrar : IMarshalOrderProviderRegistrar
{
    // Once per process: the engine's factory rejects a second provider of the same type. MarshalsWordModule calls this only at ApplyPhase.ProcessLoad.
    public void Register() => VisualOrderFactory.RegisterProvider(new MarshalVisualOrderProvider());
}
