# Unity 精简回归 / Shared compact regression

本地与 GitHub Actions 共用 `compact-edit.filter.txt`、`compact-play.filter.txt`。EditMode 4个 selectors，当前1522 cases；PlayMode 100个 selectors，当前180 cases。范围包括P15营业周期、路线协调、相关导航和真实经营布局。本轮新增6项已提交Working预留交接及5项既有Paid等待下的取餐侧回归，沿用全部既有测试范围和断言。

CI 仍只通过 `workflow_dispatch` 手动触发，Unity登录仍须勾选 `allow_unity_login`。`test_scope` 默认 `compact`；`full` 保留为主动完整回归。两者成功均可满足 `Unity CI`，diagnosis scopes不满足。Unity版本仍6000.5.5f1；一次activation后顺序跑两模式，禁止自动activation retry。150分钟是含冷导入的上限，不是预计耗时。

`p15-ci-four-failures` 只用于核查本次四项Linux布局故障及完整路线协调EditMode class，使用原case、原时限，不打开live observer。它不替代 `compact`，也不满足必需的 `Unity CI` check；诊断通过后仍跑正式精简CI。

## 本地使用相同范围

从项目根目录运行，使用已配置好的 Unity CLI / Editor：

```powershell
$editFilter = python Tools/Testing/unity_regression.py filter --mode edit
$playFilter = python Tools/Testing/unity_regression.py filter --mode play
# 把上面对应filter传给现有安全回归runner的 -Filter；不要再维护另一份列表。
# Or pass --filter to the installed Unity CLI with the matching test mode.
```

GameCI `testMode: all` 使用两份列表的union；namespace明确区分EditMode/PlayMode，因此各平台只发现自身对应的相同范围。compact不附加诊断或到店时间重放参数。

## 结果核查 / Result validation

```powershell
python -B -m unittest discover -s Tools/Testing -p test_unity_regression.py
python Tools/Testing/unity_regression.py verify --scope compact --results artifacts/unity
```

结果目录必须包含唯一的 `editmode-results.xml`、`playmode-results.xml`。缺报、0 PASS、FAIL、Inconclusive、重复fullname、selector漏跑、异范围case或discovery数量漂移都会失败。SKIP原样报告，不转换成PASS。2026-11-16修复前的本地记录为1459P和179P/1SKIP；当前源码须重新验证，既有D2指定状态未捕获的SKIP仍未覆盖。

若以后有意增加/删除测试，需要同步检查selector文件与 `unity_regression.py` 的 `EXPECTED` discovery基线，避免悄悄缩减范围。Windows本地通过不代表Linux云端已通过；CI结果必须单独记录对应commit。
