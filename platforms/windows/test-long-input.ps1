[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$EspeakExe,

    [Parameter(Mandatory = $true)]
    [string]$DataPath
)

$ErrorActionPreference = 'Stop'

$cases = @(
    @{ Name = '690-byte unbroken word'; Text = ('a' * 690) },
    @{ Name = '1200-byte unbroken word'; Text = ('a' * 1200) },
    @{ Name = 'long UTF-8 word'; Text = ([string][char]0x0105 * 340) },
    @{ Name = '120-digit integer'; Text = ('1' * 120) },
    @{ Name = 'long decimal fraction'; Text = ('3,' + ('5' * 120)) }
)

foreach ($case in $cases) {
    & $EspeakExe "--path=$DataPath" -q -v pl $case.Text
    if ($LASTEXITCODE -ne 0) {
        throw "eSpeak failed for $($case.Name) (exit code $LASTEXITCODE)."
    }
}

Write-Host 'Long-input tests OK: ASCII, UTF-8, integer and decimal boundary cases.'
