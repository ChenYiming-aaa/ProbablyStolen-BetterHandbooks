# 编译并部署 更好的手册 到游戏 Mods 目录
# 用法：.\build.ps1
#      .\build.ps1 -GameDir "X:\path\to\Probably Stolen"

param(
    [string]$GameDir = "D:\SteamLibrary\steamapps\common\Probably Stolen"
)

$ErrorActionPreference = "Stop"
$here = Split-Path -Parent $MyInvocation.MyCommand.Path

dotnet build "$here\BetterHandbooks.csproj" -c Release "/p:GameDir=$GameDir"
if ($LASTEXITCODE -ne 0) { throw "build failed" }

$mods = Join-Path $GameDir "Mods\BetterHandbooks"
New-Item -ItemType Directory -Force -Path $mods | Out-Null

# 游戏在跑时 dll 会被内存映射锁住，替换必然失败。
# 用 File.Open 探测并不可靠——有时能独占打开但 Copy-Item 仍报"用户映射区域"。
# 所以直接尝试复制，再用哈希校验确认真的换掉了。
$dll = Join-Path $mods "BetterHandbooks.dll"
$srcDll = "$here\..\build\BetterHandbooks.dll"
$copied = $false
for ($i = 0; $i -lt 3; $i++) {
    try {
        Copy-Item $srcDll $dll -Force -ErrorAction Stop
        $copied = $true
        break
    } catch {
        Start-Sleep -Seconds 1
    }
}
# 分两步写：早期版本把 `not $copied` 和两次 Get-FileHash 塞在同一个 -or 里，
# 复制失败时右半边仍可能求值，报出一个看不懂的「Path 参数为空」。
if (-not $copied) {
    Write-Host ""
    Write-Host "!! BetterHandbooks.dll 替换失败——游戏还在运行。" -ForegroundColor Yellow
    Write-Host "!! 请完全退出「Probably Stolen」（结束进程，不是回主菜单）后重新运行本脚本。" -ForegroundColor Yellow
    exit 1
}
$srcHash = (Get-FileHash -LiteralPath $srcDll).Hash
$dstHash = (Get-FileHash -LiteralPath $dll).Hash
if ($srcHash -ne $dstHash) {
    Write-Host ""
    Write-Host "!! DLL 校验不一致，部署未完成。" -ForegroundColor Yellow
    exit 1
}

Copy-Item "$here\manifest.xml" $mods -Force

$loc = Join-Path $mods "Localization"
New-Item -ItemType Directory -Force -Path $loc | Out-Null
Copy-Item "$here\Localization\*.csv" $loc -Force

$tex = Join-Path $mods "Textures\Items"
New-Item -ItemType Directory -Force -Path $tex | Out-Null
Copy-Item "$here\Textures\Items\*.png" $tex -Force

Write-Host "deployed to $mods"
