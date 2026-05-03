# AGENTS.md — NumLock Indicator

## Project Overview

A lightweight WPF desktop utility that displays NumLock and CapsLock key states as floating transparent overlay windows with system tray integration and a middle-mouse-button double-click filter.

**Tech stack**: .NET 8 (`net8.0-windows`), C# 12, WPF + XAML, Nullable enabled.
**UI language**: Mixed Chinese/English — menu items and labels are in Chinese, code identifiers in English.
**Solution file**: `num lock.sln` (root). Project lives in `NumLockIndicator/`.

---

## Build & Run Commands

```bash
# All commands should be run from the NumLockIndicator/ directory (or root with path prefix)
dotnet restore
dotnet build
dotnet run

# Publish (framework-dependent)
dotnet publish -c Release -r win-x64 --self-contained false

# Publish (self-contained single file)
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true

# Publish output: bin/Release/net8.0-windows/win-x64/publish/
```

There are no automated tests in this project. Verification is manual (`dotnet run`).

---

## Project Structure

```
NumLockIndicator/
├── App.xaml                 # Application entry XAML — global ResourceDictionary (colors, button/textbox/label styles)
├── App.xaml.cs              # App lifecycle: Mutex single-instance, creates windows, SnapManager, TrayManager, MiddleButtonFilter
├── AppSettings.cs           # Settings model with JSON Load/Save to %APPDATA%\NumLockIndicator\settings.json
├── IndicatorWindow.xaml      # Floating indicator window UI (borderless, transparent, topmost, no taskbar)
├── IndicatorWindow.xaml.cs   # Key state polling via user32.dll GetKeyState, animations, drag, position save/restore
├── SnapManager.cs           # Two-window snap logic (vertical/horizontal吸附 + linked dragging)
├── MiddleButtonFilter.cs    # Global low-level mouse hook (WH_MOUSE_LL) to block rapid middle-button double-clicks
├── TrayManager.cs           # System tray icon + right-click context menu (filter toggle, settings, hide/show, exit)
├── SettingsWindow.xaml      # Settings dialog UI (font, size, window dims, ON/OFF text per key, middle-button filter)
├── SettingsWindow.xaml.cs   # Settings logic — loads from AppSettings, applies/saves, fires SettingsSaved event
├── Helpers/
│   └── TopmostManager.cs    # Centralized topmost window manager with priority-based ordering
├── NumLockIndicator.csproj  # Project file — WinExe, net8.0-windows, UseWPF, Hardcodet.NotifyIcon.Wpf 2.0.1
├── dotnet-tools.json        # ilspycmd 10.0.0.8330 (decompiler utility)
├── icon.ico                 # Tray icon (embedded as <Resource>)
├── README.md                # User-facing README (Chinese)
├── SOP.md                   # Detailed development SOP (Chinese)
└── png/                     # Documentation images
```

---

## Architecture & Data Flow

```
App.OnStartup
  ├── AppSettings.Load()                    ← %APPDATA%\NumLockIndicator\settings.json
  ├── IndicatorWindow(NumLock)  ──┐
  ├── IndicatorWindow(CapsLock) ──┤  Each window polls independently at 150ms
  ├── SnapManager(win1, win2)   ──┘  Snap detection + linked drag
  ├── MiddleButtonFilter(thresholdMs)     Global WH_MOUSE_LL hook
  └── TrayManager(windows, settings, filter)  Tray icon + context menu

Shutdown:
  App.OnExit
  ├── IndicatorWindow.SavePosition()  → writes to _settings fields
  ├── AppSettings.Save()              → writes settings.json
  ├── MiddleButtonFilter.Dispose()    → unhook + flush log
  └── TrayManager.Dispose()           → release tray icon
```

**Settings propagation**: `SettingsWindow.SettingsSaved` is a static event. `IndicatorWindow` and `App` subscribe to it and update in real time via `Dispatcher.Invoke`.

---

## Key Conventions & Patterns

- **File-scoped namespaces**: `namespace NumLockIndicator;` (no block namespaces)
- **No XML doc comments** on methods/classes
- **Nullable reference types** enabled — fields use `?` where null is intentional
- **P/Invoke**: Used directly in the classes that need it (`IndicatorWindow` for `GetKeyState`, `MiddleButtonFilter` for mouse hooks). Declarations are `private static extern` at class level.
- **Settings model**: `AppSettings` is a plain POCO with `System.Text.Json` serialization. Uses `JsonNumberHandling.AllowNamedFloatingPointLiterals` for `double.NaN` position defaults.
- **UI resources**: Colors and shared styles are centralized in `App.xaml` ResourceDictionary (e.g., `ModernButton`, `SecondaryButton`, implicit TextBox/ComboBox/Slider/Label styles).
- **Default font**: `Microsoft YaHei` throughout UI
- **Window naming**: `IndicatorWindow` is the floating overlay (not "MainWindow" — there is no MainWindow)
- **IndicatorType enum**: `NumLock`, `CapsLock` — determines virtual key code and settings fields to read

