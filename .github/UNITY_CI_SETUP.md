# Unity CI setup

The `Unity tests` workflow runs EditMode and PlayMode tests for pull requests
targeting `main` and for pushes to `main`. It uses the Unity Editor version
recorded in `ProjectSettings/ProjectVersion.txt`.

## One-time activation setup

1. Obtain a Unity license file (`.ulf`) for the account and license tier used
   to run the project. Follow the [GameCI activation guide](https://game.ci/docs/github/activation/).
2. In GitHub, open **Settings** > **Secrets and variables** > **Actions** >
   **New repository secret**.
3. Create a secret named `UNITY_LICENSE` and paste in the entire contents of
   the `.ulf` file.
4. Also create `UNITY_EMAIL` and `UNITY_PASSWORD` for the same Unity account.
   Personal CI requires all three. 输入在 GitHub Secrets，不发到聊天或写入代码。
5. Push a commit or rerun a workflow to verify that both `editmode` and
   `playmode` jobs run.

Never commit a Unity license file or its contents to this repository.

## Results

Each job publishes its Unity test results as a workflow artifact for 14 days.
The workflow cancels superseded runs for the same branch or pull request.

See [Unity CI Guide](../Docs/Unity_CI_Guide.md) for the complete configuration,
report checks, and the separate owner step to require the `Unity CI` status check.
