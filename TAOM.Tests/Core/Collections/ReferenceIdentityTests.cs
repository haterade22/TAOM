using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Core.Collections;

namespace TAOM.Tests.Core.Collections;

/// <summary>
/// The comparer must never call a key's own <c>GetHashCode</c> or <c>Equals</c>: <c>Formation.GetHashCode</c> is
/// <c>Team.TeamIndex * 10 + FormationIndex</c>, which throws on the order preview's team-less copy.
/// </summary>
[TestClass]
public class ReferenceIdentityTests
{
    /// <summary>Stands in for a team-less <c>Formation</c>: both identity overrides throw.</summary>
    private sealed class ThrowingKey
    {
        public override int GetHashCode() => throw new InvalidOperationException("GetHashCode read");

        public override bool Equals(object? obj) => throw new InvalidOperationException("Equals read");
    }

    [TestMethod]
    public void Dictionary_KeyWhoseGetHashCodeThrows_StoresAndFindsIt()
    {
        var key = new ThrowingKey();
        Assert.ThrowsException<InvalidOperationException>(() => key.GetHashCode(), "the key must be hostile");
        var map = new Dictionary<ThrowingKey, int>(ReferenceIdentity.Instance);

        map[key] = 7;

        Assert.IsTrue(map.TryGetValue(key, out int value));
        Assert.AreEqual(7, value);
        Assert.IsFalse(map.ContainsKey(new ThrowingKey()), "another instance is another key");
    }
}
