param(
    [string]$GameDir = "D:\GAME\Steam\steamapps\common\Probably Stolen",
    [switch]$Deploy = $true
)

$ErrorActionPreference = "Stop"

$ProjectDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ManagedDir = Join-Path $GameDir "Probably Stolen_Data\Managed"
$BuildDir = Join-Path $ProjectDir "build"
$OutDll = Join-Path $BuildDir "NightHeist.dll"
$Csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"

if (!(Test-Path $Csc)) {
    throw "C# compiler not found at: $Csc"
}
if (!(Test-Path $ManagedDir)) {
    throw "Game Managed directory not found at: $ManagedDir"
}

New-Item -ItemType Directory -Force -Path $BuildDir | Out-Null

$Sources = @(
    (Join-Path $ProjectDir "src\NightHeistMod.cs"),
    (Join-Path $ProjectDir "src\HeistLootRoller.cs"),
    (Join-Path $ProjectDir "src\NightHeistLocation.cs")
)

$Refs = @(
    "mscorlib.dll",
    "netstandard.dll",
    "System.dll",
    "System.Core.dll",
    "0Harmony.dll",
    "Assembly-CSharp.dll",
    "UnityEngine.dll",
    "UnityEngine.CoreModule.dll",
    "UnityEngine.UI.dll",
    "Unity.TextMeshPro.dll"
)

$ArgsList = @(
    "/nologo",
    "/noconfig",
    "/nostdlib+",
    "/target:library",
    "/optimize+",
    "/utf8output",
    "/out:$OutDll"
)

foreach ($ref in $Refs) {
    $refPath = Join-Path $ManagedDir $ref
    if (Test-Path $refPath) {
        $ArgsList += "/r:$refPath"
    } else {
        Write-Warning "Reference not found (skipping): $refPath"
    }
}

$ArgsList += $Sources

Write-Host "Compiling NightHeist.dll..."
& $Csc @ArgsList
if ($LASTEXITCODE -ne 0) {
    throw "Compilation failed with exit code $LASTEXITCODE"
}

Write-Host "Build succeeded: $OutDll"

if ($Deploy) {
    $DeployDir = Join-Path $GameDir "Mods\NightHeist"
    $LocSrc = Join-Path $ProjectDir "Localization"
    $LocDst = Join-Path $DeployDir "Localization"

    New-Item -ItemType Directory -Force -Path $DeployDir | Out-Null
    New-Item -ItemType Directory -Force -Path $LocDst | Out-Null

    Copy-Item -Force (Join-Path $ProjectDir "manifest.xml") (Join-Path $DeployDir "manifest.xml")
    Copy-Item -Force $OutDll (Join-Path $DeployDir "NightHeist.dll")
    Copy-Item -Force (Join-Path $LocSrc "*.csv") $LocDst

    $PreviewSrc = Join-Path $ProjectDir "preview.png"
    if (Test-Path $PreviewSrc) {
        Copy-Item -Force $PreviewSrc (Join-Path $DeployDir "preview.png")
    }

    $TexSrc = Join-Path $ProjectDir "Textures"
    if (Test-Path $TexSrc) {
        $TexDst = Join-Path $DeployDir "Textures"
        New-Item -ItemType Directory -Force -Path $TexDst | Out-Null
        Copy-Item -Force -Recurse (Join-Path $TexSrc "*") $TexDst
    }

    Write-Host "Deployed to: $DeployDir"
}
