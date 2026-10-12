$ErrorActionPreference='Stop'
Set-Location $PSScriptRoot
New-Item package -ItemType Directory -Force | Out-Null
Copy-Item assets/* package -Force
node tools/generate-localization.cjs
if ($LASTEXITCODE) { throw 'Localization generation failed' }
npm --prefix ui run build
if ($LASTEXITCODE) { throw 'UI build failed' }
dotnet build src/RoadStyleIntoKorea.csproj -c Release --nologo
if ($LASTEXITCODE) { throw 'SDK build failed' }
$binary=Join-Path $PSScriptRoot 'src/bin/Release/net48'
dotnet build tools/LocaleExport -c Release --nologo
if ($LASTEXITCODE) { throw 'Locale exporter build failed' }
& ./tools/LocaleExport/bin/Release/net48/LocaleExport.exe $binary $env:CSII_MANAGEDPATH (Join-Path $PSScriptRoot 'lang')
if ($LASTEXITCODE) { throw 'Locale export failed' }
node tools/verify-localization.cjs
if ($LASTEXITCODE) { throw 'Translation verification failed' }
Copy-Item "$binary/RoadStyleIntoKorea.dll","$binary/RoadStyleIntoKorea_win_x86_64.dll" package -Force
Copy-Item lang package -Recurse -Force
Write-Output 'Build complete: package/'
