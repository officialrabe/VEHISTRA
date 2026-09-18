<#
.SYNOPSIS
    Erzeugt eine vollstaendige, auslieferbare Version von Vehistra.

.DESCRIPTION
    Das Skript fuehrt alle Schritte einer Veroeffentlichung aus:

      1. Voraussetzungen pruefen (.NET SDK, Inno Setup)
      2. Projektmappe bereinigen, wiederherstellen und uebersetzen
      3. Automatisierte Tests ausfuehren
      4. Alle vier Programme nach artifacts\publish veroeffentlichen
      5. Anleitungen als Markdown und PDF erzeugen
      6. Vehistra-Setup.exe und Vehistra-Update.exe bauen
      7. Release\<Version> mit allen Dateien, latest.json, Pruefsummen,
         Versionshinweisen und Beispielkonfiguration zusammenstellen

    Das Codesignaturzertifikat wird NIEMALS im Repository abgelegt. Es wird
    ueber den Parameter -SignTool als vorkonfigurierter Inno-Setup-Signierer
    uebergeben, zum Beispiel:

        /DSignTool=lspsign

.PARAMETER Version
    Semantische Version der Veroeffentlichung, zum Beispiel 1.1.0.

.PARAMETER ReleaseNotesFile
    Datei mit den Versionshinweisen. Fehlt sie, wird eine Vorlage erzeugt.

.PARAMETER Mandatory
    Kennzeichnet die Version als Pflichtupdate. Aeltere Versionen weigern
    sich dann zu starten, bis das Update eingespielt wurde.

.PARAMETER MinimumVersion
    Aelteste Version, die direkt auf diese Version aktualisieren darf.

.PARAMETER SignTool
    Name eines in Inno Setup hinterlegten Signierwerkzeugs. Ohne Angabe
    werden die Pakete unsigniert erzeugt.

.PARAMETER SkipTests
    Ueberspringt die automatisierten Tests (nur fuer Zwischenstaende).

.EXAMPLE
    .\CreateRelease.ps1 -Version 1.1.0 -ReleaseNotesFile .\notes-1.1.0.txt

.EXAMPLE
    .\CreateRelease.ps1 -Version 2.0.0 -Mandatory -MinimumVersion 1.5.0 -SignTool lspsign

.NOTES
    Entwickelt von LSP Virtual Services - vehistra.dev
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string] $Version,

    [string] $ReleaseNotesFile,

    [switch] $Mandatory,

    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string] $MinimumVersion = '1.0.0',

    [string] $SignTool,

    [string] $Configuration = 'Release',

    [string] $Runtime = 'win-x64',

    [switch] $SkipTests
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# ---------------------------------------------------------------------------
# Verzeichnisse
# ---------------------------------------------------------------------------
$scriptRoot   = Split-Path -Parent $MyInvocation.MyCommand.Definition
$repositoryRoot = Resolve-Path (Join-Path $scriptRoot '..\..')
$solution     = Join-Path $repositoryRoot 'Vehistra.sln'
$artifacts    = Join-Path $repositoryRoot 'artifacts'
$publishDir   = Join-Path $artifacts 'publish'
$installerOut = Join-Path $artifacts 'installer'
$docsSource   = Join-Path $repositoryRoot 'docs'
$installerDir = Join-Path $repositoryRoot 'tools\Installer'
$releaseDir   = Join-Path $repositoryRoot "Release\$Version"

function Write-Step {
    param([int] $Number, [string] $Text)
    Write-Host ''
    Write-Host "[$Number/9] $Text" -ForegroundColor Cyan
    Write-Host ('-' * (($Text.Length) + 6)) -ForegroundColor DarkGray
}

function Invoke-Checked {
    param([string] $File, [string[]] $Arguments, [string] $Description)

    Write-Host "     > $File $($Arguments -join ' ')" -ForegroundColor DarkGray
    & $File @Arguments

    if ($LASTEXITCODE -ne 0) {
        throw "$Description ist mit dem Rueckgabewert $LASTEXITCODE fehlgeschlagen."
    }
}

function Find-InnoSetup {
    if ($env:ISCC_PATH -and (Test-Path $env:ISCC_PATH)) { return $env:ISCC_PATH }

    $candidates = @(
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles(x86)}\Inno Setup 5\ISCC.exe"
    )

    foreach ($candidate in $candidates) {
        if ($candidate -and (Test-Path $candidate)) { return $candidate }
    }

    $command = Get-Command 'ISCC.exe' -ErrorAction SilentlyContinue
    if ($command) { return $command.Source }

    return $null
}

