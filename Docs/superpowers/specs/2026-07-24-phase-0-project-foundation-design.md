# AnimalCafe Phase 0 — Project Foundation Design

> 状态：Approved Design
>
> 日期：2026-07-24
>
> 适用环境：Windows / Unity 6

## 1. Goal

建立简单、可理解、可测试，并能为后续系统扩展的 Unity project foundation。

Phase 0 完成后，`MainCafe` scene 应具备统一的 Camera、input、interaction 和 game time 基础，并能通过手动 Play Mode 验收与自动 Play Mode tests。

## 2. Scope

### Included

- 整理 Phase 0 所需的 `Assets` folder structure。
- 在 `MainCafe` scene 中使用固定斜俯视 Camera。
- 使用鼠标左键拖拽平移 Camera。
- 使用鼠标滚轮缩放 Camera。
- 使用 Camera position bounds 和 zoom bounds 限制可视范围。
- 将短按识别为 tap，并与 drag 区分。
- 点击可选择的测试对象时使用变色 visual feedback。
- 点击其他对象时切换选择，点击空白处时取消选择。
- 建立 Pause、`1x` 和 `2x` Game Time。
- 使用一个来回移动的测试对象直观显示 Game Time 变化。
- 建立基础 Play Mode tests。
- 建立清楚的 input、Camera、interaction、time、UI 和 testing 职责边界。

### Not Included

- Customer AI
- Orders
- Economy
- Inventory
- Save system
- 正式 UI art
- 真正的 touch input
- Dependency injection package
- Service locator
- 复杂 state machine

## 3. Architecture

Phase 0 使用小型、可扩展的 component architecture。

```text
Input Adapter
    ↓ 产生 pan / zoom / tap 意图
Camera + Interaction
    ↓
Game Events
    ↓
Visual Feedback

Game Time Service
    ↓ 提供统一的速度状态
Time-controlled Behaviours
```

### 3.1 Responsibility Boundaries

- `Input` 只读取当前设备输入，并产生设备无关的操作意图。
- `Camera` 只负责 pan、zoom 和 bounds，不直接读取鼠标。
- `Interaction` 负责区分 tap 与 drag，并对场景执行 selection raycast。
- `Selectable` 管理对象是否可选，以及 selected / deselected visual feedback。
- `Game Time` 集中管理 Pause、`1x` 和 `2x`，其他系统不能直接修改 `Time.timeScale`。
- `Events` 只用于 selection 和 game speed 等跨系统状态通知。
- `Config` 使用 `ScriptableObject` 保存 Camera 速度、范围和 drag threshold 等可调参数。

该 architecture 为未来 touch input 和 gameplay systems 保留替换与扩展边界，但不会在 Phase 0 提前建立没有实际使用者的复杂 framework。

## 4. Planned Files

```text
Assets/
├─ Scenes/
│  └─ MainCafe.unity
├─ Scripts/
│  ├─ Core/
│  │  ├─ Events/
│  │  │  ├─ GameEventBus.cs
│  │  │  └─ GameEvents.cs
│  │  └─ Time/
│  │     ├─ IGameTimeService.cs
│  │     └─ GameTimeService.cs
│  ├─ Input/
│  │  ├─ ICameraInputSource.cs
│  │  └─ MouseCameraInput.cs
│  ├─ Camera/
│  │  ├─ CameraSettings.cs
│  │  └─ CafeCameraController.cs
│  ├─ Interaction/
│  │  ├─ ISelectable.cs
│  │  ├─ SceneInteractionController.cs
│  │  └─ ColorSelectable.cs
│  ├─ UI/
│  │  └─ TimeControlPanel.cs
│  └─ Testing/
│     └─ TimeTestMover.cs
├─ Config/
│  └─ DefaultCameraSettings.asset
└─ Tests/
   └─ PlayMode/
      └─ Phase0PlayModeTests.cs
```

Unity 会为新增 folders 和 assets 自动创建对应 `.meta` files。

### 4.1 Component Responsibilities

- `GameEventBus.cs`：发布 selection 和 game-speed 变化。
- `GameEvents.cs`：集中定义 Phase 0 event data。
- `IGameTimeService.cs`：定义 gameplay systems 使用的时间控制 contract。
- `GameTimeService.cs`：验证并应用 Pause、`1x` 和 `2x`。
- `ICameraInputSource.cs`：定义 Camera 可消费的设备无关 input contract。
- `MouseCameraInput.cs`：把鼠标操作转换成 pan、zoom 和 tap。
- `CameraSettings.cs`：保存 Camera 速度、位置边界、zoom 范围和 drag threshold。
- `CafeCameraController.cs`：消费 input，并限制 Camera position 和 zoom。
- `ISelectable.cs`：定义 selectable object contract。
- `SceneInteractionController.cs`：raycast、selection 切换和空白 deselection。
- `ColorSelectable.cs`：保存原颜色，selected 时变色，deselected 时恢复。
- `TimeControlPanel.cs`：将三个 placeholder buttons 连接到 Game Time。
- `TimeTestMover.cs`：按照统一 Game Time 在两个位置之间来回移动。
- `Phase0PlayModeTests.cs`：集中 Phase 0 的少量自动 tests。

