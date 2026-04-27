# 中键双击过滤模块 — 开发 SOP

> 附加至 NumLockIndicator 项目（net8.0-windows / WPF）

---

## 1. 需求描述

鼠标中键硬件故障，单击随机触发双击。需要在系统层拦截异常的第二次中键按下事件，使其对所有应用透明，表现为正常单击。

---

## 2. 新增文件

```
NumLockIndicator/
└── MiddleButtonFilter.cs   ← 新增，全部逻辑封装于此
```

其余文件仅做小幅修改（见第 4 节）。

---

## 3. MiddleButtonFilter.cs 实现规格

### 3.1 类定义

```csharp
namespace NumLockIndicator;

internal sealed class MiddleButtonFilter : IDisposable
```

### 3.2 P/Invoke 声明（文件顶部）

```csharp
[DllImport("user32.dll")] static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);
[DllImport("user32.dll")] static extern bool   UnhookWindowsHookEx(IntPtr hhk);
[DllImport("user32.dll")] static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
[DllImport("kernel32.dll", CharSet = CharSet.Auto)] static extern IntPtr GetModuleHandle(string lpModuleName);
[DllImport("user32.dll")] static extern uint   GetDoubleClickTime();

delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

const int WH_MOUSE_LL    = 14;
const int WM_MBUTTONDOWN = 0x0207;
const int HC_ACTION      = 0;
```

### 3.3 字段

```csharp
private IntPtr _hookId = IntPtr.Zero;
private readonly LowLevelMouseProc _proc;   // 必须持有引用，防止 GC 回收委托
private long _lastTickMs = 0;               // 上次中键按下的 Environment.TickCount64
public bool IsEnabled { get; set; } = true; // 运行时开关
```

### 3.4 构造 / 析构

```csharp
public MiddleButtonFilter()
{
    _proc = HookCallback;
    using var curProcess = System.Diagnostics.Process.GetCurrentProcess();
    using var curModule  = curProcess.MainModule!;
    _hookId = SetWindowsHookEx(WH_MOUSE_LL, _proc,
                GetModuleHandle(curModule.ModuleName!), 0);
}

public void Dispose()
{
    if (_hookId != IntPtr.Zero)
    {
        UnhookWindowsHookEx(_hookId);
        _hookId = IntPtr.Zero;
    }
}
```

### 3.5 核心回调逻辑

```csharp
private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
{
    if (nCode == HC_ACTION && IsEnabled && wParam == WM_MBUTTONDOWN)
    {
        long now       = Environment.TickCount64;
        long threshold = GetDoubleClickTime();          // 读取系统设置（默认 500ms）
        long interval  = now - _lastTickMs;

        if (_lastTickMs != 0 && interval <= threshold)
        {
            // 判定为双击误触，拦截第二次按下
            _lastTickMs = 0;                            // 重置，避免第三次也被拦
            return (IntPtr)1;                           // 非零 = 吞掉事件
        }

        _lastTickMs = now;
    }

    return CallNextHookEx(_hookId, nCode, wParam, lParam);
}
```

> **为什么不需要补发单击**：第一次 `MBUTTONDOWN` 已正常放行，只需吞掉误触的第二次。

---

## 4. 修改现有文件

### 4.1 AppSettings.cs

添加一个字段：

```csharp
public bool MiddleButtonFilterEnabled { get; set; } = true;
```

### 4.2 App.xaml.cs

```csharp
// 字段
private MiddleButtonFilter? _middleButtonFilter;

// OnStartup 末尾添加
if (_settings.MiddleButtonFilterEnabled)
    _middleButtonFilter = new MiddleButtonFilter();

// OnExit 中添加（在 AppSettings.Save() 之前）
_middleButtonFilter?.Dispose();
```

### 4.3 TrayManager.cs

在右键菜单"设置…"上方添加一项开关菜单：

```csharp
// 构造函数参数新增
MiddleButtonFilter? filter

// 菜单项
var menuFilter = new MenuItem { Header = "中键过滤", IsCheckable = true };
menuFilter.IsChecked = filter?.IsEnabled ?? false;
menuFilter.Click += (_, _) =>
{
    if (filter != null)
    {
        filter.IsEnabled = !filter.IsEnabled;
        menuFilter.IsChecked = filter.IsEnabled;
        _settings.MiddleButtonFilterEnabled = filter.IsEnabled;
    }
};
```

