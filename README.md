# Valorant Toolkit v2.0

**by Astoom**

一个 Windows 桌面工具，用于一键修改 VALORANT（无畏契约）的游戏内分辨率和画面设置，同时支持切换 Windows 系统分辨率——专为需要拉伸分辨率的玩家设计。

---

## 功能

### 1. 智能扫描配置文件

启动后先尝试缓存、再快速扫描已知路径、再浅层搜索非标准路径、最后通过 Everything SDK 全盘秒搜。

**扫描分四层：**

- **缓存加载** — 启动时优先从 `%LOCALAPPDATA%\ValorantToolkit\path_cache.json` 加载上次扫描结果（7 天内有效），无需重新扫描，启动即用。

- **快速扫描（已知路径）** — 检查标准安装路径：

| 路径 |
|------|
| `:\Program Files (x86)\Tencent Games\VALORANT\live\ShooterGame\Saved\Config` |
| `:\Program Files\Tencent Games\VALORANT\live\ShooterGame\Saved\Config` |
| `:\Tencent Games\VALORANT\live\ShooterGame\Saved\Config` |
| `%LOCALAPPDATA%\VALORANT\Saved\Config` |

- **浅层搜索（网吧等非标准路径）** — 遍历盘符根目录最多 2 层，匹配关键词（无畏契约 / VALORANT / 腾讯 / 游戏 / Games 等），覆盖 `D:\网络游戏\无畏契约\` 等自定义安装。

- **Everything SDK 全盘秒搜** — 如果 Everything 已安装并运行，通过 SDK 直接查询索引数据库，**9ms 内**全盘定位所有 `GameUserSettings.ini`，然后通过内容校验确认是否为 VALORANT 配置。

扫描结果以勾选列表展示，默认全选。右键列表项可：
- **打开文件** — 用记事本查看当前配置
- **打开所在文件夹** — 在资源管理器中定位
- **复制路径** — 复制到剪贴板

### 2. 手动指定路径

支持手动输入或浏览选择任意 `GameUserSettings.ini` 文件路径。**手动路径优先级最高**，会和勾选的扫描结果一起写入。

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

- **内容校验** — 所有找到的 `GameUserSettings.ini` 必须通过内容验证（包含 VALORANT 特有节头 + 关键字段），防止误改其他游戏的配置
- **自动备份** — 首次修改时自动创建 `.bak` 备份文件，后续修改不会覆盖已有备份
- **只读处理** — 自动去除再恢复文件的只读属性
- **确认对话框** — 应用前列出所有目标文件，用户确认后写入
- **错误处理**：
  - 权限不足 → 提示以管理员身份运行
  - 文件被占用 → 提示关闭游戏
  - 路径不存在 → 询问是否仍要创建

---

## 系统要求

| 项目 | 说明 |
|------|------|
| 操作系统 | Windows 10 / 11 (x64) |
| 运行时 | 无需安装 — 自包含发布，双击即用 |
| 权限 | 建议**以管理员身份运行**（游戏目录可能有权限保护） |
| 游戏版本 | 仅支持国服（腾讯无畏契约） |

---

## 使用方法

1. 从 [Releases](../../releases) 下载 `ValorantConfigTool.exe` 和 `Everything64.dll`
2. 将两个文件放在**同一个文件夹**
3. **右键 → 以管理员身份运行**
4. 程序自动扫描已安装的 VALORANT 配置文件（缓存 → 快速路径 → 浅层搜索 → Everything 秒搜）
5. 勾选要修改的配置（或手动指定路径）
6. 在下拉菜单选择分辨率预设，或选"自定义…"手动输入
7. 在预览区确认配置内容
8. 点击 **✅ 应用配置**
9. 如需切换系统分辨率，在下拉菜单选择后点击 **切换**

---

## 自行编译

```bash
# 安装 .NET 8.0 SDK
# https://dotnet.microsoft.com/download/dotnet/8.0

# 克隆仓库
git clone https://github.com/Astoom/ValorantToolkit.git
cd ValorantToolkit

# 发布（生成单个 exe + Everything64.dll）
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

输出文件位于 `bin/Release/net8.0-windows/win-x64/publish/`

---

## 技术栈

| 项目 | 说明 |
|------|------|
| 语言 | C# (Windows Forms) |
| 框架 | .NET 8.0 |
| 发布模式 | Self-contained single file (PublishSingleFile) |
| 文件搜索 | Everything SDK (`Everything64.dll`) + 原生快速扫描 |
| 内容校验 | INI 节头 + 关键字段双重验证 |
| 路径缓存 | JSON 文件缓存，7 天有效期 |
| 系统 API | `user32.dll` (`ChangeDisplaySettings` / `EnumDisplaySettings`) |
| 第三方依赖 | Everything SDK DLL（随发布包附带） |

---

## 文件结构

```
ValorantToolkit/
├── Program.cs                  # 入口点，高 DPI 初始化
├── MainForm.cs                 # Windows Forms UI
├── Scanner.cs                  # 四层扫描：缓存 + 快速 + 浅层 + Everything SDK
├── ConfigBuilder.cs            # INI 配置模板构建与安全写入
├── DisplayChanger.cs           # Windows 桌面分辨率切换
├── ContentValidator.cs         # 基于内容的 INI 真伪验证
├── EverythingSdk.cs            # Everything SDK 托管封装（运行时 DLL 加载）
├── EverythingNative.cs         # Everything64.dll P/Invoke 声明
├── PathCache.cs                # JSON 缓存读写
├── ValorantConfigTool.csproj   # 项目文件
├── Everything64.dll            # Everything SDK 原生 DLL（随发布附带）
├── GameUserSettings.ini        # 配置模板参考
├── .gitignore
└── README.md
```

---

## 注意事项

- **关闭游戏** — 修改配置前请先退出 VALORANT，否则文件被占用无法写入
- **备份** — 程序会自动创建 `.bak` 备份，如需恢复，删除修改后的 `.ini` 并将 `.bak` 重命名即可
- **系统分辨率** — 切换系统分辨率仅影响 Windows 桌面。如切换后黑屏，Windows 会在 15 秒后自动恢复
- **拉伸分辨率** — 想要 4:3 拉伸效果（去除黑边），需先在 NVIDIA 控制面板 / AMD Software 中将缩放模式设为"全屏"
- **Everything** — 如果电脑未安装 Everything，程序仍可通过快速扫描和浅层搜索找到配置文件，只是没有全盘秒搜加速

---

## License

MIT License — 仅供学习交流使用。对游戏文件的修改行为请自行承担风险。
