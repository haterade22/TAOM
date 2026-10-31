namespace TAOM.Features.FordsOfWar;

/// <summary>One row of a main-menu prompt: the id the flow keeps and the label to show. The label is raw <c>{=KEY}English</c> text; the menu adapter resolves it.</summary>
internal sealed record FordsChoice(string Id, string Label);