> TrayManager 构造调用处（App.xaml.cs）同步更新传参。

---

## 5. .csproj 无需修改

所有 API（`user32.dll`、`kernel32.dll`）均为系统内置，无需新增 NuGet 包。

---

## 6. 注意事项

| 事项 | 说明 |
|---|---|
| 委托生命周期 | `_proc` 必须作为字段持有，不能用局部变量，否则 GC 回收后 Hook 崩溃 |
| 回调速度 | `HookCallback` 内禁止任何 I/O 或耗时操作，系统检测超时会自动移除 Hook |
| 管理员权限 | 普通权限已可 Hook 全局鼠标；如需钩取 UAC 提升进程，需以管理员运行 |
| 阈值说明 | `GetDoubleClickTime()` 读取"控制面板 → 鼠标 → 双击速度"，与系统设置联动 |
| Hook 失败处理 | 建议在构造后检查 `_hookId != IntPtr.Zero`，失败时写 `Debug.WriteLine` |

---

## 7. 测试验收

1. 启动程序，托盘菜单确认"中键过滤"已勾选
2. 在任意窗口快速连击中键两次，第二次应被吞掉（浏览器不会打开新标签 / 关闭标签）
3. 取消勾选"中键过滤"，双击中键恢复原始行为
4. 正常慢速单击中键，功能不受影响
5. 退出并重启，过滤状态与上次一致（通过 settings.json 持久化）




好的，我已经了解了你的项目背景。这是一个 .NET 8.0 WPF 项目 **NumLockIndicator**，现在需要为其添加一个「鼠标中键双击拦截→转为单击」的功能模块，并生成一份供 AI 阅读的 `sop.md`（标准操作文档）。下面是完整的 `sop.md` 内容：

---

```markdown
# SOP: 鼠标中键双击修复模块

## 项目背景

本项目为 `NumLockIndicator`，基于 .NET 8.0 / WPF，用于系统托盘指示器显示。
当前问题：物理鼠标中键（滚轮键）硬件损坏，单次按压随机触发双击事件（`WM_MBUTTONDBLCLK`），
需要在软件层拦截此异常行为，将其强制还原为单击（`WM_MBUTTONDOWN` + `WM_MBUTTONUP`）。

---

## 目标

在现有项目中新增一个 `MiddleClickFixer` 模块，满足以下要求：

1. 使用 Windows **低级鼠标钩子**（`WH_MOUSE_LL`）全局监听鼠标消息
2. 检测到 `WM_MBUTTONDBLCLK`（双击中键）时，**拦截该事件**（不传递给系统）
3. 补发一组标准的 `WM_MBUTTONDOWN` + `WM_MBUTTONUP` 消息，模拟正常单击
4. 该功能可在托盘菜单中**开关切换**（与现有 `TrayManager` 集成）
5. 开关状态持久化到现有的 `AppSettings`

---

## 项目结构（现有）

```
NumLockIndicator/
├── App.xaml / App.xaml.cs          # 应用入口
├── AppSettings.cs                  # 设置读写（JSON 持久化）
├── IndicatorWindow.xaml/.cs        # 指示器悬浮窗
├── SettingsWindow.xaml/.cs         # 设置窗口
├── SnapManager.cs                  # 窗口吸附逻辑
├── TrayManager.cs                  # 托盘图标与菜单管理
├── NumLockIndicator.csproj         # .NET 8.0 项目文件
```

---

## 新增文件

### `MiddleClickFixer.cs`

**职责：** 全局低级鼠标钩子，拦截双击中键并补发单击。

**需要实现的内容：**

```csharp
// 命名空间：NumLockIndicator
// 依赖：System.Runtime.InteropServices, PInvoke / user32.dll

public class MiddleClickFixer : IDisposable
{
    // 1. 安装 WH_MOUSE_LL 钩子
    public void Install();

    // 2. 卸载钩子
    public void Uninstall();

