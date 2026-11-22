# 更好的手册 —— 发布打包脚本
#
# 用法（在 mod/BetterHandbooks 目录下）：
#   powershell -ExecutionPolicy Bypass -File tools\package.ps1
#
# 产物：mod\dist\BetterHandbooks\  —— 这个文件夹整份扔进 Steam 创意工坊上传器即可。
#
# 脚本做三件事：清空重建 dist、按清单拷贝、跑一遍发布前校验。
# 校验不过会以非零退出，避免把残缺包传上去。

$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $PSScriptRoot        # mod/BetterHandbooks
$root = Split-Path -Parent $here                # mod
$dist = Join-Path (Split-Path -Parent $root) 'dist\BetterHandbooks'
$dll  = Join-Path $root 'build\BetterHandbooks.dll'

Write-Host '== 1/4 编译 ==' -ForegroundColor Cyan
Push-Location $here
try {
    dotnet build BetterHandbooks.csproj -c Release "-p:GameDir=D:\SteamLibrary\steamapps\common\Probably Stolen" | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'dotnet build 失败' }
} finally {
    Pop-Location
}
if (-not (Test-Path $dll)) { throw "找不到构建产物：$dll" }

Write-Host '== 2/4 重建 dist ==' -ForegroundColor Cyan
if (Test-Path $dist) { Remove-Item $dist -Recurse -Force }
New-Item -ItemType Directory -Force -Path $dist, (Join-Path $dist 'Localization'), (Join-Path $dist 'Textures\Items') | Out-Null

Copy-Item $dll $dist
Copy-Item (Join-Path $here 'manifest.xml') $dist
# 文件名就是游戏的 Locale Code：中文是 zh（不是 zh-Hans），源目录里直接叫 zh.csv。
Copy-Item (Join-Path $here 'Localization\*.csv') (Join-Path $dist 'Localization')
Copy-Item (Join-Path $here 'Textures\Items\*.png') (Join-Path $dist 'Textures\Items')

# 上传目录只放游戏要读的东西：DLL / manifest / 本地化 / 贴图。
# README、CHANGELOG、LICENSE 只进手动安装包，不给工坊订阅者塞无关文件。

# 创意工坊素材：放在 dist 根目录（不在 mod 文件夹里），上传时单独选给 Steam。
$distRoot = Split-Path -Parent $dist
if (Test-Path (Join-Path $here 'workshop')) {
    Copy-Item (Join-Path $here 'workshop\*.txt') $distRoot -Force
}
if (Test-Path (Join-Path $here 'PUBLISH.md')) { Copy-Item (Join-Path $here 'PUBLISH.md') $distRoot -Force }

# steamcmd 用的 VDF。publishedfileid 若已存在则沿用，避免更新时又发一条新条目。
$vdfPath = Join-Path $distRoot 'workshop_build.vdf'
$publishedId = '0'
if (Test-Path $vdfPath) {
    $m = Select-String -Path $vdfPath -Pattern '"publishedfileid"\s+"(\d+)"'
    if ($m) { $publishedId = $m.Matches[0].Groups[1].Value }
}
$manifestXml = [xml](Get-Content (Join-Path $dist 'manifest.xml') -Raw -Encoding UTF8)
$vdfLines = @(
    '"workshopitem"',
    '{',
    "`t`"appid`"`t`t`"4348910`"",
    "`t`"publishedfileid`"`t`t`"$publishedId`"",
    "`t`"contentfolder`"`t`t`"$dist`"",
    "`t`"previewfile`"`t`t`"$(Join-Path $distRoot 'workshop_preview.png')`"",
    "`t`"descriptionfile`"`t`t`"$(Join-Path $distRoot 'description.zh.txt')`"",
    "`t`"visibility`"`t`t`"0`"",
    "`t`"title`"`t`t`"更好的手册 / Better Handbooks`"",
    "`t`"changenote`"`t`t`"$($manifestXml.Mod.ModVersion)`"",
    '}'
)
[System.IO.File]::WriteAllText($vdfPath, ($vdfLines -join "`r`n"), (New-Object System.Text.UTF8Encoding($false)))

Write-Host '== 3/4 校验 ==' -ForegroundColor Cyan
$fail = @()

