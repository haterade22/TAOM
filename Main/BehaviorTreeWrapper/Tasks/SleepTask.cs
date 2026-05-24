using System;
using BehaviorTrees;
using BehaviorTrees.Nodes;

namespace BehaviorTreeWrapper.Tasks;

public class SleepTask : BTTask
{
    private readonly TimeSpan _sleepDuration;
    private DateTime _lastTime;
    private bool _isExecuting;

    public SleepTask(TimeSpan timeSpan)
    {
        _sleepDuration = timeSpan;
    }

    public override BTTaskStatus Execute()
    {
        if (_isExecuting)
        {
            if (DateTime.Now - _lastTime > _sleepDuration)
            {
                _isExecuting = false;
                return BTTaskStatus.FinishedWithTrue;
            }
            return BTTaskStatus.Running;
        }
        _isExecuting = true;
        _lastTime = DateTime.Now;
        return BTTaskStatus.Running;
    }
}
