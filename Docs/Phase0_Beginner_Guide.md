# AnimalCafe Phase 0 Beginner Guide

> 适合：Unity 零基础、coding 零基础  
> Unity version：`6000.5.5f1`  
> Scene：`Assets/Scenes/MainCafe.unity`

## 1. 这份文档要教会你什么

读完并实际操作一次后，你应该能够：

- 在 Unity 中打开 `MainCafe`。
- 找到 Phase 0 添加的 GameObjects。
- 在 Inspector 中查看 Camera 和 gameplay components。
- 分辨正式技术基础与 placeholder 测试内容。
- 进入 Play Mode 测试 Camera、selection 和 Game Time。
- 查看 Console 是否有 error。
- 运行 Play Mode tests。
- 知道每个 Phase 0 file 的大概用途。
- 在不懂 C# 的情况下，判断 Phase 0 有没有正常工作。

这份文档不要求你现在学会写 C#。第一目标只是让你看懂项目由哪些部分组成，以及如何验证它们。

---

## 2. 先认识 Unity 的五个主要区域

打开 AnimalCafe 项目后，Unity 通常会显示以下区域。

### 2.1 Hierarchy

Hierarchy 是当前 Scene 中所有 GameObjects 的清单。

你可以把它想成当前场景的“目录”：

```text
MainCafe
├─ Main Camera
├─ Directional Light
├─ CafeFloor
├─ Global Volume
├─ Phase0_Runtime
├─ Phase0_Demo
├─ Phase0_TimeControls
└─ EventSystem
```

点击 Hierarchy 中的 GameObject，右侧 Inspector 就会显示它的设置。

### 2.2 Scene View

Scene View 是编辑场景的工作区域。

你可以在这里观察和摆放 GameObjects。这里不是玩家最终看到的画面。

常用操作：

- 鼠标滚轮：在 Scene View 中缩放编辑视角。
- 鼠标中键拖拽：移动编辑视角。
- 按住鼠标右键：旋转编辑视角。
- 选中 GameObject 后按 `F`：将编辑视角聚焦到该对象。

这些是 Unity Editor 的编辑操作，与我们为游戏编写的左键拖拽 Camera 不同。

### 2.3 Game View

Game View 是玩家运行游戏时看到的画面。

点击顶部的 Play 按钮后，应主要在 Game View 中测试 Phase 0。

### 2.4 Inspector

Inspector 显示当前选中 GameObject 的 Components 和参数。

例如选择 `Main Camera` 后，你会看到：

- Transform
- Camera
- Universal Additional Camera Data

选择 `Phase0_Runtime` 后，你会看到我们添加的 Phase 0 scripts。

### 2.5 Project Window

Project Window 显示项目 files 和 folders，例如：

```text
Assets
├─ Config
├─ Editor
├─ Materials
├─ Scenes
├─ Scripts
└─ Tests
```

Hierarchy 显示当前 Scene 里的 GameObjects；Project Window 显示硬盘中的 project files。不要把两者混淆。

---

## 3. 如何打开正确的 Scene

1. 在 Project Window 打开 `Assets`。
2. 打开 `Scenes`。
3. 双击 `MainCafe`。
4. 查看 Unity 顶部或 Hierarchy 上方，确认当前 Scene 名称是 `MainCafe`。

正确路径：

```text
Assets/Scenes/MainCafe.unity
```

`SampleScene` 是 Unity template 自带的示例 Scene，不是现在的主要开发 Scene。

---

## 4. Phase 0 的 Scene 里有什么

## 4.1 原有基础对象

### Main Camera

决定玩家在 Game View 中看到什么。

当前试做使用：

```text
Projection: Orthographic
Rotation X: 35.264°
Rotation Y: 45°
Position: (-10, 10, -10)
```

这是 classic isometric baseline，仍可根据实际视觉方向调整。

### Directional Light

为整个场景提供主要方向光。

### CafeFloor

当前的测试地面，之后会被正式 cafe layout 扩展或替换。

### Global Volume

保存 URP 的画面效果设置。Phase 0 没有重点修改正式视觉效果。

## 4.2 Phase0_Runtime

这是 Phase 0 的运行系统集合。

在 Hierarchy 选择 `Phase0_Runtime`，Inspector 中应看到：

- `Mouse Camera Input`
- `Cafe Camera Controller`
- `Game Time Service`
- `Scene Interaction Controller`

