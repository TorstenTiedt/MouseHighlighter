// Maus-Highlighter: zeigt einen farbigen Kreis um den Mauszeiger – auf allen Monitoren.
// Kompilieren: build.cmd (nutzt den in Windows enthaltenen C#-Compiler, keine Installation nötig)
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: System.Reflection.AssemblyTitle("Maus-Highlighter")]
[assembly: System.Reflection.AssemblyProduct("Maus-Highlighter")]
[assembly: System.Reflection.AssemblyVersion("1.0.0.0")]

namespace MausHighlighter
{
    static class Native
    {
        [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
        [StructLayout(LayoutKind.Sequential)] public struct SIZE { public int cx, cy; }
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct BLENDFUNCTION { public byte BlendOp, BlendFlags, SourceConstantAlpha, AlphaFormat; }
        [StructLayout(LayoutKind.Sequential)]
        public struct MSLLHOOKSTRUCT { public POINT pt; public uint mouseData, flags, time; public IntPtr dwExtraInfo; }

        public delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, ref POINT pptDst, ref SIZE psize,
            IntPtr hdcSrc, ref POINT pptSrc, int crKey, ref BLENDFUNCTION pblend, int dwFlags);
        [DllImport("user32.dll")] public static extern IntPtr GetDC(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
        [DllImport("gdi32.dll")] public static extern IntPtr CreateCompatibleDC(IntPtr hDC);
        [DllImport("gdi32.dll")] public static extern bool DeleteDC(IntPtr hdc);
        [DllImport("gdi32.dll")] public static extern IntPtr SelectObject(IntPtr hDC, IntPtr hObject);
        [DllImport("gdi32.dll")] public static extern bool DeleteObject(IntPtr hObject);
        [DllImport("user32.dll")]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
        [DllImport("user32.dll")] public static extern bool GetCursorPos(out POINT p);
        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SetWindowsHookEx(int idHook, HookProc fn, IntPtr hMod, uint threadId);
        [DllImport("user32.dll")] public static extern bool UnhookWindowsHookEx(IntPtr hhk);
        [DllImport("user32.dll")]
        public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr GetModuleHandle(string name);
        [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint mods, uint vk);
        [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        [DllImport("user32.dll")] public static extern IntPtr MonitorFromPoint(POINT pt, uint flags);
        [DllImport("shcore.dll")] public static extern int GetDpiForMonitor(IntPtr hmon, int type, out uint dpiX, out uint dpiY);
        [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr ctx);
        [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
        [DllImport("user32.dll")] public static extern bool DestroyIcon(IntPtr h);

        public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        public const uint SWP_NOSIZE = 0x1, SWP_NOACTIVATE = 0x10;
    }

    // ---------------------------------------------------------------- Einstellungen
    class Settings
    {
        public Color Color = Color.FromArgb(255, 214, 0);
        public int Radius = 40;        // Pixel (bei 100 % Skalierung)
        public int FillOpacity = 35;   // Prozent
        public int RingWidth = 3;      // Pixel
        public bool Enabled = true;
        public bool ClickRipple = true;     // Wellen bei Linksklick
        public int RippleWaves = 3;         // Anzahl Wellen pro Klick
        public int RippleDuration = 700;    // Millisekunden
        public int RippleWidth = 3;         // Linienstärke in Pixel
        public bool RippleOwnColor = false; // false = Kreisfarbe verwenden
        public Color RippleColor = Color.White;
        public bool FirstRun = true;

        static bool TryColor(string v, out Color c)
        {
            int rgb;
            c = Color.Empty;
            if (!int.TryParse(v.TrimStart('#'), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out rgb)) return false;
            c = Color.FromArgb(255, Color.FromArgb(rgb));
            return true;
        }

        static string Hex(Color c) { return "#" + (c.ToArgb() & 0xFFFFFF).ToString("X6"); }
        static bool Bool(string v) { return v == "1" || v.ToLowerInvariant() == "true"; }

        static string FilePath
        {
            get
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "MausHighlighter", "settings.ini");
            }
        }

        static int Clamp(int v, int min, int max) { return Math.Max(min, Math.Min(max, v)); }

        public static Settings Load()
        {
            var s = new Settings();
            try
            {
                if (!File.Exists(FilePath)) return s;
                s.FirstRun = false;
                foreach (var raw in File.ReadAllLines(FilePath))
                {
                    int eq = raw.IndexOf('=');
                    if (eq < 0) continue;
                    string k = raw.Substring(0, eq).Trim().ToLowerInvariant(), v = raw.Substring(eq + 1).Trim();
                    int n;
                    Color col;
                    switch (k)
                    {
                        case "color": if (TryColor(v, out col)) s.Color = col; break;
                        case "ripplecolor": if (TryColor(v, out col)) s.RippleColor = col; break;
                        case "rippleowncolor": s.RippleOwnColor = Bool(v); break;
                        case "ripplewaves": if (int.TryParse(v, out n)) s.RippleWaves = Clamp(n, 1, 6); break;
                        case "rippleduration": if (int.TryParse(v, out n)) s.RippleDuration = Clamp(n, 200, 2000); break;
                        case "ripplewidth": if (int.TryParse(v, out n)) s.RippleWidth = Clamp(n, 1, 12); break;
                        case "radius": if (int.TryParse(v, out n)) s.Radius = Clamp(n, 5, 300); break;
                        case "fillopacity": if (int.TryParse(v, out n)) s.FillOpacity = Clamp(n, 0, 100); break;
                        case "ringwidth": if (int.TryParse(v, out n)) s.RingWidth = Clamp(n, 0, 30); break;
                        case "enabled": s.Enabled = v == "1" || v.ToLowerInvariant() == "true"; break;
                        case "clickripple": s.ClickRipple = v == "1" || v.ToLowerInvariant() == "true"; break;
                    }
                }
            }
            catch { }
            return s;
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                var sb = new StringBuilder();
                sb.AppendLine("color=" + Hex(Color));
                sb.AppendLine("radius=" + Radius);
                sb.AppendLine("fillopacity=" + FillOpacity);
                sb.AppendLine("ringwidth=" + RingWidth);
                sb.AppendLine("enabled=" + (Enabled ? "1" : "0"));
                sb.AppendLine("clickripple=" + (ClickRipple ? "1" : "0"));
                sb.AppendLine("ripplewaves=" + RippleWaves);
                sb.AppendLine("rippleduration=" + RippleDuration);
                sb.AppendLine("ripplewidth=" + RippleWidth);
                sb.AppendLine("rippleowncolor=" + (RippleOwnColor ? "1" : "0"));
                sb.AppendLine("ripplecolor=" + Hex(RippleColor));
                File.WriteAllText(FilePath, sb.ToString());
                FirstRun = false;
            }
            catch { }
        }
    }

