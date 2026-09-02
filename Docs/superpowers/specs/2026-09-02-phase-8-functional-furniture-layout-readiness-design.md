# AnimalCafe Phase 8 — Functional Furniture & Layout Readiness Spec

> 状态：`Planned / Not Implemented`
>
> 本 Spec 从当前 Roadmap 的 Phase 8 contract 整理而来，不代表 implementation 已获授权或完成。

## Goal

让未来经营系统通过 furniture capabilities 和 interaction anchors 使用 Layout，而不是依赖固定 Scene object names。

## Included

- Furniture capability definitions。
- Cash Register、Coffee Machine 和 Pick-up Surface requirements。
- Employee / Customer interaction anchors。
- Pick-up Surface Slot。
- Anchor rotation、world-coordinate conversion 与 validity。
- Layout readiness report。
- 缺少必要功能或路径准备条件时禁止营业，并返回具体原因。

## Core Rules

- Anchor 使用 furniture-local coordinates；家具移动或旋转后重新计算 world position。
- Furniture move 后旧 anchor reference 必须 invalidated。
- Readiness 不能只返回笼统的“布局无效”；必须指出 furniture、capability、anchor 或 failure reason。
- Phase 8 只建立 Cafe Loop 所需的最小 Layout contract，不实现 Customer、Employee、Order 或 Queue gameplay。

## Required Test Cases

- 四个 cardinal rotations 下的 anchor coordinates。
- Move/rotate 后旧 anchors invalidated，新 anchors 与 visual/data 一致。
- Blocked、locked、occupied 或墙外 anchor 被标记 invalid。
- 缺少 Cash Register、Coffee Machine 或 Pick-up Surface 时 readiness 失败。
- Readiness report 包含具体 furniture identity 和 failure reason。
- Invalid readiness 不产生 partial mutation。
- Scene gizmos/feedback 与 data result 一致。
- Phase 1–7 regression 保持通过。

## Not Included

- Customer、Employee、Order、Queue 或正式 Navigation gameplay。
- Save/Load。
- 新 Decoration feature、建墙、拆墙或视觉重设计。

## Gate

当前仓库没有 Phase 8 implementation 或通过的 test evidence。开始 coding 前仍需要 Studio Owner 批准最终 spec、test cases 和 implementation plan。
