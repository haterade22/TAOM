using TAOM.Features.Execution;

namespace TAOM.Features.WandererAllegiance;

/// <summary>One town a wanderer could be placed in: its owner's side ids, whether placing is allowed there at all, and whether an unhired wanderer already stands in it.</summary>
public sealed record WandererTownOption(SideIds Side, bool Usable, bool HasWanderer);
