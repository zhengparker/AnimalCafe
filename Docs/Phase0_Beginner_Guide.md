# AnimalCafe Phase 0 Beginner Guide

> 这是一份面向 Unity 和 coding 初学者的 educational note。
> 它只解释 Phase 0，不负责解释 Phase 1 的 Layout Data Model。

## 1. 这个 Phase 的目标

Phase 0 的目标不是制作完整咖啡店，而是建立之后所有 Phase 都会使用的技术基础：

- 可以运行的 `MainCafe` Scene。
- 固定角度的 orthographic isometric Camera。
- Camera pan 和 zoom。
- 鼠标点击与 drag 的 input 区分。
- 可复用的 selection 系统。
- `Pause`、`1x`、`2x` 游戏时间控制。
- EditMode 和 PlayMode automated test 基础。

Phase 0 完成后，项目具备了一个稳定、可重复测试的 Unity foundation。

## 2. 开发前是什么状态

项目最初只有很少的 Unity 默认内容，还没有统一的：

- Camera config；
- mouse input；
- selection contract；
- game-time service；
- automated regression tests；
- beginner testing workflow。

因此 Phase 0 先解决“项目怎样稳定运行和验证”，没有开始制作顾客、员工、柜台或咖啡流程。

## 3. Phase 0 做了什么改动

### 3.1 Camera

`Main Camera` 使用 orthographic projection 和固定 isometric rotation。

Camera 支持：

- mouse drag pan；
- mouse wheel zoom；
- position bounds；
- minimum/maximum zoom；
- input drag threshold。

Camera 参数保存在 `DefaultCameraSettings.asset`，运行逻辑由 `CafeCameraController` 负责。

### 3.2 Input

`MouseCameraInput` 负责读取鼠标：

- 按下位置；
- drag distance；
- pan delta；
- scroll wheel；
- click 和 drag 的区别。

其他系统通过 interface 使用这些输入，不需要直接读取鼠标设备。

### 3.3 Selection

`SceneInteractionController` 从 Camera 发出 raycast，寻找可以选择的 object。

`ColorSelectable` 是 Phase 0 用来证明 selection contract 能工作的简单实现。正式 Scene 现在不再保留测试 cube；automated tests 会创建自己的 test-local fixture。

### 3.4 Game Time

`GameTimeService` 提供三种速度：

- `Paused`：`Time.timeScale = 0`
- `Normal`：`Time.timeScale = 1`
- `Fast`：`Time.timeScale = 2`

`TimeControlPanel` 把 `Pause`、`1x`、`2x` buttons 连接到这个 service。

### 3.5 Scene Setup

`Phase0SceneSetup` 是一个 Editor tool，用来创建或修复正式的 Phase 0 Scene 基础：

- `Phase0_Runtime`
- `Phase0_TimeControls`
- `EventSystem`
- Camera references
- runtime component references

它可以重复执行，而不会持续创建重复的正式 objects。

Setup 会从当前 loaded `MainCafe` Scene 的 roots 中寻找这些 objects，包括 inactive objects。如果出现同名 duplicates，它会只保留一个 canonical root，并清理重复的 setup-owned components。因此重复运行 repair 后，Hierarchy 仍应只有一个 `Phase0_Runtime`、一个 `Phase0_TimeControls` 和一个 `EventSystem`。

## 4. 重要概念解释

### GameObject

Scene 中的一个 object，例如 Camera、Canvas 或 runtime root。

### Component

挂在 GameObject 上的功能。例如 `Camera`、`GameTimeService` 或 `CafeCameraController`。

### Scene

Unity 保存一个游戏空间内容的文件。AnimalCafe 当前主要 Scene 是：

```text
Assets/Scenes/MainCafe.unity
```

### Runtime

游戏进入 Play Mode 后真正运行的逻辑。

### Editor Tool

只在 Unity Editor 中帮助创建、检查或修改项目内容的工具，不会成为玩家看到的 gameplay。

### Test-local fixture

automated test 自己临时创建的测试 object。测试结束后会删除，不会污染正式 Scene。

### Regression Test

防止已经正确的功能以后被意外改坏的测试。

## 5. Phase 0 Files

### Camera

```text
Assets/Scripts/Camera/CameraSettings.cs
Assets/Scripts/Camera/CafeCameraController.cs
Assets/Config/DefaultCameraSettings.asset
```

### Input

```text
Assets/Scripts/Input/ICameraInputSource.cs
Assets/Scripts/Input/MouseCameraInput.cs
```

### Interaction

```text
Assets/Scripts/Interaction/ISelectable.cs
Assets/Scripts/Interaction/ColorSelectable.cs
Assets/Scripts/Interaction/SceneInteractionController.cs
```

