using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.CharacterCreationContent;

namespace TAOM.Adapters;

/// <summary>
/// <see cref="ICharacterCreationStagesAdapter"/> over one <see cref="CharacterCreationManager"/>, through
/// its public stage calls only (v1.5.3): <c>GetStage&lt;T&gt;</c> and <c>RemoveStage&lt;T&gt;</c> act on
/// the first stage that is a <c>T</c>, and <c>AddStage</c> only appends. One adapter per character
/// creation, so the stages it keeps die with their manager.
/// </summary>
public sealed class CharacterCreationStagesAdapter : ICharacterCreationStagesAdapter
{
    /// <summary>Each kind's engine class and calls, in one table: FactionUIBindingTests checks that
    /// vanilla builds these classes in <c>FactionPickService.EngineOrder</c>.</summary>
    private static readonly Dictionary<CharacterCreationStageKind, StageCalls> Calls = new()
    {
        [CharacterCreationStageKind.Culture] = StageCalls.Of<CharacterCreationCultureStage>(),
        [CharacterCreationStageKind.FaceGenerator] = StageCalls.Of<CharacterCreationFaceGeneratorStage>(),
        [CharacterCreationStageKind.Narrative] = StageCalls.Of<CharacterCreationNarrativeStage>(),
        [CharacterCreationStageKind.BannerEditor] = StageCalls.Of<CharacterCreationBannerEditorStage>(),
        [CharacterCreationStageKind.ClanNaming] = StageCalls.Of<CharacterCreationClanNamingStage>(),
        [CharacterCreationStageKind.Review] = StageCalls.Of<CharacterCreationReviewStage>(),
        [CharacterCreationStageKind.Options] = StageCalls.Of<CharacterCreationOptionsStage>(),
    };

    private readonly CharacterCreationManager _manager;
    private readonly Dictionary<CharacterCreationStageKind, CharacterCreationStageBase> _removed = new();

    public CharacterCreationStagesAdapter(CharacterCreationManager manager)
    {
        _manager = manager;
    }

    internal static Type TypeOf(CharacterCreationStageKind kind) => Calls[kind].Type;

    public int Count => _manager.GetTotalStagesCount();

    public int CurrentIndex => _manager.GetIndexOfCurrentStage();

    public bool HoldsRemoved => _removed.Count > 0;

    public bool Has(CharacterCreationStageKind kind) => Calls[kind].Find(_manager) != null;

    public bool Remove(CharacterCreationStageKind kind)
    {
        var calls = Calls[kind];
        var stage = calls.Find(_manager);
        if (stage == null || !calls.RemoveFirst(_manager))
            return false;
        _removed[kind] = stage;
        return true;
    }

    public bool Append(CharacterCreationStageKind kind)
    {
        if (!_removed.TryGetValue(kind, out var stage))
            return false;
        _manager.AddStage(stage);
        _removed.Remove(kind);
        return true;
    }

    private sealed class StageCalls
    {
        private StageCalls(Type type, Func<CharacterCreationManager, CharacterCreationStageBase?> find, Func<CharacterCreationManager, bool> removeFirst)
        {
            Type = type;
            Find = find;
            RemoveFirst = removeFirst;
        }

        public Type Type { get; }

        public Func<CharacterCreationManager, CharacterCreationStageBase?> Find { get; }

        public Func<CharacterCreationManager, bool> RemoveFirst { get; }

        public static StageCalls Of<T>() where T : CharacterCreationStageBase =>
            new(typeof(T), manager => manager.GetStage<T>(), manager => manager.RemoveStage<T>());
    }
}
