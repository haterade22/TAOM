using TAOM.Features.FieldCommission.Domain;

namespace TAOM.Features.FieldCommission;

public interface IFieldCommissionConfigProvider
{
    FieldCommissionConfig GetConfig();
}
