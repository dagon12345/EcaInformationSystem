$ErrorActionPreference = "Stop"

$msdeploy = "C:\Program Files\IIS\Microsoft Web Deploy V3\msdeploy.exe"
$root = $PSScriptRoot

Write-Host "Publishing API..." -ForegroundColor Cyan
dotnet publish "$root\EcaInformationSystem.API\EcaInformationSystem.API.csproj" -c Release -o "$root\publish\api"

Write-Host "Deploying API to MonsterASP..." -ForegroundColor Cyan
& $msdeploy -verb:sync -source:contentPath="$root\publish\api" -dest:contentPath=site76298,ComputerName="https://site76298.siteasp.net:8172/msdeploy.axd?site=site76298",UserName=site76298,Password=REDACTED,AuthType=Basic -allowUntrusted

Write-Host "Publishing Client..." -ForegroundColor Cyan
dotnet publish "$root\EcaInformationSystem.Client\EcaInformationSystem.Client.csproj" -c Release -o "$root\publish\client"

Write-Host "Deploying Client to MonsterASP..." -ForegroundColor Cyan
& $msdeploy -verb:sync -source:contentPath="$root\publish\client" -dest:contentPath=site76299,ComputerName="https://site76299.siteasp.net:8172/msdeploy.axd?site=site76299",UserName=site76299,Password=REDACTED,AuthType=Basic -allowUntrusted

Write-Host "Done. Both API and Client deployed." -ForegroundColor Green