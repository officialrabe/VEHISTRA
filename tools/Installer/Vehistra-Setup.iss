; ---------------------------------------------------------------------------
;  Vehistra-Setup.exe - Erstinstallation an einem Arbeitsplatz
;  Entwickelt von LSP Virtual Services (vehistra.dev)
;
;  Uebersetzen:
;    ISCC.exe /DAppVersion=1.0.0 /DSourceDir=..\..\artifacts\publish ^
;             /DOutputDir=..\..\artifacts\installer Vehistra-Setup.iss
;
;  Optional fuer die Codesignatur (das Zertifikat liegt NIEMALS im Repository):
;    /DSignTool=lspsign
; ---------------------------------------------------------------------------

#include "Gemeinsam.iss"

[Setup]
AppId={#AppId}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
VersionInfoVersion={#AppVersion}
VersionInfoCompany={#AppPublisher}
VersionInfoDescription=Vehistra - Arbeitsplatzinstallation
VersionInfoCopyright={#AppCopyright}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppPublisherUrl}
AppSupportURL={#AppSupportUrl}
AppUpdatesURL={#AppUpdatesUrl}
AppCopyright={#AppCopyright}

DefaultDirName={autopf}\LSP Virtual Services\Vehistra
DefaultGroupName=Vehistra
DisableProgramGroupPage=yes
DisableDirPage=no
AllowNoIcons=yes

OutputDir={#OutputDir}
OutputBaseFilename=Vehistra-Setup
SetupIconFile=..\..\assets\Vehistra.ico
UninstallDisplayName={#AppName} {#AppVersion}
UninstallDisplayIcon={app}\{#AppExeName}

Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64compatible
ArchitecturesAllowed=x64compatible
PrivilegesRequired=admin
MinVersion=10.0
CloseApplications=yes
CloseApplicationsFilter=*.exe,*.dll
RestartApplications=no
AppMutex={#AppMutexName}

#ifdef SignTool
SignTool={#SignTool}
SignedUninstaller=yes
#endif

[Languages]
Name: "deutsch"; MessagesFile: "compiler:Languages\German.isl"

[Tasks]
Name: "desktopicon"; Description: "Verknüpfung auf dem Desktop anlegen"; GroupDescription: "Zusätzliche Verknüpfungen"
Name: "startmenuicon"; Description: "Verknüpfung im Startmenü anlegen"; GroupDescription: "Zusätzliche Verknüpfungen"; Flags: checkedonce

[Dirs]
; Veraenderliche Daten liegen ausschliesslich unterhalb von ProgramData.
Name: "{commonappdata}\LSP Virtual Services"; Flags: uninsneveruninstall
Name: "{commonappdata}\LSP Virtual Services\Vehistra"; Flags: uninsneveruninstall
Name: "{commonappdata}\LSP Virtual Services\Vehistra\Logs"; Permissions: users-modify; Flags: uninsneveruninstall
Name: "{commonappdata}\LSP Virtual Services\Vehistra\UpdateBackup"; Permissions: users-modify; Flags: uninsneveruninstall

[Files]
; Hauptanwendung
Source: "{#SourceDir}\Vehistra.exe";          DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceDir}\Vehistra.Updater.exe";  DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceDir}\VehistraServerCheck.exe";      DestDir: "{app}"; Flags: ignoreversion

; Laufzeitdateien und Bibliotheken
Source: "{#SourceDir}\*.dll";                        DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceDir}\*.json";                       DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceDir}\runtimes\*";                   DestDir: "{app}\runtimes"; Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist
Source: "{#SourceDir}\de\*";                         DestDir: "{app}\de"; Flags: ignoreversion recursesubdirs skipifsourcedoesntexist

; Anleitungen als PDF - werden im Startmenue verlinkt
Source: "{#SourceDir}\Dokumentation\*";              DestDir: "{app}\Dokumentation"; Flags: ignoreversion recursesubdirs skipifsourcedoesntexist

[Icons]
Name: "{group}\Vehistra"; Filename: "{app}\{#AppExeName}"; WorkingDir: "{app}"; Comment: "Vehistra starten"; Tasks: startmenuicon
Name: "{group}\Serverprüfung"; Filename: "{app}\VehistraServerCheck.exe"; WorkingDir: "{app}"; Comment: "Verbindung und Server prüfen"; Tasks: startmenuicon
Name: "{group}\Anleitungen"; Filename: "{app}\Dokumentation"; Tasks: startmenuicon
Name: "{group}\{cm:UninstallProgram,Vehistra}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\Vehistra"; Filename: "{app}\{#AppExeName}"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "Vehistra jetzt starten"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Der Programmordner wird geleert - Konfiguration, Protokolle und Sicherungen
; unter ProgramData bleiben bewusst erhalten.
Type: filesandordirs; Name: "{app}\runtimes"
Type: filesandordirs; Name: "{app}\Dokumentation"

[Messages]
deutsch.WelcomeLabel2=Dieser Assistent installiert Vehistra {#AppVersion} auf diesem Computer.%n%nDas Programm arbeitet mit der zentralen Fuhrparkdatenbank auf dem Firmenserver. Beim ersten Start wählen Sie einmalig die Serververbindung aus - alternativ die vom Server erzeugte Datei Fuhrpark-Firmenkonfiguration.fmcfg.%n%nEntwickelt von LSP Virtual Services.
deutsch.FinishedLabel=Vehistra wurde installiert.%n%nBeim ersten Start werden Sie nach der Serververbindung gefragt. Die Anleitung "NEUEN-PC-IN-5-MINUTEN.pdf" liegt im Startmenü unter "Anleitungen".

[Code]
var
  RuntimeWarningShown: Boolean;

function InitializeSetup(): Boolean;
begin
  Result := True;
  RuntimeWarningShown := False;

  if not IsDotNetDesktopRuntimeInstalled() then
  begin
    RuntimeWarningShown := True;

    if MsgBox(
      'Auf diesem Computer wurde die benötigte .NET Desktop Runtime 10 nicht gefunden.' + #13#10 + #13#10 +
      'Vehistra benötigt diese Laufzeitumgebung von Microsoft. Sie ist kostenlos und ' +
      'wird einmalig je Computer installiert.' + #13#10 + #13#10 +
      'So gehen Sie vor:' + #13#10 +
      '1. Öffnen Sie https://dotnet.microsoft.com/download/dotnet/10.0' + #13#10 +
      '2. Laden Sie unter "Desktop Runtime" die Variante "Windows x64" herunter' + #13#10 +
      '3. Installieren Sie die Datei und starten Sie dieses Setup danach erneut' + #13#10 + #13#10 +
      'Möchten Sie die Installation trotzdem fortsetzen?',
      mbConfirmation, MB_YESNO) = IDNO then
    begin
      Result := False;
    end;
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    CreateCommonDataDirectories();
  end;
end;

function InitializeUninstall(): Boolean;
begin
  Result := True;

  MsgBox(
    'Beim Entfernen des Programms bleiben erhalten:' + #13#10 + #13#10 +
    '• die zentrale Fuhrparkdatenbank auf dem Server' + #13#10 +
    '• alle Fahrzeugdokumente in der Dokumentenablage' + #13#10 +
    '• die Serverkonfiguration und die Protokolle unter' + #13#10 +
    '  C:\ProgramData\LSP Virtual Services\Vehistra' + #13#10 + #13#10 +
    'Es werden ausschließlich die Programmdateien dieses Computers entfernt.',
    mbInformation, MB_OK);
end;
