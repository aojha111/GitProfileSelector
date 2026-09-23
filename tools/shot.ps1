param([string]$Out, [string]$Theme = 'dark', [int]$Width = 0, [int]$Height = 0)
$repo = Split-Path -Parent $PSScriptRoot
$exe = Join-Path $repo 'src\GitProfile.App\bin\Debug\net10.0-windows\GitProfile.exe'
$env:GITPROFILE_THEME = $Theme
Start-Process -FilePath $exe -WorkingDirectory $repo
Start-Sleep -Seconds 5
& (Join-Path $PSScriptRoot 'capture.ps1') -Out (Join-Path $repo $Out) -Width $Width -Height $Height
Get-Process GitProfile -ErrorAction SilentlyContinue | Stop-Process -Force
Remove-Item Env:GITPROFILE_THEME
