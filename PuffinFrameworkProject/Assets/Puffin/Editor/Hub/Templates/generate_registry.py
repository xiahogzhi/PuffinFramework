#!/usr/bin/env python3
"""
自动生成 registry.json 的脚本
放置在 GitHub 仓库的 scripts/ 目录下
通过 GitHub Actions 在 manifest.json 更新时自动运行
"""

import json
import os
from pathlib import Path

def generate_registry():
    modules_dir = Path("modules")
    registry = {
        "name": "Puffin Official",
        "version": "1.0.0",
        "modules": {}
    }

    if not modules_dir.exists():
        print("modules/ directory not found")
        return

    for module_dir in modules_dir.iterdir():
        if not module_dir.is_dir():
            continue

        module_id = module_dir.name
        versions = []
        latest = None

        for version_dir in sorted(module_dir.iterdir()):
            if not version_dir.is_dir():
                continue

            manifest_path = version_dir / "manifest.json"
            if manifest_path.exists():
                versions.append(version_dir.name)
                latest = version_dir.name

        if versions:
            registry["modules"][module_id] = {
                "latest": latest,
                "versions": versions
            }

    with open("registry.json", "w", encoding="utf-8") as f:
        json.dump(registry, f, indent=2, ensure_ascii=False)

    print(f"Generated registry.json with {len(registry['modules'])} modules")

if __name__ == "__main__":
    generate_registry()
