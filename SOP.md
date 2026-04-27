# NumLock Indicator — 开发与维护 SOP

## 1. 项目概览

| 项 | 值 |
|---|---|
| 项目名 | NumLockIndicator |
| 类型 | WPF 桌面应用（WinExe） |
| 目标框架 | `net8.0-windows` |
| 语言 | C# 12 / Nullable enabled |
| UI 框架 | WPF + XAML |
| 关键 NuGet | `Hardcodet.NotifyIcon.Wpf` 2.0.1 |
| 设置持久化 | `System.Text.Json` → `%APPDATA%\NumLockIndicator\settings.json` |
| 单实例机制 | `Mutex("NumLockIndicator_SingleInstance")` |

---

## 2. 文件结构

```
NumLockIndicator/
├── App.xaml              # 应用入口 XAML，全局资源字典（颜色/按钮样式）
├── App.xaml.cs           # 应用生命周期：启动 → 创建窗口 → 退出 → 保存
├── AppSettings.cs        # 设置模型 + Load/Save（JSON）
├── IndicatorWindow.xaml   # 指示器窗口 UI（无边框透明 Topmost）
├── IndicatorWindow.xaml.cs # 指示器逻辑：轮询键状态、动画、拖拽、位置记忆
├── SnapManager.cs        # 双窗口吸附逻辑（垂直/水平吸附 + 联动拖拽）
├── TrayManager.cs        # 系统托盘图标 + 右键菜单
├── SettingsWindow.xaml   # 设置窗口 UI
├── SettingsWindow.xaml.cs # 设置窗口逻辑（加载/应用/保存）
├── NumLockIndicator.csproj
├── NumLockIndicator.sln
├── icon.ico              # 托盘图标资源
├── dotnet-tools.json     # ilspycmd 工具
└── README.md
```

---

## 3. 架构与数据流

```
App.OnStartup
  ├── AppSettings.Load()              ← settings.json
  ├── IndicatorWindow(NumLock)  ──┐
  ├── IndicatorWindow(CapsLock) ──┤  每个窗口独立 150ms 轮询
  ├── SnapManager(win1, win2)    ──┘  吸附 + 联动拖拽
  └── TrayManager(win1, win2, settings)  托盘图标 + 菜单

退出流程:
  App.OnExit
  ├── IndicatorWindow.SavePosition()  → 写入 _settings
  ├── AppSettings.Save()              → 写入 settings.json
  └── TrayManager.Dispose()           → 释放托盘图标
```

---

## 4. 核心模块说明

### 4.1 IndicatorWindow（指示器窗口）

- **键状态检测**：通过 `user32.dll → GetKeyState()` P/Invoke
  - NumLock 虚拟键码 `0x90`
  - CapsLock 虚拟键码 `0x14`
- **轮询方式**：`DispatcherTimer`，间隔 150ms
- **状态变更动画**：Storyboard ScaleTransform（弹跳效果 1.0→1.08→1.0）
- **视觉状态**：
  - ON → 绿色半透明背景 `#3027AE60` + 绿色指示点
  - OFF → 红色半透明背景 `#E0E74C3C` + 黄色指示点
- **拖拽**：`MouseLeftButtonDown → DragMove()`，自动吸附到屏幕边缘（至少保留 20px 可见）

### 4.2 SnapManager（窗口吸附）

- 检测两个 IndicatorWindow 之间的距离
- 吸附阈值默认 10px，吸附间距默认 2px
- 支持垂直和水平吸附
- 吸附后拖拽 leader 窗口时 follower 联动移动
- 拖拽 follower 窗口会解除吸附

### 4.3 AppSettings（设置管理）

- 默认值：字号 20，窗口 120×50，字体 Microsoft YaHei
- 位置字段初始化为 `double.NaN`（首次启动时自动定位到屏幕右上角）
- JSON 序列化选项：`WriteIndented = true`，支持 `NaN` 字面量

### 4.4 TrayManager（系统托盘）

- 使用 `Hardcodet.NotifyIcon.Wpf`
- 右键菜单：设置… | 隐藏/显示 | 退出
- 设置窗口通过 `ShowDialog()` 模态打开，防止重复实例

---

## 5. 构建与发布 SOP

### 5.1 环境要求

