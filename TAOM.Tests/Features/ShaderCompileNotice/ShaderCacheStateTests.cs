using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.ShaderCompileNotice;

namespace TAOM.Tests.Features.ShaderCompileNotice;

/// <summary>
/// One line in every log answers "was this session cold from the start?": v1.4.8 rewrites
/// shader_mapping.bin at startup when it drops its runtime cache. The literals are twin-pinned in
/// tools/tests/test_triage_battle_load.py.
/// </summary>
[TestClass]
public class ShaderCacheStateTests
{
    private static readonly DateTime Start = new DateTime(2026, 11, 4, 10, 0, 0, DateTimeKind.Utc);

    [TestMethod]
    public void Describe_FolderMissing_SaysNotFound()
    {
        Assert.AreEqual(
            "[ShaderCache] CoreShaders/D3D11 folder not found",
            ShaderCacheState.Describe(false, null, null, 0, Start));
    }

    [TestMethod]
    public void Describe_NoMapping_SaysAbsent()
    {
        Assert.AreEqual(
            "[ShaderCache] CoreShaders/D3D11 rewrittenThisStart=unknown mapping=absent sacx=0",
            ShaderCacheState.Describe(true, null, null, 0, Start));
    }

    [TestMethod]
    public void Describe_MappingWrittenAfterStart_SaysRewritten()
    {
        Assert.AreEqual(
            "[ShaderCache] CoreShaders/D3D11 rewrittenThisStart=true mappingBytes=13 sacx=0 mappingWrittenUtc=2026-11-04T10:00:03Z processStartUtc=2026-11-04T10:00:00Z",
            ShaderCacheState.Describe(true, 13, new DateTime(2026, 11, 4, 10, 0, 3, DateTimeKind.Utc), 0, Start));
    }

    [TestMethod]
    public void Describe_MappingWrittenBeforeStart_SaysKept()
    {
        Assert.AreEqual(
            "[ShaderCache] CoreShaders/D3D11 rewrittenThisStart=false mappingBytes=38461 sacx=363 mappingWrittenUtc=2026-08-14T13:44:00Z processStartUtc=2026-11-04T10:00:00Z",
            ShaderCacheState.Describe(true, 38461, new DateTime(2026, 8, 14, 13, 44, 0, DateTimeKind.Utc), 363, Start));
    }
}
