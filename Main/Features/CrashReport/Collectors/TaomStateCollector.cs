using System;
using System.Collections.Generic;
using TAOM.Features.CrashReport.Domain;

namespace TAOM.Features.CrashReport.Collectors;

// Placeholder collector for TAOM-specific in-game state (career, resources, feats,
// revolt tuning, messengers). Each subsystem provides its own lightweight snapshot
// adapter via constructor injection. If a subsystem isn't wired yet, that field
// remains null in the report — partial data is better than no data.
//
// Future: replace the lambda providers with per-feature adapters (e.g.,
// ICareerSnapshotProvider) registered in IoC so the dependency direction is
// CrashReport → Feature, not the other way around.
public sealed class TaomStateCollector
{
    private readonly Func<CareerStateSnapshot?>? _career;
    private readonly Func<IReadOnlyList<SpecialResourceEntry>>? _resources;
    private readonly Func<CulturalFeatsStateSnapshot?>? _feats;
    private readonly Func<RevoltTuningStateSnapshot?>? _revolt;
    private readonly Func<int?>? _messengers;

    public TaomStateCollector(
        Func<CareerStateSnapshot?>? career = null,
        Func<IReadOnlyList<SpecialResourceEntry>>? resources = null,
        Func<CulturalFeatsStateSnapshot?>? feats = null,
        Func<RevoltTuningStateSnapshot?>? revolt = null,
        Func<int?>? messengers = null)
    {
        _career = career;
        _resources = resources;
        _feats = feats;
        _revolt = revolt;
        _messengers = messengers;
    }

    public TaomStateSnapshot Collect()
    {
        return new TaomStateSnapshot(
            Career: Safe(_career),
            SpecialResources: SafeList(_resources),
            CulturalFeats: Safe(_feats),
            RevoltTuning: Safe(_revolt),
            ActiveMessengersCount: SafeInt(_messengers));
    }

    private static T? Safe<T>(Func<T?>? f) where T : class { if (f == null) return null; try { return f(); } catch { return null; } }
    private static IReadOnlyList<T> SafeList<T>(Func<IReadOnlyList<T>>? f) { if (f == null) return Array.Empty<T>(); try { return f() ?? Array.Empty<T>(); } catch { return Array.Empty<T>(); } }
    private static int? SafeInt(Func<int?>? f) { if (f == null) return null; try { return f(); } catch { return null; } }
}
