using TAOM.Core.Logging;

namespace TAOM.Features.SeatOfSeeing;

/// <summary>
/// The logger the Seat's patches use at patch time, before any mission exists: the module hands it over once the container is built
/// (<c>SeatOfSeeingModule.InitializeStatics</c>), because a patch in <c>TAOM.Client</c> may not read the container itself.
/// </summary>
internal static class SeatPatchLog
{
    internal static IModLogger? Logger { get; set; }
}
