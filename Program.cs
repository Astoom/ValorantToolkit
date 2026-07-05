using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

#region Scanner — known-path lookup + keyword deep scan
static class Scanner
{
    // System dirs to skip at drive root (case-insensitive)
    static readonly HashSet<string> RootSkip = new(StringComparer.OrdinalIgnoreCase)
    {
        "Windows", "$Recycle.Bin", "System Volume Information",
        "Recovery", "Config.Msi", "MSOCache", "PerfLogs",
        "Documents and Settings", "$WinREAgent", "Boot",
    };

    // ── Phase 1: known-path quick scan (instant) ──

    static readonly string[] ConfigPaths =
    {
        @"Program Files (x86)\Tencent Games\VALORANT\live\ShooterGame\Saved\Config",
        @"Program Files\Tencent Games\VALORANT\live\ShooterGame\Saved\Config",
        @"Tencent Games\VALORANT\live\ShooterGame\Saved\Config",
        @"Tencent Games\无畏契约\live\ShooterGame\Saved\Config",
    };

    static List<string> QuickScan()
    {
        var results = new List<string>();

        foreach (string drive in Environment.GetLogicalDrives())
        {
            try { if (!new DriveInfo(drive).IsReady) continue; }
            catch { continue; }

            foreach (string relPath in ConfigPaths)
            {
                string configDir = Path.Combine(drive, relPath);
                if (!Directory.Exists(configDir)) continue;
                CollectFromConfig(configDir, results);
            }

            // Also check %LOCALAPPDATA%
            string localConfig = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                @"VALORANT\Saved\Config");
            if (Directory.Exists(localConfig))
                CollectFromConfig(localConfig, results);
        }

        return results;
    }

    // ── Phase 2: keyword-based deep scan (like Everything) ──

    static List<string> DeepSearch(IProgress<string>? progress, CancellationToken cancel)
    {
        var bag = new ConcurrentBag<string>();
        var drives = Environment.GetLogicalDrives();

        Parallel.ForEach(drives, drive =>
        {
            try { if (!new DriveInfo(drive).IsReady) return; }
            catch { return; }

            Walk(drive, 0, bag, progress, cancel);
        });

        return bag.ToList();
    }

    static void Walk(string dir, int depth, ConcurrentBag<string> bag,
        IProgress<string>? progress, CancellationToken cancel)
    {
        if (cancel.IsCancellationRequested || depth > 15) return;

        try
        {
            foreach (string sub in Directory.EnumerateDirectories(dir))
            {
                if (cancel.IsCancellationRequested) return;

                string name = Path.GetFileName(sub);

                // Root-level system dir skip
                if (depth == 0 && RootSkip.Contains(name)) continue;

                // Skip hidden + system dirs entirely
                FileAttributes attr;
                try { attr = File.GetAttributes(sub); }
                catch { continue; }
                if ((attr & (FileAttributes.Hidden | FileAttributes.System)) != 0) continue;

                progress?.Report(sub);

                // ── "ShooterGame" is the gold key — unique to UE games ──
                if (name.Equals("ShooterGame", StringComparison.OrdinalIgnoreCase))
                {
                    string cfg = Path.Combine(sub, "Saved", "Config");
                    if (Directory.Exists(cfg))
                        CollectFromConfig(cfg, bag);
                    continue; // no need to go deeper
                }

                // ── Also check for WindowsClient / GameUserSettings.ini in case structure is flat ──
                string directIni = Path.Combine(sub, "GameUserSettings.ini");
                if (File.Exists(directIni) && IsValorantConfig(directIni))
                    bag.Add(directIni);

                // ── Recurse into promising dirs, or stay shallow near root ──
                bool promising = name.Contains("VALORANT") || name.Contains("无畏契约") ||
                                 name.Contains("Game") ||
                                 name.Contains("Tencent") || name.Contains("腾讯") ||
                                 name.Contains("Program")   // Program Files / Program Files (x86)
                                 ;

                if (promising || depth < 2)
                    Walk(sub, depth + 1, bag, progress, cancel);
            }
        }
        catch (UnauthorizedAccessException) { }
        catch (PathTooLongException) { }
        catch (DirectoryNotFoundException) { }
    }

    // ── Collect GameUserSettings.ini from a "Config" directory ──
    static void CollectFromConfig(string configDir, System.Collections.Concurrent.ConcurrentBag<string> bag)
    {
        try
        {
            foreach (string entry in Directory.EnumerateDirectories(configDir))
            {
                string entryName = Path.GetFileName(entry);

                if (entryName.Equals("WindowsClient", StringComparison.OrdinalIgnoreCase))
                {
                    // Option A: Config/WindowsClient/GameUserSettings.ini
                    string ini = Path.Combine(entry, "GameUserSettings.ini");
                    if (File.Exists(ini) && IsValorantConfig(ini))
                        bag.Add(ini);
                }
                else
                {
                    // Option B: Config/<GUID>/WindowsClient/GameUserSettings.ini
                    string ini = Path.Combine(entry, "WindowsClient", "GameUserSettings.ini");
                    if (File.Exists(ini) && IsValorantConfig(ini))
                        bag.Add(ini);
                }
            }
        }
        catch { }
    }

    static void CollectFromConfig(string configDir, List<string> list)
    {
        try
        {
            foreach (string entry in Directory.EnumerateDirectories(configDir))
            {
                string entryName = Path.GetFileName(entry);

                if (entryName.Equals("WindowsClient", StringComparison.OrdinalIgnoreCase))
                {
                    string ini = Path.Combine(entry, "GameUserSettings.ini");
                    if (File.Exists(ini) && IsValorantConfig(ini))
                        list.Add(ini);
                }
                else
                {
                    string ini = Path.Combine(entry, "WindowsClient", "GameUserSettings.ini");
                    if (File.Exists(ini) && IsValorantConfig(ini))
                        list.Add(ini);
                }
            }
        }
        catch { }
    }

    // ── Combined: quick + deep, deduped ──
    public static List<string> ScanAll(IProgress<string>? progress = null, CancellationToken cancel = default)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Phase 1 — known paths (instant)
        foreach (string f in QuickScan()) seen.Add(f);

        // Phase 2 — recursive keyword search
        if (!cancel.IsCancellationRequested)
        {
            foreach (string f in DeepSearch(progress, cancel))
                seen.Add(f);
        }

        return seen.OrderBy(x => x).ToList();
    }

    public static bool IsValorantConfig(string path)
    {
        string l = path.ToLowerInvariant();
        return l.Contains("shootergame") && l.Contains("windowsclient") &&
               l.Contains("saved") && l.Contains("config");
    }
}
#endregion