Write-Host ''
Write-Host '===========================================================' -ForegroundColor White
Write-Host " VEHISTRA - VERSION $Version ERSTELLEN" -ForegroundColor White
Write-Host ' LSP Virtual Services - vehistra.dev' -ForegroundColor DarkGray
Write-Host '===========================================================' -ForegroundColor White

# ---------------------------------------------------------------------------
# 1. Voraussetzungen
# ---------------------------------------------------------------------------
Write-Step 1 'Voraussetzungen pruefen'

if (-not (Get-Command 'dotnet' -ErrorAction SilentlyContinue)) {
    throw 'Das .NET SDK wurde nicht gefunden. Bitte von https://dotnet.microsoft.com/download installieren.'
}

$sdkVersion = (& dotnet --version).Trim()
Write-Host "     .NET SDK      : $sdkVersion"

$iscc = Find-InnoSetup
if ($iscc) {
    Write-Host "     Inno Setup    : $iscc"
} else {
    Write-Warning 'Inno Setup 6 wurde nicht gefunden. Die Installationspakete werden uebersprungen.'
    Write-Warning 'Download: https://jrsoftware.org/isdl.php - danach ISCC_PATH setzen oder neu ausfuehren.'
}

if ($SignTool) {
    Write-Host "     Signatur      : Signierwerkzeug '$SignTool'"
} else {
    Write-Host '     Signatur      : ohne Codesignatur (Zertifikat liegt nie im Repository)'
}

# ---------------------------------------------------------------------------
# 2. Bereinigen
# ---------------------------------------------------------------------------
Write-Step 2 'Ausgabeverzeichnisse bereinigen'

foreach ($directory in @($publishDir, $installerOut, $releaseDir)) {
    if (Test-Path $directory) { Remove-Item $directory -Recurse -Force }
    New-Item -ItemType Directory -Path $directory -Force | Out-Null
}

Write-Host "     Zielordner    : $releaseDir"

# ---------------------------------------------------------------------------
# 3. Uebersetzen
# ---------------------------------------------------------------------------
Write-Step 3 'Projektmappe uebersetzen'

Invoke-Checked 'dotnet' @('restore', $solution) 'Die Wiederherstellung der Pakete'
Invoke-Checked 'dotnet' @(
    'build', $solution,
    '--configuration', $Configuration,
    '--no-restore',
    "-p:Version=$Version",
    "-p:FileVersion=$Version.0",
    "-p:AssemblyVersion=$Version.0"
) 'Die Uebersetzung'

# ---------------------------------------------------------------------------
# 4. Tests
# ---------------------------------------------------------------------------
Write-Step 4 'Automatisierte Tests ausfuehren'

if ($SkipTests) {
    Write-Warning 'Die Tests wurden per Parameter uebersprungen. Das ist fuer Auslieferungen nicht zulaessig.'
} else {
    Invoke-Checked 'dotnet' @(
        'test', $solution,
        '--configuration', $Configuration,
        '--no-build'
    ) 'Die automatisierten Tests'
}

# ---------------------------------------------------------------------------
# 5. Veroeffentlichen
# ---------------------------------------------------------------------------
Write-Step 5 'Programme veroeffentlichen'

$projects = @(
    @{ Name = 'Vehistra';         Path = 'src\Vehistra.Client\Vehistra.Client.csproj' },
    @{ Name = 'Vehistra.Updater'; Path = 'src\Vehistra.Updater\Vehistra.Updater.csproj' },
    @{ Name = 'VehistraServerSetup';     Path = 'src\Vehistra.ServerSetup\Vehistra.ServerSetup.csproj' },
    @{ Name = 'VehistraServerCheck';     Path = 'src\Vehistra.ServerCheck\Vehistra.ServerCheck.csproj' }
)

foreach ($project in $projects) {
    Write-Host "     $($project.Name) ..."

    Invoke-Checked 'dotnet' @(
        'publish', (Join-Path $repositoryRoot $project.Path),
        '--configuration', $Configuration,
        '--runtime', $Runtime,
        '--self-contained', 'false',
        '--output', $publishDir,
        '-p:PublishSingleFile=false',
        '-p:DebugType=embedded',
        "-p:Version=$Version",
        "-p:FileVersion=$Version.0",
        "-p:AssemblyVersion=$Version.0"
    ) "Die Veroeffentlichung von $($project.Name)"
}

