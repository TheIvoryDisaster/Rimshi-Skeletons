# Builds RimshiSkeletons.dll into 1.6\Assemblies. Run from anywhere.
$proj = Join-Path $PSScriptRoot 'Source\RimshiSkeletons\RimshiSkeletons.csproj'
dotnet build $proj -c Release
if ($LASTEXITCODE -eq 0) { Write-Host "Built: $(Join-Path $PSScriptRoot '1.6\Assemblies\RimshiSkeletons.dll')" }