### Game Time and UI

```text
Assets/Scripts/Core/Time/GameSpeed.cs
Assets/Scripts/Core/Time/GameTimeService.cs
Assets/Scripts/UI/TimeControlPanel.cs
```

### Editor and Tests

```text
Assets/Editor/Phase0SceneSetup.cs
Assets/Tests/PlayMode/Phase0PlayModeTests.cs
```

## 6. Tests 和 Bug Cases

Phase 0 automated tests 验证：

- 只接受 `Paused`、`Normal`、`Fast` 三种速度。
- Pause 把 `Time.timeScale` 设为 `0`。
- Camera position 和 zoom 不会超出 bounds。
- 不同 scroll-wheel 数值都按一个 zoom step 处理。
- click 与 drag 使用 threshold 正确区分。
- 真实 virtual mouse press / drag / release 在 Pause 时仍可读取。
- Camera 与 interaction 在同一 frame 读取相同 cached input。
- selection 能选择、切换和清除。
- selected object 被 disabled、设为 inactive 或 destroyed 后，selection reference 会安全清理。
- Renderer 缺少可用 material color property 时输出明确 warning，并保持 Scene 可运行。
- time-control buttons 调用正确速度。
- `MainCafe` 包含正式 runtime objects。

原始 Phase 0 acceptance 是 `16 / 16` PlayMode；Game Time owner hardening 后 baseline 增加到 `21 / 21`。2026-07-30 review hardening 的 fresh automated evidence 是 EditMode `116 / 116` 与 PlayMode `31 / 31`，failed、skipped、inconclusive 都是 `0`。后续 Phase 不应让这些基础失效。

## 7. Unity Manual Test

1. 使用 Unity `6000.5.5f1` 打开项目。
2. 打开 `Assets/Scenes/MainCafe.unity`。
3. 在 Hierarchy 确认只有一个 `Phase0_Runtime`、一个 `Phase0_TimeControls` 和一个 `EventSystem`。
4. 清空 Console。
5. 进入 Play Mode。
6. 用 mouse wheel 测试 Camera zoom。
7. 用 mouse drag 测试 Camera pan。
8. 点击 `Pause`、`1x`、`2x`，确认 buttons 可以响应。
9. 退出 Play Mode。
10. 确认 Console 没有 unexpected error 或 warning。
11. 在 Test Runner 运行 EditMode tests，确认 `116 / 116` 全部绿色。
12. 运行 PlayMode tests，确认 `31 / 31` 全部绿色。

## 8. Phase 0 没有做什么

Phase 0 没有实现：

- Grid 或 Layout Data Model；
- furniture placement；
- 顾客或员工；
- 柜台和咖啡机；
- order flow；
- economy、save 或 progression；
- 正式视觉资产。

Phase 0 曾经使用 demo cubes 和一个 time mover 来观察功能。它们是临时测试内容，不是正式 gameplay，并已在 Phase 1 清理。

## 9. Beginner Glossary

| Term | 简单解释 |
| --- | --- |
| Asset | Unity Project 中保存的文件 |
| Inspector | 查看和编辑当前选中 object/component 的面板 |
| Hierarchy | 当前 Scene 中 GameObjects 的列表 |
| Project Window | 项目 files 和 assets 的列表 |
| Play Mode | 在 Editor 中运行游戏 |
| EditMode Test | 不需要运行 gameplay Scene 的测试 |
| PlayMode Test | 进入 Unity runtime 环境执行的测试 |
| Raycast | 从一个位置沿方向寻找被击中的 object |
| Orthographic Camera | 没有透视缩小效果的 Camera |
| Isometric | 常见的斜俯视角表现 |
| `.meta` | Unity 用来保存 asset identity 的 metadata |
| `.asmdef` | Unity assembly definition |

## 10. 完成状态和下一步

Phase 0 已完成并成为 regression baseline。

2026-07-30 的 completed-phase hardening 已通过 automated verification 和用户 manual acceptance：Hierarchy 三个 setup-owned roots 各一个，Camera pan / zoom、Pause / `1x` / `2x` 正常，Console clean，EditMode `116 / 116` 与 PlayMode `31 / 31` 全部通过。当前等待用户通过 GitHub Desktop commit；之后才把最新 `main` 整合到 Phase 2 branch。

Phase 1 在这个基础上建立 Layout Data Model，并清理 Phase 0 的 demo-only Scene 内容。Phase 1 的解释请阅读：

```text
Docs/Phase1_Beginner_Guide.md
```

不要因为正式 Scene 现在没有 demo cubes，就认为 Phase 0 selection 或 time system 被删除；这些正式能力仍然存在，并由 automated tests 保护。
