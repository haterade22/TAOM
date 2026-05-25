using System;

namespace TAOM.Features.CrashReport.DevTriggers;

// Tagged exception type so post-mortem scripts can distinguish manufactured QA
// crashes from real player crashes. Always thrown via CrashReportDevTrigger.
public sealed class TaomDevTriggerException : Exception
{
    public TaomDevTriggerException(string message) : base(message) { }
}
