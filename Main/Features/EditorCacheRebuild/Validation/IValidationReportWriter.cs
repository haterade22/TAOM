namespace TAOM.Features.EditorCacheRebuild.Validation;

public interface IValidationReportWriter
{
    void Write(string filePath, ValidationReport report);
}
