; ============================================================
;  סידור עבודה — Inno Setup Installer Script
;  Author : Hananel Sabag
;  Version: 1.0.0
; ============================================================

#define AppName      "סידור עבודה"
#define AppNameEn    "WorkSchedule"
#define AppVersion   "1.0.0"
#define AppPublisher "Hananel Sabag"
#define AppExe       "WorkSchedule.exe"
#define AppIcon      "..\WorkSchedule\logo.ico"
#define PublishDir   "..\publish\win-x64"

[Setup]
; Basic info
AppId                    = {{A3F7C2D1-9E4B-4A56-B8F0-2C1D3E5A7B9C}
AppName                  = {#AppName}
AppVersion               = {#AppVersion}
AppPublisher             = {#AppPublisher}
AppPublisherURL          = https://github.com/hananel12345
AppSupportURL            = https://github.com/hananel12345
VersionInfoVersion       = {#AppVersion}
VersionInfoCompany       = {#AppPublisher}
VersionInfoDescription   = {#AppName} - ניהול סידור עבודה שבועי

; Install location
DefaultDirName           = {autopf}\{#AppNameEn}
DefaultGroupName         = {#AppName}
DisableProgramGroupPage  = yes

; Output
OutputDir                = Output
OutputBaseFilename       = WorkSchedule-Setup-{#AppVersion}
SetupIconFile            = {#AppIcon}

; Compression
Compression              = lzma2/ultra64
SolidCompression         = yes
LZMAUseSeparateProcess   = yes

; Appearance
WizardStyle              = modern
WizardSmallImageFile     = wizard_small.bmp
ShowLanguageDialog       = no

; Require admin rights for Program Files install
PrivilegesRequired       = lowest
PrivilegesRequiredOverridesAllowed = dialog

; Misc
AllowNoIcons             = yes
UninstallDisplayIcon     = {app}\{#AppExe}
UninstallDisplayName     = {#AppName}

; Minimum Windows 10
MinVersion               = 10.0.17763

[Languages]
Name: "he"; MessagesFile: "compiler:Languages\Hebrew.isl"; \
  LicenseFile: ""; \
  InfoBeforeFile: ""; \
  InfoAfterFile: ""
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"
Name: "startmenuicon"; Description: "הוסף לתפריט התחל"; GroupDescription: "{cm:AdditionalIcons}"; Flags: checked

[Files]
; Main executable (self-contained single file)
Source: "{#PublishDir}\{#AppExe}";   DestDir: "{app}"; Flags: ignoreversion

; Any extra files produced by publish (runtimes, native libs)
Source: "{#PublishDir}\*";           DestDir: "{app}"; \
  Flags: ignoreversion recursesubdirs createallsubdirs; \
  Excludes: "*.pdb"

[Icons]
; Start Menu
Name: "{group}\{#AppName}"; \
  Filename: "{app}\{#AppExe}"; \
  IconFilename: "{app}\{#AppExe}"; \
  Tasks: startmenuicon

; Desktop
Name: "{autodesktop}\{#AppName}"; \
  Filename: "{app}\{#AppExe}"; \
  IconFilename: "{app}\{#AppExe}"; \
  Tasks: desktopicon

[Run]
; Optional: launch app after install
Filename: "{app}\{#AppExe}"; \
  Description: "{cm:LaunchProgram,{#StringChange(AppName, '&', '&&')}}"; \
  Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Clean up AppData folder created by the app on uninstall
Type: filesandordirs; Name: "{localappdata}\WorkSchedule"

[Code]
// Remove previous version before installing
procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssInstall then
  begin
    // Nothing special needed — single-file replaces itself
  end;
end;