这些 Components 是正式技术基础，不是装饰。

### Mouse Camera Input

负责读取：

- Mouse wheel
- Left-button drag
- Tap

它不会自己移动 Camera，只负责把 mouse 操作转换成统一 input。

### Cafe Camera Controller

负责：

- Camera pan
- Camera zoom
- Camera bounds

它读取 `Mouse Camera Input` 的结果，但不直接读取 mouse。这让我们未来可以增加 iOS touch input，而不需要重写 Camera movement。

### Game Time Service

统一控制：

- Pause：`0x`
- Normal：`1x`
- Fast：`2x`

其他 gameplay system 不应该自己随意修改 Game Time。

### Scene Interaction Controller

负责：

- 从 tap 位置发出 raycast。
- 查找被点击的 selectable object。
- 取消上一个 selection。
- 选择新 object。
- 点击空白处时清除 selection。

## 4.3 Phase0_Demo

展开 `Phase0_Demo` 后应看到：

```text
Phase0_Demo
├─ Selectable_Blue
├─ Selectable_Green
└─ Time_Test_Mover
```

这些是 placeholder 测试对象，不是正式游戏内容。

### Selectable_Blue 和 Selectable_Green

用于测试：

- Tap 是否正确识别。
- Raycast 是否击中对象。
- Selection 是否正确切换。
- Selected visual feedback 是否显示。

点击后变黄色，取消选择后恢复原色。

### Time_Test_Mover

橙色移动方块用于测试 Game Time：

- Pause 时停止。
- `1x` 时正常移动。
- `2x` 时移动更快。

它以后不会成为正式 cafe 内容。

## 4.4 Phase0_TimeControls

这是底部三个 placeholder buttons：

- Pause
- `1x`
- `2x`

buttons 的功能连接是正式基础，但现在的颜色、字体和 layout 都不是正式 UI art。

## 4.5 EventSystem

Unity UI 需要 EventSystem 才能接收 pointer click。

如果删除 EventSystem，Pause、`1x` 和 `2x` buttons 可能无法点击。

---

## 5. 正式基础与 placeholder 的区别

## 5.1 正式技术基础

以下部分计划继续用于后续 phases：

- Input adapter 的职责边界
- Camera pan、zoom 和 bounds
- Orthographic Camera
- Tap 与 drag 的区分
- Selection system
- Game Time service
- Event bus
- Play Mode test structure
- Runtime、Editor 和 Test assembly 划分

“正式基础”不代表所有参数永远不能改。Camera angle、speed 和 bounds 都可以继续调整。

## 5.2 Placeholder 测试内容

以下内容之后会替换：

- 蓝色 cube
- 绿色 cube
- 橙色移动 cube
- 当前 CafeFloor
- Pause、`1x`、`2x` buttons 的视觉样式
- 当前测试用 materials

placeholder 的作用是让功能容易观察，而不是代表最终游戏画面。

---

## 6. 如何在 Inspector 中查看我做过的设置

## 6.1 查看 Camera

1. 在 Hierarchy 点击 `Main Camera`。
2. 在 Inspector 找到 Transform。
3. 查看 Position 和 Rotation。
4. 在 Camera component 中确认 Projection 是 `Orthographic`。
5. 查看 Orthographic Size。

注意：现在不要随意修改 Transform。Camera position、rotation 与 bounds 需要一起调整。

## 6.2 查看 Camera config

1. 在 Project Window 打开 `Assets/Config`。
2. 点击 `DefaultCameraSettings`。
3. 在 Inspector 查看：

```text
Pan Speed
Zoom Speed
Position Min
Position Max
Min Orthographic Size
Max Orthographic Size
Drag Threshold Pixels
```

参数含义：

- `Pan Speed`：拖拽时 Camera 移动速度。
- `Zoom Speed`：每次 mouse wheel 事件改变多少 zoom。
- `Position Min/Max`：Camera 允许移动的世界范围。
- `Min/Max Orthographic Size`：允许的 zoom 范围。
- `Drag Threshold Pixels`：mouse 移动多少 pixels 后，操作从 tap 变成 drag。

## 6.3 查看 selectable object

1. 展开 `Phase0_Demo`。
2. 点击 `Selectable_Blue`。
3. 在 Inspector 查看：

```text
Transform
Mesh Filter
Mesh Renderer
Box Collider
Color Selectable
```

重要关系：

