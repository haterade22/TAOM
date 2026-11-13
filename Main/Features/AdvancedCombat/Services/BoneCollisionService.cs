using System;
using System.Collections.Generic;

namespace TAOM.Features.AdvancedCombat.Services;

public class BoneCollisionService : IBoneCollisionService
{
    private readonly List<BoneCheck> _boneCheckComponents = new();

    public void AddBoneCheckComponent(BoneCheck component)
    {
        if (component == null)
            throw new ArgumentNullException(nameof(component));
        _boneCheckComponents.Add(component);
    }

    public void TickBoneChecks(float dt)
    {
        for (int i = _boneCheckComponents.Count - 1; i >= 0; i--)
        {
            bool isAlive = _boneCheckComponents[i].Tick(dt);
            if (!isAlive)
                _boneCheckComponents.RemoveAt(i);
        }
    }

    public void Clear()
    {
        _boneCheckComponents.Clear();
    }
}
