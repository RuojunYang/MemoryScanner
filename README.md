# MemoryScanner（内存扫描）

Windows 上的轻量 **WPF 内存扫描/写入** 工具。最初是为了在 **《帝国时代 4》炼狱模式** 里调整 **天赋点（perk）** 数值：游戏对 **Cheat Engine（CE）** 有较明显的指纹/行为检测，本工具用独立进程读写内存，不依赖 CE。

> 修改游戏内存可能违反用户协议或触发反作弊，请仅在你能接受风险的单机/自定义场景使用，后果自负。与 Microsoft / Relic 无关。

## 功能

- 选择进程、精确整数 **首次扫描 / 再次扫描**
- 1 / 2 / 4 / 8 字节，有符号 / 无符号，小端精确匹配
- 已提交且可读的内存区域；后台扫描、进度与取消
- 写入后 **回读校验**

不包含：未知初值、变大/变小、浮点、指针链、调试器、驱动或反作弊绕过。结果列表最多显示 10,000 条；再次扫描仍基于完整候选集（单次扫描最多保留 10,000,000 个匹配）。

### 帝国时代 4 炼狱 perk（示例流程）

1. **以管理员身份** 启动 `MemoryScanner.exe`，启动游戏并进入可查看/修改 perk 点的界面。
2. 在列表中找到游戏进程（如 `RelicCardinal.exe` 等，以任务管理器为准），点击 **打开进程**。
3. 选择 **4 字节**（或你确认过的类型），输入当前 perk 点数，**首次扫描**。
4. 在游戏里改变点数（升级/消耗等），输入新数值，**再次扫描**，重复直到候选很少。
5. 选中地址，输入目标值，**写入并校验**；建议先备份存档。

游戏更新后地址/指针可能失效，需重新扫描。

## 下载（Release）

在 GitHub **[Releases](https://github.com/RuojunYang/MemoryScanner/releases)** 页面下载最新 `MemoryScanner-win-x64.zip`（见下方「发布新版本」生成）。

解压后运行 `MemoryScanner.exe`。需要已安装 **[.NET 8 Desktop Runtime（x64）](https://dotnet.microsoft.com/download/dotnet/8.0)**（与 `RollForward=Major` 兼容更高主版本）。若无法打开目标进程，请 **右键 → 以管理员身份运行**。

## 从源码运行

需要 Windows x64 与 .NET 8 SDK（运行仅需 Desktop Runtime）。

```powershell
dotnet run --project .\src\MemoryScanner\MemoryScanner.csproj -c Release
```

1. 选择目标进程 → **打开进程**
2. 选类型、输入当前值 → **首次扫描**
3. 改游戏内数值、输入新值 → **再次扫描**
4. 选中结果 → **写入并校验**

## 构建与测试

```powershell
dotnet build .\内存扫描.sln -c Release
dotnet test .\tests\MemoryScanner.Tests\MemoryScanner.Tests.csproj -c Release
```

本地 `dotnet build -c Release` 可得到 `src\MemoryScanner\bin\Release\net8.0-windows\MemoryScanner.exe`；对外发布请用下方脚本或 CI，生成 zip。勿将 `bin/`、`obj/`、`dist/` 提交到 Git。

## 发布新版本（GitHub Release）

### 方式一：打 tag，自动构建（推荐）

仓库已配置 [`.github/workflows/release.yml`](.github/workflows/release.yml)。在仓库根目录：

```powershell
git tag v1.0.0
git push origin v1.0.0
```

GitHub Actions 会在 Windows 上 `publish`，并把 zip 挂到同名 Release。之后可在 Releases 页编辑说明。

### 方式二：本机打包后手动上传

```powershell
.\scripts\publish-release.ps1
# 生成 dist\MemoryScanner-win-x64.zip
```

然后打开 **GitHub → Releases → Draft a new release**，选择 tag（如 `v1.0.0`），把 zip 拖到 **Assets** 并发布。

也可使用 GitHub CLI：

```powershell
gh release create v1.0.0 dist\MemoryScanner-win-x64.zip --title "v1.0.0" --notes "首个可用构建"
```

## 使用边界

仅扫描和修改你拥有或明确获准测试的进程。程序只使用标准 Windows 用户态 API，不尝试规避操作系统权限、反作弊或受保护进程机制。