```text
Box Collider
→ 让 raycast 可以击中 object

Color Selectable
→ 决定 selected / deselected 时的颜色反馈

Mesh Renderer
→ 真正显示 object 和颜色
```

如果 Collider 被删除，object 可能看得见但点不到。

如果 Renderer reference 丢失，selection 可能存在但无法正确显示颜色。

---

## 7. 如何进入 Play Mode 测试

1. 保存 Scene：按 `Ctrl + S`。
2. 点击 Unity 顶部中央的 Play 三角形按钮。
3. 等待按钮变成蓝色。
4. 切换到 Game View。

测试：

### Camera

- Left-button drag：Camera pan。
- Mouse wheel：zoom。
- 持续拖拽：Camera 最终会停在 bounds。

### Selection

- 短按蓝色 cube：变黄色。
- 短按绿色 cube：蓝色恢复，绿色变黄色。
- 点击地面：取消选择。
- 拖拽后松开：不应误选 object。

### Game Time

- Pause：橙色 cube 停止。
- Pause 状态下：Camera、selection 和 UI 仍可使用。
- `1x`：橙色 cube 正常移动。
- `2x`：橙色 cube 明显加速。

完成后再次点击 Play 按钮退出 Play Mode。

重要：Play Mode 中临时修改的很多 Inspector 数值会在退出后恢复。不要把 Play Mode 当成正式编辑状态。

---

## 8. 如何查看 Console

打开方式：

```text
Window → General → Console
```

Console 常见三种信息：

- 白色 Log：普通运行信息。
- 黄色 Warning：程序还能继续，但有值得注意的问题。
- 红色 Error：通常代表功能失败或代码异常。

Phase 0 正常运行时：

- 不应出现红色 error。
- 不应出现 `A Renderer is required` warning。
- 测试无效 Game Time speed 时产生的 expected warning 只会出现在自动 test 场景，并由 test 明确处理。

如果发现 error：

1. 不要连续修改多个东西。
2. 点击 Console 中的 error。
3. 复制完整 message。
4. 记录触发 error 前做了什么。
5. 把 message 和操作步骤提供给 coding assistant。

---

## 9. 如何运行 Play Mode tests

Play Mode tests 会让 Unity 自动检查 Phase 0 行为。

### 9.1 打开 Test Runner

尝试从菜单打开：

```text
Window → General → Test Runner
```

如果 Unity 将它显示在其他位置，可以使用顶部菜单的 Search 查找 `Test Runner`。

### 9.2 运行 tests

1. 打开 Test Runner。
2. 选择 `PlayMode`。
3. 展开 `AnimalCafe.PlayModeTests`。
4. 找到 `Phase0PlayModeTests`。
5. 点击 `Run All`。

运行时 Unity 会自动进入和退出 Play Mode，不要同时操作 Scene。

### 9.3 如何看结果

- 绿色：test passed。
- 红色：test failed。
- 灰色：test 没有运行或被 skipped。

当前 Phase 0 suite 验证的内容包括：

- Game Time 支持 Pause、`1x`、`2x`。
- 无效 Game Time speed 被拒绝。
- Tap 与 drag threshold。
- Camera position bounds。
- Camera zoom bounds。
- 不同 mouse wheel 数值产生一致 zoom step。
- Selection、切换 selection 和清除 selection。
- Renderer 初始化顺序。
- Pause 停止 moving object。
- `2x` 比 `1x` 移动更快。
- UI buttons 改变 Game Time。
- `MainCafe` 可以加载必要 Phase 0 objects。
- Camera 使用目标 isometric rotation。

自动 tests 通过不代表视觉方向一定符合你的喜好。Camera angle 和操作手感仍需要人工 Play Mode 检查。

---

## 10. Phase 0 files 是做什么的

你不需要现在读懂每一行代码。先理解每个 file 的责任。

## 10.1 Camera

```text
Assets/Scripts/Camera/
├─ CameraSettings.cs
└─ CafeCameraController.cs
```

- `CameraSettings.cs`：定义可以在 Inspector 调整的 Camera config。
- `CafeCameraController.cs`：执行 pan、zoom 和 bounds。

## 10.2 Input

```text
Assets/Scripts/Input/
├─ CameraInputFrame.cs
├─ ICameraInputSource.cs
└─ MouseCameraInput.cs
```

