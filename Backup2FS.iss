; ============================================================================
;  Backup2FS by Elusive Data - branded Inno Setup installer
;  Dark title bar + gradient ribbon header + dark theme + consent tick-box.
;  Adapted from the custom Clario installer for the standalone Backup2FS app.
;  Publish first (PublishBackup2FS.ps1), then build:
;    "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" Backup2FS.iss
; ============================================================================

#define MyAppName "Backup2FS"
#define MyAppVersion "3.0.0"
#define MyAppNumericVersion "3.0.0"
#define MyAppFileVersion "3.0.0"
#define MyAppPublisher "Collara Works AB, trading as Elusive Data"
#define MyAppURL "https://www.elusivedata.io"
#define MyAppExeName "Backup2FS.exe"
#define MyAppDescription "iOS Backup Normalizer for Forensic Analysis"
#define MyAppCopyright "Copyright (C) 2025-2026 Collara Works AB, trading as Elusive Data"
#define MyEulaVersion "1.0"
#define MyEulaEffectiveDate "2026-06-19"

; --- Build guard: refuse to compile against a missing/partial publish ---
; publish\Backup2FS\ is produced by PublishBackup2FS.ps1 (self-contained win-x64).
#define B2fsPublish AddBackslash(SourcePath) + "publish\Backup2FS\"
#if !FileExists(B2fsPublish + "Backup2FS.exe")
  #error Missing Backup2FS.exe in publish\Backup2FS - run PublishBackup2FS.ps1 first.
#endif

