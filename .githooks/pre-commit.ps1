# Pre-commit hook: No warnings/errors policy
# This hook ensures code compiles without warnings or errors before committing

Write-Host "[CHECK] Checking for compilation errors and warnings..." -ForegroundColor Cyan
Write-Host ""

# Run dotnet build and capture output
$buildOutput = & dotnet build 2>&1
$buildExitCode = $LASTEXITCODE

# Check for actual errors (exclude "0 Error(s)" success message)
$errorLines = $buildOutput | Select-String -Pattern '\berror\b' | Where-Object { 
    $_.Line -notmatch '0 Error\(s\)' -and 
    $_.Line -notmatch ': 0$'
}
if ($errorLines -or $buildOutput -match 'Build FAILED') {
    Write-Host "[FAIL] BUILD FAILED: Compilation errors detected" -ForegroundColor Red
    Write-Host ""
    $errorLines | Format-Table -AutoSize
    exit 1
}

# Check for actual warnings (exclude "0 Warning(s)" success message)
$warningLines = $buildOutput | Select-String -Pattern '\bwarning\b' | Where-Object {
    $_.Line -notmatch '0 Warning\(s\)' -and
    $_.Line -notmatch ': 0$'
}
if ($warningLines) {
    Write-Host "[WARN] BUILD WARNING: Warnings detected (no-warnings policy enforced)" -ForegroundColor Yellow
    Write-Host ""
    $warningLines | Format-List
    Write-Host ""
    Write-Host "Please fix all warnings before committing." -ForegroundColor Yellow
    exit 1
}

# Check build exit code
if ($buildExitCode -ne 0) {
    Write-Host "[FAIL] BUILD FAILED: dotnet build exited with code $buildExitCode" -ForegroundColor Red
    Write-Host ""
    $buildOutput | Format-List
    exit 1
}

Write-Host "[OK] No errors or warnings detected" -ForegroundColor Green
Write-Host "[OK] Build successful - proceeding with commit" -ForegroundColor Green
Write-Host ""
