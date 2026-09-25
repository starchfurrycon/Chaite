# 用 PowerShell 5.1 编辑源码把 Runtime.cs 的 30 处中文串写坏了（2026-09-22）

## 0. 一句话

我在 `pwsh` 工具里用 `Get-Content -Raw` + `-replace` + `Set-Content` 改
`src/Chaite.Plugin/Runtime.cs`，把 30 处中文字符串的**结尾引号**抹掉了。文件
变成非法 UTF-8，`read` 工具直接拒绝读取。已完整修复（795 个测试全过），但
这个坑必须写下来：**在这台机器上，那组 cmdlet 不能用来改源码。**

## 1. 我做了什么

为了删掉三处 `_weaponIssueCooldown`，我用了一条"省事"的命令：

```powershell
$c = Get-Content $f -Raw
$c = $c -replace '(?m)^\s*private static int _weaponIssueCooldown;\r?\n',''
...
Set-Content $f -Value $c -NoNewline
```

命令本身语法没问题，退出码 0，也没报任何错。改完之后 `Select-String` 打出
来的中文是乱码，我一度以为那只是控制台编码问题——**不是**。

## 2. 真正的机制（用字节数组复现过）

`powershell.exe` 是 **5.1.26100**。在 5.1 里 `Get-Content` 和 `Set-Content`
的默认编码都是 `Default`，也就是**系统 ANSI 代码页**（这台机器是 GBK/936）。
所以那次往返实际是：

```
原始 UTF-8 字节  --GBK 解码-->  字符串  --GBK 编码-->  新字节
```

GBK 往返只在"这段 UTF-8 字节流恰好也是合法 GBK"时才无损。它**恰好**在一种
情况下有损：**一个 3 字节 UTF-8 汉字后面紧跟一个小于 0x40 的字节**（空格
`0x20`、引号 `0x22`、逗号 `0x2C`、右括号 `0x29`、加号 `0x2B` 都算）。这时
`<汉字第3字节><后一个字节>` 构成非法 GBK 序列，.NET 解码器把**这两个字节一起**
换成一个 `?`。

我用纯字节数组复现了（`artifacts/_rt-report.txt`，避开了脚本文件自身的编码干扰）：

| 原始字节 | 往返后 |
|---|---|
| `41 e38082 22 2c 42` (`A。" ,B`) | `41 e380 3f 2c 42` |
| `41 efbc88 22 42` (`A（"B`) | `41 efbc 3f 42` |
| `41 efbc89 22 2c 42` (`A）",B`) | `41 efbc 3f 2c 42` |
| `41 efbc9a 22 2c 42` (`A：",B`) | `41 efbc 3f 2c 42` |
| `41 e8af95 42` (`A试B`) | `41 e8af95 42`（**无损**） |

最后一行是关键：只有后面跟的字节 < 0x40 才会出事。`该` 后面跟空格也一样会
中招（`e8afa5 20` → `e8af 3f`），这正是第 720 行丢的不是引号而是"该 "的原因。

因为汉字的**前两个字节会留下来**，每个受损点都能被定位和还原。

## 3. 为什么我第一次测试没测出来

我先写了个 `.ps1` 来复现，结果 `powershell.exe -File` 把**脚本文件本身**按
ANSI 读了，字符串字面量在进入测试前就已经坏了——测试自己被同一个 bug 污染。
只有改用纯字节数组（命令里不出现任何中文）才拿到干净结论。

**推论：在这台机器上，任何含中文的 `.ps1` 都不能用 `powershell.exe -File` 跑。**

## 4. 修复

`artifacts/_repair-runtime.py`。做法：

1. 先备份受损文件到 `Runtime.cs.damaged-backup`。
2. 对 30 个受损点逐个做**带上下文的字节替换**，每个锚点都断言"恰好匹配 1 次"
   （有 3 组锚点前缀相同，比如 `）"。", AudioCue` 出现在 348/507/707 三行，
   必须用更长的前缀区分）。
3. 还原用的文案**逐行对照 `git show HEAD:src/Chaite.Plugin/Runtime.cs`**，不靠猜。
   这一步救了两处：
   - 第 720 行不是 `未通过"`，而是 `未通过该 Boss`（丢的是 `该`+空格）；
   - 第 1033 行不是 `固定公式：`，而是 `固定公式（`。
4. 写回后断言：合法 UTF-8、`U+FFFD` 计数为 0。

结果：61,842 字节 → 61,872 字节，合法 UTF-8，`tools\build.ps1` 退出码 0，
测试 **795 通过 / 0 失败**。

`artifacts/_repair-verify.txt` 里逐段打印了修复后的原文，可以肉眼核对。

## 5. 规则（加进铁律）

1. **不要用 `Get-Content -Raw` + `Set-Content` 改任何含非 ASCII 的源文件。**
   用 `edit` / `write` 工具，或者 `python`（显式 `encoding='utf-8'`）。
2. 万不得已要用 PowerShell 写文件，必须显式指定：
   `Get-Content -Raw -Encoding UTF8` 且
   `Set-Content -Encoding UTF8`（或 `[System.IO.File]::WriteAllText` + 
   `New-Object System.Text.UTF8Encoding $false`）。
3. **含中文的 `.ps1` 不要用 `powershell.exe -File` 跑**；改用 `pwsh`（7+）或把
   中文字面量换成字节/码点。
4. 用 PowerShell 跑纯 ASCII 命令（比如 hex dump、`git show`）是安全的——这次
   出事的只有"读-改-写同一个含中文的文件"。

## 6. 顺带确认的事

- `git show HEAD:...` 的输出重定向 `>` 在 5.1 里会写成 **UTF-16LE**（BOM `ff fe`），
  Python 按 UTF-8 读会失败。用 `subprocess` 直接取 stdout，或 `Out-File -Encoding utf8`。
- 受损文件保留在 `src/Chaite.Plugin/Runtime.cs.damaged-backup`，可用于复核这次
  修复确实是对着真实损坏做的，而不是我重写了一遍。
