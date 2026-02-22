#define MyAppName "HA PC Remote Service"
#define MyAppVersion GetEnv("APP_VERSION")
#if MyAppVersion == ""
  #define MyAppVersion "0.0.0"
#endif
#define MyAppPublisher "NeskireDK"
#define MyAppURL "https://github.com/NeskireDK/ha-pc-remote-service"
#define MyAppExeName "HaPcRemote.Service.exe"
#define TrayExeName "HaPcRemote.Tray.exe"
#define ServiceName "HaPcRemoteService"
#define ServiceDisplayName "HA PC Remote Service"
#define ServiceDescription "Home Assistant PC Remote Service"
#define ServicePort "5000"

[Setup]
AppId={{7A3B5C1D-9E2F-4A6B-8C0D-1E3F5A7B9C2D}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}/issues
DefaultDirName={autopf}\{#MyAppName}
DisableProgramGroupPage=yes
OutputBaseFilename=HaPcRemoteService-Setup-{#MyAppVersion}
Compression=lzma
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
SetupIconFile=..\resources\windows\pcremote.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
WizardStyle=modern

[Files]
Source: "..\publish\win-x64\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\publish\tray\{#TrayExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\src\HaPcRemote.Service\appsettings.json"; DestDir: "{app}"; Flags: onlyifdoesntexist

[Dirs]
Name: "{app}\tools"
Name: "{app}\monitor-profiles"

[Icons]
Name: "{commonstartup}\HA PC Remote Tray"; Filename: "{app}\{#TrayExeName}"; Comment: "HA PC Remote system tray helper"

[Run]
Filename: "{app}\{#TrayExeName}"; Flags: nowait runasoriginaluser

[Code]
const
  SERVICE_QUERY_CONFIG  = $0001;
  DOTNET_DESKTOP_URL    = 'https://aka.ms/dotnet/10.0/windowsdesktop-runtime-win-x64.exe';
  DOTNET_REG_KEY        = 'SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App';
  SERVICE_ALL_ACCESS    = $F01FF;
  SC_MANAGER_ALL_ACCESS = $F003F;
  SERVICE_CONTROL_STOP  = $1;
  SERVICE_STOPPED       = $1;

type
  SERVICE_STATUS = record
    dwServiceType: DWORD;
    dwCurrentState: DWORD;
    dwControlsAccepted: DWORD;
    dwWin32ExitCode: DWORD;
    dwServiceSpecificExitCode: DWORD;
    dwCheckPoint: DWORD;
    dwWaitHint: DWORD;
  end;

function OpenSCManager(lpMachineName, lpDatabaseName: String;
  dwDesiredAccess: DWORD): THandle;
  external 'OpenSCManagerW@advapi32.dll stdcall';
function OpenService(hSCManager: THandle; lpServiceName: String;
  dwDesiredAccess: DWORD): THandle;
  external 'OpenServiceW@advapi32.dll stdcall';
function CloseServiceHandle(hSCObject: THandle): Boolean;
  external 'CloseServiceHandle@advapi32.dll stdcall';
function ControlService(hService: THandle; dwControl: DWORD;
  var lpServiceStatus: SERVICE_STATUS): Boolean;
  external 'ControlService@advapi32.dll stdcall';
function QueryServiceStatus(hService: THandle;
  var lpServiceStatus: SERVICE_STATUS): Boolean;
  external 'QueryServiceStatus@advapi32.dll stdcall';
function DeleteService(hService: THandle): Boolean;
  external 'DeleteService@advapi32.dll stdcall';

function IsServiceInstalled: Boolean;
var
  hSCM, hSvc: THandle;
begin
  Result := False;
  hSCM := OpenSCManager('', '', SC_MANAGER_ALL_ACCESS);
  if hSCM <> 0 then begin
    hSvc := OpenService(hSCM, '{#ServiceName}', SERVICE_QUERY_CONFIG);
    if hSvc <> 0 then begin
      Result := True;
      CloseServiceHandle(hSvc);
    end;
    CloseServiceHandle(hSCM);
  end;
end;

function StopServiceAndWait: Boolean;
var
  hSCM, hSvc: THandle;
  Status: SERVICE_STATUS;
  Attempts: Integer;
begin
  Result := False;
  hSCM := OpenSCManager('', '', SC_MANAGER_ALL_ACCESS);
  if hSCM <> 0 then begin
    hSvc := OpenService(hSCM, '{#ServiceName}', SERVICE_ALL_ACCESS);
    if hSvc <> 0 then begin
      ControlService(hSvc, SERVICE_CONTROL_STOP, Status);
      Attempts := 0;
      repeat
        Sleep(1000);
        QueryServiceStatus(hSvc, Status);
        Inc(Attempts);
      until (Status.dwCurrentState = SERVICE_STOPPED) or (Attempts >= 30);
      Result := (Status.dwCurrentState = SERVICE_STOPPED);
      CloseServiceHandle(hSvc);
    end;
    CloseServiceHandle(hSCM);
  end;
end;

procedure RemoveServiceEntry;
var
  hSCM, hSvc: THandle;
begin
  hSCM := OpenSCManager('', '', SC_MANAGER_ALL_ACCESS);
  if hSCM <> 0 then begin
    hSvc := OpenService(hSCM, '{#ServiceName}', SERVICE_ALL_ACCESS);
    if hSvc <> 0 then begin
      DeleteService(hSvc);
      CloseServiceHandle(hSvc);
    end;
    CloseServiceHandle(hSCM);
  end;
end;

procedure KillTrayApp;
var
  ResultCode: Integer;
begin
  Exec(ExpandConstant('{sys}\taskkill.exe'),
    '/F /IM {#TrayExeName}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

function RunPowerShell(const Cmd: String): Boolean;
var
  ResultCode: Integer;
begin
  Result := Exec(
    ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe'),
    '-NoProfile -ExecutionPolicy Bypass -Command "' + Cmd + '"',
    '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Result := Result and (ResultCode = 0);
end;

function DownloadAndExtract(const URL, ZipName, DestDir: String): Boolean;
var
  TmpZip: String;
begin
  TmpZip := ExpandConstant('{tmp}\' + ZipName);
  Result := RunPowerShell(
    '[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12; ' +
    'Invoke-WebRequest -UseBasicParsing -Uri ''' + URL + ''' -OutFile ''' + TmpZip + '''; ' +
    'Expand-Archive -LiteralPath ''' + TmpZip + ''' -DestinationPath ''' + DestDir + ''' -Force');
end;

procedure ExecHidden(const FileName, Params: String);
var
  ResultCode: Integer;
begin
  Exec(ExpandConstant(FileName), Params, '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

function IsDotNet10DesktopInstalled: Boolean;
var
  Keys: TArrayOfString;
  I: Integer;
begin
  Result := False;
  if RegGetSubKeyNames(HKLM, DOTNET_REG_KEY, Keys) then
    for I := 0 to High(Keys) do
      if Copy(Keys[I], 1, 3) = '10.' then begin
        Result := True;
        Break;
      end;
end;

function InstallDotNet10Desktop: Boolean;
var
  TmpExe: String;
  ResultCode: Integer;
begin
  TmpExe := ExpandConstant('{tmp}\dotnet-runtime-win-x64.exe');
  Result := RunPowerShell(
    '[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12; ' +
    'Invoke-WebRequest -UseBasicParsing -Uri ''' + DOTNET_DESKTOP_URL + ''' -OutFile ''' + TmpExe + '''');
  if Result then
    Result := Exec(TmpExe, '/install /quiet /norestart', '', SW_HIDE, ewWaitUntilTerminated, ResultCode)
      and (ResultCode = 0);
end;

// --- Install lifecycle ---

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';

  // Install .NET 10 Windows Desktop Runtime if missing (required by tray app)
  if not IsDotNet10DesktopInstalled then begin
    WizardForm.StatusLabel.Caption := 'Installing .NET 10 Desktop Runtime...';
    if not InstallDotNet10Desktop then begin
      Result := '.NET 10 Desktop Runtime is required but could not be installed. ' +
        'Please install it manually from https://dotnet.microsoft.com/download/dotnet/10.0 and retry.';
      Exit;
    end;
  end;

  // Stop tray app before upgrade
  KillTrayApp;
  if IsServiceInstalled then begin
    if not StopServiceAndWait then
      Result := 'Could not stop the existing service. Please stop it manually and retry.';
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  ToolsDir, ExePath: String;
begin
  if CurStep = ssPostInstall then begin
    ToolsDir := ExpandConstant('{app}\tools');
    ExePath := ExpandConstant('{app}\{#MyAppExeName}');

    // Download NirSoft tools
    WizardForm.StatusLabel.Caption := 'Downloading SoundVolumeView...';
    DownloadAndExtract(
      'https://www.nirsoft.net/utils/soundvolumeview-x64.zip',
      'soundvolumeview-x64.zip', ToolsDir);

    WizardForm.StatusLabel.Caption := 'Downloading MultiMonitorTool...';
    DownloadAndExtract(
      'https://www.nirsoft.net/utils/multimonitortool-x64.zip',
      'multimonitortool-x64.zip', ToolsDir);

    // Register Windows Service
    WizardForm.StatusLabel.Caption := 'Registering service...';
    if not IsServiceInstalled then begin
      ExecHidden('{sys}\sc.exe',
        'create {#ServiceName} start= auto binPath= "' + ExePath + '"');
      ExecHidden('{sys}\sc.exe',
        'description {#ServiceName} "{#ServiceDescription}"');
    end;

    // Firewall rule
    WizardForm.StatusLabel.Caption := 'Adding firewall rule...';
    ExecHidden('{sys}\netsh.exe',
      'advfirewall firewall add rule name="{#ServiceDisplayName}"' +
      ' dir=in action=allow protocol=TCP localport={#ServicePort}' +
      ' program="' + ExePath + '" enable=yes');

    // Start service
    WizardForm.StatusLabel.Caption := 'Starting service...';
    ExecHidden('{sys}\sc.exe', 'start {#ServiceName}');
  end;
end;

// --- Uninstall lifecycle ---

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then begin
    KillTrayApp;
    if IsServiceInstalled then begin
      StopServiceAndWait;
      RemoveServiceEntry;
    end;
    ExecHidden('{sys}\netsh.exe',
      'advfirewall firewall delete rule name="{#ServiceDisplayName}"');
  end;
end;
