# Unity CI：glyph readback 的 dirty asset 污染修复

## 失败与根因

- [完整 Linux CI 38411927222](https://github.com/zhengparker/AnimalCafe/actions/runs/38411927222)，source `a0164da0da651592264d3a663ec6792e47af58dd`：EditMode 2485 PASS / 4 FAIL；PlayMode 1221 PASS / 0 FAIL / 5 SKIP。
- 四项失败为 `P8RRefinedBMigrationTests.RefinedB_StyleOnlyMigrationPreservesLayoutAndRollsBackEveryTarget(False/True)`、`P8RSceneWiringTests.CompleteP8R_ValidatesAndBothAuthoringCommandsAreByteStable`、`P8RSceneWiringTests.ExistingUnloadedMainCafe_RemainsInTheSameSceneListAfterNoOpAuthoring`，均被 dirty loaded assets 检查拦住。
- 本地顺序运行 glyph → migration → scene wiring，13 项中 9 PASS / 4 FAIL，准确复现上述四项。
- 新增 dirty-state 回归取得 1 PASS / 1 FAIL 的 RED：`confirm_cocoa.png` 的 anisotropy 数值、序列化内容和文件字节保持原样，但干净纹理在 GPU readback 后变成 dirty。恢复 `anisoLevel` 数值不能恢复 Editor 的 dirty 标记。

## 最小修复

- 仅修改 `Assets/Tests/EditMode/P8R/P8RGlyphMinificationTests.cs`；生产 authoring guard、游戏逻辑、场景、质量设置和 CI workflow 不改。
- 创建同尺寸、格式、色彩空间、mip 数量的临时 Texture2D，使用 `Graphics.CopyTexture` 原样复制 GPU mip；只在副本上关闭 anisotropy，`finally` 销毁副本。
- 保留 imported glyph 与 unmipped legacy 的真实 GPU 比较、原尺寸 alpha 误差 `<1e-6`、覆盖率 `0.97–1.03`、相对误差 `<legacy*0.5`。不重新生成 mip、不更改源纹理 Read/Write、不保存或统一清除 loaded assets。
- 新增 clean / already-dirty 两个用例，断言原纹理参数、序列化内容、磁盘字节与 dirty 状态均保留；测试只清理自己创建的标记，并先检查资源未改变。
- [Unity CopyTexture API](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Graphics.CopyTexture.html) 与 [isReadable API](https://docs.unity3d.com/cn/6000.0/ScriptReference/Texture-isReadable.html)：GPU copy 不要求源纹理可读，因此无需改 importer。

## 验证边界

- Windows Direct3D 定向顺序回归：15 PASS / 0 FAIL / 0 SKIP，包括原四项失败、两个 glyph 精度用例及两个新增 dirty-state 回归。
- 完整 Windows Direct3D EditMode：2491 PASS / 0 FAIL / 0 SKIP，2135.153 秒；含两个新增用例。核对全部 2491 个 test-case，原四项失败逐项映射为 PASS。证据：`outputs/ci-dirty-asset-repair/full-edit.xml`、`full-edit-review.json`。
- Windows NVIDIA OpenGL 同样顺序回归：15 PASS / 0 FAIL / 0 SKIP；包括 GPU copy 的真实图像精度、dirty 状态及后续 migration / scene wiring。证据：`outputs/ci-dirty-asset-repair/opengl-green.xml`。不能代替 Linux llvmpipe 结果。
- 完整 EditMode 生成的 31 个资源文件按测试前备份和 SHA256 恢复；MainCafe、P12 scene、EditorBuildSettings 和 QualitySettings 的 hash 与测试前一致。实际代码改动只有一个测试文件。
- 本地验证后，Owner 已授权 push 修复并启动一次完整云端 CI；发布前云端状态为 NOT_RUN。将使用 `test_scope=full`、`allow_unity_login=true`，不自动重试登录、不 rerun、不修改 Secrets 或 merge。准确发布 commit 与 run 记录保存在 `outputs/ci-dirty-asset-repair/cloud-dispatch.json`；云端最终结果不能由本地通过推断。
- 原始完整证据与失败堆栈：`outputs/ci-review-38411927222/CI_Result_Review.md`。
- 本轮 RED/GREEN：`outputs/ci-dirty-asset-repair/baseline.xml`、`dirty-state-red.xml`、`focused-gpu-copy-green.xml`；逐项失败 message / stack 保存在 `red-green-evidence.json`。
