namespace TAOM.Features.Diplomacy.Models;

// A kingdom that refuses every call to war against one target, whoever makes the call (#1283). The row has a
// direction: Called refuses a call against Target; Target can still be called against Called.
public class CallToWarRefusal
{
    public string Called { get; set; } = "";
    public string Target { get; set; } = "";
}
