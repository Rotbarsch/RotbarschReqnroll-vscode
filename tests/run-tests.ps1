<#
.SYNOPSIS
    Runs the Playwright VS Code extension tests.

.DESCRIPTION
    Installs Node dependencies (if needed) and executes the Playwright test suite
    that validates the Rotbarsch Reqnroll VS Code extension.

    No browser download is required: the tests drive VS Code as an Electron app
    directly via Playwright's built-in Electron support, using the locally
    installed VS Code executable.

    Prerequisites:
      - Node.js installed
      - VS Code installed (checked automatically)
      - Demo/Example.NUnit built at least once (for test-discovery/-execution tests)

.PARAMETER Headed
    Run in headed mode (shows VS Code window during test execution).

.PARAMETER Test
    Run only the tests whose names match this filter string (passed to --grep).

.EXAMPLE
    .\run-tests.ps1
    .\run-tests.ps1 -Headed
    .\run-tests.ps1 -Test "syntax"
    .\run-tests.ps1 -Test "Test Execution"
#>
param(
    [switch]$Headed,
    [string]$Test
)

$ErrorActionPreference = 'Stop'
$TestsDir = $PSScriptRoot

Push-Location $TestsDir

try {
    # Install / restore Node dependencies
    if (-not (Test-Path 'node_modules')) {
        Write-Host "Installing Node.js dependencies..." -ForegroundColor Cyan
        npm install
        if ($LASTEXITCODE -ne 0) { throw "npm install failed" }
    }

    # No 'playwright install' needed: we drive VS Code as an Electron app.
    # Playwright's _electron API connects to the locally installed VS Code
    # binary without requiring any browser downloads.

    # Build the arguments for the test runner
    $testArgs = @()
    if ($Headed) { $testArgs += '--headed' }
    if ($Test)   { $testArgs += '--grep', $Test }

    Write-Host "Running Playwright tests..." -ForegroundColor Cyan
    npx playwright test @testArgs

    if ($LASTEXITCODE -ne 0) {
        Write-Host "Some tests failed. Opening HTML report..." -ForegroundColor Yellow
        npx playwright show-report playwright-report
    }
} finally {
    Pop-Location
}
