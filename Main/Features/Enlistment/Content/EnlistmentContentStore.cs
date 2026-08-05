using TAOM.Core.Logging;
using TAOM.Features.Enlistment.Content.Domain;

namespace TAOM.Features.Enlistment.Content;

public class EnlistmentContentStore : IEnlistmentContentStore
{
    private readonly ServiceContentRecord _record = new ServiceContentRecord();
    private readonly IModLogger _logger;

    public EnlistmentContentStore(IModLogger logger)
    {
        _logger = logger;
    }

    public ServiceContentRecord Record => _record;

    public string Serialize() => _record.Serialize();

    public void Deserialize(string section)
    {
        if (string.IsNullOrEmpty(section))
        {
            _record.Reset();
            return;
        }

        if (ServiceContentRecord.TryParse(section, out var parsed))
        {
            _record.CopyFrom(parsed);
            return;
        }

        _logger?.LogWarning("EnlistmentContentStore: malformed content section — resetting progression (core service state is unaffected)");
        _record.Reset();
    }

    public void Clear() => _record.Reset();
}
