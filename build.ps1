param([string]$OutputDirectory, [switch]$VideoModel = $true)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) { $OutputDirectory = $root }
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (!(Test-Path -LiteralPath $compiler)) { throw '需要 Windows .NET Framework 4.x 编译器。' }
$output = Join-Path $OutputDirectory 'gugugaga pet.exe'
$art = @(
  "/resource:$root\assets\penguin-rig.png,PenguinPet.Rig"
  "/resource:$root\assets\penguin-expressions.png,PenguinPet.Expressions"
)
if ($VideoModel) {
  $art += "/resource:$root\assets\guga-video-body.png,PenguinPet.VideoBody"
  $art += "/resource:$root\assets\guga-video-faces.png,PenguinPet.VideoFaces"
}
& $compiler /nologo /target:winexe /platform:x64 /optimize+ /utf8output "/win32manifest:$root\app.manifest" /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Runtime.Serialization.dll @art "/out:$output" "$root\src\PenguinPet.cs" "$root\src\DeskScene.cs" "$root\src\CompanionMotion.cs" "$root\src\Ui.cs"
if ($LASTEXITCODE -ne 0) { throw '编译失败' }
Copy-Item -LiteralPath $output -Destination (Join-Path $OutputDirectory 'PenguinPet.exe') -Force
Write-Host "已生成 $output"
