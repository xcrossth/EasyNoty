$ErrorActionPreference = 'Stop'
$scriptPath = Join-Path $PSScriptRoot 'verify.py'
python $scriptPath
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}
