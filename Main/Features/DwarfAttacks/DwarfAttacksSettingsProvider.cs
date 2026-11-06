using System;

namespace TAOM.Features.DwarfAttacks;

/// <summary>
/// The MCM settings (Battle Tactics/Dwarf Attacks in <see cref="TaomSettings"/>), read live on every tree decision
/// and every impact. The settings object is taken once, on the first non-null read (the hot-path pattern,
/// HotPathSettingsProvidersTests). The damage slider is clamped here: a hand-edited json2 can hold any integer.
/// </summary>
public class DwarfAttacksSettingsProvider
{
    private TaomSettings? _settings;
    private TaomSettings? Settings => _settings ??= TaomSettings.Instance;

    public DwarfAttacksSettingsProvider() { }
    internal DwarfAttacksSettingsProvider(TaomSettings? settings) => _settings = settings;

    public bool Enabled => Settings?.DwarfAttacksEnabled ?? true;

    public int DamagePercent => Math.Max(0, Math.Min(DwarfAttacksConfig.MaxDamagePercent,
        Settings?.DwarfAttacksDamagePercent ?? DwarfAttacksConfig.DefaultDamagePercent));
}
