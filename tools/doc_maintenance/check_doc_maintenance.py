#!/usr/bin/env python3

import subprocess
import sys
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[2]
DOC_ROOT = REPO_ROOT / "doc"
ITER_DIR = DOC_ROOT / "迭代记录"
ITER_INDEX = ITER_DIR / "迭代记录索引.md"
LONG_INDEX = DOC_ROOT / "长期主文档" / "主文档索引.md"

ALWAYS_REQUIRE_PREFIXES = (
    "tools/",
    "ProjectSettings/",
    "Packages/",
    ".githooks/",
)

REQUIRE_ASSET_EXTENSIONS = {
    ".cs",
    ".asmdef",
    ".prefab",
    ".unity",
    ".controller",
    ".overridecontroller",
    ".playable",
}

PURE_RESOURCE_EXTENSIONS = {
    ".png",
    ".jpg",
    ".jpeg",
    ".tga",
    ".psd",
    ".tif",
    ".tiff",
    ".exr",
    ".bmp",
    ".gif",
    ".svg",
    ".wav",
    ".mp3",
    ".ogg",
    ".aiff",
    ".fbx",
    ".obj",
    ".blend",
    ".mat",
    ".ttf",
    ".otf",
    ".mp4",
    ".mov",
}


def run_git(*args: str, text: bool = True):
    result = subprocess.run(
        ["git", "-C", str(REPO_ROOT), *args],
        check=True,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        text=text,
    )
    return result.stdout


def staged_files() -> list[str]:
    # 使用 -z 读取 Git 原始路径，避免中文文件名被 quotePath 转义后无法正确识别。
    output = run_git("diff", "--cached", "--name-only", "--diff-filter=ACMR", "-z", text=False)
    return [
        chunk.decode("utf-8")
        for chunk in output.split(b"\0")
        if chunk
    ]


def asset_extension(path: str) -> str:
    return Path(path).suffix.lower()


def is_meta_file(path: str) -> bool:
    return asset_extension(path) == ".meta"


def meta_target_extension(path: str) -> str:
    if not is_meta_file(path):
        return ""
    target = Path(path[:-5])
    return target.suffix.lower()


def should_require_iteration_log(path: str) -> bool:
    # 这里把“会影响项目实现或长期规则的改动”都视为需要收尾记录。
    # 纯文档改动和纯资源改动不触发这条规则，避免策划/美术被开发迭代日志误拦。
    if path.startswith(ALWAYS_REQUIRE_PREFIXES):
        return True

    if not path.startswith("Assets/"):
        return False

    ext = asset_extension(path)
    if ext in REQUIRE_ASSET_EXTENSIONS:
        return True

    if ext in PURE_RESOURCE_EXTENSIONS:
        return False

    if is_meta_file(path):
        target_ext = meta_target_extension(path)
        if target_ext in REQUIRE_ASSET_EXTENSIONS:
            return True
        if target_ext in PURE_RESOURCE_EXTENSIONS:
            return False

    return False


def is_iteration_log(path: str) -> bool:
    return path.startswith("doc/迭代记录/") and not path.endswith("迭代记录索引.md")


def sync_indexes() -> None:
    subprocess.run(
        ["python3", str(REPO_ROOT / "tools/doc_maintenance/update_project_docs.py"), "--doc-root", str(DOC_ROOT), "sync"],
        check=True,
        cwd=str(REPO_ROOT),
    )


def stage_if_exists(path: Path) -> None:
    if path.exists():
        subprocess.run(
            ["git", "-C", str(REPO_ROOT), "add", str(path.relative_to(REPO_ROOT))],
            check=True,
        )


def main() -> int:
    files = staged_files()
    if not files:
        return 0

    if not any(should_require_iteration_log(path) for path in files):
        return 0

    # 索引属于派生文件，提交前自动同步一次，避免明明写了文档却漏了索引刷新。
    sync_indexes()
    stage_if_exists(ITER_INDEX)
    stage_if_exists(LONG_INDEX)

    files = staged_files()
    if any(is_iteration_log(path) for path in files):
        return 0

    message = (
        "[doc-check] 检测到本次提交包含实现或流程相关改动，但没有暂存任何迭代记录文件。\n"
        "[doc-check] 请先执行文档收尾，例如：\n"
        "  tools/doc_maintenance/finalize_task.sh --summary \"本轮完成了什么\" --done \"已完成项\" --next \"下一步\"\n"
        "[doc-check] 完成后重新 git add 并提交。\n"
    )
    sys.stderr.write(message)
    return 1


if __name__ == "__main__":
    raise SystemExit(main())
