<#
.SYNOPSIS
    Smoke-tests a published FLARE Windows executable.

.DESCRIPTION
    Verifies that the executable exists, that its PE header matches the expected
    CPU architecture, and - when the current host can execute that architecture -
    that the process starts and stays alive instead of crashing on startup.

.PARAMETER ExePath
    Path to the published single-file executable.

.PARAMETER Architecture
    Architecture the executable is supposed to be built for.

.PARAMETER TimeoutSeconds
    How long the launched process has to stay alive before it counts as healthy.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string] $ExePath,
    [Parameter(Mandatory = $true)][ValidateSet('x64', 'arm64', 'x86')][string] $Architecture,
    [int] $TimeoutSeconds = 25
)

$ErrorActionPreference = 'Stop'

$expectedMachine = @{ x86 = 0x014C; x64 = 0x8664; arm64 = 0xAA64 }[$Architecture]

if (-not (Test-Path -LiteralPath $ExePath -PathType Leaf)) {
    throw "Executable not found: $ExePath"
}

$exe = (Resolve-Path -LiteralPath $ExePath).Path

$stream = [System.IO.File]::OpenRead($exe)
try {
    $reader = New-Object System.IO.BinaryReader($stream)
    $stream.Position = 0x3C
    $peOffset = $reader.ReadInt32()
    $stream.Position = $peOffset + 4
    $machine = $reader.ReadUInt16()
}
finally {
    $stream.Dispose()
}

$actual = switch ($machine) { 0x014C { 'x86' } 0x8664 { 'x64' } 0xAA64 { 'arm64' } default { '0x{0:X4}' -f $machine } }
Write-Host "PE machine type: $actual (expected $Architecture)"
if ($machine -ne $expectedMachine) {
    throw "Architecture mismatch: $exe is $actual but $Architecture was expected."
}

$sizeMb = [math]::Round((Get-Item -LiteralPath $exe).Length / 1MB, 1)
Write-Host "Executable size: $sizeMb MB"

# ARM64 Windows runs x64/x86 binaries through emulation; x64 Windows cannot run ARM64 ones.
$hostArch = [System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString().ToLowerInvariant()
$canExecute = ($hostArch -eq $Architecture) -or
              ($hostArch -eq 'arm64' -and $Architecture -in @('x64', 'x86'))
if (-not $canExecute) {
    Write-Warning "Host is $hostArch and cannot execute a $Architecture binary - skipping the launch check."
    return
}

Write-Host "Launching $exe and waiting up to $TimeoutSeconds s ..."
$process = Start-Process -FilePath $exe -WorkingDirectory (Split-Path -LiteralPath $exe) -PassThru
try {
    for ($elapsed = 0; $elapsed -lt $TimeoutSeconds; $elapsed++) {
        Start-Sleep -Seconds 1
        if ($process.HasExited) {
            throw "Process exited after ${elapsed}s with code $($process.ExitCode) - the application failed to start."
        }
    }
    Write-Host "Process is alive after $TimeoutSeconds s (pid $($process.Id)) - smoke test passed."
}
finally {
    if (-not $process.HasExited) {
        $process | Stop-Process -Force
        Write-Host "Stopped the smoke-test process."
    }
}


