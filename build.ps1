# Builds RimshiSkeletons.dll into <version>\Assemblies for each game version this install can compile against. Run from anywhere.
#   .\build.ps1            every version whose game files are present (1.6 needs RimWorldWin64_Data\Managed, 1.7 the Waylith game folder)
#   .\build.ps1 1.7        just one version
param([string[]]$Versions = @('1.6', '1.7'))
$proj = Join-Path $PSScriptRoot 'Source\RimshiSkeletons\RimshiSkeletons.csproj'
$game = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$failed = $false
foreach ($v in $Versions) {
    $present = if ($v -eq '1.7') { Test-Path (Join-Path $game 'Waylith.dll') } else { Test-Path (Join-Path $game 'RimWorldWin64_Data\Managed\Assembly-CSharp.dll') }
    if (-not $present) { Write-Host "Skipped ${v}: this RimWorld install is not on $v."; continue }
    dotnet build $proj -c Release "-p:RimWorldVersion=$v"
    if ($LASTEXITCODE -eq 0) { Write-Host "Built: $(Join-Path $PSScriptRoot "$v\Assemblies\RimshiSkeletons.dll")" } else { $failed = $true }
}
if ($failed) { exit 1 }
