namespace TAOM.Features.InitialChildGeneration;

public interface IRandomSource
{
    double NextDouble();
    int Next(int minInclusive, int maxExclusive);
}
