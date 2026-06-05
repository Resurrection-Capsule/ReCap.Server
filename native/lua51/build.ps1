$ErrorActionPreference = 'Stop'
$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$vs = & $vswhere -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
if (-not $vs) { throw "MSVC not found. Install VS Build Tools with C++ workload." }
$vcvars = Join-Path $vs 'VC\Auxiliary\Build\vcvars64.bat'
$src = Join-Path $PSScriptRoot 'lua-5.1.4\src'
$out = Join-Path $PSScriptRoot 'out'
New-Item -ItemType Directory -Force $out | Out-Null
Push-Location $out
try {
    $core = (Get-ChildItem $src -Filter *.c | Where-Object { $_.Name -notin 'lua.c','luac.c','print.c' } | ForEach-Object FullName) -join ' '
    cmd /c "`"$vcvars`" >nul && cl /nologo /O2 /W3 /MD /D_CRT_SECURE_NO_DEPRECATE /DLUA_BUILD_AS_DLL $core /link /DLL /OUT:recaplua51.dll"
    if ($LASTEXITCODE -ne 0) { throw "DLL build failed" }
    cmd /c "`"$vcvars`" >nul && cl /nologo /O2 /W3 /MD /D_CRT_SECURE_NO_DEPRECATE $core `"$src\luac.c`" `"$src\print.c`" /Fe:luac.exe"
    if ($LASTEXITCODE -ne 0) { throw "luac build failed" }
} finally { Pop-Location }
Write-Host "OK: $out\recaplua51.dll + $out\luac.exe"
