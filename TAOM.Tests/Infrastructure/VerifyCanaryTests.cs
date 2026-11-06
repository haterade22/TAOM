using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Infrastructure;

/// <summary>
/// Canary for /verify: with TAOM_VERIFY_CANARY=1 this test must fail, proving the test run can
/// report a failure. Unset, it passes and costs nothing.
/// </summary>
[TestClass]
public class VerifyCanaryTests
{
    [TestMethod]
    public void Canary_EnvironmentFlagSet_FailsWithToken()
    {
        if (Environment.GetEnvironmentVariable("TAOM_VERIFY_CANARY") == "1")
        {
            Assert.Fail("VERIFY_CANARY_TRIPPED: expected only in /verify Step 2c; TAOM_VERIFY_CANARY=1 is set here, unset it for a normal run.");
        }
    }
}
