# Unity CI setup

The `Unity tests` workflow is **manual only**, with an explicit login authorization
checkbox. It activates once, then runs EditMode and PlayMode sequentially.
GameCI's activation retry loop is removed from its pinned runner before use.
Unity Editor may still reconnect internally during that one activation invocation.

**Each manual run requires the owner's authorization for Unity login/testing.**
The workflow was enabled for owner-authorized P12/P13 runs; it has no automatic
push or pull-request trigger. Updating Secrets does not authorize a run.

## One-time activation setup

1. Obtain a Unity license file (`.ulf`) for the account and license tier used
   to run the project. Follow the [GameCI activation guide](https://game.ci/docs/github/activation/).
2. In GitHub, open **Settings** > **Secrets and variables** > **Actions** >
   **New repository secret**.
3. Create a secret named `UNITY_LICENSE` and paste in the entire contents of
   the `.ulf` file.
4. Also create `UNITY_EMAIL` and `UNITY_PASSWORD` for the same Unity account.
   Personal CI requires all three. 输入在 GitHub Secrets，不发到聊天或写入代码。
5. Do not push/rerun to verify credentials. After account recovery and explicit
   owner approval, enable the workflow if necessary and dispatch it manually from
   the intended branch, selecting the authorization checkbox.

Never commit a Unity license file or its contents to this repository.

## Results

One job publishes both suites' results for 14 days. Both NUnit XML reports must
exist and pass. All branches share a concurrency group to prevent simultaneous
account login. A failed activation ends that run without a second activation call.
There is no automatic trigger for PR changes or main pushes.

The complete job has a **150-minute upper limit**, shared by both suites and
Library imports, and stops as soon as they finish. A cold P13 run exhausted the
old 90-minute combined budget: PlayMode completed in 27 minutes, while EditMode
was still rebuilding legacy assets. This budget change does not filter tests or
weaken the requirement for both final XML reports. The run summary records the
tested commit and each suite's duration. Missing XML or timeout still fails CI.

完整job的150分钟是总上限，不是固定等待时间；冷启动会重建Library和旧资源。
两套最终XML、零失败及原有SKIP的报告要求保持不变。

See [Unity CI Guide](../Docs/Unity_CI_Guide.md) for the complete configuration,
report checks, and the separate owner step to require the `Unity CI` status check.