# ---------------------------------------------------------------------------
# 6. Anleitungen
# ---------------------------------------------------------------------------
Write-Step 6 'Anleitungen erzeugen'

$docBuilder = Join-Path $repositoryRoot 'tools\DocBuilder\Vehistra.DocBuilder.csproj'
$docsTarget = Join-Path $publishDir 'Dokumentation'
New-Item -ItemType Directory -Path $docsTarget -Force | Out-Null

if (Test-Path $docBuilder) {
    Invoke-Checked 'dotnet' @(
        'run', '--project', $docBuilder,
        '--configuration', $Configuration,
        '--', $docsSource, $docsTarget, $Version
    ) 'Die Erzeugung der Anleitungen'
} else {
    Write-Warning 'tools\DocBuilder wurde nicht gefunden - es werden nur die Markdown-Dateien kopiert.'
    Get-ChildItem $docsSource -Filter '*.md' | Copy-Item -Destination $docsTarget -Force
}

Get-ChildItem $docsTarget | ForEach-Object { Write-Host "     $($_.Name)" }

# ---------------------------------------------------------------------------
# 7. Installationspakete
# ---------------------------------------------------------------------------
Write-Step 7 'Installationspakete bauen'

if ($iscc) {
    foreach ($script in @('Vehistra-Setup.iss', 'Vehistra-Update.iss')) {
        $arguments = @(
            "/DAppVersion=$Version",
            "/DSourceDir=$publishDir",
            "/DOutputDir=$installerOut"
        )

        if ($SignTool) { $arguments += "/DSignTool=$SignTool" }
        $arguments += (Join-Path $installerDir $script)

        Invoke-Checked $iscc $arguments "Das Uebersetzen von $script"
    }
} else {
    Write-Warning 'Ohne Inno Setup koennen Vehistra-Setup.exe und Vehistra-Update.exe nicht gebaut werden.'
}

# ---------------------------------------------------------------------------
# 8. Releaseordner zusammenstellen
# ---------------------------------------------------------------------------
Write-Step 8 'Releaseordner zusammenstellen'

New-Item -ItemType Directory -Path (Join-Path $releaseDir 'Dokumentation') -Force | Out-Null

# Installationspakete
foreach ($file in @('Vehistra-Setup.exe', 'Vehistra-Update.exe')) {
    $source = Join-Path $installerOut $file
    if (Test-Path $source) {
        Copy-Item $source (Join-Path $releaseDir $file) -Force
        Write-Host "     $file"
    } else {
        Write-Warning "     $file fehlt (Inno Setup nicht ausgefuehrt)."
    }
}

# Serverwerkzeuge werden einzeln ausgeliefert - sie laufen direkt auf dem Server.
$serverTools = Join-Path $releaseDir 'Serverwerkzeuge'
New-Item -ItemType Directory -Path $serverTools -Force | Out-Null
Copy-Item (Join-Path $publishDir '*') $serverTools -Recurse -Force
Get-ChildItem $serverTools -Filter 'Dokumentation' -Directory | Remove-Item -Recurse -Force
Write-Host '     Serverwerkzeuge\ (VehistraServerSetup.exe, VehistraServerCheck.exe)'

# Anleitungen
Copy-Item (Join-Path $docsTarget '*') (Join-Path $releaseDir 'Dokumentation') -Force
Write-Host '     Dokumentation\'

# Versionshinweise
$releaseNotesTarget = Join-Path $releaseDir 'release-notes.txt'

if ($ReleaseNotesFile -and (Test-Path $ReleaseNotesFile)) {
    Copy-Item $ReleaseNotesFile $releaseNotesTarget -Force
} else {
    Write-Warning 'Keine Versionshinweise uebergeben - es wird eine Vorlage erzeugt. Bitte vor der Auslieferung ausfuellen.'

    @"
VEHISTRA $Version
Veroeffentlicht am $(Get-Date -Format 'dd.MM.yyyy')
Entwickelt von LSP Virtual Services - vehistra.dev

NEU
- (bitte ausfuellen)

VERBESSERT
- (bitte ausfuellen)

BEHOBEN
- (bitte ausfuellen)

HINWEISE ZUM UPDATE
- Vor jeder Datenbankaenderung wird automatisch eine Sicherung erstellt.
- Schlaegt die Sicherung fehl, wird das Update abgebrochen.
- Es werden keine Daten geloescht; bestehende Daten bleiben vollstaendig erhalten.
"@ | Set-Content $releaseNotesTarget -Encoding UTF8
}

