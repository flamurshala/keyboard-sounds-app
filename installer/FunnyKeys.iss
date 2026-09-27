#define AppVersion "1.0.2"
[Setup]
AppId={{F561AD53-A4A1-468A-AB65-492BAD2A0120}
AppName=FunnyKeys
AppVersion={#AppVersion}
AppPublisher=FunnyKeys
DefaultDirName={autopf}\FunnyKeys
DefaultGroupName=FunnyKeys
DisableProgramGroupPage=yes
OutputDir=..\dist
OutputBaseFilename=FunnyKeys-Setup
SetupIconFile=..\src\FunnyKeys\Assets\Icons\FunnyKeys.ico
UninstallDisplayIcon={app}\FunnyKeys.exe
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=dialog commandline
CloseApplications=yes
RestartApplications=no
AppMutex=Local\FunnyKeys.Desktop
UninstallDisplayName=FunnyKeys

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Shortcuts:"; Flags: unchecked

[Files]
Source: "..\dist\app\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\FunnyKeys"; Filename: "{app}\FunnyKeys.exe"
Name: "{autodesktop}\FunnyKeys"; Filename: "{app}\FunnyKeys.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\FunnyKeys.exe"; Description: "Launch FunnyKeys"; Flags: nowait postinstall skipifsilent runasoriginaluser

[Code]
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  Command: String;
begin
  if CurUninstallStep = usUninstall then
    if RegQueryStringValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'FunnyKeys', Command) then
      if Pos('"' + ExpandConstant('{app}\FunnyKeys.exe') + '"', Command) = 1 then
        RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'FunnyKeys');
end;

