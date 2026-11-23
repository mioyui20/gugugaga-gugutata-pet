param([switch]$SkipBuild,[string]$ExecutablePath)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
if (!$SkipBuild) { & (Join-Path $root 'build.ps1') }
if ([string]::IsNullOrWhiteSpace($ExecutablePath)) { $ExecutablePath = Join-Path $root 'gugugaga pet.exe' }
$dist = Join-Path $root 'dist'
$source = Join-Path $dist 'gugugaga-pet-source'
$portable = Join-Path $dist 'gugugaga-pet-Windows-x64'
foreach ($folder in @($dist,$source,$portable)) { New-Item -ItemType Directory -Path $folder -Force | Out-Null }
foreach ($folder in @('src','assets','docs')) { New-Item -ItemType Directory -Path (Join-Path $source $folder) -Force | Out-Null }
foreach ($file in @('.gitignore','README.md','README.zh-CN.md','app.manifest','build.ps1','test.ps1','package.ps1')) { Copy-Item -LiteralPath (Join-Path $root $file) -Destination $source -Force }
Get-ChildItem -LiteralPath (Join-Path $root 'src') -Filter '*.cs' | Copy-Item -Destination (Join-Path $source 'src') -Force
Copy-Item -LiteralPath (Join-Path $root '.github') -Destination $source -Recurse -Force
foreach ($file in @('penguin-rig.png','penguin-expressions.png','rig-generation.txt','expression-generation.txt','guga-video-body.png','guga-video-faces.png','guga-video-generation.txt')) { Copy-Item -LiteralPath (Join-Path $root "assets\$file") -Destination (Join-Path $source 'assets') -Force }
Get-ChildItem -LiteralPath (Join-Path $root 'docs') -Filter '*.png' | Copy-Item -Destination (Join-Path $source 'docs') -Force
Copy-Item -LiteralPath $ExecutablePath -Destination (Join-Path $portable 'gugugaga pet.exe') -Force
foreach ($file in @('README.md','README.zh-CN.md')) { Copy-Item -LiteralPath (Join-Path $root $file) -Destination $portable -Force }
Copy-Item -LiteralPath (Join-Path $source 'docs') -Destination $portable -Recurse -Force
$sourceZip = Join-Path $dist 'gugugaga-pet-source.zip'
$portableZip = Join-Path $dist 'gugugaga-pet-Windows-x64.zip'
Compress-Archive -LiteralPath $source -DestinationPath $sourceZip -Force
Compress-Archive -LiteralPath $portable -DestinationPath $portableZip -Force
$lines = @($sourceZip,$portableZip) | ForEach-Object { $hash = Get-FileHash -LiteralPath $_ -Algorithm SHA256; '{0}  {1}' -f $hash.Hash.ToLowerInvariant(),[IO.Path]::GetFileName($_) }
$lines | Set-Content -LiteralPath (Join-Path $dist 'SHA256SUMS.txt') -Encoding ascii
Write-Host "Packages ready: $dist"
