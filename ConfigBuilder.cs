using System;
using System.IO;
using System.Windows.Forms;

namespace ValorantConfigTool;

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
