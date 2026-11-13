using System;
using System.Linq;
using System.Reflection;
using Autofac;
using Common;
using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Messages;
using GameInterface;
using LiteNetLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.CoopCompat.Tests.Transport;

/// <summary>
/// The installed Coop has every member CoopTransport calls, with the shape it calls. The test project
/// compiles against the same CoopBinDir, so a Coop that dropped a member fails here before the game does.
/// </summary>
[TestClass]
[TestCategory("LiveInstall")]
public class CoopBindingTests
{
    private static MethodInfo Method(Type type, string name, params Type[] parameters)
    {
        var m = type.GetMethod(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static, null, parameters, null);
        Assert.IsNotNull(m, $"{type.FullName}.{name}({string.Join(", ", parameters.Select(p => p.Name))}) is gone from the installed Coop.");
        return m!;
    }

    [TestMethod]
    public void ClientCampaignReady_IsAPublicEvent()
    {
        Assert.IsTrue(typeof(ClientCampaignReady).IsPublic);
        Assert.IsTrue(typeof(IEvent).IsAssignableFrom(typeof(ClientCampaignReady)));
    }

    [TestMethod]
    public void MessageBroker_HasInstanceSubscribeAndUnsubscribe()
    {
        var instance = typeof(MessageBroker).GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(instance);
        Assert.AreEqual(typeof(MessageBroker), instance!.PropertyType);

        foreach (var name in new[] { "Subscribe", "Unsubscribe", "Publish" })
        {
            Assert.IsTrue(typeof(IMessageBroker).GetMethods().Any(m => m.Name == name && m.IsGenericMethodDefinition), name);
        }
    }

    [TestMethod]
    public void MessagePayload_ExposesWhoAndWhat()
    {
        var payload = typeof(MessagePayload<IMessage>);

        Assert.AreEqual(typeof(object), payload.GetProperty("Who")!.PropertyType);
        Assert.AreEqual(typeof(IMessage), payload.GetProperty("What")!.PropertyType);
    }

    [TestMethod]
    public void INetwork_HasSendAndSendAllForMessages()
    {
        Method(typeof(INetwork), "Send", typeof(NetPeer), typeof(IMessage));
        Method(typeof(INetwork), "SendAll", typeof(IMessage));
    }

    [TestMethod]
    public void ContainerProvider_HasTryGetContainerAndTryResolve()
    {
        Method(typeof(ContainerProvider), "TryGetContainer", typeof(ILifetimeScope).MakeByRefType());
        var tryResolve = typeof(ContainerProvider).GetMethod("TryResolve", BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(tryResolve);
        Assert.IsTrue(tryResolve!.IsGenericMethodDefinition);
    }

    [TestMethod]
    public void GameThread_RunSafeTakesAnActionAndOptionalArguments()
    {
        var run = Method(typeof(GameThread), "RunSafe", typeof(Action), typeof(bool), typeof(string), typeof(string), typeof(string));

        Assert.IsTrue(run.IsStatic);
        Assert.AreEqual("context", run.GetParameters()[2].Name, "CoopTransport passes context by name.");
        Assert.IsTrue(run.GetParameters().Skip(1).All(p => p.IsOptional), "CoopTransport calls RunSafe(action, context: ...) only.");
    }

    [TestMethod]
    public void ModInformation_IsServer_IsAStaticBool()
    {
        var p = typeof(ModInformation).GetProperty("IsServer", BindingFlags.Public | BindingFlags.Static);

        Assert.IsNotNull(p);
        Assert.AreEqual(typeof(bool), p!.PropertyType);
        Assert.IsNotNull(p.GetGetMethod());
    }
}
