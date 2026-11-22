// TibiaSnap - press your hotkey (default End) in Tibia -> Tibia saves its OWN screenshot ->
// TibiaSnap shows it fullscreen so you can drag a box and copy the part you want.
//
// TibiaSnap only LOOKS at whether your hotkey is pressed (GetAsyncKeyState). It does
// not block, change or send any keys, and never reads from or touches the Tibia
// client. It only watches Tibia's screenshot folder for the new file.
//
// Written for the C# 5 compiler that ships with Windows (.NET Framework 4.x).

using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: System.Reflection.AssemblyTitle("TibiaSnap")]
[assembly: System.Reflection.AssemblyProduct("TibiaSnap")]
[assembly: System.Reflection.AssemblyVersion("1.4.3.0")]

namespace TibiaSnap
{
    // ======================================================================
    // What's new - newest first. Shown in a small window after an update.
    // ======================================================================
    static class ChangeLog
    {
        public static readonly string[][] Entries =
        {
            new string[] { "1.4.3",
                "The area you're marking is now always fully solid (100%) - only the rest of the screenshot is see-through.",
                "Fixed the marked area sometimes showing up see-through.",
                "Custom see-through window fixed: text no longer gets cut off on scaled screens." },
            new string[] { "1.4.2",
                "Play mode is now the default: the screenshot opens see-through (40%), the marked area stays 100%, and your keys stay in Tibia. You can switch back to solid under tray > See-through viewer." },
            new string[] { "1.4.1",
                "See-through viewer now lines up exactly with your Tibia game window, so the screenshot sits right on top of the live game (it opens on the monitor Tibia is on)." },
            new string[] { "1.4.0",
                "New: see-through viewer (tray > See-through viewer). Watch your hunt through the screenshot while you crop.",
                "The area you're marking stays fully visible, the rest is see-through. What you copy is always the normal 100% screenshot.",
                "'Play mode' preset: screenshot at 40%, marked area 100%, and your keyboard stays in Tibia so your hotkeys keep working while you crop.",
                "Or set both values yourself under 'Custom...'." },
            new string[] { "1.3.0",
                "Opens a lot faster: no more slow folder scan on every key press, and the screenshot is prepared quicker.",
                "New: choose what happens when you press the key (tray > When I press the key): open the crop window, just copy the whole screenshot to your clipboard, or keep it for later so nothing pops up mid-hunt.",
                "Double-click the tray icon to crop your latest screenshot anytime.",
                "Small messages (like 'Copied') now show as a little box in the corner that never steals focus from Tibia, and can't be hidden by Windows notification settings." },
            new string[] { "1.2.0",
                "New built-in crop: drag a box over the screenshot and it's copied to your clipboard instantly. No more waiting for the Windows Snipping Tool.",
                "Enter or double-click copies the whole screenshot.",
                "Crops are also saved to Pictures\\TibiaSnap (you can turn that off in the tray menu).",
                "Choose which monitor the screenshot opens on (tray menu > Open on monitor).",
                "Much faster: new screenshots are picked up instantly and shown sharper on high-res / scaled screens.",
                "This 'What's new' window after updates :)",
                "\"Check for updates\" now always shows a popup with the result." },
            new string[] { "1.1.0",
                "Fixed the 'no new screenshot' warning showing up even though everything was set up right.",
                "TibiaSnap now updates itself." },
        };

        // all changes newer than 'from' (or just the newest version if from is null)
        public static string TextSince(Version from)
        {
            StringBuilder sb = new StringBuilder();
            Version fromN = from == null ? null : Updater.Normalize(from);
            foreach (string[] e in Entries)
            {
                Version v = Updater.Normalize(new Version(e[0]));
                if (fromN != null && v <= fromN) continue;
                sb.Append("v").Append(e[0]).Append("\r\n");
                for (int i = 1; i < e.Length; i++) sb.Append("  \u2022 ").Append(e[i]).Append("\r\n");
                sb.Append("\r\n");
                if (fromN == null) break;
            }
            return sb.ToString().TrimEnd();
        }

        public static string All()
        {
            return TextSince(new Version(0, 0));
        }
    }

    static class Program
    {
        [DllImport("user32.dll")] static extern bool SetProcessDPIAware();
        [DllImport("shcore.dll")] static extern int SetProcessDpiAwareness(int value);