- `CameraInputFrame.cs`：保存一个 frame 内的 pan、zoom 和 tap 数据。
- `ICameraInputSource.cs`：定义 Camera input 必须提供什么。
- `MouseCameraInput.cs`：读取 Windows mouse。

未来 iOS 可以添加新的 touch input class，并继续使用现有 Camera controller。

## 10.3 Interaction

```text
Assets/Scripts/Interaction/
├─ ISelectable.cs
├─ ColorSelectable.cs
└─ SceneInteractionController.cs
```

- `ISelectable.cs`：规定 selectable object 必须能 Select 和 Deselect。
- `ColorSelectable.cs`：用颜色显示 selection。
- `SceneInteractionController.cs`：raycast 并管理当前 selection。

## 10.4 Game Time

```text
Assets/Scripts/Core/Time/
├─ GameSpeed.cs
├─ IGameTimeService.cs
└─ GameTimeService.cs
```

- `GameSpeed.cs`：定义 Pause、Normal、Fast。
- `IGameTimeService.cs`：定义其他 systems 如何使用 Game Time。
- `GameTimeService.cs`：真正修改 Unity 的 time scale。

## 10.5 Events

```text
Assets/Scripts/Core/Events/
├─ GameEvents.cs
└─ GameEventBus.cs
```

- `GameEvents.cs`：定义跨系统通知中的数据。
- `GameEventBus.cs`：发布 selection 和 speed 变化通知。

## 10.6 UI 与 Testing

```text
Assets/Scripts/UI/TimeControlPanel.cs
Assets/Scripts/Testing/TimeTestMover.cs
```

- `TimeControlPanel.cs`：连接三个 buttons 与 Game Time service。
- `TimeTestMover.cs`：测试不同 Game Time speed。

## 10.7 Editor setup

```text
Assets/Editor/Phase0SceneSetup.cs
```

这是开发工具，不会成为 gameplay system。

它负责可重复地：

- 配置 Main Camera。
- 创建 Phase 0 runtime root。
- 创建 placeholder demo objects。
- 创建 time buttons。
- 连接 Inspector references。
- 将 `MainCafe` 设为 build scene。

可以从 Unity menu 运行：

```text
AnimalCafe → Phase 0 → Configure Scene
```

不要在已经手动调整正式 Scene 后随意运行它，因为它会重新应用 Phase 0 默认 Camera 和 demo settings。

## 10.8 Tests

```text
Assets/Tests/PlayMode/
├─ AnimalCafe.PlayModeTests.asmdef
└─ Phase0PlayModeTests.cs
```

- `.asmdef`：告诉 Unity 这些 files 属于 test assembly。
- `Phase0PlayModeTests.cs`：包含 Phase 0 automated tests。

---

## 11. 整个系统如何合作

### 11.1 Camera pan

```text
Mouse left drag
→ MouseCameraInput
→ CameraInputFrame.PanDelta
→ CafeCameraController
→ Camera position
→ Clamp to bounds
```

### 11.2 Camera zoom

```text
Mouse wheel
→ MouseCameraInput
→ CameraInputFrame.ZoomDelta
→ CafeCameraController
→ Orthographic Size
→ Clamp to zoom bounds
```

### 11.3 Selection

```text
Short tap
→ MouseCameraInput
→ SceneInteractionController
→ Physics raycast
→ ISelectable
→ ColorSelectable
→ Object changes color
```

### 11.4 Game Time

```text
Pause / 1x / 2x button
→ TimeControlPanel
→ GameTimeService
→ Unity time scale
→ TimeTestMover speed changes
```

---

## 12. 哪些参数可以安全查看或调整

在已经保存 Git checkpoint 的前提下，以下参数适合后续小幅调整：

- `Pan Speed`
- `Zoom Speed`
- `Min Orthographic Size`
- `Max Orthographic Size`
- `Drag Threshold Pixels`
- selected color
- placeholder button layout

以下内容要一起设计，不建议单独随意修改：

- Main Camera Position
- Main Camera Rotation
- Camera Position Min/Max
- Cafe layout size
- object Collider
- runtime component references

原因是它们互相关联。只改 Camera rotation 可能让 Camera 不再对准 cafe；只改 bounds 可能让玩家看到场景外。

---

## 13. Beginner 安全规则

### 规则 1：先退出 Play Mode 再编辑

如果顶部 Play 按钮是蓝色，先点击退出。

### 规则 2：修改前先知道当前选中了什么

