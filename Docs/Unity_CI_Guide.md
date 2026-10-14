# Unity CI：配置与使用 / Setup and usage

2026-10-13修订：Owner 已批准**同仓库、目标为 main 的非 Draft PR 自动测试**。新建、更新 commit、重开或从 Draft 转为 Ready 时运行完整回归；没有 main push/merge 后的重复运行。使用 **Unity 6000.5.5f1**，一次激活后顺序执行 EditMode、PlayMode。**Merge 必须由 Owner 确认，不启用 auto-merge。** Fork PR 不自动使用 Unity Secrets。

手动入口仍保留：勾选本次允许 Unity 登录/测试后，`test_scope=full` 执行完整回归；`remaining-ci-failures` 只诊断两个 glyph 和一个 UI transition，不满足完整 `Unity CI` check。Linux CI 不替代 Windows Editor、Player、移动设备或 Owner 视觉验收。

## 文件与执行方式

- `.github/workflows/unity-tests.yml`：单一测试job，取消双job重复登录。固定Actions commit和GameCI源代码commit；运行前核对激活脚本SHA256并去掉其五轮重试，失败即退出；全分支共用concurrency group，最多 100 个待运行排队，不同时认证或取消正在使用账号的任务。Library cache按版本、平台和源文件隔离。
- artifacts 保存 NUnit XML 和 GameCI 生成的 logs，保留 14 天；即使测试失败也执行上传。若启动前缺少 license 或运行未产生文件，查看对应 job log。
- `Unity CI` 汇总 check：只有两套 tests 均成功，且均产生至少一项 PASS、零 FAIL 的 NUnit test-run XML，才通过。缺少配置、编译失败、取消或跳过 job 都不能得到汇总 PASS。已有 opt-in evidence SKIP 原样报告。
- PR 使用 `pull_request` 测试临时 merge commit，覆盖与 main 的集成；不使用 `pull_request_target` 执行 PR 代码。Fork / Draft 的测试 job 不运行，汇总也不能得到成功。只更新 PR 标题、描述或 comments 不触发测试。
- `.github/scripts/unity-authentication-guard.py`：自动登录前读取最近的 Actions checkpoint；上次实际 Unity 尝试未生成两套有效、非空 NUnit XML，则阻止所有分支的后续自动登录。没有完整 XML 也可能是编译/启动失败，不能直接归因为密码错误。普通 assertion 失败有两套 XML 时允许修复后的新 commit 自动复测。
- 认证 checkpoint 按 `run_attempt` 命名并保存 90 天；读取完整有界 run metadata，按实际 attempt 的开始时间排序，包含旧 run 的较晚 rerun 和当前 run 的 prior attempts。API 不可用、历史检查不完整/超出有界范围、checkpoint 缺失且 job 记录实际调用过 Unity，都在凭据使用前 fail closed。手动 `full` 产生两套有效非空 XML 才解除认证 gate；普通测试 assertion 失败仍须修复才能满足合并检查。Focused diagnosis 不能清除旧 halt，其认证/启动失败仍会设置 halt。更新 Secret 本身不会清除暂停。`.github/scripts/test_unity_authentication_guard.py` 为离线回归，不登录 Unity。

## 自动失败跟进 / Automatic failure follow-up

Codex 的 `unity-ci` heartbeat 在本线程定期核对当前 open PR 的最新 SHA 和完整 CI；GitHub Actions 自身只运行测试，不修代码。普通测试/编译失败可由 Codex 在对应隔离 branch 中定位、修复、验证并 push，让新 commit 触发一次新的 PR CI。不得删失败测试、放宽断言或把 SKIP 当 PASS；不直接写 main、force push 或 merge。每个失败签名最多两轮经过验证的自动修复；无进展、设计变更、证据缺失或需要凭据时停止并报告。

