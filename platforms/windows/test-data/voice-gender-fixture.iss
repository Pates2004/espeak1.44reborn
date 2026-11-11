#ifndef FixtureOutput
  #error Supply an isolated fixture output directory.
#endif
#ifndef CaseFile
  #error Supply the read-only case table.
#endif
#ifndef ResultFile
  #error Supply the exact result path.
#endif

[Setup]
AppName=eSpeak voice metadata fixture
AppVersion=1.0
CreateAppDir=no
Uninstallable=no
CreateUninstallRegKey=no
PrivilegesRequired=lowest
OutputDir={#FixtureOutput}
OutputBaseFilename=voice-gender-fixture
Compression=none
SetupLogging=no

[CustomMessages]
GenderReadFailed=The voice definition could not be read: %1

[Code]
#include "..\voice-gender.iss"

function InitializeSetup: Boolean;
var
  Lines: TArrayOfString;
  Index: Integer;
  Separator: Integer;
  DirectoryName: String;
  VoiceName: String;
  Expected: String;
  Actual: String;
  Line: String;
begin
  Result := False;
  if not LoadStringsFromFile('{#CaseFile}', Lines) then
    RaiseException('Cannot read the fixture case table.');
  if GetArrayLength(Lines) = 0 then
    RaiseException('The fixture case table is empty.');
  for Index := 0 to GetArrayLength(Lines) - 1 do
  begin
    Line := Lines[Index];
    Separator := Pos(#9, Line);
    if Separator = 0 then RaiseException('Missing case directory.');
    DirectoryName := Copy(Line, 1, Separator - 1);
    Line := Copy(Line, Separator + 1, Length(Line));
    Separator := Pos(#9, Line);
    if Separator = 0 then RaiseException('Missing expected gender.');
    VoiceName := Copy(Line, 1, Separator - 1);
    Expected := Copy(Line, Separator + 1, Length(Line));
    Actual := VoiceGender(DirectoryName, VoiceName);
    if Actual <> Expected then
      RaiseException(VoiceName + ': expected ' + Expected + ', actual ' + Actual);
  end;
  if not SaveStringToFile('{#ResultFile}', 'PASS cases=' + IntToStr(GetArrayLength(Lines)), False) then
    RaiseException('Cannot write the fixture result.');
  // Explicitly abort before wizard creation, file installation or registration.
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  SaveStringToFile('{#ResultFile}.installation-requested', 'FAIL', False);
  RaiseException('This parser fixture must never enter installation.');
end;
