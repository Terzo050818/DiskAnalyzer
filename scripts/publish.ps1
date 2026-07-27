param(
    [string]$Version = "0.1.2"
)

$ErrorActionPreference = "Stop"
$workspaceRoot = [System.IO.Path]::GetFullPath(
    (Join-Path $PSScriptRoot ".."))
$publishDirectory = [System.IO.Path]::GetFullPath(
    (Join-Path $workspaceRoot "artifacts\publish\win-x64"))
$archivePath = [System.IO.Path]::GetFullPath(
    (Join-Path $workspaceRoot "artifacts\DiskAnalyzer-$Version-win-x64.zip"))
$artifactsRoot = [System.IO.Path]::GetFullPath(
    (Join-Path $workspaceRoot "artifacts"))

foreach ($path in @($publishDirectory, $archivePath)) {
    if (-not $path.StartsWith(
        $artifactsRoot + [System.IO.Path]::DirectorySeparatorChar,
        [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to modify a path outside the artifacts directory: $path"
    }
}

if (Test-Path -LiteralPath $publishDirectory) {
    Remove-Item -LiteralPath $publishDirectory -Recurse -Force
}

if (Test-Path -LiteralPath $archivePath) {
    Remove-Item -LiteralPath $archivePath -Force
}

dotnet restore (Join-Path $workspaceRoot "DiskAnalyzer.sln") `
    --configfile (Join-Path $workspaceRoot "NuGet.Config")

if ($LASTEXITCODE -ne 0) {
    throw "Solution restore failed; publishing was stopped."
}

dotnet restore `
    (Join-Path $workspaceRoot "src\DiskAnalyzer.App\DiskAnalyzer.App.csproj") `
    --runtime win-x64 `
    --configfile (Join-Path $workspaceRoot "NuGet.Config")

if ($LASTEXITCODE -ne 0) {
    throw "Windows runtime restore failed; publishing was stopped."
}

dotnet test (Join-Path $workspaceRoot "DiskAnalyzer.sln") `
    --configuration Release `
    --no-restore

if ($LASTEXITCODE -ne 0) {
    throw "Tests failed; publishing was stopped."
}

dotnet publish `
    (Join-Path $workspaceRoot "src\DiskAnalyzer.App\DiskAnalyzer.App.csproj") `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    --no-restore `
    --output $publishDirectory `
    -p:PublishProfile=win-x64 `
    -p:Version=$Version `
    -p:AssemblyVersion="$Version.0" `
    -p:FileVersion="$Version.0" `
    -p:InformationalVersion="$Version-mvp"

if ($LASTEXITCODE -ne 0) {
    throw "Publishing failed."
}

Compress-Archive `
    -Path (Join-Path $publishDirectory "*") `
    -DestinationPath $archivePath `
    -CompressionLevel Optimal

$hash = Get-FileHash -LiteralPath $archivePath -Algorithm SHA256
$hashLine = "$($hash.Hash.ToLowerInvariant())  $([System.IO.Path]::GetFileName($archivePath))"
$hashPath = Join-Path $artifactsRoot "SHA256SUMS.txt"
[System.IO.File]::WriteAllText(
    $hashPath,
    $hashLine + [Environment]::NewLine,
    [System.Text.UTF8Encoding]::new($false))

Write-Output "Published: $publishDirectory"
Write-Output "Archive: $archivePath"
Write-Output "SHA256: $($hash.Hash.ToLowerInvariant())"
