; ---------------------------------------------------------------------------
;  Vehistra-Update.exe - Updatepaket fuer bereits installierte Arbeitsplaetze
;  Entwickelt von LSP Virtual Services (vehistra.dev)
;
;  Dieses Paket wird von Vehistra.Updater.exe still aufgerufen:
;    Vehistra-Update.exe /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /DIR="<Programmordner>"
;
;  Es tauscht ausschliesslich die Programmdateien aus. Die Datenbank wird
;  NICHT geloescht und NICHT neu erstellt - die Datenbankaenderungen fuehrt
;  anschliessend Vehistra.Updater.exe per EF-Core-Migration durch,
;  nachdem eine Sicherung erstellt wurde.
;
;  Uebersetzen:
;    ISCC.exe /DAppVersion=1.1.0 /DSourceDir=..\..\artifacts\publish ^
;             /DOutputDir=..\..\artifacts\installer Vehistra-Update.iss
; ---------------------------------------------------------------------------

#include "Gemeinsam.iss"

[Setup]
; Dieselbe AppId wie die Erstinstallation - damit wird aktualisiert statt parallel installiert.
AppId={#AppId}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
VersionInfoVersion={#AppVersion}
VersionInfoCompany={#AppPublisher}
VersionInfoDescription=Vehistra - Programmupdate
VersionInfoCopyright={#AppCopyright}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppPublisherUrl}
AppSupportURL={#AppSupportUrl}
AppUpdatesURL={#AppUpdatesUrl}
AppCopyright={#AppCopyright}

DefaultDirName={autopf}\LSP Virtual Services\Vehistra
DefaultGroupName=Vehistra
UsePreviousAppDir=yes
UsePreviousGroup=yes
UsePreviousTasks=yes
DisableDirPage=yes
DisableProgramGroupPage=yes
DisableReadyPage=yes
DisableWelcomePage=yes
DisableFinishedPage=yes

OutputDir={#OutputDir}
OutputBaseFilename=Vehistra-Update
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
RestartApplications=no
AppMutex={#AppMutexName}

#ifdef SignTool
SignTool={#SignTool}
SignedUninstaller=yes
#endif

[Languages]
Name: "deutsch"; MessagesFile: "compiler:Languages\German.isl"

[Dirs]
Name: "{commonappdata}\LSP Virtual Services\Vehistra"; Flags: uninsneveruninstall
Name: "{commonappdata}\LSP Virtual Services\Vehistra\Logs"; Permissions: users-modify; Flags: uninsneveruninstall
Name: "{commonappdata}\LSP Virtual Services\Vehistra\UpdateBackup"; Permissions: users-modify; Flags: uninsneveruninstall

[Files]
Source: "{#SourceDir}\Vehistra.exe";          DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceDir}\Vehistra.Updater.exe";  DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceDir}\VehistraServerCheck.exe";      DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceDir}\*.dll";                        DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceDir}\*.json";                       DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceDir}\runtimes\*";                   DestDir: "{app}\runtimes"; Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist
Source: "{#SourceDir}\de\*";                         DestDir: "{app}\de"; Flags: ignoreversion recursesubdirs skipifsourcedoesntexist
Source: "{#SourceDir}\Dokumentation\*";              DestDir: "{app}\Dokumentation"; Flags: ignoreversion recursesubdirs skipifsourcedoesntexist

[Icons]
Name: "{group}\Vehistra"; Filename: "{app}\{#AppExeName}"; WorkingDir: "{app}"
Name: "{group}\Serverprüfung"; Filename: "{app}\VehistraServerCheck.exe"; WorkingDir: "{app}"
Name: "{group}\Anleitungen"; Filename: "{app}\Dokumentation"
Name: "{group}\{cm:UninstallProgram,Vehistra}"; Filename: "{uninstallexe}"

[Code]
{ --------------------------------------------------------------------------
  Das Updatepaket setzt eine vorhandene Installation voraus. Wird es von Hand
  auf einem Computer ohne Vehistra gestartet, erklaert es das
  verstaendlich und verweist auf Vehistra-Setup.exe.
  -------------------------------------------------------------------------- }
function GetInstalledLocation(): String;
var
  Key: String;
begin
  Result := '';
  Key := 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{#AppId}_is1';

  if not RegQueryStringValue(HKEY_LOCAL_MACHINE, Key, 'InstallLocation', Result) then
    RegQueryStringValue(HKEY_CURRENT_USER, Key, 'InstallLocation', Result);
end;

function InitializeSetup(): Boolean;
var
  Location: String;
begin
  Result := True;
  Location := GetInstalledLocation();

  if Location = '' then
  begin
    if WizardSilent() then
    begin
      { Im stillen Lauf durch den Updater wird der Zielordner per /DIR uebergeben. }
      Exit;
    end;

    MsgBox(
      'Auf diesem Computer ist Vehistra noch nicht installiert.' + #13#10 + #13#10 +
      'Dieses Paket aktualisiert nur eine vorhandene Installation.' + #13#10 +
      'Für die Erstinstallation verwenden Sie bitte Vehistra-Setup.exe.',
      mbInformation, MB_OK);

    Result := False;
    Exit;
  end;

  if not WizardSilent() then
  begin
    if MsgBox(
      'Vehistra wird auf Version {#AppVersion} aktualisiert.' + #13#10 + #13#10 +
      'Es werden ausschließlich die Programmdateien ausgetauscht.' + #13#10 +
      'Die Fuhrparkdatenbank auf dem Server bleibt vollständig erhalten;' + #13#10 +
      'notwendige Datenbankanpassungen erfolgen anschließend automatisch' + #13#10 +
      'und immer erst nach einer Sicherung.' + #13#10 + #13#10 +
      'Möchten Sie das Update jetzt durchführen?',
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
