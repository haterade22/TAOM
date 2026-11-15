using System;
using System.Windows.Forms;
using TAOM.Core.Logging;
using TAOM.Features.ShaderCompileNotice;

namespace TAOM.Client.Features.ShaderCompileNotice;

/// <summary>Runs the shader notice's WinForms message loop on the calling STA thread (ADR-013); core's null host shows nothing.</summary>
internal sealed class ShaderCompileNoticeWindowHost : IShaderCompileNoticeWindowHost
{
    public void Run(ShaderCompileNoticeTexts texts, ShaderCompileNoticeMode mode, IModLogger logger, Func<bool> stopRequested)
    {
        using var context = new ShaderCompileNoticeContext(texts, mode, logger, stopRequested);
        Application.Run(context);
    }
}
