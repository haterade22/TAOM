using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.LongDefeat.Coop;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Tests.Features.LongDefeat.Coop;

[TestClass]
public class LongDefeatSessionPayloadTests
{
    private static readonly RunOptions Full = new()
    {
        StopAfterWaves = 7,
        Autoplay = true,
        GarrisonScale = 0.5f,
        Seed = RunOptions.MaxSeed,
        StartWave = 3,
        StartRerolls = 2,
        StartGold = 1234,
        BreakEnginesAtClear = 4,
        ClassId = "cavalry",
        LordFalls = 2,
    };

    [TestMethod]
    public void RoundTrip_KeepsTheFortressAndEveryOption()
    {
        Assert.IsTrue(LongDefeatSessionPayload.TryDecode(LongDefeatSessionPayload.Encode("helms", Full), out var fortressId, out var options));

        Assert.AreEqual("helms", fortressId);
        Assert.AreEqual(Full, options);
    }

    // A field the payload leaves out would default on every member and still pass the round trip; this list makes a new field fail here.
    [TestMethod]
    public void ThePayload_KnowsEveryRunOption()
    {
        var names = typeof(RunOptions).GetProperties(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public)
            .Select(p => p.Name).OrderBy(n => n).ToArray();

        CollectionAssert.AreEqual(new[]
        {
            "Autoplay", "BreakEnginesAtClear", "ClassId", "GarrisonScale", "LordFalls", "Seed", "StartGold", "StartRerolls", "StartWave", "StopAfterWaves",
        }, names, "add the new field to LongDefeatSessionPayload, then to this list");
    }

    // A payload comes from the network: a bad string length must be refused, not thrown.
    [TestMethod]
    public void TryDecode_RefusesABadStringLength()
    {
        var tooLong = new byte[] { 1, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x01 };
        var negative = new byte[] { 1, 0xFF, 0xFF, 0xFF, 0xFF, 0x0F };

        Assert.IsFalse(LongDefeatSessionPayload.TryDecode(tooLong, out _, out _));
        Assert.IsFalse(LongDefeatSessionPayload.TryDecode(negative, out _, out _));
    }

    [TestMethod]
    public void RoundTrip_KeepsNullOptionsNull()
    {
        Assert.IsTrue(LongDefeatSessionPayload.TryDecode(LongDefeatSessionPayload.Encode("helms", RunOptions.Default), out _, out var options));

        Assert.AreEqual(RunOptions.Default, options);
        Assert.IsNull(options!.Seed);
        Assert.IsNull(options.ClassId);
    }

    [TestMethod]
    public void TryDecode_RefusesAnotherVersion()
    {
        var bytes = LongDefeatSessionPayload.Encode("helms", Full);
        bytes[0] = 99;

        Assert.IsFalse(LongDefeatSessionPayload.TryDecode(bytes, out _, out _));
    }

    [TestMethod]
    public void TryDecode_RefusesEveryTruncatedPayload()
    {
        var bytes = LongDefeatSessionPayload.Encode("helms", Full);

        for (int length = 0; length < bytes.Length; length++)
            Assert.IsFalse(LongDefeatSessionPayload.TryDecode(bytes.Take(length).ToArray(), out _, out _), $"length {length}");
    }

    [TestMethod]
    public void TryDecode_RefusesTrailingBytesAndNull()
    {
        var bytes = LongDefeatSessionPayload.Encode("helms", Full).Concat(new byte[] { 0 }).ToArray();

        Assert.IsFalse(LongDefeatSessionPayload.TryDecode(bytes, out _, out _));
        Assert.IsFalse(LongDefeatSessionPayload.TryDecode(null, out _, out _));
    }

    [TestMethod]
    public void Encode_WithLongIds_StaysUnderTheSessionPayloadLimit()
    {
        var options = Full with { ClassId = new string('c', 64) };

        var bytes = LongDefeatSessionPayload.Encode(new string('f', 64), options);

        Assert.IsTrue(bytes.Length < LongDefeatCoopContract.MaxPayloadBytes, $"{bytes.Length} bytes");
    }
}
