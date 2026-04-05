using SandBox.View.Map;
using TaleWorlds.InputSystem;

namespace TAOM.Features.TimeAcceleration;

public class MapInputAdapter : IMapInputAdapter
{
    public bool IsMapActive => MapScreen.Instance != null;

    public bool IsKeyPressed(InputKey key) =>
        MapScreen.Instance?.Input?.IsKeyPressed(key) ?? false;

    public bool IsKeyReleased(InputKey key) =>
        MapScreen.Instance?.Input?.IsKeyReleased(key) ?? false;

    public bool IsControlDown() =>
        MapScreen.Instance?.Input?.IsControlDown() ?? false;
}