#region Config builder
static class ConfigBuilder
{
    const string Template =
@"[/Script/ShooterGame.ShooterGameUserSettings]
DefaultMonitorDeviceID=
DefaultMonitorIndex=-1
LastConfirmedDefaultMonitorDeviceID=
LastConfirmedDefaultMonitorIndex=-1
bShouldLetterbox=False
bLastConfirmedShouldLetterbox=False
bUseVSync=False
bUseDynamicResolution=False
ResolutionSizeX={X}
ResolutionSizeY={Y}
LastUserConfirmedResolutionSizeX={X}
LastUserConfirmedResolutionSizeY={Y}
WindowPosX=0
WindowPosY=0
LastConfirmedFullscreenMode=2
PreferredFullscreenMode=2
AudioQualityLevel=0
LastConfirmedAudioQualityLevel=0
FrameRateLimit=0.000000
DesiredScreenWidth=1280
DesiredScreenHeight=720
LastUserConfirmedDesiredScreenWidth=1280
LastUserConfirmedDesiredScreenHeight=720
LastRecommendedScreenWidth=-1.000000
LastRecommendedScreenHeight=-1.000000
LastCPUBenchmarkResult=-1.000000
LastGPUBenchmarkResult=-1.000000
LastGPUBenchmarkMultiplier=1.000000
bUseHDRDisplayOutput=False
HDRDisplayOutputNits=1000
FullscreenMode=2

[/Script/Engine.GameUserSettings]
bUseDesiredScreenHeight=False

[ScalabilityGroups]
sg.ViewDistanceQuality=3
sg.AntiAliasingQuality=3
sg.ShadowQuality=3
sg.PostProcessQuality=3
sg.TextureQuality=2
sg.EffectsQuality=3
sg.FoliageQuality=3
sg.ShadingQuality=3

[Internationalization.AssetGroupCultures]
Mature=zh-CN

[ShaderPipelineCache.CacheFile]
LastOpened=ShooterGame
";

    public static string Build(int x, int y) =>
        Template.Replace("{X}", x.ToString()).Replace("{Y}", y.ToString());

