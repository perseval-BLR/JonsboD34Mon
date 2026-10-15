# Builds Jonsbo D34 Mon and its installers.
#   powershell -ExecutionPolicy Bypass -File build.ps1 [-Version 1.0.0] [-Dotnet path\to\dotnet.exe]
# Output: dist\ — "Jonsbo D34 Mon <version> (ru).msi" and "(en).msi".
param(
    [string]$Version = '1.1.1',
    [string]$Dotnet = 'dotnet'
)
$ErrorActionPreference = 'Stop'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'

$root = $PSScriptRoot
$app = Join-Path $env:TEMP 'jd34-publish'
$dist = Join-Path $root 'dist'

# 1. The app, self-contained (.NET included) single-file exe.
if (Test-Path $app) { Remove-Item $app -Recurse -Force }
& $Dotnet publish "$root\JonsboD34Mon\JonsboD34Mon.csproj" -c Release -o $app --self-contained true -p:Version=$Version
if ($LASTEXITCODE) { throw 'app publish failed' }

# 2. License page of the setup wizard: Installer\License.txt -> License.rtf (non-ASCII as \uN escapes).
$rtf = New-Object Text.StringBuilder
[void]$rtf.Append('{\rtf1\ansi\ansicpg1252\deff0{\fonttbl{\f0\fswiss Segoe UI;}}\viewkind4\uc1\pard\sa120\f0\fs18 ')
$lines = [IO.File]::ReadAllLines("$root\Installer\License.txt", [Text.Encoding]::UTF8)
for ($i = 0; $i -lt $lines.Count; $i++) {
    if ($i -gt 0) { [void]$rtf.Append('\par ') }
    $bold = $i -eq 0
    if ($bold) { [void]$rtf.Append('\b ') }
    foreach ($ch in $lines[$i].ToCharArray()) {
        $c = [int]$ch
        if ($c -gt 127) { [void]$rtf.Append('\u' + $c + '?') }
        elseif ('\{}'.Contains($ch)) { [void]$rtf.Append('\' + $ch) }
        else { [void]$rtf.Append($ch) }
    }
    if ($bold) { [void]$rtf.Append('\b0 ') }
}
[void]$rtf.Append('\par }')
[IO.File]::WriteAllText("$root\Installer\License.rtf", $rtf.ToString(), [Text.Encoding]::ASCII)

# 3. The MSI, one per language.
& $Dotnet build "$root\Installer\JonsboD34Mon.Installer.wixproj" -c Release -p:AppDir=$app -p:Version=$Version
if ($LASTEXITCODE) { throw 'installer build failed' }

New-Item -ItemType Directory -Force $dist | Out-Null
foreach ($c in @{ 'ru-RU' = 'ru'; 'en-US' = 'en' }.GetEnumerator()) {
    Copy-Item "$root\Installer\bin\Release\$($c.Key)\JonsboD34Mon.msi" (Join-Path $dist "Jonsbo D34 Mon $Version ($($c.Value)).msi") -Force
}

# 4. Portable zip: the same exe + portable marker (settings next to the exe) + PawnIO setup + licenses.
$port = Join-Path $env:TEMP "jd34-portable\Jonsbo D34 Mon $Version"
if (Test-Path (Split-Path $port)) { Remove-Item (Split-Path $port) -Recurse -Force }
New-Item -ItemType Directory -Force $port | Out-Null
Copy-Item "$app\JonsboD34Mon.exe", "$root\Portable\JonsboD34Mon.portable", "$root\Portable\README.txt",
          "$root\Installer\redist\PawnIO_setup.exe", "$root\THIRD-PARTY-NOTICES.md",
          "$root\JonsboD34Mon\Assets\DSEG-LICENSE.txt" $port
Copy-Item "$root\LICENSE" "$port\LICENSE.txt"
$zip = Join-Path $dist "Jonsbo D34 Mon $Version portable.zip"
if (Test-Path $zip) { Remove-Item $zip }
Compress-Archive -Path $port -DestinationPath $zip -CompressionLevel Optimal

# 5. Checksums for the release page.
Get-ChildItem $dist -File | Where-Object { $_.Extension -in '.msi', '.zip' } | ForEach-Object {
    "$((Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLower())  $($_.Name)"
} | Set-Content (Join-Path $dist 'SHA256SUMS.txt') -Encoding ASCII
Get-ChildItem $dist