[Setup]
; Unique AppId so Backup2FS installs/uninstalls independently of any other product.
AppId={{7A3C9E12-5B84-4D71-9E2A-3F6C1D8B0A45}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
AppCopyright={#MyAppCopyright}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
LicenseFile=Backup2FS_EULA.rtf
OutputDir=Installer
OutputBaseFilename=Backup2FS_Setup_{#MyAppFileVersion}_x64
SetupIconFile=Backup2FS\Resources\Icons\app_icon.ico
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
WizardImageFile=Backup2FS_Welcome.bmp
WizardSmallImageFile=Backup2FS_Header.bmp
WizardImageStretch=yes
; Streamlined flow: License -> (Folder) -> Installing -> Finished(Launch)
DisableWelcomePage=yes
DisableReadyPage=yes
DisableProgramGroupPage=yes
MinVersion=10.0
ArchitecturesInstallIn64BitMode=x64compatible
ArchitecturesAllowed=x64compatible
PrivilegesRequired=admin
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}
VersionInfoVersion={#MyAppNumericVersion}
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription={#MyAppDescription}
VersionInfoCopyright={#MyAppCopyright}
VersionInfoProductName={#MyAppName}
VersionInfoProductVersion={#MyAppNumericVersion}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; Self-contained publish: everything Backup2FS needs (incl. the .NET runtime) lives
; under publish\Backup2FS\ - no machine-wide .NET prerequisite.
Source: "publish\Backup2FS\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon; IconFilename: "{app}\{#MyAppExeName}"

[Registry]
Root: HKLM; Subkey: "SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\{#MyAppExeName}"; ValueType: string; ValueName: ""; ValueData: "{app}\{#MyAppExeName}"; Flags: uninsdeletekey

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}\Logs"
Type: filesandordirs; Name: "{app}\Temp"

[Messages]
SetupAppTitle=Setup - {#MyAppName}
SetupWindowTitle=Setup - {#MyAppName} {#MyAppVersion}
BeveledLabel=Backup2FS - by Elusive Data
StatusExtractFiles=Installing Backup2FS...
WizardLicense=License Agreement
LicenseLabel=Please review the End User License Agreement before installing {#MyAppName}.
LicenseLabel3=Please read the following End User License Agreement. You must accept its terms before installing {#MyAppName}.%n%nEULA Version: {#MyEulaVersion}    Effective Date: {#MyEulaEffectiveDate}

[Code]
const
  DWMWA_USE_IMMERSIVE_DARK_MODE     = 20;
  DWMWA_USE_IMMERSIVE_DARK_MODE_OLD = 19;
  DWMWA_CAPTION_COLOR               = 35;   // Win11 22000+: title bar background
  DWMWA_TEXT_COLOR                  = 36;   // Win11 22000+: title bar text
  PBM_SETBARCOLOR                   = $0409;
  PBM_SETBKCOLOR                    = $2001;
  PBM_SETSTATE                      = $0410;
  PBST_NORMAL                       = 1;
  clBgDark    = $00352516;  // #162535 dark blue
  clBgPanel   = $004D3521;  // #21354D panel blue
  clAccent    = $003479F3;  // #F37934 orange (COLORREF 0x00BBGGRR)
  clTextLight = $00E6DED6;  // light text
  clWhiteX    = $00FFFFFF;

function DwmSetWindowAttribute(hwnd: HWND; dwAttribute: Integer;
  var pvAttribute: Integer; cbAttribute: Integer): Integer;
  external 'DwmSetWindowAttribute@dwmapi.dll stdcall';
function SetWindowTheme(hwnd: HWND; SubAppName: WideString; SubIdList: WideString): Integer;
  external 'SetWindowTheme@uxtheme.dll stdcall';
function SendMessage(hwnd: HWND; Msg: Cardinal; wParam: Longint; lParam: Longint): Longint;
  external 'SendMessageW@user32.dll stdcall';

var
  AcceptCheckBox: TNewCheckBox;
  AcceptLabel: TNewStaticText;
  FooterLabel: TNewStaticText;
  MemoOrigH: Integer;

procedure SetDarkTitleBar(H: HWND);
var
  V, ColBg, ColTx: Integer;
begin
  V := 1;
  if DwmSetWindowAttribute(H, DWMWA_USE_IMMERSIVE_DARK_MODE, V, SizeOf(V)) <> 0 then
    DwmSetWindowAttribute(H, DWMWA_USE_IMMERSIVE_DARK_MODE_OLD, V, SizeOf(V));
  // Force the caption dark even when the window is ACTIVE (no dodger-blue accent).
  ColBg := clBgDark;
  DwmSetWindowAttribute(H, DWMWA_CAPTION_COLOR, ColBg, SizeOf(ColBg));
  ColTx := clWhiteX;
  DwmSetWindowAttribute(H, DWMWA_TEXT_COLOR, ColTx, SizeOf(ColTx));
end;

procedure ApplyDarkTheme(AParent: TWinControl);
var
  I: Integer;
  C: TControl;
begin
  for I := 0 to AParent.ControlCount - 1 do
  begin
    C := AParent.Controls[I];
    if C is TNewStaticText then
      TNewStaticText(C).Font.Color := clTextLight
    else if C is TLabel then
    begin
      TLabel(C).Font.Color := clTextLight;
      TLabel(C).Transparent := True;
    end
    else if C is TNewCheckBox then
    begin
      TNewCheckBox(C).ParentFont := False;
      TNewCheckBox(C).Font.Color := clWhiteX;
    end
    else if C is TNewRadioButton then
    begin
      TNewRadioButton(C).ParentFont := False;
      TNewRadioButton(C).Font.Color := clWhiteX;
    end
    else if C is TNewCheckListBox then
    begin
      TNewCheckListBox(C).ParentFont := False;
      TNewCheckListBox(C).Font.Color := clWhiteX;
      TNewCheckListBox(C).Color := clBgDark;
    end
    else if C is TRichEditViewer then
      TRichEditViewer(C).Color := clBgPanel
    else if C is TNewEdit then
    begin
      TNewEdit(C).Color := clBgPanel;
      TNewEdit(C).Font.Color := clWhiteX;
    end
    else if C is TEdit then
    begin
      TEdit(C).Color := clBgPanel;
      TEdit(C).Font.Color := clWhiteX;
    end
    else if C is TNewNotebookPage then
      TNewNotebookPage(C).Color := clBgDark
    else if C is TPanel then
      TPanel(C).Color := clBgDark;

    if C is TWinControl then
      ApplyDarkTheme(TWinControl(C));
  end;
end;

procedure StyleHeaderBand;
begin
  // Stretch the gradient ribbon across the whole header panel; hide default titles.
  WizardForm.MainPanel.Color := clBgDark;
  WizardForm.PageNameLabel.Visible := False;
  WizardForm.PageDescriptionLabel.Visible := False;
  WizardForm.WizardSmallBitmapImage.Stretch := True;
  WizardForm.WizardSmallBitmapImage.Left := 0;
  WizardForm.WizardSmallBitmapImage.Top := 0;
  WizardForm.WizardSmallBitmapImage.Width := WizardForm.MainPanel.Width;
  WizardForm.WizardSmallBitmapImage.Height := WizardForm.MainPanel.Height;
end;

procedure StyleProgressBar;
begin
  // Strip the visual style so a custom colour takes effect, then paint brand orange.
  SetWindowTheme(WizardForm.ProgressGauge.Handle, '', '');
  SendMessage(WizardForm.ProgressGauge.Handle, PBM_SETSTATE, PBST_NORMAL, 0);
  SendMessage(WizardForm.ProgressGauge.Handle, PBM_SETBKCOLOR, 0, clBgPanel);
  SendMessage(WizardForm.ProgressGauge.Handle, PBM_SETBARCOLOR, 0, clAccent);
end;

procedure HideStockGlyphs;
begin
  WizardForm.BeveledLabel.Visible := False;          // etched footer text (looked doubled)
  WizardForm.SelectDirBitmapImage.Visible := False;  // white folder icon -> gone
end;

procedure AcceptCheckBoxClick(Sender: TObject);
begin
  WizardForm.LicenseAcceptedRadio.Checked := AcceptCheckBox.Checked;
  WizardForm.NextButton.Enabled := AcceptCheckBox.Checked;
end;

procedure AcceptLabelClick(Sender: TObject);
begin
  AcceptCheckBox.Checked := not AcceptCheckBox.Checked;
  AcceptCheckBoxClick(nil);
end;

procedure InitializeWizard;
begin
  SetDarkTitleBar(WizardForm.Handle);

  WizardForm.Color := clBgDark;

  ApplyDarkTheme(WizardForm);
  StyleHeaderBand;
  HideStockGlyphs;

  // Crisp branded footer (replaces Inno's etched/blurry BeveledLabel).
  FooterLabel := TNewStaticText.Create(WizardForm);
  FooterLabel.Parent := WizardForm;
  FooterLabel.AutoSize := True;
  FooterLabel.Caption := 'Backup2FS  -  by Elusive Data';
  FooterLabel.Font.Color := clTextLight;
  FooterLabel.Visible := False;

  // Consent tick-box (box only) + a separate white static label for the caption,
  // because themed checkboxes ignore Font.Color for their own caption text.
  AcceptCheckBox := TNewCheckBox.Create(WizardForm);
  AcceptCheckBox.Parent := WizardForm.LicenseMemo.Parent;
  AcceptCheckBox.Width := ScaleX(20);
  AcceptCheckBox.Height := ScaleY(20);
  AcceptCheckBox.Caption := '';
  AcceptCheckBox.Visible := False;
  AcceptCheckBox.OnClick := @AcceptCheckBoxClick;

  AcceptLabel := TNewStaticText.Create(WizardForm);
  AcceptLabel.Parent := WizardForm.LicenseMemo.Parent;
  AcceptLabel.AutoSize := True;
  AcceptLabel.Caption := 'I have read and agree to the End User License Agreement';
  AcceptLabel.Font.Color := clWhiteX;
  AcceptLabel.Visible := False;
  AcceptLabel.OnClick := @AcceptLabelClick;

  WizardForm.LicenseAcceptedRadio.Visible := False;
  WizardForm.LicenseNotAcceptedRadio.Visible := False;
end;

procedure CurPageChanged(CurPageID: Integer);
var
  CbTop: Integer;
begin
  SetDarkTitleBar(WizardForm.Handle);
  StyleHeaderBand;
  HideStockGlyphs;
  ApplyDarkTheme(WizardForm);   // re-theme: task checkboxes / run list are created lazily

  // Crisp footer on every page EXCEPT the Finish page (its left panel already brands,
  // and the footer's box would overlap the welcome image there).
  if CurPageID = wpFinished then
    FooterLabel.Visible := False
  else
  begin
    FooterLabel.Top := WizardForm.BeveledLabel.Top;
    FooterLabel.Left := WizardForm.BeveledLabel.Left;
    FooterLabel.Visible := True;
  end;

  if CurPageID = wpInstalling then
    StyleProgressBar;

  if CurPageID = wpLicense then
  begin
    if MemoOrigH = 0 then
      MemoOrigH := WizardForm.LicenseMemo.Height;
    WizardForm.LicenseMemo.Height := MemoOrigH - ScaleY(44);
    CbTop := WizardForm.LicenseMemo.Top + WizardForm.LicenseMemo.Height + ScaleY(12);
    AcceptCheckBox.Left := WizardForm.LicenseMemo.Left;
    AcceptCheckBox.Top := CbTop;
    AcceptCheckBox.Visible := True;
    AcceptLabel.Left := AcceptCheckBox.Left + ScaleX(24);
    AcceptLabel.Top := CbTop + ScaleY(1);
    AcceptLabel.Font.Color := clWhiteX;   // re-assert: ApplyDarkTheme set static text grey
    AcceptLabel.Visible := True;
    WizardForm.NextButton.Enabled := AcceptCheckBox.Checked;
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssInstall then
    StyleProgressBar;
  if CurStep = ssPostInstall then
    CreateDir(ExpandConstant('{app}\Logs'));
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  DeleteUserData: Integer;
begin
  if CurUninstallStep = usUninstall then
  begin
    DeleteUserData := MsgBox('Do you want to remove all user data and settings?',
                             mbConfirmation, MB_YESNO);
    if DeleteUserData = IDYES then
    begin
      DelTree(ExpandConstant('{localappdata}\Backup2FS'), True, True, True);
      DelTree(ExpandConstant('{userappdata}\Backup2FS'), True, True, True);
    end;
  end;
end;
