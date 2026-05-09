import importlib.util
import json
import os
import subprocess
import sys
import tempfile
import textwrap
import unittest
from datetime import datetime
from pathlib import Path
from shutil import copy2
from unittest import mock


TOOLS_ROOT = Path(__file__).resolve().parents[2]
SCRIPT_PATH = TOOLS_ROOT / "doc_maintenance" / "update_project_docs.py"
FINALIZE_SCRIPT = TOOLS_ROOT / "doc_maintenance" / "finalize_task.sh"
SYNC_SKILLS_SCRIPT = TOOLS_ROOT / "repo_maintenance" / "sync_codex_skills.sh"
PRE_COMMIT_HOOK = TOOLS_ROOT.parents[0] / ".githooks" / "pre-commit"
COMMIT_MSG_HOOK = TOOLS_ROOT.parents[0] / ".githooks" / "commit-msg"
POST_COMMIT_HOOK = TOOLS_ROOT.parents[0] / ".githooks" / "post-commit"

spec = importlib.util.spec_from_file_location("update_project_docs", SCRIPT_PATH)
update_project_docs = importlib.util.module_from_spec(spec)
assert spec.loader is not None
sys.modules[spec.name] = update_project_docs
spec.loader.exec_module(update_project_docs)


def write_file(root: Path, relative_path: str, content: str) -> Path:
    path = root / relative_path
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(textwrap.dedent(content).lstrip("\n"), encoding="utf-8")
    return path


def create_doc_root(root: Path) -> Path:
    doc_root = root / "doc"
    write_file(
        root,
        "doc/长期主文档/协作与执行/Agent 启动与验收规范.md",
        """
        # Agent 启动与验收规范

        ## 迭代文档默认分工状态

        - Agent 1：Shared 与骨架继续保持冻结，本轮未改
        - Agent 2：地图与棋盘纯逻辑作为被验证对象，本轮未改生产逻辑
        - Agent 3：任务与长期进度纯逻辑作为被验证对象，本轮未改生产逻辑
        - Agent 4：UI 与验证，暂未启动
        - Agent 5：测试与质量保障，默认按需启动，本轮未启动
        - Agent 6：挑刺与问题审查，默认在阶段里程碑完成后按需启动
        """,
    )
    write_file(
        root,
        "doc/长期主文档/项目总览.md",
        """
        # 项目总览

        ## 当前阶段

        - 当前阶段：暂无
        - 当前主线：暂无
        - 当前建议起点：暂无明确建议
        """,
    )
    return doc_root


def init_git_repo(root: Path) -> None:
    subprocess.run(["git", "init"], cwd=root, check=True, capture_output=True, text=True)
    subprocess.run(["git", "config", "user.name", "Codex Test"], cwd=root, check=True, capture_output=True, text=True)
    subprocess.run(["git", "config", "user.email", "codex@example.com"], cwd=root, check=True, capture_output=True, text=True)


def install_doc_maintenance_tools(root: Path, include_finalize: bool = False) -> Path:
    doc_tools_dir = root / "tools" / "doc_maintenance"
    doc_tools_dir.mkdir(parents=True, exist_ok=True)
    copy2(SCRIPT_PATH, doc_tools_dir / "update_project_docs.py")
    if include_finalize:
        copy2(FINALIZE_SCRIPT, doc_tools_dir / "finalize_task.sh")
    return doc_tools_dir


def install_repo_maintenance_tools(root: Path, include_sync: bool = False) -> Path:
    repo_tools_dir = root / "tools" / "repo_maintenance"
    repo_tools_dir.mkdir(parents=True, exist_ok=True)
    if include_sync:
        copy2(SYNC_SKILLS_SCRIPT, repo_tools_dir / "sync_codex_skills.sh")
    return repo_tools_dir


def install_git_hooks(
    root: Path,
    include_pre_commit: bool = True,
    include_commit_msg: bool = False,
    include_post_commit: bool = False,
) -> Path:
    hooks_dir = root / ".githooks"
    hooks_dir.mkdir(parents=True, exist_ok=True)
    if include_pre_commit:
        copy2(PRE_COMMIT_HOOK, hooks_dir / "pre-commit")
        os.chmod(hooks_dir / "pre-commit", 0o755)
    if include_commit_msg:
        copy2(COMMIT_MSG_HOOK, hooks_dir / "commit-msg")
        os.chmod(hooks_dir / "commit-msg", 0o755)
    if include_post_commit:
        copy2(POST_COMMIT_HOOK, hooks_dir / "post-commit")
        os.chmod(hooks_dir / "post-commit", 0o755)
    subprocess.run(["git", "config", "core.hooksPath", ".githooks"], cwd=root, check=True, capture_output=True, text=True)
    return hooks_dir


