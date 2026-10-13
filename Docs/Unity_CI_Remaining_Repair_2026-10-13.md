# Unity CI 剩余 3 项修复（2026-10-13）

本记录接续 `Unity_CI_Failure_Review_2026-10-13.md`。前轮完整 CI `38392233116` 仍有两个 glyph 缩图测试及一个 UI transition 测试失败；P12 专项 83 项通过。本轮仅修测试的时钟与采样控制，保留游戏行为、图标原图、Importer、项目画质和原始精度 / owner 断言。

## 动画等待

受控动画每帧推进 `1/60` 秒，旧等待 helper 却用真实时间 2 秒截止。Linux 慢帧会让真实时间先耗尽，Catalogue alpha 仍大于零。

`Phase6DecorationUiPlayModeTests.cs` 的等待 helper 改为 120 帧模拟预算，稳定性观察也使用同一帧步长。仍有严格终止上限，不忽略异常。原用例加入连续 10 个 250ms 慢帧；本地 RED 为 0 PASS / 1 FAIL，修复后 1 PASS / 0 FAIL。Linux 同一用例通过。

## 图标采样

短诊断 `38404802917` 确认连 256px 原尺寸 GPU copy 都与 PNG alpha 不一致。单图标像素采集 `38407096648` 进一步确认：源 PNG 与 checkout 完全一致，CPU 解码匹配 PNG；imported / legacy 的 GPU copy 完全相同，却多了共同的 3×3 模糊核（中心约 0.332、边约 0.122、角约 0.045）。因此不是图标文件变更、前序 fixture 干扰或 mip 层号导致。

PC 画质的全局 anisotropic filtering 为 ForceEnable，非零纹理等级会被提升到至少 9；见 [Unity Texture.anisoLevel](https://docs.unity3d.com/ja/2020.3/ScriptReference/Texture-anisoLevel.html)。此 benchmark 测量正交、像素对齐的 mip alpha coverage，额外 anisotropic 采样会混入另一层过滤。

`P8RGlyphMinificationTests.cs` 的 GPU readback scope 暂时将输入纹理 `anisoLevel` 设为 0，并在 `finally` 恢复调用前等级。仍使用真实 GPU、导入的 mip、trilinear / bilinear 模式；没有降低原来的覆盖率或相对误差标准。新增原尺寸 GPU copy alpha 误差 < `1e-6` 的断言，防止其它过滤再次污染基准。

Linux scoped 验证恢复原尺寸误差为零，32px 总误差由 `1.533857` 降为 `0.007269`，64px 由 `0.782777` 降为 `0.004827`，两个原用例通过。临时原始像素导出 helper 已移除；数据与分析保留在 outputs。

## 云端与本地证据

- [Linux 3 项验证 38410016002](https://github.com/zhengparker/AnimalCafe/actions/runs/38410016002)，source `59d944e637c27c79c41ac25f04827841c6178c2b`：EditMode 2 PASS / 0 FAIL、PlayMode 1 PASS / 0 FAIL，renderer llvmpipe LLVM 15.0.7。
- 该 run 的 Unity CI 汇总 check 故意失败，因为 `remaining-ci-failures` 不能替代完整回归；两个 suite XML、tests job、结果 guard 均通过。
- Artifact SHA256：`b214ecfe95a5d87f0e1eb995500c87a1699acd3d79300d54738fd010bf64efa0`，已匹配。完整证据在 `outputs/ci-review-38410016002/artifacts/`。
- scoped sampler 本地检查：glyph 及原资源契约共 12 PASS / 0 FAIL。连续慢帧 RED/GREEN 位于 `outputs/ci-remaining-repair/`。
- workflow 增加固定诊断范围；默认仍为 `full`，仍只手动触发、勾选登录授权、单次激活，无自动重试。项目场景 / BuildSettings / QualitySettings 保留原内容。
- 清理临时导出代码后再做本地定向检查，并启动 `test_scope=full` 的完整云端回归；完整云端结果在启动时仍为 PENDING，不能由这 3 项 PASS 宣称全部测试通过。