Inspector 修改的是当前选中的 GameObject 或 asset。

### 规则 3：不要删除不认识的 Components

Components 之间可能通过 reference 连接。删除一个 component 可能让另一个 component 出现 Missing reference。

### 规则 4：一次只改一个参数

修改后立刻 Play Mode 测试。这样出问题时容易知道原因。

### 规则 5：Console 出现红色 error 时先停下

先保存 error message，不要同时尝试多个修复。

### 规则 6：不要手动编辑 `.unity`、`.asset` 或 `.meta` text

这些 files 通常由 Unity 管理。应优先通过 Unity Inspector 或 Editor tool 修改。

### 规则 7：不要删除 `.meta`

`.meta` 保存 Unity asset GUID。删除它可能使 Scene 或 Prefab references 断开。

---

## 14. 如何判断 Phase 0 是否正常

使用这张 checklist：

- [ ] `MainCafe` 可以打开。
- [ ] Console 没有红色 error。
- [ ] Camera 是 Orthographic。
- [ ] Camera 使用确认中的 isometric angle。
- [ ] Left drag 可以 pan。
- [ ] Mouse wheel 可以 zoom。
- [ ] Camera 不会无限移出 bounds。
- [ ] Drag 松开时不会误选 object。
- [ ] 蓝绿 cubes 可以 selected 并变色。
- [ ] 可以切换 selection。
- [ ] 点击地面可以取消 selection。
- [ ] Pause 停止橙色 cube。
- [ ] Pause 时 Camera 和 UI 仍可操作。
- [ ] `1x` 与 `2x` 有明显速度差异。
- [ ] Play Mode tests 全部通过。

如果以上都通过，说明 Phase 0 的技术基础处于可继续开发状态。

---

## 15. Beginner Glossary

### Asset

Unity 项目中的 file，例如 Scene、Material、Script 或 image。

### Component

附加在 GameObject 上的一项功能。Transform、Camera、Collider 和 scripts 都是 Components。

### GameObject

Scene 中的基本对象容器。GameObject 通过 Components 获得功能。

### Scene

一个游戏场景 file。当前主要 Scene 是 `MainCafe`。

### Transform

GameObject 的 Position、Rotation 和 Scale。

### Inspector

查看和修改当前对象 Components 与参数的窗口。

### Script

用 C# 编写的功能代码。Script 加到 GameObject 后会显示为 Component。

### Collider

不可见的碰撞形状。Physics raycast 需要 Collider 才能点中对象。

### Raycast

从 Camera 穿过 pointer position 发出一条不可见射线，用来判断点击了什么。

### Orthographic Camera

没有近大远小透视效果的 Camera，常用于 isometric 和 management games。

### Isometric

从斜上方同时看到物体两个侧面的视觉方向。Unity 中通常由 Orthographic Camera 加固定 rotation 实现。

### Placeholder

用于测试功能的临时内容，不代表最终美术或正式 gameplay content。

### Runtime

游戏运行时执行的功能。

### Editor Tool

只在 Unity Editor 中使用的开发工具，不属于玩家运行时 gameplay。

### Interface

一种代码 contract，规定对象必须提供哪些功能，但不限制内部怎么实现。

### Event

系统状态变化时发送的通知，例如 selection changed。

### Play Mode

Unity Editor 中模拟游戏运行的模式。

### Play Mode Test

让 Unity 自动进入运行环境并验证 gameplay behavior 的测试。

### Regression

原本正常的功能因为后续修改再次损坏。

### `.meta`

Unity 为每个 asset 生成的身份 file，保存 GUID 和 import settings。

### `.asmdef`

Assembly Definition。告诉 Unity 哪些 scripts 一起编译，以及它们可以引用哪些其他 assemblies。

---

## 16. 推荐的学习顺序

第一次不要尝试一次看懂所有 scripts。建议：

1. 先熟悉 Hierarchy、Scene、Game、Inspector、Project 和 Console。
2. 在 Play Mode 中完整测试一次 Phase 0。
3. 对照本文件查看 `Phase0_Runtime` 的 Components。
4. 查看 `DefaultCameraSettings` 的参数。
5. 运行一次 Play Mode tests。
6. 最后才打开一个简单 script，例如 `TimeTestMover.cs`。

当你能解释“button 如何让橙色 cube 改变速度”时，就已经理解了 Phase 0 最重要的基础数据流之一。