    // 3. 钩子回调：
    //    - 若消息为 WM_MBUTTONDBLCLK → 返回非零值（拦截）
    //      并调用 SendInput 补发 MOUSEEVENTF_MIDDLEDOWN + MOUSEEVENTF_MIDDLEUP
    //    - 其他消息 → 调用 CallNextHookEx 正常传递

    // 4. IsEnabled 属性：控制是否生效（false 时即使安装也直接透传）

    // P/Invoke 需要：
    // SetWindowsHookEx, UnhookWindowsHookEx, CallNextHookEx
    // SendInput, INPUT struct, MOUSEINPUT struct
    // WM_MBUTTONDBLCLK = 0x0209
}
```

**关键 P/Invoke 常量：**

| 名称 | 值 |
|---|---|
| `WH_MOUSE_LL` | `14` |
| `WM_MBUTTONDOWN` | `0x0207` |
| `WM_MBUTTONUP` | `0x0208` |
| `WM_MBUTTONDBLCLK` | `0x0209` |
| `MOUSEEVENTF_MIDDLEDOWN` | `0x0020` |
| `MOUSEEVENTF_MIDDLEUP` | `0x0040` |

---

## 修改现有文件

### `AppSettings.cs`

新增属性：

```csharp
public bool MiddleClickFixerEnabled { get; set; } = true;
```

---

### `TrayManager.cs`

在托盘右键菜单中新增菜单项：

- 菜单文字：`"中键修复 (已启用)"` / `"中键修复 (已禁用)"`
- 点击时切换 `MiddleClickFixer.IsEnabled`，同步更新 `AppSettings.MiddleClickFixerEnabled` 并保存
- 菜单项显示勾选状态（`Checked`）

---

### `App.xaml.cs`

- 在应用启动时实例化 `MiddleClickFixer`，根据 `AppSettings.MiddleClickFixerEnabled` 决定是否调用 `Install()`
- 在应用退出时调用 `Uninstall()` 并 `Dispose()`
- 将实例传递给 `TrayManager` 以便菜单交互

---

## 实现注意事项

1. **钩子必须在 UI 线程（STA）安装**，否则低级钩子不会收到消息回调。
2. `LowLevelMouseProc` 回调中**不能执行耗时操作**，拦截和补发需在回调内同步完成。
3. 使用 `SendInput` 补发时，需将 `INPUT.type` 设为 `INPUT_MOUSE`，坐标字段填 `0`（相对移动为 0），仅设置 `dwFlags`。
4. 为避免补发的 `MIDDLEDOWN/UP` 再次触发钩子形成循环，可用一个 `bool _isSending` 标志位在回调中跳过自己补发的事件。
5. `MiddleClickFixer` 实现 `IDisposable`，确保在任何退出路径下都能卸载钩子，防止系统钩子泄漏。
6. 项目目标框架为 `.NET 8.0`，P/Invoke 使用 `[DllImport("user32.dll")]` 即可，无需额外 NuGet 包。

---

## 验收标准

- [ ] 物理单击中键 → 正常触发一次中键单击事件
- [ ] 物理损坏导致的双击中键 → 被拦截，仅输出一次单击事件
- [ ] 托盘菜单可切换功能开关，状态即时生效
- [ ] 重启应用后开关状态保持上次设置
- [ ] 应用退出后钩子被正确卸载，无残留

---

## 文件清单（完成后）

```
NumLockIndicator/
├── MiddleClickFixer.cs             ← 新增
├── AppSettings.cs                  ← 修改（新增属性）
├── TrayManager.cs                  ← 修改（新增菜单项）
├── App.xaml.cs                     ← 修改（生命周期管理）
└── ...（其余文件不变）
```
```

---

这份 `sop.md` 的设计思路是让 AI 能够**直接上手编码**，无需再问你"项目结构是什么"或"用什么框架"。文档涵盖了：背景与目标、精确的 Win32 常量、每个文件的改动范围、以及实现时最容易踩坑的注意事项（特别是防循环触发的 `_isSending` 标志位和 STA 线程要求）。把这份 `sop.md` 放到项目根目录，直接丢给 AI 即可。
