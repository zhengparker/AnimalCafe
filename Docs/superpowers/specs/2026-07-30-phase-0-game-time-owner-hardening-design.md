# Phase 0 Game Time Owner Hardening Design

> 状态：等待用户书面确认
>
> 类型：Phase 0 bug hardening
>
> 影响系统：`GameTimeService`

## 1. 这个修复是干什么的

可以把 `GameTimeService` 想成教室里唯一可以控制时钟的人。

正常情况下只有一个人负责 Pause、`1x` 和 `2x`。如果 Scene 配置错误，意外出现第二个 `GameTimeService`，第二个不能也去控制同一个全局时钟，否则两个 object 会互相覆盖状态。

本修复确保第一个出现的 `GameTimeService` 是唯一 owner，后来出现的 duplicate 不能改变全局游戏时间。

## 2. 已批准行为

- 第一个启用的 `GameTimeService` 成为 `activeOwner`。
- 单一 owner 继续正常支持 Pause、`1x` 和 `2x`。
- Duplicate 调用 `TrySetSpeed()` 时：
  - 返回 `false`；
  - 不改变 `Time.timeScale`；
  - 不改变自己的 `CurrentSpeed`；
  - 不发布 `GameSpeedChanged` event；
  - 写入一条明确的 Console warning。
- Duplicate 被销毁时，不改变真正 owner 或 `Time.timeScale`。
- Owner 被销毁时，`Time.timeScale` 恢复为 `1x`，并清除 ownership。
- 已存在的 duplicate 不自动接管 ownership。这样 Scene 配置错误不会被静默隐藏。

## 3. Root Cause

当前 `Awake()` 只在 `activeOwner == null` 时登记 owner，但 `TrySetSpeed()` 没有检查调用者是不是 owner。

因此 duplicate 虽然不是 `activeOwner`，仍然可以修改全局 `Time.timeScale`、改变自己的状态并发布 event。

## 4. 修改范围

只修改：

```text
Assets/Scripts/Core/Time/GameTimeService.cs
Assets/Tests/PlayMode/Phase0PlayModeTests.cs
```

不修改：

- Scene 或 Prefab；
- Phase 1 / Phase 2 Layout code；
- `.gitignore` 或 `AnimalCafe.slnx`；
- P2 worktree cleanup；
- 自动接管、multi-scene architecture 或新的 service framework。

## 5. Test-first Plan

先增加 PlayMode bug tests，并在旧代码上确认失败：

1. Duplicate 不能改变 `Time.timeScale`。
2. Duplicate 返回 `false` 且保持自己的 `CurrentSpeed`。
3. Duplicate 不发布 `GameSpeedChanged` event。
4. Duplicate 被销毁时不影响 owner。
5. Owner 被销毁时恢复 `1x`，duplicate 不自动接管。
6. 原有单一 owner Pause、`1x`、`2x` tests 继续通过。

然后只在 `TrySetSpeed()` 增加 owner guard，完成最小修复。

## 6. Acceptance Gate

- 新 bug tests 在旧代码上得到预期 RED。
- 修复后 focused PlayMode tests 全部通过。
- 完整 EditMode 和 PlayMode regression 全部通过。
- Console 只有 test 明确预期的 duplicate warning，没有意外 error。
- 不 commit、不 push；由用户使用 GitHub Desktop 处理。
