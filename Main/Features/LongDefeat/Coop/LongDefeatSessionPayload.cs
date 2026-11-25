using System;
using System.IO;
using System.Text;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Features.LongDefeat.Coop;

/// <summary>
/// The payload of a Long Defeat co-op session (#1280): the fortress id and the run's options, so every member builds the same
/// run. Coop carries it as opaque bytes. A version byte comes first; a payload of another version, a short one or one with
/// bytes left over is refused.
/// </summary>
internal static class LongDefeatSessionPayload
{
    private const byte Version = 2;

    public static byte[] Encode(string fortressId, RunOptions options)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8))
        {
            writer.Write(Version);
            writer.Write(fortressId);
            WriteNullable(writer, options.StopAfterWaves);
            writer.Write(options.Autoplay);
            writer.Write(options.GarrisonScale);
            writer.Write(options.Seed.HasValue);
            if (options.Seed.HasValue)
                writer.Write(options.Seed.Value);
            writer.Write(options.StartWave);
            writer.Write(options.StartRerolls);
            WriteNullable(writer, options.StartGold);
            WriteNullable(writer, options.BreakEnginesAtClear);
            writer.Write(options.ClassId != null);
            if (options.ClassId != null)
                writer.Write(options.ClassId);
            WriteNullable(writer, options.LordFalls);
            writer.Write((byte)options.Difficulty);
        }

        return stream.ToArray();
    }

    public static bool TryDecode(byte[]? payload, out string fortressId, out RunOptions? options)
    {
        fortressId = "";
        options = null;
        if (payload == null || payload.Length == 0 || payload[0] != Version)
            return false;

        try
        {
            using var reader = new BinaryReader(new MemoryStream(payload), Encoding.UTF8);
            reader.ReadByte();
            var id = reader.ReadString();
            var decoded = new RunOptions
            {
                StopAfterWaves = ReadNullable(reader),
                Autoplay = reader.ReadBoolean(),
                GarrisonScale = reader.ReadSingle(),
                Seed = reader.ReadBoolean() ? reader.ReadUInt64() : null,
                StartWave = reader.ReadInt32(),
                StartRerolls = reader.ReadInt32(),
                StartGold = ReadNullable(reader),
                BreakEnginesAtClear = ReadNullable(reader),
                ClassId = reader.ReadBoolean() ? reader.ReadString() : null,
                LordFalls = ReadNullable(reader),
            };
            var difficulty = reader.ReadByte();
            if (difficulty > (byte)RunDifficulty.Hard || reader.BaseStream.Position != payload.Length)
                return false;

            decoded = decoded with { Difficulty = (RunDifficulty)difficulty };

            fortressId = id;
            options = decoded;
            return true;
        }
        catch (IOException)
        {
            // A short payload, or a string length below zero (BinaryReader.ReadString).
            return false;
        }
        catch (FormatException)
        {
            // A string length prefix of more than 5 bytes (BinaryReader.Read7BitEncodedInt).
            return false;
        }
    }

    private static void WriteNullable(BinaryWriter writer, int? value)
    {
        writer.Write(value.HasValue);
        if (value.HasValue)
            writer.Write(value.Value);
    }

    private static int? ReadNullable(BinaryReader reader) => reader.ReadBoolean() ? reader.ReadInt32() : null;
}
