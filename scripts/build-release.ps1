param(
    [Parameter(Mandatory = $true)][string]$GameManagedDir,
    [Parameter(Mandatory = $true)][string]$BepInExCoreDir
)
$ErrorActionPreference = 'Stop'
$Root = Split-Path -Parent $PSScriptRoot
foreach ($Name in @('Assembly-CSharp.dll', 'TeamCherry.TK2D.dll', 'Newtonsoft.Json.dll')) {
    if (!(Test-Path (Join-Path $GameManagedDir $Name))) { throw "Dependência ausente: $Name" }
}
foreach ($Name in @('BepInEx.dll', '0Harmony.dll')) {
    if (!(Test-Path (Join-Path $BepInExCoreDir $Name))) { throw "Dependência ausente: $Name" }
}
& (Join-Path $PSScriptRoot 'test.ps1')
& dotnet build (Join-Path $Root 'src/SilkThreadCrest.csproj') -c Release -v:minimal "-p:GameManagedDir=$GameManagedDir" "-p:BepInExCoreDir=$BepInExCoreDir"
if ($LASTEXITCODE -ne 0) { throw 'Compilação falhou.' }
$Source = Get-Content (Join-Path $Root 'src/SilkThreadCrestPlugin.cs') -Raw
$Version = [regex]::Match($Source, 'PluginVersion = "([^"]+)"').Groups[1].Value
if (!$Version) { throw 'PluginVersion não encontrada.' }
$Out = Join-Path $Root "artifacts/$Version"
$Stage = Join-Path ([IO.Path]::GetTempPath()) ([Guid]::NewGuid().ToString())
try {
    $PluginStage = Join-Path $Stage 'windows/BepInEx/plugins/SilkThreadCrest'
    $SourceStage = Join-Path $Stage 'source'
    New-Item -ItemType Directory -Force -Path $PluginStage, $SourceStage, $Out | Out-Null
    $Dll = Join-Path $Root 'src/bin/Release/net472/SilkThreadCrest.dll'
    Copy-Item $Dll $PluginStage
    Copy-Item (Join-Path $Root 'Skins') $PluginStage -Recurse
    Copy-Item (Join-Path $Root 'README.md') (Join-Path $Stage 'windows')
    foreach ($Name in @('src','Skins','docs','scripts','tests','.github','README.md','AGENTS.md','CONTRIBUTING.md','CHANGELOG.md','NOTICE.md','.gitignore','.gitattributes')) {
        $Path = Join-Path $Root $Name
        if (Test-Path $Path) { Copy-Item $Path $SourceStage -Recurse }
    }
    Get-ChildItem $SourceStage -Directory -Recurse -Force | Where-Object { $_.Name -in @('bin','obj') } | Sort-Object { $_.FullName.Length } -Descending | Remove-Item -Recurse -Force
    Copy-Item $Dll (Join-Path $Out "SilkThreadCrest-$Version.dll") -Force
    # ZIPs created in a new temp directory do not retain obsolete files.
    Compress-Archive -Path (Join-Path $Stage 'windows/*') -DestinationPath (Join-Path $Stage 'windows.zip')
    # .NET preserves dotfiles such as .github and .gitignore in source archives.
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [IO.Compression.ZipFile]::CreateFromDirectory($SourceStage, (Join-Path $Stage 'source.zip'))
    Move-Item (Join-Path $Stage 'windows.zip') (Join-Path $Out "SilkThreadCrest-$Version-Windows.zip") -Force
    Move-Item (Join-Path $Stage 'source.zip') (Join-Path $Out "SilkThreadCrest-$Version-Source.zip") -Force
    Get-ChildItem $Out
    Write-Host 'Build criada. Validação manual no jogo continua necessária.'
} finally {
    if (Test-Path $Stage) { Remove-Item $Stage -Recurse -Force }
}
