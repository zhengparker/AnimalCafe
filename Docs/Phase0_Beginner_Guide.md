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

UI button 和 Scene 是两层不同的操作区域。点击 `Pause`、`1x` 或 `2x` 时，应该只操作这个 button；不能同时点击或选中 UI 后面 Scene 里的 object。这样玩家按时间控制时，原来选中的世界 object 不会被意外改变。

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
- 真实 virtual Mouse、`InputSystemUIInputModule`、Canvas 和 `GraphicRaycaster` 会一起验证 UI pointer；点击 UGUI 时不会改变 world selection。
- selected object 被 disabled、设为 inactive 或 destroyed 后，selection reference 会安全清理。
- Renderer 缺少可用 material color property 时输出明确 warning，并保持 Scene 可运行。
- time-control buttons 调用正确速度。
- `MainCafe` 包含正式 runtime objects。

原始 Phase 0 acceptance 是 `16 / 16` PlayMode；Game Time owner hardening 后 baseline 增加到 `21 / 21`。2026-07-30 早期 review hardening 的 fresh automated evidence 是 EditMode `116 / 116` 与 PlayMode `31 / 31`，failed、skipped、inconclusive 都是 `0`。本轮 Option A full regression 的最新 XML 结果是 EditMode `191 / 191` 与 PlayMode `35 / 35`，failed、skipped、inconclusive 都是 `0`。后续 Phase 不应让这些基础失效。

## 7. Unity Manual Test

1. 使用 Unity `6000.5.5f1` 打开项目。
2. 打开 `Assets/Scenes/MainCafe.unity`。
3. 在 Hierarchy 确认只有一个 `Phase0_Runtime`、一个 `Phase0_TimeControls` 和一个 `EventSystem`。
4. 清空 Console。
5. 点击 Unity 顶部中间的 Play 按钮进入 Play Mode。确认 Play 按钮已经变成蓝色，再进行下一步；这样下面创建的 object 才是临时 fixture。
6. 在 Hierarchy 的空白位置点击右键，选择 `3D Object > Cube`。
7. 把新 Cube 重命名为 `Temporary_Selection_Test_Cube`。
8. 选中它，在 Inspector 的 Transform 输入：
   - Position：`X = 0`、`Y = 0`、`Z = 0`
   - Rotation：`X = 0`、`Y = 0`、`Z = 0`
   - Scale：`X = 2`、`Y = 2`、`Z = 2`
9. 在 Inspector 底部点击 `Add Component`，搜索并添加 `ColorSelectable`。不用手动填写 Renderer；它会自动找到 Cube 已有的 `Mesh Renderer`。
10. 点击 `Game` tab，然后用 mouse 点击画面中央的 Cube。Cube 应变为黄色 / 橙黄色，表示 world selection 已生效。
11. 依次点击画面底部的 `Pause`、`1x`、`2x`。每次点击后，Cube 都应保持黄色 / 橙黄色；UI button 不能清除 selection，也不能把点击穿透到 Scene。
12. 用 mouse wheel 测试 Camera zoom，再用 mouse drag 测试 Camera pan。drag 不应被误判成 click。
13. 如果 Game view 看不到 Cube：
    - 先确认当前是 `Game` tab，而不是 `Scene` tab；
    - 在 Hierarchy 重新选中 `Temporary_Selection_Test_Cube`，再次核对 Position 是 `(0, 0, 0)`；
    - 可以只在 Play Mode 中把 Scale 临时改为 `(3, 3, 3)`；
    - 不要移动 `Main Camera`，不要修改或保存 production Scene asset；如果仍不可见，停止并记录 Console 信息。
14. 点击顶部蓝色 Play 按钮退出 Play Mode。确认 `Temporary_Selection_Test_Cube` 自动从 Hierarchy 消失。
15. 这个 Cube 是在 Play Mode 中创建的，因此退出时 Unity 会丢弃它；不要在 Play Mode 中保存 Scene，这样不会把 fixture 写进 `MainCafe.unity`。
16. 确认 Console 没有 unexpected error 或 warning。
17. 在 Test Runner 运行 EditMode tests，确认 `191 / 191` 全部绿色。
18. 运行 PlayMode tests，确认 `35 / 35` 全部绿色。

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

2026-07-30 的 completed-phase hardening 已通过 automated verification 和用户 manual acceptance：Hierarchy 三个 setup-owned roots 各一个，Camera pan / zoom、Pause / `1x` / `2x` 正常，Console clean。当前 Phase 2 PR hardening Option A 的 fresh full regression 为 EditMode `191 / 191`、PlayMode `35 / 35`，failed、skipped、inconclusive 都是 `0`；真实 UGUI/Input System tests 已验证点击时间 UI controls 不会改变已有的 world selection。

用户也已完成本 guide 的 Play Mode-only temporary Cube manual acceptance：点击 `Pause`、`1x`、`2x` 后 Cube selection 一直保持，Camera pan / zoom 正常，Console clean。下一步是完成 Phase 2 branch commit / push；之后再由用户批准 merge，并在 merged `main` 上运行 full regression。

Phase 1 在这个基础上建立 Layout Data Model，并清理 Phase 0 的 demo-only Scene 内容。Phase 1 的解释请阅读：

```text
Docs/Phase1_Beginner_Guide.md
```

不要因为正式 Scene 现在没有 demo cubes，就认为 Phase 0 selection 或 time system 被删除；这些正式能力仍然存在，并由 automated tests 保护。