# manifest 结构
$manifestPath = Join-Path $dist 'manifest.xml'
try { $xml = [xml](Get-Content $manifestPath -Raw -Encoding UTF8) } catch { $fail += "manifest.xml 不是合法 XML：$($_.Exception.Message)" }
if ($xml) {
    foreach ($tag in 'ID', 'Name', 'Author', 'ModVersion', 'Description') {
        if (-not $xml.Mod.$tag) { $fail += "manifest 缺少 <$tag>" }
    }
    if (-not $xml.Mod.SelectSingleNode('Assemblies')) { $fail += 'manifest 缺少 <Assemblies>' }
    if (-not $xml.Mod.SelectSingleNode('Capabilities')) {
        $fail += 'manifest 缺少 <Capabilities>（即使是空也要写 <Capabilities/>）'
    } else {
        $caps = $xml.Mod.SelectSingleNode('Capabilities').SelectNodes('Capability')
        if ($caps.Count -gt 0) {
            $declared = @($caps | ForEach-Object { $_.InnerText.Trim() })
            Write-Host ("   声明能力：" + ($declared -join ', ') + " —— 请确认与游戏日志 [ModLoader] audit 的 caps= 一致") -ForegroundColor Yellow
        } else {
            Write-Host '   声明能力：none（与加载器扫描结果 caps=none 一致时才是对的）' -ForegroundColor DarkGray
        }
    }
    $asm = @($xml.Mod.Assemblies.Assembly)
    foreach ($a in $asm) {
        if (-not (Test-Path (Join-Path $dist $a))) { $fail += "manifest 列出的程序集不存在：$a" }
    }
    # 未列出的 DLL 会被加载器当成可疑文件
    Get-ChildItem $dist -Recurse -Filter *.dll | ForEach-Object {
        if ($asm -notcontains $_.Name) { $fail += "包内有未在 manifest 声明的 DLL：$($_.Name)" }
    }
    # 版本一致性：manifest 与程序集版本
    $asmVersion = [System.Reflection.AssemblyName]::GetAssemblyName((Join-Path $dist $asm[0])).Version.ToString()
    if (-not $asmVersion.StartsWith($xml.Mod.ModVersion)) {
        $fail += "manifest 版本 $($xml.Mod.ModVersion) 与程序集版本 $asmVersion 不一致"
    }
}

# 本地化：无 BOM + 键齐全
# 册子有 6 句赠送台词；招牌箱只在博士柜台上卖，不需要对话；
# 回执手册按举报成绩分三套（g/m/b），每套 5 句，g 和 m 各多一句"没抓到人"的变体。
$books = @('ins', 'lot', 'prod', 'phn')
$shopItems = @('sgn')
$receiptVariants = @('rcp_g', 'rcp_m', 'rcp_b')
$required = @()
foreach ($b in $books) {
    $required += "${b}_name"; $required += "${b}_desc"
    1..6 | ForEach-Object { $required += "${b}_d$_" }
}
foreach ($b in $shopItems) {
    $required += "${b}_name"; $required += "${b}_desc"
}
$required += 'rcp_name'; $required += 'rcp_desc'
foreach ($v in $receiptVariants) {
    1..5 | ForEach-Object { $required += "${v}_d$_" }
}
$required += 'rcp_g_d2_zero'; $required += 'rcp_m_d2_zero'
foreach ($csv in Get-ChildItem (Join-Path $dist 'Localization') -Filter *.csv) {
    $bytes = [System.IO.File]::ReadAllBytes($csv.FullName)
    if ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF) {
        $fail += "$($csv.Name) 带 UTF-8 BOM，游戏会把首列键名读错"
    }
    $keys = (Get-Content $csv.FullName -Encoding UTF8 | Select-Object -Skip 1) | ForEach-Object { ($_ -split ',')[0].Trim() }
    $missing = $required | Where-Object { $keys -notcontains $_ }
    if ($missing) { $fail += "$($csv.Name) 缺少键：$($missing -join ', ')" }
}

# 贴图：必须是 16 的整数倍（物体形状按 16px 块采样）
Add-Type -AssemblyName System.Drawing
foreach ($png in Get-ChildItem (Join-Path $dist 'Textures\Items') -Filter *.png) {
    $img = [System.Drawing.Image]::FromFile($png.FullName)
    try {
        if ($img.Width % 16 -ne 0 -or $img.Height % 16 -ne 0) {
            $fail += "$($png.Name) 尺寸 $($img.Width)x$($img.Height) 不是 16 的整数倍"
        }
    } finally { $img.Dispose() }
}

Write-Host '== 4/4 结果 ==' -ForegroundColor Cyan
$files = Get-ChildItem $dist -Recurse -File
$sum = ($files | Measure-Object Length -Sum).Sum
$files | Sort-Object FullName | ForEach-Object {
    $rel = $_.FullName.Substring($dist.Length + 1)
    Write-Host ("   {0,-34} {1,8:N0} B" -f $rel, $_.Length)
}
Write-Host ("   合计 {0} 个文件，{1:N1} KB" -f $files.Count, ($sum / 1KB))

if ($fail.Count -gt 0) {
    Write-Host ''
    Write-Host '校验未通过：' -ForegroundColor Red
    $fail | ForEach-Object { Write-Host " - $_" -ForegroundColor Red }
    exit 1
}

# 顺手打一个手动安装包：给不用创意工坊的人（解压到 Mods\ 即可）。
# 说明类文件只加进这个包，不混进上传目录。
$stage = Join-Path $distRoot '_zipstage'
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item -ItemType Directory -Force -Path $stage | Out-Null
Copy-Item $dist (Join-Path $stage 'BetterHandbooks') -Recurse -Force
foreach ($doc in 'README.md', 'CHANGELOG.md', 'LICENSE.md') {
    $src = Join-Path $here $doc
    if (Test-Path $src) { Copy-Item $src (Join-Path $stage ('BetterHandbooks\' + $doc)) -Force }
}
$zip = Join-Path $distRoot ("BetterHandbooks-{0}.zip" -f $manifestXml.Mod.ModVersion)
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path (Join-Path $stage 'BetterHandbooks') -DestinationPath $zip -CompressionLevel Optimal
Remove-Item $stage -Recurse -Force
Write-Host ''
Write-Host ("手动安装包：$zip（{0:N1} KB）" -f ((Get-Item $zip).Length / 1KB)) -ForegroundColor DarkGray
Write-Host "上传目录：$dist" -ForegroundColor Green
