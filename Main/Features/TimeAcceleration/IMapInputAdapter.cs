using TaleWorlds.InputSystem;

namespace TAOM.Features.TimeAcceleration;

public interface IMapInputAdapter
{
    bool IsMapActive { get; }
    bool IsKeyPressed(InputKey key);
    bool IsKeyReleased(InputKey key);
    bool IsControlDown();
}