## 5. Input and Interaction Design

### 5.1 Mouse Mapping

- 鼠标滚轮：zoom，对应未来 iOS pinch。
- 鼠标左键拖拽：pan，对应未来 iOS 单指 drag。
- 鼠标左键短按并松开：tap，对应未来 iOS tap。

`MouseCameraInput` 使用 screen-space drag threshold 区分 tap 和 drag。只要本次按压移动超过 threshold，松开时就不能产生 tap，避免平移结束后误选对象。

### 5.2 Selection Flow

```text
短按且移动距离未超过 threshold
→ SceneInteractionController 执行 raycast
→ 查找 ISelectable
→ 取消上一个对象
→ 选中新对象并变色
→ 发布 selection changed event
```

- 点击当前对象不会重复触发无意义的 selection change。
- 点击另一个 selectable object 会恢复前一个对象的原色。
- 点击空白或不可选择对象会取消当前选择。
- 当前对象 disabled 或 destroyed 时，selection reference 会安全清理。

## 6. Camera Design

- Camera 保持固定斜俯视角度；玩家不能旋转 Camera。
- Pan 改变 Camera 在水平场景平面上的位置。
- Zoom 使用 Camera orthographic size。
- Camera position 和 orthographic size 在每次变更后立即 clamp。
- Input 仍使用 unscaled frame time，因此 Pause 时 Camera 可以继续操作。
- Camera 可调参数全部来自 `DefaultCameraSettings.asset`。

如果 position bounds 或 zoom bounds 配置反向，component 会在初始化时规范化范围并输出明确 warning。

## 7. Game Time Design

可用速度只有：

```text
Pause = 0x
Normal = 1x
Fast = 2x
```

```text
Pause / 1x / 2x Button
→ GameTimeService 验证速度
→ 应用统一 game speed
→ 发布 time speed changed event
→ TimeTestMover 按该速度移动
```

- `GameTimeService` 是唯一允许修改 `Time.timeScale` 的 Phase 0 component。
- 不支持的速度会被拒绝，并输出明确 warning。
- 重复选择当前速度不会发布多余 event。
- Camera、input、selection 和 UI 使用 unscaled time 或不依赖 scaled time，因此 Pause 后仍可操作。
- `TimeTestMover` 使用 Unity scaled delta time，确保 Pause、`1x` 和 `2x` 对其产生正确影响。

## 8. Scene Composition

`MainCafe` 保留现有 scene 内容，并增加：

- 一个明确命名的 Phase 0 runtime root。
- Camera input 与 Camera controller components。
- Game Time service。
- Scene interaction controller。
- 至少两个带 Collider 和 `ColorSelectable` 的 placeholder objects。
- 一个带 `TimeTestMover` 的测试对象。
- 一个简单 Canvas，包含 Pause、`1x` 和 `2x` placeholder buttons。

Phase 0 不制作正式 UI art。测试对象和 buttons 只需要清楚、可辨认并方便验收。

## 9. Error Handling

- 缺少 Camera、settings 或必要引用时，输出包含 component 名称和缺失项的 Console error，并安全 disable 受影响的 component。
- 缺少可选 event listener 不视为 error。
- Raycast 没有命中 selectable object 时正常 deselect，不输出 error。
- Renderer 或 material 不满足变色要求时，`ColorSelectable` 输出明确 warning，并保持 scene 可运行。
- 无效 Game Time speed 不会改变当前状态。
- 重复的有效状态请求不会造成重复 event。
- 测试完成后恢复全局 `Time.timeScale`，避免影响其他 tests 或 Editor Play Mode。

## 10. Verification

### 10.1 Manual Play Mode Checks

- 左键拖拽可以平移 Camera。
- 拖拽结束不会误选对象。
- 滚轮可以平滑 zoom。
- Camera 无法越过 position bounds。
- Camera 无法超过最小或最大 zoom。
- 短按测试对象后对象变色。
- 选择另一个对象时前一个对象恢复原色。
- 点击空白处取消选择。
- Pause 时测试物体停止。
- Pause 时 Camera、selection 和 UI 仍可操作。
- `1x` 与 `2x` 的移动速度有明显差异。
- Console 没有未处理 error。

### 10.2 Automated Play Mode Tests

`Phase0PlayModeTests.cs` 至少包含以下独立 tests：

- Camera position stays inside configured bounds。
- Camera zoom stays inside configured bounds。
- Tapping a selectable object selects it。
- Selecting another object deselects the previous object。
- Clicking empty space clears selection。
- Pause stops the time-controlled test mover。
- `2x` moves the test mover farther than `1x` over the same real duration。
- Unsupported game speed is rejected。

## 11. Completion Gate

Phase 0 只有在以下条件全部满足后才算完成：

- Manual Play Mode checks 全部通过。
- Automated Play Mode tests 全部通过。
- Console 没有未处理 error。
- `MainCafe` scene 保存了所有必要 references。
- Phase 0 architecture 中没有提前实现 Phase 1 或后续 gameplay。
- 项目重新打开后可以进入 `MainCafe` 并重复完成验收。

