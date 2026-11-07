# Erzeugt MausHighlighter.ico (16–256 px) – Mauszeiger im gelben Highlight-Kreis mit Klick-Welle.
# Aufruf:  powershell -ExecutionPolicy Bypass -File tools\make-icon.ps1
param([string]$Out = (Join-Path $PSScriptRoot "..\MausHighlighter.ico"))

Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

public static class IconMaker
{
    static Bitmap Draw(int s)
    {
        var bmp = new Bitmap(s, s, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.Clear(Color.Transparent);
            float f = s / 256f;
            Color gold = Color.FromArgb(255, 214, 0);

            // Highlight-Kreis: halbtransparente Füllung + kräftiger Rand
            float ringW = Math.Max(1.5f, 14 * f);
            float r = s / 2f - ringW / 2f - Math.Max(0.5f, 2 * f);
            float c = s / 2f;
            using (var b = new SolidBrush(Color.FromArgb(235, 255, 240, 150))) g.FillEllipse(b, c - r, c - r, 2 * r, 2 * r);
            using (var p = new Pen(gold, ringW)) g.DrawEllipse(p, c - r, c - r, 2 * r, 2 * r);

            // Klick-Welle (ab 32 px sichtbar)
            if (s >= 32)
            {
                float wr = r * 0.62f;
                using (var p = new Pen(Color.FromArgb(170, 240, 180, 0), Math.Max(1.5f, 7 * f)))
                    g.DrawEllipse(p, c - wr, c - wr, 2 * wr, 2 * wr);
            }

            // Klassischer Pfeil-Mauszeiger (Spitze leicht links oberhalb der Mitte)
            PointF[] arrow = {
                new PointF(0, 0), new PointF(0, 17), new PointF(4, 13), new PointF(7, 20),
                new PointF(9.5f, 19), new PointF(6.5f, 12), new PointF(12, 12)
            };
            float u = s * (s <= 16 ? 0.034f : 0.028f);   // Einheitsgröße des Pfeils
            float ox = s * 0.40f, oy = s * 0.24f;
            if (s <= 16) { ox = s * 0.34f; oy = s * 0.16f; }
            var pts = new PointF[arrow.Length];
            for (int i = 0; i < arrow.Length; i++) pts[i] = new PointF(ox + arrow[i].X * u, oy + arrow[i].Y * u);
            using (var path = new GraphicsPath())
            {
                path.AddPolygon(pts);
                // weicher Schatten
                if (s >= 48)
                    using (var m = new Matrix()) using (var sh = (GraphicsPath)path.Clone())
                    {
                        m.Translate(4 * f, 5 * f); sh.Transform(m);
                        using (var b = new SolidBrush(Color.FromArgb(70, 0, 0, 0))) g.FillPath(b, sh);
                    }
                using (var b = new SolidBrush(Color.White)) g.FillPath(b, path);
                using (var p = new Pen(Color.FromArgb(30, 30, 30), Math.Max(1f, 9 * f)) { LineJoin = LineJoin.Round })
                    g.DrawPath(p, path);
            }
        }
        return bmp;
    }

    // ICO-Eintrag als 32-bit-DIB (für kleine Größen am kompatibelsten)
    static byte[] Dib(Bitmap bmp)
    {
        int s = bmp.Width;
        int maskStride = ((s + 31) / 32) * 4;
        using (var ms = new MemoryStream())
        using (var w = new BinaryWriter(ms))
        {
            w.Write(40); w.Write(s); w.Write(s * 2); w.Write((short)1); w.Write((short)32);
            w.Write(0); w.Write(s * s * 4 + maskStride * s); w.Write(0); w.Write(0); w.Write(0); w.Write(0);
            for (int y = s - 1; y >= 0; y--)
                for (int x = 0; x < s; x++)
                {
                    Color px = bmp.GetPixel(x, y);
                    w.Write(px.B); w.Write(px.G); w.Write(px.R); w.Write(px.A);
                }
            for (int y = s - 1; y >= 0; y--)
            {
                var row = new byte[maskStride];
                for (int x = 0; x < s; x++)
                    if (bmp.GetPixel(x, y).A == 0) row[x / 8] |= (byte)(0x80 >> (x % 8));
                w.Write(row);
            }
            return ms.ToArray();
        }
    }

    public static void Make(string path)
    {
        int[] sizes = { 16, 20, 24, 32, 40, 48, 64, 128, 256 };
        var images = new List<byte[]>();
        foreach (int s in sizes)
            using (var bmp = Draw(s))
            {
                if (s == 256)
                    using (var ms = new MemoryStream()) { bmp.Save(ms, ImageFormat.Png); images.Add(ms.ToArray()); }
                else images.Add(Dib(bmp));
            }
        using (var fs = File.Create(path))
        using (var w = new BinaryWriter(fs))
        {
            w.Write((short)0); w.Write((short)1); w.Write((short)sizes.Length);
            int offset = 6 + 16 * sizes.Length;
            for (int i = 0; i < sizes.Length; i++)
            {
                int s = sizes[i];
                w.Write((byte)(s >= 256 ? 0 : s)); w.Write((byte)(s >= 256 ? 0 : s));
                w.Write((byte)0); w.Write((byte)0); w.Write((short)1); w.Write((short)32);
                w.Write(images[i].Length); w.Write(offset);
                offset += images[i].Length;
            }
            foreach (var img in images) w.Write(img);
        }
        using (var prev = Draw(256)) prev.Save(Path.ChangeExtension(path, ".png"), ImageFormat.Png);
    }
}
'@

[IconMaker]::Make([System.IO.Path]::GetFullPath($Out))
"Icon geschrieben: $([System.IO.Path]::GetFullPath($Out))"
