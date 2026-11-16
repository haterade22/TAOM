namespace TAOM.Features.Siege;

/// <summary>
/// The siege defense popup's text (#1385). siege_defense_config.json holds plain English for each
/// kingdom and field; the service shows it as <c>{=taom_siege_defense_&lt;kingdom&gt;_&lt;field&gt;}English</c>,
/// so a translated row under that key reaches the player. This class is the one place the key
/// formula lives. taom_module_strings.xml is the registry: SiegeDefenseTextRegistryTests keeps its
/// rows equal to the JSON, and tools/loc_composed_families.json declares the keys to the orphan gate.
/// The defaults are the rows of the reserved kingdom id "default".
/// </summary>
internal static class SiegeDefenseText
{
    public const string Title = "title";
    public const string Body = "body";
    public const string AcceptButton = "accept_button";
    public const string AcceptMessage = "accept_message";
    public const string RewardMessage = "reward_message";

    public const string DefaultTitle = "{=taom_siege_defense_default_title}{attacker} is besieging {settlement}!";
    public const string DefaultBody = "{=taom_siege_defense_default_body}Will you answer the call to defend? You have {days} days to reach the settlement.";
    public const string DefaultAcceptButton = "{=taom_siege_defense_default_accept_button}Help Defend";
    public const string DefaultAcceptMessage = "{=taom_siege_defense_default_accept_message}You have pledged to defend {settlement}. Ride now!";
    public const string DefaultRewardMessage = "{=taom_siege_defense_default_reward_message}You answered the call! +{influence} influence, +{relation} relation.";

    public const string Ignore = "{=taom_siege_defense_ignore}Ignore";

    public static string KeyFor(string kingdomId, string field) => "taom_siege_defense_" + kingdomId + "_" + field;

    /// <summary>
    /// The kingdom's configured English under its key, or <paramref name="fallback"/> when the entry
    /// has none: null or "". A value of only spaces is kept as written (#660).
    /// </summary>
    public static string Keyed(string kingdomId, string field, string? configured, string fallback) =>
        configured is null || configured.Length == 0 ? fallback : "{=" + KeyFor(kingdomId, field) + "}" + configured;
}