    // ---------------------------------------------------------------- Basis: transparentes Fenster
    // Durchklickbar, immer im Vordergrund, mit Pro-Pixel-Transparenz. Positioniert in virtuellen
    // Bildschirmkoordinaten, funktioniert daher auf allen Monitoren.
    class LayeredWindow : Form
    {
        public LayeredWindow()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            Text = "Maus-Highlighter";
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                // LAYERED | TRANSPARENT (Klicks gehen durch) | TOOLWINDOW (kein Alt+Tab) | TOPMOST | NOACTIVATE
                cp.ExStyle |= 0x80000 | 0x20 | 0x80 | 0x8 | 0x8000000;
                return cp;
            }
        }

        protected override bool ShowWithoutActivation { get { return true; } }
        protected override void OnPaintBackground(PaintEventArgs e) { }
        protected override void OnPaint(PaintEventArgs e) { }

        public static double ScaleAt(Native.POINT p)
        {
            try
            {
                IntPtr mon = Native.MonitorFromPoint(p, 2); // MONITOR_DEFAULTTONEAREST
                uint dx, dy;
                if (Native.GetDpiForMonitor(mon, 0, out dx, out dy) == 0 && dx > 0) return dx / 96.0;
            }
            catch { }
            return 1.0;
        }

        protected void SetBitmap(Bitmap bmp, int x, int y)
        {
            IntPtr screenDc = Native.GetDC(IntPtr.Zero);
            IntPtr memDc = Native.CreateCompatibleDC(screenDc);
            IntPtr hBmp = IntPtr.Zero, old = IntPtr.Zero;
            try
            {
                hBmp = bmp.GetHbitmap(Color.FromArgb(0)); // vormultipliziertes Alpha
                old = Native.SelectObject(memDc, hBmp);
                var sz = new Native.SIZE { cx = bmp.Width, cy = bmp.Height };
                var src = new Native.POINT();
                var dst = new Native.POINT { X = x, Y = y };
                var blend = new Native.BLENDFUNCTION { BlendOp = 0, BlendFlags = 0, SourceConstantAlpha = 255, AlphaFormat = 1 };
                Native.UpdateLayeredWindow(Handle, screenDc, ref dst, ref sz, memDc, ref src, 0, ref blend, 2);
            }
            finally
            {
                Native.ReleaseDC(IntPtr.Zero, screenDc);
                if (hBmp != IntPtr.Zero)
                {
                    Native.SelectObject(memDc, old);
                    Native.DeleteObject(hBmp);
                }
                Native.DeleteDC(memDc);
            }
        }
    }

    // ---------------------------------------------------------------- Kreis-Overlay (folgt der Maus)
    // Zeichnet den Kreis und nach einem Linksklick die Wellen ("Stein ins Wasser"),
    // die von der Mitte nach außen laufen und nur innerhalb des Kreises sichtbar sind.
    class Overlay : LayeredWindow
    {
        public event EventHandler HotkeyPressed;
        readonly Settings s;
        int size;
        double renderedScale = -1;
        Native.POINT lastPos;
        bool hasPos;
        readonly System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();
        readonly List<double> clicks = new List<double>();   // Startzeitpunkte (ms) laufender Wellen
        readonly System.Windows.Forms.Timer anim = new System.Windows.Forms.Timer { Interval = 15 };

        public Overlay(Settings settings)
        {
            s = settings;
            anim.Tick += delegate { Animate(); };
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x0312 && HotkeyPressed != null) HotkeyPressed(this, EventArgs.Empty); // WM_HOTKEY
            base.WndProc(ref m);
        }

        public void AddRipple()
        {
            if (!s.Enabled || !s.ClickRipple || !Visible) return;
            clicks.Add(clock.Elapsed.TotalMilliseconds);
            anim.Start();
            Animate();
        }

        void Animate()
        {
            double now = clock.Elapsed.TotalMilliseconds;
            clicks.RemoveAll(t0 => now - t0 > s.RippleDuration);
            if (clicks.Count == 0) anim.Stop();
            if (hasPos && Visible) Render(ScaleAt(lastPos), lastPos); // letzter Frame ohne Wellen räumt auf
        }

        public void Follow(Native.POINT p, bool force)
        {
            if (!s.Enabled || !Visible) return;
            double scale = ScaleAt(p);
            if (force || scale != renderedScale || clicks.Count > 0)
                Render(scale, p);
            else if (!hasPos || p.X != lastPos.X || p.Y != lastPos.Y)
                Native.SetWindowPos(Handle, Native.HWND_TOPMOST, p.X - size / 2, p.Y - size / 2, 0, 0,
                    Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
            lastPos = p;
            hasPos = true;
        }

        void Render(double scale, Native.POINT p)
        {
            int r = Math.Max(1, (int)Math.Round(s.Radius * scale));
            int ring = (int)Math.Round(s.RingWidth * scale);
            if (s.RingWidth > 0 && ring < 1) ring = 1;
            int sz = 2 * r + ring + 6;
            using (var bmp = new Bitmap(sz, sz, PixelFormat.Format32bppArgb))
            {
                using (var g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.Clear(Color.Transparent);
                    float c = sz / 2f;
                    if (s.FillOpacity > 0)
                        using (var b = new SolidBrush(Color.FromArgb(s.FillOpacity * 255 / 100, s.Color)))
                            g.FillEllipse(b, c - r, c - r, 2 * r, 2 * r);
                    if (clicks.Count > 0)
                        DrawRipples(g, c, r, ring, scale);
                    if (ring > 0)
                        using (var pen = new Pen(Color.FromArgb(230, s.Color), ring))
                            g.DrawEllipse(pen, c - r, c - r, 2 * r, 2 * r);
                }
                SetBitmap(bmp, p.X - sz / 2, p.Y - sz / 2);
            }
            size = sz;
            renderedScale = scale;
            Native.SetWindowPos(Handle, Native.HWND_TOPMOST, p.X - sz / 2, p.Y - sz / 2, 0, 0,
                Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
        }

        void DrawRipples(Graphics g, float c, int r, int ring, double scale)
        {
            double now = clock.Elapsed.TotalMilliseconds;
            int waves = Math.Max(1, s.RippleWaves);
            double total = Math.Max(50, s.RippleDuration);
            double waveDur = waves > 1 ? total * 0.65 : total;          // Laufzeit einer einzelnen Welle
            double delay = waves > 1 ? (total - waveDur) / (waves - 1) : 0;
            float lw = (float)Math.Max(1, s.RippleWidth * scale);
            float endR = r - ring / 2f - lw / 2f;                       // Wellen enden am Kreisrand
            if (endR <= 1) return;
            float startR = Math.Min(endR, (float)(2 * scale));
            Color col = s.RippleOwnColor ? s.RippleColor : s.Color;

            using (var clip = new GraphicsPath())
            {
                clip.AddEllipse(c - r, c - r, 2 * r, 2 * r);            // nichts außerhalb des Kreises zeichnen
                g.SetClip(clip);
                foreach (double t0 in clicks)
                {
                    for (int i = 0; i < waves; i++)
                    {
                        double t = (now - t0 - i * delay) / waveDur;
                        if (t < 0 || t > 1) continue;
                        double ease = 1 - Math.Pow(1 - t, 2.5);        // schnell los, sanft auslaufen
                        float rr = (float)(startR + (endR - startR) * ease);
                        int alpha = (int)(245 * Math.Pow(1 - t, 1.3));
                        float w = Math.Max(1f, (float)(lw * (1.2 - 0.6 * t)));
                        using (var pen = new Pen(Color.FromArgb(Math.Max(0, Math.Min(255, alpha)), col), w))
                            g.DrawEllipse(pen, c - rr, c - rr, 2 * rr, 2 * rr);
                    }
                }
                g.ResetClip();
            }
        }
    }

    // ---------------------------------------------------------------- Einstellungsfenster
    class SettingsForm : Form
    {
        public event EventHandler SettingsChanged;
        readonly Settings s;
        readonly Button btnColor, btnRippleColor;
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string RunName = "MausHighlighter";

        public SettingsForm(Settings settings)
        {
            s = settings;
            Text = "Maus-Highlighter – Einstellungen";
            // Layout ist für 96 DPI entworfen und wird am Ende passend zur Schriftgröße skaliert
            Font = new Font("Segoe UI", 9f);
            AutoScaleMode = AutoScaleMode.None;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(440, 684);
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { } // App-Icon aus der exe
            TopMost = true;

            // ---- Kreis
            var gCircle = new GroupBox { Text = "Kreis", Location = new Point(10, 8), Size = new Size(420, 282) };
            Controls.Add(gCircle);
            int y = 24;
            gCircle.Controls.Add(new Label { Text = "Farbe", Location = new Point(12, y), AutoSize = true });
            y += 20;
            btnColor = new Button { Location = new Point(12, y), Size = new Size(60, 30), FlatStyle = FlatStyle.Flat, BackColor = s.Color };
            btnColor.Click += delegate { PickColor(); };
            gCircle.Controls.Add(btnColor);

            Color[] presets = { Color.FromArgb(255, 214, 0), Color.FromArgb(255, 60, 60), Color.FromArgb(40, 200, 90),
                                Color.FromArgb(30, 140, 255), Color.FromArgb(255, 60, 200), Color.White };
            int x = 88;
            foreach (var pc in presets)
            {
                var col = pc;
                var b = new Button { Location = new Point(x, y + 3), Size = new Size(24, 24), FlatStyle = FlatStyle.Flat, BackColor = col };
                b.FlatAppearance.BorderColor = Color.Gray;
                b.Click += delegate { SetColor(col); };
                gCircle.Controls.Add(b);
                x += 30;
            }
            var more = new Button { Text = "Weitere…", Location = new Point(x + 6, y + 2), Size = new Size(80, 27) };
            more.Click += delegate { PickColor(); };
            gCircle.Controls.Add(more);
            y += 44;

            AddSlider(gCircle, "Radius (Pixel)", 5, 300, s.Radius, v => { s.Radius = v; Changed(); }, ref y);
            AddSlider(gCircle, "Füllung – Deckkraft (%)", 0, 100, s.FillOpacity, v => { s.FillOpacity = v; Changed(); }, ref y);
            AddSlider(gCircle, "Randstärke (Pixel, 0 = kein Rand)", 0, 30, s.RingWidth, v => { s.RingWidth = v; Changed(); }, ref y);

            // ---- Klick-Wellen
            var gRipple = new GroupBox { Text = "Klick-Wellen (nur innerhalb des Kreises)", Location = new Point(10, 300), Size = new Size(420, 282) };
            Controls.Add(gRipple);
            y = 24;
            var cbRipple = new CheckBox { Text = "Wellen bei Linksklick anzeigen", Location = new Point(12, y), AutoSize = true, Checked = s.ClickRipple };
            cbRipple.CheckedChanged += delegate { s.ClickRipple = cbRipple.Checked; Changed(); };
            gRipple.Controls.Add(cbRipple);
            y += 30;

            var cbOwn = new CheckBox { Text = "Eigene Wellenfarbe", Location = new Point(12, y + 4), AutoSize = true, Checked = s.RippleOwnColor };
            btnRippleColor = new Button
            {
                Location = new Point(170, y), Size = new Size(60, 28), FlatStyle = FlatStyle.Flat,
                BackColor = s.RippleColor, Enabled = s.RippleOwnColor
            };
            cbOwn.CheckedChanged += delegate { s.RippleOwnColor = cbOwn.Checked; btnRippleColor.Enabled = cbOwn.Checked; Changed(); };
            btnRippleColor.Click += delegate
            {
                using (var dlg = new ColorDialog { Color = s.RippleColor, FullOpen = true, AnyColor = true })
                    if (dlg.ShowDialog(this) == DialogResult.OK)
                    {
                        s.RippleColor = Color.FromArgb(255, dlg.Color);
                        btnRippleColor.BackColor = s.RippleColor;
                        Changed();
                    }
            };
            gRipple.Controls.Add(cbOwn);
            gRipple.Controls.Add(btnRippleColor);
            y += 40;

            AddSlider(gRipple, "Anzahl Wellen", 1, 6, s.RippleWaves, v => { s.RippleWaves = v; Changed(); }, ref y);
            AddSlider(gRipple, "Dauer (Millisekunden)", 200, 2000, s.RippleDuration, v => { s.RippleDuration = v; Changed(); }, ref y);
            AddSlider(gRipple, "Linienstärke (Pixel)", 1, 12, s.RippleWidth, v => { s.RippleWidth = v; Changed(); }, ref y);

            // ---- Allgemein
            var cbAuto = new CheckBox { Text = "Mit Windows starten", Location = new Point(16, 594), AutoSize = true, Checked = IsAutostart() };
            cbAuto.CheckedChanged += delegate { SetAutostart(cbAuto.Checked); };
            Controls.Add(cbAuto);

            Controls.Add(new Label
            {
                Text = "Ein/Aus: Strg+Alt+H oder Rechtsklick auf das Tray-Symbol.\nTipp: Ein Klick hier im Fenster zeigt die Wellen als Vorschau.",
                Location = new Point(16, 622), AutoSize = true, ForeColor = SystemColors.GrayText
            });

            var close = new Button { Text = "Schließen", Location = new Point(344, 648), Size = new Size(86, 28), DialogResult = DialogResult.OK };
            close.Click += delegate { Close(); };
            Controls.Add(close);
            AcceptButton = close;
            CancelButton = close;

            // Schrift wird mit der System-DPI gerendert – Positionen/Größen im selben Verhältnis mitskalieren
            float f;
            using (var g = Graphics.FromHwnd(IntPtr.Zero)) f = g.DpiY / 96f;
            if (Math.Abs(f - 1f) > 0.01f) Scale(new SizeF(f, f));
            ClientSize = new Size((int)Math.Round(440 * f), (int)Math.Round(690 * f));
            // Titelleiste kann auf Monitoren mit anderer Skalierung höher sein: nach dem Anzeigen nachmessen
            Shown += delegate
            {
                int needW = close.Right + (int)(10 * f), needH = close.Bottom + (int)(10 * f);
                int dw = Math.Max(0, needW - ClientSize.Width), dh = Math.Max(0, needH - ClientSize.Height);
                if (dw > 0 || dh > 0) Size = new Size(Width + dw, Height + dh);
            };
        }

        void AddSlider(Control parent, string label, int min, int max, int value, Action<int> onChange, ref int y)
        {
            value = Math.Max(min, Math.Min(max, value));
            parent.Controls.Add(new Label { Text = label, Location = new Point(12, y), AutoSize = true });
            var tb = new TrackBar
            {
                AutoSize = false, Minimum = min, Maximum = max, Value = value, Location = new Point(6, y + 18), Size = new Size(310, 34),
                TickStyle = TickStyle.None, SmallChange = 1, LargeChange = Math.Max(1, (max - min) / 10)
            };
            var nud = new NumericUpDown { Minimum = min, Maximum = max, Value = value, Location = new Point(322, y + 20), Size = new Size(84, 24) };
            bool sync = false;
            tb.ValueChanged += delegate
            {
                if (sync) return;
                sync = true; nud.Value = tb.Value; sync = false;
                onChange(tb.Value);
            };
            nud.ValueChanged += delegate
            {
                if (sync) return;
                sync = true; tb.Value = (int)nud.Value; sync = false;
                onChange((int)nud.Value);
            };
            parent.Controls.Add(tb);
            parent.Controls.Add(nud);
            y += 62;
        }

        void PickColor()
        {
            using (var dlg = new ColorDialog { Color = s.Color, FullOpen = true, AnyColor = true })
                if (dlg.ShowDialog(this) == DialogResult.OK) SetColor(dlg.Color);
        }

        void SetColor(Color c)
        {
            s.Color = Color.FromArgb(255, c);
            btnColor.BackColor = s.Color;
            Changed();
        }

        void Changed()
        {
            if (SettingsChanged != null) SettingsChanged(this, EventArgs.Empty);
        }

        static bool IsAutostart()
        {
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(RunKey))
                    return k != null && k.GetValue(RunName) != null;
            }
            catch { return false; }
        }

        static void SetAutostart(bool on)
        {
            try
            {
                using (var k = Registry.CurrentUser.CreateSubKey(RunKey))
                {
                    if (on) k.SetValue(RunName, "\"" + Application.ExecutablePath + "\"");
                    else if (k.GetValue(RunName) != null) k.DeleteValue(RunName);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Autostart konnte nicht geändert werden:\n" + ex.Message, "Maus-Highlighter");
            }
        }
    }

    // ---------------------------------------------------------------- App (Tray, Hook, Hotkey)
    class App : ApplicationContext
    {
        readonly Settings s;
        readonly Overlay ov;
        readonly NotifyIcon tray;
        readonly ToolStripMenuItem miEnabled;
        readonly System.Windows.Forms.Timer timer;
        readonly Native.HookProc hookProc; // Referenz halten, sonst räumt der GC den Delegate weg
        IntPtr hook;
        IntPtr iconHandle;
        SettingsForm sf;

        public App()
        {
            s = Settings.Load();
            ov = new Overlay(s);
            IntPtr h = ov.Handle; // Fensterhandle erzeugen
            ov.HotkeyPressed += delegate { SetEnabled(!s.Enabled); };
            Native.RegisterHotKey(h, 1, 0x1 | 0x2 | 0x4000, (uint)Keys.H); // Strg+Alt+H

            var menu = new ContextMenuStrip();
            miEnabled = new ToolStripMenuItem("Highlight aktiv   (Strg+Alt+H)") { Checked = s.Enabled };
            miEnabled.Click += delegate { SetEnabled(!s.Enabled); };
            menu.Items.Add(miEnabled);
            menu.Items.Add("Einstellungen…", null, delegate { ShowSettings(); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Beenden", null, delegate { Quit(); });

            tray = new NotifyIcon { ContextMenuStrip = menu, Text = "Maus-Highlighter", Visible = true };
            tray.DoubleClick += delegate { ShowSettings(); };
            UpdateTrayIcon();

            hookProc = HookCallback;
            hook = Native.SetWindowsHookEx(14, hookProc, Native.GetModuleHandle(null), 0); // WH_MOUSE_LL

            timer = new System.Windows.Forms.Timer { Interval = 15 };
            timer.Tick += delegate
            {
                Native.POINT p;
                if (Native.GetCursorPos(out p)) ov.Follow(p, false);
            };
            timer.Start();

            if (s.Enabled) ShowOverlay();

            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--test-ripple") > 0)
            {
                // Entwickler-Test: nach dem Start eine Welle am Mauszeiger auslösen
                var t = new System.Windows.Forms.Timer { Interval = 1500 };
                t.Tick += delegate { t.Stop(); ov.AddRipple(); };
                t.Start();
            }

            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--settings") > 0)
                ShowSettings();

            if (s.FirstRun)
            {
                s.Save();
                tray.ShowBalloonTip(5000, "Maus-Highlighter läuft",
                    "Rechtsklick auf das Tray-Symbol für Farbe & Radius. Ein/Aus mit Strg+Alt+H.", ToolTipIcon.Info);
            }
        }

        IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && wParam == (IntPtr)0x0200) // WM_MOUSEMOVE
            {
                var info = (Native.MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(Native.MSLLHOOKSTRUCT));
                ov.Follow(info.pt, false);
            }
            else if (nCode >= 0 && wParam == (IntPtr)0x0201 && s.Enabled && s.ClickRipple) // WM_LBUTTONDOWN
            {
                // nicht im Hook selbst zeichnen, sondern direkt danach (Hook muss schnell zurückkehren)
                ov.BeginInvoke((MethodInvoker)ov.AddRipple);
            }
            return Native.CallNextHookEx(hook, nCode, wParam, lParam);
        }

        void ShowOverlay()
        {
            ov.Show();
            Native.POINT p;
            Native.GetCursorPos(out p);
            ov.Follow(p, true);
        }

        void SetEnabled(bool on)
        {
            s.Enabled = on;
            miEnabled.Checked = on;
            if (on) ShowOverlay(); else ov.Hide();
            s.Save();
        }

        void ShowSettings()
        {
            if (sf != null) { sf.Activate(); return; }
            sf = new SettingsForm(s);
            sf.SettingsChanged += delegate
            {
                Native.POINT p;
                Native.GetCursorPos(out p);
                ov.Follow(p, true);
                UpdateTrayIcon();
            };
            sf.FormClosed += delegate { s.Save(); sf = null; };
            sf.Show();
            sf.Activate();
        }

        void UpdateTrayIcon()
        {
            using (var bmp = new Bitmap(32, 32, PixelFormat.Format32bppArgb))
            {
                using (var g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.Clear(Color.Transparent);
                    using (var b = new SolidBrush(Color.FromArgb(150, s.Color))) g.FillEllipse(b, 2, 2, 27, 27);
                    using (var pen = new Pen(s.Color, 3)) g.DrawEllipse(pen, 3, 3, 25, 25);
                    using (var pen = new Pen(Color.FromArgb(60, 60, 60), 1.5f)) g.DrawEllipse(pen, 1, 1, 29, 29);
                    using (var b = new SolidBrush(Color.FromArgb(40, 40, 40))) g.FillEllipse(b, 13, 13, 6, 6);
                }
                IntPtr hIcon = bmp.GetHicon();
                var oldHandle = iconHandle;
                tray.Icon = Icon.FromHandle(hIcon);
                iconHandle = hIcon;
                if (oldHandle != IntPtr.Zero) Native.DestroyIcon(oldHandle);
            }
        }

        void Quit()
        {
            timer.Stop();
            if (hook != IntPtr.Zero) Native.UnhookWindowsHookEx(hook);
            Native.UnregisterHotKey(ov.Handle, 1);
            s.Save();
            tray.Visible = false;
            tray.Dispose();
            if (sf != null) sf.Close();
            ov.Close();
            ExitThread();
        }
    }

    static class Program
    {
        [STAThread]
        static void Main()
        {
            bool created;
            using (new Mutex(true, "MausHighlighter_SingleInstance", out created))
            {
                if (!created) return;
                try { Native.SetProcessDpiAwarenessContext(new IntPtr(-4)); } // Per-Monitor v2
                catch { try { Native.SetProcessDPIAware(); } catch { } }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new App());
            }
        }
    }
}
