[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
dotnet test "$root/tests/EasyNoty.Tests/EasyNoty.Tests.csproj" -c Release -p:Platform=x64
