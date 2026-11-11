// Shared by installation and the isolated metadata regression fixture.
function ReadDeclaredVoiceGender(FileName: String): String;
var
  Lines: TArrayOfString;
  Index: Integer;
  Separator: Integer;
  Line: String;
  Field: String;
  Value: String;
begin
  Result := '';
  if not FileExists(FileName) then
    Exit;
  if not LoadStringsFromFile(FileName, Lines) then
    RaiseException(FmtMessage(CustomMessage('GenderReadFailed'), [FileName]));
  for Index := 0 to GetArrayLength(Lines) - 1 do
  begin
    Line := Trim(Lines[Index]);
    Separator := Pos('//', Line);
    if Separator > 0 then
      Line := Copy(Line, 1, Separator - 1);
    StringChangeEx(Line, #9, ' ', True);
    Separator := Pos(' ', Line);
    if Separator > 0 then
    begin
      Field := Lowercase(Copy(Line, 1, Separator - 1));
      Value := Lowercase(Trim(Copy(Line, Separator + 1, Length(Line))));
      Separator := Pos(' ', Value);
      if Separator > 0 then
        Value := Copy(Value, 1, Separator - 1);
      if Field = 'gender' then
      begin
        if Value = 'female' then Result := 'Female';
        if Value = 'male' then Result := 'Male';
      end;
    end;
  end;
end;

function FindBaseVoiceGender(DirectoryName: String; VoiceName: String): String;
var
  Entry: TFindRec;
  ChildName: String;
begin
  Result := '';
  if FileExists(AddBackslash(DirectoryName) + VoiceName) then
  begin
    Result := ReadDeclaredVoiceGender(AddBackslash(DirectoryName) + VoiceName);
    Exit;
  end;
  if FindFirst(AddBackslash(DirectoryName) + '*', Entry) then
  begin
    try
      repeat
        ChildName := Lowercase(Entry.Name);
        if ((Entry.Attributes and FILE_ATTRIBUTE_DIRECTORY) <> 0) and
          (ChildName <> '.') and (ChildName <> '..') and
          (ChildName <> '!v') and (ChildName <> 'test') then
        begin
          Result := FindBaseVoiceGender(AddBackslash(DirectoryName) + Entry.Name, VoiceName);
          if Result <> '' then
            Exit;
        end;
      until not FindNext(Entry);
    finally
      FindClose(Entry);
    end;
  end;
end;

function VoiceGender(VoicesDirectory: String; VoiceName: String): String;
var
  Separator: Integer;
  BaseName: String;
  VariantName: String;
  DeclaredGender: String;
begin
  Result := 'Male';
  Separator := Pos('+', VoiceName);
  BaseName := VoiceName;
  VariantName := '';
  if Separator > 0 then
  begin
    BaseName := Copy(VoiceName, 1, Separator - 1);
    VariantName := Copy(VoiceName, Separator + 1, Length(VoiceName));
  end;
  // A voice identifier is a file name, not a path.
  if ExtractFileName(BaseName) = BaseName then
  begin
    DeclaredGender := FindBaseVoiceGender(VoicesDirectory, BaseName);
    if DeclaredGender <> '' then
      Result := DeclaredGender;
  end;
  if (VariantName <> '') and (ExtractFileName(VariantName) = VariantName) then
  begin
    DeclaredGender := ReadDeclaredVoiceGender(AddBackslash(VoicesDirectory) + '!v\' + VariantName);
    if DeclaredGender <> '' then
      Result := DeclaredGender;
  end;
end;
