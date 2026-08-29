"""把 design/ 下的手工维护 CSV 导出为 Assets/Config/DT_*.json。

约定：
- 输入：UTF-8（允许 BOM），首行为表头，逗号分隔，允许末尾空行。
- 输出：JSON 数组（与既有 DT_*.json 一致），UTF-8 无 BOM，indent=2，ensure_ascii=False。
- 类型：整列可解析为整数则转 int，否则可解析为浮点则转 float，否则保留字符串。
- 文件名：默认与源 CSV 同名（替换扩展名），保持 1:1 可追溯。

用法：
    python tools/csv_to_dt_config.py <csv路径> [<csv路径> ...] [--out-dir DIR]
"""
from __future__ import annotations

import argparse
import csv
import json
import re
import sys
from pathlib import Path

INT_RE = re.compile(r"^-?\d+$")
FLOAT_RE = re.compile(r"^-?\d+\.\d+$")


def coerce_column(values: list[str]) -> list[object]:
    """按整列推断类型：全为整数 -> int；全为浮点 -> float；否则原样字符串。"""
    if not values:
        return []
    stripped = [v.strip() for v in values]
    if all(INT_RE.match(v) for v in stripped) and any(stripped):
        return [int(v) for v in stripped]
    if all(FLOAT_RE.match(v) for v in stripped) and any(stripped):
        return [float(v) for v in stripped]
    return values


def convert(csv_path: Path, out_dir: Path) -> Path:
    # utf-8-sig 自动剥离 BOM，避免首列名带 \ufeff
    with csv_path.open("r", encoding="utf-8-sig", newline="") as handle:
        reader = csv.reader(handle)
        rows = [row for row in reader if any(cell.strip() for cell in row)]

    if not rows:
        raise SystemExit(f"{csv_path}: 空文件，无表头")

    header = [col.strip() for col in rows[0]]
    body = rows[1:]

    # 校验列数一致，避免静默错位
    for index, row in enumerate(body, start=2):
        if len(row) != len(header):
            raise SystemExit(
                f"{csv_path}:{index} 列数 {len(row)} 与表头 {len(header)} 不一致 -> {row!r}"
            )

    columns = {name: [row[i] for row in body] for i, name in enumerate(header)}
    typed = {name: coerce_column(values) for name, values in columns.items()}

    records = [
        {name: typed[name][row_index] for name in header}
        for row_index in range(len(body))
    ]

    out_dir.mkdir(parents=True, exist_ok=True)
    out_path = out_dir / f"{csv_path.stem}.json"
    out_path.write_text(
        json.dumps(records, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )
    return out_path, len(records), header


def main() -> int:
    parser = argparse.ArgumentParser(description="CSV -> DT_*.json 配置导出")
    parser.add_argument("csv_files", nargs="+", type=Path, help="源 CSV 路径")
    parser.add_argument(
        "--out-dir",
        type=Path,
        default=Path(__file__).resolve().parents[1] / "Assets" / "Config",
        help="输出目录，默认 Assets/Config",
    )
    args = parser.parse_args()

    for csv_path in args.csv_files:
        if not csv_path.is_file():
            raise SystemExit(f"找不到文件：{csv_path}")
        out_path, count, header = convert(csv_path, args.out_dir)
        print(f"[ok] {csv_path.name} -> {out_path}  ({count} 条，列：{', '.join(header)})")
    return 0


if __name__ == "__main__":
    sys.exit(main())
