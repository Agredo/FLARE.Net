<#
.SYNOPSIS
    Builds the signed Release APK for Android.

.DESCRIPTION
    Signing uses the FLARE_ANDROID_KEYSTORE, FLARE_ANDROID_KEY_ALIAS, FLARE_ANDROID_STORE_PASS and
    FLARE_ANDROID_KEY_PASS environment variables. When they are not set, the credentials are read from
    %USERPROFILE%\.flare\keystore-credentials.txt (see the comment in Flare.Maui.csproj); without them
    the build produces an unsigned APK.

    The result is written to
    src\Flare.Maui\bin\Release\net10.0-android36.0\publish\com.flare.maui-Signed.apk
#>
param(
    [string]$AndroidSdk = 'C:\Android',
    [string]$JavaSdk = 'C:\Program Files\Microsoft\jdk-25.0.0.36-hotspot'
)

$ErrorActionPreference = 'Stop'
$credentials = Join-Path $env:USERPROFILE '.flare\keystore-credentials.txt'

if (-not $env:FLARE_ANDROID_KEYSTORE -and (Test-Path $credentials)) {
    $values = @{}
    foreach ($line in Get-Content $credentials) {
        $name, $value = $line -split '=', 2
        if ($name -and $value) { $values[$name.Trim()] = $value.Trim() }
    }
    $env:FLARE_ANDROID_KEYSTORE = $values['keystore']
    $env:FLARE_ANDROID_KEY_ALIAS = $values['alias']
    $env:FLARE_ANDROID_STORE_PASS = $values['password']
    $env:FLARE_ANDROID_KEY_PASS = $values['password']
}

if (-not $env:FLARE_ANDROID_KEYSTORE) {
    Write-Warning 'No signing credentials found - the APK will not be signed.'
}

$env:JAVA_HOME = $JavaSdk
$env:ANDROID_SDK_ROOT = $AndroidSdk
$env:ANDROID_HOME = $AndroidSdk

Push-Location $PSScriptRoot
try {
    dotnet publish src\Flare.Maui\Flare.Maui.csproj -f net10.0-android36.0 -c Release `
        -p:JavaSdkDirectory=$JavaSdk -p:AndroidSdkDirectory=$AndroidSdk
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE" }

    $apk = Join-Path $PSScriptRoot 'src\Flare.Maui\bin\Release\net10.0-android36.0\publish\com.flare.maui-Signed.apk'
    if (-not (Test-Path $apk)) { throw "Signed APK not found at $apk" }

    & (Join-Path $AndroidSdk 'build-tools\37.0.0\apksigner.bat') verify $apk
    if ($LASTEXITCODE -ne 0) { throw 'APK signature verification failed' }

    Write-Host "Signed APK: $apk" -ForegroundColor Green
}
finally {
    Pop-Location
}
