# Unity CI setup

The `Unity tests` workflow is **manual only**, with an explicit login authorization
checkbox. It activates once, then runs EditMode and PlayMode sequentially.
GameCI's activation retry loop is removed from its pinned runner before use.
Unity Editor may still reconnect internally during that one activation invocation.

**Currently disabled in GitHub. Do not enable or dispatch until the owner explicitly
allows Unity login/testing again.** Updating Secrets does not authorize a run.

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
   owner approval, enable the workflow and dispatch it manually from the intended
   branch, selecting the authorization checkbox. This has not been done yet.

Never commit a Unity license file or its contents to this repository.

## Results

One job publishes both suites' results for 14 days. Both NUnit XML reports must
exist and pass. All branches share a concurrency group to prevent simultaneous
account login. A failed activation ends that run without a second activation call.
There is no automatic trigger for PR changes or main pushes.

See [Unity CI Guide](../Docs/Unity_CI_Guide.md) for the complete configuration,
report checks, and the separate owner step to require the `Unity CI` status check.