认证/激活失败不自动 rerun、不重试登录、不修改 Secrets，暂停跟进并通知 Owner。Owner 修正账号/license 后，通过手动 full run 明确授权恢复；之后再恢复 Codex 跟进。正常等待或状态未变时保持安静；成功后报告完整 PASS/FAIL/SKIP 和 run 链接，merge 仍等待 Owner。

跟进仅在 Codex app 的本地任务与 scheduler 可运行、GitHub 连接可用时执行，并非 GitHub 云端持续运行的修复机器人。

## 一次性配置 / One-time configuration

1. 打开仓库 **Settings → Secrets and variables → Actions → New repository secret**。
2. Unity Personal：按 [GameCI Activation](https://game.ci/docs/github/activation/) 配置 `UNITY_LICENSE`、`UNITY_EMAIL`、`UNITY_PASSWORD`。Unity Pro：配置 `UNITY_SERIAL`、`UNITY_EMAIL`、`UNITY_PASSWORD`。只在 GitHub Secrets 页面输入敏感内容，不写入 repo、log 或聊天。
3. 正常 PR 不再需要勾选或手动 Run。认证保护暂停后，先核实账号已解锁及 Secrets 正确；再到 **Actions → Unity tests → Run workflow** 选择对应 branch，勾选本次登录授权，保留 `test_scope=full`。只有诊断已知剩余失败时选择 `remaining-ci-failures`。
4. main 的 branch protection / Ruleset 需启用 **Require status checks to pass before merging → Unity CI**，并要求 branch 与 main 保持最新。设置不等于 merge 授权；Owner 仍须主动确认合并。规则的实际发布/read-back 状态见本次交付结果；workflow 文件本身不能证明服务器端已启用规则。
5. Unity 升级时同步修改 ProjectVersion 与 workflow 版本，并先确认对应 GameCI image 可用。

## 当前证据与限制 / Evidence and limits

- 最新完整 [Linux CI 38449626517](https://github.com/zhengparker/AnimalCafe/actions/runs/38449626517)，source `8c71133e23669765846721dacbaf62da4380d5f8`：EditMode **2491 PASS / 0 FAIL / 0 SKIP**；PlayMode **1221 PASS / 0 FAIL / 5 SKIP**。P12 Edit 49、Play 83 全 PASS，Linux llvmpipe glyph / UI transition / dirty-asset 修复全部通过。P12 已合并到 main `302f3e9`，与 tested source 只差 Beginner Guide 的说明句。
- 以下是历史诊断记录；旧失败、Secrets 未配置或 NOT_RUN 仅描述当时状态，不能覆盖上述最终完整回归。当前自动触发版本的首次运行结果应单独核对，不由旧成功推断。

- 2026-10-13 Owner 已授权并运行两轮完整 Linux CI。[38392233116](https://github.com/zhengparker/AnimalCafe/actions/runs/38392233116) 的 EditMode 为 2487 PASS / 2 FAIL，PlayMode 为 1220 PASS / 1 FAIL / 5 SKIP；P12 专项 83 PASS。剩余两个 glyph 缩图误差和一个动画等待时钟问题，不能宣称完整 CI 通过。
- 本轮剩余 3 项已在 [Linux 定向验证 38410016002](https://github.com/zhengparker/AnimalCafe/actions/runs/38410016002) 全部通过。动画等待共用受控帧预算；glyph readback 控制并恢复纹理 anisotropy，排除额外过滤，并新增原像素一致性断言。完整回归仍需独立结果，见 `Unity_CI_Remaining_Repair_2026-10-13.md`。默认完整回归与单次激活保护保持原样。

- 2026-10-10 已通过 Docker Hub 只读查询确认 `unityci/editor:ubuntu-6000.5.5f1-base-3` 存在。
- 已通过 GitHub API 确认 checkout/cache/upload-artifact/GameCI 的固定 v4 commit 存在。
- 本地 YAML parser、3 个 Bash blocks 语法检查通过；报告 guard 的成功、失败、全部 SKIP、无 NUnit XML、inconclusive 五种输入均得到预期结果，也通过真实 Unity XML（55 PASS、0 FAIL、2 SKIP）检查。Unity Test Framework 1.7 可将含可选 SKIP 的成功运行标成 `Skipped:Ignored`；仅在有实际 PASS、零 FAIL、零 inconclusive 时接受此状态。这是配置检查，不等于 GitHub 上真实 Unity 测试运行。
- 仓库 Actions Secrets 名称列表当时为空，未读任何 Secrets 内容。云端真实运行 **NOT_RUN**，需要有效 license 配置和发布 workflow 后再验证。
- 2026-10-11 Owner 完成 Personal 配置后，只读 metadata 已确认 `UNITY_EMAIL`、`UNITY_PASSWORD`、`UNITY_LICENSE` 三个名称存在；未读值。本轮将 workflow 发布到现有 P12 PR branch，Secrets 能否成功激活以该 PR 的云端运行结果为准，不能由“名称齐全”推断 PASS。
- 发布后同步远程 main 的 CI-only commit，解决同路径 workflow 的 add/add 冲突。首次 P12 云端运行 [38200045287](https://github.com/zhengparker/AnimalCafe/actions/runs/38200045287) 的配置检查成功，但 GameCI v4.4 wrapper 下载的 CLI 拒绝 `--no-coverageEnabled`，两项 jobs 在 Unity 激活前失败，不能据此判断 license 无效。现固定官方 v4.3.2 commit `fa6ced25861c16ef56187828c43f76d00df43a23`（不下载浮动 CLI），保留完整 suites / XML guard / artifacts。旧版含 Unity 内置 coverage 启动参数，但主项目不新增 Code Coverage package、不以 coverage 代替测试；禁用额外 GitHub check 写入，只用 Actions job 和汇总 check。
- 此次未修改 branch protection / Ruleset，未注册 self-hosted runner，未安装 Unity、modules 或 Code Coverage package。
- GitHub-hosted Linux 全套测试如出现平台差异，应保留失败结果、定位原因；不能删掉失败测试或将其算作 PASS。

参考：[GameCI Test runner](https://game.ci/docs/github/test-runner/)。


## 认证重试保护（2026-10-12）

旧两次CI各有两个job；每个job日志记录15条Unity登录失败及五轮激活调用，收到HTTP401后继续重试。Owner的邮件明确连续10次登录失败会锁定60分钟。密码已由Owner更新；未读取Secret值，也未验证新密码。

新增`.github/scripts/disable-gameci-activation-retries.py`只改写固定GameCI副本的`activate.sh`：先检查完整SHA256，再保留原激活参数去掉while/sleep/retry。上游源码变化会在使用凭据前停止。workflow由pinned checkout下载同一v4.3.2 runner，再用local action调用修改后的副本，保持原测试实现与license处理方式。

**边界：一次Editor激活调用不等于一次HTTP登录请求。** Unity自身内部仍可能重连；本轮消除的是已确认导致重复放大的GameCI五轮调用和两个并行认证job。未改Unity二进制或拦截登录协议，不承诺所有来源都不可能触发账号锁定。自动 PR 授权已由 Owner 给出；实际认证/启动失败仍须人工核实并通过明确授权的手动 full run 恢复。

report guard明确要求`editmode-results.xml`与`playmode-results.xml`都存在、有实际PASS且零FAIL；不能用一套结果替代完整验收。此轮YAML解析、Python AST语法、公开固定源脚本hash匹配/改写、生成Bash及workflow命令的`bash -n`静态语法检查和`git diff --check`均通过；没有执行生成的激活或workflow命令。此轮仅进行配置/源码静态检查，**Unity登录、Unity tests、云端CI、密码有效性验证均NOT_RUN**。配置发布到P12 branch后，workflow仍保持GitHub disabled；发布不包含enable/dispatch，恢复登录或测试需另行授权。
