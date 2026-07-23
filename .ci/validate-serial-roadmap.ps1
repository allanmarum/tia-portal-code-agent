#Requires -Version 5.1
[CmdletBinding()]
param(
    [string]$HeadRoadmapPath,
    [string]$BaseRoadmapPath,
    [string]$PullRequestBody,
    [string]$PullRequestTitle,
    [string]$BranchName,
    [switch]$SelfTest
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Assert-Condition {
    param(
        [Parameter(Mandatory = $true)][bool]$Condition,
        [Parameter(Mandatory = $true)][string]$Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

function Read-Roadmap {
    param([Parameter(Mandatory = $true)][string]$Path)

    Assert-Condition (Test-Path -LiteralPath $Path) "Roadmap file not found: $Path"
    try {
        return Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
    }
    catch {
        throw "Invalid roadmap JSON at '$Path': $($_.Exception.Message)"
    }
}

function Get-Items {
    param([Parameter(Mandatory = $true)]$Roadmap)
    return @($Roadmap.items)
}

function Get-ItemIndex {
    param(
        [Parameter(Mandatory = $true)]$Roadmap,
        [Parameter(Mandatory = $true)][string]$Key
    )

    $items = Get-Items $Roadmap
    for ($index = 0; $index -lt $items.Count; $index++) {
        if ([string]$items[$index].key -eq $Key) {
            return $index
        }
    }

    return -1
}

function Assert-RoadmapShape {
    param(
        [Parameter(Mandatory = $true)]$Roadmap,
        [Parameter(Mandatory = $true)][string]$Context
    )

    Assert-Condition ([int]$Roadmap.schemaVersion -eq 1) "$Context roadmap must use schemaVersion 1."
    Assert-Condition ([string]$Roadmap.executionMode -eq 'strictly-serial') "$Context roadmap executionMode must be 'strictly-serial'."

    $items = Get-Items $Roadmap
    Assert-Condition ($items.Count -ge 2) "$Context roadmap must contain at least one issue and the terminal item."

    $keys = @($items | ForEach-Object { [string]$_.key })
    Assert-Condition (($keys | Select-Object -Unique).Count -eq $keys.Count) "$Context roadmap contains duplicate keys."

    $issueNumbers = @($items | Where-Object { $null -ne $_.issue } | ForEach-Object { [int]$_.issue })
    Assert-Condition (($issueNumbers | Select-Object -Unique).Count -eq $issueNumbers.Count) "$Context roadmap contains duplicate issue numbers."

    $terminalKey = [string]$Roadmap.terminal
    Assert-Condition (-not [string]::IsNullOrWhiteSpace($terminalKey)) "$Context roadmap terminal key is required."
    Assert-Condition ([string]$items[$items.Count - 1].key -eq $terminalKey) "$Context roadmap terminal item must be last."
    Assert-Condition ($null -eq $items[$items.Count - 1].issue) "$Context roadmap terminal item must not reference a GitHub issue."

    $currentKey = [string]$Roadmap.current
    Assert-Condition (-not [string]::IsNullOrWhiteSpace($currentKey)) "$Context roadmap current key is required."
    $currentIndex = Get-ItemIndex $Roadmap $currentKey
    Assert-Condition ($currentIndex -ge 0) "$Context roadmap current key '$currentKey' does not exist."

    $activeItems = @($items | Where-Object { [string]$_.status -eq 'active' })
    Assert-Condition ($activeItems.Count -eq 1) "$Context roadmap must contain exactly one active item."
    Assert-Condition ([string]$activeItems[0].key -eq $currentKey) "$Context roadmap current key must match the active item."

    $allowedStatuses = @('done', 'active', 'blocked')
    for ($index = 0; $index -lt $items.Count; $index++) {
        $item = $items[$index]
        $status = [string]$item.status
        Assert-Condition ($allowedStatuses -contains $status) "$Context roadmap item '$($item.key)' has invalid status '$status'."

        if ($index -lt $currentIndex) {
            Assert-Condition ($status -eq 'done') "$Context roadmap item '$($item.key)' appears before current and must be done."
        }
        elseif ($index -eq $currentIndex) {
            Assert-Condition ($status -eq 'active') "$Context roadmap current item '$($item.key)' must be active."
        }
        else {
            Assert-Condition ($status -eq 'blocked') "$Context roadmap item '$($item.key)' appears after current and must be blocked."
        }
    }

    $expectedPrevious = if ($currentIndex -gt 0) { [string]$items[$currentIndex - 1].key } else { $null }
    $actualPrevious = if ($null -eq $Roadmap.previous) { $null } else { [string]$Roadmap.previous }
    Assert-Condition ($actualPrevious -eq $expectedPrevious) "$Context roadmap previous must be '$expectedPrevious'."

    $expectedNext = if ($currentIndex + 1 -lt $items.Count) { [string]$items[$currentIndex + 1].key } else { $null }
    $actualNext = if ($null -eq $Roadmap.next) { $null } else { [string]$Roadmap.next }
    Assert-Condition ($actualNext -eq $expectedNext) "$Context roadmap next must be '$expectedNext'."
}

function Get-PullRequestMetadata {
    param(
        [Parameter(Mandatory = $true)][string]$Body,
        [Parameter(Mandatory = $true)][string]$Title,
        [Parameter(Mandatory = $true)][string]$Branch
    )

    $closeMatches = [regex]::Matches(
        $Body,
        '(?im)^\s*(?:close[sd]?|fix(?:e[sd])?|resolve[sd]?)\s+#(?<issue>\d+)\s*$'
    )
    Assert-Condition ($closeMatches.Count -eq 1) 'PR body must contain exactly one standalone closing line such as: Closes #28'

    $sequenceMatches = [regex]::Matches($Body, '(?im)^\s*Sequence:\s*(?<value>REL-\d{3})\s*$')
    Assert-Condition ($sequenceMatches.Count -eq 1) 'PR body must contain exactly one Sequence: REL-XXX line.'

    $previousMatches = [regex]::Matches($Body, '(?im)^\s*Previous:\s*(?<value>REL-\d{3}|none)\s*$')
    Assert-Condition ($previousMatches.Count -eq 1) 'PR body must contain exactly one Previous: REL-XXX or Previous: none line.'

    $nextMatches = [regex]::Matches($Body, '(?im)^\s*Next:\s*(?<value>REL-\d{3}|RELEASE-COMPLETE)\s*$')
    Assert-Condition ($nextMatches.Count -eq 1) 'PR body must contain exactly one Next: REL-XXX or Next: RELEASE-COMPLETE line.'

    $sequence = $sequenceMatches[0].Groups['value'].Value.ToUpperInvariant()
    $issueNumber = [int]$closeMatches[0].Groups['issue'].Value
    $previous = $previousMatches[0].Groups['value'].Value.ToUpperInvariant()
    $next = $nextMatches[0].Groups['value'].Value.ToUpperInvariant()

    $branchMatch = [regex]::Match(
        $Branch,
        '(?i)^issue/(?<issue>\d+)-(?<sequence>rel-\d{3})-[a-z0-9][a-z0-9-]*$'
    )
    Assert-Condition $branchMatch.Success 'Branch must match: issue/<number>-rel-xxx-<slug>.'
    Assert-Condition ([int]$branchMatch.Groups['issue'].Value -eq $issueNumber) 'Branch issue number must match the Closes line.'
    Assert-Condition ($branchMatch.Groups['sequence'].Value.ToUpperInvariant() -eq $sequence) 'Branch sequence must match the Sequence line.'
    Assert-Condition ($Title.StartsWith("[$sequence]", [System.StringComparison]::OrdinalIgnoreCase)) "PR title must start with [$sequence]."

    return [pscustomobject]@{
        Issue = $issueNumber
        Sequence = $sequence
        Previous = $previous
        Next = $next
    }
}

function Assert-ImmutableItemIdentity {
    param(
        [Parameter(Mandatory = $true)]$BaseRoadmap,
        [Parameter(Mandatory = $true)]$HeadRoadmap
    )

    $baseItems = Get-Items $BaseRoadmap
    $headItems = Get-Items $HeadRoadmap
    Assert-Condition ($baseItems.Count -eq $headItems.Count) 'Roadmap item count cannot change during a normal serial transition.'

    for ($index = 0; $index -lt $baseItems.Count; $index++) {
        $baseItem = $baseItems[$index]
        $headItem = $headItems[$index]
        Assert-Condition ([string]$baseItem.key -eq [string]$headItem.key) "Roadmap key/order changed at index $index."
        Assert-Condition ([string]$baseItem.title -eq [string]$headItem.title) "Roadmap title changed for '$($baseItem.key)'."

        $baseIssue = if ($null -eq $baseItem.issue) { $null } else { [int]$baseItem.issue }
        $headIssue = if ($null -eq $headItem.issue) { $null } else { [int]$headItem.issue }
        Assert-Condition ($baseIssue -eq $headIssue) "Roadmap issue number changed for '$($baseItem.key)'."
    }
}

function Invoke-SerialValidation {
    param(
        [Parameter(Mandatory = $true)][string]$HeadPath,
        [string]$BasePath,
        [Parameter(Mandatory = $true)][string]$Body,
        [Parameter(Mandatory = $true)][string]$Title,
        [Parameter(Mandatory = $true)][string]$Branch
    )

    $head = Read-Roadmap $HeadPath
    Assert-RoadmapShape $head 'Head'
    $metadata = Get-PullRequestMetadata -Body $Body -Title $Title -Branch $Branch

    $hasBase = -not [string]::IsNullOrWhiteSpace($BasePath) -and (Test-Path -LiteralPath $BasePath)
    if (-not $hasBase) {
        $headItems = Get-Items $head
        Assert-Condition ($headItems.Count -ge 3) 'Bootstrap roadmap must contain REL-000, REL-001, and the terminal item.'

        $first = $headItems[0]
        $second = $headItems[1]
        Assert-Condition ([string]$first.key -eq 'REL-000') 'Bootstrap transition must begin with REL-000.'
        Assert-Condition ([string]$first.status -eq 'done') 'Bootstrap transition must mark REL-000 done.'
        Assert-Condition ([string]$second.status -eq 'active') 'Bootstrap transition must activate REL-001.'
        Assert-Condition ($metadata.Sequence -eq [string]$first.key) 'Bootstrap PR Sequence must be REL-000.'
        Assert-Condition ($metadata.Issue -eq [int]$first.issue) 'Bootstrap PR must close the REL-000 issue.'
        Assert-Condition ($metadata.Previous -eq 'NONE') 'Bootstrap PR Previous value must be none.'
        Assert-Condition ($metadata.Next -eq [string]$second.key) 'Bootstrap PR Next value must be REL-001.'
        Assert-Condition ([string]$head.previous -eq [string]$first.key) 'Bootstrap roadmap previous must be REL-000.'
        Assert-Condition ([string]$head.current -eq [string]$second.key) 'Bootstrap roadmap current must be REL-001.'

        Write-Host "Serial roadmap bootstrap validation passed for $($metadata.Sequence) / issue #$($metadata.Issue)."
        return
    }

    $base = Read-Roadmap $BasePath
    Assert-RoadmapShape $base 'Base'
    Assert-ImmutableItemIdentity -BaseRoadmap $base -HeadRoadmap $head

    $baseItems = Get-Items $base
    $headItems = Get-Items $head
    $activeKey = [string]$base.current
    $activeIndex = Get-ItemIndex $base $activeKey
    $activeItem = $baseItems[$activeIndex]
    $nextKey = if ($null -eq $base.next) { $null } else { [string]$base.next }

    Assert-Condition (-not [string]::IsNullOrWhiteSpace($nextKey)) 'Active roadmap item has no next item to activate.'
    $nextIndex = Get-ItemIndex $base $nextKey
    Assert-Condition ($nextIndex -eq ($activeIndex + 1)) 'Base roadmap next item must immediately follow current.'

    Assert-Condition ($metadata.Sequence -eq $activeKey) "PR Sequence must match active roadmap item '$activeKey'."
    Assert-Condition ($metadata.Issue -eq [int]$activeItem.issue) "PR must close issue #$($activeItem.issue)."

    $expectedPreviousMetadata = if ($null -eq $base.previous) { 'NONE' } else { ([string]$base.previous).ToUpperInvariant() }
    Assert-Condition ($metadata.Previous -eq $expectedPreviousMetadata) "PR Previous must be '$expectedPreviousMetadata'."
    Assert-Condition ($metadata.Next -eq $nextKey.ToUpperInvariant()) "PR Next must be '$nextKey'."

    for ($index = 0; $index -lt $baseItems.Count; $index++) {
        $baseStatus = [string]$baseItems[$index].status
        $headStatus = [string]$headItems[$index].status

        if ($index -eq $activeIndex) {
            Assert-Condition ($baseStatus -eq 'active' -and $headStatus -eq 'done') "Current item '$activeKey' must transition from active to done."
        }
        elseif ($index -eq $nextIndex) {
            Assert-Condition ($baseStatus -eq 'blocked' -and $headStatus -eq 'active') "Next item '$nextKey' must transition from blocked to active."
        }
        else {
            Assert-Condition ($baseStatus -eq $headStatus) "Only current and next item statuses may change; '$($baseItems[$index].key)' changed unexpectedly."
        }
    }

    $expectedHeadNext = if ($nextIndex + 1 -lt $headItems.Count) { [string]$headItems[$nextIndex + 1].key } else { $null }
    Assert-Condition ([string]$head.previous -eq $activeKey) "Head roadmap previous must be '$activeKey'."
    Assert-Condition ([string]$head.current -eq $nextKey) "Head roadmap current must be '$nextKey'."

    $actualHeadNext = if ($null -eq $head.next) { $null } else { [string]$head.next }
    Assert-Condition ($actualHeadNext -eq $expectedHeadNext) "Head roadmap next must be '$expectedHeadNext'."

    Write-Host "Serial roadmap transition validation passed: $activeKey -> $nextKey (issue #$($metadata.Issue))."
}

function Invoke-SelfTest {
    $root = Join-Path ([System.IO.Path]::GetTempPath()) ("tia-agent-serial-test-" + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $root -Force | Out-Null

    try {
        $base = [ordered]@{
            schemaVersion = 1
            repository = 'example/repo'
            executionMode = 'strictly-serial'
            previous = $null
            current = 'REL-000'
            next = 'REL-001'
            terminal = 'RELEASE-COMPLETE'
            items = @(
                [ordered]@{ key = 'REL-000'; issue = 10; title = 'First'; status = 'active' },
                [ordered]@{ key = 'REL-001'; issue = 11; title = 'Second'; status = 'blocked' },
                [ordered]@{ key = 'RELEASE-COMPLETE'; issue = $null; title = 'Complete'; status = 'blocked' }
            )
        }
        $head = [ordered]@{
            schemaVersion = 1
            repository = 'example/repo'
            executionMode = 'strictly-serial'
            previous = 'REL-000'
            current = 'REL-001'
            next = 'RELEASE-COMPLETE'
            terminal = 'RELEASE-COMPLETE'
            items = @(
                [ordered]@{ key = 'REL-000'; issue = 10; title = 'First'; status = 'done' },
                [ordered]@{ key = 'REL-001'; issue = 11; title = 'Second'; status = 'active' },
                [ordered]@{ key = 'RELEASE-COMPLETE'; issue = $null; title = 'Complete'; status = 'blocked' }
            )
        }

        $basePath = Join-Path $root 'base.json'
        $headPath = Join-Path $root 'head.json'
        $base | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $basePath -Encoding UTF8
        $head | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $headPath -Encoding UTF8

        Invoke-SerialValidation `
            -HeadPath $headPath `
            -BasePath $basePath `
            -Body "Closes #10`n`nSequence: REL-000`nPrevious: none`nNext: REL-001" `
            -Title '[REL-000] Test valid transition' `
            -Branch 'issue/10-rel-000-test-transition'

        $invalidHead = [ordered]@{
            schemaVersion = 1
            repository = 'example/repo'
            executionMode = 'strictly-serial'
            previous = 'REL-001'
            current = 'RELEASE-COMPLETE'
            next = $null
            terminal = 'RELEASE-COMPLETE'
            items = @(
                [ordered]@{ key = 'REL-000'; issue = 10; title = 'First'; status = 'done' },
                [ordered]@{ key = 'REL-001'; issue = 11; title = 'Second'; status = 'blocked' },
                [ordered]@{ key = 'RELEASE-COMPLETE'; issue = $null; title = 'Complete'; status = 'active' }
            )
        }
        $invalidPath = Join-Path $root 'invalid.json'
        $invalidHead | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $invalidPath -Encoding UTF8

        $failedAsExpected = $false
        try {
            Invoke-SerialValidation `
                -HeadPath $invalidPath `
                -BasePath $basePath `
                -Body "Closes #10`n`nSequence: REL-000`nPrevious: none`nNext: REL-001" `
                -Title '[REL-000] Test invalid transition' `
                -Branch 'issue/10-rel-000-test-transition'
        }
        catch {
            $failedAsExpected = $true
        }
        Assert-Condition $failedAsExpected 'Self-test expected the skipped transition to fail.'

        Write-Host 'Serial roadmap validator self-test passed.'
    }
    finally {
        Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction SilentlyContinue
    }
}

if ($SelfTest) {
    Invoke-SelfTest
    exit 0
}

Assert-Condition (-not [string]::IsNullOrWhiteSpace($HeadRoadmapPath)) 'HeadRoadmapPath is required.'
Assert-Condition (-not [string]::IsNullOrWhiteSpace($PullRequestBody)) 'PullRequestBody is required.'
Assert-Condition (-not [string]::IsNullOrWhiteSpace($PullRequestTitle)) 'PullRequestTitle is required.'
Assert-Condition (-not [string]::IsNullOrWhiteSpace($BranchName)) 'BranchName is required.'

Invoke-SerialValidation `
    -HeadPath $HeadRoadmapPath `
    -BasePath $BaseRoadmapPath `
    -Body $PullRequestBody `
    -Title $PullRequestTitle `
    -Branch $BranchName
