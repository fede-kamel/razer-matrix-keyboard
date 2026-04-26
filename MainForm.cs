using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace RazerKeyboard;

public sealed class MainForm : Form
{
    // ── grid ──────────────────────────────────────────────────���───
    const int CW = 16, CH = 20;

    // ── Matrix character alphabet (half-width katakana + extras) ──
    static readonly char[] Alphabet =
        "ｦｧｨｩｪｫｬｭｮｯｰｱｲｳｴｵｶｷｸｹｺｻｼｽｾｿﾀﾁﾂﾃﾄﾅﾆﾇﾈﾉﾊﾋﾌﾍﾎﾏﾐﾑﾒﾓﾔﾕﾖﾗﾘﾙﾚﾛﾜﾝ01234567890:.=|<>"
        .ToCharArray();

    // ── pre-allocated colors ───────────────────────────────────���──
    static readonly Color CHead   = Color.FromArgb(230, 255, 230);  // near-white head
    static readonly Color CBright = Color.FromArgb(0,   255,  70);  // bright green
    static readonly Color CMid    = Color.FromArgb(0,   160,   0);
    static readonly Color CDim    = Color.FromArgb(0,    70,   0);
    static readonly Color CDark   = Color.FromArgb(0,    30,   0);
    static readonly Color CGlow   = Color.FromArgb(0,   180,   0);
    static readonly Color CFade   = Color.FromArgb(30,    0,   0, 0);

    // ── pre-allocated brushes (not recreated each frame) ─────────
    readonly SolidBrush _brHead   = new(CHead);
    readonly SolidBrush _brBright = new(CBright);
    readonly SolidBrush _brMid    = new(CMid);
    readonly SolidBrush _brDim    = new(CDim);
    readonly SolidBrush _brDark   = new(CDark);
    readonly SolidBrush _brFade   = new(CFade);

    // ── fonts ─────────────────────────────────────────────────────
    readonly Font _fChar   = new("Courier New", 10f, FontStyle.Bold);
    readonly Font _fLogo1  = new("Courier New", 28f, FontStyle.Bold);
    readonly Font _fLogo2  = new("Courier New", 50f, FontStyle.Bold);
    readonly Font _fBtn    = new("Courier New", 10f, FontStyle.Bold);
    readonly Font _fStatus = new("Courier New",  8f, FontStyle.Regular);

    // ── state ─────────────────────────────────────────────────────
    readonly ChromaClient _chroma = new();
    readonly MatrixRain   _kbRain = new();
    readonly Random       _rng    = new();

    KeyboardMode _mode = KeyboardMode.Matrix;
    bool         _chromaReady;
    int          _kbTick;

    Bitmap?   _canvas;
    Graphics? _cg;
    Bitmap?   _logoCache;    // cached logo + reflection composite
    bool      _logoDirty = true;

    Drop[] _drops = [];

    Rectangle _btnMatrix, _btnGreen;
    bool      _hovMatrix, _hovGreen;

    readonly System.Windows.Forms.Timer _uiTimer = new() { Interval = 40 };
    readonly System.Windows.Forms.Timer _kbTimer = new() { Interval = 80 };

    // ── construction ──────────────────────────────────────────────
    public MainForm()
    {
        Text            = "The Matrix";
        ClientSize      = new Size(940, 600);
        MinimumSize     = new Size(640, 440);
        BackColor       = Color.Black;
        DoubleBuffered  = true;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox     = false;
        KeyPreview      = true;

        // Set form icon from the exe's embedded resource
        var exeDir = AppContext.BaseDirectory;
        var icoPath = Path.Combine(exeDir, "icon.ico");
        if (!File.Exists(icoPath))
            icoPath = Path.ChangeExtension(Application.ExecutablePath, ".ico");
        if (File.Exists(icoPath))
            Icon = new Icon(icoPath);

        _ = _chroma.InitAsync().ContinueWith(t => _chromaReady = t.Result);

        _uiTimer.Tick += OnUiTick;
        _kbTimer.Tick += OnKbTick;
    }

