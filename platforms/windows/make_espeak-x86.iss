; Inno Setup 6 installer for the native 32-bit Windows build of eSpeak 1.44.05

#ifndef PackageDir
  #error Build with build-x86.ps1 to supply the centralized package directory.
#endif
#ifndef InstallerDir
  #error Build with build-x86.ps1 to supply the final installer directory.
#endif

[Setup]
AppName=eSpeak
AppId={{B0E0EE35-0C47-4C1A-A37D-82D12B0A6C32}
AppVersion=1.44.05
AppVerName=eSpeak version 1.44.05 r39 (32-bit)
AppCopyright=Licensed under GNU General Public License version 3.   (See file License.txt for details).
WizardStyle=modern
LicenseFile=..\..\License.txt
PrivilegesRequired=admin
ArchitecturesAllowed=x86compatible

DefaultDirName={autopf}\eSpeak
DefaultGroupName=eSpeak
OutputDir={#InstallerDir}
OutputBaseFilename=setup_espeak-1.44.05-x86-r39
Compression=lzma2/ultra64
SolidCompression=yes
ShowLanguageDialog=auto
UninstallDisplayIcon={app}\TTSApp.exe
VersionInfoVersion=1.44.5.39
VersionInfoProductName=eSpeak
VersionInfoDescription=eSpeak 1.44.05 r39 native 32-bit installer

[InstallDelete]
Type: files; Name: "{app}\espeak.dll"
Type: filesandordirs; Name: "{app}\espeak-data\voices\test"
Type: files; Name: "{app}\Vario.exe"; Check: ShouldRemoveVario
Type: files; Name: "{group}\Vario - eSpeak voice manager (32-bit).lnk"; Check: ShouldRemoveVario

[Dirs]
Name: "{app}\espeak-data\soundicons"
Name: "{app}\espeak-data\mbrola"

[Files]
Source: "{#PackageDir}\espeak_sapi.dll"; DestDir: "{app}"; Flags: regserver ignoreversion
Source: "{#PackageDir}\TTSApp.exe"; DestDir:"{app}"; Flags: ignoreversion
Source: "{#PackageDir}\Vario.exe"; DestDir:"{app}"; Flags: ignoreversion; Check: ShouldInstallVario
Source: "{#PackageDir}\espeak-data\*"; DestDir: "{app}\espeak-data"; Flags: recursesubdirs createallsubdirs ignoreversion
Source: "{#PackageDir}\dictsource\*"; DestDir: "{app}\dictsource"; Flags: recursesubdirs createallsubdirs ignoreversion
Source: "{#PackageDir}\docs\*"; DestDir: "{app}\docs"; Flags: recursesubdirs createallsubdirs ignoreversion
Source: "{#PackageDir}\command_line\*"; DestDir: "{app}\command_line"; Flags: recursesubdirs createallsubdirs ignoreversion
Source: "{#PackageDir}\Readme.txt"; DestDir: "{app}"; Flags: isreadme ignoreversion
Source: "{#PackageDir}\License.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#PackageDir}\licenses\*"; DestDir: "{app}\licenses"; Flags: ignoreversion

[Icons]
Name: "{group}\eSpeak SAPI test (32-bit)"; Filename: "{app}\TTSApp.exe"; WorkingDir: "{app}"
Name: "{group}\Vario - eSpeak voice manager (32-bit)"; Filename: "{app}\Vario.exe"; WorkingDir: "{app}"; Check: ShouldInstallVario
Name: "{group}\eSpeak documentation (32-bit)"; Filename: "{app}\docs\index.html"
Name: "{group}\Uninstall eSpeak (32-bit)"; Filename: "{uninstallexe}"


[Languages]
Name: "en"; MessagesFile: "compiler:Default.isl"
Name: "cs"; MessagesFile: "compiler:Languages\Czech.isl"
Name: "de"; MessagesFile: "compiler:Languages\German.isl"
Name: "es"; MessagesFile: "compiler:Languages\Spanish.isl"
Name: "fi"; MessagesFile: "compiler:Languages\Finnish.isl"
Name: "fr"; MessagesFile: "compiler:Languages\French.isl"
Name: "hu"; MessagesFile: "compiler:Languages\Hungarian.isl"
Name: "it"; MessagesFile: "compiler:Languages\Italian.isl"
Name: "nl"; MessagesFile: "compiler:Languages\Dutch.isl"
Name: "no"; MessagesFile: "compiler:Languages\Norwegian.isl"
Name: "pl"; MessagesFile: "compiler:Languages\Polish.isl"
Name: "pt"; MessagesFile: "compiler:Languages\Portuguese.isl"
Name: "ru"; MessagesFile: "compiler:Languages\Russian.isl"
Name: "sk"; MessagesFile: "compiler:Languages\Slovak.isl"
Name: "tr"; MessagesFile: "compiler:Languages\Turkish.isl"

[CustomMessages]
v1=Select which voices to install
v2=or press Enter to accept defaults
v3=Enter voice names, eg: (for Portuguese)   pt,  or with a variant, eg: pt+f3
VarioPageTitle=Optional companion application
VarioPageDescription=Choose whether to install Vario before accepting the license.
VarioPageSubcaption=Vario is an accessible manager for installed SAPI voices, variants, per-voice modulation and Sonic speed boost.
InstallVario=Install Vario (recommended)
GenderReadFailed=The voice definition could not be read: %1
RegistrationFailed=The SAPI voice settings could not be updated.
RegistrationRollbackFailed=The previous SAPI voice settings could not be fully restored. Run the installer again to repair the registration.
TooManyRegistrationVoices=Select at most 200 SAPI voices.

pl.v1=Wybierz głosy, które mają zostać zainstalowane
pl.v2=lub naciśnij Enter, aby przyjąć wartości domyślne
pl.v3=Wpisz nazwy głosów, np. pl albo wariant pl+f3
pl.VarioPageTitle=Opcjonalna aplikacja dodatkowa
pl.VarioPageDescription=Wybierz, czy zainstalować Vario przed zaakceptowaniem licencji.
pl.VarioPageSubcaption=Vario to dostępny menedżer zainstalowanych głosów SAPI, wariantów, modulacji dla poszczególnych głosów i przyspieszania Sonic.
pl.InstallVario=Zainstaluj Vario (zalecane)
pl.GenderReadFailed=Nie można odczytać definicji głosu: %1
pl.RegistrationFailed=Nie można zaktualizować ustawień głosów SAPI.
pl.RegistrationRollbackFailed=Nie udało się w pełni przywrócić poprzednich ustawień głosów SAPI. Uruchom instalator ponownie, aby naprawić rejestrację.
pl.TooManyRegistrationVoices=Wybierz najwyżej 200 głosów SAPI.

fr.v1=Sélectionnez les voix à installer
fr.v2=ou appuyez sur Entrée pour accepter les valeurs par défaut.
fr.v3=Entrez le nom des voix, ex : (pour le Français) fr, ou avec une variante ex : fr+f3

pt.v1=Seleccione as vozes que pretende instalar
pt.v2=ou precione enter para aceitar as predefinidas.
pt.v3=Introduza os nomes das vozes, ex: (Brazil) pt (ou Portugal) pt-pt, ou com outras caracter?sticas, ex: pt+f3

[Code]
var
  UILanguage: Integer;
  UIVoice: String;
  Page: TInputQueryWizardPage;
  VarioPage: TInputOptionWizardPage;

const
  sEspeak = 'eSpeak-';
  RegVoice1 = 'Software\Microsoft\Speech\Voices\Tokens\eSpeak';
  RegPhConv = 'Software\Microsoft\Speech\PhoneConverters\Tokens';
  RegUninstall = 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{B0E0EE35-0C47-4C1A-A37D-82D12B0A6C32}_is1';

#include "voice-registration.iss"

procedure ConfigureRegistration;
begin
  ConfigureVoiceRegistration(HKEY_LOCAL_MACHINE_32, HKEY_LOCAL_MACHINE, $200,
    'Software\Microsoft\Speech\Voices\Tokens', RegPhConv + '\eSpeak');
end;


function VoiceFromLanguage(language: Integer): String;
var
  lang_main: Integer;

begin
  lang_main := language and $3ff;
  Result := 'en';

  // Translation from microsoft codes to language codes
  // Used to set default voices for inatallation.
  case lang_main of
  //$02: Result := 'bg';
  $03: Result := 'ca';
  $04: Result := 'zh';
  $05: Result := 'cs';
  $06: Result := 'da';
  $07: Result := 'de';
  $08: Result := 'el';
  $09: Result := 'en';
  $0a: Result := 'es-la  es-la+m3  es-la+f3';
  $0b: Result := 'fi';
  $0c: Result := 'fr';
  $0e: Result := 'hu';
  $0f: Result := 'is';
  $10: Result := 'it';
  //$12: Result := 'ko';
  $13: Result := 'nl';
  $14: Result := 'no';
  $15: Result := 'pl';
  $16: Result := 'pt';
  $18: Result := 'ro';
  $19: Result := 'ru';
  $1a: Result := 'hr';
  $1b: Result := 'sk';
  $1c: Result := 'sq';
  $1d: Result := 'sv';
  $1f: Result := 'tr';
  $21: Result := 'id';
  $26: Result := 'lv';
  $2a: Result := 'vi';
  $2b: Result := 'hy';
  //$2c: Result := 'az';
  //$2d: Result := 'eu';
  $2f: Result := 'mk';
  $36: Result := 'af';
  $39: Result := 'hi';
  $41: Result := 'sw';
  $49: Result := 'ta';
  //$4b: Result := 'kn';
  //$50: Result := 'mn';
  $52: Result := 'cy';
  //$61: Result := 'ne';
  //$87: Result := 'rw';
  end;

  // is there a match on the full language code?
  case language of
  $80a: Result := 'es-la  es-la+f3  es-la+m3';
  $c0a: Result := 'es';
  $816: Result := 'pt-pt  es-la';
  $41a: Result := 'hr';
  $81a: Result := 'sr';
  $c1a: Result := 'sr';
  $141a: Result := 'bs';
  end;
end;


function LanguageFromVoice(voice: String): String;
var
  value: Integer;
  lang1: String;
  len: Integer;
begin
  value := $409;   // default en-us

  lang1 := Copy(voice,0,2);
  if lang1 = 'mb' then
  begin
    lang1 := Copy(voice,3,3);
    len := Length(voice);
    if len > 8 then
      lang1 := Copy(voice,8,6);  // eg. mb-de4-en, return 'en'
  end;
  
  // Used to set the correct Microsoft language code in the registry
  // when a SAPI5 voice is installed.
  case lang1 of
  'af': value := $436;
  'az': value := $42c;
  'bg': value := $402;
  'bs': value := $41a;   // should be $141a but Jaws crashes on startup
  'ca': value := $403;
  'cs': value := $405;
  'cy': value := $452;
  'da': value := $406;
  'de': value := $407;
  'el': value := $408;
  'en': value := $409;
  'es': value := $40a;
  'eu': value := $42d;
  'fi': value := $40b;
  'fr': value := $40c;
  'hi': value := $439;
  'hr': value := $41a;
  'hu': value := $40e;
  'hy': value := $42b;
  'id': value := $421;
  'is': value := $40f;
  'it': value := $410;
  'kn': value := $44b;
  'ko': value := $412;
  'lv': value := $426;
  'mk': value := $42f;
  'mn': value := $450;
  'ne': value := $461;
  'nl': value := $413;
  'no': value := $414;
  'pl': value := $415;
  'pt': value := $416;
  'ro': value := $418;
  'ru': value := $419;
  'rw': value := $487;
  'sk': value := $41b;
  'sq': value := $41c;
  'sr': value := $81a;
  'sv': value := $41d;
  'sw': value := $441;
  'ta': value := $449;
  'tr': value := $41f;
  'vi': value := $42a;
  'zh': value := $804;
  // mbrola voices
  '-af': value := $436;
  '-br': value := $416;
  '-ca': value := $c0c;
  '-cr': value := $41a;
  '-cz': value := $405;
  '-de': value := $407;
  '-en': value := $809;
  '-es': value := $40a;
  '-fr': value := $40c;
  '-gr': value := $408;
  '-hu': value := $40e;
  '-ic': value := $40f;
  '-in': value := $439;
  '-it': value := $410;
  '-mx': value := $80a;
  '-nl': value := $413;
  '-pl': value := $415;
  '-pt': value := $816;
  '-ro': value := $418;
  '-sw': value := $41d;
  '-us': value := $409;
  '-vz': value := $200a;
  end;

  // check for specific voices
  case voice of
  'pt-pt': value := $816;
  end;
  
  Result := Format('%X',[value]);
end;


// Use an installed converter when it already supports this exact language.
function CheckPhoneConvertors(Lcode: String): Integer;
var
  Convertors: TArrayOfString;
  Index: Integer;
  Languages: String;
begin
  Result := 0;
  if RegGetSubkeyNames(RegistrationRoot, RegPhConv, Convertors) then
    for Index := 0 to GetArrayLength(Convertors) - 1 do
      if RegQueryStringValue(RegistrationRoot, RegPhConv + '\' + Convertors[Index] +
        '\Attributes', 'Language', Languages) then
        if Pos(';' + Uppercase(Lcode) + ';', ';' + Uppercase(Languages) + ';') > 0 then
          Result := 1;
end;

procedure SetPhoneConvertor(Lcode: String);
var Languages: String;
begin
  if CheckPhoneConvertors(Lcode) <> 0 then Exit;
  Languages := '';
  RegQueryStringValue(RegistrationRoot, RegistrationConverterPath + '\Attributes',
    'Language', Languages);
  // Keep changes inside the eSpeak-owned tree covered by the rollback snapshot.
  RequireRegistration(RegWriteStringValue(RegistrationRoot, RegistrationConverterPath,
    'CLSID', '{9185F743-1143-4C28-86B5-BFF14F20E5C8}'));
  RequireRegistration(RegWriteStringValue(RegistrationRoot, RegistrationConverterPath,
    'PhoneMap', '- 0001'));
  if Languages <> '' then Languages := ';' + Languages;
  RequireRegistration(RegWriteStringValue(RegistrationRoot, RegistrationConverterPath +
    '\Attributes', 'Language', Lcode + Languages));
end;

#include "voice-gender.iss"

procedure SetupVoice(Voice, TokenName: String; Inflection: Cardinal);
var
  TokenPath, AttributesPath, VoiceUC, Lcode, Gender: String;
begin
  Lcode := LanguageFromVoice(Voice);
  Gender := VoiceGender(ExpandConstant('{app}\espeak-data\voices'), Voice);
  TokenPath := RegistrationVoicesPath + '\' + TokenName;
  AttributesPath := TokenPath + '\Attributes';
  if Voice = 'default' then VoiceUC := 'default' else VoiceUC := Uppercase(Voice);

  RequireRegistration(RegWriteStringValue(RegistrationRoot, TokenPath, '', sEspeak + VoiceUC));
  RequireRegistration(RegWriteStringValue(RegistrationRoot, TokenPath, 'CLSID',
    '{BE985C8D-BE32-4A22-AA93-55C16A6D1D91}'));
  RequireRegistration(RegWriteStringValue(RegistrationRoot, TokenPath, 'Path', ExpandConstant('{app}')));
  RequireRegistration(RegWriteStringValue(RegistrationRoot, TokenPath, 'VoiceName', Voice));
  RequireRegistration(RegWriteDWordValue(RegistrationRoot, TokenPath, 'Inflection', Inflection));
  RequireRegistration(RegWriteStringValue(RegistrationRoot, AttributesPath, 'Name', sEspeak + Voice));
  RequireRegistration(RegWriteStringValue(RegistrationRoot, AttributesPath, 'Gender', Gender));
  RequireRegistration(RegWriteStringValue(RegistrationRoot, AttributesPath, 'Age', 'Adult'));
  RequireRegistration(RegWriteStringValue(RegistrationRoot, AttributesPath, 'Language', Lcode));
  RequireRegistration(RegWriteStringValue(RegistrationRoot, AttributesPath, 'Vendor', 'http://espeak.sf.net'));
  SetPhoneConvertor(Lcode);
end;

function ShouldInstallVario: Boolean;
begin
  Result := VarioPage.Values[0];
end;


function ShouldRemoveVario: Boolean;
begin
  Result := not ShouldInstallVario;
end;



function PrepareToInstall(var NeedsRestart: Boolean): String;
var Field: Integer;
begin
  Result := '';
  try
    // The user has accepted installation. Snapshot immediately before mutation,
    // not while the welcome/license pages are still open.
    ConfigureRegistration;
    CaptureVoiceRegistration;
    for Field := 0 to 4 do PlanVoiceLine(Page.Values[Field]);
  except
    Result := GetExceptionMessage;
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then ApplyVoiceRegistration;
  if CurStep = ssDone then RegistrationCommitted := True;
end;

procedure DeinitializeSetup;
begin
  if RegistrationStarted and not RegistrationCommitted then
    try
      RestoreVoiceRegistration;
    except
      SuppressibleMsgBox(CustomMessage('RegistrationRollbackFailed'), mbError, MB_OK, IDOK);
    end;
end;

procedure InitializeWizard;
var
  Index, Field: Integer;
  lang: String;
  voice2: String;
  uilang: String;
begin
  ConfigureRegistration;
  // Create the language selection page

  VarioPage := CreateInputOptionPage(wpWelcome,
    CustomMessage('VarioPageTitle'), CustomMessage('VarioPageDescription'),
    CustomMessage('VarioPageSubcaption'), False, False);
  VarioPage.Add(CustomMessage('InstallVario'));
  VarioPage.Values[0] := True;

  UILanguage := GetUILanguage;
  UIVoice := VoiceFromLanguage(UILanguage);
  uilang := Format('  (Language code %x)',[UILanguage]);

  lang := ActiveLanguage;
  Page := CreateInputQueryPage(wpSelectDir,CustomMessage('v1'),CustomMessage('v2')+uilang,CustomMessage('v3'));

  // Add items (False means it's not a password edit)
  Page.Add('', False);
  Page.Add('', False);
  Page.Add('', False);
  Page.Add('', False);
  Page.Add('', False);


  // Set initial values (optional)
  if UIVoice = 'en' then
    voice2 := 'en-us'
  else
    voice2 := 'en';
    
  if Pos('+',UIVoice) = 0 then
    Page.Values[0] := Format('%s  %s',[UIVoice, UIVoice+'+f2'])
  else
    Page.Values[0] := Format('%s',[UIVoice]);
  Page.Values[1] := Format('%s',[voice2]);

  ReadRegisteredVoices;
  if (GetArrayLength(OriginalTokens) > 0) or RegKeyExists(RegistrationRoot, RegUninstall) then
  begin
    for Field := 0 to 4 do Page.Values[Field] := '';
    for Index := 0 to GetArrayLength(OriginalVoices) - 1 do
    begin
      Field := Index div 40;
      if Field > 4 then Field := 4;
      if Page.Values[Field] <> '' then Page.Values[Field] := Page.Values[Field] + ' ';
      Page.Values[Field] := Page.Values[Field] + OriginalVoices[Index].Name;
    end;
  end;
end;

procedure ClearRegistry;
var Names: TArrayOfString; Index: Integer;
begin
  ConfigureRegistration;
  Names := ManagedVoiceTokens;
  for Index := 0 to GetArrayLength(Names) - 1 do
    DeleteRegistrationTree(RegistrationVoicesPath + '\' + Names[Index]);
  DeleteRegistrationTree(RegistrationConverterPath);
end;

function InitializeSetup: Boolean;
begin
  // Opening or canceling the wizard must not change installed voices.
  Result := True;
end;

function InitializeUninstall: Boolean;
begin
  Result := True;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  // This stage is reached only after the user accepts actual uninstallation.
  if CurUninstallStep = usPostUninstall then ClearRegistry;
end;





