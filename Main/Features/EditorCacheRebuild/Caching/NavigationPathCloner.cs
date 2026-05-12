using System;
using TaleWorlds.Library;

namespace TAOM.Features.EditorCacheRebuild.Caching;

public static class NavigationPathCloner
{
    public static NavigationPath Clone(NavigationPath source)
    {
        var clone = new NavigationPath();
        var size = source.Size;
        Array.Copy(source.PathPoints, clone.PathPoints, size);
        clone.Size = size;
        return clone;
    }
}
