param([ValidateSet('Debug','Release')][string]$Config = 'Release')
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$bin  = Join-Path $root 'bin'
$lib  = Join-Path $root 'lib'

if (-not (Test-Path (Join-Path $lib 'SoulsFormats.dll'))) {
  throw "brak lib\SoulsFormats.dll"
}

if (Test-Path $bin) { Remove-Item $bin -Recurse -Force }
New-Item -ItemType Directory -Force $bin | Out-Null

$order = @(
  'src\FlverConv\FlverConv.csproj',
  'src\PortKit\PortKit.csproj',
  'src\FlverDump\FlverDump.csproj',
  'src\ApiProbe\ApiProbe.csproj',
  'src\PortGui\PortGui.csproj'
)

foreach ($rel in $order) {
  $proj = Join-Path $root $rel
  if (-not (Test-Path $proj)) { throw "brak $rel" }
  Write-Host "=== $rel ==="
  dotnet build $proj -c $Config --nologo -v minimal
  if ($LASTEXITCODE -ne 0) { throw "budowa $rel nieudana" }
}

foreach ($exe in 'FlverConv.exe','PortKit.exe','PortGui.exe') {
  if (-not (Test-Path (Join-Path $bin $exe))) { throw "po budowie brak $exe w bin" }
}

& (Join-Path $bin 'PortKit.exe') check
if ($LASTEXITCODE -ne 0) { throw "PortKit nie startuje" }

& (Join-Path $bin 'FlverConv.exe')
if ($LASTEXITCODE -ne 2) { throw "FlverConv nie startuje poprawnie" }

$oodle = Get-ChildItem -Filter 'oo2core_*_win64.dll' -ErrorAction SilentlyContinue
if (-not $oodle) {
  Write-Warning "brak oo2core_*_win64.dll w katalogu glownym projektu - pliki .dcx z Oodle sie nie odczytaja"
}

Write-Host ""
Write-Host "GOTOWE: $bin" -ForegroundColor Green
Get-ChildItem $bin -Filter *.exe | Select-Object Name, Length | Format-Table