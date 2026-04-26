using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Text;

// Half-width katakana U+FF66..U+FF9D plus digits
var alphabet = new char[0x9D - 0x66 + 1 + 10];
for (int i = 0; i <= 0x9D - 0x66; i++) alphabet[i] = (char)(0xFF66 + i);
for (int i = 0; i < 10;            i++) alphabet[0x9D - 0x66 + 1 + i] = (char)('0' + i);

int[] sizes = [256, 64, 48, 32, 16];
var   blobs = sizes.Select(sz => DrawIcon(sz)).ToArray();
WriteIco(blobs, sizes, args.Length > 0 ? args[0] : "icon.ico");
Console.WriteLine("Done.");

byte[] DrawIcon(int size)
{
    using var bmp = new Bitmap(size, size);
    using var g   = Graphics.FromImage(bmp);
    g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
    g.SmoothingMode     = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
    g.Clear(Color.Black);

    var rng = new Random(42);

    // ── background: rain texture (large sizes only) ───────────────
    if (size >= 48)
    {
        float fs    = MathF.Max(5f, size / 17f);
        using var f = new Font("Courier New", fs, FontStyle.Bold);
        float cw    = size / (fs * 0.75f);
        float ch    = size / (fs * 1.3f);
        for (int c = 0; c < (int)cw; c++)
        for (int r = 0; r < (int)ch; r++)
        {
            int   alpha = rng.Next(20, 85);
            int   green = rng.Next(50, 170);
            using var b = new SolidBrush(Color.FromArgb(alpha, 0, green, 0));
            g.DrawString(alphabet[rng.Next(alphabet.Length)].ToString(), f, b,
                         c * fs * 0.75f, r * fs * 1.3f);
        }
    }

    // ── semi-transparent square behind "M" ────────────────────────
    int   pad  = (int)(size * 0.13f);
    var   sq   = new Rectangle(pad, pad, size - pad * 2, size - pad * 2);
    using var bgBr = new SolidBrush(Color.FromArgb(155, 0, 0, 0));
    g.FillRectangle(bgBr, sq);

    // ── glowing "M" ───────────────────────────────────────────────
    float mfs  = MathF.Max(7f, size * 0.50f);
    using var mf = new Font("Courier New", mfs, FontStyle.Bold);
    var   msz  = g.MeasureString("M", mf);
    float mx   = (size - msz.Width)  / 2f - 1f;
    float my   = (size - msz.Height) / 2f - 1f;

    for (int pass = 6; pass >= 1; pass--)
    {
        int a = pass * 16;
        using var gb = new SolidBrush(Color.FromArgb(a, 0, 210, 0));
        g.DrawString("M", mf, gb, mx - pass, my - pass);
        g.DrawString("M", mf, gb, mx + pass, my + pass);
    }

    using var mb  = new SolidBrush(Color.FromArgb(0, 255, 70));
    using var mhi = new SolidBrush(Color.FromArgb(130, 210, 255, 210));
    g.DrawString("M", mf, mb,  mx, my);
    g.DrawString("M", mf, mhi, mx, my);

    // ── green border ──────────────────────────────────────────────
    if (size >= 32)
    {
        using var pen = new Pen(Color.FromArgb(70, 0, 190, 0), 1.5f);
        g.DrawRectangle(pen, 1, 1, size - 3, size - 3);
    }

    using var ms = new MemoryStream();
    bmp.Save(ms, ImageFormat.Png);
    return ms.ToArray();
}

void WriteIco(byte[][] blobs, int[] sizes, string path)
{
    using var out2 = new MemoryStream();
    using var w    = new BinaryWriter(out2);

    w.Write((ushort)0);              // reserved
    w.Write((ushort)1);              // type = icon
    w.Write((ushort)sizes.Length);   // count

    int dataOffset = 6 + 16 * sizes.Length;
    for (int i = 0; i < sizes.Length; i++)
    {
        byte dim = sizes[i] >= 256 ? (byte)0 : (byte)sizes[i];
        w.Write(dim);                        // width
        w.Write(dim);                        // height
        w.Write((byte)0);                    // color count
        w.Write((byte)0);                    // reserved
        w.Write((ushort)1);                  // planes
        w.Write((ushort)32);                 // bit depth
        w.Write((uint)blobs[i].Length);      // data size
        w.Write((uint)dataOffset);           // data offset
        dataOffset += blobs[i].Length;
    }
    foreach (var b in blobs) w.Write(b);

    w.Flush();
    File.WriteAllBytes(path, out2.ToArray());
}
