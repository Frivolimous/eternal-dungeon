# Exports the Windows and Linux builds into builds/.
#   powershell -ExecutionPolicy Bypass -File tools/build.ps1          (release)
#   powershell -ExecutionPolicy Bypass -File tools/build.ps1 -Debug   (debug build, with console logging)
# Set $env:GODOT to use a Godot other than the pinned one.
param([switch]$Debug)
$ErrorActionPreference = 'Stop'

$root = Split-Path $PSScriptRoot -Parent
$godot = if ($env:GODOT) { $env:GODOT } else {
    'C:\Program Files (x86)\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe'
}
if (-not (Test-Path $godot)) { throw "Godot not found at $godot. Set `$env:GODOT to the console exe." }

$templates = Join-Path $env:APPDATA 'Godot\export_templates\4.7.2.stable.mono'
if (-not (Test-Path $templates)) {
    throw "Export templates for 4.7.2 .NET are missing ($templates). In the Godot editor: Editor > Manage Export Templates > Download and Install."
}

$mode = if ($Debug) { '--export-debug' } else { '--export-release' }
$builds = Join-Path $root 'builds'
$targets = @(
    @{ Preset = 'Windows Desktop'; Out = 'windows\EternalDungeon.exe' },
    @{ Preset = 'Linux'; Out = 'linux\EternalDungeon.x86_64' }
)

foreach ($t in $targets) {
    $out = Join-Path $builds $t.Out
    $dir = Split-Path $out -Parent
    if (Test-Path $dir) { Remove-Item $dir -Recurse -Force }
    New-Item -ItemType Directory $dir | Out-Null

    Write-Host "== $($t.Preset) -> $out"
    & $godot --headless --path (Join-Path $root 'game') $mode $t.Preset $out
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path $out)) { throw "$($t.Preset) export failed." }
}
Write-Host "Builds are in $builds"
