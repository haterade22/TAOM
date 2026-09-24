namespace TAOM.Composition;

/// <summary>
/// The ordered feature-module list, one line per migrated feature. The runner visits modules in
/// this order in every step, so list order is registration order, behavior add order and
/// mission-behavior add order. Features not migrated yet are still wired by hand in IoC.cs and
/// SubModule.cs, and every module here runs AFTER all of them in each phase. Append; when a module
/// must precede another, say why on its line and pin it with an index test in FeatureModulesTests.
/// </summary>
internal static class FeatureModules
{
    internal static readonly ITaomFeatureModule[] All =
    {
    };
}