Write-Host '     release-notes.txt'

# Beispielkonfiguration - enthaelt NIEMALS Passwoerter
$sampleConfig = Join-Path $releaseDir 'Fuhrpark-Firmenkonfiguration-Beispiel.fmcfg'

@"
{
  "formatVersion": 1,
  "createdAt": "$(Get-Date -Format 'yyyy-MM-ddTHH:mm:ss')",
  "createdBy": "CreateRelease.ps1",
  "hinweis": "Diese Datei enthaelt bewusst keine Passwoerter. Sie wird vom Server-Setup erzeugt und beim Einrichten neuer Arbeitsplaetze ausgewaehlt.",
  "server": "FUHRPARK-SRV01\\SQLEXPRESS",
  "database": "VehistraDB",
  "useWindowsAuthentication": true,
  "documentsPath": "\\\\FUHRPARK-SRV01\\Fuhrpark\\Dokumente",
  "updatePath": "\\\\FUHRPARK-SRV01\\Fuhrpark\\Updates",
  "connectTimeoutSeconds": 15,
  "commandTimeoutSeconds": 60,
  "trustServerCertificate": true,
  "encrypt": true
}
"@ | Set-Content $sampleConfig -Encoding UTF8

Write-Host '     Fuhrpark-Firmenkonfiguration-Beispiel.fmcfg'

# ---------------------------------------------------------------------------
# 9. Pruefsummen und latest.json
# ---------------------------------------------------------------------------
Write-Step 9 'Pruefsummen und latest.json erzeugen'

$checksumFile = Join-Path $releaseDir 'checksums.sha256'
$lines = @()

Get-ChildItem $releaseDir -Recurse -File |
    Where-Object { $_.Name -ne 'checksums.sha256' } |
    Sort-Object FullName |
    ForEach-Object {
        $relative = $_.FullName.Substring($releaseDir.Length + 1).Replace('\', '/')
        $hash = (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        $lines += "$hash  $relative"
    }

$lines | Set-Content $checksumFile -Encoding ASCII
Write-Host "     checksums.sha256 ($($lines.Count) Eintraege)"

$updatePackage = Join-Path $releaseDir 'Vehistra-Update.exe'
$updateChecksum = if (Test-Path $updatePackage) {
    (Get-FileHash $updatePackage -Algorithm SHA256).Hash.ToLowerInvariant()
} else {
    ''
}

$manifest = [ordered]@{
    version              = $Version
    minimumVersion       = $MinimumVersion
    installer            = "$Version/Vehistra-Update.exe"
    releaseNotes         = "$Version/release-notes.txt"
    checksum             = $updateChecksum
    mandatory            = [bool] $Mandatory
    releasedAt           = (Get-Date -Format 'yyyy-MM-ddTHH:mm:ss')
    minimumDatabaseVersion = $null
}

$manifestPath = Join-Path $releaseDir 'latest.json'
$manifest | ConvertTo-Json -Depth 4 | Set-Content $manifestPath -Encoding UTF8
Write-Host '     latest.json'

# ---------------------------------------------------------------------------
# Abschluss
# ---------------------------------------------------------------------------
Write-Host ''
Write-Host '===========================================================' -ForegroundColor Green
Write-Host " VERSION $Version WURDE ERSTELLT" -ForegroundColor Green
Write-Host '===========================================================' -ForegroundColor Green
Write-Host ''
Write-Host "Ordner: $releaseDir"
Write-Host ''
Write-Host 'Naechste Schritte fuer die Auslieferung:'
Write-Host "  1. release-notes.txt pruefen und fertigstellen"
Write-Host "  2. Ordner Release\$Version nach \\FUHRPARK-SRV01\Fuhrpark\Updates\$Version kopieren"
Write-Host "  3. latest.json aus diesem Ordner in das Wurzelverzeichnis der Updateablage kopieren"
Write-Host "     (\\FUHRPARK-SRV01\Fuhrpark\Updates\latest.json)"
Write-Host "  4. Die Arbeitsplaetze melden das Update beim naechsten Programmstart"
Write-Host ''
Write-Host 'Die Fuhrparkdatenbank wird durch ein Update niemals geloescht oder neu erstellt.'
Write-Host ''
