# Git 提交建议与确认规则

这页固定 Gatebreaker Arena 的 Git 提交标题、八位编号和确认口径。

## 八位编号

提交标题必须以八位编号开头：

```text
[TMMSSSSS] 类型：摘要
```

- `TMM`：三位模块编号。
- `SSSSS`：五位流水号，从 `00001` 开始。
- 示例：`[61000001] 架构：初始化 Gatebreaker Arena 工程骨架`

当前默认模块：

- `210`：项目总览、主文档索引、研发入口
- `220`：架构与边界、热更新边界
- `230`：协作与执行、提交规则、文档维护工具
- `240`：方案、GDD、玩法计划
- `250`：配置表与数据
- `260`：迭代记录
- `610`：通用工程初始化与无法稳定归类的工程级改动

## 提交前流程

1. 执行必要验证。
   - 如提示 Git hooks 未安装，先执行：

```bash
bash tools/repo_maintenance/install_git_hooks.sh
```

2. 如果本轮包含工程级改动，补迭代记录或执行：

```bash
bash tools/doc_maintenance/finalize_task.sh \
  --summary "本轮完成了什么" \
  --done "完成项" \
  --next "下一步" \
  --agent6-review not-required
```

3. 执行文档同步：

```bash
python3 tools/doc_maintenance/update_project_docs.py --doc-root doc sync
```

4. 使用带八位编号的提交标题。

## 首次提交建议

首次初始化提交使用：

```text
[61000001] 架构：初始化 Gatebreaker Arena 工程骨架
```

提交正文建议：

```text
- 建立 Unity + HybridCLR + YooAssets 独立工程骨架
- 接入 App.AOT / App.Shared / App.HotUpdate 三层结构
- 新增 Gatebreaker Arena 热更占位入口和长期文档体系
```

## 确认口径

- 用户要求“提交并推送”时，允许执行 `git commit` 和 `git push`。
- 如果远端认证失败，只报告失败原因和本地提交状态，不改写历史。

## Hook 约束

- `core.hooksPath` 必须指向 `.githooks`；可执行 `python3 tools/repo_maintenance/check_git_hooks_installed.py` 检查。
- `prepare-commit-msg` 会在 merge 来源时提前生成规范 merge 标题和正文。
- `commit-msg` 会校验普通提交的八位编号；merge commit 还必须使用 `合并：` 标题前缀，并包含规范正文条目。
