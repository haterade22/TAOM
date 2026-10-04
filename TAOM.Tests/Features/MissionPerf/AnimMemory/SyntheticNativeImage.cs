using System;
using System.Text;

namespace TAOM.Tests.Features.MissionPerf.AnimMemory;

/// <summary>
/// Builds a fake loaded native image for the clip memory probe tests: an x64 PE32+ header block with
/// a chosen section table, and a <c>.text</c> blob holding the eviction-pass site whose two rip
/// displacements point wherever the test wants.
/// </summary>
internal static class SyntheticNativeImage
{
    internal const long Base = 0x180000000L;

    /// <summary>The 50 bytes at the eviction pass's counter load in the v1.5.3 TaleWorlds.Native.dll.</summary>
    internal static readonly byte[] RealSite =
    {
        0x8B, 0x05, 0x2B, 0xDE, 0xB8, 0x00, 0x41, 0x8B, 0xEC, 0x48, 0x8B, 0x3D, 0x09, 0xDE, 0xB8, 0x00,
        0x48, 0x2B, 0x3D, 0xFA, 0xDD, 0xB8, 0x00, 0x48, 0xC1, 0xFF, 0x04, 0x83, 0xEF, 0x01, 0x66, 0x0F,
        0x6E, 0xC0, 0x0F, 0x5B, 0xC0, 0xF3, 0x0F, 0x5C, 0x05, 0xA0, 0x02, 0x91, 0x00, 0xF3, 0x44, 0x0F,
        0x2C, 0xF8,
    };

    internal static readonly (string Name, int Va, int Size)[] Standard =
    {
        (".text", 0x1000, 0x200),
        (".rdata", 0x2000, 0x100),
        (".data", 0x3000, 0x100),
    };

    internal static byte[] Headers(params (string Name, int Va, int Size)[] sections)
    {
        var h = new byte[4096];
        h[0] = (byte)'M';
        h[1] = (byte)'Z';
        PutInt32(h, 0x3C, 0x80);
        h[0x80] = (byte)'P';
        h[0x81] = (byte)'E';
        PutUInt16(h, 0x84, 0x8664);
        PutUInt16(h, 0x86, sections.Length);
        PutUInt16(h, 0x94, 0xF0);
        PutUInt16(h, 0x98, 0x20B);
        var table = 0x80 + 24 + 0xF0;
        for (var i = 0; i < sections.Length; i++)
        {
            var entry = table + 40 * i;
            var name = Encoding.ASCII.GetBytes(sections[i].Name);
            Array.Copy(name, 0, h, entry, Math.Min(8, name.Length));
            PutInt32(h, entry + 8, sections[i].Size);
            PutInt32(h, entry + 12, sections[i].Va);
            PutInt32(h, entry + 16, sections[i].Size);
            PutInt32(h, entry + 20, sections[i].Va);
        }
        return h;
    }

    internal static byte[] TextWithSiteAt(int textOffset, int counterRva, int budgetRva, int textRva = 0x1000, int textSize = 0x200)
    {
        var text = new byte[textSize];
        PutSite(text, textOffset, counterRva, budgetRva, textRva);
        return text;
    }

    /// <summary>Copies the real site into <paramref name="text"/> and points its two displacements.</summary>
    internal static void PutSite(byte[] text, int textOffset, int counterRva, int budgetRva, int textRva = 0x1000)
    {
        Array.Copy(RealSite, 0, text, textOffset, RealSite.Length);
        PutInt32(text, textOffset + 2, counterRva - (textRva + textOffset + 6));
        PutInt32(text, textOffset + 41, budgetRva - (textRva + textOffset + 37 + 8));
    }

    internal static void PutInt32(byte[] buffer, int offset, int value) =>
        Array.Copy(BitConverter.GetBytes(value), 0, buffer, offset, 4);

    internal static void PutUInt16(byte[] buffer, int offset, int value)
    {
        buffer[offset] = (byte)(value & 0xFF);
        buffer[offset + 1] = (byte)((value >> 8) & 0xFF);
    }
}
