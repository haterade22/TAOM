using System;
using TAOM.Core.Logging;

namespace TAOM.Features.ShaderCompileNotice;

/// <summary>
/// Runs the notice's window and message loop on the calling STA thread until stopRequested or the mode's cap.
/// WinForms, so TAOM.Client's (ADR-013).
/// </summary>
public interface IShaderCompileNoticeWindowHost
{
    void Run(ShaderCompileNoticeTexts texts, ShaderCompileNoticeMode mode, IModLogger logger, Func<bool> stopRequested);
}
