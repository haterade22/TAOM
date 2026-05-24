using System;
using BehaviorTreeWrapper.AbstractDecoratorsListeners;

namespace BehaviorTreeWrapper.Decorators;

public class WaitNSecondsTickDecorator : BannerlordTickTimedDecorator
{
    private readonly TimeSpan _waitSeconds;
    private DateTime? _lastTime;

    public WaitNSecondsTickDecorator(double timeToWait)
        : base(timeToWait)
    {
        _waitSeconds = TimeSpan.FromSeconds(timeToWait);
    }

    public override bool Evaluate()
    {
        if (!_lastTime.HasValue)
        {
            _lastTime = DateTime.Now;
            return false;
        }
        if (DateTime.Now - _lastTime > _waitSeconds)
        {
            _lastTime = null;
            return true;
        }
        return false;
    }

    public override void Notify(object[] data) { }
}
