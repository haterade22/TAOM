using System.Collections.Generic;

namespace TAOM.Features.Execution;

public interface IAlignmentConfigProvider
{
    Dictionary<string, string> LoadAlignments();
}
