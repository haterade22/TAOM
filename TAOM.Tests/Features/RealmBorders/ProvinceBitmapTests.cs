using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.RealmBorders.Domain;

namespace TAOM.Tests.Features.RealmBorders;

[TestClass]
public class ProvinceBitmapTests
{
    [TestMethod]
    public void Encode_WritesTheHeaderAndPadsEachRowToFourBytes()
    {
        var bytes = ProvinceBitmap.Encode(3, 2, new uint[6]);

        Assert.AreEqual((byte)'B', bytes[0]);
        Assert.AreEqual((byte)'M', bytes[1]);
        Assert.AreEqual(54 + 12 * 2, bytes.Length, "3 pixels are 9 bytes, padded to 12");
        Assert.AreEqual(bytes.Length, BitConverter.ToInt32(bytes, 2));
        Assert.AreEqual(54, BitConverter.ToInt32(bytes, 10));
        Assert.AreEqual(3, BitConverter.ToInt32(bytes, 18));
        Assert.AreEqual(2, BitConverter.ToInt32(bytes, 22));
        Assert.AreEqual(24, BitConverter.ToInt16(bytes, 28));
    }

    [TestMethod]
    public void Encode_StoresEachPixelBlueGreenRed_FirstRowAtTheBottom()
    {
        var bytes = ProvinceBitmap.Encode(1, 2, new[] { 0xFF102030u, 0xFFA0B0C0u });

        CollectionAssert.AreEqual(new byte[] { 0x30, 0x20, 0x10 }, new[] { bytes[54], bytes[55], bytes[56] }, "the first row is stored first");
        CollectionAssert.AreEqual(new byte[] { 0xC0, 0xB0, 0xA0 }, new[] { bytes[58], bytes[59], bytes[60] });
    }

    [TestMethod]
    public void Encode_PixelsThatDoNotFillTheImage_Throw()
    {
        Assert.ThrowsException<ArgumentException>(() => ProvinceBitmap.Encode(2, 2, new uint[3]));
        Assert.ThrowsException<ArgumentException>(() => ProvinceBitmap.Encode(0, 2, new uint[0]));
    }
}
