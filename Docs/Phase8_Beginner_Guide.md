# AnimalCafe Phase 8 Beginner Guide — Functional Furniture & Layout Readiness

> 当前状态：`Not Started / Tests Not Run`。本 Guide 解释计划中的功能，但不把计划误写成已完成。

## 1. Phase 8 要解决什么

现在玩家可以摆放家具，但未来的员工和顾客不能靠 Scene object name 猜测“哪里结账、哪里制作咖啡、哪里取餐”。Phase 8 要让家具明确报告自己的功能和可用 interaction positions。

简单例子：Cash Register 被旋转 90° 后，员工操作点和顾客排队点也必须一起旋转。如果顾客点落在墙里，Layout Readiness 应说明是哪台 Cash Register、哪个 anchor 被阻挡。

## 2. 计划中的内容

- Furniture capability definitions。
- Cash Register、Coffee Machine、Pick-up Surface。
- Employee / Customer anchors。
- Anchor rotation 和 validity。
- Layout readiness report 与具体 failure reason。

## 3. Required test cases

以下 cases 已定义，但当前尚未执行，因此不能标为 PASS：

- 四个 rotations 下 anchor coordinates 正确。
- Furniture move/rotate 后旧 anchor invalidated。
- Blocked/locked/occupied/out-of-bounds anchor 被拒绝。
- 缺少必要 functional furniture 时不能进入营业准备状态。
- Readiness report 指出具体 furniture 和原因。
- Invalid operation 不留下 partial mutation。
- Scene feedback、gizmos 和 data 一致。
- Phase 1–7 full regression 保持通过。

## 4. 完成条件

Phase 8 只有在 approved implementation、automated tests、Engineering/QA review、Studio Owner manual acceptance、merge 和 merged-main regression 全部完成后，才能改成 `Completed` 并进入 Phase 8R。

## 5. Beginner glossary

| Term | 简单解释 |
|---|---|
| `Capability` | 一件家具能提供什么经营功能。 |
| `Anchor` | NPC 应站立或交互的精确位置和方向。 |
| `Local coordinates` | 相对于家具本身的位置；家具旋转时会一起变化。 |
| `Layout Readiness` | 检查咖啡厅是否具备开始经营所需的家具与安全位置。 |
| `Invalidation` | 家具变化后宣布旧结果不再可用，避免 NPC 使用过期位置。 |
