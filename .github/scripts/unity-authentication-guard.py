"""Stop subsequent automatic logins after an incomplete Unity startup.
使用只读 Actions metadata；认证/启动失败后，自动 PR 必须等待人工恢复。
"""
import json
from datetime import datetime
import os
from pathlib import Path
import sys
import urllib.request
import xml.etree.ElementTree as ET

OK = "unity-authentication-ok"
HALTED = "unity-authentication-halted"
DIAGNOSTIC = "unity-authentication-diagnostic"


class AuthenticationHalted(RuntimeError):
    pass


def check_previous_runs(get_json, current_run_id):
    # No search filters: filtered searches can truncate history at 1,000 runs.
    # 获取完整有界 metadata，再按实际 attempt 时间排序，不能按 run 创建顺序放行。
    history = {}
    for page in range(1, 21):
        data = get_json(f"actions/workflows/unity-tests.yml/runs?page={page}&per_page=100")
        runs = data["workflow_runs"]
        history.update({run["id"]: run for run in runs})
        if len(runs) < 100:
            if len(history) < data.get("total_count", len(history)):
                raise AuthenticationHalted("Incomplete workflow history; automatic login blocked.")
            break
    else:
        raise AuthenticationHalted("Workflow history exceeds bounded check; review manually.")

    attempts = []
    for run in history.values():
        latest = int(run.get("run_attempt", 1))
        if latest > 100:
            raise AuthenticationHalted("Attempt history exceeds bounded check; review manually.")
        for attempt in range(latest, 0, -1):
            if run["id"] == current_run_id and attempt == latest:
                continue
            metadata = run if attempt == latest else get_json(f'actions/runs/{run["id"]}/attempts/{attempt}')
            if metadata.get("status", "completed") != "completed":
                continue
            started = datetime.fromisoformat(metadata["run_started_at"].replace("Z", "+00:00"))
            attempts.append((started, run["id"], attempt, metadata))
    artifacts_by_run = {}
    for _, run_id, attempt, metadata in sorted(attempts, reverse=True, key=lambda item: item[:3]):
        if run_id not in artifacts_by_run:
            data = get_json(f"actions/runs/{run_id}/artifacts?per_page=100")
            if data.get("total_count", len(data["artifacts"])) > len(data["artifacts"]):
                raise AuthenticationHalted("Incomplete checkpoint metadata; review manually.")
            artifacts_by_run[run_id] = {item["name"] for item in data["artifacts"]}
        names = artifacts_by_run[run_id]
        suffix = f"-attempt-{attempt}"
        if HALTED + suffix in names:
            raise AuthenticationHalted(
                f"Unity startup halted in run {run_id} attempt {attempt}. Verify account/license, "
                "then explicitly authorize one manual full run; automatic login is blocked."
            )
        if OK + suffix in names:
            return
        if DIAGNOSTIC + suffix in names:
            continue
        # A missing upload or cancelled rerun must not inherit an old OK marker.
        jobs = get_json(f"actions/runs/{run_id}/attempts/{attempt}/jobs?per_page=100")["jobs"]
        actual = any(step["name"] == "Run complete Unity test suites after one activation"
                     and step.get("conclusion") in ("success", "failure", "cancelled")
                     for job in jobs for step in job.get("steps", []))
        if actual:
            if metadata["conclusion"] == "success":
                return  # Previously verified full workflow before checkpoint rollout.
            raise AuthenticationHalted(
                f"Unity attempt in run {run_id} attempt {attempt} has no checkpoint; review manually."
            )


def record_attempt(results_path, state_path, scope="full"):
    marker = OK
    try:
        for suite in ("editmode", "playmode"):
            paths = list(Path(results_path).rglob(f"{suite}-results.xml"))
            if len(paths) != 1:
                raise ValueError("Incomplete reports")
            root = ET.parse(paths[0]).getroot()
            if root.tag != "test-run" or int(root.get("total", "0")) <= 0:
                raise ValueError("Unity did not run both suites")
    except (OSError, ValueError, ET.ParseError):
        # Missing reports may also be compilation/startup failure: halt conservatively.
        # 缺少报告不能直接断定密码错误，但不能继续自动登录。
        marker = HALTED
    if marker == OK and scope != "full":
        marker = DIAGNOSTIC
    state = Path(state_path)
    state.mkdir(parents=True, exist_ok=True)
    (state / "state.json").write_text(json.dumps({"marker": marker}), encoding="utf-8")
    return marker


def github_json(path):
    repository = os.environ["GITHUB_REPOSITORY"]
    request = urllib.request.Request(
        f"https://api.github.com/repos/{repository}/{path}",
        headers={"Authorization": "Bearer " + os.environ["GH_TOKEN"],
                 "Accept": "application/vnd.github+json",
                 "X-GitHub-Api-Version": "2022-11-28"},
    )
    # Never log request headers or response bodies; errors fail closed before Secrets.
    with urllib.request.urlopen(request, timeout=30) as response:
        return json.load(response)


def main():
    if sys.argv[1] == "check":
        try:
            check_previous_runs(github_json, int(os.environ["GITHUB_RUN_ID"]))
        except Exception as error:
            message = str(error) if isinstance(error, AuthenticationHalted) else "Cannot verify prior Unity authentication; automatic login blocked."
            print("::error::" + message)
            return 1
        print("Prior Unity authentication permits one automatic attempt.")
    elif sys.argv[1] == "record":
        marker = record_attempt(sys.argv[2], sys.argv[3], os.environ.get("TEST_SCOPE", "full"))
        with open(os.environ["GITHUB_OUTPUT"], "a", encoding="utf-8") as output:
            output.write(f"marker={marker}\n")
        print("Recorded Unity authentication state: " + marker)
    else:
        raise ValueError("Expected check or record")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
