namespace TAOM.Features.AdvancedCombat.Services;

public interface IBoneCollisionService
{

    void AddBoneCheckComponent(BoneCheck component);
    void TickBoneChecks(float dt);
    void Clear();
}
