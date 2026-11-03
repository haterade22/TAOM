using TAOM.Features.LongDefeat.Spike;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.LongDefeat;

/// <summary>
/// Builds the game manager that loads The Long Defeat. The manager derives from a CustomBattle type, so the real factory lives in
/// TAOM.Client (ADR-013); core registers <see cref="NullLongDefeatGameManagerFactory"/> and the client composition replaces it.
/// </summary>
internal interface ILongDefeatGameManagerFactory
{
    MBGameManager? Create(LongDefeatSpikeOptions options);
}
