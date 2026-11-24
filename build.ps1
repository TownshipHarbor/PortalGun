param([string]$PortalGun = '..\Portal-Gun')
$ErrorActionPreference = 'Stop'

$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$pack = Join-Path $here 'res\portalgun.pack'
$out  = Join-Path $here 'build'

if (-not (Test-Path (Join-Path $PortalGun 'Assets\PortalGun'))) { throw "clone https://github.com/calebv2/Portal-Gun and pass its folder with -PortalGun" }
New-Item -ItemType Directory -Force (Join-Path $here 'res') | Out-Null
& py -3 -I (Join-Path $here 'tools\bake.py') $PortalGun $pack
if ($LASTEXITCODE -ne 0) { throw 'bake failed' }

& dotnet build (Join-Path $here 'quest\PortalGunQuest.csproj') -c Release -o $out -nologo -v q
if ($LASTEXITCODE -ne 0) { throw 'build failed' }
$dll = Join-Path $out 'PortalGunQuest.dll'
Write-Host ("{0} {1}" -f $dll, (Get-FileHash $dll -Algorithm SHA256).Hash.Substring(0, 8).ToLower())
