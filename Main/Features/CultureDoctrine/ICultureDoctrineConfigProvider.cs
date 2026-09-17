using TAOM.Features.CultureDoctrine.Domain;

namespace TAOM.Features.CultureDoctrine;

/// <summary>The validated doctrine catalog. Loaded once per process (<c>Reuse.Singleton</c>):
/// a JSON edit needs a full game restart, not a new battle.</summary>
public interface ICultureDoctrineConfigProvider
{
    DoctrineCatalog GetCatalog();
}