---

## Key Implementation Details

### IndicatorWindow
- Uses `user32.dll GetKeyState()` P/Invoke (not `Control.IsKeyLocked`)
- NumLock virtual key = `0x90`, CapsLock = `0x14`
- Polls every **150ms** via `DispatcherTimer`
- ON state: green semi-transparent bg (`#3027AE60`) + green dot
- OFF state: red semi-transparent bg (`#E0E74C3C`) + yellow dot
- Bounce animation on state change: ScaleTransform 1.0 → 1.08 → 1.0
- Position initialized to `double.NaN` → placed at top-right of screen
- `ClampToScreen()` ensures at least 20px visible on screen edges
- `DragMove()` on `MouseLeftButtonDown` for repositioning
- Registers with `TopmostManager` on Loaded and unregisters on Closed

### SnapManager
- Two-window snap system with threshold 10px and gap 2px
- Supports vertical and horizontal snapping
- Leader/follower model: dragging leader moves follower; dragging follower detaches
- Uses `_updating` flag to prevent recursive position change handlers

### TopmostManager
- Centralized static class for managing topmost window ordering
- Uses a single `DispatcherTimer` (1-second interval) to update all registered windows
- Priority-based ordering: lower priority values processed first, higher priority values end up on top
- Automatic timer management: stops when no windows registered, starts when first window registers
- `Register(Window, priority)` and `Unregister(Window)` methods for window lifecycle management
- Both IndicatorWindow instances register with priority=1

### MiddleButtonFilter
- Global low-level mouse hook (`WH_MOUSE_LL = 14`) via `SetWindowsHookEx`
- Blocks middle-button clicks arriving faster than threshold (default 200ms)
- Writes activity log to `middlebutton.log` in app directory via `BlockingCollection` + background thread
- Must call `Dispose()` on exit to unhook and flush log

### TrayManager
- Uses `Hardcodet.NotifyIcon.Wpf` (`TaskbarIcon`)
- Icon loaded from `pack://application:,,,/icon.ico`
- Context menu: 中键过滤 (toggle) | 设置... | 隐藏/显示 | separator | 退出
- Settings window opened as modal dialog (`ShowDialog()`), prevents duplicate instances
- `ShutdownMode="OnExplicitShutdown"` in App.xaml — app exits only via `Application.Current.Shutdown()`

---

## Gotchas & Non-Obvious Patterns

1. **Single-instance Mutex**: Named `"NumLockIndicator_SingleInstance"`. If already running, `Shutdown()` is called immediately. If the app won't start, check for a zombie process.

2. **Window position on disconnected monitors**: Saved positions are validated against `SystemParameters.PrimaryScreenWidth/Height`. If out of bounds, windows reset to top-right. If position seems lost, delete `%APPDATA%\NumLockIndicator\settings.json`.

3. **`ShutdownMode="OnExplicitShutdown"`**: App does NOT exit when all windows close. It exits only when `Application.Current.Shutdown()` is called from the tray "退出" menu or `TrayManager.ExitApp()`. Closing indicator windows just hides them.

4. **Settings propagate via static event**: `SettingsWindow.SettingsSaved` is a static `event Action?`. Multiple subscribers (both `IndicatorWindow` instances + `App`) listen. This means settings changes apply immediately without restart.

5. **MiddleButtonFilter is a global hook**: It affects ALL middle-clicks system-wide while the app runs. The hook callback runs on the thread that installed it (UI thread), so UI updates are safe, but the hook must be fast to avoid system-wide input lag.

6. **Two .sln files**: Root has `num lock.sln`, project directory has `NumLockIndicator.sln`. Both reference the same `.csproj`. Use the root one.

7. **`icon.ico` is a `<Resource>` not `<Content>`**: Loaded via `pack://` URI. If missing or not embedded, the tray icon won't display.

8. **`new` keyword on Show/Hide**: `IndicatorWindow` uses `public new void Show()` / `Hide()` to shadow the base `Window` methods. This is used by `TrayManager.ToggleWindows()`.

9. **No logging framework**: `MiddleButtonFilter` has its own file-based log. Everything else uses no logging. Debug with `Debug.WriteLine` + DebugView if needed.

---

## Dependencies

| Package | Version | Purpose |
|---|---|---|
| Hardcodet.NotifyIcon.Wpf | 2.0.1 | System tray icon |
| ilspycmd (dotnet tool) | 10.0.0.8330 | Decompiler (dev utility, not runtime) |

---

## Existing Documentation

- `NumLockIndicator/README.md` — User-facing feature overview and build instructions (Chinese)
- `NumLockIndicator/SOP.md` — Detailed development SOP with architecture, debugging guide, and feature-addition recipes (Chinese)
- `sop.txt` — Earlier version of the SOP (Chinese)
- `NumLockIndicator/png/MiddleButtonFilter_SOP.md` — MiddleButtonFilter-specific SOP
