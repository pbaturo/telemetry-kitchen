# Pre-commit hook: No warnings/errors policy
# This hook ensures code compiles without warnings or errors before committing

Write-Host "[CHECK] Checking for compilation errors and warnings..." -ForegroundColor Cyan
Write-Host ""

# Run dotnet build and capture output
$buildOutput = & dotnet build 2>&1
$buildExitCode = $LASTEXITCODE

# Check for errors
if ($buildOutput -match '(\berror\b|^Build FAILED)') {
    Write-Host "[FAIL] BUILD FAILED: Compilation errors detected" -ForegroundColor Red
    Write-Host ""
    $buildOutput | Select-String -Pattern 'error' | Format-Table -AutoSize
    exit 1
}

# Check for warnings
if ($buildOutput -match '\bwarning\b') {
    Write-Host "[WARN] BUILD WARNING: Warnings detected (no-warnings policy enforced)" -ForegroundColor Yellow
    Write-Host ""
    $buildOutput | Select-String -Pattern 'warning'
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
