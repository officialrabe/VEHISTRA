; ---------------------------------------------------------------------------
;  Vehistra - gemeinsame Definitionen der Installationsskripte
;  Entwickelt von LSP Virtual Services (vehistra.dev)
;
;  Diese Datei wird von Vehistra-Setup.iss und Vehistra-Update.iss
;  eingebunden. Sie enthaelt Herstellerangaben, Verzeichnisse und die
;  gemeinsamen Hilfsfunktionen.
; ---------------------------------------------------------------------------

#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif

#ifndef SourceDir
  ; Standardmaessig das Veroeffentlichungsverzeichnis aus CreateRelease.ps1
  #define SourceDir "..\..\artifacts\publish"
#endif

#ifndef OutputDir
  #define OutputDir "..\..\artifacts\installer"
#endif

#define AppName            "Vehistra"
#define AppPublisher       "LSP Virtual Services"
#define AppPublisherUrl    "https://vehistra.dev"
#define AppSupportUrl      "https://vehistra.dev"
#define AppUpdatesUrl      "https://vehistra.dev"
#define AppCopyright       "© LSP Virtual Services"
#define AppExeName         "Vehistra.exe"
#define AppMutexName       "Global\LSPVirtualServices.Vehistra"
; In [Setup] wird die doppelte Klammer benoetigt - Inno macht daraus ein "{".
#define AppId              "{{8D3B4E21-7C4A-4E2F-9B6D-5A1C0E7F3D42}"
; Fuer Zeichenketten im [Code]-Abschnitt, wo nicht entschluesselt wird.
#define AppIdRaw           "{8D3B4E21-7C4A-4E2F-9B6D-5A1C0E7F3D42}"

; Veraenderliche Daten liegen niemals unterhalb von "Program Files".
#define CommonDataDir      "{commonappdata}\LSP Virtual Services\Vehistra"

[Code]
{ --------------------------------------------------------------------------
  Prueft, ob die benoetigte .NET-Desktop-Runtime installiert ist.
  Der Installer bricht nicht ab, sondern erklaert verstaendlich, was fehlt.
  -------------------------------------------------------------------------- }
function IsDotNetDesktopRuntimeInstalled(): Boolean;
var
  Names: TArrayOfString;
  Index: Integer;
  RootKey32, RootKey64: String;
begin
  Result := False;

  RootKey64 := 'SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App';
  RootKey32 := 'SOFTWARE\WOW6432Node\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App';

  if RegGetValueNames(HKEY_LOCAL_MACHINE, RootKey64, Names) or
     RegGetValueNames(HKEY_LOCAL_MACHINE, RootKey32, Names) then
  begin
    for Index := 0 to GetArrayLength(Names) - 1 do
    begin
      if Pos('10.', Names[Index]) = 1 then
      begin
        Result := True;
        Exit;
      end;
    end;
  end;

  { Alternativ das Verzeichnis der gemeinsamen Laufzeit pruefen. }
  if DirExists(ExpandConstant('{commonpf64}\dotnet\shared\Microsoft.WindowsDesktop.App')) then
    Result := True;
end;

{ --------------------------------------------------------------------------
  Legt die Datenverzeichnisse unterhalb von ProgramData an und erteilt allen
  Benutzern Schreibrechte auf das Protokollverzeichnis.
  -------------------------------------------------------------------------- }
procedure CreateCommonDataDirectories();
var
  Base: String;
begin
  Base := ExpandConstant('{commonappdata}\LSP Virtual Services\Vehistra');

  ForceDirectories(Base);
  ForceDirectories(Base + '\Logs');
  ForceDirectories(Base + '\UpdateBackup');
end;
