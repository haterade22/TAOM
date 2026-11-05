namespace TAOM.Adapters;

/// <summary>
/// One hero's <c>CharacterObject</c>, seen through its raw fields. <c>CharacterObject.Culture</c> and <c>Name</c>
/// answer from the hero, so a hollow character (one XML never reached) stays hidden until <c>FillFrom</c> copies its
/// nulls onto a child's new character, which is not a hero yet (crash bundle 3fbdc0c5).
/// </summary>
public interface IHeroCharacterStateAdapter
{
    string HeroId { get; }

    bool IsCharacterHollow { get; }

    /// <summary>"name=value ..." facts for the log, with no whitespace inside a value.</summary>
    string DescribeState();

    /// <summary>
    /// Makes the character safe to clone. With a healthy registered character of the same id, it takes that one as
    /// its origin and copies every field from it. With none, it takes the culture from the character, the saved
    /// <c>Hero.Culture</c> or the hero's clan, and fills the other null fields inert. Never invents a culture: with no
    /// source, or with a lost engine binding, the result is false.
    /// </summary>
    bool TryRepairCharacter();
}
