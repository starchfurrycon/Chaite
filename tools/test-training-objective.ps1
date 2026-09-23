param(
    # Defaults to the real trainer. Overridable so the check can be pointed at a
    # previous revision of the file to prove it is not vacuous: run against the
    # revision that ranked on the clean-loop count, it has to fail.
    [string]$Trainer = ''
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# Pins the policy search's ordering, because that ordering was wrong for three
# rounds and nothing noticed.
#
# The search used to rank on the COUNT of clean closed loops, with hits second
# and "win" nowhere at all. Scoring the nine measured combinations with that
# comparator put a route that never won, died in all five seeds and took
# sixty-eight hits first, and the only route that has ever won the fight fifth.
# The order below is therefore not a style preference: it is what makes the
# search point at the acceptance criterion, which is a win with zero hits.
#
# The check reads the ordering out of the source rather than trusting a comment,
# because the comment and the code disagreed for exactly as long as the bug
# existed: the file's own header described hitsPerThousandTicks, cleanLoopShare,
# damage while the code ran clean-count, hits, damage. Any future edit that
# reorders the terms without updating the header, or the reverse, fails here.

$root = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$trainer = if ([string]::IsNullOrEmpty($Trainer)) { Join-Path $PSScriptRoot 'train-policy.ps1' } else { $Trainer }
if (-not (Test-Path -LiteralPath $trainer)) { throw "Trainer not found: $trainer" }
$source = [IO.File]::ReadAllText($trainer)

function Get-FunctionText([string]$Path, [string]$Name) {
    $tokens = $null; $errors = $null
    $ast = [Management.Automation.Language.Parser]::ParseFile($Path, [ref]$tokens, [ref]$errors)
    if ($errors.Count -ne 0) { throw "train-policy.ps1 does not parse: $($errors[0].Message)" }
    $found = @($ast.FindAll({ param($node)
        $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -ceq $Name }, $true))
    if ($found.Count -ne 1) { throw "Ambiguous or missing function: $Name" }
    return $found[0].Extent.Text
}

$script:passed = 0
function Assert-True([bool]$Condition, [string]$Name) {
    if (-not $Condition) { throw "Training objective regression: $Name" }
    $script:passed++
    Write-Output "PASS $Name"
}

# 1. The ordering, read out of the code.
#
# Every comparison in Test-Better is of the form
#   if ($Candidate.<Field> -gt|-lt $Incumbent.<Field>) { return ... }
# so the sequence of field names is the priority order. It is deduplicated
# because each term appears twice, once per direction.
$body = [regex]::Match($source, '(?s)function Test-Better \{.*?\n\}').Value
if ([string]::IsNullOrEmpty($body)) { throw 'Test-Better was not found in train-policy.ps1.' }
$order = @()
foreach ($match in [regex]::Matches($body, '\$Candidate\.(\w+)\s+-(?:gt|lt)\s+\$Incumbent\.')) {
    $field = $match.Groups[1].Value
    if ($order -notcontains $field) { $order += $field }
}
$expectedOrder = @('NoHitWins', 'Wins', 'Deaths', 'Hits', 'CleanShare', 'PlayerDamagePerK', 'Damage')
Assert-True (($order -join ',') -ceq ($expectedOrder -join ',')) `
    "ordering is $($order -join ' > ')"

# 2. The header must describe the ordering the code implements, in the same
#    sequence. This is the check that the old file would have failed.
#
#    Only the numbered term headings are read, not the prose under them: the
#    explanation of one term naturally mentions another ("a hits total can
#    hide"), and matching free text would compare the wrong occurrences. The
#    headings are the ordering declaration.
$header = $source.Substring(0, $source.IndexOf('[CmdletBinding()]'))
$headings = @()
foreach ($line in ($header -split "`n")) {
    $heading = [regex]::Match($line, '^#\s+\d+\.\s+(?<term>[A-Za-z]+)\s*,')
    if ($heading.Success) { $headings += $heading.Groups['term'].Value }
}
if ($headings.Count -eq 0) { throw 'Training objective regression: the header declares no ordering.' }
$expectedHeadings = @('noHitWins', 'wins', 'deaths', 'hits', 'cleanLoopShare', 'playerDamagePerK', 'damage')
Assert-True (($headings -join ',') -ceq ($expectedHeadings -join ',')) `
    "header ordering is $($headings -join ' > ')"

. ([scriptblock]::Create((Get-FunctionText -Path $trainer -Name 'Test-Better')))

function New-Score([hashtable]$Values) {
    $base = @{ NoHitWins = 0; Wins = 0; Deaths = 0; Hits = 0; CleanShare = 0.0
               PlayerDamagePerK = 0.0; Damage = 0; Clean = 0 }
    foreach ($key in $Values.Keys) { $base[$key] = $Values[$key] }
    return [pscustomobject]$base
}

# 3. The criterion itself outranks everything.
Assert-True (Test-Better -Candidate (New-Score @{ NoHitWins = 1 }) `
        -Incumbent (New-Score @{ Wins = 5; CleanShare = 100.0; Clean = 999 })) `
    'a no-hit win outranks a perfect loop proxy'

# 4. The exact defect, replayed: the route that won two seeds must beat the one
#    with the most clean loops and no wins. These are the measured numbers for
#    the Fishron strong-wing route and the Fishron fairy-wing route.
$winner = New-Score @{ Wins = 2; Deaths = 4; Hits = 38; CleanShare = 67.2; Clean = 39 }
$loopRich = New-Score @{ Wins = 0; Deaths = 5; Hits = 68; CleanShare = 81.3; Clean = 87 }
Assert-True (Test-Better -Candidate $winner -Incumbent $loopRich) `
    'the winning route outranks the loop-rich losing route'
