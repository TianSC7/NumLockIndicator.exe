# NumLock Indicator

一个轻量级的 WPF 桌面工具，用于实时显示 NumLock 键的状态。

## 功能

- **实时状态显示** — 浮动透明窗口显示 NumLock 开/关状态，带有颜色变化和动画效果
- **系统托盘** — 最小化到系统托盘，左键点击切换窗口显示/隐藏
- **可自定义设置** — 字体、字号、窗口尺寸、ON/OFF 显示文字均可配置
- **窗口位置记忆** — 自动保存并恢复窗口位置
- **单实例运行** — 通过 Mutex 确保只有一个实例运行
- **双击打开键盘设置** — 双击指示器窗口快速打开 Windows 键盘设置

## 技术栈

- **.NET 8** (WPF, `net8.0-windows`)
- **Hardcodet.NotifyIcon.Wpf** 2.0.1 — 系统托盘图标支持
- **System.Text.Json** — 设置持久化

## 界面预览

| NumLock 开启 | NumLock 关闭 |
|:---:|:---:|
| 绿色半透明背景 + 绿色指示点 | 红色半透明背景 + 黄色指示点 |

## 构建与运行

```bash
# 还原依赖
dotnet restore

# 构建项目
dotnet build

# 运行
dotnet run

# 发布为单文件 (Windows)
dotnet publish -c Release -r win-x64 --self-contained false
```

## 设置

右键点击托盘图标 → **设置...** 可打开设置窗口：

- **字体** — 从系统已安装字体中选择
- **字体大小** — 12 ~ 72
- **窗口宽度/高度** — 自定义指示器窗口尺寸
- **ON 文字 / OFF 文字** — 自定义显示的状态文本
- **实时预览** — 设置窗口内可直接预览效果

设置文件保存位置：`%APPDATA%\NumLockIndicator\settings.json`

## 许可

MIT License
