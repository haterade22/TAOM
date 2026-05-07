namespace TAOM.Features.SiegeDismount.Models;

public interface IMountSnapshot
{
    bool HasMount { get; }
    bool HasHarness { get; }
    string? MountItemId { get; }
    string? HarnessItemId { get; }
}
