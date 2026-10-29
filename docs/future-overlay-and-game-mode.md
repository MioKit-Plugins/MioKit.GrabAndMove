# 后续功能：几何 Overlay 与 Game Mode 抑制

> 状态：**未实现（v1 之后）**  
> 对照：PowerToys Grab And Move（MIT）`main.cpp` 中的 `ShowOverlay` / `IsSuppressedByGameMode`  
> 本插件**不是** Microsoft / PowerToys 官方组件。

v1 已交付：修饰键拖移、右键缩放、排除列表、Alt 菜单抑制等核心手势。下列两项产品范围内保留，实现前以本文为规格草稿。

---

## 1. 拖动几何 Overlay（`ShowOverlay`）

### PowerToys 行为

- 进入 Active 拖移/缩放后，在目标窗口上方显示半透明覆盖层。
- 覆盖层跟手更新位置与尺寸；可选显示几何信息（X/Y/宽/高）。
- 缩放时按最近边/角切换光标（对角 / 水平 / 垂直）。
- 对应设置：**Show window geometry**。

### v1 挂接点

| 位置 | 说明 |
|------|------|
| `GrabAndMoveCore.BeginInteractionUnlocked` | 开始交互后可通知 Overlay「显示」 |
| `HandleDragMoveUnlocked` / `HandleDragResizeUnlocked` | 每次 `SetWindowPos` 后同步 Overlay 矩形 |
| `StopInteractionUnlocked` | 隐藏 / 销毁 Overlay |
| SettingEav | 新增「显示窗口几何」布尔项（默认关，未接线） |

Hook 回调线程**禁止**直接操作 Avalonia UI；必须调度到 UI 线程。

### 实现备选

#### A. Avalonia 顶层窗口（推荐优先评估）

- 无边框、透明、`Topmost`、不激活（避免抢焦点）。
- 用 `WindowManager` / `PluginWindowBehavior` 归属本插件 Id，停插件时自动关窗。
- 几何文字可用简单 `TextBlock`；轮廓可用半透明 `Border`。
- 依赖：`miokit-plugin-avalonia-ui` 约定；注意 DPI 与物理像素 ↔ DIP。

#### B. Win32 原生 Overlay（更接近 PT）

- 自建分层 HWND + 自绘边框/填充（PT 使用多枚 overlay 窗）。
- 延迟通常更低，与 `main.cpp` 对照成本更小。
- 需自行处理 DPI、圆角边距（PT 的 `PrepareOverlayMetrics` / DWM 扩展框）。

### 非目标

- 不在 v1 强行引入 UI 项目依赖。
- 不声称与 PowerToys 像素级视觉一致。

---

## 2. Game Mode 抑制（`IsSuppressedByGameMode`）

### PowerToys 行为

- 设置：**Do not activate when Game Mode is on**（默认开）。
- 当系统处于游戏模式时，修饰键按下不武装 Grab And Move，键事件交给游戏。
- 远程会话（`SM_REMOTESESSION`）下 PT 直接视为不抑制，避免误判。

### v1 挂接点

| 位置 | 说明 |
|------|------|
| `GrabAndMoveCore.HandleKeyDown` | 武装修饰键会话前调用 `IsSuppressedByGameMode()`；为 true 则 `Chain` |
| SettingEav | 新增「游戏模式时禁用」布尔项（默认建议 `true`） |

与排除列表的优先级建议：

1. 功能总开关关闭 → 不处理  
2. Game Mode 抑制开启且处于游戏模式 → 不武装  
3. 前台属于「排除应用（修饰键）」→ 不武装  
4. 目标窗口属于「排除应用」→ 可武装但点击后 passthrough / 不拖  

### 实现要点

- 对照 PowerToys 现有 `detect_game_mode()`（或同等 Windows API / 广播）再移植，避免臆造探测。
- 纯逻辑短路，**不需要 UI**。
- 探测失败时偏向「不抑制」，以免误伤日常桌面；或按设置显式失败策略写进代码注释。

### 非目标

- 不在 v1 引入游戏模式 API 依赖探测失败时的复杂诊断 UI。

---

## 3. 建议开发顺序

1. Game Mode 抑制（改动面小，无 UI）。  
2. Overlay 无几何文字的轮廓跟手（先 Win32 或 Avalonia 二选一）。  
3. 「显示窗口几何」开关与数字叠加。  

实现时继续保留 `THIRD_PARTY_NOTICES.md` 中的 MIT 归属，并在新增移植文件头注明 PowerToys 来源。
