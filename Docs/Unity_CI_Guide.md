# Unity CI：配置与使用 / Setup and usage

GitHub Actions 对每个 PR、main push 和手动运行，使用 **Unity 6000.5.5f1** 分别执行现有完整 EditMode、PlayMode suites。没有 testFilter，也不会将 Linux CI 当作 Windows Editor、Player 或移动设备验收。

## 文件与执行方式

- `.github/workflows/unity-tests.yml`：两项独立测试 jobs；任一失败不会取消另一项。固定 Actions commit，Library cache 按版本、平台、测试模式隔离；重复更新同一 PR 时取消旧运行。
- artifacts 保存 NUnit XML 和 GameCI 生成的 logs，保留 14 天；即使测试失败也执行上传。若启动前缺少 license 或运行未产生文件，查看对应 job log。
- `Unity CI` 汇总 check：只有两套 tests 均成功，且均产生至少一项 PASS、零 FAIL 的 NUnit test-run XML，才通过。缺少配置、编译失败、取消或跳过 job 都不能得到汇总 PASS。已有 opt-in evidence SKIP 原样报告。
- PR checkout 使用 `pull_request`，不使用 `pull_request_target`。fork PR 默认拿不到 Secrets，会因缺少 license 明确失败；不能通过自动暴露 Secrets 来解决。

## 一次性配置 / One-time configuration

1. 打开仓库 **Settings → Secrets and variables → Actions → New repository secret**。
2. Unity Personal：按 [GameCI Activation](https://game.ci/docs/github/activation/) 配置 `UNITY_LICENSE`、`UNITY_EMAIL`、`UNITY_PASSWORD`。Unity Pro：配置 `UNITY_SERIAL`、`UNITY_EMAIL`、`UNITY_PASSWORD`。只在 GitHub Secrets 页面输入敏感内容，不写入 repo、log 或聊天。
3. workflow push 到现有 PR branch 后，查看该 PR 的 Checks；本 workflow 首次进入默认 branch 后，也可在 **Actions → Unity tests → Run workflow** 手动运行。
4. 两套测试均成功后，在 main 的 branch protection / Ruleset 中启用 **Require status checks to pass before merging**，选择 **Unity CI**。仅添加 workflow 不会自动阻止 merge；branch rules 需要仓库 owner 单独设置。
5. Unity 升级时同步修改 ProjectVersion 与 workflow 版本，并先确认对应 GameCI image 可用。

## 当前证据与限制 / Evidence and limits

- 2026-10-10 已通过 Docker Hub 只读查询确认 `unityci/editor:ubuntu-6000.5.5f1-base-3` 存在。
- 已通过 GitHub API 确认 checkout/cache/upload-artifact/GameCI 的固定 v4 commit 存在。
- 本地 YAML parser、3 个 Bash blocks 语法检查通过；报告 guard 的成功、失败、全部 SKIP、无 NUnit XML、inconclusive 五种输入均得到预期结果，也通过真实 Unity XML（55 PASS、0 FAIL、2 SKIP）检查。Unity Test Framework 1.7 可将含可选 SKIP 的成功运行标成 `Skipped:Ignored`；仅在有实际 PASS、零 FAIL、零 inconclusive 时接受此状态。这是配置检查，不等于 GitHub 上真实 Unity 测试运行。
- 仓库 Actions Secrets 名称列表当时为空，未读任何 Secrets 内容。云端真实运行 **NOT_RUN**，需要有效 license 配置和发布 workflow 后再验证。
- 2026-10-11 Owner 完成 Personal 配置后，只读 metadata 已确认 `UNITY_EMAIL`、`UNITY_PASSWORD`、`UNITY_LICENSE` 三个名称存在；未读值。本轮将 workflow 发布到现有 P12 PR branch，Secrets 能否成功激活以该 PR 的云端运行结果为准，不能由“名称齐全”推断 PASS。
- 此次未修改 branch protection / Ruleset，未注册 self-hosted runner，未安装 Unity、modules 或 Code Coverage package。
- GitHub-hosted Linux 全套测试如出现平台差异，应保留失败结果、定位原因；不能删掉失败测试或将其算作 PASS。

参考：[GameCI Test runner](https://game.ci/docs/github/test-runner/)。