Assert-True (-not (Test-Better -Candidate $loopRich -Incumbent $winner)) `
    'the loop-rich losing route does not outrank the winning route'

# 5. Deaths must be compared before hits, or a run that dies early looks safe:
#    it accumulates fewer hits than one that survives.
$survives = New-Score @{ Wins = 1; Deaths = 0; Hits = 12 }
$diesFast = New-Score @{ Wins = 1; Deaths = 1; Hits = 3 }
Assert-True (Test-Better -Candidate $survives -Incumbent $diesFast) `
    'surviving outranks dying with fewer hits'
Assert-True (-not (Test-Better -Candidate $diesFast -Incumbent $survives)) `
    'dying with fewer hits does not outrank surviving'

# 6. Wins are compared before deaths and hits, so a policy that starts winning
#    is preferred even while it is still dying.
Assert-True (Test-Better -Candidate (New-Score @{ Wins = 1; Deaths = 5; Hits = 40 }) `
        -Incumbent (New-Score @{ Wins = 0; Deaths = 0; Hits = 1 })) `
    'a win outranks a clean loss'

# 7. The loop share is a ratio over loops produced, so a longer fight does not
#    buy a better score with it: the same share on more loops is a tie, and the
#    tie falls through to the damage rate rather than to the loop count.
Assert-True (-not (Test-Better `
        -Candidate (New-Score @{ CleanShare = 80.0; Clean = 800; PlayerDamagePerK = 200.0 }) `
        -Incumbent (New-Score @{ CleanShare = 80.0; Clean = 80; PlayerDamagePerK = 200.0 }))) `
    'a bigger clean-loop count does not win on the share term'
Assert-True (Test-Better `
        -Candidate (New-Score @{ CleanShare = 80.0; Clean = 80; PlayerDamagePerK = 180.0 }) `
        -Incumbent (New-Score @{ CleanShare = 80.0; Clean = 800; PlayerDamagePerK = 200.0 })) `
    'the damage rate breaks a share tie'

# 8. The tolerance applies to the damage rate only, and it suppresses a
#    difference rather than vetoing the candidate: a rate inside the band does
#    not by itself decide, so the comparison falls through to damage dealt. That
#    is the intended reading -- the band says "this difference is smaller than
#    the seed-to-seed spread", not "this candidate loses".
Assert-True (-not (Test-Better `
        -Candidate (New-Score @{ PlayerDamagePerK = 195.0; Damage = 1 }) `
        -Incumbent (New-Score @{ PlayerDamagePerK = 200.0; Damage = 1 }))) `
    'a damage rate inside the tolerance decides nothing on its own'
Assert-True (Test-Better `
        -Candidate (New-Score @{ PlayerDamagePerK = 195.0; Damage = 999999 }) `
        -Incumbent (New-Score @{ PlayerDamagePerK = 200.0; Damage = 1 })) `
    'inside the tolerance the damage tie-break decides'
Assert-True (Test-Better `
        -Candidate (New-Score @{ PlayerDamagePerK = 180.0; Damage = 1 }) `
        -Incumbent (New-Score @{ PlayerDamagePerK = 200.0; Damage = 999999 })) `
    'a clearly lower damage rate outranks more damage dealt'
Assert-True (-not (Test-Better `
        -Candidate (New-Score @{ PlayerDamagePerK = 220.0; Damage = 999999 }) `
        -Incumbent (New-Score @{ PlayerDamagePerK = 200.0; Damage = 1 }))) `
    'a clearly higher damage rate is not bought with damage dealt'
Assert-True (Test-Better `
        -Candidate (New-Score @{ PlayerDamagePerK = 200.0; Damage = 999999 }) `
        -Incumbent (New-Score @{ PlayerDamagePerK = 200.0; Damage = 1 })) `
    'damage dealt breaks a full tie'

# 9. An unusable score is never an improvement, and any usable score beats none.
Assert-True (-not (Test-Better -Candidate $null -Incumbent $winner)) `
    'an unusable candidate is rejected'
Assert-True (Test-Better -Candidate $winner -Incumbent $null) `
    'a usable candidate replaces nothing'

# 10. The scorer must actually produce the fields the ordering reads, or every
#     comparison silently sees $null and the search degrades to the last term.
foreach ($field in @('NoHitWins', 'Wins', 'Deaths', 'Hits', 'CleanShare', 'PlayerDamagePerK', 'Damage')) {
    if ($source -notmatch ('(?m)^\s*' + $field + '\s+=')) {
        throw "Training objective regression: Get-TagScore never sets $field"
    }
}
$script:passed++
Write-Output 'PASS scorer-publishes-every-ranked-field'

# 11. NoHitWins must be the acceptance criterion and not a looser reading of it:
#     it counts wins with zero hits, so it has to test both.
$scorer = [regex]::Match($source, '(?s)function Get-TagScore \{.*?\n\}').Value
Assert-True ($scorer -match 'win -eq \$true -and \[int\]\$r\.hits -eq 0') `
    'no-hit wins require both a win and zero hits'

Write-Output "PASS training objective contract: $($script:passed) checks, no game process, no probe run."
exit 0
