namespace TAOM.Features.TimeAcceleration;

public interface IMapInputAdapter
{
    bool IsMapActive { get; }
    bool IsSpacePressed { get; }
    bool IsSpaceReleased { get; }
    bool IsEKeyPressed { get; }
    bool IsControlDown { get; }
}
