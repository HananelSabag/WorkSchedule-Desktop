; ============================================================
;  סידור עבודה - Inno Setup Installer Script
;  Author : Hananel Sabag
;  Version: 1.0.0
; ============================================================

#define AppName      "Work Schedule"
#define AppNameHe    "סידור עבודה"
#define AppVersion   "1.0.0"
#define AppPublisher "Hananel Sabag"
#define AppExe       "WorkSchedule.exe"
#define AppIcon      "..\WorkSchedule\logo.ico"
#define PublishDir   "..\publish\win-x64"

[Setup]
AppId={{A3F7C2D1-9E4B-4A56-B8F0-2C1D3E5A7B9C}
AppName={#AppNameHe}
AppVersion={#AppVersion}
AppVerName={#AppNameHe} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL=https://github.com/hananel12345
AppSupportURL=https://github.com/hananel12345
VersionInfoVersion={#AppVersion}
VersionInfoCompany={#AppPublisher}
VersionInfoDescription={#AppNameHe}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppNameHe}
DisableProgramGroupPage=yes
OutputDir=Output
OutputBaseFilename=WorkSchedule-Setup-{#AppVersion}
SetupIconFile={#AppIcon}
Compression=lzma2/ultra64
SolidCompression=yes
LZMAUseSeparateProcess=yes
WizardStyle=modern
ShowLanguageDialog=no
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
AllowNoIcons=yes
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppNameHe}
MinVersion=10.0.17763

[Languages]
Name: "he"; MessagesFile: "compiler:Languages\Hebrew.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "{#PublishDir}\{#AppExe}"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "*.pdb"

[Icons]
Name: "{group}\{#AppNameHe}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppNameHe}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppNameHe}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{localappdata}\WorkSchedule"
