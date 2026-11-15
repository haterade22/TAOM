using System;
using TAOM.Core.Logging;

namespace TAOM.Features.ShaderCompileNotice;

/// <summary>Core's default (ADR-013): shows nothing and returns at once. TAOM.Client replaces it on a rendering client.</summary>
public sealed class NullShaderCompileNoticeWindowHost : IShaderCompileNoticeWindowHost
{
    public void Run(ShaderCompileNoticeTexts texts, ShaderCompileNoticeMode mode, IModLogger logger, Func<bool> stopRequested) { }
}
