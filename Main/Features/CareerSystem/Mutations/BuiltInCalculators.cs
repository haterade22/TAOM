namespace TAOM.Features.CareerSystem.Mutations;

public static class BuiltInCalculators
{
    public static void RegisterAll(IMutationCalculatorRegistry registry)
    {
        registry.Register("flat", (baseValue, hero, p) =>
            baseValue + p.GetFloat("value"));

        registry.Register("skill_scaling", (baseValue, hero, p) =>
            baseValue + hero.GetSkillValue(p.GetString("skill")) * p.GetFloat("factor", 0.01f));

        registry.Register("level_scaling", (baseValue, hero, p) =>
            baseValue + hero.Level * p.GetFloat("factor", 0.01f));

        registry.Register("replace", (baseValue, hero, p) =>
            p.GetFloat("value", baseValue));

        registry.Register("multiply", (baseValue, hero, p) =>
            baseValue * p.GetFloat("factor", 1f));
    }
}
