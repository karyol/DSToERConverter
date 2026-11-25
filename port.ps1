param(
  [Parameter(Mandatory)][string]$Ds3Dir,
  [Parameter(Mandatory)][int]$SrcId,
  [Parameter(Mandatory)][int]$DstId,
  [Parameter(Mandatory)][string]$ErDir,
  [Parameter(Mandatory)][string]$ModDir,
  [Parameter(Mandatory)][string]$Reg,
  [int]$DonorId = 2130
)
$ErrorActionPreference = 'Stop'
$kit = "$PSScriptRoot\PortKit\bin\Debug\net11.0\PortKit.exe"
$conv = "$PSScriptRoot\FlverConv\bin\Debug\net11.0\FlverConv.exe"
New-Item -ItemType Directory -Force "$ModDir\chr" | Out-Null
Copy-Item "$Reg" "$Reg.bak.$(Get-Date -f yyyyMMddHHmmss)" -Force

# 1. chrbnd DS3
& witchyBND "$Ds3Dir\chr\c$SrcId.chrbnd.dcx"
& witchyBND "$Ds3Dir\chr\c$SrcId.anibnd.dcx"

# 2. flver
& $conv "$Ds3Dir\chr\c$SrcId.chrbnd.dcx\c$SrcId.flver" "c$DstId.flver" "$PSScriptRoot\matmap.csv" --model $DstId
if ($LASTEXITCODE -ne 0) { throw "konwersja FLVER nie przeszła weryfikacji" }

# 3. materiały
& $kit matclone --matmap "$PSScriptRoot\matmap.csv" --er $ErDir --out $ModDir --model $DstId
if ($LASTEXITCODE -ne 0) { throw "klonowanie materiałów nieudane" }

# 4. parametry
& $kit paramport --src-reg "$Ds3Dir\regulation.bin" --dst-reg $Reg --out "$PSScriptRoot\regulation.ported.bin" --src $SrcId --dst $DstId --donor $DonorId
if ($LASTEXITCODE -ne 0) { throw "port parametrów nieudany" }

Write-Host "`n--- RĘCZNIE ---"
@"
1. spakuj c$DstId.flver + tpf do chr\c$DstId.chrbnd.dcx (WitchyBND)
2. skopiuj c$DonorId.skel -> c$DstId.skel, c$DonorId.hkx ragdoll analogicznie
3. behbnd: c$DonorId.behbnd.dcx jako szkielet, stan bossa do przebudowy
4. AI: Lua z DS3 (script\c$SrcId*) do przepisania na API ER
5. przejrzyj paramport-report.csv — każde TYLKO_ER i TYP_* do decyzji
6. regulation.ported.bin -> regulation.bin
7. spawn przez randomizer, test renderu, dopiero potem logika
"@ | Write-Host