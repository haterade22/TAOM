using SandBox.View.Map;
using TaleWorlds.InputSystem;

namespace TAOM.Features.TimeAcceleration;

public class MapInputAdapter : IMapInputAdapter
{
    public bool IsMapActive => MapScreen.Instance != null;

    public bool IsSpacePressed =>
        MapScreen.Instance?.Input?.IsKeyPressed(InputKey.Space) ?? false;

    public bool IsSpaceReleased =>
        MapScreen.Instance?.Input?.IsKeyReleased(InputKey.Space) ?? false;

    public bool IsEKeyPressed =>
        MapScreen.Instance?.Input?.IsKeyPressed(InputKey.E) ?? false;

    public bool IsControlDown =>
        MapScreen.Instance?.Input?.IsControlDown() ?? false;
}
