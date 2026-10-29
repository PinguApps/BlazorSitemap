param(
    [Parameter(Mandatory)][AllowEmptyString()][string]$Tag,
    [AllowEmptyString()][string]$Name,
    [switch]$Stable
)
$ErrorActionPreference = 'Stop'

# SemVer 2.0: ASCII digits, no leading zeroes, and nonempty identifiers.
$number = '(?:0|[1-9][0-9]*)'
$identifier = '(?:0|[1-9][0-9]*|[0-9]*[A-Za-z-][0-9A-Za-z-]*)'
$pattern = "\Av(?<version>$number\.$number\.$number(?:-$identifier(?:\.$identifier)*)?(?:\+[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?)\z"
$match = [regex]::Match($Tag, $pattern)
if (-not $match.Success) { throw 'Release tag must be v<SemVer>, for example v2.0.1.' }
if ($Stable -and $Tag -cnotmatch "\Av$number\.$number\.$number\z") {
    throw 'Scheduled releases require a stable v<major>.<minor>.<patch> tag.'
}
if ($PSBoundParameters.ContainsKey('Name') -and $Name -cne $Tag) {
    throw 'Release name must exactly match its v<SemVer> tag.'
}
$match.Groups['version'].Value
