using System.Collections.Generic;

namespace TAOM.Features.FordsOfWar.Diagnostics;

/// <summary>What the process has seen of the menu entry and the console, across every run.</summary>
internal sealed class FordsSessionEvidence
{
    public FordsMenuEvidence Menu = new();
    public List<FordsConsoleRecord> Console = new();
    public int RunsBegun;
}

internal sealed class FordsMenuEvidence
{
    public bool Installed;
    public bool HasContent;
    public int Factions;
    public int Maps;
    public long Polls;
    public bool? FirstPollHidden;
    public bool? LastPollHidden;
    public int PollChanges;
    public int Clicks;
    public List<FordsMenuFlow> Flows = new();
}

internal sealed class FordsMenuFlow
{
    public List<string> Shown = new();
    public List<string> Answered = new();
    public string Ended = "open";                             // open|start|cancel|abandoned|fault
    public int Run;
}

internal sealed class FordsConsoleRecord
{
    public string Command = "";
    public List<string> Args = new();
    public bool Ran;
    public string Reply = "";
    public int Run;
    public string Setup = "";
    public bool MatchLive;
    public bool MatchStarted;
    public string Failure = "";
}
