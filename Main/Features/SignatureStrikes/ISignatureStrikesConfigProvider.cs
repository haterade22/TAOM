using TAOM.Features.SignatureStrikes.Domain;

namespace TAOM.Features.SignatureStrikes;

public interface ISignatureStrikesConfigProvider
{
    SignatureStrikesConfig GetConfig();
}
