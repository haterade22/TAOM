using System;
using System.IO;
using System.Text;

namespace TAOM.Features.FiefGranting;

/// <summary>
/// Encodes a TAOM fief election for Coop's <c>KingdomDecisionTypeRegistry</c> (#1346). Coop sends the
/// kingdom, trigger time and flags itself; this carries the settlement, the excluded clan and the
/// proposer. Decoding refuses anything malformed; Coop then drops the decision on that client alone.
/// </summary>
public static class FiefClaimantWire
{
    public const byte Version = 1;
    public const int MaxIdLength = 128;

    public static byte[] Encode(FiefClaimantWireData data)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));

        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8))
        {
            writer.Write(Version);
            writer.Write(data.SettlementId ?? "");
            writer.Write(data.ProposerClanId ?? "");
            writer.Write(data.ClanToExcludeId != null);
            if (data.ClanToExcludeId != null) writer.Write(data.ClanToExcludeId);
        }

        return stream.ToArray();
    }

    public static bool TryDecode(byte[] payload, out FiefClaimantWireData data)
    {
        data = null;
        if (payload == null || payload.Length == 0) return false;

        try
        {
            using var stream = new MemoryStream(payload, writable: false);
            using var reader = new BinaryReader(stream, Encoding.UTF8);
            if (reader.ReadByte() != Version) return false;

            var settlementId = reader.ReadString();
            var proposerClanId = reader.ReadString();
            var clanToExcludeId = reader.ReadBoolean() ? reader.ReadString() : null;
            if (stream.Position != stream.Length) return false;

            if (!IsId(settlementId) || !IsId(proposerClanId)) return false;
            if (clanToExcludeId != null && !IsId(clanToExcludeId)) return false;

            data = new FiefClaimantWireData(settlementId, proposerClanId, clanToExcludeId);
            return true;
        }
        catch (Exception ex) when (ex is EndOfStreamException || ex is IOException || ex is FormatException)
        {
            return false;
        }
    }

    private static bool IsId(string text) => !string.IsNullOrEmpty(text) && text.Length <= MaxIdLength;
}
