[CmdletBinding()]
param(
    [switch]$SkipProtocol,
    [switch]$SkipWindows,
    [switch]$SkipAndroid
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

function Invoke-CheckedStep {
    param(
        [Parameter(Mandatory)] [string]$Name,
        [Parameter(Mandatory)] [scriptblock]$Action
    )

    Write-Host "`n== $Name ==" -ForegroundColor Cyan
    & $Action
    if ($LASTEXITCODE -ne 0) {
        throw "$Name failed with exit code $LASTEXITCODE"
    }
}

Push-Location $root
try {
    if (-not $SkipProtocol) {
        $protocolVerifier = Join-Path $root 'protocol\scripts\verify.py'
        if (-not (Test-Path -LiteralPath $protocolVerifier)) {
            throw "Protocol verifier not found: $protocolVerifier"
        }
        Invoke-CheckedStep 'Protocol v1 contract verification' {
            python $protocolVerifier
        }
    }

    if (-not $SkipWindows) {
        $solution = Join-Path $root 'windows\EasyNoty.Windows.slnx'
        if (-not (Test-Path -LiteralPath $solution)) {
            throw "Windows solution not found: $solution"
        }
        Invoke-CheckedStep 'Windows restore' {
            dotnet restore $solution
        }
        Invoke-CheckedStep 'Windows release build' {
            dotnet build $solution -c Release --no-restore
        }
        Invoke-CheckedStep 'Windows tests' {
            dotnet test $solution -c Release --no-build
        }
    }

    if (-not $SkipAndroid) {
        $gradleWrapper = Join-Path $root 'android\gradlew.bat'
        $androidRoot = Join-Path $root 'android'
        if (-not (Test-Path -LiteralPath $gradleWrapper)) {
            throw "Android Gradle wrapper not found: $gradleWrapper"
        }

        $javac = Get-Command javac -ErrorAction SilentlyContinue
        $androidJdks = Join-Path $env:ProgramFiles 'Android\openjdk'
        $jdkCandidates = @(
            $env:JAVA_HOME
            if ($javac) { Split-Path -Parent (Split-Path -Parent $javac.Source) }
            (Join-Path $env:ProgramFiles 'Android\Android Studio\jbr')
            if (Test-Path -LiteralPath $androidJdks) {
                Get-ChildItem -LiteralPath $androidJdks -Directory | Sort-Object Name -Descending | ForEach-Object FullName
            }
        ) | Where-Object { $_ -and (Test-Path -LiteralPath (Join-Path $_ 'bin\javac.exe')) } | Where-Object {
            $javaVersion = & (Join-Path $_ 'bin\java.exe') -version 2>&1 | Out-String
            $javaVersion -match 'version "(?:1\.)?(?<major>\d+)' -and [int]$Matches.major -ge 17
        }

        if ($jdkCandidates.Count -eq 0) {
            throw 'A JDK 17 or newer is required for the Android build.'
        }

        $previousJavaHome = $env:JAVA_HOME
        $env:JAVA_HOME = @($jdkCandidates)[0]
        try {
            Invoke-CheckedStep 'Android unit tests and debug APK' {
                Push-Location $androidRoot
                try {
                    & $gradleWrapper --no-daemon testDebugUnitTest assembleDebug
                }
                finally {
                    Pop-Location
                }
            }
        }
        finally {
            $env:JAVA_HOME = $previousJavaHome
        }
    }

    Write-Host "`nAll selected EasyNoty verification gates passed." -ForegroundColor Green
}
finally {
    Pop-Location
}
