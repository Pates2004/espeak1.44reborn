// Transactional managed-token registration, shared with an isolated fixture.
type
  // Inno Setup 6's script process is 32-bit, including for the 64-bit view.
  HKEY = Integer;
  TStoredRegistryValue = record
    Path: String;
    Name: String;
    Kind: Cardinal;
    Data: AnsiString;
  end;
  TStoredRegistryKey = record
    Path: String;
    Security: AnsiString;
  end;
  TRegisteredVoice = record
    Token: String;
    Name: String;
    Inflection: Cardinal;
  end;
var
  RegistrationRoot: HKEY;
  RegistrationNativeRoot: HKEY;
  RegistrationView: Cardinal;
  RegistrationVoicesPath: String;
  RegistrationConverterPath: String;
  OriginalTokens: TArrayOfString;
  OriginalVoices: array of TRegisteredVoice;
  PlannedVoices: array of TRegisteredVoice;
  StoredValues: array of TStoredRegistryValue;
  StoredKeys: array of TStoredRegistryKey;
  RegistrationSnapshotReady: Boolean;
  RegistrationStarted: Boolean;
  RegistrationCommitted: Boolean;

procedure SetupVoice(Voice, TokenName: String; Inflection: Cardinal); forward;

procedure ConfigureVoiceRegistration(Root, NativeRoot: HKEY; View: Cardinal;
  VoicesPath, ConverterPath: String);
begin
  RegistrationRoot := Root;
  RegistrationNativeRoot := NativeRoot;
  RegistrationView := View;
  RegistrationVoicesPath := VoicesPath;
  RegistrationConverterPath := ConverterPath;
  RegistrationSnapshotReady := False;
  RegistrationStarted := False;
  RegistrationCommitted := False;
  SetArrayLength(PlannedVoices, 0);
end;

function ProfileOpenKey(Root: HKEY; Path: String; Reserved, Access: Cardinal; var Key: HKEY): Integer;
  external 'RegOpenKeyExW@advapi32.dll stdcall';
function ProfileCreateKey(Root: HKEY; Path: String; Reserved: Cardinal; ClassName: Integer;
  Options, Access: Cardinal; SecurityAttributes: Integer; var Key: HKEY; var Disposition: Cardinal): Integer;
  external 'RegCreateKeyExW@advapi32.dll stdcall';
function ProfileCloseKey(Key: HKEY): Integer;
  external 'RegCloseKey@advapi32.dll stdcall';
function ProfileValueSize(Key: HKEY; Name: String; Reserved: Integer; var Kind: Cardinal;
  Data: Integer; var Bytes: Cardinal): Integer;
  external 'RegQueryValueExW@advapi32.dll stdcall';
function ProfileReadValue(Key: HKEY; Name: String; Reserved: Integer; var Kind: Cardinal;
  Data: AnsiString; var Bytes: Cardinal): Integer;
  external 'RegQueryValueExW@advapi32.dll stdcall';
function ProfileWriteValue(Key: HKEY; Name: String; Reserved, Kind: Cardinal;
  Data: AnsiString; Bytes: Cardinal): Integer;
  external 'RegSetValueExW@advapi32.dll stdcall';
function ProfileSecuritySize(Key: HKEY; Information: Cardinal; Data: Integer; var Bytes: Cardinal): Integer;
  external 'RegGetKeySecurity@advapi32.dll stdcall';
function ProfileReadSecurity(Key: HKEY; Information: Cardinal; Data: AnsiString; var Bytes: Cardinal): Integer;
  external 'RegGetKeySecurity@advapi32.dll stdcall';
function ProfileWriteSecurity(Key: HKEY; Information: Cardinal; Data: AnsiString): Integer;
  external 'RegSetKeySecurity@advapi32.dll stdcall';

procedure RequireRegistration(Succeeded: Boolean);
begin
  if not Succeeded then RaiseException(CustomMessage('RegistrationFailed'));
end;

function IsManagedVoiceToken(Name: String): Boolean;
var Index: Integer;
begin
  Name := Lowercase(Name);
  Result := Name = 'espeak';
  if Result then Exit;
  if (Copy(Name, 1, 7) <> 'espeak_') or (Length(Name) < 8) then Exit;
  for Index := 8 to Length(Name) do
    if (Name[Index] < '0') or (Name[Index] > '9') then Exit;
  Result := True;
end;

function ManagedVoiceTokens: TArrayOfString;
var Names: TArrayOfString; Index, Count: Integer;
begin
  SetArrayLength(Result, 0);
  if not RegKeyExists(RegistrationRoot, RegistrationVoicesPath) then Exit;
  RequireRegistration(RegGetSubkeyNames(RegistrationRoot, RegistrationVoicesPath, Names));
  for Index := 0 to GetArrayLength(Names) - 1 do
    if IsManagedVoiceToken(Names[Index]) then
    begin
      Count := GetArrayLength(Result);
      SetArrayLength(Result, Count + 1);
      Result[Count] := Names[Index];
    end;
