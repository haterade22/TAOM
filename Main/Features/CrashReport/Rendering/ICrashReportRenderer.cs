using TAOM.Features.CrashReport.Domain;

namespace TAOM.Features.CrashReport.Rendering;

public interface ICrashReportRenderer
{
    string Render(ExceptionContext context);
}
