# Unity CI：配置与使用 / Setup and usage

2026-10-13修订：GitHub Actions **仅手动触发**，且需勾选本次允许Unity登录/测试。使用 **Unity 6000.5.5f1**，一次激活后顺序执行 EditMode、PlayMode。`test_scope` 默认 `full`，执行完整回归；`remaining-ci-failures` 只诊断两个 glyph 缩图用例和一个 UI transition 用例，使用固定 testFilter。诊断运行不会通过完整 `Unity CI` 汇总 check。更新 Secrets 不等于允许运行；Linux CI 也不替代 Windows Editor、Player 或移动设备验收。

## 文件与执行方式

- `.github/workflows/unity-tests.yml`：单一测试job，取消双job重复登录。固定Actions commit和GameCI源代码commit；运行前核对激活脚本SHA256并去掉其五轮重试，失败即退出；全分支共用concurrency group，不同时启动认证。Library cache按版本、平台和源文件隔离。
- artifacts 保存 NUnit XML 和 GameCI 生成的 logs，保留 14 天；即使测试失败也执行上传。若启动前缺少 license 或运行未产生文件，查看对应 job log。
- `Unity CI` 汇总 check：只有两套 tests 均成功，且均产生至少一项 PASS、零 FAIL 的 NUnit test-run XML，才通过。缺少配置、编译失败、取消或跳过 job 都不能得到汇总 PASS。已有 opt-in evidence SKIP 原样报告。
- 取消PR/main push自动触发，保留`workflow_dispatch`，登录授权checkbox默认false。未来获得Owner单独授权后才enable并手动选择对应branch运行；不自动暴露Secrets给fork PR。

## 一次性配置 / One-time configuration

1. 打开仓库 **Settings → Secrets and variables → Actions → New repository secret**。
2. Unity Personal：按 [GameCI Activation](https://game.ci/docs/github/activation/) 配置 `UNITY_LICENSE`、`UNITY_EMAIL`、`UNITY_PASSWORD`。Unity Pro：配置 `UNITY_SERIAL`、`UNITY_EMAIL`、`UNITY_PASSWORD`。只在 GitHub Secrets 页面输入敏感内容，不写入 repo、log 或聊天。
3. 获得 Owner 明确允许登录/测试后，在 Actions 启用 workflow，选择对应 branch，勾选本次登录授权。正常验收保留 `test_scope=full`；只有定位已知剩余失败时选择 `remaining-ci-failures`。
4. 两套测试均成功后，在 main 的 branch protection / Ruleset 中启用 **Require status checks to pass before merging**，选择 **Unity CI**。仅添加 workflow 不会自动阻止 merge；branch rules 需要仓库 owner 单独设置。
5. Unity 升级时同步修改 ProjectVersion 与 workflow 版本，并先确认对应 GameCI image 可用。

## 当前证据与限制 / Evidence and limits

- 2026-10-13 Owner 已授权并运行两轮完整 Linux CI。[38392233116](https://github.com/zhengparker/AnimalCafe/actions/runs/38392233116) 的 EditMode 为 2487 PASS / 2 FAIL，PlayMode 为 1220 PASS / 1 FAIL / 5 SKIP；P12 专项 83 PASS。剩余两个 glyph 缩图误差和一个动画等待时钟问题，不能宣称完整 CI 通过。
- 本轮动画等待按受控动画帧预算验证，连续 10 个慢帧已取得本地 RED/GREEN；图标增加来源尺寸、格式、base/mip 像素误差诊断，等待 Linux 证据。现有精度阈值与 UI owner 断言保持原样；默认完整回归与单次激活保护保持原样。

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

**边界：一次Editor激活调用不等于一次HTTP登录请求。** Unity自身内部仍可能重连；本轮消除的是已确认导致重复放大的GameCI五轮调用和两个并行认证job。未改Unity二进制或拦截登录协议，不承诺所有来源都不可能触发账号锁定。仍需人工确认账号已解锁、邮箱/密码可用，且获得Owner单独授权才可恢复。

report guard明确要求`editmode-results.xml`与`playmode-results.xml`都存在、有实际PASS且零FAIL；不能用一套结果替代完整验收。此轮YAML解析、Python AST语法、公开固定源脚本hash匹配/改写、生成Bash及workflow命令的`bash -n`静态语法检查和`git diff --check`均通过；没有执行生成的激活或workflow命令。此轮仅进行配置/源码静态检查，**Unity登录、Unity tests、云端CI、密码有效性验证均NOT_RUN**。配置发布到P12 branch后，workflow仍保持GitHub disabled；发布不包含enable/dispatch，恢复登录或测试需另行授权。
