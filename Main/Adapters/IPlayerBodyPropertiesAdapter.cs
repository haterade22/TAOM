namespace TAOM.Adapters;

public interface IPlayerBodyPropertiesAdapter
{
    bool TryApplyFromXml(string bodyPropertiesXml);
}
