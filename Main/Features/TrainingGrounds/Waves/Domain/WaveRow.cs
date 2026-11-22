using System.Collections.Generic;

namespace TAOM.Features.TrainingGrounds.Waves.Domain;

/// <summary>
/// One wave of the bet table (docs/features/training-waves.md): how many fighters in all, how many may stand at once, the weight of
/// each troop tier (3, 4 and 5) in the draw, the practice weapon stage (1 to 3), the most that may be staked on the wave, and the
/// odds in tenths (15 pays the stake back 1.5 times). Whole numbers only, so no NaN path exists.
/// </summary>
internal sealed record WaveRow(int Count, int AtOnce, IReadOnlyDictionary<int, int> TierWeights, int WeaponStage, int Cap, int OddsTenths);
