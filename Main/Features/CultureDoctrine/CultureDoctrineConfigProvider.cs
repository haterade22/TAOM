using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Core.Validation;
using TAOM.Features.CultureDoctrine.Domain;

namespace TAOM.Features.CultureDoctrine;

/// <summary>
/// Validating boundary loader for <c>culture_doctrine/culture_doctrines.json</c>
/// (SignatureStrikesConfigProvider pattern): a missing file gives the vanilla-equivalent catalog
/// with a warning, a parse failure the same with an error, and a parseable-but-invalid row
/// reverts its bad field or is dropped with a warning (csharp-architecture.md "Config Providers
/// MUST Validate"). A tactic id the roster could never match is dropped rather than kept, because
/// a row the author believes is live must not be silent.
/// </summary>
public sealed class CultureDoctrineConfigProvider : ICultureDoctrineConfigProvider
{
    private static readonly JsonSerializerSettings SerializerSettings = new JsonSerializerSettings
    {
        ObjectCreationHandling = ObjectCreationHandling.Replace,
    };

    // Five times vanilla's weight already pins a tactic; the 1.5x switch hysteresis in
    // TeamAIComponent.MakeDecision makes anything larger indistinguishable from "always".
    private const float MinMultiplier = 0f;
    private const float MaxMultiplier = 5f;
    private const int MinTacticsFloor = 0;
    private const int MaxTacticsFloor = 300;

    private readonly IPathService _pathService;
    private readonly IModLogger _logger;
    private readonly Lazy<DoctrineCatalog> _catalog;

    public CultureDoctrineConfigProvider(IPathService pathService, IModLogger logger)
    {
        _pathService = pathService;
        _logger = logger;
        _catalog = new Lazy<DoctrineCatalog>(Load);
    }

    public DoctrineCatalog GetCatalog() => _catalog.Value;

    private DoctrineCatalog Load()
    {
        var path = Path.Combine(_pathService.ModuleDataPath, "culture_doctrine", "culture_doctrines.json");
        if (!File.Exists(path))
        {
            _logger.LogWarning($"CultureDoctrineConfigProvider: culture_doctrines.json not found at {path}, using the vanilla-equivalent doctrine for every culture");
            return DoctrineCatalog.VanillaEquivalent();
        }

        CultureDoctrineConfig parsed;
        try
        {
            parsed = JsonConvert.DeserializeObject<CultureDoctrineConfig>(File.ReadAllText(path), SerializerSettings)
                ?? new CultureDoctrineConfig();
        }
        catch (Exception ex)
        {
            _logger.LogError($"CultureDoctrineConfigProvider: Failed to parse culture_doctrines.json: {ex.Message}");
            return DoctrineCatalog.VanillaEquivalent();
        }

        return Validate(parsed);
    }

    private DoctrineCatalog Validate(CultureDoctrineConfig parsed)
    {
        var rejected = false;
        if (parsed.Doctrines == null)
        {
            _logger.LogWarning("CultureDoctrineConfigProvider: doctrines is null, using the vanilla-equivalent doctrine for every culture");
            _logger.LogWarning("CultureDoctrineConfigProvider: culture_doctrines.json contained invalid values. See prior warnings for details.");
            return new DoctrineCatalog(parsed.Enabled, DoctrineCatalog.VanillaDefault(), Array.Empty<Doctrine>());
        }

        Doctrine? fallback = null;
        var cultures = new List<Doctrine>();
        foreach (var pair in parsed.Doctrines)
        {
            var key = pair.Key?.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(key))
            {
                _logger.LogWarning("CultureDoctrineConfigProvider: a doctrine has an empty culture key, dropping it");
                rejected = true;
                continue;
            }
            if (pair.Value?.Tactics == null)
            {
                _logger.LogWarning($"CultureDoctrineConfigProvider: doctrines['{key}'].tactics is null, dropping the doctrine");
                rejected = true;
                continue;
            }

            var isDefault = key == DoctrineCatalog.DefaultKey;
            var doctrine = new Doctrine(key!, ValidateRows(key!, pair.Value.Tactics, ref rejected), isDefault,
                ValidateMorale(key!, pair.Value.Morale, ref rejected),
                ValidateAggression(key!, pair.Value.Aggression, ref rejected),
                ValidateFormations(key!, pair.Value.Formations, ref rejected));
            if (isDefault)
                fallback = doctrine;
            else
                cultures.Add(doctrine);
        }

        if (fallback == null)
        {
            _logger.LogWarning("CultureDoctrineConfigProvider: no 'default' doctrine, cultures without a row get the vanilla-equivalent default");
            rejected = true;
            fallback = DoctrineCatalog.VanillaDefault();
        }

        if (rejected)
            _logger.LogWarning("CultureDoctrineConfigProvider: culture_doctrines.json contained invalid values. See prior warnings for details.");
        else
            _logger.LogInfo($"CultureDoctrineConfigProvider: Loaded culture_doctrines.json ({cultures.Count} culture doctrine(s) plus default)");

