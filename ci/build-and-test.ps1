param([switch]$CheckMSBuild)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repo = Split-Path -Parent $PSScriptRoot
Push-Location $repo
$previousInterlock = $env:POWERLOCKGUARD_NO_NATIVE_ACTIONS
$env:POWERLOCKGUARD_NO_NATIVE_ACTIONS = '1'
$extractedReferences = $null
try {
    $cache = Join-Path $repo '.ci'
    $artifacts = Join-Path $repo 'artifacts'
    New-Item -ItemType Directory -Force $cache, $artifacts | Out-Null
    $package = Join-Path $cache 'net40.nupkg'
    $expectedHash = '54D6E20A1B61CAF79395D6D71D091265E81F5A5705AC4AE52AF45CA143C3C694'
    if (-not (Test-Path -LiteralPath $package)) {
        $url = 'https://api.nuget.org/v3-flatcontainer/microsoft.netframework.referenceassemblies.net40/1.0.3/microsoft.netframework.referenceassemblies.net40.1.0.3.nupkg'
        & curl.exe --fail --location --max-time 120 --output $package $url
        if ($LASTEXITCODE -ne 0) { throw 'Downloading .NET 4.0 reference assemblies failed.' }
    }
    if ((Get-FileHash -LiteralPath $package -Algorithm SHA256).Hash -ne $expectedHash) { throw 'Reference package SHA256 mismatch.' }
    $referenceRoot = Join-Path $cache 'net40'
    $referenceBuild = Join-Path $referenceRoot 'build'
    $referenceDir = Join-Path $referenceBuild '.NETFramework/v4.0'
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    # Extract to a fresh directory every run; do not trust a modified extracted cache.
    $referenceRoot = Join-Path $cache ('net40-' + [Guid]::NewGuid().ToString('N'))
    $extractedReferences = $referenceRoot
    [IO.Compression.ZipFile]::ExtractToDirectory($package, $referenceRoot)
    $referenceBuild = Join-Path $referenceRoot 'build'
    $referenceDir = Join-Path $referenceBuild '.NETFramework/v4.0'
    $compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework/v4.0.30319/csc.exe'
    if (-not (Test-Path -LiteralPath $compiler)) { throw 'Legacy C# compiler not found.' }
    [xml]$project = Get-Content -LiteralPath 'PowerLockGuard.csproj'
    $ns = New-Object Xml.XmlNamespaceManager($project.NameTable)
    $ns.AddNamespace('m', 'http://schemas.microsoft.com/developer/msbuild/2003')
    $sources = @($project.SelectNodes('//m:Compile', $ns) | ForEach-Object { Join-Path $repo $_.Include })
    # Boundary check: destructive APIs and shutdown executable belong only to the reviewed Windows adapter.
    foreach ($source in $sources) {
        if ((Split-Path -Leaf $source) -eq 'WindowsAdapters.cs') { continue }
        if (Select-String -LiteralPath $source -Pattern '\.Kill\s*\(|SetSuspendState\s*\(|LockWorkStation\s*\(|shutdown\.exe' -Quiet) {
            throw "Native action escaped the Windows adapter: $source"
        }
    }
    $references = @('mscorlib', 'System', 'System.Core', 'System.Drawing', 'System.Windows.Forms') | ForEach-Object { '/reference:' + (Join-Path $referenceDir ($_.ToString() + '.dll')) }
    foreach ($configuration in @('Debug', 'Release')) {
        $binaryDir = Join-Path $repo ('bin/Verification/' + $configuration)
        $resultsDir = Join-Path $artifacts $configuration
        New-Item -ItemType Directory -Force $binaryDir, $resultsDir | Out-Null
        $app = Join-Path $binaryDir 'PowerLockGuard.exe'
        $test = Join-Path $binaryDir 'SafetyTests.exe'
        $optimization = if ($configuration -eq 'Release') { '/optimize+' } else { '/debug+' }
        & $compiler /nologo /noconfig /nostdlib+ /warn:4 /warnaserror+ $optimization /target:winexe "/win32icon:$repo/app.ico" "/out:$app" @references @sources
        if ($LASTEXITCODE -ne 0) { throw "$configuration application compilation failed." }
        & $compiler /nologo /noconfig /nostdlib+ /warn:4 /warnaserror+ /target:exe "/out:$test" @references "/reference:$app" (Join-Path $repo 'tests/SafetyTests.cs')
        if ($LASTEXITCODE -ne 0) { throw "$configuration test compilation failed." }
        & $test $resultsDir
        if ($LASTEXITCODE -ne 0) { throw "$configuration safety tests failed." }
        # Invoke the reviewed executable only through its pre-UI, recording-only scenario entry point.
        $scenario = Join-Path $resultsDir 'cli-simulation.log'
        $process = Start-Process -FilePath $app -ArgumentList @('--simulate', 'all', ('"' + $scenario + '"')) -WindowStyle Hidden -Wait -PassThru
        if ($process.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $scenario)) { throw "$configuration CLI simulation failed." }
        if (-not (Select-String -LiteralPath $scenario -SimpleMatch 'Native actions invoked: 0' -Quiet)) { throw 'Missing simulation evidence.' }
        Copy-Item -LiteralPath $app -Destination (Join-Path $resultsDir 'PowerLockGuard.exe')
        Copy-Item -LiteralPath 'config.ini', 'app.ico' -Destination $resultsDir
        (Get-FileHash -LiteralPath $app -Algorithm SHA256).Hash | Set-Content -LiteralPath (Join-Path $resultsDir 'PowerLockGuard.sha256')
    }
    if ($CheckMSBuild) {
        $msbuild = Get-Command msbuild.exe -ErrorAction Stop
        foreach ($configuration in @('Debug', 'Release')) {
            & $msbuild.Source 'PowerLockGuard.sln' /nologo /t:Rebuild "/p:Configuration=$configuration" '/p:Platform=Any CPU' ("/p:TargetFrameworkRootPath=" + $referenceBuild.Replace('\', '/') + '/') '/p:EnableFrameworkPathOverride=false' '/p:NoStdLib=true' '/p:AdditionalExplicitAssemblyReferences=mscorlib' '/p:TreatWarningsAsErrors=true'
            if ($LASTEXITCODE -ne 0) { throw "$configuration solution build failed." }
        }
    }
    & cmd.exe /c 'Build.bat --no-pause'
    if ($LASTEXITCODE -ne 0) { throw 'Legacy Build.bat entry point failed.' }
    @('Reference target: .NET Framework 4.0 (NuGet 1.0.3, SHA256 verified)', 'Native action interlock: enabled', 'Build.bat: passed', "MSBuild requested: $CheckMSBuild") | Set-Content -LiteralPath (Join-Path $artifacts 'build-evidence.txt')
}
finally {
    if ($null -ne $extractedReferences -and (Test-Path -LiteralPath $extractedReferences)) {
        $resolvedExtract = [IO.Path]::GetFullPath($extractedReferences)
        $resolvedCache = [IO.Path]::GetFullPath((Join-Path $repo '.ci')) + [IO.Path]::DirectorySeparatorChar
        if (-not $resolvedExtract.StartsWith($resolvedCache, [StringComparison]::OrdinalIgnoreCase)) { throw 'Refusing cleanup outside the verification cache.' }
        Remove-Item -LiteralPath $resolvedExtract -Recurse -Force
    }
    $env:POWERLOCKGUARD_NO_NATIVE_ACTIONS = $previousInterlock
    Pop-Location
}