class UpdateProjectDocsTests(unittest.TestCase):
    def test_finalize_task_help_mentions_sync_for_overview_and_index(self):
        completed = subprocess.run(
            ["bash", str(FINALIZE_SCRIPT), "--help"],
            capture_output=True,
            text=True,
            check=False,
        )

        self.assertEqual(completed.returncode, 0, completed.stderr)
        self.assertIn("passed / passed-with-suggestions / failed / pending / deferred / not-required", completed.stdout)
        self.assertIn("项目总览.md", completed.stdout)
        self.assertIn("update_project_docs.py sync", completed.stdout)
        self.assertIn("文档维护", completed.stdout)
        self.assertIn("Git 提交建议", completed.stdout)
        self.assertIn("提交确认", completed.stdout)

    def test_new_iteration_uses_default_agent_statuses(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            doc_root = create_doc_root(root)

            path = update_project_docs.create_iteration(doc_root, "主题", "目标", "进行中")
            text = path.read_text(encoding="utf-8")

            self.assertIn("- Agent 1：Shared 与骨架继续保持冻结，本轮未改", text)
            self.assertIn("- Agent 5：测试与质量保障，默认按需启动，本轮未启动", text)
            self.assertIn("- Agent 6：挑刺与问题审查，默认在阶段里程碑完成后按需启动", text)
            self.assertNotIn("Agent 1：待补充", text)

    def test_append_iteration_overrides_only_target_agent(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            doc_root = create_doc_root(root)
            path = update_project_docs.create_iteration(doc_root, "主题", "目标", "进行中")

            update_project_docs.append_iteration(
                doc_root,
                str(path),
                False,
                "完成边界测试补强",
                [],
                [],
                [],
                ["Agent 6：已启动并完成本轮挑刺与问题审查"],
            )
            text = path.read_text(encoding="utf-8")

            self.assertIn("- Agent 6：已启动并完成本轮挑刺与问题审查", text)
            self.assertIn("- Agent 1：Shared 与骨架继续保持冻结，本轮未改", text)
            self.assertIn("- Agent 4：UI 与验证，暂未启动", text)

    def test_append_iteration_backfills_placeholder_agent_block(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            doc_root = create_doc_root(root)
            path = write_file(
                root,
                "doc/迭代记录/迭代记录_20260330_001.md",
                """
                # 迭代记录 2026-03-30 001

                ## 分工状态

                - Agent 1：待补充
                - Agent 2：待补充
                - Agent 3：待补充
                - Agent 4：待补充
                - Agent 5：待补充
                - Agent 6：待补充

                ## 工作日志

                ## 完成项

                - 暂无

                ## 风险与阻塞

                - 暂无

                ## 下一步

                - 待补充
                """,
            )

            update_project_docs.append_iteration(
                doc_root,
                str(path),
                False,
                "补录测试结果",
                ["新增脚本测试"],
                [],
                ["继续补充分工状态规则"],
                [],
            )
            text = path.read_text(encoding="utf-8")

            self.assertIn("- Agent 2：地图与棋盘纯逻辑作为被验证对象，本轮未改生产逻辑", text)
            self.assertIn("- Agent 6：挑刺与问题审查，默认在阶段里程碑完成后按需启动", text)
            self.assertIn("- 新增脚本测试", text)
            self.assertNotIn("## 完成项\n\n- 暂无", text)
            self.assertNotIn("Agent 3：待补充", text)

    def test_append_iteration_preserves_manual_agent_lines(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            doc_root = create_doc_root(root)
            path = write_file(
                root,
                "doc/迭代记录/迭代记录_20260331_001.md",
                """
                # 迭代记录 2026-03-31 001

                ## 分工状态

                - Agent 1：Shared 与骨架继续保持冻结，本轮未改
                - Agent 2：已启动并完成地图配置接线
                - Agent 3：待补充
                - Agent 4：待补充
                - Agent 5：待补充
                - Agent 6：待补充
                """,
            )

            update_project_docs.append_iteration(doc_root, str(path), False, "", [], [], [], [])
            text = path.read_text(encoding="utf-8")

            self.assertIn("- Agent 2：已启动并完成地图配置接线", text)
            self.assertIn("- Agent 3：任务与长期进度纯逻辑作为被验证对象，本轮未改生产逻辑", text)
            self.assertIn("- Agent 5：测试与质量保障，默认按需启动，本轮未启动", text)
            self.assertIn("- Agent 6：挑刺与问题审查，默认在阶段里程碑完成后按需启动", text)

    def test_summary_does_not_infer_agent_status(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            doc_root = create_doc_root(root)
            path = update_project_docs.create_iteration(doc_root, "主题", "目标", "进行中")

            update_project_docs.append_iteration(
                doc_root,
                str(path),
                False,
                "验证地图线并补 Agent 5 测试",
                [],
                [],
                [],
                [],
            )
            text = path.read_text(encoding="utf-8")

            self.assertIn("- Agent 5：测试与质量保障，默认按需启动，本轮未启动", text)
            self.assertIn("- Agent 6：挑刺与问题审查，默认在阶段里程碑完成后按需启动", text)
            self.assertNotIn("已启动并完成", text)

    def test_backfill_agent_status_updates_only_placeholders(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            doc_root = create_doc_root(root)
            old_path = write_file(
                root,
                "doc/迭代记录/迭代记录_20260329_001.md",
                """
                # 旧记录

                ## 分工状态

                - Agent 1：待补充
                - Agent 2：待补充
                - Agent 3：待补充
                - Agent 4：待补充
                - Agent 5：待补充
                - Agent 6：待补充
                """,
            )
            new_path = write_file(
                root,
                "doc/迭代记录/迭代记录_20260330_001.md",
                """
                # 新记录

                ## 分工状态

                - Agent 1：待补充
                - Agent 2：已启动并完成地图配置接线
                - Agent 3：待补充
                - Agent 4：待补充
                - Agent 5：待补充
                - Agent 6：待补充

                ## 完成项

                - 新增地图配置入口
                - 暂无

                ## 风险与阻塞

                - 暂无

                ## 下一步

                - 继续补齐配置校验
                - 待补充
                """,
            )

            updated = update_project_docs.backfill_agent_status(doc_root, "20260330")
            self.assertEqual([path.name for path in updated], [new_path.name])

            self.assertIn("Agent 1：待补充", old_path.read_text(encoding="utf-8"))
            text = new_path.read_text(encoding="utf-8")
            self.assertIn("- Agent 2：已启动并完成地图配置接线", text)
            self.assertIn("- Agent 3：任务与长期进度纯逻辑作为被验证对象，本轮未改生产逻辑", text)
            self.assertIn("- Agent 6：挑刺与问题审查，默认在阶段里程碑完成后按需启动", text)
            self.assertNotIn("## 完成项\n\n- 暂无", text)
            self.assertNotIn("## 下一步\n\n- 待补充", text)

    def test_extract_plan_progress_reads_status_and_note(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            create_doc_root(root)
            plan_path = write_file(
                root,
                "doc/长期主文档/方案与数据/Gatebreaker Arena v1 正式建筑内容表方案.md",
                """
                # Gatebreaker Arena v1 正式建筑内容表方案

                ## 完成情况

                - 当前状态：进行中
                - 进度说明：已完成表结构定义，待运行时和导表链完全对齐。
                """,
            )

            progress = update_project_docs.extract_plan_progress(plan_path)

            self.assertEqual(progress["status"], "进行中")
            self.assertEqual(progress["note"], "已完成表结构定义，待运行时和导表链完全对齐。")

    def test_write_long_index_shows_plan_status_label_for_scheme_docs(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            doc_root = create_doc_root(root)
            write_file(
                root,
                "doc/长期主文档/方案与数据/Gatebreaker Arena v1 长期成长表方案.md",
                """
                # Gatebreaker Arena v1 长期成长表方案

                ## 完成情况

                - 当前状态：已完成
                - 进度说明：配置规则和运行时接线已全部完成。
                """,
            )

            update_project_docs.write_long_index(doc_root)
            index_text = (doc_root / "长期主文档" / "主文档索引.md").read_text(encoding="utf-8")

            self.assertIn("[已完成] Gatebreaker Arena v1 长期成长表方案", index_text)

    def test_write_long_index_falls_back_to_unstarted_when_plan_progress_missing(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            doc_root = create_doc_root(root)
            write_file(
                root,
                "doc/长期主文档/方案与数据/Gatebreaker Arena_v1方案.md",
                """
                # Gatebreaker Arena v1 方案

                ## Summary

                - 测试旧方案文档缺少完成情况时的索引回退。
                """,
            )

            update_project_docs.write_long_index(doc_root)
            index_text = (doc_root / "长期主文档" / "主文档索引.md").read_text(encoding="utf-8")

            self.assertIn("[未开始] Gatebreaker Arena v1 方案", index_text)

    def test_write_plan_progress_normalizes_block_and_updates_index_label(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            doc_root = create_doc_root(root)
            plan_path = write_file(
                root,
                "doc/长期主文档/方案与数据/Gatebreaker Arena v1 排行榜系统方案.md",
                """
                # Gatebreaker Arena v1 排行榜系统方案

                ## Summary

                - 排行榜首期方案。
                """,
            )

            result = update_project_docs.write_plan_progress(
                doc_root,
                str(plan_path),
                "进行中",
                "已完成配置口径，待运行时接线。",
                "补齐首期进度状态",
                "2026-05-06",
            )
            update_project_docs.write_long_index(doc_root)

            text = plan_path.read_text(encoding="utf-8")
            index_text = (doc_root / "长期主文档" / "主文档索引.md").read_text(encoding="utf-8")

            self.assertTrue(result["changed"])
            self.assertIn("- 当前状态：进行中", text)
            self.assertIn("- 进度说明：已完成配置口径，待运行时接线。", text)
            self.assertIn("- 最近更新：2026-05-06，补齐首期进度状态", text)
            self.assertIn("[进行中] Gatebreaker Arena v1 排行榜系统方案", index_text)

    def test_validate_plan_progress_warns_for_scheme_docs_without_block_but_not_plain_notes(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            doc_root = create_doc_root(root)
            write_file(
                root,
                "doc/长期主文档/方案与数据/Gatebreaker Arena v1 排行榜系统方案.md",
                """
                # Gatebreaker Arena v1 排行榜系统方案

                ## Summary

                - 排行榜首期方案。
                """,
            )
            write_file(
                root,
                "doc/长期主文档/方案与数据/Gatebreaker Arena 字体使用与预览说明.md",
                """
                # Gatebreaker Arena 字体使用与预览说明

                ## Summary

                - 这里只是说明文档。
                """,
            )
            write_file(
                root,
                "doc/长期主文档/方案与数据/Gatebreaker Arena 八位提交编号方案.md",
                """
                # Gatebreaker Arena 八位提交编号方案

                ## Summary

                - 这里只是流程规则文档。
                """,
            )
            write_file(
                root,
                "doc/长期主文档/方案与数据/子目录/Gatebreaker Arena 子目录落地方案.md",
                """
                # Gatebreaker Arena 子目录落地方案

                ## Summary

                - 子目录里的真实方案也需要 warning。
                """,
            )

            warnings = update_project_docs.validate_plan_progress(doc_root)

            joined = "\n".join(warnings)
            self.assertEqual(len(warnings), 2)
            self.assertIn("Gatebreaker Arena v1 排行榜系统方案.md", joined)
            self.assertIn("Gatebreaker Arena 子目录落地方案.md", joined)
            self.assertNotIn("Gatebreaker Arena 字体使用与预览说明.md", joined)
            self.assertNotIn("Gatebreaker Arena 八位提交编号方案.md", joined)

    def test_validate_last_finalize_fails_when_bound_plan_content_changes(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            doc_root = create_doc_root(root)
            init_git_repo(root)
            plan_path = write_file(
                root,
                "doc/长期主文档/方案与数据/Gatebreaker Arena v1 排行榜系统方案.md",
                """
                # Gatebreaker Arena v1 排行榜系统方案

                ## 完成情况

                - 当前状态：未开始
                - 进度说明：尚未开始。
                - 最近更新：2026-05-06，创建方案进度块
                """,
            )
            subprocess.run(["git", "add", "doc"], cwd=root, check=True, capture_output=True, text=True)
            subprocess.run(["git", "commit", "-m", "seed"], cwd=root, check=True, capture_output=True, text=True)

            update_project_docs.write_plan_progress(
                doc_root,
                str(plan_path),
                "进行中",
                "已进入第一阶段。",
                "更新为进行中",
                "2026-05-06",
            )
            worktree_status_at_finalize = update_project_docs.current_worktree_status(doc_root)
            update_project_docs.write_last_finalize_report(
                doc_root,
                {
                    "summary": "绑定方案收尾",
                    "agent6_review": "not-required",
                    "doc_log_skipped": False,
                    "plan_doc": plan_path.resolve().as_posix(),
                    "plan_doc_hash": update_project_docs.file_content_hash(plan_path),
                    "head_commit": update_project_docs.current_head_commit(doc_root),
                    "worktree_status": worktree_status_at_finalize,
                    "report_text": "\n".join(
                        [
                            "文档维护：已执行",
                            "Git 提交建议：暂不建议提交。原因是：测试",
                            update_project_docs.UNSUITABLE_COMMIT_CONFIRMATION,
                            "会话建议：继续当前会话",
                        ]
                    ),
                    "created_at": "2026-05-06T10:00:00",
                },
            )

            update_project_docs.write_plan_progress(
                doc_root,
                str(plan_path),
                "已完成",
                "第一阶段已闭环。",
                "更新为已完成",
                "2026-05-06",
            )
            self.assertEqual(worktree_status_at_finalize, update_project_docs.current_worktree_status(doc_root))

            result = update_project_docs.validate_last_finalize_report(doc_root)

            self.assertFalse(result["valid"])
            self.assertIn("绑定方案文档内容已变化", "\n".join(result["reasons"]))

    def test_suggest_handoff_warns_when_plan_landing_lacks_plan_doc(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            doc_root = create_doc_root(root)

            report = update_project_docs.suggest_handoff(
                doc_root,
                "按排行榜方案落地第一阶段",
                [],
                [],
                [],
                "not-required",
                "continue",
                "continue",
                "",
                "",
                [],
                True,
                False,
                0,
            )
            text = update_project_docs.format_handoff_report(report)

            self.assertIn("方案进度：提醒", text)
            self.assertIn("--plan-doc / --plan-status / --plan-note", text)

    def test_append_iteration_cli_warns_that_wrapup_is_incomplete(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            doc_tools_dir = install_doc_maintenance_tools(root)
            create_doc_root(root)
            write_file(
                root,
                "doc/迭代记录/迭代记录_20260405_001.md",
                """
                # 迭代记录 2026-04-05 001

                ## 分工状态

                - Agent 1：待补充
                - Agent 2：待补充
                - Agent 3：待补充
                - Agent 4：待补充
                - Agent 5：待补充
                - Agent 6：待补充

                ## 工作日志

                ## 完成项

                - 暂无

                ## 风险与阻塞

                - 暂无

                ## 下一步

                - 待补充
                """,
            )

            completed = subprocess.run(
                [
                    "python3",
                    str(doc_tools_dir / "update_project_docs.py"),
                    "--doc-root",
                    str(root / "doc"),
                    "append-iteration",
                    "--latest",
                    "--summary",
                    "补一条文档记录",
                ],
                cwd=root,
                capture_output=True,
                text=True,
                check=False,
            )

            self.assertEqual(completed.returncode, 0, completed.stderr)
            self.assertIn("updated iteration log", completed.stdout)
            self.assertIn("这仍属于半收尾", completed.stdout)
            self.assertIn("suggest-handoff", completed.stdout)

    def test_append_iteration_clears_cached_last_finalize_report(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            doc_tools_dir = install_doc_maintenance_tools(root)
            doc_root = create_doc_root(root)
            init_git_repo(root)
            write_file(root, "README.md", "seed\n")
            subprocess.run(["git", "add", "README.md"], cwd=root, check=True, capture_output=True, text=True)
            subprocess.run(["git", "commit", "-m", "seed"], cwd=root, check=True, capture_output=True, text=True)

            update_project_docs.write_last_finalize_report(
                doc_root,
                {
                    "summary": "已有完整收尾",
                    "agent6_review": "passed",
                    "doc_log_skipped": False,
                    "head_commit": "abc123",
                    "worktree_status": "",
                    "report_text": "文档维护：已执行",
                    "created_at": "2026-04-06T10:00:00",
                },
            )

            write_file(
                root,
                "doc/迭代记录/迭代记录_20260405_001.md",
                """
                # 迭代记录 2026-04-05 001

                ## 分工状态

                - Agent 1：待补充
                - Agent 2：待补充
                - Agent 3：待补充
                - Agent 4：待补充
                - Agent 5：待补充
                - Agent 6：待补充

                ## 工作日志

                ## 完成项

                - 暂无

                ## 风险与阻塞

                - 暂无

                ## 下一步

                - 待补充
                """,
            )

            completed = subprocess.run(
                [
                    "python3",
                    str(doc_tools_dir / "update_project_docs.py"),
                    "--doc-root",
                    str(root / "doc"),
                    "append-iteration",
                    "--latest",
                    "--summary",
                    "补一条文档记录",
                ],
                cwd=root,
                capture_output=True,
                text=True,
                check=False,
            )

            self.assertEqual(completed.returncode, 0, completed.stderr)
            self.assertIsNone(update_project_docs.read_last_finalize_report(doc_root))

    def test_append_iteration_skips_duplicate_round_notes(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            doc_tools_dir = install_doc_maintenance_tools(root)
            create_doc_root(root)
            iteration_path = write_file(
                root,
                "doc/迭代记录/迭代记录_20260405_001.md",
                """
                # 迭代记录 2026-04-05 001

                ## 分工状态

                - Agent 1：待补充
                - Agent 2：待补充
                - Agent 3：待补充
                - Agent 4：待补充
                - Agent 5：待补充
                - Agent 6：待补充

                ## 工作日志

                ## 完成项

                - 暂无

                ## 风险与阻塞

                - 暂无

                ## 下一步

                - 待补充
                """,
            )

            command = [
                "python3",
                str(doc_tools_dir / "update_project_docs.py"),
                "--doc-root",
                str(root / "doc"),
                "append-iteration",
                "--latest",
                "--summary",
                "修正 finalize 参数",
                "--done",
                "完成脚本幂等保护",
                "--risk",
                "无新增风险",
                "--next",
                "继续验证收尾流程",
            ]
            for _ in range(2):
                completed = subprocess.run(
                    command,
                    cwd=root,
                    capture_output=True,
                    text=True,
                    check=False,
                )
                self.assertEqual(completed.returncode, 0, completed.stderr)

            text = iteration_path.read_text(encoding="utf-8")
            self.assertEqual(text.count("- 修正 finalize 参数"), 1)
            self.assertEqual(text.count("- 完成脚本幂等保护"), 1)
            self.assertEqual(text.count("- 无新增风险"), 1)
            self.assertEqual(text.count("- 继续验证收尾流程"), 1)

    def test_finalize_task_forwards_agent_status(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            repo_root = Path(temp_dir)
            doc_tools_dir = install_doc_maintenance_tools(repo_root, include_finalize=True)
            doc_root = create_doc_root(repo_root)

            iteration_path = write_file(
                repo_root,
                "doc/迭代记录/迭代记录_20260402_001.md",
                """
                # 迭代记录 2026-04-02 001

                ## 分工状态

                - Agent 1：待补充
                - Agent 2：待补充
                - Agent 3：待补充
                - Agent 4：待补充
                - Agent 5：待补充
                - Agent 6：待补充

                ## 工作日志

                ## 完成项

                - 暂无

                ## 风险与阻塞

                - 暂无

                ## 下一步

                - 待补充
                """,
            )

            completed = subprocess.run(
                [
                    "bash",
                    str(doc_tools_dir / "finalize_task.sh"),
                    "--file",
                    str(iteration_path),
                    "--summary",
                    "完成边界检查脚本规则收敛",
                    "--done",
                    "分工状态改为长期规则源驱动",
                    "--agent-status",
                    "Agent 5：已启动并完成核心脚本验证",
                    "--skip-temp-cleanup",
                ],
                cwd=repo_root,
                env={**os.environ, "CODEX_HOME": str(repo_root / ".codex")},
                capture_output=True,
                text=True,
                check=False,
            )

            self.assertEqual(completed.returncode, 0, completed.stderr)
            text = iteration_path.read_text(encoding="utf-8")
            self.assertIn("- Agent 5：已启动并完成核心脚本验证", text)
            self.assertIn("- Agent 6：挑刺与问题审查，默认在阶段里程碑完成后按需启动", text)
            self.assertIn("- 分工状态改为长期规则源驱动", text)

    def test_finalize_task_records_last_finalize_report(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            repo_root = Path(temp_dir)
            doc_tools_dir = install_doc_maintenance_tools(repo_root, include_finalize=True)
            doc_root = create_doc_root(repo_root)
            init_git_repo(repo_root)
            write_file(repo_root, "README.md", "seed\n")
            subprocess.run(["git", "add", "README.md"], cwd=repo_root, check=True, capture_output=True, text=True)
            subprocess.run(["git", "commit", "-m", "seed"], cwd=repo_root, check=True, capture_output=True, text=True)

            iteration_path = write_file(
                repo_root,
                "doc/迭代记录/迭代记录_20260402_001.md",
                """
                # 迭代记录 2026-04-02 001

                ## 分工状态

                - Agent 1：待补充
                - Agent 2：待补充
                - Agent 3：待补充
                - Agent 4：待补充
                - Agent 5：待补充
                - Agent 6：待补充

                ## 工作日志

                ## 完成项

                - 暂无

                ## 风险与阻塞

                - 暂无

                ## 下一步

                - 待补充
                """,
            )

            completed = subprocess.run(
                [
                    "bash",
                    str(doc_tools_dir / "finalize_task.sh"),
                    "--file",
                    str(iteration_path),
                    "--summary",
                    "修复新会话收尾断裂问题",
                    "--done",
                    "补齐 completion finalize 阶段",
                    "--agent6-review",
                    "passed",
                    "--skip-temp-cleanup",
                ],
                cwd=repo_root,
                env={**os.environ, "CODEX_HOME": str(repo_root / ".codex")},
                capture_output=True,
                text=True,
                check=False,
            )

            self.assertEqual(completed.returncode, 0, completed.stderr)
            payload = update_project_docs.read_last_finalize_report(doc_root)
            self.assertIsNotNone(payload)
            assert payload is not None
            self.assertEqual(payload["summary"], "修复新会话收尾断裂问题")
            self.assertEqual(payload["agent6_review"], "passed")
            self.assertIn("文档维护：已执行", payload["report_text"])
            self.assertTrue((repo_root / ".git" / "codex" / "last_finalize_report.json").exists())

    def test_finalize_task_accepts_repo_relative_iteration_file(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            repo_root = Path(temp_dir)
            doc_tools_dir = install_doc_maintenance_tools(repo_root, include_finalize=True)
            create_doc_root(repo_root)
            init_git_repo(repo_root)
            write_file(repo_root, "README.md", "seed\n")
            subprocess.run(["git", "add", "README.md"], cwd=repo_root, check=True, capture_output=True, text=True)
            subprocess.run(["git", "commit", "-m", "seed"], cwd=repo_root, check=True, capture_output=True, text=True)

            iteration_path = write_file(
                repo_root,
                "doc/迭代记录/迭代记录_20260402_001.md",
                """
                # 迭代记录 2026-04-02 001

                ## 分工状态

                - Agent 1：待补充
                - Agent 2：待补充
                - Agent 3：待补充
                - Agent 4：待补充
                - Agent 5：待补充
                - Agent 6：待补充

                ## 工作日志

                ## 完成项

                - 暂无

                ## 风险与阻塞

                - 暂无

                ## 下一步

                - 待补充
                """,
            )

            completed = subprocess.run(
                [
                    "bash",
                    str(doc_tools_dir / "finalize_task.sh"),
                    "--file",
                    "doc/迭代记录/迭代记录_20260402_001.md",
                    "--summary",
                    "校验相对路径收尾",
                    "--done",
                    "允许 finalize_task 直接接受仓库相对路径",
                    "--agent6-review",
                    "not-required",
                    "--skip-temp-cleanup",
                ],
                cwd=repo_root,
                env={**os.environ, "CODEX_HOME": str(repo_root / ".codex")},
                capture_output=True,
                text=True,
                check=False,
            )

            self.assertEqual(completed.returncode, 0, completed.stderr)
            self.assertIn("文档维护：已执行", completed.stdout)
            self.assertIn("校验相对路径收尾", iteration_path.read_text(encoding="utf-8"))

    def test_finalize_task_outputs_fixed_wrapup_sections_with_chinese_commit_message(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            repo_root = Path(temp_dir)
            doc_tools_dir = install_doc_maintenance_tools(repo_root, include_finalize=True)
            create_doc_root(repo_root)

            iteration_path = write_file(
                repo_root,
                "doc/迭代记录/迭代记录_20260402_001.md",
                """
                # 迭代记录 2026-04-02 001

                ## 分工状态

                - Agent 1：待补充
                - Agent 2：待补充
                - Agent 3：待补充
                - Agent 4：待补充
                - Agent 5：待补充
                - Agent 6：待补充

                ## 工作日志

                ## 完成项

                - 暂无

                ## 风险与阻塞

                - 暂无

                ## 下一步

                - 待补充
                """,
            )

            completed = subprocess.run(
                [
                    "bash",
                    str(doc_tools_dir / "finalize_task.sh"),
                    "--file",
                    str(iteration_path),
                    "--summary",
                    "统一新会话启动与收尾脚本输出",
                    "--done",
                    "为收尾流程补固定三段输出",
                    "--done",
                    "为提交建议补中文标题和内容",
                    "--agent6-review",
                    "passed",
                    "--skip-temp-cleanup",
                ],
                cwd=repo_root,
                env={**os.environ, "CODEX_HOME": str(repo_root / ".codex")},
                capture_output=True,
                text=True,
                check=False,
            )

            self.assertEqual(completed.returncode, 0, completed.stderr)
            self.assertIn("文档维护：已执行", completed.stdout)
            self.assertIn("Git 提交建议：适合提交", completed.stdout)
            self.assertIn("标题：[23000001] 流程：新会话启动与收尾脚本输出", completed.stdout)
            self.assertIn("内容：", completed.stdout)
            self.assertIn("- 为收尾流程补固定三段输出", completed.stdout)
            self.assertIn("- 为提交建议补中文标题和内容", completed.stdout)
            self.assertIn("提交确认：如需我继续执行，请回复 1（提交并推送） / 2（只提交） / 确认 / 提交 / 直接提交（只提交）。", completed.stdout)
            self.assertIn("会话建议：", completed.stdout)

    def test_show_last_finalize_cli_prints_cached_report(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            doc_tools_dir = install_doc_maintenance_tools(root)
            doc_root = create_doc_root(root)
            init_git_repo(root)
            write_file(root, "README.md", "seed\n")
            subprocess.run(["git", "add", "README.md"], cwd=root, check=True, capture_output=True, text=True)
            subprocess.run(["git", "commit", "-m", "seed"], cwd=root, check=True, capture_output=True, text=True)

            update_project_docs.write_last_finalize_report(
                doc_root,
                {
                    "summary": "最近一次完整收尾",
                    "agent6_review": "passed",
                    "doc_log_skipped": False,
                    "head_commit": "abc123",
                    "worktree_status": " M doc/长期主文档/项目总览.md",
                    "report_text": "文档维护：已执行\nGit 提交建议：适合提交",
                    "created_at": "2026-04-06T10:00:00",
                },
            )

            completed = subprocess.run(
                [
                    "python3",
                    str(doc_tools_dir / "update_project_docs.py"),
                    "--doc-root",
                    str(root / "doc"),
                    "show-last-finalize",
                ],
                cwd=root,
                capture_output=True,
                text=True,
                check=False,
            )

            self.assertEqual(completed.returncode, 0, completed.stderr)
            self.assertIn("最近一次完整收尾", completed.stdout)
            self.assertIn("Git 提交建议：适合提交", completed.stdout)

    def test_check_last_finalize_cli_passes_when_state_matches(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            doc_tools_dir = install_doc_maintenance_tools(root)
            doc_root = create_doc_root(root)
            init_git_repo(root)
            write_file(root, "README.md", "seed\n")
            subprocess.run(["git", "add", "README.md"], cwd=root, check=True, capture_output=True, text=True)
            subprocess.run(["git", "commit", "-m", "seed"], cwd=root, check=True, capture_output=True, text=True)

            update_project_docs.write_last_finalize_report(
                doc_root,
                {
                    "summary": "最近一次完整收尾",
                    "agent6_review": "passed",
                    "doc_log_skipped": False,
                    "head_commit": update_project_docs.current_head_commit(doc_root),
                    "worktree_status": update_project_docs.current_worktree_status(doc_root),
                    "report_text": (
                        "文档维护：已执行\n"
                        "Git 提交建议：适合提交\n"
                        f"{update_project_docs.SUITABLE_COMMIT_CONFIRMATION}\n"
                        "会话建议：继续当前会话"
                    ),
                    "created_at": "2026-04-06T10:00:00",
                },
            )

            completed = subprocess.run(
                [
                    "python3",
                    str(doc_tools_dir / "update_project_docs.py"),
                    "--doc-root",
                    str(root / "doc"),
                    "check-last-finalize",
                ],
                cwd=root,
                capture_output=True,
                text=True,
                check=False,
            )

            self.assertEqual(completed.returncode, 0, completed.stderr)
            self.assertIn("[ok] 最近一次完整收尾状态仍然有效。", completed.stdout)

    def test_check_last_finalize_cli_fails_when_worktree_changed(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            doc_tools_dir = install_doc_maintenance_tools(root)
            doc_root = create_doc_root(root)
            init_git_repo(root)
            write_file(root, "README.md", "seed\n")
            subprocess.run(["git", "add", "README.md"], cwd=root, check=True, capture_output=True, text=True)
            subprocess.run(["git", "commit", "-m", "seed"], cwd=root, check=True, capture_output=True, text=True)

            update_project_docs.write_last_finalize_report(
                doc_root,
                {
                    "summary": "最近一次完整收尾",
                    "agent6_review": "passed",
                    "doc_log_skipped": False,
                    "head_commit": update_project_docs.current_head_commit(doc_root),
                    "worktree_status": update_project_docs.current_worktree_status(doc_root),
                    "report_text": (
                        "文档维护：已执行\n"
                        "Git 提交建议：适合提交\n"
                        f"{update_project_docs.SUITABLE_COMMIT_CONFIRMATION}\n"
                        "会话建议：继续当前会话"
                    ),
                    "created_at": "2026-04-06T10:00:00",
                },
            )
            write_file(root, "notes.txt", "changed\n")

            completed = subprocess.run(
                [
                    "python3",
                    str(doc_tools_dir / "update_project_docs.py"),
                    "--doc-root",
                    str(root / "doc"),
                    "check-last-finalize",
                ],
                cwd=root,
                capture_output=True,
                text=True,
                check=False,
            )

            self.assertNotEqual(completed.returncode, 0)
            self.assertIn("当前工作区状态已变化", completed.stderr or completed.stdout)

    def test_check_last_finalize_cli_fails_when_confirmation_prompt_is_outdated(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            doc_tools_dir = install_doc_maintenance_tools(root)
            doc_root = create_doc_root(root)
            init_git_repo(root)
            write_file(root, "README.md", "seed\n")
            subprocess.run(["git", "add", "README.md"], cwd=root, check=True, capture_output=True, text=True)
            subprocess.run(["git", "commit", "-m", "seed"], cwd=root, check=True, capture_output=True, text=True)

            update_project_docs.write_last_finalize_report(
                doc_root,
                {
                    "summary": "最近一次完整收尾",
                    "agent6_review": "passed",
                    "doc_log_skipped": False,
                    "head_commit": update_project_docs.current_head_commit(doc_root),
                    "worktree_status": update_project_docs.current_worktree_status(doc_root),
                    "report_text": (
                        "文档维护：已执行\n"
                        "Git 提交建议：适合提交\n"
                        "提交确认：如需我直接提交到 git，请回复 1 / 确认 / 提交 / 直接提交。\n"
                        "会话建议：继续当前会话"
                    ),
                    "created_at": "2026-04-06T10:00:00",
                },
            )

            completed = subprocess.run(
                [
                    "python3",
                    str(doc_tools_dir / "update_project_docs.py"),
                    "--doc-root",
                    str(root / "doc"),
                    "check-last-finalize",
                ],
                cwd=root,
                capture_output=True,
                text=True,
                check=False,
            )

            self.assertNotEqual(completed.returncode, 0)
            self.assertIn("最新 `提交确认` 提示词", completed.stderr or completed.stdout)

    def test_sync_keeps_last_finalize_report_when_worktree_status_is_unchanged(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            doc_tools_dir = install_doc_maintenance_tools(root)
            doc_root = create_doc_root(root)
            init_git_repo(root)
            write_file(root, "README.md", "seed\n")
            subprocess.run(["git", "add", "README.md"], cwd=root, check=True, capture_output=True, text=True)
            subprocess.run(["git", "commit", "-m", "seed"], cwd=root, check=True, capture_output=True, text=True)

            update_project_docs.write_last_finalize_report(
                doc_root,
                {
                    "summary": "最近一次完整收尾",
                    "agent6_review": "passed",
                    "doc_log_skipped": False,
                    "head_commit": update_project_docs.current_head_commit(doc_root),
                    "worktree_status": update_project_docs.current_worktree_status(doc_root),
                    "report_text": (
                        "文档维护：已执行\n"
                        "Git 提交建议：适合提交\n"
                        f"{update_project_docs.SUITABLE_COMMIT_CONFIRMATION}\n"
                        "会话建议：继续当前会话"
                    ),
                    "created_at": "2026-04-06T10:00:00",
                },
            )

            completed = subprocess.run(
                [
                    "python3",
                    str(doc_tools_dir / "update_project_docs.py"),
                    "--doc-root",
                    str(root / "doc"),
                    "sync",
                ],
                cwd=root,
                capture_output=True,
                text=True,
                check=False,
            )

            self.assertEqual(completed.returncode, 0, completed.stderr)
            self.assertIsNotNone(update_project_docs.read_last_finalize_report(doc_root))

    def test_finalize_task_auto_syncs_skills_when_skill_source_changes(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            repo_root = Path(temp_dir)
            doc_tools_dir = install_doc_maintenance_tools(repo_root, include_finalize=True)
            install_repo_maintenance_tools(repo_root, include_sync=True)
            create_doc_root(repo_root)
            iteration_path = write_file(
                repo_root,
                "doc/迭代记录/迭代记录_20260402_001.md",
                """
                # 迭代记录 2026-04-02 001

                ## 分工状态

                - Agent 1：待补充
                - Agent 2：待补充
                - Agent 3：待补充
                - Agent 4：待补充
                - Agent 5：待补充
                - Agent 6：待补充

                ## 工作日志

                ## 完成项

                - 暂无

                ## 风险与阻塞

                - 暂无

                ## 下一步

                - 待补充
                """,
            )
            write_file(
                repo_root,
                "doc/长期主文档/协作与执行/skills/unity-hotupdate-boundary/SKILL.md",
                """
                ---
                name: unity-hotupdate-boundary
                description: fixture
                ---

                # Fixture Skill
                """,
            )

            subprocess.run(["git", "init"], cwd=repo_root, check=True, capture_output=True, text=True)

            completed = subprocess.run(
                [
                    "bash",
                    str(doc_tools_dir / "finalize_task.sh"),
                    "--file",
                    str(iteration_path),
                    "--summary",
                    "同步 skill 真源",
                    "--skip-temp-cleanup",
                ],
                cwd=repo_root,
                env={**os.environ, "CODEX_HOME": str(repo_root / ".codex")},
                capture_output=True,
                text=True,
                check=False,
            )

            self.assertEqual(completed.returncode, 0, completed.stderr)
            synced_path = repo_root / ".codex" / "skills" / "unity-hotupdate-boundary" / "SKILL.md"
            self.assertTrue(synced_path.exists())
            self.assertIn("自动同步到 ~/.codex/skills", completed.stdout)

    def test_finalize_task_skips_skill_sync_when_skill_source_is_unchanged(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            repo_root = Path(temp_dir)
            doc_tools_dir = install_doc_maintenance_tools(repo_root, include_finalize=True)
            install_repo_maintenance_tools(repo_root, include_sync=True)
            create_doc_root(repo_root)
            iteration_path = write_file(
                repo_root,
                "doc/迭代记录/迭代记录_20260402_001.md",
                """
                # 迭代记录 2026-04-02 001

                ## 分工状态

                - Agent 1：待补充
                - Agent 2：待补充
                - Agent 3：待补充
                - Agent 4：待补充
                - Agent 5：待补充
                - Agent 6：待补充

                ## 工作日志

                ## 完成项

                - 暂无

                ## 风险与阻塞

                - 暂无

                ## 下一步

                - 待补充
                """,
            )

            subprocess.run(["git", "init"], cwd=repo_root, check=True, capture_output=True, text=True)

            completed = subprocess.run(
                [
                    "bash",
                    str(doc_tools_dir / "finalize_task.sh"),
                    "--file",
                    str(iteration_path),
                    "--summary",
                    "普通任务收尾",
                    "--skip-temp-cleanup",
                ],
                cwd=repo_root,
                env={**os.environ, "CODEX_HOME": str(repo_root / ".codex")},
                capture_output=True,
                text=True,
                check=False,
            )

            self.assertEqual(completed.returncode, 0, completed.stderr)
            synced_root = repo_root / ".codex" / "skills"
            self.assertFalse(synced_root.exists())
            self.assertNotIn("自动同步到 ~/.codex/skills", completed.stdout)

    def test_suggest_agent_start_does_not_recommend_agent_6(self):
        suggested = update_project_docs.suggest_agent_start(["- 先交给 Agent 6 做挑刺审查"])
        self.assertNotEqual(suggested, "Agent 6：挑刺与问题审查")
        self.assertEqual(suggested, "暂无明确建议")

    def test_latest_iteration_context_prefers_product_mainline_over_process_next_steps(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            doc_root = create_doc_root(root)
            write_file(
                root,
                "doc/迭代记录/迭代记录_20260405_001.md",
                """
                # 迭代记录 2026-04-05 001

                ## 迭代标识

                - 建议起始 Agent：Agent 4：UI 与验证

                ## 当前状态

                - 当前阶段：进行中
                - 整体判断：当前需先补 Gatebreaker ArenaCoreValidationMenu 的真实任务推进验证，再回同一审查链复审。

                ## 下一步

                - 继续压缩协作文档
                - 统一更新入口文档
                - 继续 Agent 4：把当前代码生成 UI 收敛为正式 Gatebreaker Arena Panel/Presenter/Scene 资产。

                ## 给下一轮的人

                - 先回同一 Agent 6 审查链复审；通过后进入 Agent 4/UI 联调。
                """,
            )

            context = update_project_docs.latest_iteration_context(doc_root)

            self.assertEqual(context["stage"], "进行中")
            self.assertEqual(context["start_agent"], "Agent 4：UI 与验证")
            self.assertIn("Agent 4/UI 联调", context["mainline"])
            self.assertNotIn("压缩协作文档", context["mainline"])

    def test_write_iteration_index_uses_explicit_start_agent_instead_of_guessing_from_next_steps(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            doc_root = create_doc_root(root)
            write_file(
                root,
                "doc/迭代记录/迭代记录_20260405_001.md",
                """
                # 迭代记录 2026-04-05 001

                ## 迭代标识

                - 建议起始 Agent：Agent 4：UI 与验证

                ## 当前状态

                - 当前阶段：进行中
                - 整体判断：先回同一审查链复审，再进入 UI 联调。

                ## 下一步

                - 继续 Agent 3/组合层：接入 IPersistence。
                - 回同一 Agent 6 复审当前 smoke 修复。
                """,
            )

            update_project_docs.write_iteration_index(doc_root)
            index_text = (doc_root / "迭代记录" / "迭代记录索引.md").read_text(encoding="utf-8")

            self.assertIn("## 建议从哪个 Agent 开始继续", index_text)
            self.assertIn("- Agent 4：UI 与验证", index_text)
            self.assertNotIn("- Agent 3：任务与长期进度", index_text)

    def test_suggest_handoff_pending_review_uses_review_session_when_context_degraded(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            doc_root = create_doc_root(root)
            today = datetime.now().strftime("%Y%m%d")
            write_file(
                root,
                f"doc/迭代记录/迭代记录_{today}_001.md",
                f"""
                # 迭代记录 {datetime.now().strftime('%Y-%m-%d')} 001

                ## 本轮主题

                流程与协作规则收尾

                ## 工作日志

                ### {datetime.now().strftime('%Y-%m-%d %H:%M')}

                - 完成第一轮规则收口
                """,
            )

            report = update_project_docs.suggest_handoff(
                doc_root,
                "完成第二轮规则收口",
                ["补齐脚本规则"],
                [],
                ["继续当前主线"],
                "pending",
                "auto",
                "auto",
                "",
                "",
                [],
                False,
                True,
                0,
            )

            self.assertEqual(report["session_advice"], "建议新开修复/复审会话")
            self.assertIn("审查已发起但结果尚未回传", report["session_reason"])
            self.assertIn("自动压缩背景信息", report["session_reason"])

    def test_suggest_handoff_deferred_review_after_reviewer_handoff_stays_in_current_fix_session(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            doc_root = create_doc_root(root)

            # Old reviewer timed out; a new reviewer continues the same review_chain_id.
            report = update_project_docs.suggest_handoff(
                doc_root,
                "继续收敛 Agent 6 去阻塞规则",
                ["补齐状态机定义"],
                [],
                ["继续当前修复与验证"],
                "deferred",
                "auto",
                "continue",
                "",
                "",
                [],
                False,
                False,
                0,
            )

            self.assertEqual(report["session_advice"], "继续当前修复/验证会话")
            self.assertIn("待回补复审", report["session_reason"])
            self.assertEqual(report["iteration_advice"], "继续当前迭代记录")

    def test_suggest_handoff_prefers_new_session_after_two_same_session_major_tasks(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            doc_root = create_doc_root(root)

            report = update_project_docs.suggest_handoff(
                doc_root,
                "继续补第三轮规则收口",
                ["更新收尾模板"],
                [],
                ["继续当前主线"],
                "passed",
                "auto",
                "auto",
                "",
                "",
                [],
                False,
                False,
                2,
            )

            self.assertEqual(report["session_advice"], "建议新开下一阶段会话")
            self.assertIn("当前会话内已连续完成 2 个大任务", report["session_reason"])

    def test_suggest_handoff_generates_chinese_commit_suggestion(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            doc_root = create_doc_root(root)

            report = update_project_docs.suggest_handoff(
                doc_root,
                "统一新会话启动与收尾流程",
                ["为收尾流程补固定三段输出", "为提交建议补中文标题和内容"],
                [],
                ["继续推进脚本收尾规则"],
                "passed",
                "auto",
                "auto",
                "",
                "",
                [],
                False,
                False,
                0,
            )

            self.assertEqual(report["doc_maintenance_status"], "已执行")
            self.assertTrue(report["commit_suggestion"]["suitable"])
            self.assertEqual(report["commit_suggestion"]["title"], "[23000001] 流程：新会话启动与收尾流程")
            self.assertEqual(
                report["commit_suggestion"]["content"][:2],
                ["为收尾流程补固定三段输出", "为提交建议补中文标题和内容"],
            )
            formatted = update_project_docs.format_handoff_report(report)
            self.assertIn("文档维护：已执行", formatted)
            self.assertIn("Git 提交建议：适合提交", formatted)
            self.assertIn("标题：[23000001] 流程：新会话启动与收尾流程", formatted)
            self.assertIn("提交确认：如需我继续执行，请回复 1（提交并推送） / 2（只提交） / 确认 / 提交 / 直接提交（只提交）。", formatted)
            self.assertIn("会话建议：", formatted)

    def test_classify_topic_prefers_document_category_for_doc_words(self):
        self.assertEqual(
            update_project_docs.classify_topic("整理项目总览与长期规则文档入口"),
            "文档整理",
        )

    def test_classify_commit_module_distinguishes_plan_and_table_docs(self):
        self.assertEqual(
            update_project_docs.classify_commit_module("补齐 Gatebreaker Arena_v1 方案落地描述", "文档整理"),
            "240",
        )
        self.assertEqual(
            update_project_docs.classify_commit_module("补齐正式建筑内容表方案与表结构说明", "文档整理"),
            "250",
        )

    def test_suggest_handoff_uses_document_prefix_for_doc_cleanup(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            doc_root = create_doc_root(root)

            report = update_project_docs.suggest_handoff(
                doc_root,
                "整理项目总览与长期规则文档入口",
                [],
                [],
                ["继续维护文档入口"],
                "passed",
                "continue",
                "continue",
                "",
                "",
                [],
                False,
                False,
                0,
            )

            self.assertTrue(report["commit_suggestion"]["suitable"])
            self.assertEqual(report["commit_suggestion"]["title"], "[21000001] 文档：整理项目总览与长期规则文档入口")

    def test_suggest_commit_message_uses_next_numbered_commit_sequence_per_module(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            doc_root = create_doc_root(root)
            subprocess.run(["git", "init"], cwd=root, check=True, capture_output=True, text=True)
            subprocess.run(["git", "config", "user.name", "Codex Test"], cwd=root, check=True, capture_output=True, text=True)
            subprocess.run(["git", "config", "user.email", "codex@example.com"], cwd=root, check=True, capture_output=True, text=True)

            tracked = write_file(root, "README.md", "hello\n")
            subprocess.run(["git", "add", "README.md"], cwd=root, check=True, capture_output=True, text=True)
            subprocess.run(["git", "commit", "-m", "[23000003] 流程：已有 230 模块提交"], cwd=root, check=True, capture_output=True, text=True)

            tracked.write_text("world\n", encoding="utf-8")
            subprocess.run(["git", "add", "README.md"], cwd=root, check=True, capture_output=True, text=True)
            subprocess.run(["git", "commit", "-m", "[21000002] 文档：其他模块提交"], cwd=root, check=True, capture_output=True, text=True)

            report = update_project_docs.suggest_handoff(
                doc_root,
                "统一新会话启动与收尾流程",
                ["为收尾流程补固定三段输出"],
                [],
                ["继续推进脚本收尾规则"],
                "passed",
                "auto",
                "auto",
                "",
                "",
                [],
                False,
                False,
                0,
            )

            self.assertTrue(report["commit_suggestion"]["suitable"])
            self.assertEqual(report["commit_suggestion"]["title"], "[23000004] 流程：新会话启动与收尾流程")

    def test_suggest_commit_message_ignores_legacy_four_digit_sequence_titles(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            doc_root = create_doc_root(root)
            subprocess.run(["git", "init"], cwd=root, check=True, capture_output=True, text=True)
            subprocess.run(["git", "config", "user.name", "Codex Test"], cwd=root, check=True, capture_output=True, text=True)
            subprocess.run(["git", "config", "user.email", "codex@example.com"], cwd=root, check=True, capture_output=True, text=True)

            tracked = write_file(root, "README.md", "hello\n")
            subprocess.run(["git", "add", "README.md"], cwd=root, check=True, capture_output=True, text=True)
            subprocess.run(["git", "commit", "-m", "[0055] 玩法：旧四位编号提交"], cwd=root, check=True, capture_output=True, text=True)

            report = update_project_docs.suggest_handoff(
                doc_root,
                "统一新会话启动与收尾流程",
                ["为收尾流程补固定三段输出"],
                [],
                ["继续推进脚本收尾规则"],
                "passed",
                "auto",
                "auto",
                "",
                "",
                [],
                False,
                False,
                0,
            )

            self.assertTrue(report["commit_suggestion"]["suitable"])
            self.assertEqual(report["commit_suggestion"]["title"], "[23000001] 流程：新会话启动与收尾流程")

    def test_suggest_handoff_caches_pending_commit_suggestion_in_git_dir(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            doc_root = create_doc_root(root)
            subprocess.run(["git", "init"], cwd=root, check=True, capture_output=True, text=True)
            subprocess.run(["git", "config", "user.name", "Codex Test"], cwd=root, check=True, capture_output=True, text=True)
            subprocess.run(["git", "config", "user.email", "codex@example.com"], cwd=root, check=True, capture_output=True, text=True)

            tracked = write_file(root, "README.md", "hello\n")
            subprocess.run(["git", "add", "README.md"], cwd=root, check=True, capture_output=True, text=True)
            subprocess.run(["git", "commit", "-m", "[23000002] 流程：已有编号提交"], cwd=root, check=True, capture_output=True, text=True)

            report = update_project_docs.suggest_handoff(
                doc_root,
                "统一新会话启动与收尾流程",
                ["为收尾流程补固定三段输出"],
                [],
                ["继续推进脚本收尾规则"],
                "passed",
                "auto",
                "auto",
                "",
                "",
                [],
                False,
                False,
                0,
            )

            cache = update_project_docs.read_pending_commit_suggestion(doc_root)
            self.assertTrue(report["commit_suggestion"]["suitable"])
            self.assertIsNotNone(cache)
            self.assertEqual(cache["title"], "[23000003] 流程：新会话启动与收尾流程")
            self.assertEqual(cache["content"], ["为收尾流程补固定三段输出", "统一新会话启动与收尾流程"])
            self.assertTrue(cache["head_commit"])
            self.assertEqual(cache["module_code"], "230")
            self.assertEqual(cache["sequence"], 3)

    def test_sync_commit_sequence_registry_writes_latest_modules(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            doc_root = create_doc_root(root)
            init_git_repo(root)

            tracked = write_file(root, "README.md", "hello\n")
            subprocess.run(["git", "add", "README.md"], cwd=root, check=True, capture_output=True, text=True)
            subprocess.run(["git", "commit", "-m", "[23000002] 流程：已有提交流水"], cwd=root, check=True, capture_output=True, text=True)

            tracked.write_text("world\n", encoding="utf-8")
            subprocess.run(["git", "add", "README.md"], cwd=root, check=True, capture_output=True, text=True)
            subprocess.run(["git", "commit", "-m", "[21000001] 文档：补入口说明"], cwd=root, check=True, capture_output=True, text=True)

            result = update_project_docs.sync_commit_sequence_registry(doc_root)

            self.assertTrue(result["changed"])
            payload = json.loads((doc_root / "长期主文档/协作与执行/状态登记/commit_module_sequences.json").read_text(encoding="utf-8"))
            self.assertEqual(payload["modules"]["230"], 2)
            self.assertEqual(payload["modules"]["210"], 1)

    def test_validate_commit_message_updates_registry_and_stages_file(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            doc_root = create_doc_root(root)
            init_git_repo(root)
            install_doc_maintenance_tools(root)

            tracked = write_file(root, "README.md", "hello\n")
            subprocess.run(["git", "add", "README.md"], cwd=root, check=True, capture_output=True, text=True)
            subprocess.run(["git", "commit", "-m", "[23000002] 流程：已有提交流水"], cwd=root, check=True, capture_output=True, text=True)

            tracked.write_text("next\n", encoding="utf-8")
            subprocess.run(["git", "add", "README.md"], cwd=root, check=True, capture_output=True, text=True)
            message_file = write_file(root, ".git/COMMIT_EDITMSG", "[23000003] 流程：继续补规则\n")

            completed = subprocess.run(
                [
                    "python3",
                    str(root / "tools/doc_maintenance/update_project_docs.py"),
                    "--doc-root",
                    str(doc_root),
                    "validate-commit-message",
                    "--message-file",
                    str(message_file),
                    "--no-fetch",
                    "--stage-registry",
                ],
                cwd=root,
                capture_output=True,
                text=True,
                check=False,
            )

            self.assertEqual(completed.returncode, 0, completed.stderr)
            registry_path = root / "doc/长期主文档/协作与执行/状态登记/commit_module_sequences.json"
            payload = json.loads(registry_path.read_text(encoding="utf-8"))
            self.assertEqual(payload["modules"]["230"], 3)
            staged_output = subprocess.run(
                ["git", "diff", "--cached", "--name-only", "-z"],
                cwd=root,
                capture_output=True,
                check=True,
            ).stdout
            staged = [chunk.decode("utf-8") for chunk in staged_output.split(b"\0") if chunk]
            self.assertIn("doc/长期主文档/协作与执行/状态登记/commit_module_sequences.json", staged)

    def test_validate_commit_message_rewrites_default_merge_title(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            doc_root = create_doc_root(root)
            init_git_repo(root)
            install_doc_maintenance_tools(root)

            write_file(root, "README.md", "hello\n")
            subprocess.run(["git", "add", "README.md"], cwd=root, check=True, capture_output=True, text=True)
            subprocess.run(["git", "commit", "-m", "[23000002] 流程：已有提交流水"], cwd=root, check=True, capture_output=True, text=True)

            write_file(root, "doc/长期主文档/协作与执行/Git 提交建议与确认规则.md", "规则\n")
            subprocess.run(
                ["git", "add", "doc/长期主文档/协作与执行/Git 提交建议与确认规则.md"],
                cwd=root,
                check=True,
                capture_output=True,
                text=True,
            )
            message_file = write_file(
                root,
                ".git/COMMIT_EDITMSG",
                """
                Merge branch 'main' of github.com:wx383847364/Gatebreaker Arena

                # Please enter the commit message for your changes.
                """,
            )

            completed = subprocess.run(
                [
                    "python3",
                    str(root / "tools/doc_maintenance/update_project_docs.py"),
                    "--doc-root",
                    str(doc_root),
                    "validate-commit-message",
                    "--message-file",
                    str(message_file),
                    "--no-fetch",
                    "--stage-registry",
                ],
                cwd=root,
                capture_output=True,
                text=True,
                check=False,
            )

            self.assertEqual(completed.returncode, 0, completed.stderr)
            rewritten = message_file.read_text(encoding="utf-8")
            self.assertTrue(rewritten.startswith("[23000003] 合并：同步 main 分支\n"), rewritten)
            self.assertIn("- 合并 main 分支并同步当前分支基线", rewritten)
            self.assertIn("- 保持合并提交标题和正文符合 Gatebreaker Arena 八位编号规则", rewritten)
            self.assertIn("# Please enter the commit message for your changes.", rewritten)

            registry_path = root / "doc/长期主文档/协作与执行/状态登记/commit_module_sequences.json"
            payload = json.loads(registry_path.read_text(encoding="utf-8"))
            self.assertEqual(payload["modules"]["230"], 3)
            staged_output = subprocess.run(
                ["git", "diff", "--cached", "--name-only", "-z"],
                cwd=root,
                capture_output=True,
                check=True,
            ).stdout
            staged = [chunk.decode("utf-8") for chunk in staged_output.split(b"\0") if chunk]
            self.assertIn("doc/长期主文档/协作与执行/状态登记/commit_module_sequences.json", staged)

    def test_merge_source_parses_remote_tracking_branch(self):
        source = update_project_docs.merge_source_from_subject("Merge remote-tracking branch 'origin/main'")

        self.assertEqual(source, "origin/main")

    def test_validate_commit_message_rejects_plain_non_numbered_title(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            doc_root = create_doc_root(root)
            init_git_repo(root)
            install_doc_maintenance_tools(root)
            message_file = write_file(root, ".git/COMMIT_EDITMSG", "流程：缺少编号\n")

            completed = subprocess.run(
                [
                    "python3",
                    str(root / "tools/doc_maintenance/update_project_docs.py"),
                    "--doc-root",
                    str(doc_root),
                    "validate-commit-message",
                    "--message-file",
                    str(message_file),
                    "--no-fetch",
                    "--stage-registry",
                ],
                cwd=root,
                capture_output=True,
                text=True,
                check=False,
            )

            self.assertNotEqual(completed.returncode, 0)
            self.assertIn("提交标题必须以 [TMMSSSSS] 八位编号开头。", completed.stderr)
            self.assertIn("当前标题：流程：缺少编号", completed.stderr)

    def test_validate_commit_message_rejects_empty_title(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            doc_root = create_doc_root(root)
            init_git_repo(root)
            install_doc_maintenance_tools(root)
            message_file = write_file(root, ".git/COMMIT_EDITMSG", "\n# comment only\n")

            completed = subprocess.run(
                [
                    "python3",
                    str(root / "tools/doc_maintenance/update_project_docs.py"),
                    "--doc-root",
                    str(doc_root),
                    "validate-commit-message",
                    "--message-file",
                    str(message_file),
                    "--no-fetch",
                    "--stage-registry",
                ],
                cwd=root,
                capture_output=True,
                text=True,
                check=False,
            )

            self.assertNotEqual(completed.returncode, 0)
            self.assertIn("提交标题不能为空，且必须以 [TMMSSSSS] 八位编号开头。", completed.stderr)

    def test_validate_commit_message_rejects_non_numbered_revert_fixup_and_squash(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            doc_root = create_doc_root(root)
            init_git_repo(root)
            install_doc_maintenance_tools(root)

            for subject in [
                "Revert \"[23000002] 流程：已有提交流水\"",
                "fixup! [23000002] 流程：已有提交流水",
                "squash! [23000002] 流程：已有提交流水",
            ]:
                with self.subTest(subject=subject):
                    message_file = write_file(root, ".git/COMMIT_EDITMSG", subject + "\n")

                    completed = subprocess.run(
                        [
                            "python3",
                            str(root / "tools/doc_maintenance/update_project_docs.py"),
                            "--doc-root",
                            str(doc_root),
                            "validate-commit-message",
                            "--message-file",
                            str(message_file),
                            "--no-fetch",
                            "--stage-registry",
                        ],
                        cwd=root,
                        capture_output=True,
                        text=True,
                        check=False,
                    )

                    self.assertNotEqual(completed.returncode, 0)
                    self.assertIn("提交标题必须以 [TMMSSSSS] 八位编号开头。", completed.stderr)
                    self.assertEqual(message_file.read_text(encoding="utf-8"), subject + "\n")

    def test_fetch_commit_sequence_baseline_reuses_recent_cache(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            doc_root = create_doc_root(root)
            init_git_repo(root)

            cache_path = update_project_docs.commit_sequence_fetch_cache_path(doc_root)
            assert cache_path is not None
            cache_path.parent.mkdir(parents=True, exist_ok=True)
            cache_path.write_text(
                json.dumps(
                    {
                        "target": "origin/main",
                        "fetched_at": datetime.now().isoformat(timespec="seconds"),
                    },
                    ensure_ascii=False,
                )
                + "\n",
                encoding="utf-8",
            )

            with mock.patch.object(update_project_docs, "branch_upstream", return_value="origin/main"), mock.patch.object(
                update_project_docs, "list_git_remotes", return_value=["origin"]
            ), mock.patch.object(update_project_docs, "run_git", side_effect=AssertionError("should not fetch")):
                result = update_project_docs.fetch_commit_sequence_baseline(doc_root)

            self.assertTrue(result["ok"])
            self.assertFalse(result["attempted"])
            self.assertIn("复用", result["message"])

    def test_next_commit_sequence_fetch_latest_bypasses_recent_cache(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            doc_root = create_doc_root(root)
            init_git_repo(root)

            cache_path = update_project_docs.commit_sequence_fetch_cache_path(doc_root)
            assert cache_path is not None
            cache_path.parent.mkdir(parents=True, exist_ok=True)
            cache_path.write_text(
                json.dumps(
                    {
                        "target": "origin/main",
                        "fetched_at": datetime.now().isoformat(timespec="seconds"),
                    },
                    ensure_ascii=False,
                )
                + "\n",
                encoding="utf-8",
            )

            with mock.patch.object(update_project_docs, "branch_upstream", return_value="origin/main"), mock.patch.object(
                update_project_docs, "list_git_remotes", return_value=["origin"]
            ), mock.patch.object(update_project_docs, "run_git") as mocked_run_git, mock.patch.object(
                update_project_docs, "latest_module_sequence_from_git_history", return_value=0
            ):
                mocked_run_git.side_effect = [
                    mock.Mock(returncode=0, stdout="", stderr=""),
                    mock.Mock(returncode=0, stdout="", stderr=""),
                ]
                next_sequence = update_project_docs.next_commit_sequence(doc_root, "230", fetch_latest=True)

            self.assertEqual(next_sequence, 1)
            self.assertGreaterEqual(mocked_run_git.call_count, 1)
            self.assertEqual(mocked_run_git.call_args_list[0].args[1][:2], ["fetch", "--quiet"])

    def test_suggest_current_commit_handles_registry_only_change(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            init_git_repo(root)
            doc_root = create_doc_root(root)

            write_file(root, "README.md", "hello\n")
            subprocess.run(["git", "add", "."], cwd=root, check=True, capture_output=True, text=True)
            subprocess.run(["git", "commit", "-m", "[23000001] 流程：初始化规则"], cwd=root, check=True, capture_output=True, text=True)

            registry_path = write_file(
                root,
                "doc/长期主文档/协作与执行/状态登记/commit_module_sequences.json",
                """
                {
                  "version": 1,
                  "modules": {
                    "230": 1
                  }
                }
                """,
            )
            subprocess.run(["git", "add", str(registry_path.relative_to(root))], cwd=root, check=True, capture_output=True, text=True)
            registry_path.write_text(
                textwrap.dedent(
                    """
                    {
                      "version": 1,
                      "modules": {
                        "230": 2
                      }
                    }
                    """
                ).lstrip("\n"),
                encoding="utf-8",
            )
            subprocess.run(["git", "add", str(registry_path.relative_to(root))], cwd=root, check=True, capture_output=True, text=True)

            report = update_project_docs.suggest_current_commit(doc_root)

            self.assertTrue(report["suitable"])
            self.assertEqual(report["title"], "[23000003] 流程：同步协作模块编号登记")
            self.assertIn("同步 230 模块提交编号到最新状态", report["content"])
            cache = update_project_docs.read_pending_commit_suggestion(doc_root)
            self.assertIsNotNone(cache)
            self.assertEqual(cache["title"], report["title"])

    def test_suggest_current_commit_allows_auxiliary_docs_and_tests_with_single_primary_module(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            init_git_repo(root)
            doc_root = create_doc_root(root)

            write_file(root, "README.md", "hello\n")
            write_file(root, "tools/doc_maintenance/update_project_docs.py", "print('seed')\n")
            write_file(root, "tools/tests/doc_maintenance/test_update_project_docs.py", "def test_seed():\n    assert True\n")
            write_file(root, "doc/迭代记录/迭代记录索引.md", "# 索引\n")
            write_file(root, "doc/长期主文档/主文档索引.md", "# 主文档索引\n")
            write_file(
                root,
                "doc/迭代记录/迭代记录_20260408_001.md",
                """
                # 迭代记录 2026-04-08 001

                ## 分工状态

                - Agent 1：a
                - Agent 2：b
                - Agent 3：c
                - Agent 4：d
                - Agent 5：e
                - Agent 6：f
                """,
            )
            subprocess.run(["git", "add", "."], cwd=root, check=True, capture_output=True, text=True)
            subprocess.run(["git", "commit", "-m", "[23000001] 流程：初始化规则"], cwd=root, check=True, capture_output=True, text=True)

            write_file(root, "tools/doc_maintenance/update_project_docs.py", "print('fast path')\n")
            write_file(root, "tools/tests/doc_maintenance/test_update_project_docs.py", "def test_fast_path():\n    assert True\n")
            write_file(root, "doc/长期主文档/协作与执行/Git 提交建议与确认规则.md", "# 规则\n")
            write_file(root, "doc/迭代记录/迭代记录索引.md", "# 索引\n- 新增\n")
            write_file(root, "doc/长期主文档/主文档索引.md", "# 主文档索引\n- 新增\n")
            write_file(
                root,
                "doc/迭代记录/迭代记录_20260408_001.md",
                """
                # 迭代记录 2026-04-08 001

                ## 分工状态

                - Agent 1：a
                - Agent 2：b
                - Agent 3：c
                - Agent 4：d
                - Agent 5：e
                - Agent 6：f

                ## 工作日志

                - 优化快路径
                """,
            )
            subprocess.run(["git", "add", "."], cwd=root, check=True, capture_output=True, text=True)

            report = update_project_docs.suggest_current_commit(doc_root)

            self.assertTrue(report["suitable"])
            self.assertTrue(report["title"].startswith("[23000002] 流程：修复 Git hook 编号登记时机"))

    def test_suggest_current_commit_reuses_unsuitable_snapshot_cache(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            init_git_repo(root)
            doc_root = create_doc_root(root)

            write_file(root, "tools/doc_maintenance/update_project_docs.py", "print('seed')\n")
            subprocess.run(["git", "add", "."], cwd=root, check=True, capture_output=True, text=True)
            subprocess.run(["git", "commit", "-m", "[23000001] 流程：初始化规则"], cwd=root, check=True, capture_output=True, text=True)

            first = update_project_docs.suggest_current_commit(doc_root)
            self.assertFalse(first["suitable"])
            with mock.patch.object(
                update_project_docs,
                "current_commit_title_and_content",
                side_effect=AssertionError("should reuse cached suggestion"),
            ), mock.patch.object(
                update_project_docs,
                "fetch_commit_sequence_baseline",
                side_effect=AssertionError("should not fetch again"),
            ):
                second = update_project_docs.suggest_current_commit(doc_root)

            self.assertEqual(second, first)

    def test_suggest_current_commit_does_not_reuse_suitable_snapshot_cache(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            init_git_repo(root)
            doc_root = create_doc_root(root)

            target = write_file(root, "tools/doc_maintenance/update_project_docs.py", "print('seed')\n")
            subprocess.run(["git", "add", "."], cwd=root, check=True, capture_output=True, text=True)
            subprocess.run(["git", "commit", "-m", "[23000001] 流程：初始化规则"], cwd=root, check=True, capture_output=True, text=True)

            target.write_text("print('fast path')\n", encoding="utf-8")
            subprocess.run(["git", "add", "."], cwd=root, check=True, capture_output=True, text=True)
            first = update_project_docs.suggest_current_commit(doc_root)
            self.assertTrue(first["suitable"])
            with mock.patch.object(
                update_project_docs,
                "current_commit_title_and_content",
                return_value=("230", "[23000002] 流程：修复 Git hook 编号登记时机", ["x"]),
            ) as mocked_builder:
                report = update_project_docs.suggest_current_commit(doc_root)

            self.assertTrue(report["suitable"])
            self.assertEqual(mocked_builder.call_count, 1)

    def test_suggest_current_commit_invalidates_unsuitable_cache_when_worktree_changes(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            init_git_repo(root)
            doc_root = create_doc_root(root)

            target = write_file(root, "tools/doc_maintenance/update_project_docs.py", "print('seed')\n")
            subprocess.run(["git", "add", "."], cwd=root, check=True, capture_output=True, text=True)
            subprocess.run(["git", "commit", "-m", "[23000001] 流程：初始化规则"], cwd=root, check=True, capture_output=True, text=True)

            update_project_docs.suggest_current_commit(doc_root)

            target.write_text("print('fast path')\n", encoding="utf-8")
            subprocess.run(["git", "add", "."], cwd=root, check=True, capture_output=True, text=True)
            with mock.patch.object(
                update_project_docs,
                "current_commit_title_and_content",
                return_value=("230", "[23000002] 流程：修复 Git hook 编号登记时机", ["x"]),
            ) as mocked_builder:
                report = update_project_docs.suggest_current_commit(doc_root)

            self.assertTrue(report["suitable"])
            self.assertEqual(mocked_builder.call_count, 1)

    def test_suggest_current_commit_rejects_partially_staged_same_file(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            init_git_repo(root)
            doc_root = create_doc_root(root)

            tracked = write_file(root, "tools/doc_maintenance/update_project_docs.py", "print('seed')\n")
            subprocess.run(["git", "add", "."], cwd=root, check=True, capture_output=True, text=True)
            subprocess.run(["git", "commit", "-m", "[23000001] 流程：初始化规则"], cwd=root, check=True, capture_output=True, text=True)

            tracked.write_text("print('stage-1')\n", encoding="utf-8")
            subprocess.run(["git", "add", str(tracked.relative_to(root))], cwd=root, check=True, capture_output=True, text=True)
            tracked.write_text("print('stage-1')\nprint('unstaged-2')\n", encoding="utf-8")

            report = update_project_docs.suggest_current_commit(doc_root)

            self.assertFalse(report["suitable"])
            self.assertIn("部分已暂存、部分未暂存", report["reason"])

    def test_commit_msg_hook_fetches_remote_and_rejects_stale_sequence(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            temp = Path(temp_dir)
            remote = temp / "remote.git"
            local = temp / "local"
            other = temp / "other"

            subprocess.run(["git", "init", "--bare", str(remote)], check=True, capture_output=True, text=True)
            subprocess.run(["git", "clone", str(remote), str(local)], check=True, capture_output=True, text=True)

            create_doc_root(local)
            subprocess.run(["git", "config", "user.name", "Codex Test"], cwd=local, check=True, capture_output=True, text=True)
            subprocess.run(["git", "config", "user.email", "codex@example.com"], cwd=local, check=True, capture_output=True, text=True)
            install_doc_maintenance_tools(local)
            install_git_hooks(local, include_pre_commit=False, include_commit_msg=True)
            write_file(local, "README.md", "hello\n")

            subprocess.run(["git", "add", "README.md"], cwd=local, check=True, capture_output=True, text=True)
            subprocess.run(["git", "commit", "-m", "[23000001] 流程：初始化规则"], cwd=local, check=True, capture_output=True, text=True)
            subprocess.run(["git", "push", "-u", "origin", "HEAD"], cwd=local, check=True, capture_output=True, text=True)

            subprocess.run(["git", "clone", str(remote), str(other)], check=True, capture_output=True, text=True)
            create_doc_root(other)
            subprocess.run(["git", "config", "user.name", "Codex Test"], cwd=other, check=True, capture_output=True, text=True)
            subprocess.run(["git", "config", "user.email", "codex@example.com"], cwd=other, check=True, capture_output=True, text=True)
            install_doc_maintenance_tools(other)
            install_git_hooks(other, include_pre_commit=False, include_commit_msg=True)
            write_file(other, "README.md", "other\n")
            subprocess.run(["git", "add", "README.md"], cwd=other, check=True, capture_output=True, text=True)
            subprocess.run(["git", "commit", "-m", "[23000002] 流程：远端先占号"], cwd=other, check=True, capture_output=True, text=True)
            subprocess.run(["git", "push"], cwd=other, check=True, capture_output=True, text=True)

            write_file(local, "README.md", "local\n")
            subprocess.run(["git", "add", "README.md"], cwd=local, check=True, capture_output=True, text=True)
            completed = subprocess.run(
                ["git", "commit", "-m", "[23000002] 流程：本地旧号"],
                cwd=local,
                capture_output=True,
                text=True,
                check=False,
            )

            self.assertNotEqual(completed.returncode, 0)
            self.assertIn("提交标题编号校验失败", completed.stderr)
            self.assertIn("[23000003]", completed.stderr)

    def test_commit_hooks_amend_sequence_registry_into_same_commit(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            doc_root = create_doc_root(root)
            init_git_repo(root)
            install_doc_maintenance_tools(root)
            install_git_hooks(root, include_pre_commit=False, include_commit_msg=True, include_post_commit=True)

            write_file(root, "README.md", "hello\n")
            subprocess.run(["git", "add", "."], cwd=root, check=True, capture_output=True, text=True)
            subprocess.run(["git", "commit", "-m", "[23000001] 流程：初始化规则"], cwd=root, check=True, capture_output=True, text=True)

            registry_path = "doc/长期主文档/协作与执行/状态登记/commit_module_sequences.json"
            head_registry = json.loads(
                subprocess.run(
                    ["git", "show", f"HEAD:{registry_path}"],
                    cwd=root,
                    check=True,
                    capture_output=True,
                    text=True,
                ).stdout
            )
            self.assertEqual(head_registry["modules"]["230"], 1)
            self.assertEqual(
                subprocess.run(["git", "status", "--short"], cwd=root, check=True, capture_output=True, text=True).stdout,
                "",
            )

            write_file(root, "README.md", "next\n")
            subprocess.run(["git", "add", "README.md"], cwd=root, check=True, capture_output=True, text=True)
            subprocess.run(["git", "commit", "-m", "[23000002] 流程：继续规则"], cwd=root, check=True, capture_output=True, text=True)

            head_registry = json.loads(
                subprocess.run(
                    ["git", "show", f"HEAD:{registry_path}"],
                    cwd=root,
                    check=True,
                    capture_output=True,
                    text=True,
                ).stdout
            )
            self.assertEqual(head_registry["modules"]["230"], 2)
            self.assertEqual(
                subprocess.run(["git", "status", "--short"], cwd=root, check=True, capture_output=True, text=True).stdout,
                "",
            )

    def test_unsuitable_handoff_clears_pending_commit_cache(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            doc_root = create_doc_root(root)
            subprocess.run(["git", "init"], cwd=root, check=True, capture_output=True, text=True)
            subprocess.run(["git", "config", "user.name", "Codex Test"], cwd=root, check=True, capture_output=True, text=True)
            subprocess.run(["git", "config", "user.email", "codex@example.com"], cwd=root, check=True, capture_output=True, text=True)

            tracked = write_file(root, "README.md", "hello\n")
            subprocess.run(["git", "add", "README.md"], cwd=root, check=True, capture_output=True, text=True)
            subprocess.run(["git", "commit", "-m", "[23000005] 流程：已有编号提交"], cwd=root, check=True, capture_output=True, text=True)

            update_project_docs.suggest_handoff(
                doc_root,
                "统一新会话启动与收尾流程",
                ["为收尾流程补固定三段输出"],
                [],
                ["继续推进脚本收尾规则"],
                "passed",
                "auto",
                "auto",
                "",
                "",
                [],
                False,
                False,
                0,
            )
            self.assertIsNotNone(update_project_docs.read_pending_commit_suggestion(doc_root))

            update_project_docs.suggest_handoff(
                doc_root,
                "继续修复 Agent 6 findings",
                ["补齐回归验证"],
                [],
                ["继续当前修复"],
                "failed",
                "auto",
                "auto",
                "",
                "",
                [],
                False,
                False,
                0,
            )

            self.assertIsNone(update_project_docs.read_pending_commit_suggestion(doc_root))

    def test_format_handoff_report_for_unsuitable_commit_includes_do_not_commit_prompt(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            doc_root = create_doc_root(root)

            report = update_project_docs.suggest_handoff(
                doc_root,
                "继续修复 Agent 6 findings",
                ["补齐回归验证"],
                [],
                ["继续当前修复"],
                "failed",
                "auto",
                "auto",
                "",
                "",
                [],
                False,
                False,
                0,
            )

            formatted = update_project_docs.format_handoff_report(report)
            self.assertIn("Git 提交建议：暂不建议提交。原因是：Agent 6 审查未通过", formatted)
            self.assertIn("提交确认：当前不建议直接提交；如需强制提交，请明确说明。", formatted)


if __name__ == "__main__":
    unittest.main()
