using System.Collections.Generic;

namespace TAOM.Features.CareerSystem.Domain;

public sealed class MutationDefinition
{
    public string TargetTemplateId { get; }
    public string PropertyName { get; }
    public string CalculatorId { get; }
    public OperationType Operation { get; }
    public IReadOnlyDictionary<string, string> Params { get; }

    public MutationDefinition(
        string targetTemplateId,
        string propertyName,
        string calculatorId,
        OperationType operation,
        IReadOnlyDictionary<string, string> parameters)
    {
        TargetTemplateId = targetTemplateId;
        PropertyName = propertyName;
        CalculatorId = calculatorId;
        Operation = operation;
        Params = parameters ?? new Dictionary<string, string>();
    }
}
