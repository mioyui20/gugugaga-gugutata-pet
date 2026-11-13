using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Threading;
using System.Windows.Forms;
using System.Diagnostics;

[assembly: AssemblyTitle("gugugaga pet")]
[assembly: AssemblyProduct("gugugaga pet")]
[assembly: AssemblyDescription("guga — 你的桌面伙伴")]
[assembly: AssemblyVersion("1.1.0.0")]
[assembly: AssemblyFileVersion("1.1.0.0")]
[assembly: AssemblyInformationalVersion("1.1.0")]

namespace PenguinPet {
    static class Program {
        public const string Version = "1.1.0";
        [STAThread] static void Main(string[] args) {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            bool test = args.Length > 0 && args[0] == "--self-test";
            bool owner;
            using (var mutex = new Mutex(true, test ? "Local\\PenguinPet.Test" : "Local\\PenguinPet.Desktop", out owner)) {
                if (!owner) { var existing = Native.FindWindow(null, "gugugaga pet"); if (existing == IntPtr.Zero) existing = Native.FindWindow(null, "企鹅伴伴"); if (existing != IntPtr.Zero) Native.PostMessage(existing, 0x8001, IntPtr.Zero, IntPtr.Zero); return; }
                try { Application.Run(new PetWindow(test)); }
                catch (Exception e) {
                    if (test) { File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "test-failure.txt"), e.ToString()); Environment.ExitCode = 1; }
                    else MessageBox.Show(e.Message, "gugugaga pet · "+Ui.Text("启动失败"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
    }

    [DataContract] sealed class Preferences {
        [DataMember] public int Size = 400;
        [DataMember] public int Alpha = 100;
        [DataMember] public int X = int.MinValue;
        [DataMember] public int Y = int.MinValue;
        [DataMember] public bool Topmost = true;
        [DataMember] public bool Effects = true;
        [DataMember] public string Language = "zh-CN";
        static string FilePath { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PenguinPet", "settings.json"); } }
        public static Preferences Load() {
            try { using (var s = File.OpenRead(FilePath)) { var p = (Preferences)new DataContractJsonSerializer(typeof(Preferences)).ReadObject(s); p.Size = Math.Max(160, Math.Min(440, p.Size)); p.Alpha = Math.Max(35, Math.Min(100, p.Alpha)); return p; } }
            catch { return new Preferences(); }
        }
        public bool Save() {
            try { Directory.CreateDirectory(Path.GetDirectoryName(FilePath)); using (var s = File.Create(FilePath)) new DataContractJsonSerializer(typeof(Preferences)).WriteObject(s, this); return true; }
            catch { return false; }
        }
    }

    // A dedicated message-loop thread keeps input callbacks independent of rendering.
    // Key codes are transient animation state, never translated to text or saved.
    sealed class InputMonitor : IDisposable {
        public int Keys, Clicks, Wheels;
        public readonly int[] Held = new int[256];
        public int LastKey = 65, LeftButton, RightButton;
        public void Reset() { for (int i = 0; i < Held.Length; i++) Interlocked.Exchange(ref Held[i], 0); Interlocked.Exchange(ref LeftButton, 0); Interlocked.Exchange(ref RightButton, 0); }
        public bool IsHeld(int vk) { return vk >= 0 && vk < Held.Length && Volatile.Read(ref Held[vk]) != 0; }
        public int AnimationKey { get { int recent=Volatile.Read(ref LastKey); if(IsHeld(recent)) return recent; for(int i=0;i<Held.Length;i++) if(IsHeld(i)) return i; return recent; } }
        public volatile bool Enabled = true;
        public bool Available { get; private set; }
        public int Error { get; private set; }
        Native.HookProc keyboard, mouse;
        IntPtr kh, mh;
        uint threadId;
        Thread thread;
        readonly ManualResetEvent ready = new ManualResetEvent(false);
        public InputMonitor() {
            thread = new Thread(Run); thread.IsBackground = true; thread.Name = "Pet input events"; thread.Start();
            if (!ready.WaitOne(5000)) throw new InvalidOperationException(Ui.Text("键鼠监听线程未能启动。"));
        }
        void Run() {
            threadId = Native.GetCurrentThreadId();
            Native.MSG message;
            Native.PeekMessage(out message, IntPtr.Zero, 0, 0, 0);
            keyboard = delegate(int n, IntPtr w, IntPtr l) {
                if (n >= 0 && Enabled) {
                    int vk = Marshal.ReadInt32(l); long msg = w.ToInt64();
                    if (vk >= 0 && vk < 256) {
                        bool down = msg == 0x100 || msg == 0x104;
                        if (down || msg == 0x101 || msg == 0x105) Interlocked.Exchange(ref Held[vk], down ? 1 : 0);
                        if (down) { Interlocked.Exchange(ref LastKey, vk); Interlocked.Increment(ref Keys); }
                    }
                }
                return Native.CallNextHookEx(kh, n, w, l);
            };
            mouse = delegate(int n, IntPtr w, IntPtr l) {
                if (n >= 0 && Enabled) {
                    long msg = w.ToInt64();
                    if (msg == 0x201 || msg == 0x204 || msg == 0x207 || msg == 0x20B) Interlocked.Increment(ref Clicks);
                    if (msg == 0x20A || msg == 0x20E) Interlocked.Increment(ref Wheels);
                    if (msg == 0x201 || msg == 0x202) Interlocked.Exchange(ref LeftButton, msg == 0x201 ? 1 : 0);
                    if (msg == 0x204 || msg == 0x205) Interlocked.Exchange(ref RightButton, msg == 0x204 ? 1 : 0);
                }
                return Native.CallNextHookEx(mh, n, w, l);
            };
            try {
                kh = Native.SetWindowsHookEx(13, keyboard, Native.GetModuleHandle(null), 0);
                if (kh == IntPtr.Zero) Error = Marshal.GetLastWin32Error();
                mh = Native.SetWindowsHookEx(14, mouse, Native.GetModuleHandle(null), 0);
                if (mh == IntPtr.Zero) Error = Marshal.GetLastWin32Error();
                Available = kh != IntPtr.Zero && mh != IntPtr.Zero; ready.Set();
                Application.Run();
            } finally {
                if (kh != IntPtr.Zero) Native.UnhookWindowsHookEx(kh);
                if (mh != IntPtr.Zero) Native.UnhookWindowsHookEx(mh);
                ready.Set();
            }
        }
        public void Dispose() { Enabled = false; Native.PostThreadMessage(threadId, 0x12, IntPtr.Zero, IntPtr.Zero); thread.Join(2000); ready.Dispose(); }
    }

    sealed class PetWindow : Form {
        readonly Preferences prefs;
        readonly bool testing;
        readonly Bitmap[] sprites = new Bitmap[4];
        readonly Bitmap[] expressions = new Bitmap[6];
        readonly CompanionMotion motion = new CompanionMotion();
        readonly DeskScene scene;
        readonly InputMonitor input;
        readonly Stopwatch clock = Stopwatch.StartNew();
        readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
        readonly NotifyIcon tray;
        readonly ContextMenuStrip menu;
        readonly ToolStripMenuItem pauseItem, throughItem, topItem, hideItem, chineseItem, englishItem;
        SettingsWindow settings;
        bool paused, through, dragging, hidden;
        Point dragStart, windowStart, previousMouse;
        double lastActivity, keyUntil, clickUntil, wheelUntil, moveUntil, lastKey, sway, mouseY;
        int hand, taps, clicks;
        public int TestLayerUpdates;
        int testPhase;
        double testRenderStart;
        string testFolder;
        public string Status { get { return paused ? Ui.Text("已暂停响应") : !input.Available ? Ui.Text("监听不可用 · 错误 ") + input.Error : hidden ? Ui.Text("已隐藏 · 仍在托盘中") : Ui.Text("陪伴中 · 等待你的操作"); } }
        public string Counters { get { return string.Format(Ui.Text("本次启动：{0} 次敲击 / {1} 次点击"),taps,clicks); } }
        double Now { get { return clock.Elapsed.TotalSeconds; } }
        public PetWindow(bool test) {
            testing = test; prefs = test ? new Preferences() : Preferences.Load();
            Ui.English=prefs.Language=="en";
            Text = "gugugaga pet"; FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = true; TopMost = prefs.Topmost;
            StartPosition = FormStartPosition.Manual;
            LoadSprites(); LoadExpressions(); scene = new DeskScene(sprites,expressions); ApplySize(prefs.Size);
            var area = Screen.PrimaryScreen.WorkingArea;
            Location = prefs.X == int.MinValue ? new Point(area.Right - Width - 40, area.Bottom - Height - 12) : new Point(prefs.X, prefs.Y);
            ClampPosition(); previousMouse = Cursor.Position;
            menu = new ContextMenuStrip(); menu.Font = new Font("Microsoft YaHei UI", 10);
            var title = menu.Items.Add("{pet} / gugugaga pet"); title.Enabled = false;
            menu.Items.Add("设置与使用说明", null, delegate { OpenSettings(); });
            menu.Items.Add("摸摸头", null, delegate { Pet(); });
            menu.Items.Add("让咕咕嘎嘎打个盹", null, delegate { Doze(); });
            menu.Items.Add(new ToolStripSeparator());
            pauseItem = new ToolStripMenuItem("暂停响应", null, delegate { TogglePause(); }); menu.Items.Add(pauseItem);
            topItem = new ToolStripMenuItem("始终置顶", null, delegate { prefs.Topmost = !prefs.Topmost; TopMost = prefs.Topmost; SyncMenu(); }); menu.Items.Add(topItem);
            throughItem = new ToolStripMenuItem("鼠标穿透（从托盘恢复）", null, delegate { SetThrough(!through); }); menu.Items.Add(throughItem);
            hideItem = new ToolStripMenuItem("隐藏宠物", null, delegate { ToggleHidden(); }); menu.Items.Add(hideItem);
            menu.Items.Add("回到屏幕右下角", null, delegate { ResetPosition(); });
            var language=new ToolStripMenuItem("语言 / Language");
            chineseItem=new ToolStripMenuItem("中文",null,delegate { SetLanguage(false); });
            englishItem=new ToolStripMenuItem("English",null,delegate { SetLanguage(true); });
            language.DropDownItems.Add(chineseItem); language.DropDownItems.Add(englishItem); menu.Items.Add(language);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("退出", null, delegate { Close(); });
            foreach(ToolStripItem item in menu.Items) item.Tag=item.Text;
            tray = new NotifyIcon { Icon = CreateTrayIcon(), ContextMenuStrip = menu, Visible = !test };
            tray.DoubleClick += delegate { if (hidden) ToggleHidden(); Pet(); };
            input = new InputMonitor();
            SyncMenu();
            timer.Interval = 16; timer.Tick += delegate { TickPet(); }; timer.Start();
            Shown += delegate {
                DrawFrame();
                if (!test) { tray.BalloonTipTitle = Ui.PetName+" "+Ui.Text("来啦"); tray.BalloonTipText = Ui.Text("试试打字和移动鼠标吧！右键可打开设置。"); tray.ShowBalloonTip(4500); }
                if (!input.Available && !test) MessageBox.Show(Ui.Text("全局键鼠监听未能注册，错误：") + input.Error + Ui.Text("。宠物仍可拖动与互动。"), "gugugaga pet");
            };
        }
        protected override bool ShowWithoutActivation { get { return true; } }
        protected override CreateParams CreateParams { get { var cp = base.CreateParams; cp.ExStyle |= 0x80000 | 0x08000000; return cp; } }
        void LoadSprites() {
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("PenguinPet.Rig"))
            using (var atlas = new Bitmap(stream)) {
                int w = atlas.Width / 2, h = atlas.Height / 2;
                for (int i = 0; i < 4; i++) {
                    // Atlas cells have a thin separator; ignore its outer margin.
                    using (var cell = atlas.Clone(new Rectangle(i % 2 * w + 12, i / 2 * h + 12, w - 24, h - 24), PixelFormat.Format32bppArgb)) {
                        int left = cell.Width, top = cell.Height, right = 0, bottom = 0;
                        for (int y = 0; y < cell.Height; y++) for (int x = 0; x < cell.Width; x++) if (cell.GetPixel(x,y).A > 100) { left = Math.Min(left,x); right = Math.Max(right,x); top = Math.Min(top,y); bottom = Math.Max(bottom,y); }
                        sprites[i] = cell.Clone(Rectangle.FromLTRB(left,top,right+1,bottom+1), PixelFormat.Format32bppPArgb);
                    }
                }
            }
        }
        Icon CreateTrayIcon() {
            using (var b = new Bitmap(48, 48)) using (var g = Graphics.FromImage(b)) {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.DrawImage(sprites[0], 0, 0, 48, 48);
                IntPtr handle = b.GetHicon(); try { using (var icon = Icon.FromHandle(handle)) return (Icon)icon.Clone(); } finally { Native.DestroyIcon(handle); }
            }
        }
        void LoadExpressions() {
            using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("PenguinPet.Expressions"))
            using(var atlas=new Bitmap(stream)) {
                int w=atlas.Width/3,h=atlas.Height/2;
                // A common crop keeps eyes and hood in the same place in all frames.
                int left=w,top=h,right=0,bottom=0;
                for(int i=0;i<6;i++) for(int y=5;y<h-5;y++) for(int x=5;x<w-5;x++) if(atlas.GetPixel(i%3*w+x,i/3*h+y).A>100) { left=Math.Min(left,x); top=Math.Min(top,y); right=Math.Max(right,x); bottom=Math.Max(bottom,y); }
                for(int i=0;i<6;i++) expressions[i]=atlas.Clone(new Rectangle(i%3*w+left,i/3*h+top,right-left+1,bottom-top+1),PixelFormat.Format32bppPArgb);
                sprites[0].Dispose(); sprites[0]=(Bitmap)expressions[0].Clone();
            }
        }
        public void ApplySize(int size) {
            prefs.Size = Math.Max(160, Math.Min(440, size)); ClientSize = new Size(prefs.Size, (int)(prefs.Size * .82));
            if (IsHandleCreated) { ClampPosition(); DrawFrame(); }
        }
        public void ApplyAlpha(int alpha) { prefs.Alpha = alpha; DrawFrame(); }
        public void SetEffects(bool value) { prefs.Effects = value; }
        public int PetSize { get { return prefs.Size; } }
        public int PetAlpha { get { return prefs.Alpha; } }
        public bool Effects { get { return prefs.Effects; } }
        public bool IsPaused { get { return paused; } }
        public bool IsThrough { get { return through; } }
        public void TogglePause() {
            paused = !paused; input.Enabled = !paused;
            input.Reset();
            motion.Reset(Now);
            Interlocked.Exchange(ref input.Keys, 0); Interlocked.Exchange(ref input.Clicks, 0); Interlocked.Exchange(ref input.Wheels, 0);
            keyUntil = clickUntil = wheelUntil = moveUntil = 0; lastActivity = Now; SyncMenu();
        }
        public void SetThrough(bool value) {
            through = value;
            int style = Native.GetWindowLong(Handle, -20);
            Native.SetWindowLong(Handle, -20, value ? style | 0x20 : style & ~0x20); SyncMenu();
            if (value && !testing) { tray.BalloonTipTitle = Ui.Text("已开启鼠标穿透"); tray.BalloonTipText = Ui.Text("右键系统托盘中的宠物图标，可关闭穿透。"); tray.ShowBalloonTip(3000); }
        }
        void ToggleHidden() { hidden = !hidden; if (hidden) Hide(); else { Show(); ClampPosition(); DrawFrame(); } SyncMenu(); }
        public void SetLanguage(bool english) { Ui.English=english; prefs.Language=english?"en":"zh-CN"; SyncMenu(); if(settings!=null&&!settings.IsDisposed) settings.RefreshLanguage(); }
        void SyncMenu() {
            foreach(ToolStripItem item in menu.Items) if(item.Tag is string) item.Text=Ui.Text((string)item.Tag);
            pauseItem.Checked = paused; topItem.Checked = prefs.Topmost; throughItem.Checked = through; hideItem.Text = Ui.Text(hidden ? "显示宠物" : "隐藏宠物");
            chineseItem.Checked=!Ui.English; englishItem.Checked=Ui.English;
            tray.Text="gugugaga pet · "+Ui.PetName+" · "+Ui.Text("右键设置 / 双击摸摸头");
        }
        public void Pet() { lastActivity = Now; motion.Pet(Now); }
        public void Doze() {
            Interlocked.Exchange(ref input.Keys,0); Interlocked.Exchange(ref input.Clicks,0); Interlocked.Exchange(ref input.Wheels,0);
            keyUntil=clickUntil=wheelUntil=moveUntil=0; lastActivity=Now-46; previousMouse=Cursor.Position; motion.Doze(Now);
        }
        public void ResetPosition() {
            var a = Screen.FromPoint(Cursor.Position).WorkingArea; Location = new Point(a.Right - Width - 24, a.Bottom - Height - 8);
            if (hidden) ToggleHidden(); ClampPosition(); DrawFrame();
        }
        void ClampPosition() {
            var a = Screen.FromRectangle(Bounds).WorkingArea;
            Location = new Point(Math.Max(a.Left, Math.Min(a.Right - Width, Left)), Math.Max(a.Top, Math.Min(a.Bottom - Height, Top)));
        }
        void OpenSettings() {
            if (settings == null || settings.IsDisposed) settings = new SettingsWindow(this);
            settings.Show(); settings.Activate();
        }
        void OnKeys(int count) {
            if (count <= 0 || paused) return;
            taps += count;
            // Limit pose flips to 12 Hz so fast typing still has readable animation.
            if (Now - lastKey > 0.08) { hand = 1 - hand; lastKey = Now; }
            lastActivity = Now; keyUntil = Now + 0.13; motion.Key(Now);
        }
        void OnClicks(int count) { if (count <= 0 || paused) return; clicks += count; lastActivity = Now; clickUntil = Now + 0.12; motion.Click(Now); }
        void TickPet() {
            // Recover from a launcher using SW_HIDE. Never override an explicit hide.
            if (!hidden && !Visible) { Show(); Native.ShowWindow(Handle, 4); }
            double now = Now;
            OnKeys(Interlocked.Exchange(ref input.Keys, 0)); OnClicks(Interlocked.Exchange(ref input.Clicks, 0));
            int scroll = Interlocked.Exchange(ref input.Wheels, 0);
            if (scroll > 0 && !paused) { wheelUntil = now + 0.5; lastActivity = now; motion.Activity(now); }
            Point pos = Cursor.Position;
            if (!paused && pos != previousMouse) { moveUntil = now + 0.16; lastActivity = now; motion.Activity(now); }
            previousMouse = pos;
            var viewport = Screen.FromPoint(pos).Bounds;
            double target = paused ? 0 : Math.Max(-1, Math.Min(1, (pos.X - viewport.Left) * 2.0 / viewport.Width - 1));
            double ty = paused ? 0 : Math.Max(-1, Math.Min(1, (pos.Y - viewport.Top) * 2.0 / viewport.Height - 1));
            sway += (target - sway) * 0.35; mouseY += (ty - mouseY) * 0.35;
            if (testing) RunSelfTest();
            if (!hidden) DrawFrame();
        }
        int Pose(double now) {
            if (paused) return 0;
            if (now < clickUntil) return 4;
            if (now < keyUntil) return 1 + hand;
            if (now < wheelUntil) return 4;
            if (now < moveUntil) return 3;
            if (now - lastActivity > 45) return 5;
            return 0;
        }
        Bitmap Render(int forcedPose) {
            double now = Now; int pose = forcedPose < 0 ? Pose(now) : forcedPose;
            bool preview = forcedPose >= 0;
            int activeKey=input.AnimationKey;
            var mf=preview?CompanionMotion.Preview(pose==5?4:pose==4?2:pose==1||pose==2?3:0):motion.Sample(now,(float)sway,paused,dragging);
            return scene.Render(Width, Height, now, mf.Face==4, paused, prefs.Effects,
                preview ? (pose == 3 ? .9f : 0) : (float)sway, preview ? (pose == 3 ? -.6f : 0) : (float)mouseY,
                preview ? (pose == 1 || pose == 2) : !paused && (now < keyUntil || input.IsHeld(activeKey)),
                preview ? (pose == 2 ? 74 : 65) : activeKey,
                preview ? pose == 4 : !paused && (Volatile.Read(ref input.LeftButton) != 0 || now < clickUntil),
                !paused && Volatile.Read(ref input.RightButton) != 0, !paused && now < wheelUntil,
                delegate(int vk) { return !paused && !preview && input.IsHeld(vk); },preview,mf);
        }
        static void DrawBubble(Graphics g, string text, Color color, float x, float y) {
            using (var f = new Font("Segoe UI", 23, FontStyle.Bold)) using (var brush = new SolidBrush(color)) g.DrawString(text, f, brush, x, y);
        }
        void DrawFrame() {
            if (!IsHandleCreated || IsDisposed) return;
            using (var b = Render(-1)) Native.SetBitmap(Handle, b, Left, Top, (byte)(prefs.Alpha * 255 / 100));
            TestLayerUpdates++;
        }
        protected override void OnMouseDown(MouseEventArgs e) {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Right) { menu.Show(Cursor.Position); return; }
            if (e.Button == MouseButtons.Left) { dragging = true; dragStart = Cursor.Position; windowStart = Location; Capture = true; }
        }
        protected override void OnMouseMove(MouseEventArgs e) { base.OnMouseMove(e); if (dragging) { var p = Cursor.Position; Location = new Point(windowStart.X + p.X - dragStart.X, windowStart.Y + p.Y - dragStart.Y); } }
        protected override void OnMouseUp(MouseEventArgs e) {
            base.OnMouseUp(e); if (!dragging || e.Button != MouseButtons.Left) return;
            dragging = false; Capture = false; ClampPosition();
            if (Math.Abs(Cursor.Position.X - dragStart.X) + Math.Abs(Cursor.Position.Y - dragStart.Y) < 5) Pet();
        }
        protected override void WndProc(ref Message m) {
            if (m.Msg == 0x8001) { hidden = false; prefs.Alpha = 100; TopMost = prefs.Topmost = true; SetThrough(false); Show(); Native.ShowWindow(Handle,4); ResetPosition(); SyncMenu(); return; }
            if (m.Msg == 0x21) { m.Result = new IntPtr(3); return; } // MA_NOACTIVATE
            if (m.Msg == 0x7E) ClampPosition(); // display changed
            base.WndProc(ref m);
        }
        protected override void OnFormClosed(FormClosedEventArgs e) {
            timer.Stop(); timer.Dispose(); input.Dispose();
            if (settings != null) settings.Close();
            tray.Visible = false; tray.Icon.Dispose(); tray.Dispose(); menu.Dispose();
            foreach (var sprite in sprites) sprite.Dispose();
            foreach (var expression in expressions) expression.Dispose(); scene.Dispose();
            if (!testing) { prefs.X = Left; prefs.Y = Top; if (!prefs.Save()) MessageBox.Show(Ui.Text("设置未能保存，下次启动会使用默认设置。"), "gugugaga pet"); }
            base.OnFormClosed(e);
        }
        void Assert(bool success, string name) { if (!success) throw new InvalidOperationException("Self-test: " + name); }
        void RunSelfTest() {
            try {
                if (testPhase == 0) {
                    testFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "test-output"); Directory.CreateDirectory(testFolder);
                    Assert(input.Available, "global hooks registered, Win32=" + input.Error);
                    Assert(Native.IsWindowVisible(Handle), "pet is visible on desktop");
                    var mood=new CompanionMotion();
                    Assert(mood.Sample(0,0,false,false).Face==0,"initial calm expression");
                    Assert(mood.Sample(3.71,0,false,false).Face==1,"natural blink");
                    Assert(mood.Sample(3.95,0,false,false).Face==0,"blink ends without sleeping");
                    Assert(mood.Sample(46,0,false,false).Face==4,"expression falls asleep");
                    mood.Activity(46.1); Assert(mood.Sample(46.11,0,false,false).Face==5,"wake expression");
                    mood.Key(48); Assert(mood.Sample(48,0,false,false).Face==3,"typing focus");
                    mood.Pet(48.2); Assert(mood.Sample(48.4,0,false,false).Face==2,"petting smile");
                    Assert(mood.Sample(48.6,0,true,false).Face==0,"pause calms expression");
                    var doze=new CompanionMotion(); MotionFrame settled=new MotionFrame();
                    for(int i=0;i<=490;i++) settled=doze.Sample(i*.1,0,false,false);
                    Assert(settled.Rest>.98f,"gradual settling into resting pose");
                    float high=-1,low=1;
                    for(int i=0;i<=90;i++) { var f=doze.Sample(49+i*.1,0,false,false); high=Math.Max(high,f.Nod); low=Math.Min(low,f.Nod); }
                    Assert(high>.85f&&low<.05f,"sleep nod includes dip and rebound");
                    doze.Key(58.1); var waking=doze.Sample(58.12,0,false,false); Assert(waking.Face==5,"key immediately wakes dozing face");
                    for(int i=1;i<=10;i++) waking=doze.Sample(58.12+i*.1,0,false,false);
                    Assert(waking.Rest<.01f,"wake smoothly restores upright pose");
                    doze.Doze(60); Assert(doze.Sample(60,0,false,false).Face==4,"manual doze preview");
                    input.Held[65]=1; input.Held[66]=1; input.LastKey=66; input.Held[66]=0;
                    Assert(input.AnimationKey==65,"releasing last key preserves another held key"); input.Reset();
                    using (var transparent = Render(0)) Assert(transparent.GetPixel(0,0).A == 0, "transparent canvas");
                    string[] names = { "idle", "typing-left", "typing-right", "mouse", "happy", "sleep" };
                    for (int i = 0; i < 6; i++) using (var b = Render(i)) b.Save(Path.Combine(testFolder, names[i] + ".png"));
                    // Visual QA of joint continuity at the most stretched reaches.
                    int[] reachKeys = { 9, 8, 160, 32 };
                    for (int i=0;i<reachKeys.Length;i++) using(var b=scene.Render(400,328,0,false,false,false,i%2==0?-1:1,i<2?-1:1,true,reachKeys[i],false,false,false,delegate(int vk){return false;},true,CompanionMotion.Preview(3))) b.Save(Path.Combine(testFolder,"reach-"+i+".png"));
                    for(int i=0;i<6;i++) using(var b=scene.Render(400,328,0,i==4,false,true,0,0,false,65,false,false,false,delegate(int vk){return false;},true,CompanionMotion.Preview(i))) b.Save(Path.Combine(testFolder,"expression-"+i+".png"));
                    var snooze=new CompanionMotion();
                    for(int i=0;i<=510;i++) {
                        double t=i*.1; var f=snooze.Sample(t,0,false,false);
                        if(i==450||i==460||i==465||i==480||i==510) using(var b=scene.Render(400,328,t,f.Face==4,false,true,0,0,false,65,false,false,false,delegate(int vk){return false;},true,f)) b.Save(Path.Combine(testFolder,"doze-"+i+".png"));
                    }
                    OnKeys(1); Assert(Pose(Now) == 1 + hand && taps == 1, "keyboard action");
                    OnClicks(1); Assert(Pose(Now) == 4 && clicks == 1, "mouse click action");
                    TogglePause(); OnKeys(4); OnClicks(4); Assert(taps == 1 && clicks == 1 && Pose(Now) == 0 && !input.Enabled, "pause"); TogglePause();
                    keyUntil = clickUntil = wheelUntil = moveUntil = 0; lastActivity = Now - 50; Assert(Pose(Now) == 5, "idle sleep");
                    lastActivity = Now; moveUntil = Now + 1; Assert(Pose(Now) == 3, "mouse movement"); moveUntil = 0;
                    SetThrough(true); Assert((Native.GetWindowLong(Handle, -20) & 0x20) != 0, "click through"); SetThrough(false);
                    ApplySize(160); Assert(Width == 160, "resize small"); ApplySize(440); Assert(Width == 440, "resize large"); ApplySize(280); ApplyAlpha(65); ApplyAlpha(100);
                    var old = Location; Location = new Point(-99999, -99999); ClampPosition(); Assert(Screen.FromRectangle(Bounds).WorkingArea.IntersectsWith(Bounds), "recover offscreen"); Location = old;
                    ToggleHidden(); Assert(hidden && !Visible, "hide"); ToggleHidden(); Assert(!hidden && Visible, "show");
                    settings = new SettingsWindow(this); settings.Show();
                    testPhase = 1;
                } else if (testPhase == 1 && Now > 1) {
                    using (var b = new Bitmap(settings.Width, settings.Height)) { settings.DrawToBitmap(b, new Rectangle(Point.Empty, b.Size)); b.Save(Path.Combine(testFolder, "settings.png")); }
                    Assert(Ui.PetName=="咕咕嘎嘎","Chinese pet name");
                    settings.EnglishLanguageButton.PerformClick(); Assert(Ui.PetName=="guga"&&settings.Text=="gugugaga pet · Settings"&&englishItem.Checked,"English switch from settings button");
                    using(var b=new Bitmap(settings.Width,settings.Height)) { settings.DrawToBitmap(b,new Rectangle(Point.Empty,b.Size)); b.Save(Path.Combine(testFolder,"settings-en.png")); }
                    settings.ChineseLanguageButton.PerformClick(); Assert(Ui.PetName=="咕咕嘎嘎"&&settings.Text=="gugugaga pet · 设置"&&chineseItem.Checked,"Chinese switch from settings button");
                    settings.Close(); testPhase = 2; testRenderStart=Now;
                } else if (testPhase == 2 && Now-testRenderStart > 1.5) {
                    Assert(TestLayerUpdates > 20, "layered rendering loop");
                    File.WriteAllText(Path.Combine(testFolder, "results.txt"), "PASS\r\nVisible desktop window + global keyboard / mouse hooks registered.\r\nResting pose settling, breathing/nod cycle, manual doze, immediate wake response and smooth pose recovery passed.\r\nCurved textured arms, six expressions, blink timing, focus, petting smile, multi-key release, transparent rendering, pause, resize, alpha, click-through, offscreen recovery, hide/show, settings UI passed.\r\nLayer updates: " + TestLayerUpdates + "\r\nThis test does not inject real keyboard/mouse input.\r\n"); Close();
                }
            } catch (Exception e) { File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "test-failure.txt"), e.ToString()); Environment.ExitCode = 1; Close(); }
        }
    }

    sealed class SettingsWindow : Form {
        readonly PetWindow pet;
        readonly Label status;
        readonly Label counts;
        readonly Button pause, through;
        public readonly Button ChineseLanguageButton,EnglishLanguageButton;
        readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
        static Color ink = Color.FromArgb(35, 48, 64), muted = Color.FromArgb(108, 122, 138);
        public SettingsWindow(PetWindow p) {
            pet = p; Text = "gugugaga pet · "+Ui.Text("设置"); ClientSize = new Size(430, 580); AutoScaleMode = AutoScaleMode.Dpi;
            StartPosition = FormStartPosition.CenterScreen; FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false;
            BackColor = Color.FromArgb(247, 249, 252); Font = new Font("Microsoft YaHei UI", 10); ForeColor = ink;
            AddLabel("gugugaga pet · v"+Program.Version, 28, 22, 370, 22, 9, muted);
            AddLabel("{pet}", 26, 49, 220, 42, 25, ink);
            AddLabel("语言 / Language",250,47,160,20,8,muted);
            ChineseLanguageButton=AddButton("中文",252,70,64,delegate {pet.SetLanguage(false);});
            EnglishLanguageButton=AddButton("English",322,70,80,delegate {pet.SetLanguage(true);});
            ChineseLanguageButton.Height=EnglishLanguageButton.Height=29;
            ChineseLanguageButton.Font=new Font("Microsoft YaHei UI",9); EnglishLanguageButton.Font=new Font("Segoe UI",9);
            ChineseLanguageButton.Name="ChineseLanguage"; EnglishLanguageButton.Name="EnglishLanguage";
            ChineseLanguageButton.BringToFront(); EnglishLanguageButton.BringToFront();
            AddLabel("每一次敲击，都有小小的回应。", 28, 100, 370, 24, 10, muted);
            status = AddLabel("", 28, 141, 370, 26, 11, Color.FromArgb(30, 135, 133));
            counts = AddLabel("", 28, 171, 370, 24, 9, muted);
            AddLabel("宠物大小", 28, 219, 110, 25, 10, ink);
            var sizeText = AddLabel(pet.PetSize + " px", 310, 219, 90, 25, 10, muted);
            sizeText.Tag=null;
            var size = new TrackBar { Minimum = 160, Maximum = 440, Value = pet.PetSize, TickStyle = TickStyle.None, Bounds = new Rectangle(24, 250, 378, 32), SmallChange = 10, LargeChange = 20 };
            size.ValueChanged += delegate { pet.ApplySize(size.Value); sizeText.Text = size.Value + " px"; }; Controls.Add(size);
            AddLabel("不透明度", 28, 292, 110, 25, 10, ink);
            var alphaText = AddLabel(pet.PetAlpha + "%", 310, 292, 90, 25, 10, muted);
            alphaText.Tag=null;
            var alpha = new TrackBar { Minimum = 35, Maximum = 100, Value = pet.PetAlpha, TickStyle = TickStyle.None, Bounds = new Rectangle(24, 323, 378, 32) };
            alpha.ValueChanged += delegate { pet.ApplyAlpha(alpha.Value); alphaText.Text = alpha.Value + "%"; }; Controls.Add(alpha);
            var effects = new CheckBox { Tag="显示鼠标轨迹和睡眠气泡", Text = Ui.Text("显示鼠标轨迹和睡眠气泡"), Checked = pet.Effects, Bounds = new Rectangle(28, 369, 370, 28) };
            effects.CheckedChanged += delegate { pet.SetEffects(effects.Checked); }; Controls.Add(effects);
            pause = AddButton("", 28, 415, 177, delegate { pet.TogglePause(); RefreshStatus(); });
            through = AddButton("", 219, 415, 177, delegate { pet.SetThrough(!pet.IsThrough); RefreshStatus(); });
            AddLabel("拖动移动位置 · 单击摸摸头 · 右键更多设置", 28, 472, 380, 24, 9, muted);
            AddLabel("打字敲键盘 · 移动鼠标同步握鼠手\n按住保持按下，松开自动抬手。\n鼠标穿透后，可从系统托盘菜单恢复。", 28, 504, 380, 67, 9, muted);
            timer.Interval = 500; timer.Tick += delegate { RefreshStatus(); }; timer.Start(); RefreshLanguage();
        }
        Label AddLabel(string text, int x, int y, int w, int h, float size, Color color) { var l = new Label { Tag=text, Text = Ui.Text(text), Bounds = new Rectangle(x,y,w,h), Font = new Font("Microsoft YaHei UI", size), ForeColor = color }; Controls.Add(l); return l; }
        Button AddButton(string text, int x, int y, int w, EventHandler action) { var b = new Button { Text = text, Bounds = new Rectangle(x,y,w,39), FlatStyle = FlatStyle.Flat, BackColor = Color.White, Cursor = Cursors.Hand }; b.FlatAppearance.BorderColor = Color.FromArgb(214,223,232); b.Click += action; Controls.Add(b); return b; }
        public void RefreshLanguage() { Text="gugugaga pet · "+Ui.Text("设置"); foreach(Control c in Controls) if(c.Tag is string) c.Text=Ui.Text((string)c.Tag); ChineseLanguageButton.BackColor=Ui.English?Color.White:Color.FromArgb(30,135,133); ChineseLanguageButton.ForeColor=Ui.English?ink:Color.White; EnglishLanguageButton.BackColor=Ui.English?Color.FromArgb(30,135,133):Color.White; EnglishLanguageButton.ForeColor=Ui.English?Color.White:ink; RefreshStatus(); }
        void RefreshStatus() { status.Text = "●  " + pet.Status; counts.Text = pet.Counters; pause.Text = Ui.Text(pet.IsPaused ? "继续响应" : "暂停响应"); through.Text = Ui.Text(pet.IsThrough ? "关闭鼠标穿透" : "开启鼠标穿透"); }
        protected override void OnFormClosed(FormClosedEventArgs e) { timer.Stop(); timer.Dispose(); base.OnFormClosed(e); }
    }

    static class Native {
        [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern IntPtr FindWindow(string cls, string title);
        [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h, uint msg, IntPtr w, IntPtr l);
        [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int command);
        [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
        public delegate IntPtr HookProc(int code, IntPtr wParam, IntPtr lParam);
        [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X,Y; public POINT(int x,int y) { X=x; Y=y; } }
        [StructLayout(LayoutKind.Sequential)] public struct SIZE { public int X,Y; public SIZE(int x,int y) { X=x; Y=y; } }
        [StructLayout(LayoutKind.Sequential, Pack=1)] public struct BLEND { public byte Op,Flags,Alpha,Format; }
        [StructLayout(LayoutKind.Sequential)] public struct MSG { public IntPtr Hwnd; public uint Message; public UIntPtr WParam; public IntPtr LParam; public uint Time; public POINT Point; public uint Private; }
        [DllImport("user32.dll", SetLastError=true)] public static extern IntPtr SetWindowsHookEx(int id, HookProc callback, IntPtr module, uint tid);
        [DllImport("user32.dll")] public static extern IntPtr CallNextHookEx(IntPtr hook, int n, IntPtr w, IntPtr l);
        [DllImport("user32.dll")] public static extern bool UnhookWindowsHookEx(IntPtr hook);
        [DllImport("kernel32.dll", CharSet=CharSet.Auto)] public static extern IntPtr GetModuleHandle(string name);
        [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();
        [DllImport("user32.dll")] public static extern bool PostThreadMessage(uint id, uint msg, IntPtr w, IntPtr l);
        [DllImport("user32.dll")] public static extern bool PeekMessage(out MSG msg, IntPtr hwnd, uint min, uint max, uint remove);
        [DllImport("user32.dll")] public static extern int GetWindowLong(IntPtr hwnd, int index);
        [DllImport("user32.dll")] public static extern int SetWindowLong(IntPtr hwnd, int index, int value);
        [DllImport("user32.dll")] public static extern bool DestroyIcon(IntPtr icon);
        [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr hwnd);
        [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr hwnd, IntPtr dc);
        [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleDC(IntPtr dc);
        [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr dc);
        [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr dc, IntPtr obj);
        [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr obj);
        [DllImport("user32.dll", SetLastError=true)] static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr destDC, ref POINT dest, ref SIZE size, IntPtr sourceDC, ref POINT source, uint key, ref BLEND blend, uint flags);
        public static void SetBitmap(IntPtr hwnd, Bitmap bitmap, int x, int y, byte alpha) {
            IntPtr screen = GetDC(IntPtr.Zero), dc = IntPtr.Zero, hb = IntPtr.Zero, previous = IntPtr.Zero;
            try {
                dc = CreateCompatibleDC(screen); hb = bitmap.GetHbitmap(Color.FromArgb(0)); previous = SelectObject(dc, hb);
                var dest = new POINT(x,y); var size = new SIZE(bitmap.Width, bitmap.Height); var src = new POINT(0,0); var blend = new BLEND { Op=0, Flags=0, Alpha=alpha, Format=1 };
                if (!UpdateLayeredWindow(hwnd, screen, ref dest, ref size, dc, ref src, 0, ref blend, 2)) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            } finally { if (previous != IntPtr.Zero) SelectObject(dc, previous); if (hb != IntPtr.Zero) DeleteObject(hb); if (dc != IntPtr.Zero) DeleteDC(dc); if (screen != IntPtr.Zero) ReleaseDC(IntPtr.Zero, screen); }
        }
    }
}