        [STAThread]
        static void Main(string[] args)
        {
            // real pixels on scaled (125%/150%) screens = sharp image, no Windows stretching
            try { SetProcessDpiAwareness(2); }        // per monitor (Win 8.1+)
            catch { try { SetProcessDPIAware(); } catch { } }

            int ai = Array.IndexOf(args, "--after-update");
            bool afterUpdate = ai >= 0;
            Version updatedFrom = null;
            if (afterUpdate && ai + 1 < args.Length)
            {
                Version v;
                if (Version.TryParse(args[ai + 1], out v)) updatedFrom = v;
            }

            bool created;
            using (Mutex m = new Mutex(true, "TibiaSnap_SingleInstance", out created))
            {
                if (!created)
                {
                    bool got = false;
                    if (afterUpdate)
                    {
                        // the old version is still closing - wait for it
                        try { got = m.WaitOne(15000); }
                        catch (AbandonedMutexException) { got = true; }
                    }
                    if (!got)
                    {
                        MessageBox.Show("TibiaSnap is already running (look in your tray).", "TibiaSnap");
                        return;
                    }
                }
                Updater.CleanupOld();
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new TrayApp(afterUpdate, updatedFrom));
            }
        }
    }

    // ======================================================================
    // Auto-update from GitHub Releases (github.com/AlexFrr/TibiaSnap)
    // ======================================================================
    static class Updater
    {
        public const string Repo = "AlexFrr/TibiaSnap";

        public static Version Current
        {
            get { return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version; }
        }

        public static string CurrentText
        {
            get { Version v = Current; return v.Major + "." + v.Minor + "." + v.Build; }
        }

        static string ExePath { get { return Application.ExecutablePath; } }

        public static void CleanupOld()
        {
            string old = ExePath + ".old";
            for (int i = 0; i < 10 && File.Exists(old); i++)
            {
                try { File.Delete(old); }
                catch { Thread.Sleep(300); }
            }
        }

        public static Version Normalize(Version v)
        {
            return new Version(v.Major, v.Minor, Math.Max(v.Build, 0), Math.Max(v.Revision, 0));
        }

        public static bool TryGetNewer(out Version newVersion, out string url)
        {
            newVersion = null;
            url = null;

            System.Net.ServicePointManager.SecurityProtocol = (System.Net.SecurityProtocolType)3072; // TLS 1.2
            string json;
            using (System.Net.WebClient wc = new System.Net.WebClient())
            {
                wc.Headers.Add("User-Agent", "TibiaSnap-Updater");
                wc.Headers.Add("Accept", "application/vnd.github+json");
                json = wc.DownloadString("https://api.github.com/repos/" + Repo + "/releases/latest");
            }

            Match tag = Regex.Match(json, "\"tag_name\"\\s*:\\s*\"v?([0-9]+(\\.[0-9]+){1,3})\"");
            Match asset = Regex.Match(json, "\"browser_download_url\"\\s*:\\s*\"([^\"]*/TibiaSnap\\.exe)\"", RegexOptions.IgnoreCase);
            if (!tag.Success || !asset.Success) return false;

            Version latest = Normalize(new Version(tag.Groups[1].Value));
            if (latest <= Normalize(Current)) return false;

            newVersion = latest;
            url = asset.Groups[1].Value;
            return true;
        }

        // Downloads the new exe, swaps it in and starts it. Caller exits the app after.
        public static void DownloadAndSwap(string url)
        {
            string exe = ExePath;
            string tmp = exe + ".new";
            string old = exe + ".old";

            System.Net.ServicePointManager.SecurityProtocol = (System.Net.SecurityProtocolType)3072;
            using (System.Net.WebClient wc = new System.Net.WebClient())
            {
                wc.Headers.Add("User-Agent", "TibiaSnap-Updater");
                wc.DownloadFile(url, tmp);
            }

            byte[] head = new byte[2];
            long len;
            using (FileStream fs = File.OpenRead(tmp))
            {
                len = fs.Length;
                fs.Read(head, 0, 2);
            }
            if (len < 10000 || head[0] != 'M' || head[1] != 'Z')
            {
                File.Delete(tmp);
                throw new Exception("downloaded file doesn't look like TibiaSnap.exe");
            }

            if (File.Exists(old)) File.Delete(old);
            File.Move(exe, old);      // Windows allows renaming a running exe
            try { File.Move(tmp, exe); }
            catch { File.Move(old, exe); throw; }

            Process.Start(exe, "--after-update " + CurrentText);
        }
    }

    // ======================================================================
    // Settings (%APPDATA%\TibiaSnap\settings.ini)
    // ======================================================================
    class Settings
    {
        public string Folder;
        public bool Enabled = true;
        public bool OwnViewer = true;
        public string CropMode = "builtin";   // builtin | snip | none
        public bool SaveCrops = true;
        public string OnPress = "viewer";     // viewer | copy | later
        public int ViewOpacity = 40;          // see-through viewer: whole screenshot %  (default = play mode)
        public int SelOpacity = 100;          // marked area %
        public bool KeepFocus = true;         // keyboard stays in Tibia while cropping
        public bool PlayDefaultApplied = false;
        public string Monitor = "mouse";      // mouse | primary | \\.\DISPLAYn
        public Keys HotkeyKey = Keys.End;
        public Keys HotkeyMods = Keys.None;

        public string HotkeyName
        {
            get { return new KeysConverter().ConvertToString(HotkeyKey | HotkeyMods); }
        }

        static string SettingsDir
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TibiaSnap"); }
        }
        static string SettingsFile
        {
            get { return Path.Combine(SettingsDir, "settings.ini"); }
        }
        public static string DefaultFolder
        {
            get
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    @"Tibia\packages\Tibia\screenshots");
            }
        }
        public static string CropsFolder
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "TibiaSnap"); }
        }

        public static Settings Load()
        {
            Settings s = new Settings();
            s.Folder = DefaultFolder;
            bool hasCropMode = false;
            bool legacyNoSnip = false;
            try
            {
                if (File.Exists(SettingsFile))
                {
                    foreach (string line in File.ReadAllLines(SettingsFile))
                    {
                        int i = line.IndexOf('=');
                        if (i <= 0) continue;
                        string key = line.Substring(0, i).Trim();
                        string val = line.Substring(i + 1).Trim();
                        int n;
                        if (key == "Folder" && val.Length > 0) s.Folder = val;
                        else if (key == "Enabled") s.Enabled = val == "1";
                        else if (key == "OwnViewer") s.OwnViewer = val == "1";
                        else if (key == "CropMode" && (val == "builtin" || val == "snip" || val == "none")) { s.CropMode = val; hasCropMode = true; }
                        else if (key == "AutoSnip") legacyNoSnip = val == "0";
                        else if (key == "SaveCrops") s.SaveCrops = val == "1";
                        else if (key == "OnPress" && (val == "viewer" || val == "copy" || val == "later")) s.OnPress = val;
                        else if (key == "ViewOpacity") { if (int.TryParse(val, out n)) s.ViewOpacity = Math.Max(10, Math.Min(100, n)); }
                        else if (key == "SelOpacity") { } // marked area is always 100% now
                        else if (key == "KeepFocus") s.KeepFocus = val == "1";
                        else if (key == "PlayDefaultApplied") s.PlayDefaultApplied = val == "1";
                        else if (key == "Monitor" && val.Length > 0) s.Monitor = val;
                        else if (key == "HotkeyKey") { if (int.TryParse(val, out n) && n > 0) s.HotkeyKey = (Keys)n; }
                        else if (key == "HotkeyMods") { if (int.TryParse(val, out n)) s.HotkeyMods = (Keys)n & Keys.Modifiers; }
                    }
                }
            }
            catch { }
            if (!hasCropMode && legacyNoSnip) s.CropMode = "none";
            // one time: older versions saved 'solid' as the default -> switch to play mode
            // (custom values someone picked themselves are kept)
            if (!s.PlayDefaultApplied)
            {
                if (s.ViewOpacity == 100 && s.SelOpacity == 100 && !s.KeepFocus)
                {
                    s.ViewOpacity = 40;
                    s.SelOpacity = 100;
                    s.KeepFocus = true;
                }
                s.PlayDefaultApplied = true;
                s.Save();
            }
            return s;
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(SettingsDir);
                File.WriteAllLines(SettingsFile, new string[] {
                    "Folder=" + Folder,
                    "Enabled=" + (Enabled ? "1" : "0"),
                    "OwnViewer=" + (OwnViewer ? "1" : "0"),
                    "CropMode=" + CropMode,
                    "SaveCrops=" + (SaveCrops ? "1" : "0"),
                    "OnPress=" + OnPress,
                    "ViewOpacity=" + ViewOpacity,
                    "SelOpacity=" + SelOpacity,
                    "KeepFocus=" + (KeepFocus ? "1" : "0"),
                    "PlayDefaultApplied=" + (PlayDefaultApplied ? "1" : "0"),
                    "Monitor=" + Monitor,
                    "HotkeyKey=" + (int)HotkeyKey,
                    "HotkeyMods=" + (int)HotkeyMods
                });
            }
            catch { }
        }
    }

    // ======================================================================
    // Screenshot loaded + pre-scaled to the target monitor (done off the UI thread)
    // ======================================================================
    class PreparedImage : IDisposable
    {
        public Bitmap Original;      // full resolution, used for cropping
        public Bitmap Scaled;        // already fitted to the screen, so drawing is instant
        public Rectangle ScreenBounds;
        public Rectangle ImageRect;  // where Scaled sits inside the window
        public float ScaleX, ScaleY;
        public string Path;

        // target = where to put the image inside 'screen' (e.g. exactly over Tibia's game area);
        // Rectangle.Empty = fit to the whole screen
        public static PreparedImage Load(string path, Rectangle screen, Rectangle target)
        {
            Bitmap orig = null;
            try
            {
                orig = LoadOriginal(path);

                int w, h;
                Rectangle imageRect;
                if (target.Width > 0 && target.Height > 0)
                {
                    w = target.Width;
                    h = target.Height;
                    imageRect = target;
                }
                else
                {
                    float fit = Math.Min((float)screen.Width / orig.Width, (float)screen.Height / orig.Height);
                    w = Math.Max(1, (int)Math.Round(orig.Width * fit));
                    h = Math.Max(1, (int)Math.Round(orig.Height * fit));
                    imageRect = new Rectangle((screen.Width - w) / 2, (screen.Height - h) / 2, w, h);
                }

                Bitmap scaled = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
                using (Graphics g = Graphics.FromImage(scaled))
                using (ImageAttributes ia = new ImageAttributes())
                {
                    g.InterpolationMode = (w == orig.Width && h == orig.Height) ? InterpolationMode.NearestNeighbor
                        : InterpolationMode.Bilinear; // fast; crops still come from the full-res original
                    g.PixelOffsetMode = PixelOffsetMode.HighSpeed;
                    g.CompositingMode = CompositingMode.SourceCopy;
                    ia.SetWrapMode(WrapMode.TileFlipXY); // no dark edges
                    g.DrawImage(orig, new Rectangle(0, 0, w, h), 0, 0, orig.Width, orig.Height, GraphicsUnit.Pixel, ia);
                }

                PreparedImage p = new PreparedImage();
                p.Original = orig;
                p.Scaled = scaled;
                p.ScaleX = (float)w / orig.Width;
                p.ScaleY = (float)h / orig.Height;
                p.Path = path;
                p.ScreenBounds = screen;
                p.ImageRect = imageRect;
                return p;
            }
            catch
            {
                if (orig != null) orig.Dispose();
                throw;
            }
        }

        // full decode into memory, so the file isn't locked; throws while Tibia is still writing
        public static Bitmap LoadOriginal(string path)
        {
            using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (Image tmp = Image.FromStream(fs, false, true))
                return new Bitmap(tmp);
        }

        public void Dispose()
        {
            if (Original != null) { Original.Dispose(); Original = null; }
            if (Scaled != null) { Scaled.Dispose(); Scaled = null; }
        }
    }

    // ======================================================================
    // Fullscreen viewer with built-in crop
    //   drag = copy that part, Enter / double-click = copy whole,
    //   Esc / right-click / middle-click = close
    // ======================================================================
    class ViewerForm : Form
    {
        readonly PreparedImage img;
        readonly bool cropEnabled;
        readonly bool showHint;
        readonly bool saveCrops;
        readonly Action<string> notify;
        readonly bool keepFocus;   // keyboard stays in Tibia while the viewer is open
        readonly bool seeThrough;  // some transparency is on
        readonly int selOpacity;
        SelWindow selWin;          // solid copy of the marked area, on top of the see-through viewer

        bool dragging;
        bool hintVisible = true;
        Point dragStart;
        Rectangle sel = Rectangle.Empty;

        static readonly Font HintFont = new Font("Segoe UI", 10f);
        static readonly Font SizeFont = new Font("Segoe UI", 9f, FontStyle.Bold);
        static readonly SolidBrush Shade = new SolidBrush(Color.FromArgb(140, 0, 0, 0));
        static readonly SolidBrush PillBrush = new SolidBrush(Color.FromArgb(190, 20, 20, 24));
        static readonly Pen SelPen = new Pen(Color.FromArgb(255, 232, 178, 54), 2f);

        const int WS_EX_NOACTIVATE = 0x08000000;

        protected override bool ShowWithoutActivation { get { return keepFocus; } }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                if (keepFocus) cp.ExStyle |= WS_EX_NOACTIVATE;
                return cp;
            }
        }

        public ViewerForm(PreparedImage img, string cropMode, bool saveCrops, Action<string> notify,
                          int viewOpacity, int selOpacity, bool keepFocus)
        {
            this.img = img;
            this.cropEnabled = cropMode == "builtin";
            this.showHint = cropMode != "snip"; // hint would end up in the Snipping Tool shot
            this.saveCrops = saveCrops;
            this.notify = notify;
            this.keepFocus = keepFocus;
            this.selOpacity = selOpacity;
            this.seeThrough = viewOpacity < 100 || selOpacity < 100;
            if (viewOpacity < 100) Opacity = viewOpacity / 100.0;

            Text = "TibiaSnap - " + System.IO.Path.GetFileName(img.Path);
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            Bounds = img.ScreenBounds;
            BackColor = Color.Black;
            TopMost = true;
            KeyPreview = true;
            ShowInTaskbar = false;
            Cursor = cropEnabled ? Cursors.Cross : Cursors.Default;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.Opaque, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Rectangle ir = img.ImageRect;

            // black bars + image (straight copy, already scaled)
            g.CompositingMode = CompositingMode.SourceCopy;
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            using (Region bars = new Region(ClientRectangle))
            {
                bars.Exclude(ir);
                g.FillRegion(Brushes.Black, bars);
            }
            g.DrawImage(img.Scaled, ir);

            g.CompositingMode = CompositingMode.SourceOver;
            g.PixelOffsetMode = PixelOffsetMode.Default;

            if (dragging && sel.Width > 0 && sel.Height > 0)
            {
                if (!seeThrough)
                {
                    // solid mode: darken everything outside the box
                    int W = ClientSize.Width, H = ClientSize.Height;
                    g.FillRectangle(Shade, 0, 0, W, sel.Top);
                    g.FillRectangle(Shade, 0, sel.Bottom, W, H - sel.Bottom);
                    g.FillRectangle(Shade, 0, sel.Top, sel.Left, sel.Height);
                    g.FillRectangle(Shade, sel.Right, sel.Top, W - sel.Right, sel.Height);
                    g.DrawRectangle(SelPen, sel.X, sel.Y, sel.Width - 1, sel.Height - 1);
                }
                // (see-through mode: the solid SelWindow on top shows the box + border)

                Rectangle src = ToSource(sel);
                DrawPill(g, src.Width + " x " + src.Height, SizeFont, new Point(sel.Left, sel.Bottom + 6), false);
            }
            else if (showHint && hintVisible)
            {
                string close = keepFocus ? "Right-click = close" : "Esc = close";
                string whole = keepFocus ? "Double-click = whole screenshot" : "Enter = whole screenshot";
                string hint = cropEnabled
                    ? "Drag to copy a part   \u00B7   " + whole + "   \u00B7   " + close
                    : (keepFocus ? "Double-click = copy screenshot" : "Enter = copy screenshot") + "   \u00B7   " + close;
                DrawPill(g, hint, HintFont, new Point(ClientSize.Width / 2, 18), true);
            }
        }

        void DrawPill(Graphics g, string text, Font font, Point at, bool centered)
        {
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            SizeF sz = g.MeasureString(text, font);
            float x = centered ? at.X - sz.Width / 2 - 10 : at.X;
            RectangleF r = new RectangleF(x, at.Y, sz.Width + 20, sz.Height + 8);
            g.FillRectangle(PillBrush, r);
            g.DrawString(text, font, Brushes.White, r.X + 10, r.Y + 4);
        }

        // screen selection -> pixels in the full-res screenshot
        Rectangle ToSource(Rectangle s)
        {
            Rectangle ir = img.ImageRect;
            int x1 = (int)Math.Floor((s.Left - ir.X) / img.ScaleX);
            int y1 = (int)Math.Floor((s.Top - ir.Y) / img.ScaleY);
            int x2 = (int)Math.Ceiling((s.Right - ir.X) / img.ScaleX);
            int y2 = (int)Math.Ceiling((s.Bottom - ir.Y) / img.ScaleY);
            x1 = Math.Max(0, Math.Min(x1, img.Original.Width));
            y1 = Math.Max(0, Math.Min(y1, img.Original.Height));
            x2 = Math.Max(0, Math.Min(x2, img.Original.Width));
            y2 = Math.Max(0, Math.Min(y2, img.Original.Height));
            return Rectangle.FromLTRB(x1, y1, x2, y2);
        }

        Rectangle MakeSel(Point a, Point b)
        {
            Rectangle r = Rectangle.FromLTRB(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Max(a.X, b.X), Math.Max(a.Y, b.Y));
            return Rectangle.Intersect(r, img.ImageRect);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Right || e.Button == MouseButtons.Middle) { Close(); return; }
            if (e.Button == MouseButtons.Left && cropEnabled && e.Clicks == 1)
            {
                dragging = true;
                hintVisible = false;
                dragStart = e.Location;
                sel = Rectangle.Empty;
                if (seeThrough && selWin == null)
                {
                    selWin = new SelWindow(img, selOpacity);
                    selWin.Owner = this; // always stays above the viewer
                }
                Invalidate(); // whole screen changes once (shade / hint gone)
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!dragging) return;
            Rectangle old = sel;
            sel = MakeSel(dragStart, e.Location);
            if (selWin != null)
                selWin.Place(sel, new Rectangle(Left + sel.X, Top + sel.Y, sel.Width, sel.Height));
            // only repaint the area that changed (+ room for the size label)
            Rectangle u = old.IsEmpty ? sel : sel.IsEmpty ? old : Rectangle.Union(old, sel);
            Invalidate(new Rectangle(u.X - 4, u.Y - 4, u.Width + 160, u.Height + 50));
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button != MouseButtons.Left || !dragging) return;
            dragging = false;
            if (selWin != null) selWin.Hide();
            if (sel.Width >= 4 && sel.Height >= 4)
            {
                Rectangle src = ToSource(sel);
                if (src.Width > 0 && src.Height > 0)
                {
                    Finish(img.Original.Clone(src, PixelFormat.Format32bppArgb));
                    return;
                }
            }
            sel = Rectangle.Empty;
            Invalidate();
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            if (e.Button == MouseButtons.Left) CopyWhole();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Escape) Close();
            else if (e.KeyCode == Keys.Enter || (e.Control && e.KeyCode == Keys.C)) CopyWhole();
        }

        void CopyWhole()
        {
            dragging = false;
            Finish(new Bitmap(img.Original));
        }

        // clipboard (as image + PNG for Discord etc.), then save in the background
        void Finish(Bitmap result)
        {
            if (selWin != null) selWin.Hide();
            Hide(); // get out of your way right away, copying can take a moment for big shots
            bool copied = ClipboardHelper.SetImage(result);
            Close();

            bool save = saveCrops;
            Action<string> n = notify;
            ThreadPool.QueueUserWorkItem(_ =>
            {
                string msg = copied ? "Copied to clipboard" : "Couldn't use the clipboard";
                try
                {
                    if (save)
                    {
                        Directory.CreateDirectory(Settings.CropsFolder);
                        string file = System.IO.Path.Combine(Settings.CropsFolder,
                            "TibiaSnap_" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss-fff") + ".png");
                        result.Save(file, ImageFormat.Png);
                        msg += copied ? " + saved." : ", but saved it to Pictures\\TibiaSnap.";
                    }
                    else msg += ".";
                }
                catch (Exception ex) { msg += ". (Saving failed: " + ex.Message + ")"; }
                finally { result.Dispose(); }
                if (n != null) n(msg);
            });
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            if (selWin != null && !selWin.IsDisposed) selWin.Close();
            img.Dispose();
            base.OnFormClosed(e);
        }
    }

    // The marked area, drawn in its own window so it can be less transparent (or solid)
    // than the rest of the see-through viewer. Click-through and never takes focus.
    class SelWindow : Form
    {
        const int WS_EX_TRANSPARENT = 0x20, WS_EX_TOOLWINDOW = 0x80, WS_EX_NOACTIVATE = 0x08000000;
        readonly PreparedImage img;
        Rectangle sel;
        static readonly Pen Border = new Pen(Color.FromArgb(255, 232, 178, 54), 2f);

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | 0x8 /*WS_EX_TOPMOST*/;
                return cp;
            }
        }

        public SelWindow(PreparedImage img, int opacityPct)
        {
            this.img = img;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            BackColor = Color.Black;
            MinimumSize = new Size(1, 1);
            if (opacityPct < 100) Opacity = opacityPct / 100.0;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.Opaque, true);
        }

        [DllImport("user32.dll")]
        static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
        static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        const uint SWP_NOACTIVATE = 0x0010, SWP_SHOWWINDOW = 0x0040;

        // selClient = box inside the viewer, screenRect = same box in screen coordinates
        public void Place(Rectangle selClient, Rectangle screenRect)
        {
            sel = selClient;
            if (screenRect.Width < 2 || screenRect.Height < 2)
            {
                if (Visible) Hide();
                return;
            }
            if (!Visible)
            {
                Bounds = screenRect;
                Show();
            }
            // move + force it above the see-through viewer every time (topmost, no focus)
            SetWindowPos(Handle, HWND_TOPMOST, screenRect.X, screenRect.Y, screenRect.Width, screenRect.Height,
                SWP_NOACTIVATE | SWP_SHOWWINDOW);
            Invalidate();
            Update(); // paint now, not "sometime later"
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Rectangle ir = img.ImageRect;
            g.CompositingMode = CompositingMode.SourceCopy;
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            g.DrawImage(img.Scaled, new Rectangle(0, 0, sel.Width, sel.Height),
                new Rectangle(sel.X - ir.X, sel.Y - ir.Y, sel.Width, sel.Height), GraphicsUnit.Pixel);
            g.CompositingMode = CompositingMode.SourceOver;
            g.PixelOffsetMode = PixelOffsetMode.Default;
            g.DrawRectangle(Border, 1, 1, ClientSize.Width - 2, ClientSize.Height - 2);
        }
    }

    // ======================================================================
    // Custom see-through settings
    // ======================================================================
    class SeeThroughForm : Form
    {
        public int ViewOpacity;
        public bool KeepFocus;
        readonly TrackBar viewBar;
        readonly Label viewLbl;
        readonly CheckBox focusBox;

        public SeeThroughForm(int view, bool keepFocus)
        {
            Text = "TibiaSnap - See-through viewer";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            TopMost = true;
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = new Font("Segoe UI", 9.5f);
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;

            // everything stacked top to bottom and sized by Windows -> nothing can overlap
            TableLayoutPanel t = new TableLayoutPanel();
            t.ColumnCount = 1;
            t.AutoSize = true;
            t.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            t.Padding = new Padding(14);
            t.Location = new Point(0, 0);

            Label info = new Label();
            info.AutoSize = true;
            info.MaximumSize = new Size(440, 0);
            info.Margin = new Padding(3, 3, 3, 12);
            info.Text = "How visible the screenshot is. Lower = you see more of Tibia behind it.\n" +
                        "The area you're marking is always shown at 100%, and what you copy is always the normal screenshot.";
            t.Controls.Add(info);

            viewLbl = new Label();
            viewLbl.AutoSize = true;
            viewLbl.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
            t.Controls.Add(viewLbl);

            viewBar = new TrackBar();
            viewBar.Minimum = 2;   // 10%
            viewBar.Maximum = 20;  // 100%
            viewBar.TickFrequency = 2;
            viewBar.SmallChange = 1;
            viewBar.LargeChange = 2;
            viewBar.Value = Math.Max(2, Math.Min(20, (int)Math.Round(view / 5.0)));
            viewBar.Width = 440;
            viewBar.Margin = new Padding(3, 3, 3, 10);
            viewBar.ValueChanged += (s, e) => UpdateLabel();
            t.Controls.Add(viewBar);

            focusBox = new CheckBox();
            focusBox.AutoSize = true;
            focusBox.Text = "Keep my keyboard in Tibia while cropping (hotkeys keep working)";
            focusBox.Checked = keepFocus;
            focusBox.Margin = new Padding(3, 3, 3, 14);
            t.Controls.Add(focusBox);

            FlowLayoutPanel buttons = new FlowLayoutPanel();
            buttons.FlowDirection = FlowDirection.RightToLeft;
            buttons.AutoSize = true;
            buttons.Dock = DockStyle.Fill;
            buttons.Margin = new Padding(0);

            Button cancel = new Button();
            cancel.Text = "Cancel";
            cancel.AutoSize = true;
            cancel.MinimumSize = new Size(90, 30);
            cancel.DialogResult = DialogResult.Cancel;
            buttons.Controls.Add(cancel);

            Button ok = new Button();
            ok.Text = "Save";
            ok.AutoSize = true;
            ok.MinimumSize = new Size(90, 30);
            ok.DialogResult = DialogResult.OK;
            buttons.Controls.Add(ok);

            t.Controls.Add(buttons);
            Controls.Add(t);

            AcceptButton = ok;
            CancelButton = cancel;
            UpdateLabel();

            FormClosing += (s, e) =>
            {
                ViewOpacity = viewBar.Value * 5;
                KeepFocus = focusBox.Checked;
            };
        }

        void UpdateLabel()
        {
            viewLbl.Text = "Screenshot visibility: " + (viewBar.Value * 5) + "%";
        }
    }

    static class ClipboardHelper
    {
        // as a normal image + PNG (Discord etc. keep full quality). Must run on the UI thread.
        public static bool SetImage(Bitmap img)
        {
            for (int i = 0; i < 3; i++)
            {
                try
                {
                    DataObject d = new DataObject();
                    d.SetImage(img);
                    MemoryStream png = new MemoryStream();
                    img.Save(png, ImageFormat.Png);
                    d.SetData("PNG", false, png);
                    Clipboard.SetDataObject(d, true);
                    return true;
                }
                catch { Thread.Sleep(60); } // clipboard busy
            }
            return false;
        }
    }

    // ======================================================================
    // Small message box in the corner. Never takes focus away from Tibia and
    // can't be hidden by Windows notification settings / Do Not Disturb.
    // ======================================================================
    class Toast : Form
    {
        const int WS_EX_TOPMOST = 0x8, WS_EX_TOOLWINDOW = 0x80, WS_EX_NOACTIVATE = 0x08000000;
        static Toast current;
        readonly System.Windows.Forms.Timer life;

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= WS_EX_TOPMOST | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
                return cp;
            }
        }

        // call on the UI thread
        public static void Popup(string text, bool warning)
        {
            if (current != null && !current.IsDisposed) current.Close();
            current = new Toast(text, warning);
            current.Show();
        }

        Toast(string text, bool warning)
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            BackColor = Color.FromArgb(28, 28, 32);
            Opacity = 0.94;

            Panel accent = new Panel();
            accent.Dock = DockStyle.Left;
            accent.Width = 5;
            accent.BackColor = warning ? Color.FromArgb(230, 90, 70) : Color.FromArgb(232, 178, 54);
            Controls.Add(accent);

            Label l = new Label();
            l.AutoSize = true;
            l.MaximumSize = new Size(420, 0);
            l.Font = new Font("Segoe UI", 10f);
            l.ForeColor = Color.White;
            l.Text = text;
            l.Location = new Point(16, 10);
            Controls.Add(l);

            Size pref = l.GetPreferredSize(new Size(420, 0));
            ClientSize = new Size(pref.Width + 32, pref.Height + 20);

            Rectangle wa = Screen.FromPoint(Cursor.Position).WorkingArea;
            Location = new Point(wa.Right - Width - 16, wa.Bottom - Height - 16);

            l.Click += (s, e) => Close();
            Click += (s, e) => Close();

            life = new System.Windows.Forms.Timer();
            life.Interval = warning ? 5000 : 2200;
            life.Tick += (s, e) => { life.Stop(); Close(); };
            life.Start();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            life.Stop();
            life.Dispose();
            base.OnFormClosed(e);
        }
    }

    // ======================================================================
    // "Press a key" window for choosing the hotkey
    // ======================================================================
    class HotkeyForm : Form
    {
        public Keys PickedKey = Keys.None;
        public Keys PickedMods = Keys.None;

        public HotkeyForm(string current)
        {
            Text = "TibiaSnap - Change hotkey";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            TopMost = true;
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(400, 130);
            KeyPreview = true;

            Label hint = new Label();
            hint.Dock = DockStyle.Fill;
            hint.TextAlign = ContentAlignment.MiddleCenter;
            hint.Font = new Font("Segoe UI", 10f);
            hint.Text = "Now: " + current + "\n\nPress the key (or combo like Ctrl+F12) you use\nfor \"Take Screenshot\" in Tibia.\n\nEsc = cancel";
            Controls.Add(hint);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            Keys key = keyData & Keys.KeyCode;
            Keys mods = keyData & Keys.Modifiers;

            if (key == Keys.Escape && mods == Keys.None)
            {
                DialogResult = DialogResult.Cancel;
                return true;
            }
            if (key == Keys.ControlKey || key == Keys.ShiftKey || key == Keys.Menu ||
                key == Keys.LWin || key == Keys.RWin || key == Keys.None)
                return true;

            PickedKey = key;
            PickedMods = mods;
            DialogResult = DialogResult.OK;
            return true;
        }
    }

    // ======================================================================
    // "What's new" window
    // ======================================================================
    class WhatsNewForm : Form
    {
        public WhatsNewForm(string title, string text)
        {
            Text = title;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            TopMost = true;
            AutoScaleMode = AutoScaleMode.Font;
            Font = new Font("Segoe UI", 10f);
            ClientSize = new Size(520, 340);

            TextBox box = new TextBox();
            box.Multiline = true;
            box.ReadOnly = true;
            box.ScrollBars = ScrollBars.Vertical;
            box.BorderStyle = BorderStyle.None;
            box.BackColor = SystemColors.Window;
            box.Text = text;
            box.Dock = DockStyle.Fill;

            Panel inner = new Panel();
            inner.Dock = DockStyle.Fill;
            inner.Padding = new Padding(14);
            inner.BackColor = SystemColors.Window;
            inner.Controls.Add(box);

            Button ok = new Button();
            ok.Text = "Nice";
            ok.Width = 100;
            ok.Height = 32;
            ok.DialogResult = DialogResult.OK;
            ok.Click += (s, e) => Close();

            FlowLayoutPanel bottom = new FlowLayoutPanel();
            bottom.Dock = DockStyle.Bottom;
            bottom.FlowDirection = FlowDirection.RightToLeft;
            bottom.Height = 48;
            bottom.Padding = new Padding(8);
            bottom.Controls.Add(ok);

            Controls.Add(inner);
            Controls.Add(bottom);
            AcceptButton = ok;
            Shown += (s, e) => { box.SelectionLength = 0; ok.Focus(); };
        }
    }

    // ======================================================================
    // Tray app
    // ======================================================================
    class TrayApp : ApplicationContext
    {
        [DllImport("user32.dll")] static extern short GetAsyncKeyState(int vKey);
        [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr hWnd, StringBuilder sb, int max);
        [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr handle);

        const int VK_SHIFT = 0x10, VK_CONTROL = 0x11, VK_MENU = 0x12;
        const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string AppName = "TibiaSnap";
        static readonly string[] ImageExts = { ".png", ".jpg", ".jpeg", ".bmp" };

        NotifyIcon tray;
        System.Windows.Forms.Timer keyTimer;
        System.Windows.Forms.Timer updateTimer;
        Control invoker; // to get back onto the UI thread
        Settings settings;

        ToolStripMenuItem enabledItem, viewerItem, saveCropsItem, autostartItem, hotkeyItem;
        ToolStripMenuItem cropMenu, monitorMenu, pressMenu, seeMenu, seeOff, seePlay, seeCustom;

        ViewerForm currentViewer;
        bool keyWasDown;
        volatile bool busy;
        volatile bool updating;
        volatile string lastOpened;

        // folder watcher: tells us about the new screenshot the moment Tibia writes it
        FileSystemWatcher watcher;
        readonly object evLock = new object();
        string lastEventPath;
        DateTime lastEventUtc;
        readonly AutoResetEvent fileSignal = new AutoResetEvent(false);

        public TrayApp(bool afterUpdate, Version updatedFrom)
        {
            settings = Settings.Load();

            invoker = new Control();
            IntPtr forceHandle = invoker.Handle;

            tray = new NotifyIcon();
            tray.Icon = MakeIcon();
            tray.ContextMenuStrip = BuildMenu();
            tray.Visible = true;
            tray.DoubleClick += (s, e) => OpenLatestNow();
            UpdateTooltip();

            if (autostartItem.Checked) SetAutostart(true); // keep path current if the exe moved

            if (!Directory.Exists(settings.Folder))
            {
                MessageBox.Show(
                    "Couldn't find the Tibia screenshot folder here:\n\n" + settings.Folder +
                    "\n\nPick it in the next window. (If it doesn't exist yet, take one screenshot in Tibia first.)",
                    "TibiaSnap", MessageBoxButtons.OK, MessageBoxIcon.Information);
                ChooseFolder();
            }
            SetupWatcher();

            keyTimer = new System.Windows.Forms.Timer();
            keyTimer.Interval = 20;
            keyTimer.Tick += OnTick;
            keyTimer.Start();

            if (afterUpdate)
            {
                string notes = ChangeLog.TextSince(updatedFrom);
                if (notes.Length > 0)
                    new WhatsNewForm("TibiaSnap updated to v" + Updater.CurrentText + "!", notes).Show();
                else
                    ShowTip("Updated to v" + Updater.CurrentText + "!", ToolTipIcon.Info);
            }
            else
                ShowTip("Running in your tray. Press " + settings.HotkeyName + " in Tibia to open the screenshot.", ToolTipIcon.Info);

            updateTimer = new System.Windows.Forms.Timer();
            updateTimer.Interval = 15000;
            updateTimer.Tick += (s, e) => { updateTimer.Interval = 3 * 60 * 60 * 1000; CheckForUpdates(false); };
            updateTimer.Start();
        }

        // ================= menu =================

        ContextMenuStrip menuCache;

        ContextMenuStrip BuildMenu()
        {
            if (menuCache != null) return menuCache;
            ContextMenuStrip menu = new ContextMenuStrip();

            ToolStripMenuItem title = new ToolStripMenuItem("TibiaSnap v" + Updater.CurrentText);
            title.Enabled = false;
            title.Font = new Font(title.Font, FontStyle.Bold);
            menu.Items.Add(title);
            menu.Items.Add(new ToolStripSeparator());

            enabledItem = new ToolStripMenuItem("Enabled");
            enabledItem.CheckOnClick = true;
            enabledItem.Checked = settings.Enabled;
            enabledItem.CheckedChanged += (s, e) => { settings.Enabled = enabledItem.Checked; settings.Save(); UpdateTooltip(); };
            menu.Items.Add(enabledItem);

            // crop mode
            cropMenu = new ToolStripMenuItem("Crop with");
            AddCropOption("Built-in crop (fast, copies to clipboard)", "builtin");
            AddCropOption("Windows Snipping Tool", "snip");
            AddCropOption("Nothing, just show the screenshot", "none");

            // what the hotkey does
            pressMenu = new ToolStripMenuItem("When I press the key");
            AddPressOption("Open the crop window (default)", "viewer");
            AddPressOption("Just copy the whole screenshot to clipboard", "copy");
            AddPressOption("Nothing pops up - crop later (double-click tray icon)", "later");
            menu.Items.Add(pressMenu);
            menu.Items.Add(cropMenu);

            // monitor (rebuilt every time the menu opens, in case monitors changed)
            monitorMenu = new ToolStripMenuItem("Open on monitor");
            monitorMenu.DropDownItems.Add("..."); // placeholder so the arrow shows
            menu.Items.Add(monitorMenu);

            // see-through viewer
            seeMenu = new ToolStripMenuItem("See-through viewer");
            seeOff = new ToolStripMenuItem("Off - solid (100% / 100%)");
            seeOff.Click += (s, e) => SetSeeThrough(100, 100, false);
            seePlay = new ToolStripMenuItem("Play mode (default) - see the game behind it (40% / marked 100%, keys stay in Tibia)");
            seePlay.Click += (s, e) => SetSeeThrough(PlayView, PlaySel, true);
            seeCustom = new ToolStripMenuItem("Custom...");
            seeCustom.Click += (s, e) =>
            {
                using (SeeThroughForm f = new SeeThroughForm(settings.ViewOpacity, settings.KeepFocus))
                    if (f.ShowDialog() == DialogResult.OK) SetSeeThrough(f.ViewOpacity, 100, f.KeepFocus);
            };
            seeMenu.DropDownItems.Add(seeOff);
            seeMenu.DropDownItems.Add(seePlay);
            seeMenu.DropDownItems.Add(seeCustom);
            menu.Items.Add(seeMenu);
            UpdateSeeChecks();
            menu.Opening += (s, e) => RebuildMonitorMenu();

            saveCropsItem = new ToolStripMenuItem("Also save crops to Pictures\\TibiaSnap");
            saveCropsItem.CheckOnClick = true;
            saveCropsItem.Checked = settings.SaveCrops;
            saveCropsItem.CheckedChanged += (s, e) => { settings.SaveCrops = saveCropsItem.Checked; settings.Save(); };
            menu.Items.Add(saveCropsItem);

            viewerItem = new ToolStripMenuItem("Use TibiaSnap's fullscreen viewer");
            viewerItem.CheckOnClick = true;
            viewerItem.Checked = settings.OwnViewer;
            viewerItem.CheckedChanged += (s, e) => { settings.OwnViewer = viewerItem.Checked; settings.Save(); };
            menu.Items.Add(viewerItem);

            autostartItem = new ToolStripMenuItem("Start with Windows");
            autostartItem.CheckOnClick = true;
            autostartItem.Checked = IsAutostartOn();
            autostartItem.CheckedChanged += (s, e) => SetAutostart(autostartItem.Checked);
            menu.Items.Add(autostartItem);

            menu.Items.Add(new ToolStripSeparator());
            hotkeyItem = new ToolStripMenuItem("", null, (s, e) => ChangeHotkey());
            menu.Items.Add(hotkeyItem);
            menu.Items.Add("Change screenshot folder...", null, (s, e) => ChooseFolder());
            menu.Items.Add("Open screenshot folder", null, (s, e) => OpenFolder());
            menu.Items.Add("Open saved crops", null, (s, e) => OpenCropsFolder());
            menu.Items.Add("Open latest screenshot", null, (s, e) => OpenLatestNow());
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Check for updates", null, (s, e) => CheckForUpdates(true));
            menu.Items.Add("What's new", null, (s, e) => new WhatsNewForm("TibiaSnap - What's new", ChangeLog.All()).Show());
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Exit", null, (s, e) => ExitApp());

            menuCache = menu;
            return menu;
        }

        void AddCropOption(string label, string value)
        {
            ToolStripMenuItem it = new ToolStripMenuItem(label);
            it.Checked = settings.CropMode == value;
            it.Click += (s, e) =>
            {
                settings.CropMode = value;
                settings.Save();
                foreach (ToolStripItem other in cropMenu.DropDownItems)
                {
                    ToolStripMenuItem mi = other as ToolStripMenuItem;
                    if (mi != null) mi.Checked = mi == it;
                }
            };
            cropMenu.DropDownItems.Add(it);
        }

        const int PlayView = 40, PlaySel = 100;

        void SetSeeThrough(int view, int sel, bool keep)
        {
            settings.ViewOpacity = view;
            settings.SelOpacity = sel;
            settings.KeepFocus = keep;
            settings.Save();
            UpdateSeeChecks();
        }

        void UpdateSeeChecks()
        {
            bool off = settings.ViewOpacity == 100 && settings.SelOpacity == 100 && !settings.KeepFocus;
            bool play = settings.ViewOpacity == PlayView && settings.SelOpacity == PlaySel && settings.KeepFocus;
            seeOff.Checked = off;
            seePlay.Checked = play;
            seeCustom.Checked = !off && !play;
            seeCustom.Text = seeCustom.Checked
                ? "Custom... (" + settings.ViewOpacity + "%, marked area 100%" + (settings.KeepFocus ? ", keys stay in Tibia" : "") + ")"
                : "Custom...";
        }

        void AddPressOption(string label, string value)
        {
            ToolStripMenuItem it = new ToolStripMenuItem(label);
            it.Checked = settings.OnPress == value;
            it.Click += (s, e) =>
            {
                settings.OnPress = value;
                settings.Save();
                foreach (ToolStripItem other in pressMenu.DropDownItems)
                {
                    ToolStripMenuItem mi = other as ToolStripMenuItem;
                    if (mi != null) mi.Checked = mi == it;
                }
            };
            pressMenu.DropDownItems.Add(it);
        }

        void RebuildMonitorMenu()
        {
            monitorMenu.DropDownItems.Clear();
            Screen[] all = Screen.AllScreens;
            bool savedExists = settings.Monitor == "mouse" || settings.Monitor == "primary";
            foreach (Screen sc in all) if (sc.DeviceName == settings.Monitor) savedExists = true;

            AddMonitorOption("Monitor with the mouse (default)", "mouse", !savedExists);
            AddMonitorOption("Main monitor", "primary", false);
            monitorMenu.DropDownItems.Add(new ToolStripSeparator());
            for (int i = 0; i < all.Length; i++)
            {
                Screen sc = all[i];
                string label = "Monitor " + (i + 1) + "   (" + sc.Bounds.Width + " x " + sc.Bounds.Height + (sc.Primary ? ", main" : "") + ")";
                AddMonitorOption(label, sc.DeviceName, false);
            }
        }

        void AddMonitorOption(string label, string value, bool forceCheck)
        {
            ToolStripMenuItem it = new ToolStripMenuItem(label);
            it.Checked = forceCheck || settings.Monitor == value;
            it.Click += (s, e) => { settings.Monitor = value; settings.Save(); };
            monitorMenu.DropDownItems.Add(it);
        }

        static Rectangle TargetScreenBounds(string mode)
        {
            if (mode == "primary") return Screen.PrimaryScreen.Bounds;
            if (mode != "mouse")
                foreach (Screen s in Screen.AllScreens)
                    if (s.DeviceName == mode) return s.Bounds;
            return Screen.FromPoint(Cursor.Position).Bounds;
        }

        // ================= hotkey =================

        void OnTick(object sender, EventArgs e)
        {
            bool down = IsDown((int)settings.HotkeyKey);
            if (down && !keyWasDown && settings.Enabled && !busy && ModifiersMatch() && TibiaIsActive())
            {
                busy = true;
                DateTime pressedUtc = DateTime.UtcNow;
                string folder = settings.Folder;
                RememberTibiaArea();
                Rectangle screen, target;
                GetPlacement(out screen, out target);
                ThreadPool.QueueUserWorkItem(_ => WaitAndOpen(folder, pressedUtc, screen, target));
            }
            keyWasDown = down;
        }

        // ---- lining the see-through viewer up with the game ----

        [StructLayout(LayoutKind.Sequential)] struct RECT { public int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)] struct POINT { public int X, Y; }
        [DllImport("user32.dll")] static extern bool GetClientRect(IntPtr hWnd, out RECT r);
        [DllImport("user32.dll")] static extern bool ClientToScreen(IntPtr hWnd, ref POINT p);

        Rectangle lastTibiaArea = Rectangle.Empty; // Tibia's game area on screen at the last key press

        // only asks Windows where the Tibia window's inside area is (like any window manager does)
        void RememberTibiaArea()
        {
            try
            {
                IntPtr h = GetForegroundWindow();
                RECT r;
                if (h == IntPtr.Zero || !GetClientRect(h, out r)) return;
                POINT p = new POINT();
                if (!ClientToScreen(h, ref p)) return;
                int w = r.Right - r.Left, ht = r.Bottom - r.Top;
                if (w > 50 && ht > 50) lastTibiaArea = new Rectangle(p.X, p.Y, w, ht);
            }
            catch { }
        }

        bool SeeThroughActive
        {
            get { return settings.CropMode != "snip" && (settings.ViewOpacity < 100 || settings.SelOpacity < 100); }
        }

        // see-through: put the screenshot exactly over Tibia's game area so it lines up.
        // solid: fit it to the chosen monitor like before.
        void GetPlacement(out Rectangle screen, out Rectangle target)
        {
            target = Rectangle.Empty;
            if (SeeThroughActive && !lastTibiaArea.IsEmpty)
            {
                screen = Screen.FromRectangle(lastTibiaArea).Bounds;
                target = new Rectangle(lastTibiaArea.X - screen.X, lastTibiaArea.Y - screen.Y,
                                       lastTibiaArea.Width, lastTibiaArea.Height);
                return;
            }
            screen = TargetScreenBounds(settings.Monitor);
        }

        static bool TibiaIsActive()
        {
            IntPtr h = GetForegroundWindow();
            if (h == IntPtr.Zero) return false;

            StringBuilder sb = new StringBuilder(256);
            GetWindowText(h, sb, sb.Capacity);
            if (!sb.ToString().StartsWith("Tibia", StringComparison.OrdinalIgnoreCase)) return false;

            uint pid;
            GetWindowThreadProcessId(h, out pid);
            try
            {
                using (Process p = Process.GetProcessById((int)pid))
                    return p.ProcessName.Equals("client", StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        static bool IsDown(int vk)
        {
            return (GetAsyncKeyState(vk) & 0x8000) != 0;
        }

        bool ModifiersMatch()
        {
            Keys m = settings.HotkeyMods;
            return IsDown(VK_CONTROL) == ((m & Keys.Control) != 0)
                && IsDown(VK_SHIFT) == ((m & Keys.Shift) != 0)
                && IsDown(VK_MENU) == ((m & Keys.Alt) != 0);
        }

        void ChangeHotkey()
        {
            using (HotkeyForm f = new HotkeyForm(settings.HotkeyName))
            {
                keyTimer.Stop();
                if (f.ShowDialog() == DialogResult.OK)
                {
                    settings.HotkeyKey = f.PickedKey;
                    settings.HotkeyMods = f.PickedMods;
                    settings.Save();
                    UpdateTooltip();
                    ShowTip("Hotkey is now " + settings.HotkeyName + ".\nSet Tibia's \"Take Screenshot\" to the same key!", ToolTipIcon.Info);
                }
                keyWasDown = true; // ignore the key still being held
                keyTimer.Start();
            }
        }

        // ================= finding + opening the screenshot =================

        void SetupWatcher()
        {
            if (watcher != null)
            {
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
                watcher = null;
            }
            if (!Directory.Exists(settings.Folder)) return;
            try
            {
                FileSystemWatcher w = new FileSystemWatcher(settings.Folder);
                w.IncludeSubdirectories = true;
                w.NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime;
                w.InternalBufferSize = 64 * 1024;
                w.Created += (s, e) => OnFileEvent(e.FullPath);
                w.Changed += (s, e) => OnFileEvent(e.FullPath);
                w.Renamed += (s, e) => OnFileEvent(e.FullPath);
                w.EnableRaisingEvents = true;
                watcher = w;
            }
            catch { watcher = null; } // falls back to scanning
        }

        void OnFileEvent(string path)
        {
            if (Array.IndexOf(ImageExts, Path.GetExtension(path).ToLowerInvariant()) < 0) return;
            lock (evLock)
            {
                lastEventPath = path;
                lastEventUtc = DateTime.UtcNow;
            }
            fileSignal.Set();
        }

        // Finds the screenshot for this key press: any image written from 3s before
        // the press on (Tibia can be faster than us), except the one opened last time.
        void WaitAndOpen(string folder, DateTime pressedUtc, Rectangle screen, Rectangle target)
        {
            try
            {
                DateTime threshold = pressedUtc.AddSeconds(-3);
                DateTime deadline = DateTime.UtcNow.AddSeconds(15);
                // the watcher is instant; scanning the whole folder is slow with lots of
                // screenshots, so only do it as a fallback (right away only if there's no watcher)
                DateTime nextScan = watcher != null ? DateTime.UtcNow.AddMilliseconds(1500) : DateTime.UtcNow;
                string found = null;

                while (found == null && DateTime.UtcNow < deadline)
                {
                    lock (evLock)
                    {
                        if (lastEventPath != null && lastEventUtc >= threshold &&
                            !string.Equals(lastEventPath, lastOpened, StringComparison.OrdinalIgnoreCase) &&
                            File.Exists(lastEventPath))
                            found = lastEventPath;
                    }
                    if (found != null) break;

                    if (DateTime.UtcNow >= nextScan)
                    {
                        DateTime t;
                        string f = NewestImage(folder, out t);
                        if (f != null && t >= threshold && !string.Equals(f, lastOpened, StringComparison.OrdinalIgnoreCase))
                        {
                            found = f;
                            break;
                        }
                        nextScan = DateTime.UtcNow.AddMilliseconds(watcher != null ? 1500 : 250);
                    }
                    fileSignal.WaitOne(15);
                }

                if (found == null) return; // nothing new (e.g. Tibia ignored the key) - stay quiet

                lastOpened = found;
                OpenImage(found, screen, target, settings.OnPress);
            }
            catch (Exception ex)
            {
                ShowTip("Something went wrong: " + ex.Message, ToolTipIcon.Error);
            }
            finally
            {
                busy = false;
            }
        }

        // worker thread. mode: viewer | copy | later
        void OpenImage(string path, Rectangle screen, Rectangle target, string mode)
        {
            if (mode == "later")
            {
                ShowTip("Screenshot saved - double-click the tray icon to crop it.", ToolTipIcon.Info);
                return;
            }

            if (mode == "copy")
            {
                Bitmap bmp = null;
                Exception lastErr = null;
                for (int i = 0; i < 120 && bmp == null; i++) // Tibia may still be writing it
                {
                    try { bmp = PreparedImage.LoadOriginal(path); }
                    catch (Exception ex) { lastErr = ex; Thread.Sleep(25); }
                }
                if (bmp == null) throw new Exception("couldn't read the screenshot (" + (lastErr != null ? lastErr.Message : "?") + ")");
                invoker.BeginInvoke((Action)(() =>
                {
                    bool ok = ClipboardHelper.SetImage(bmp);
                    bmp.Dispose();
                    ShowTip(ok ? "Screenshot copied to clipboard" : "Couldn't use the clipboard", ToolTipIcon.Info);
                }));
                return;
            }

            if (!settings.OwnViewer)
            {
                WaitUntilWritten(path);
                Process.Start(path); // default image viewer
                if (settings.CropMode == "snip") { Thread.Sleep(700); StartSnip(); }
                return;
            }

            PreparedImage p = null;
            Exception last = null;
            for (int i = 0; i < 120 && p == null; i++) // Tibia may still be writing it
            {
                try { p = PreparedImage.Load(path, screen, target); }
                catch (Exception ex) { last = ex; Thread.Sleep(25); }
            }
            if (p == null) throw new Exception("couldn't read the screenshot (" + (last != null ? last.Message : "?") + ")");

            invoker.BeginInvoke((Action)(() => ShowViewer(p)));
        }

        void ShowViewer(PreparedImage p)
        {
            try
            {
                if (currentViewer != null && !currentViewer.IsDisposed) currentViewer.Close();
                // the Snipping Tool would capture Tibia as black through a see-through viewer
                bool snip = settings.CropMode == "snip";
                int viewOp = snip ? 100 : settings.ViewOpacity;
                int selOp = snip ? 100 : settings.SelOpacity;
                bool keep = !snip && settings.KeepFocus;

                currentViewer = new ViewerForm(p, settings.CropMode, settings.SaveCrops,
                    msg => ShowTip(msg, ToolTipIcon.Info), viewOp, selOp, keep);
                if (settings.CropMode == "snip")
                    currentViewer.Shown += (s, e) =>
                    {
                        System.Windows.Forms.Timer t = new System.Windows.Forms.Timer();
                        t.Interval = 120; // let it paint once
                        t.Tick += (s2, e2) => { t.Stop(); t.Dispose(); StartSnip(); };
                        t.Start();
                    };
                currentViewer.Show();
                if (!keep) currentViewer.Activate();
            }
            catch (Exception ex)
            {
                p.Dispose();
                ShowTip("Couldn't open the screenshot: " + ex.Message, ToolTipIcon.Error);
            }
        }

        static void StartSnip()
        {
            try { Process.Start("ms-screenclip:"); } catch { }
        }

        void OpenLatestNow()
        {
            DateTime unused;
            string f = NewestImage(settings.Folder, out unused);
            if (f == null) { ShowTip("No screenshots in that folder yet.", ToolTipIcon.Info); return; }
            lastOpened = f;
            Rectangle screen, target;
            GetPlacement(out screen, out target);
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try { OpenImage(f, screen, target, "viewer"); }
                catch (Exception ex) { ShowTip("Something went wrong: " + ex.Message, ToolTipIcon.Error); }
            });
        }

        static string NewestImage(string folder, out DateTime newestTime)
        {
            newestTime = DateTime.MinValue;
            try
            {
                DirectoryInfo dir = new DirectoryInfo(folder);
                if (!dir.Exists) return null;
                string newest = null;
                foreach (FileInfo fi in dir.EnumerateFiles("*", SearchOption.AllDirectories))
                {
                    if (Array.IndexOf(ImageExts, fi.Extension.ToLowerInvariant()) < 0) continue;
                    DateTime t = fi.CreationTimeUtc > fi.LastWriteTimeUtc ? fi.CreationTimeUtc : fi.LastWriteTimeUtc;
                    if (t >= newestTime) { newestTime = t; newest = fi.FullName; }
                }
                return newest;
            }
            catch { return null; }
        }

        static void WaitUntilWritten(string path)
        {
            long last = -1;
            for (int i = 0; i < 40; i++)
            {
                long size = -1;
                try
                {
                    using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        size = fs.Length;
                }
                catch { }
                if (size > 0 && size == last) return;
                last = size;
                Thread.Sleep(50);
            }
        }

        // ================= folders =================

        void ChooseFolder()
        {
            using (FolderBrowserDialog dlg = new FolderBrowserDialog())
            {
                dlg.Description = "Pick your Tibia screenshot folder";
                if (Directory.Exists(settings.Folder)) dlg.SelectedPath = settings.Folder;
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    settings.Folder = dlg.SelectedPath;
                    settings.Save();
                    UpdateTooltip();
                    SetupWatcher();
                }
            }
        }

        void OpenFolder()
        {
            if (Directory.Exists(settings.Folder)) Process.Start("explorer.exe", "\"" + settings.Folder + "\"");
            else ShowTip("That folder doesn't exist. Use \"Change screenshot folder...\".", ToolTipIcon.Warning);
        }

        void OpenCropsFolder()
        {
            try
            {
                Directory.CreateDirectory(Settings.CropsFolder);
                Process.Start("explorer.exe", "\"" + Settings.CropsFolder + "\"");
            }
            catch (Exception ex) { ShowTip("Couldn't open it: " + ex.Message, ToolTipIcon.Warning); }
        }

        // ================= auto update =================

        void CheckForUpdates(bool manual)
        {
            if (updating) return;
            updating = true;
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    Version v;
                    string url;
                    if (!Updater.TryGetNewer(out v, out url))
                    {
                        if (manual) ShowBox("You're on the newest version (v" + Updater.CurrentText + ").", MessageBoxIcon.Information);
                        return;
                    }
                    ShowTip("Updating to v" + v.Major + "." + v.Minor + "." + v.Build + "...", ToolTipIcon.Info);
                    Updater.DownloadAndSwap(url);
                    invoker.BeginInvoke((Action)ExitApp); // the new version is starting
                }
                catch (UnauthorizedAccessException)
                {
                    if (manual) ShowBox("Can't update here - move TibiaSnap.exe to a normal folder (not Program Files).", MessageBoxIcon.Warning);
                }
                catch (System.Net.WebException ex)
                {
                    System.Net.HttpWebResponse r = ex.Response as System.Net.HttpWebResponse;
                    if (manual && r != null && r.StatusCode == System.Net.HttpStatusCode.NotFound)
                        ShowBox("No release found on GitHub yet (github.com/" + Updater.Repo + ").\nThe repo has to exist, be public, and have a release with TibiaSnap.exe.", MessageBoxIcon.Warning);
                    else if (manual)
                        ShowBox("Update check failed: " + ex.Message, MessageBoxIcon.Warning);
                }
                catch (Exception ex)
                {
                    if (manual) ShowBox("Update check failed: " + ex.Message, MessageBoxIcon.Warning);
                }
                finally
                {
                    updating = false;
                }
            });
        }

        // ================= autostart =================

        static bool IsAutostartOn()
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKeyPath, false))
                    return k != null && k.GetValue(AppName) != null;
            }
            catch { return false; }
        }

        void SetAutostart(bool on)
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.CreateSubKey(RunKeyPath))
                {
                    if (on) k.SetValue(AppName, "\"" + Application.ExecutablePath + "\"");
                    else if (k.GetValue(AppName) != null) k.DeleteValue(AppName);
                }
            }
            catch (Exception ex)
            {
                ShowTip("Couldn't change autostart: " + ex.Message, ToolTipIcon.Error);
            }
        }

        // ================= UI helpers =================

        void ShowTip(string text, ToolTipIcon icon)
        {
            bool warn = icon == ToolTipIcon.Warning || icon == ToolTipIcon.Error;
            Action a = () => Toast.Popup(text, warn);
            if (invoker.InvokeRequired) invoker.BeginInvoke(a);
            else a();
        }

        // a real popup - can't be hidden by Do Not Disturb like tray notifications
        void ShowBox(string text, MessageBoxIcon icon)
        {
            invoker.BeginInvoke((Action)(() =>
                MessageBox.Show(text, "TibiaSnap", MessageBoxButtons.OK, icon,
                    MessageBoxDefaultButton.Button1, MessageBoxOptions.DefaultDesktopOnly)));
        }

        void UpdateTooltip()
        {
            string t = settings.Enabled ? "TibiaSnap - " + settings.HotkeyName + " in Tibia opens screenshot" : "TibiaSnap - paused";
            if (tray != null) tray.Text = t.Length > 63 ? t.Substring(0, 63) : t;
            if (hotkeyItem != null) hotkeyItem.Text = "Change hotkey (now: " + settings.HotkeyName + ")...";
        }

        void ExitApp()
        {
            keyTimer.Stop();
            if (updateTimer != null) updateTimer.Stop();
            if (watcher != null) { watcher.EnableRaisingEvents = false; watcher.Dispose(); }
            tray.Visible = false;
            tray.Dispose();
            ExitThread();
        }

        static Icon MakeIcon()
        {
            using (Bitmap bmp = new Bitmap(32, 32))
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                using (SolidBrush body = new SolidBrush(Color.FromArgb(232, 178, 54)))
                {
                    g.FillRectangle(body, 10, 5, 11, 6);
                    g.FillRectangle(body, 2, 9, 28, 19);
                }
                using (SolidBrush lens = new SolidBrush(Color.FromArgb(35, 35, 40)))
                    g.FillEllipse(lens, 9, 11, 14, 14);
                using (SolidBrush glass = new SolidBrush(Color.FromArgb(110, 195, 255)))
                    g.FillEllipse(glass, 12, 14, 8, 8);
                using (SolidBrush flash = new SolidBrush(Color.White))
                    g.FillRectangle(flash, 24, 12, 3, 3);

                IntPtr h = bmp.GetHicon();
                Icon tmp = Icon.FromHandle(h);
                Icon result = (Icon)tmp.Clone();
                tmp.Dispose();
                DestroyIcon(h);
                return result;
            }
        }
    }
}
