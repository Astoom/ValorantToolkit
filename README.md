# Valorant Toolkit v1.0

**by Astoom**

一个 Windows 桌面工具，用于一键修改 VALORANT（无畏契约）的游戏内分辨率和画面设置，同时支持切换 Windows 系统分辨率——专为需要拉伸分辨率的玩家设计。

---

## 功能

### 1. 自动扫描配置文件

启动后自动扫描所有磁盘分区，定位 VALORANT 的 `GameUserSettings.ini`，覆盖以下路径：

| 发行商 | 典型路径 |
|--------|----------|
| 腾讯（国服） | `:\Program Files (x86)\Tencent Games\VALORANT\live\ShooterGame\Saved\Config` |
| 腾讯（国服） | `:\Tencent Games\无畏契约\live\ShooterGame\Saved\Config` |
| Riot（国际服） | `:\Program Files\Riot Games\VALORANT\live\ShooterGame\Saved\Config` |
| Riot（国际服） | `:\Riot Games\VALORANT\live\ShooterGame\Saved\Config` |

同时检查 `%LOCALAPPDATA%\VALORANT\Saved\Config`，确保覆盖所有可能的安装位置。

扫描结果以勾选列表展示，默认全选。右键列表项可：
- **打开文件** — 用记事本查看当前配置
- **打开所在文件夹** — 在资源管理器中定位
- **复制路径** — 复制到剪贴板

### 2. 手动指定路径

支持手动输入或浏览选择任意 `GameUserSettings.ini` 文件路径。**手动路径优先级最高**，会和应用勾选的扫描结果一起写入。

### 3. 游戏分辨率预设

内建 5 种常用预设 + 自定义模式：

| 预设 | 分辨率 | 宽高比 |
|------|--------|--------|
| 1440×1080 | 1440 × 1080 | 4:3（常用拉伸） |
| 1280×960 | 1280 × 960 | 4:3 |
| 1024×768 | 1024 × 768 | 4:3 |
| 1920×1080 | 1920 × 1080 | 16:9（原生） |
| 1280×1024 | 1280 × 1024 | 5:4 |
| 自定义… | 手动输入 | 任意 |

选择"自定义…"后可手动输入任意 X/Y 数值。

### 4. 配置预览

实时预览将要写入的完整 INI 内容，包括：

- **`[ShooterGameUserSettings]`** — 分辨率、全屏模式 (FullscreenMode=2)、垂直同步关闭、动态分辨率关闭、HDR 关闭、帧率限制 0（无限制）
- **`[ScalabilityGroups]`** — 全部画质选项设为最高（3），贴图质量 2
- **`[Internationalization]`** — 语言设为 `zh-CN`
- **`[ShaderPipelineCache]`** — ShooterGame 缓存

预览随分辨率选择实时更新，所见即所得。

### 5. 系统分辨率切换

独立于游戏配置，直接切换 Windows 桌面分辨率。适合在进游戏前先将系统切到想要的拉伸分辨率：

| 预设 | 分辨率 | 宽高比 |
|------|--------|--------|
| 2560×1440 | 2560 × 1440 | 16:9 2K |
| 1920×1440 | 1920 × 1440 | 4:3 |
| 1920×1080 | 1920 × 1080 | 16:9 FHD |
| 1440×1080 | 1440 × 1080 | 4:3 拉伸 |
| 1280×960 | 1280 × 960 | 4:3 |
| 1024×768 | 1024 × 768 | 4:3 |

- 自动检测当前分辨率并匹配对应预设
- 切换前先**测试兼容性**（CDS_TEST），通过后才正式应用
- 应用后写入注册表持久化（CDS_UPDATEREGISTRY）

### 6. 安全机制

- **自动备份** — 首次修改时自动创建 `.bak` 备份文件，后续修改不会覆盖已有备份
- **只读处理** — 自动去除再恢复文件的只读属性
- **确认对话框** — 应用前列出所有目标文件，用户确认后写入
- **错误处理**：
  - 权限不足 → 提示以管理员身份运行
  - 文件被占用 → 提示关闭游戏和反作弊程序
  - 路径不存在 → 询问是否仍要创建

---

## 系统要求

| 项目 | 说明 |
|------|------|
| 操作系统 | Windows 10 / 11 (x64) |
| 运行时 | 无需安装 — 自包含发布，双击即用 |
| 权限 | 建议**以管理员身份运行**（游戏目录可能有权限保护） |
| 游戏版本 | 支持国服（腾讯）和国际服（Riot） |

---

## 使用方法

1. 从 [Releases](../../releases) 下载 `ValorantConfigTool.exe`
2. **右键 → 以管理员身份运行**
3. 程序自动扫描已安装的 VALORANT 配置文件
4. 勾选要修改的配置（或手动指定路径）
5. 在下拉菜单选择分辨率预设，或选"自定义…"手动输入
6. 在预览区确认配置内容
7. 点击 **✅ 应用配置**
8. 如需切换系统分辨率，在下拉菜单选择后点击 **切换**

---

## 自行编译

```bash
# 安装 .NET 8.0 SDK
# https://dotnet.microsoft.com/download/dotnet/8.0

# 克隆仓库
git clone https://github.com/Astoom/ValorantToolkit.git
cd ValorantToolkit

# 发布（生成单个 exe）
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

输出文件位于 `bin/Release/net8.0-windows/win-x64/publish/ValorantConfigTool.exe`

---

## 技术栈

- **语言** — C# (Windows Forms)
- **框架** — .NET 8.0
- **发布模式** — Self‑contained single file (PublishSingleFile)
- **系统 API** — `user32.dll` (`ChangeDisplaySettings` / `EnumDisplaySettings`)
- **无第三方依赖** — 纯 .NET 标准库

---

## 文件结构

```
ValorantToolkit/
├── Program.cs                  # 全部源码（Scanner / ConfigBuilder / DisplayChanger / MainForm）
├── ValorantConfigTool.csproj   # 项目文件
├── GameUserSettings.ini        # 配置模板参考
├── .gitignore
└── README.md
```

---

## 注意事项

- **关闭游戏** — 修改配置前请先退出 VALORANT 和反作弊程序（VGUARD），否则文件被占用无法写入
- **备份** — 程序会自动创建 `.bak` 备份，如需恢复，删除修改后的 `.ini` 并将 `.bak` 重命名即可
- **系统分辨率** — 切换系统分辨率仅影响 Windows 桌面，不影响其他程序的数据。如切换后黑屏，Windows 会在 15 秒后自动恢复
- **拉伸分辨率** — 想要 4:3 拉伸效果（去除黑边），需先在 NVIDIA 控制面板 / AMD Software 中将缩放模式设为"全屏"

---

## License

MIT License — 仅供学习交流使用。对游戏文件的修改行为请自行承担风险。
