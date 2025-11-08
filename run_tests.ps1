# PowerShell script to run all Cobra tests.

param (
    [string]$SpecificTestFile
)
# --- Configuration ---
$CobraProjectName = "Cobra"
$TestsFolder = "Tests"
# Use Release configuration by default. Change to "Debug" if needed.
$BuildConfig = "Release"
# --- End Configuration ---

$PSScriptRoot = Split-Path -Parent -Path $MyInvocation.MyCommand.Definition
$CobraExePath = Join-Path $PSScriptRoot -ChildPath "bin/$BuildConfig/net9.0/$CobraProjectName.exe"
$TestsPath = Join-Path $PSScriptRoot -ChildPath $TestsFolder

if (-not (Test-Path $CobraExePath)) {
    Write-Host "ERROR: Cobra executable not found at '$CobraExePath'"
    Write-Host "Please build the project in '$BuildConfig' configuration first."
    exit 1
}


$TestFiles = if ([string]::IsNullOrEmpty($SpecificTestFile)) {
    Get-ChildItem -Path $TestsPath -Filter "*.cb" | Sort-Object Name
} else {
    Get-Item -Path $SpecificTestFile
}

Write-Host "Starting Cobra test runner..."
Write-Host "Found $($TestFiles.Count) tests in '$TestsPath'"
Write-Host "--------------------------------------------------"

foreach ($TestFile in $TestFiles) {
    Write-Host "Running test: $($TestFile.Name)"
    & $CobraExePath $TestFile.FullName
    Write-Host "--------------------------------------------------"
}

Write-Host "All tests executed."