# AnimalCafe Phase 6 Beginner Guide — Basic Decoration Mode

> 状态：`Completed`。本文件同时保存原 manual-review Excel 和 intermediate reports 中的最终验收结论。

## 1. Phase 6 做了什么

Phase 6 让玩家在 `MainCafe` 进入 Decoration Mode，浏览四种 Counter，选择、拖动、旋转、Confirm、Cancel 和 Store 家具，并看到合法或非法摆放反馈。

简单例子：移动已有 Counter 后按 Cancel，Counter 必须准确回到原来的位置和 rotation；只有按 Confirm 才能修改正式 Layout。

## 2. 主要规则

- 进入 Decoration Mode 暂停 Game Time，退出时恢复进入前的速度。
- Preview 是临时状态，不能提前修改正式 Layout。
- Furniture footprint 来自 `FurnitureDefinition`，UI 不重复定义规则。
- Rotate 后 footprint width/depth 与 visual 一起更新。
- Illegal、overlap、locked 或越界 placement 不能 Confirm。
- UI、Modal、Scene drag、Camera pan/zoom 有明确 input ownership。
- Confirm、Cancel 和 Store 都是 transaction，不允许留下半完成状态。

## 3. Test cases 与完成记录

Studio Owner 最终完成 `P6-M-001–022` 与 `P6-M-024–030`，结果为 `29 / 29 PASS`。覆盖内容包括：

- Decoration Mode enter/exit 与 Game Time restore。
- 四种 Counter footprint、drag、rotate、Confirm、Cancel、Store。
- Valid/invalid feedback、overlap、Entrance clearance 和 Floor-edge clamp。
- Catalogue、floating actions、Toast、Modal dismissal 和 input blocking。
- Existing/new furniture rollback，以及切换 active Preview。
- Mouse、Touch ownership、Camera drag、edge auto-pan 和 Safe Area。
- Portrait/landscape layouts、beginner comprehension、reload 和 Console cleanliness。

`P6-M-023` 的真实 Android + iOS two-finger Pinch 并非失败；它由 Studio Owner 明确移至 Phase 51，不计入 Phase 6 denominator。

最终 automated evidence：

- EditMode `1136 / 1136` passed。
- Editor PlayMode `446 / 446` passed。
- Failed、Skipped、Inconclusive 均为 `0`。
- Windows standalone build：`Success`，无 C# warning/error。
- Phase 4 production validator：`8 / 8 valid`、`0 issues`。

## 4. 没有做什么

Phase 6 没有实现家具经营能力、Interaction Anchors、NPC path validation、家具商店、Atmosphere 或 Save/Load。Confirm 的 Layout 只要求在当前 runtime session 内保持；完整 persistence 属于 Phase 17。

## 5. Beginner glossary

| Term | 简单解释 |
|---|---|
| `Preview` | Confirm 前看到的临时家具状态。 |
| `Footprint` | 家具占用的全部 Grid cells。 |
| `Confirm` | 验证成功后把 Preview 正式写入 Layout。 |
| `Cancel` | 放弃 Preview，并恢复开始操作前的状态。 |
| `Store` | 确认后从当前 Layout 移除家具。 |
| `Input ownership` | 决定当前 drag 属于家具、Camera 还是 UI。 |

## 6. 最终结论

Phase 6 的 approved design、implementation、test cases、manual acceptance 和 full regression 已全部完成。原 Excel 与 intermediate closeout Markdown 可以删除；本 Guide 与 Phase 6 Spec 是长期 documentation。