    public static readonly (string label, int x, int y)[] Presets =
    {
        ("1440×1080 (4:3 常用)", 1440, 1080),
        ("1280×960  (4:3)",      1280, 960),
        ("1024×768  (4:3)",      1024, 768),
        ("1920×1080 (16:9 原生)", 1920, 1080),
        ("1280×1024 (5:4)",      1280, 1024),
        ("自定义...",             1440, 1080),
    };

    public static bool Apply(string path, string config)
    {
        try
        {
            if (File.Exists(path))
            {
                string bak = path + ".bak";
                if (!File.Exists(bak))
                    File.Copy(path, bak);

                var attrs = File.GetAttributes(path);
                if ((attrs & FileAttributes.ReadOnly) != 0)
                    File.SetAttributes(path, attrs & ~FileAttributes.ReadOnly);
            }
            else
            {
                string? d = Path.GetDirectoryName(path);
                if (d != null) Directory.CreateDirectory(d);
            }

            File.WriteAllText(path, config);
            File.SetAttributes(path, File.GetAttributes(path) | FileAttributes.ReadOnly);
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            MessageBox.Show($"权限不足!\n\n{path}\n\n请右键以管理员身份运行本程序。",
                "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        catch (IOException ex) when ((ex.HResult & 0xFFFF) == 0x0020)
        {
            MessageBox.Show($"文件被占用!\n\n{path}\n\n请先关闭游戏。",
                "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"操作失败!\n\n{path}\n\n{ex.Message}",
                "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        return false;
    }
}
#endregion

#region DisplayChanger — switch Windows system resolution
static class DisplayChanger
{
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    static extern int ChangeDisplaySettings(ref DEVMODE dm, int flags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    static extern bool EnumDisplaySettings(string? deviceName, int modeNum, ref DEVMODE dm);

    const int DM_PELSWIDTH = 0x80000;
    const int DM_PELSHEIGHT = 0x100000;
    const int CDS_UPDATEREGISTRY = 0x01;
    const int CDS_TEST = 0x02;
    const int ENUM_CURRENT_SETTINGS = -1;
    const int DISP_CHANGE_SUCCESSFUL = 0;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    struct DEVMODE
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmDeviceName;
        public short dmSpecVersion, dmDriverVersion, dmSize;
        public short dmDriverExtra;
        public int dmFields;
        public int dmPositionX, dmPositionY;
        public int dmDisplayOrientation, dmDisplayFixedOutput;
        public short dmColor, dmDuplex, dmYResolution, dmTTOption, dmCollate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmFormName;
        public short dmLogPixels, dmBitsPerPel;
        public int dmPelsWidth, dmPelsHeight, dmDisplayFlags, dmDisplayFrequency;
        public int dmICMMethod, dmICMIntent, dmMediaType, dmDitherType;
        public int dmReserved1, dmReserved2, dmPanningWidth, dmPanningHeight;
    }

    public static (int w, int h) GetCurrent()
    {
        var dm = new DEVMODE { dmSize = (short)Marshal.SizeOf<DEVMODE>() };
        if (EnumDisplaySettings(null, ENUM_CURRENT_SETTINGS, ref dm))
            return (dm.dmPelsWidth, dm.dmPelsHeight);
        return (0, 0);
    }

    public static bool Change(int width, int height)
    {
        var dm = new DEVMODE { dmSize = (short)Marshal.SizeOf<DEVMODE>() };
        dm.dmFields = DM_PELSWIDTH | DM_PELSHEIGHT;
        dm.dmPelsWidth = width;
        dm.dmPelsHeight = height;

        // Test first
        int testResult = ChangeDisplaySettings(ref dm, CDS_TEST);
        if (testResult != DISP_CHANGE_SUCCESSFUL)
            return false;

        // Apply and save
        int result = ChangeDisplaySettings(ref dm, CDS_UPDATEREGISTRY);
        return result == DISP_CHANGE_SUCCESSFUL;
    }

    public static readonly (string label, int w, int h)[] Presets =
    {
        ("2560×1440 (16:9 2K)",    2560, 1440),
        ("1920×1440 (4:3)",        1920, 1440),
        ("1920×1080 (16:9 FHD)",   1920, 1080),
        ("1440×1080 (4:3 拉伸)",   1440, 1080),
        ("1280×960  (4:3)",        1280, 960),
        ("1024×768  (4:3)",        1024, 768),
    };
}
#endregion

#region MainForm
sealed class MainForm : Form
{
    CheckedListBox _listBox = null!;
    TextBox _manualPath = null!;
    TextBox _preview = null!;
    Button _btnScan = null!;
    Button _btnApply = null!;
    ToolStripStatusLabel _status = null!;
    ComboBox _resPreset = null!;
    TextBox _resX = null!;
    TextBox _resY = null!;
    CancellationTokenSource? _scanCts;

    public MainForm()
    {
        Text = "Valorant Toolkit v1.0 by Astoom";
        Size = new Size(820, 700);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        Font = new Font("Microsoft YaHei UI", 9f);

        BuildUI();
        Load += (_, _) => { BeginInvoke(DoScan); RefreshPreview(); };
    }

    #region UI construction
    void BuildUI()
    {
        int pad = 12, y = pad;
        var bold = new Font(Font.FontFamily, 10f, FontStyle.Bold);

        // --- Scan results ---
        AddSectionLabel(ref y, pad, "扫描结果 (右键可打开文件 / 文件夹)");
        _listBox = new CheckedListBox
        {
            Location = new Point(pad, y), Size = new Size(780, 180),
            HorizontalScrollbar = true, IntegralHeight = false
        };
        _listBox.MouseUp += OnListRightClick;
        Controls.Add(_listBox);
        y += 188;

        // --- Manual path ---
        AddSectionLabel(ref y, pad, "手动指定路径 (优先级最高)");
        _manualPath = new TextBox
        {
            Location = new Point(pad, y), Size = new Size(680, 23),
            PlaceholderText = "粘贴或输入完整路径到 GameUserSettings.ini ..."
        };
        Controls.Add(_manualPath);
        Controls.Add(MakeButton("浏览...", pad + 688, y - 1, 80, 25, (_, _) =>
        {
            using var dlg = new OpenFileDialog
            { Title = "选择 GameUserSettings.ini", Filter = "INI 文件|GameUserSettings.ini|所有文件|*.*", FileName = "GameUserSettings.ini" };
            if (dlg.ShowDialog() == DialogResult.OK) _manualPath.Text = dlg.FileName;
        }));
        y += 32;

        // --- Resolution ---
        AddSectionLabel(ref y, pad, "分辨率设置");
        _resPreset = new ComboBox
        { Location = new Point(pad, y), Size = new Size(220, 23), DropDownStyle = ComboBoxStyle.DropDownList };
        foreach (var p in ConfigBuilder.Presets) _resPreset.Items.Add(p.label);
        _resPreset.SelectedIndex = 0;
        _resPreset.SelectedIndexChanged += OnPresetChanged;
        Controls.Add(_resPreset);

        _resX = AddField(ref y, pad + 235, "X:", "1440");
        _resY = AddField(ref y, pad + 318, "Y:", "1080");
        _resX.TextChanged += (_, _) => RefreshPreview();
        _resY.TextChanged += (_, _) => RefreshPreview();

        Controls.Add(new Label
        {
            Text = "选择预设或选 [自定义...] 手动输入",
            Location = new Point(pad + 405, y + 3), Size = new Size(300, 18), ForeColor = Color.Gray
        });
        y += 32;

        // --- System resolution ---
        AddSectionLabel(ref y, pad, "系统分辨率切换 (Windows 桌面)");
        var sysResCombo = new ComboBox
        {
            Location = new Point(pad, y), Size = new Size(220, 23), DropDownStyle = ComboBoxStyle.DropDownList
        };
        foreach (var p in DisplayChanger.Presets) sysResCombo.Items.Add(p.label);
        var cur = DisplayChanger.GetCurrent();
        // Select the preset matching current resolution, or add custom entry
        int matchIdx = -1;
        for (int i = 0; i < DisplayChanger.Presets.Length; i++)
            if (DisplayChanger.Presets[i].w == cur.w && DisplayChanger.Presets[i].h == cur.h)
                { matchIdx = i; break; }
        if (matchIdx >= 0)
            sysResCombo.SelectedIndex = matchIdx;
        else if (cur.w > 0)
        {
            sysResCombo.Items.Insert(0, $"当前: {cur.w}×{cur.h}");
            sysResCombo.SelectedIndex = 0;
        }
        Controls.Add(sysResCombo);

        var btnSysRes = MakeButton("切换", pad + 232, y - 1, 80, 25, (_, _) =>
        {
            int idx = sysResCombo.SelectedIndex;
            // Adjust index if we inserted "current" item
            int presetCount = DisplayChanger.Presets.Length;
            if (sysResCombo.Items.Count > presetCount) idx--;
            if (idx < 0 || idx >= presetCount) return;

            var sel = DisplayChanger.Presets[idx];
            if (MessageBox.Show($"将系统分辨率切换为:\n\n{sel.w} × {sel.h}\n\n屏幕可能会短暂黑屏，确定继续?",
                "切换分辨率", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK) return;

            if (DisplayChanger.Change(sel.w, sel.h))
            {
                _status.Text = $"系统分辨率已切换为 {sel.w}×{sel.h}";
                // Update combo to reflect new current
                if (sysResCombo.Items.Count > presetCount)
                    sysResCombo.Items.RemoveAt(0);
                sysResCombo.SelectedIndex = idx;
            }
            else
            {
                MessageBox.Show($"不支持该分辨率!\n\n{sel.w} × {sel.h}\n\n请检查显示器是否支持此模式。",
                    "切换失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        });
        Controls.Add(btnSysRes);

        Controls.Add(new Label
        {
            Text = "切换 Windows 系统分辨率 (进游戏前使用)",
            Location = new Point(pad + 322, y + 3), Size = new Size(400, 18), ForeColor = Color.Gray
        });
        y += 32;

        // --- Preview ---
        AddSectionLabel(ref y, pad, "配置预览");
        _preview = new TextBox
        {
            Location = new Point(pad, y), Size = new Size(780, 120),
            Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical,
            Font = new Font("Consolas", 8.5f), BackColor = Color.White
        };
        Controls.Add(_preview);
        y += 128;

        // --- Buttons ---
        y += 4;
        _btnScan = MakeButton("🔍 重新扫描", pad, y, 120, 32, (_, _) => DoScan(), bold: true);
        Controls.Add(_btnScan);

        _btnApply = MakeButton("✅ 应用配置", pad + 130, y, 120, 32, (_, _) => DoApply(), bold: true);
        _btnApply.BackColor = Color.FromArgb(0, 120, 212);
        _btnApply.ForeColor = Color.White;
        _btnApply.FlatStyle = FlatStyle.Flat;
        Controls.Add(_btnApply);

        Controls.Add(MakeButton("退出", pad + 520, y, 80, 32, (_, _) => Application.Exit()));
        y += 40;

        // --- Status bar ---
        var ss = new StatusStrip();
        _status = new ToolStripStatusLabel("就绪");
        ss.Items.Add(_status);
        Controls.Add(ss);
    }

    void AddSectionLabel(ref int y, int x, string text)
    {
        Controls.Add(new Label
        {
            Text = text, Location = new Point(x, y), Size = new Size(500, 20),
            Font = new Font(Font.FontFamily, 10f, FontStyle.Bold)
        });
        y += 24;
    }

    TextBox AddField(ref int y, int x, string label, string value)
    {
        Controls.Add(new Label { Text = label, Location = new Point(x, y + 3), Size = new Size(20, 18) });
        var tb = new TextBox { Location = new Point(x + 20, y), Size = new Size(55, 23), Text = value };
        Controls.Add(tb);
        return tb;
    }

    static Button MakeButton(string text, int x, int y, int w, int h, EventHandler click, bool bold = false)
    {
        var b = new Button { Text = text, Location = new Point(x, y), Size = new Size(w, h) };
        if (bold) b.Font = new Font(b.Font.FontFamily, 9f, FontStyle.Bold);
        b.Click += click;
        return b;
    }
    #endregion

    #region Right-click context menu
    void OnListRightClick(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Right) return;
        int idx = _listBox.IndexFromPoint(e.Location);
        if (idx < 0) return;

        _listBox.SelectedIndex = idx;
        string? path = _listBox.Items[idx] as string;
        if (string.IsNullOrEmpty(path)) return;

        var menu = new ContextMenuStrip();
        menu.Items.Add("📄 打开文件", null, (_, _) =>
        {
            try { Process.Start("notepad.exe", path); }
            catch (Exception ex) { MessageBox.Show($"无法打开文件:\n{ex.Message}", "错误"); }
        });
        menu.Items.Add("📁 打开所在文件夹", null, (_, _) =>
        {
            try { Process.Start("explorer.exe", $"/select,\"{path}\""); }
            catch (Exception ex) { MessageBox.Show($"无法打开文件夹:\n{ex.Message}", "错误"); }
        });
        menu.Items.Add("📋 复制路径", null, (_, _) =>
        {
            Clipboard.SetText(path);
            _status.Text = "路径已复制到剪贴板";
        });
        menu.Show(_listBox, e.Location);
    }
    #endregion

    #region Actions
    void DoScan()
    {
        // Cancel any in-flight scan
        _scanCts?.Cancel();
        _scanCts = new CancellationTokenSource();
        var cancel = _scanCts.Token;

        _listBox.Items.Clear();
        SetBusy(true, "正在全盘扫描 (关键字匹配)...");

        Task.Run(() =>
        {
            var progress = new Progress<string>(dir =>
            {
                try
                {
                    Invoke(() =>
                    {
                        // Show shortened path in status bar
                        string shortPath = dir.Length > 75
                            ? "..." + dir.Substring(dir.Length - 72)
                            : dir;
                        _status.Text = $"扫描中: {shortPath}";
                    });
                }
                catch { /* form closed */ }
            });

            var results = Scanner.ScanAll(progress, cancel);

            try
            {
                Invoke(() =>
                {
                    if (cancel.IsCancellationRequested) return;

                    foreach (string p in results)
                        _listBox.Items.Add(p, true);

                    _status.Text = results.Count > 0
                        ? $"扫描完成 — 找到 {results.Count} 个目标"
                        : "扫描完成 — 未找到 (请确认游戏已安装并至少启动过一次)";
                    SetBusy(false);
                });
            }
            catch { /* form closed before invoke */ }
        }, cancel);
    }

    void DoApply()
    {
        var targets = new List<string>();

        // Manual path has top priority
        string? manual = _manualPath.Text.Trim();
        if (!string.IsNullOrEmpty(manual))
        {
            if (File.Exists(manual))
                targets.Add(manual);
            else if (MessageBox.Show($"手动指定的路径不存在:\n\n{manual}\n\n是否仍要使用此路径?",
                         "路径无效", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                targets.Add(manual);
            else
                return;
        }

        foreach (string? item in _listBox.CheckedItems)
            if (item != null && !targets.Contains(item))
                targets.Add(item);

        if (targets.Count == 0)
        {
            MessageBox.Show("没有选择任何目标。\n请勾选扫描结果或输入手动路径。",
                "无目标", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        string msg = $"将修改以下 {targets.Count} 个文件:\n\n" +
                     string.Join("\n", targets.Select(t => $"  • {t}")) + "\n\n确定继续?";
        if (MessageBox.Show(msg, "确认操作", MessageBoxButtons.OKCancel,
                MessageBoxIcon.Question) != DialogResult.OK) return;

        if (!TryReadRes(out int x, out int y)) return;

        string config = ConfigBuilder.Build(x, y);
        int ok = targets.Count( t => ConfigBuilder.Apply(t, config));
        int fail = targets.Count - ok;

        MessageBox.Show($"完成: {ok} 成功, {fail} 失败", "结果", MessageBoxButtons.OK,
            fail > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
        _status.Text = $"应用完成 — {ok} 成功, {fail} 失败";
    }

    void SetBusy(bool busy, string? msg = null)
    {
        _btnScan.Enabled = !busy;
        _btnApply.Enabled = !busy;
        Cursor = busy ? Cursors.WaitCursor : Cursors.Default;
        if (msg != null) _status.Text = msg;
    }
    #endregion

    #region Resolution
    void OnPresetChanged(object? sender, EventArgs e)
    {
        int idx = _resPreset.SelectedIndex;
        bool custom = idx == ConfigBuilder.Presets.Length - 1;
        if (!custom && idx >= 0)
        {
            _resX.Text = ConfigBuilder.Presets[idx].x.ToString();
            _resY.Text = ConfigBuilder.Presets[idx].y.ToString();
        }
        _resX.ReadOnly = !custom;
        _resY.ReadOnly = !custom;
        RefreshPreview();
    }

    void RefreshPreview()
    {
        if (TryReadResSilent(out int x, out int y))
            _preview.Text = ConfigBuilder.Build(x, y);
    }

    bool TryReadResSilent(out int x, out int y)
    {
        bool okX = int.TryParse(_resX.Text.Trim(), out x) && x > 0;
        bool okY = int.TryParse(_resY.Text.Trim(), out y) && y > 0;
        return okX && okY;
    }

    bool TryReadRes(out int x, out int y)
    {
        if (TryReadResSilent(out x, out y)) return true;
        MessageBox.Show("请输入有效的分辨率数值 (正整数)。",
            "输入错误", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        return false;
    }
    #endregion
}
#endregion

static class Program
{
    [STAThread]
    static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new MainForm());
    }
}
