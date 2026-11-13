param([string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) { $OutputDirectory = $root }
& (Join-Path $root 'build.ps1') -OutputDirectory $OutputDirectory
$failure = Join-Path $OutputDirectory 'test-failure.txt'
$results = Join-Path $OutputDirectory 'test-output\results.txt'
if (Test-Path -LiteralPath $failure) { Remove-Item -LiteralPath $failure }
if (Test-Path -LiteralPath $results) { Remove-Item -LiteralPath $results }
$process = Start-Process -FilePath (Join-Path $OutputDirectory 'gugugaga pet.exe') -ArgumentList '--self-test' -WindowStyle Hidden -PassThru
if (!$process.WaitForExit(20000)) { $process.Kill(); throw 'Self-test timed out after 20 seconds.' }
if (Test-Path -LiteralPath $failure) { throw (Get-Content -LiteralPath $failure -Raw) }
if (!(Test-Path -LiteralPath $results)) { throw 'Self-test did not create a report.' }
if ($process.ExitCode -ne 0) { throw "Self-test exited with code $($process.ExitCode)" }
Get-Content -LiteralPath $results