end;

procedure CaptureRegistrationTree(Path: String);
var
  Key: HKEY;
  Names, Children: TArrayOfString;
  Index, Count: Integer;
  Kind, Bytes: Cardinal;
  Value, Security: AnsiString;
begin
  RequireRegistration(ProfileOpenKey(RegistrationNativeRoot, Path, 0, $20019 or RegistrationView, Key) = 0);
  try
    Bytes := 0;
    ProfileSecuritySize(Key, 4, 0, Bytes);
    RequireRegistration(Bytes > 0);
    SetLength(Security, Bytes);
    RequireRegistration(ProfileReadSecurity(Key, 4, Security, Bytes) = 0);
    SetLength(Security, Bytes);
    Count := GetArrayLength(StoredKeys);
    SetArrayLength(StoredKeys, Count + 1);
    StoredKeys[Count].Path := Path;
    StoredKeys[Count].Security := Security;
    RequireRegistration(RegGetValueNames(RegistrationRoot, Path, Names));
    for Index := 0 to GetArrayLength(Names) - 1 do
    begin
      Bytes := 0;
      RequireRegistration(ProfileValueSize(Key, Names[Index], 0, Kind, 0, Bytes) = 0);
      SetLength(Value, Bytes);
      RequireRegistration(ProfileReadValue(Key, Names[Index], 0, Kind, Value, Bytes) = 0);
      SetLength(Value, Bytes);
      Count := GetArrayLength(StoredValues);
      SetArrayLength(StoredValues, Count + 1);
      StoredValues[Count].Path := Path;
      StoredValues[Count].Name := Names[Index];
      StoredValues[Count].Kind := Kind;
      StoredValues[Count].Data := Value;
    end;
  finally
    ProfileCloseKey(Key);
  end;
  RequireRegistration(RegGetSubkeyNames(RegistrationRoot, Path, Children));
  for Index := 0 to GetArrayLength(Children) - 1 do
    CaptureRegistrationTree(Path + '\' + Children[Index]);
end;

procedure ReadRegisteredVoices;
var Index, Count: Integer; Name: String; Inflection: Cardinal;
begin
  SetArrayLength(OriginalVoices, 0);
  OriginalTokens := ManagedVoiceTokens;
  for Index := 0 to GetArrayLength(OriginalTokens) - 1 do
  begin
    if RegQueryStringValue(RegistrationRoot, RegistrationVoicesPath + '\' + OriginalTokens[Index], 'VoiceName', Name) and (Trim(Name) <> '') then
    begin
      Inflection := 50;
      RegQueryDWordValue(RegistrationRoot, RegistrationVoicesPath + '\' + OriginalTokens[Index], 'Inflection', Inflection);
      if Inflection > 100 then Inflection := 100;
      Count := GetArrayLength(OriginalVoices);
      SetArrayLength(OriginalVoices, Count + 1);
      OriginalVoices[Count].Token := OriginalTokens[Index];
      OriginalVoices[Count].Name := Name;
      OriginalVoices[Count].Inflection := Inflection;
    end;
  end;
end;

procedure CaptureVoiceRegistration;
var Index: Integer;
begin
  if RegistrationSnapshotReady then Exit;
  SetArrayLength(StoredKeys, 0);
  SetArrayLength(StoredValues, 0);
  ReadRegisteredVoices;
  for Index := 0 to GetArrayLength(OriginalTokens) - 1 do
    CaptureRegistrationTree(RegistrationVoicesPath + '\' + OriginalTokens[Index]);
  if RegKeyExists(RegistrationRoot, RegistrationConverterPath) then
    CaptureRegistrationTree(RegistrationConverterPath);
  RegistrationSnapshotReady := True;
end;

procedure DeleteRegistrationTree(Path: String);
begin
  if RegKeyExists(RegistrationRoot, Path) then
    RequireRegistration(RegDeleteKeyIncludingSubkeys(RegistrationRoot, Path));
end;

procedure RestoreVoiceRegistration;
var Names: TArrayOfString; Index: Integer; Key: HKEY; Disposition, Information: Cardinal;
begin
  Names := ManagedVoiceTokens;
  for Index := 0 to GetArrayLength(Names) - 1 do
    DeleteRegistrationTree(RegistrationVoicesPath + '\' + Names[Index]);
  DeleteRegistrationTree(RegistrationConverterPath);
  for Index := 0 to GetArrayLength(StoredKeys) - 1 do
  begin
    RequireRegistration(ProfileCreateKey(RegistrationNativeRoot, StoredKeys[Index].Path, 0, 0, 0,
      $F003F or RegistrationView, 0, Key, Disposition) = 0);
    ProfileCloseKey(Key);
  end;
  for Index := 0 to GetArrayLength(StoredValues) - 1 do
  begin
    RequireRegistration(ProfileOpenKey(RegistrationNativeRoot, StoredValues[Index].Path, 0,
      $F003F or RegistrationView, Key) = 0);
    try
      RequireRegistration(ProfileWriteValue(Key, StoredValues[Index].Name, 0, StoredValues[Index].Kind,
        StoredValues[Index].Data, Length(StoredValues[Index].Data)) = 0);
    finally ProfileCloseKey(Key); end;
  end;
  for Index := GetArrayLength(StoredKeys) - 1 downto 0 do
  begin
    RequireRegistration(ProfileOpenKey(RegistrationNativeRoot, StoredKeys[Index].Path, 0,
      $F003F or RegistrationView, Key) = 0);
    try
      Information := 4;
      if Length(StoredKeys[Index].Security) >= 4 then
        if (Ord(StoredKeys[Index].Security[4]) and $10) <> 0 then Information := Information or $80000000
        else Information := Information or $20000000;
      RequireRegistration(ProfileWriteSecurity(Key, Information, StoredKeys[Index].Security) = 0);
    finally ProfileCloseKey(Key); end;
  end;
  RegistrationStarted := False;
end;

function TokenReserved(Name: String): Boolean;
var Index: Integer;
begin
  Result := True;
  for Index := 0 to GetArrayLength(OriginalTokens) - 1 do
    if CompareText(Name, OriginalTokens[Index]) = 0 then Exit;
  for Index := 0 to GetArrayLength(PlannedVoices) - 1 do
    if CompareText(Name, PlannedVoices[Index].Token) = 0 then Exit;
  Result := False;
end;

procedure PlanVoice(Name: String);
var Index, Count, Candidate: Integer; Token: String; Inflection: Cardinal;
begin
  Name := Trim(Name);
  if Name = '' then Exit;
  for Index := 0 to GetArrayLength(PlannedVoices) - 1 do
    if CompareText(Name, PlannedVoices[Index].Name) = 0 then Exit;
  Count := GetArrayLength(PlannedVoices);
  if Count >= 200 then RaiseException(CustomMessage('TooManyRegistrationVoices'));
  Token := '';
  Inflection := 50;
  for Index := 0 to GetArrayLength(OriginalVoices) - 1 do
    if CompareText(Name, OriginalVoices[Index].Name) = 0 then
    begin
      Token := OriginalVoices[Index].Token;
      Inflection := OriginalVoices[Index].Inflection;
      Break;
    end;
  if Token = '' then
  begin
    Candidate := 0;
    repeat
      if Candidate = 0 then Token := 'eSpeak' else Token := 'eSpeak_' + IntToStr(Candidate);
      Candidate := Candidate + 1;
    until not TokenReserved(Token);
  end;
  SetArrayLength(PlannedVoices, Count + 1);
  PlannedVoices[Count].Token := Token;
  PlannedVoices[Count].Name := Name;
  PlannedVoices[Count].Inflection := Inflection;
end;

procedure PlanVoiceLine(Line: String);
var Index: Integer; Word: String;
begin
  Word := '';
  Line := Line + ' ';
  for Index := 1 to Length(Line) do
  begin
    if Line[Index] <= ' ' then
    begin
      PlanVoice(Word);
      Word := '';
    end else Word := Word + Line[Index];
  end;
end;

procedure ApplyVoiceRegistration;
var Index, Desired: Integer; Keep: Boolean; OriginalError, RestoreError: String;
begin
  RequireRegistration(RegistrationSnapshotReady);
  RegistrationStarted := True;
  try
    for Index := 0 to GetArrayLength(PlannedVoices) - 1 do
      SetupVoice(PlannedVoices[Index].Name, PlannedVoices[Index].Token, PlannedVoices[Index].Inflection);
    for Index := 0 to GetArrayLength(OriginalTokens) - 1 do
    begin
      Keep := False;
      for Desired := 0 to GetArrayLength(PlannedVoices) - 1 do
        if CompareText(OriginalTokens[Index], PlannedVoices[Desired].Token) = 0 then Keep := True;
      if not Keep then DeleteRegistrationTree(RegistrationVoicesPath + '\' + OriginalTokens[Index]);
    end;
  except
    OriginalError := GetExceptionMessage;
    try RestoreVoiceRegistration;
    except
      RestoreError := GetExceptionMessage;
      RegistrationStarted := False;
      RaiseException(CustomMessage('RegistrationRollbackFailed') + #13#10 + OriginalError + #13#10 + RestoreError);
    end;
    RaiseException(OriginalError);
  end;
end;
