using Newtonsoft.Json;
using TAOM.Features.CrashReport.Domain;

namespace TAOM.Features.CrashReport.Rendering;

// JSON-rendered ExceptionContext for the crash bundle (machine-parseable).
// Newtonsoft.Json is already a TAOM PackageReference (v13.0.3) and ships in the
// game's BCL via ButterLib, so no new dependency.
public sealed class JsonCrashReportRenderer : ICrashReportRenderer
{
    private static readonly JsonSerializerSettings Settings = new()
    {
        Formatting = Formatting.Indented,
        NullValueHandling = NullValueHandling.Include,
        ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
        DateFormatString = "yyyy-MM-ddTHH:mm:ss.fffZ",
    };

    public string Render(ExceptionContext context)
        => JsonConvert.SerializeObject(context, Settings);
}
