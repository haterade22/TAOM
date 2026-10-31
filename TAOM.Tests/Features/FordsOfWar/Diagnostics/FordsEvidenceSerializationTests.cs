using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FordsOfWar.Diagnostics;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Tests.Features.FordsOfWar.Diagnostics;

/// <summary>The evidence graph is serialized under a lock on the main thread, so it must hold no engine object.</summary>
[TestClass]
public class FordsEvidenceSerializationTests
{
    [TestMethod]
    public void EvidenceGraph_HoldsOnlyBclDiagnosticsAndDomainTypes()
    {
        var seen = new HashSet<Type>();
        var offenders = new List<string>();
        Walk(typeof(FordsRunEvidence), seen, offenders);
        Walk(typeof(FordsSessionEvidence), seen, offenders);

        Assert.IsTrue(seen.Count > 20, "the walk reached the nested evidence classes");
        Assert.AreEqual(0, offenders.Count, "engine or foreign types in the evidence:\n" + string.Join("\n", offenders));
    }

    private static bool Allowed(Type t)
    {
        var ns = t.Namespace ?? "";
        return ns == "System" || ns.StartsWith("System.Collections.Generic", StringComparison.Ordinal)
            || ns == "TAOM.Features.FordsOfWar.Diagnostics" || ns == "TAOM.Features.FordsOfWar.Domain";
    }

    private static void Walk(Type type, HashSet<Type> seen, List<string> offenders)
    {
        if (type.HasElementType)
        {
            Walk(type.GetElementType()!, seen, offenders);
            return;
        }

        if (type.IsGenericType)
        {
            foreach (var arg in type.GetGenericArguments())
                Walk(arg, seen, offenders);
            if (!Allowed(type.GetGenericTypeDefinition()))
                offenders.Add(type.FullName ?? type.Name);
            return;
        }

        if (!seen.Add(type))
            return;
        if (!Allowed(type))
        {
            offenders.Add(type.FullName ?? type.Name);
            return;
        }

        if (type.Namespace != "TAOM.Features.FordsOfWar.Diagnostics")
            return;

        foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
            Walk(field.FieldType, seen, offenders);
        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            Walk(property.PropertyType, seen, offenders);
    }
}
