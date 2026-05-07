using TaleWorlds.Core;

namespace TAOM.Features.Messengers;

public class MessengerRandomSource : IMessengerRandomSource
{
    public float NextFloat() => MBRandom.RandomFloat;
}
