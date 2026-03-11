namespace TAOM.Adapters;

public interface ICultureObjectAdapter
{
    object? ResolveCulture(string cultureId);
}
