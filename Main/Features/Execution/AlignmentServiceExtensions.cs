namespace TAOM.Features.Execution;

public static class AlignmentServiceExtensions
{
    /// <summary>
    /// Resolves a side from a pair built by <c>FactionSides</c>. An extension rather than an interface
    /// member so the many test fakes of <see cref="IAlignmentService.ResolveSide(string, string)"/> keep working.
    /// </summary>
    public static FactionSide ResolveSide(this IAlignmentService alignment, SideIds side)
        => alignment.ResolveSide(side.FactionId, side.CultureId);
}