- .NET 8 SDK（`net8.0-windows`）
- Windows 10/11

### 5.2 日常构建

```bash
dotnet restore
dotnet build
dotnet run
```

### 5.3 发布 Release

```bash
# 框架依赖（需目标机器安装 .NET 8 Runtime）
dotnet publish -c Release -r win-x64 --self-contained false

# 自包含单文件（无需安装 Runtime，体积较大）
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true

# 发布产物位于 bin/Release/net8.0-windows/win-x64/publish/
```

---

## 6. 添加新功能的 SOP

### 6.1 添加新的 Lock 键指示器（如 ScrollLock）

1. **AppSettings.cs** — 添加 ScrollLock 相关字段（位置、ON/OFF 文字）
2. **IndicatorWindow.xaml.cs** — 无需修改，`IndicatorType` 枚举添加 `ScrollLock`，`GetCurrentState()` 添加 `0x91` 检测
3. **App.xaml.cs** — 创建第三个 `IndicatorWindow(_settings, IndicatorType.ScrollLock)`，传入 `SnapManager`
4. **SnapManager.cs** — 如需三窗口吸附，需重构为 `List<IndicatorWindow>` 模式
5. **SettingsWindow.xaml(.cs)** — 添加 ScrollLock ON/OFF 文本设置
6. **TrayManager.cs** — 将新窗口加入 `_windows` 列表

### 6.2 修改轮询方式为 Hook

当前使用 150ms `DispatcherTimer` 轮询，如需切换为低延迟键盘 Hook：

1. 使用 `SetWindowsHookEx(WH_KEYBOARD_LL, ...)` 全局低级键盘钩子
2. 在 `App.xaml.cs` 中注册 Hook，回调中判断 `vkCode == 0x90/0x14`
3. 通过事件/委托通知对应 `IndicatorWindow` 更新状态
4. 移除 `DispatcherTimer` 轮询
5. 注意：Hook 回调在非 UI 线程，需 `Dispatcher.Invoke` 更新 UI

### 6.3 添加开机自启动

1. 在 `TrayManager` 右键菜单增加"开机自启"选项
2. 使用 `Microsoft.Win32.Registry` 在 `HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Run` 添加/删除键值
3. 在 `AppSettings` 中增加 `AutoStart` 布尔字段

---

## 7. 调试指南

### 7.1 常见问题

| 问题 | 原因 | 解决 |
|---|---|---|
| 启动后立即退出 | 已有实例在运行（Mutex） | 关闭已有实例或杀进程 |
| 窗口不在屏幕上 | 保存的位置在已断开的显示器上 | 删除 `%APPDATA%\NumLockIndicator\settings.json` |
| 托盘图标不显示 | `icon.ico` 资源缺失 | 确保 `icon.ico` 在项目根目录且标记为 `<Resource>` |
| 设置保存失败 | `%APPDATA%` 目录权限问题 | 检查 `AppSettings.SettingsDir` 写入权限 |

### 7.2 日志

当前无日志系统。如需调试，可在 `App.OnStartup` 中附加 `Console.WriteLine` 或使用 `Debug.WriteLine` + DebugView 工具。

---

## 8. 代码风格约定

- 文件范围命名空间（`namespace NumLockIndicator;`）
- Nullable 引用类型启用
- 无 XML 文档注释
- 中英文混合 UI 文本（按钮/菜单用中文，代码标识符用英文）
- XAML 资源集中定义在 `App.xaml` 的 `ResourceDictionary`
- 中文 UI 字体默认 `Microsoft YaHei`

---

## 9. 依赖管理

| 包 | 版本 | 用途 |
|---|---|---|
| Hardcodet.NotifyIcon.Wpf | 2.0.1 | 系统托盘图标 |
| ilspycmd (tool) | 10.0.0.8330 | 反编译工具（开发辅助） |

升级依赖：
```bash
dotnet list package --outdated
dotnet add package Hardcodet.NotifyIcon.Wpf
```

---

## 10. Git 工作流

```bash
# 分支策略
main        ← 稳定发布
dev         ← 日常开发
feature/*   ← 功能分支

# 提交前
dotnet build               # 确保编译通过
dotnet run                  # 手动验证功能

# .gitignore 已覆盖 bin/ obj/ .vs/ 等
```
