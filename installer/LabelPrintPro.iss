; LabelPrint Pro — Inno Setup 6 script
; Build via scripts/pack-release.ps1 (passes /DMyAppVersion=x.y.z)

#ifndef MyAppVersion
  #define MyAppVersion "0.8.0"
#endif

#define MyAppName "LabelPrint Pro"
#define MyAppPublisher "LabelPrint Pro"
#define MyAppURL "https://github.com/x1nktw/Printer_Lable"
#define MyAppExeName "LabelPrint.UI.exe"
#define MyAppId "{{8F3C2A91-6E4B-4D7A-9C1E-2B5F8D0A4E73}}"
; FrontPad Bridge shipped under extensions\frontpad-bridge (manifest 1.3.4)

#ifndef PublishDir
  #define PublishDir "..\artifacts\publish\LabelPrintPro"
#endif

#ifndef OutputDir
  #define OutputDir "..\artifacts\release"
#endif

[Setup]
AppId={#MyAppId}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}/issues
AppUpdatesURL={#MyAppURL}/releases
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
LicenseFile=
InfoBeforeFile=
OutputDir={#OutputDir}
OutputBaseFilename=LabelPrintPro-{#MyAppVersion}-win-x64-setup
SetupIconFile=..\src\LabelPrint.UI\Assets\app-icon.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=dialog
CloseApplications=yes
RestartApplications=no
ChangesAssociations=no
VersionInfoVersion={#MyAppVersion}.0
VersionInfoCompany={#MyAppPublisher}
VersionInfoProductName={#MyAppName}
VersionInfoProductVersion={#MyAppVersion}

[Languages]
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: checkedonce

[Files]
; Full published layout: exe, config/, plugins/, extensions/frontpad-bridge/
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"
Name: "{group}\FrontPad Bridge (INSTALL.txt)"; Filename: "{app}\extensions\frontpad-bridge\INSTALL.txt"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent
Filename: "{app}\extensions\frontpad-bridge\INSTALL.txt"; Description: "Open FrontPad Bridge install notes"; Flags: postinstall shellexec skipifsilent unchecked

[Code]
function InitializeSetup(): Boolean;
begin
  Result := True;
end;
