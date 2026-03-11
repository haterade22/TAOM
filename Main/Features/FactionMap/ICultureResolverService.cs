namespace TAOM.Features.FactionMap;

public interface ICultureResolverService
{
    object? ResolveCulture(string cultureId);
    bool IsCultureAvailable(string cultureId);
}
