# MemoryScanner（内存扫描）

用于 **《帝国时代 4》（Age of Empires IV）炼狱模式** 下查找并修改 **天赋点（perk）** 的轻量内存扫描/写入工具。

## 为什么做这个

《帝国时代 4》对 **Cheat Engine（CE）** 有较明显的检测（进程名、窗口类名、驱动/行为指纹等）。本工具尽量用独立实现做进程内存读写与数值扫描，避免直接依赖 CE，降低被识别的概率。

> **说明**：任何修改游戏内存的行为都可能违反用户协议或触发反作弊。请仅在单机/自定义等你能接受风险的场景下使用，后果自负。本项目与 Microsoft / Relic 无关。

## 功能（概览）

- 附加到游戏进程，按类型与数值扫描内存
- 定位炼狱模式相关 perk 数值并写入
- 不捆绑 CE，界面与实现与 CE 指纹分离

具体用法、快捷键与地址说明见仓库内源码与注释（上传后补充）。

## 环境要求

- Windows（与 AoE4 客户端同机运行）
- 以**管理员身份**运行（读写其他进程内存通常需要）
- 游戏版本更新后，指针/偏移可能失效，需重新扫描

## 从本机首次上传到 GitHub

若你的项目在 `桌面\内存扫描`，且远程仓库为 [RuojunYang/MemoryScanner](https://github.com/RuojunYang/MemoryScanner)：

1. 把整个项目文件夹复制/重命名为你习惯的名字（路径不要有奇怪权限问题）。
2. 在文件夹内打开 PowerShell，执行：

```powershell
.\scripts\upload-to-github.ps1
```

或手动：

```powershell
cd "C:\Users\user\Desktop\内存扫描"
git init
git remote add origin https://github.com/RuojunYang/MemoryScanner.git
git add -A
git commit -m "Initial commit: AoE4 inferno perk memory scanner"
git branch -M main
git pull origin main --allow-unrelated-histories
git push -u origin main
```

若 `origin` 已存在，改用：

```powershell
git remote set-url origin https://github.com/RuojunYang/MemoryScanner.git
```

## 构建与运行

上传源码后在此补充（例如 Visual Studio 解决方案路径、`dotnet build` 或 CMake 命令）。

## 许可证

未指定许可证前，默认保留所有权利。若你希望开源，可在仓库根目录添加 `LICENSE` 文件。

## 相关仓库

- [Nioh3-Cheat-Table-analysis](https://github.com/RuojunYang/Nioh3-Cheat-Table-analysis) — 另一款游戏的 CE 表分析笔记