        return new DoctrineCatalog(parsed.Enabled, fallback, cultures);
    }

    private IReadOnlyList<TacticEntry> ValidateRows(string culture, List<TacticEntryConfig> rows, ref bool rejected)
    {
        var kept = new List<TacticEntry>(rows.Count);
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var field = $"doctrines['{culture}'].tactics[{i}]";
            if (row == null)
            {
                _logger.LogWarning($"CultureDoctrineConfigProvider: {field} is null, dropping the row");
                rejected = true;
                continue;
            }
            if (!DoctrineTacticIds.TryParse(row.Id, out var tactic))
            {
                _logger.LogWarning($"CultureDoctrineConfigProvider: {field}.id = '{row.Id}' is not a doctrine tactic ({string.Join(", ", DoctrineTacticIds.All)}), dropping the row");
                rejected = true;
                continue;
            }

            // FiniteFloatValidator FIRST: a bare range check is false for NaN and would pass it.
            var multiplier = row.Multiplier;
            if (!FiniteFloatValidator.IsFiniteInRange(multiplier, MinMultiplier, MaxMultiplier))
            {
                _logger.LogWarning($"CultureDoctrineConfigProvider: {field}.multiplier = {multiplier} is not a finite value in [{MinMultiplier}, {MaxMultiplier}], reverting to 1");
                rejected = true;
                multiplier = 1f;
            }

            var minTactics = row.MinTactics;
            if (minTactics < MinTacticsFloor || minTactics > MaxTacticsFloor)
            {
                _logger.LogWarning($"CultureDoctrineConfigProvider: {field}.minTactics = {minTactics} is outside [{MinTacticsFloor}, {MaxTacticsFloor}], reverting to 0");
                rejected = true;
                minTactics = 0;
            }

            if (!DoctrineTacticIds.TryParseSide(row.Side, out var side))
            {
                _logger.LogWarning($"CultureDoctrineConfigProvider: {field}.side = '{row.Side}' is not any, attacker or defender, reverting to any");
                rejected = true;
                side = DoctrineSide.Any;
            }

            kept.Add(new TacticEntry(tactic, multiplier, minTactics, side));
        }
        return kept;
    }

    private CultureMorale ValidateMorale(string culture, MoraleConfig? morale, ref bool rejected)
    {
        if (morale == null)
            return CultureMorale.Vanilla;
        var bravery = morale.Bravery;
        if (!FiniteFloatValidator.IsFiniteInRange(bravery, CultureMorale.MinBravery, CultureMorale.MaxBravery))
        {
            _logger.LogWarning($"CultureDoctrineConfigProvider: doctrines['{culture}'].morale.bravery = {bravery} is not a finite value in [{CultureMorale.MinBravery}, {CultureMorale.MaxBravery}], reverting to 0");
            rejected = true;
            bravery = 0f;
        }
        return new CultureMorale(morale.NeverRout, bravery);
    }

    private CultureAggression ValidateAggression(string culture, AggressionConfig? aggression, ref bool rejected)
    {
        if (aggression == null)
            return CultureAggression.Vanilla;
        return new CultureAggression(
            Multiplier(culture, "attack", aggression.Attack, ref rejected),
            Multiplier(culture, "shield", aggression.Shield, ref rejected),
            Multiplier(culture, "shooterError", aggression.ShooterError, ref rejected),
            Multiplier(culture, "chargeDistance", aggression.ChargeDistance, ref rejected));
    }

    private float Multiplier(string culture, string name, float value, ref bool rejected)
    {
        if (FiniteFloatValidator.IsFiniteInRange(value, CultureAggression.MinMultiplier, CultureAggression.MaxMultiplier))
            return value;
        _logger.LogWarning($"CultureDoctrineConfigProvider: doctrines['{culture}'].aggression.{name} = {value} is not a finite value in [{CultureAggression.MinMultiplier}, {CultureAggression.MaxMultiplier}], reverting to 1");
        rejected = true;
        return 1f;
    }

    private FormationRouting ValidateFormations(string culture, Dictionary<string, string>? formations, ref bool rejected)
    {
        if (formations == null || formations.Count == 0)
            return FormationRouting.None;
        var kept = new Dictionary<string, TaleWorlds.Core.FormationClass>(StringComparer.Ordinal);
        foreach (var pair in formations)
        {
            var troop = pair.Key?.Trim();
            if (string.IsNullOrEmpty(troop))
            {
                _logger.LogWarning($"CultureDoctrineConfigProvider: doctrines['{culture}'].formations has an empty troop id, dropping the row");
                rejected = true;
                continue;
            }
            if (!FormationRouting.TryParseClass(pair.Value, out var formationClass))
            {
                _logger.LogWarning($"CultureDoctrineConfigProvider: doctrines['{culture}'].formations['{troop}'] = '{pair.Value}' is not a formation class ({string.Join(", ", FormationRouting.RoutableNames)}), dropping the row");
                rejected = true;
                continue;
            }
            kept[troop!] = formationClass;
        }
        return new FormationRouting(kept);
    }
}
