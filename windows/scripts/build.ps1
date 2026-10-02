[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
dotnet restore "$root/EasyNoty.Windows.slnx" -p:Platform=x64
dotnet build "$root/EasyNoty.Windows.slnx" -c Release -p:Platform=x64 --no-restore
dotnet publish "$root/src/EasyNoty.Setup/EasyNoty.Setup.csproj" -c Release -p:Platform=x64 --no-restore
