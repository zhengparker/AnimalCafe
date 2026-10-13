"""Remove GameCI activation retries from the pinned runner; never starts Unity.
只编辑固定版本的激活脚本，不读取 Secrets 或启动 Unity。
"""
import hashlib
import sys
from pathlib import Path

EXPECTED_SHA256 = "35c589f833d2ef60aaf7a9a868f35cbf5e66c06c5e68738ad06d7895948a1743"

runner = Path(sys.argv[1])
activation = runner / "dist/platforms/ubuntu/activate.sh"
source_bytes = activation.read_bytes()
if hashlib.sha256(source_bytes).hexdigest() != EXPECTED_SHA256:
    raise SystemExit("Unexpected GameCI activation script; stop before using Unity credentials.")
source = source_bytes.decode("utf-8")
start = source.index("  # Loop the unity-editor call")
end = source.index('elif [[ -n "$UNITY_LICENSING_SERVER" ]]', start)
# Preserve upstream arguments but call activation once, without its retry loop or sleeps.
# 保留原激活参数；失败直接退出，既不 sleep 也不重复启动激活。
command_start = source.index("    unity-editor ", start)
command_end = source.index("    # Store the exit code", command_start)
command = source[command_start:command_end].replace("    ", "  ", 1)
replacement = (
    "  # One Editor activation invocation; Unity may reconnect internally.\n"
    + command
    + "  UNITY_EXIT_CODE=$?\n"
    + '  if [[ $UNITY_EXIT_CODE -ne 0 ]]; then\n'
    + '    echo "::error::Unity activation failed. No automatic activation retry; verify credentials manually."\n'
    + '    exit "$UNITY_EXIT_CODE"\n'
    + "  fi\n\n"
)
activation.write_bytes((source[:start] + replacement + source[end:]).encode("utf-8"))
print("Pinned GameCI activation retries removed; Unity was not started.")
