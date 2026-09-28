using TAOM.Features.ArmourAcquisition.Domain;

namespace TAOM.Features.ArmourAcquisition;

/// <summary>armour_acquisition_config.xml, validated and cached for the process.</summary>
public interface IArmourAcquisitionConfigProvider
{
    ArmourAcquisitionConfig GetConfig();
}
