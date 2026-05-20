using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Features.CareerSystem;

public interface ICareerArchetypeService
{
    bool TryGetArchetype(string careerId, out CareerArchetype archetype);
}
