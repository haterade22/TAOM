using System;
using System.Collections.Generic;
using System.Globalization;

namespace TAOM.Features.ShaderCompileNotice;

/// <summary>
/// The arithmetic of the shader-compile notice: counts the engine's <c>compile_shader</c> log lines
/// from the chunks the notice reads off the end of <c>rgl_log_&lt;pid&gt;.txt</c>, a marker split
/// across two chunks counted once, and decides when the notice shows, what its title says and
/// whether the count is worth remembering as the next start's expected total.
/// </summary>
public sealed class ShaderCompileProgress
{
    public const string Marker = "compile_shader";

    /// <summary>A handful of compiles happens on many warm starts; that needs no window.</summary>
    public const int ShowAfter = 10;

    /// <summary>Only a full rebuild (about 1,340 on v1.5.4) is a useful "of about" total.</summary>
    public const int RememberAfter = 200;

    private const int MaxSaneTotal = 100000;

    private string _carry = "";

    public int Count { get; private set; }

    public bool ShouldShow => Count >= ShowAfter;

    public bool ShouldRememberTotal => Count > RememberAfter;

    public void Feed(string chunk)
    {
        if (string.IsNullOrEmpty(chunk)) return;
        string text = _carry + chunk;
        for (int at = text.IndexOf(Marker, StringComparison.Ordinal); at >= 0;
             at = text.IndexOf(Marker, at + Marker.Length, StringComparison.Ordinal))
        {
            Count++;
        }

        // Keep less than one marker's worth: enough to complete a split marker, never a whole one.
        int keep = Math.Min(Marker.Length - 1, text.Length);
        _carry = text.Substring(text.Length - keep);
    }

    public string Title(ShaderCompileNoticeTexts texts, int expectedTotal)
    {
        string count = Count.ToString(CultureInfo.CurrentCulture);
        return expectedTotal > 0 && expectedTotal >= Count
            ? texts.TitleWithTotal.Replace(ShaderCompileNoticeTexts.CountToken, count)
                .Replace(ShaderCompileNoticeTexts.TotalToken, expectedTotal.ToString(CultureInfo.CurrentCulture))
            : texts.TitleCountOnly.Replace(ShaderCompileNoticeTexts.CountToken, count);
    }

    /// <summary>The total remembered beside the shader cache; 0 for a missing, unreadable or absurd value.</summary>
    public static int ParseRememberedTotal(string text)
    {
        return int.TryParse(text?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int total)
            && total > 0 && total <= MaxSaneTotal
            ? total
            : 0;
    }

    /// <summary>
    /// No window on a dedicated server (nobody is looking), and none when yotthani's VanillaTuning is
    /// loaded: it shows the same notice, and two over the game would be one too many.
    /// </summary>
    public static bool ShouldStart(bool isDedicatedServer, IEnumerable<string> activeModuleIds)
    {
        if (isDedicatedServer) return false;
        if (activeModuleIds == null) return true;
        foreach (string id in activeModuleIds)
        {
            if (string.Equals(id, "VanillaTuning", StringComparison.OrdinalIgnoreCase)) return false;
        }

        return true;
    }
}
