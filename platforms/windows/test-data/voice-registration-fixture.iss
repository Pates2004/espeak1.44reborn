#ifndef FixtureOutput
  #error Supply the isolated fixture output directory.
#endif
#ifndef RegistryFixtureRoot
  #error Supply a fresh HKCU fixture prefix.
#endif
#ifndef RegistryFixtureView
  #define RegistryFixtureView 256
#endif

[Setup]
AppName=eSpeak voice registration fixture
AppVersion=1.0
CreateAppDir=no
Uninstallable=no
CreateUninstallRegKey=no
PrivilegesRequired=lowest
OutputDir={#FixtureOutput}
OutputBaseFilename=voice-registration-fixture
Compression=none
SetupLogging=no

[CustomMessages]
RegistrationFailed=Fixture registry operation failed.
RegistrationRollbackFailed=Fixture registry rollback failed.
TooManyRegistrationVoices=Too many fixture voices.

[Code]
#include "..\voice-registration.iss"

var
  Assertions, Writes, FailAfter: Integer;
  FixturePrefix: String;

procedure Check(Value: Boolean; MessageText: String);
begin
  if not Value then RaiseException(MessageText);
  Assertions := Assertions + 1;
end;

procedure SetupVoice(Voice, TokenName: String; Inflection: Cardinal);
var Path: String;
begin
  Path := RegistrationVoicesPath + '\' + TokenName;
  RequireRegistration(RegWriteStringValue(RegistrationRoot, Path, 'VoiceName', Voice));
  RequireRegistration(RegWriteDWordValue(RegistrationRoot, Path, 'Inflection', Inflection));
  RequireRegistration(RegWriteStringValue(RegistrationRoot, Path + '\NewChild', 'Added', 'new'));
  RequireRegistration(RegWriteStringValue(RegistrationRoot, RegistrationConverterPath + '\Attributes', 'Language', 'new'));
  Writes := Writes + 1;
  if Writes = FailAfter then RaiseException('Injected registration write failure');
end;

procedure RawWrite(Path, Name: String; Kind: Cardinal; Data: AnsiString);
var Key: HKEY; Disposition: Cardinal;
begin
  RequireRegistration(ProfileCreateKey(HKEY_CURRENT_USER, Path, 0, 0, 0,
    $F003F or {#RegistryFixtureView}, 0, Key, Disposition) = 0);
  try
    RequireRegistration(ProfileWriteValue(Key, Name, 0, Kind, Data, Length(Data)) = 0);
  finally ProfileCloseKey(Key); end;
end;

function FixtureBytes(Value: String): AnsiString;
begin
  Result := Value;
end;

procedure SeedOriginal;
var Path: String;
begin
  ConfigureVoiceRegistration(HKEY_CURRENT_USER, HKEY_CURRENT_USER, {#RegistryFixtureView},
    FixturePrefix + '\Voices', FixturePrefix + '\Converters\eSpeak');
  RequireRegistration(RegWriteStringValue(RegistrationRoot, RegistrationVoicesPath + '\eSpeak', 'VoiceName', 'pl+f3'));
  RequireRegistration(RegWriteDWordValue(RegistrationRoot, RegistrationVoicesPath + '\eSpeak', 'Inflection', 17));
  RequireRegistration(RegWriteStringValue(RegistrationRoot, RegistrationVoicesPath + '\eSpeak_7', 'VoiceName', 'en'));
  RequireRegistration(RegWriteDWordValue(RegistrationRoot, RegistrationVoicesPath + '\eSpeak_7', 'Inflection', 81));
  RequireRegistration(RegWriteStringValue(RegistrationRoot, RegistrationVoicesPath + '\eSpeak_99', 'VoiceName', 'pl+m2'));
  RequireRegistration(RegWriteDWordValue(RegistrationRoot, RegistrationVoicesPath + '\eSpeak_99', 'Inflection', 0));
  RequireRegistration(RegWriteStringValue(RegistrationRoot, RegistrationVoicesPath + '\eSpeak_custom', 'Keep', 'other-engine'));
  RequireRegistration(RegWriteStringValue(RegistrationRoot, RegistrationConverterPath + '\Attributes', 'Language', '415;409'));
  Path := RegistrationVoicesPath + '\eSpeak\Custom';
  RequireRegistration(RegWriteStringValue(RegistrationRoot, Path, '', 'Default value'));
  RequireRegistration(RegWriteStringValue(RegistrationRoot, Path, 'Unicode', 'Zażółć gęślą jaźń'));
  RawWrite(Path, 'Binary', 3, FixtureBytes(#0#1#127#255#0));
  RawWrite(Path, 'Empty', 3, '');
  RawWrite(Path, 'Dword', 4, FixtureBytes(#120#86#52#18));
  RawWrite(Path, 'Qword', 11, FixtureBytes(#1#2#3#4#5#6#7#8));
  RawWrite(Path, 'Expand', 2, FixtureBytes('%'#0'T'#0'E'#0'M'#0'P'#0'%'#0#0#0));
  RawWrite(Path, 'Multi', 7, FixtureBytes('a'#0#0#0'b'#0#0#0#0#0));
  Writes := 0;
  FailAfter := -1;
end;

procedure VerifySnapshot;
var
  Index: Integer; Key: HKEY; Kind, Bytes: Cardinal; Data, Security: AnsiString;
  Names: TArrayOfString; Value: String;
begin
  Names := ManagedVoiceTokens;
  Check(GetArrayLength(Names) = GetArrayLength(OriginalTokens), 'Original token count changed');
  for Index := 0 to GetArrayLength(StoredKeys) - 1 do
  begin
    Check(RegKeyExists(RegistrationRoot, StoredKeys[Index].Path), 'Original key missing');
    RequireRegistration(ProfileOpenKey(HKEY_CURRENT_USER, StoredKeys[Index].Path, 0,
      $20019 or {#RegistryFixtureView}, Key) = 0);
    try
      Bytes := 0;
      ProfileSecuritySize(Key, 4, 0, Bytes);
      SetLength(Security, Bytes);
      RequireRegistration(ProfileReadSecurity(Key, 4, Security, Bytes) = 0);
      SetLength(Security, Bytes);
      Check(Security = StoredKeys[Index].Security, 'Original DACL changed');
    finally ProfileCloseKey(Key); end;
  end;
  for Index := 0 to GetArrayLength(StoredValues) - 1 do
  begin
    RequireRegistration(ProfileOpenKey(HKEY_CURRENT_USER, StoredValues[Index].Path, 0,
      $20019 or {#RegistryFixtureView}, Key) = 0);
    try
      Bytes := 0;
      RequireRegistration(ProfileValueSize(Key, StoredValues[Index].Name, 0, Kind, 0, Bytes) = 0);
      SetLength(Data, Bytes);
      RequireRegistration(ProfileReadValue(Key, StoredValues[Index].Name, 0, Kind, Data, Bytes) = 0);
      SetLength(Data, Bytes);
      Check(Kind = StoredValues[Index].Kind, 'Registry value type changed');
      Check(Data = StoredValues[Index].Data, 'Registry value bytes changed');
    finally ProfileCloseKey(Key); end;
  end;
  Check(not RegKeyExists(RegistrationRoot, RegistrationVoicesPath + '\eSpeak\NewChild'), 'Added child survived rollback');
  Check(not RegKeyExists(RegistrationRoot, RegistrationVoicesPath + '\eSpeak_1'), 'Added token survived rollback');
  Check(RegQueryStringValue(RegistrationRoot, RegistrationVoicesPath + '\eSpeak_custom', 'Keep', Value), 'Unmanaged token missing');
  Check(Value = 'other-engine', 'Unmanaged token changed');
  Check(RegQueryStringValue(RegistrationRoot, RegistrationVoicesPath + '\eSpeak\Custom', 'Unicode', Value), 'Unicode value missing');
  Check(Value = 'Zażółć gęślą jaźń', 'Unicode value changed');
end;

procedure RunCases;
var
  Failure: Integer; Raised: Boolean; Value: String; Inflection: Cardinal;
begin
  SeedOriginal;
  CaptureVoiceRegistration;
  VerifySnapshot; // Capturing during preflight must be read-only.
  Check(IsManagedVoiceToken('eSpeak_7'), 'Numeric token not recognized');
  Check(not IsManagedVoiceToken('eSpeak_custom'), 'Unmanaged token recognized');
  for Failure := 1 to 3 do
  begin
    SetArrayLength(PlannedVoices, 0);
    PlanVoiceLine(' en  PL+F3' + #9 + 'en-us en ');
    Check(GetArrayLength(PlannedVoices) = 3, 'Whitespace/duplicate voice plan');
    Check(PlannedVoices[0].Token = 'eSpeak_7', 'Surviving English token changed');
    Check(PlannedVoices[0].Inflection = 81, 'English modulation changed');
    Check(PlannedVoices[1].Token = 'eSpeak', 'Case-insensitive Polish token changed');
    Check(PlannedVoices[1].Inflection = 17, 'Polish modulation changed');
    Check(PlannedVoices[2].Token = 'eSpeak_1', 'New voice reused removed token');
    Writes := 0;
    FailAfter := Failure;
    Raised := False;
    try ApplyVoiceRegistration; except Raised := True; end;
    Check(Raised, 'Injected failure was ignored');
    VerifySnapshot;
  end;
  Writes := 0;
  FailAfter := -1;
  ApplyVoiceRegistration;
  Check(not RegKeyExists(RegistrationRoot, RegistrationVoicesPath + '\eSpeak_99'), 'Removed voice survived');
  Check(RegQueryStringValue(RegistrationRoot, RegistrationVoicesPath + '\eSpeak_7', 'VoiceName', Value), 'Retained English missing');
  Check(Value = 'en', 'Retained English identity changed');
  Check(RegQueryDWordValue(RegistrationRoot, RegistrationVoicesPath + '\eSpeak_7', 'Inflection', Inflection), 'Retained modulation missing');
  Check(Inflection = 81, 'Retained modulation changed');
  RestoreVoiceRegistration; // A later canceled/failed stage must also restore.
  VerifySnapshot;

  ConfigureVoiceRegistration(HKEY_CURRENT_USER, HKEY_CURRENT_USER, {#RegistryFixtureView},
    FixturePrefix + '\FreshVoices', FixturePrefix + '\FreshConverter');
  CaptureVoiceRegistration;
  Check(GetArrayLength(OriginalTokens) = 0, 'Fresh profile unexpectedly contains voices');
  PlanVoice('pl');
  Writes := 0;
  FailAfter := 1;
  Raised := False;
  try ApplyVoiceRegistration; except Raised := True; end;
  Check(Raised, 'Fresh installation failure was ignored');
  Check(GetArrayLength(ManagedVoiceTokens) = 0, 'Fresh failed install left a voice');
  Check(not RegKeyExists(RegistrationRoot, RegistrationConverterPath), 'Fresh failed install left a converter');
end;

function InitializeSetup: Boolean;
var ErrorText: String;
begin
  Result := False;
  FixturePrefix := '{#RegistryFixtureRoot}';
  if Pos('Software\Pates2004\eSpeakInstallerFixture\', FixturePrefix) <> 1 then Exit;
  if RegKeyExists(HKEY_CURRENT_USER, FixturePrefix) then Exit;
  try
    RunCases;
    SaveStringToFile('{#FixtureOutput}\registration-result.txt', 'PASS assertions=' + IntToStr(Assertions), False);
  except
    ErrorText := GetExceptionMessage;
    SaveStringToFile('{#FixtureOutput}\registration-error.txt', ErrorText, False);
  end;
  if RegKeyExists(HKEY_CURRENT_USER, FixturePrefix) then
    RegDeleteKeyIncludingSubkeys(HKEY_CURRENT_USER, FixturePrefix);
  // Deliberately abort before the wizard, file installation or host registration.
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  SaveStringToFile('{#FixtureOutput}\installation-requested.txt', 'FAIL', False);
  RaiseException('Registry fixture must never install anything.');
end;
