using System;
using System.Collections.Generic;
using System.Text;

namespace TAOM.Features.MissionPerf.AnimMemory;

/// <summary>One entry of a PE section table. Raw and mapped layouts agree inside a section.</summary>
internal sealed class PeSection
{
    internal PeSection(string name, int virtualAddress, int virtualSize, int pointerToRawData, int sizeOfRawData)
    {
        Name = name;
        VirtualAddress = virtualAddress;
        VirtualSize = virtualSize;
        PointerToRawData = pointerToRawData;
        SizeOfRawData = sizeOfRawData;
    }

    internal string Name { get; }
    internal int VirtualAddress { get; }
    internal int VirtualSize { get; }
    internal int PointerToRawData { get; }
    internal int SizeOfRawData { get; }

    /// <summary>True when <paramref name="length"/> bytes at <paramref name="rva"/> all lie inside this section.</summary>
    internal bool Contains(int rva, int length) =>
        rva >= VirtualAddress && (long)rva + length <= (long)VirtualAddress + VirtualSize;
}

/// <summary>
/// Reads the section table of a mapped x64 module's header block, so the clip memory probe can prove
/// an address is inside a mapped section before it reads it (an access violation is not catchable on
/// .NET Framework).
/// </summary>
internal static class PeSectionTable
{
    private const ushort MachineAmd64 = 0x8664;
    private const ushort MagicPe32Plus = 0x20B;
    private const int SectionEntrySize = 40;

    /// <summary>The section table of an x64 PE32+ header block, or null when it is not one.</summary>
    internal static IReadOnlyList<PeSection>? Parse(byte[] headers)
    {
        if (headers == null || headers.Length < 0x40) return null;
        if (headers[0] != (byte)'M' || headers[1] != (byte)'Z') return null;

        var lfanew = BitConverter.ToInt32(headers, 0x3C);
        if (lfanew <= 0 || (long)lfanew + 26 > headers.Length) return null;
        if (headers[lfanew] != (byte)'P' || headers[lfanew + 1] != (byte)'E'
            || headers[lfanew + 2] != 0 || headers[lfanew + 3] != 0) return null;
        if (BitConverter.ToUInt16(headers, lfanew + 4) != MachineAmd64) return null;
        if (BitConverter.ToUInt16(headers, lfanew + 24) != MagicPe32Plus) return null;

        int count = BitConverter.ToUInt16(headers, lfanew + 6);
        int optionalSize = BitConverter.ToUInt16(headers, lfanew + 20);
        var table = (long)lfanew + 24 + optionalSize;
        if (table + (long)SectionEntrySize * count > headers.Length) return null;

        var sections = new List<PeSection>(count);
        for (var i = 0; i < count; i++)
        {
            var entry = (int)table + SectionEntrySize * i;
            sections.Add(new PeSection(
                ReadName(headers, entry),
                virtualAddress: BitConverter.ToInt32(headers, entry + 12),
                virtualSize: BitConverter.ToInt32(headers, entry + 8),
                pointerToRawData: BitConverter.ToInt32(headers, entry + 20),
                sizeOfRawData: BitConverter.ToInt32(headers, entry + 16)));
        }
        return sections;
    }

    internal static PeSection? Find(IReadOnlyList<PeSection> sections, string name)
    {
        for (var i = 0; i < sections.Count; i++)
            if (sections[i].Name == name)
                return sections[i];
        return null;
    }

    private static string ReadName(byte[] headers, int entry)
    {
        var length = 0;
        while (length < 8 && headers[entry + length] != 0)
            length++;
        return Encoding.ASCII.GetString(headers, entry, length);
    }
}
