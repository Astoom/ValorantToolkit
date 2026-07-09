using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ValorantConfigTool;

sealed class MainForm : Form
{
    CheckedListBox _listBox = null!;
    TextBox _manualPath = null!;
    TextBox _preview = null!;
    Button _btnScan = null!;
    Button _btnApply = null!;
    ToolStripStatusLabel _status = null!;
    ToolStripStatusLabel _statusEverything = null!;
    ToolStripStatusLabel _statusCache = null!;
    ComboBox _resPreset = null!;
    TextBox _resX = null!;
    TextBox _resY = null!;
    CancellationTokenSource? _scanCts;

    public MainForm()
    {
        AutoScaleMode = AutoScaleMode.Dpi;
        Text = "Valorant Toolkit v1.0 by Astoom";
        ClientSize = new Size(820, 660);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        Font = new Font("Microsoft YaHei UI", 9f);

        BuildUI();
        Load += (_, _) =>
        {
            BeginInvoke(() =>
            {
                UpdateEverythingStatus();
                DoScan(forceRefresh: false);
                RefreshPreview();
            });
        };
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
        _btnScan = MakeButton("🔍 重新扫描", pad, y, 120, 32, (_, _) => DoScan(forceRefresh: true), bold: true);
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
        _status = new ToolStripStatusLabel("就绪") { Spring = true };
        _statusEverything = new ToolStripStatusLabel("");
        _statusCache = new ToolStripStatusLabel("");
        ss.Items.Add(_status);
        ss.Items.Add(_statusEverything);
        ss.Items.Add(_statusCache);
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
    void DoScan(bool forceRefresh = false)
    {
        // Cancel any in-flight scan
        _scanCts?.Cancel();
        _scanCts = new CancellationTokenSource();
        var cancel = _scanCts.Token;

        _listBox.Items.Clear();

        // Try cache first (unless forced refresh)
        if (!forceRefresh)
        {
            List<string>? cached = PathCache.Load();
            if (cached != null)
            {
                foreach (string p in cached)
                    _listBox.Items.Add(p, true);

                _status.Text = $"已从缓存加载 — 找到 {cached.Count} 个目标";
                UpdateCacheStatus($"缓存 ({cached.Count} 个路径)");
                UpdateEverythingStatus();
                return;
            }
        }

        // No valid cache or forced refresh — run scan
        UpdateCacheStatus(null);
        UpdateEverythingStatus();
        SetBusy(true, "正在扫描...");

        Task.Run(() =>
        {
            var progress = new Progress<string>(msg =>
            {
                try { Invoke(() => _status.Text = msg); }
                catch { /* form closed */ }
            });

            var (results, source) = Scanner.ScanAll(progress, cancel);

            try
            {
                Invoke(() =>
                {
                    if (cancel.IsCancellationRequested) return;

                    foreach (string p in results)
                        _listBox.Items.Add(p, true);

                    _status.Text = results.Count > 0
                        ? $"扫描完成 — 找到 {results.Count} 个目标 ({source})"
                        : "扫描完成 — 未找到 (请确认游戏已安装并至少启动过一次)";

                    // Save results to cache
                    PathCache.Save(results, source);
                    UpdateCacheStatus($"缓存 ({results.Count} 个路径)");

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

    void UpdateEverythingStatus()
    {
        if (EverythingSdk.IsAvailable)
        {
            _statusEverything.Text = EverythingSdk.Version != null
                ? $"Everything {EverythingSdk.Version}"
                : "Everything";
            _statusEverything.ForeColor = Color.Green;
        }
        else
        {
            _statusEverything.Text = "Everything: N/A";
            _statusEverything.ForeColor = Color.Gray;
        }
    }

    void UpdateCacheStatus(string? message)
    {
        _statusCache.Text = message ?? "";
        _statusCache.ForeColor = message != null ? Color.Gray : SystemColors.ControlText;
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
