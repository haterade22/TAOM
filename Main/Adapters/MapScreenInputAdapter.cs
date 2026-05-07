using TaleWorlds.InputSystem;

namespace TAOM.Adapters;

public class MapScreenInputAdapter : IMapScreenInputAdapter
{
    public bool IsF6Pressed => Input.IsKeyPressed(InputKey.F6);
}
