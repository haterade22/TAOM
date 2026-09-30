using System;

namespace TAOM.Features.RealmBorders.Domain;

/// <summary>
/// Encodes pixels as a 24-bit Windows bitmap, the one picture format every Windows viewer opens and
/// the simplest to write without an imaging library. Rows are stored bottom-up, the bitmap's own
/// order, so the first row given is the picture's bottom row.
/// </summary>
public static class ProvinceBitmap
{
    private const int HeaderSize = 54;

    /// <param name="pixels">ARGB, <paramref name="width"/> per row, bottom row first; alpha is ignored.</param>
    public static byte[] Encode(int width, int height, uint[] pixels)
    {
        if (width <= 0 || height <= 0 || pixels == null || pixels.Length != width * height)
            throw new ArgumentException("the pixels must fill width x height exactly");

        int stride = (width * 3 + 3) & ~3; // every row pads to four bytes
        var bytes = new byte[HeaderSize + stride * height];
        bytes[0] = (byte)'B';
        bytes[1] = (byte)'M';
        Write(bytes, 2, bytes.Length);
        Write(bytes, 10, HeaderSize);
        Write(bytes, 14, 40); // BITMAPINFOHEADER
        Write(bytes, 18, width);
        Write(bytes, 22, height);
        bytes[26] = 1;  // one plane
        bytes[28] = 24; // bits per pixel
        Write(bytes, 34, stride * height);

        for (int row = 0; row < height; row++)
        {
            for (int column = 0; column < width; column++)
            {
                uint argb = pixels[row * width + column];
                int at = HeaderSize + row * stride + column * 3;
                bytes[at] = (byte)argb;             // blue
                bytes[at + 1] = (byte)(argb >> 8);  // green
                bytes[at + 2] = (byte)(argb >> 16); // red
            }
        }
        return bytes;
    }

    private static void Write(byte[] bytes, int offset, int value)
    {
        bytes[offset] = (byte)value;
        bytes[offset + 1] = (byte)(value >> 8);
        bytes[offset + 2] = (byte)(value >> 16);
        bytes[offset + 3] = (byte)(value >> 24);
    }
}