    // ── lifecycle ─────────────────────────────────────────────────
    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        RebuildCanvas();
        RebuildDrops();
        _uiTimer.Start();
        _kbTimer.Start();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _uiTimer.Stop();
        _kbTimer.Stop();
        _chroma.DisposeAsync().AsTask().Wait(800);
        DisposeResources();
        base.OnFormClosing(e);
    }

    void DisposeResources()
    {
        _canvas?.Dispose(); _cg?.Dispose(); _logoCache?.Dispose();
        _brHead.Dispose(); _brBright.Dispose(); _brMid.Dispose();
        _brDim.Dispose(); _brDark.Dispose(); _brFade.Dispose();
        _fChar.Dispose(); _fLogo1.Dispose(); _fLogo2.Dispose();
        _fBtn.Dispose(); _fStatus.Dispose();
    }

    // ── canvas management ─────────────────────────────────────────
    void RebuildCanvas()
    {
        _canvas?.Dispose(); _cg?.Dispose();
        _canvas = new Bitmap(ClientSize.Width, ClientSize.Height);
        _cg     = Graphics.FromImage(_canvas);
        _cg.Clear(Color.Black);
        _logoDirty = true;
    }

    void RebuildDrops()
    {
        int cols = ClientSize.Width / CW + 2;
        _drops = new Drop[cols];
        for (int i = 0; i < cols; i++)
            _drops[i] = MakeDrop(i * CW, _rng.Next(-ClientSize.Height, 0));
    }

    Drop MakeDrop(int x, int startY)
    {
        var chars = new char[32];
        for (int i = 0; i < chars.Length; i++)
            chars[i] = Alphabet[_rng.Next(Alphabet.Length)];
        return new Drop
        {
            X      = x,
            Y      = startY,
            Speed  = _rng.Next(1, 4) * CH,
            Length = _rng.Next(8, 24),
            Chars  = chars,
            White  = _rng.Next(6) == 0,   // ~17% of drops have a white-head glint
        };
    }

    // ── animation tick ────────────────────────────────────────────
    void OnUiTick(object? s, EventArgs e)
    {
        if (_canvas == null || _cg == null) return;
        var g = _cg;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

        // Fade existing pixels toward black (creates the trailing effect)
        g.FillRectangle(_brFade, 0, 0, _canvas.Width, _canvas.Height);

        // Draw rain drops
        foreach (var d in _drops)
        {
            // Randomly mutate a character in this drop
            if (_rng.Next(12) == 0)
                d.Chars[_rng.Next(d.Chars.Length)] = Alphabet[_rng.Next(Alphabet.Length)];

            int headY = d.Y;
            int h     = _canvas.Height;

            // Head — white or bright green
            if (headY >= -CH && headY <= h)
            {
                var headBr = d.White ? _brHead : _brBright;
                g.DrawString(d.Chars[0].ToString(), _fChar, headBr, d.X, headY);
            }

            // Trail — only draw a few cells below the head to avoid full-trail redraw
            // (the fade handles most of the visual trailing)
            for (int i = 1; i <= 3; i++)
            {
                int ty = headY - i * CH;
                if (ty < -CH) break;
                if (ty > h)   continue;
                var br = i == 1 ? _brBright : i == 2 ? _brMid : _brDim;
                g.DrawString(d.Chars[i % d.Chars.Length].ToString(), _fChar, br, d.X, ty);
            }

            // Advance drop
            d.Y += d.Speed;
            if (d.Y - d.Length * CH > h)
            {
                var nd = MakeDrop(d.X, _rng.Next(-h / 2, -CH));
                d.Y = nd.Y; d.Speed = nd.Speed; d.Length = nd.Length;
                d.White = nd.White;
                Array.Copy(nd.Chars, d.Chars, nd.Chars.Length);
            }
        }

        // Overlay cached logo (redrawn only when needed)
        DrawLogoOverlay(g);

        // Overlay UI controls (fast — only a few rects + strings)
        DrawControls(g);

        Invalidate();
    }

    void OnKbTick(object? s, EventArgs e)
    {
        if (!_chromaReady) return;
        var frame = _kbRain.NextFrame();  // safe: only called here on the UI thread
        int tick  = ++_kbTick;
        _ = Task.Run(async () =>
        {
            try
            {
                if (_mode == KeyboardMode.Green)
                    await _chroma.SetStaticGreenAsync();
                else
                    await _chroma.SetCustomAsync(frame);

                if (tick % 50 == 0)
                    await _chroma.HeartbeatAsync();
            }
            catch { /* Chroma SDK temporarily unavailable — skip frame */ }
        });
    }

    // ── logo ──────────────────────────────────────────────────────
    void DrawLogoOverlay(Graphics g)
    {
        if (_canvas == null) return;
        if (_logoDirty) RebuildLogoCache();
        if (_logoCache != null)
            g.DrawImageUnscaled(_logoCache, 0, 0);
    }

    void RebuildLogoCache()
    {
        if (_canvas == null) return;
        _logoCache?.Dispose();
        _logoCache = new Bitmap(_canvas.Width, 220);
        using var lg = Graphics.FromImage(_logoCache);
        lg.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        lg.Clear(Color.Transparent);

        int cx = _logoCache.Width / 2;

        const string sub   = "T H E";
        const string title = "M A T R I X";
        var          sz1   = lg.MeasureString(sub,   _fLogo1);
        var          sz2   = lg.MeasureString(title, _fLogo2);
        int x1 = (int)(cx - sz1.Width / 2), y1 = 18;
        int x2 = (int)(cx - sz2.Width / 2), y2 = 62;

        // Outer glow passes
        for (int pass = 5; pass >= 1; pass--)
        {
            int   a  = pass * 14;
            using var gb = new SolidBrush(Color.FromArgb(a, 0, 200, 0));
            for (int dx = -pass; dx <= pass; dx += pass)
            for (int dy = -pass; dy <= pass; dy += pass)
            {
                lg.DrawString(sub,   _fLogo1, gb, x1 + dx, y1 + dy);
                lg.DrawString(title, _fLogo2, gb, x2 + dx, y2 + dy);
            }
        }

        // Sharp text
        lg.DrawString(sub,   _fLogo1, _brMid,    x1, y1);
        lg.DrawString(title, _fLogo2, _brBright, x2, y2);
        // Title highlight (draw head color on top slightly offset)
        using var hiB = new SolidBrush(Color.FromArgb(160, 230, 255, 230));
        lg.DrawString(title, _fLogo2, hiB, x2, y2);

        // Reflection — flipped, faded
        int refY = y2 + (int)sz2.Height + 2;
        using var refBmp = new Bitmap((int)sz2.Width + 4, (int)sz2.Height);
        using (var rg = Graphics.FromImage(refBmp))
        {
            rg.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            rg.Clear(Color.Transparent);
            using var rb = new SolidBrush(Color.FromArgb(55, 0, 200, 0));
            rg.DrawString(title, _fLogo2, rb, 0, 0);
        }
        refBmp.RotateFlip(RotateFlipType.RotateNoneFlipY);

        // Fade the reflection from top to bottom using a gradient clip
        using var ia = new System.Drawing.Imaging.ImageAttributes();
        var fadeRect = new Rectangle(x2, refY, (int)sz2.Width + 4, (int)sz2.Height);
        lg.DrawImage(refBmp, fadeRect, 0, 0, refBmp.Width, refBmp.Height,
                     GraphicsUnit.Pixel, ia);

        _logoDirty = false;
    }

    // ── controls bar ──────────────────────────────────────────────
    void DrawControls(Graphics g)
    {
        if (_canvas == null) return;
        const int BW = 185, BH = 38, gap = 16;
        int by = _canvas.Height - 68;
        int cx = _canvas.Width / 2;

        _btnMatrix = new Rectangle(cx - BW - gap / 2, by, BW, BH);
        _btnGreen  = new Rectangle(cx + gap / 2,      by, BW, BH);

        // Dim background strip behind controls
        using var strip = new SolidBrush(Color.FromArgb(120, 0, 0, 0));
        g.FillRectangle(strip, 0, by - 8, _canvas.Width, BH + 52);

        DrawButton(g, _btnMatrix, "[ MATRIX RAIN ]",  _mode == KeyboardMode.Matrix, _hovMatrix);
        DrawButton(g, _btnGreen,  "[ SOLID GREEN  ]",  _mode == KeyboardMode.Green,  _hovGreen);

        // Status text
        string st = _chromaReady
            ? $"KEYBOARD: {(_mode == KeyboardMode.Matrix ? "MATRIX RAIN" : "SOLID GREEN")}   |   M / G to switch   |   ESC to close"
            : "CONNECTING TO RAZER CHROMA SDK…";

        using var sb  = new SolidBrush(Color.FromArgb(0, 110, 0));
        var       ssz = g.MeasureString(st, _fStatus);
        g.DrawString(st, _fStatus, sb, cx - ssz.Width / 2, _canvas.Height - 22);
    }

    void DrawButton(Graphics g, Rectangle r, string label, bool active, bool hover)
    {
        // Fill
        int alpha = active ? 45 : hover ? 22 : 8;
        using var fill = new SolidBrush(Color.FromArgb(alpha, 0, 120, 0));
        g.FillRectangle(fill, r);

        // Border
        var borderCol = active ? CBright : hover ? CMid : CDim;
        float pw      = active ? 2f      : 1f;
        using var pen = new Pen(borderCol, pw);
        g.DrawRectangle(pen, r);

        // Label
        var textCol = active ? CHead : hover ? CBright : CDim;
        using var tb  = new SolidBrush(textCol);
        var       tsz = g.MeasureString(label, _fBtn);
        g.DrawString(label, _fBtn, tb,
            r.X + (r.Width  - tsz.Width)  / 2,
            r.Y + (r.Height - tsz.Height) / 2);
    }

    // ── WinForms overrides ────────────────────────────────────────
    protected override void OnPaint(PaintEventArgs e)
    {
        if (_canvas != null)
            e.Graphics.DrawImageUnscaled(_canvas, 0, 0);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        _hovMatrix = _btnMatrix.Contains(e.Location);
        _hovGreen  = _btnGreen.Contains(e.Location);
        base.OnMouseMove(e);
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        if (_btnMatrix.Contains(e.Location)) _mode = KeyboardMode.Matrix;
        if (_btnGreen.Contains(e.Location))  _mode = KeyboardMode.Green;
        base.OnMouseClick(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        switch (e.KeyCode)
        {
            case Keys.M:      _mode = KeyboardMode.Matrix; break;
            case Keys.G:      _mode = KeyboardMode.Green;  break;
            case Keys.Escape: Close(); break;
        }
        base.OnKeyDown(e);
    }
}

// ── data types ────────────────────────────────────────────────────
sealed class Drop
{
    public int    X, Y, Speed, Length;
    public bool   White;
    public char[] Chars = [];
}

enum KeyboardMode { Matrix, Green }
