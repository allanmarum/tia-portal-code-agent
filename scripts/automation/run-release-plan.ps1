<#
.SYNOPSIS
    Processes the TIA Portal Code Agent release issues autonomously.

.DESCRIPTION
    For each configured GitHub issue, the script:

    1. Updates the main branch.
    2. Creates or resumes an issue branch.
    3. Runs Agy as the implementation agent.
    4. Commits and pushes the implementation.
    5. Creates or reuses a draft pull request.
    6. Runs Claude Code as an autonomous reviewer/fixer.
    7. Runs the local validation command.
    8. Waits for GitHub checks.
    9. Squash-merges the pull request.
   10. Publishes and validates release tags for issues 55, 57, and 58.

   11. Continues with the next issue only after success.

    The process stops immediately when an issue fails.

.NOTES
    Default repository:
        C:\github\tia-portal-code-agent

    Required commands:
        git
        gh
        pwsh
        agy
        claude

    Agy invocation varies between installations. The default mode sends the
    prompt through stdin:

        Get-Content prompt.txt -Raw | agy

    Alternative examples:

        .\run-release-plan.ps1 -AgyMode Argument -AgyPromptArgument "-p"
        .\run-release-plan.ps1 -AgyMode File -AgyPromptArgument "--prompt-file"

.EXAMPLE
    .\run-release-plan.ps1

.EXAMPLE
    .\run-release-plan.ps1 -FromIssue 49 -ToIssue 53

.EXAMPLE
    .\run-release-plan.ps1 -IssueNumbers 44,45,46

.EXAMPLE
    .\run-release-plan.ps1 -AllowNoChecks

.EXAMPLE
    .\run-release-plan.ps1 -SkipMerge
#>

[CmdletBinding()]
param(
    [string]$RepoPath = "C:\github\tia-portal-code-agent",

    [int[]]$IssueNumbers = @(
        44, 45, 46, 47, 48,
        49, 50, 51, 52, 53,
        54, 55, 56, 57, 58
    ),

    [int]$FromIssue = 0,
    [int]$ToIssue = 0,

    [string]$BaseBranch = "main",
    [string]$TestCommand = ".\build.ps1 all",

    [string]$AgyExecutable = "agy",

    [ValidateSet("Stdin", "Argument", "File")]
    [string]$AgyMode = "Stdin",

    [string]$AgyPromptArgument = "-p",

    [string]$ClaudeExecutable = "claude",
    [int]$ClaudeMaxTurns = 40,
    [int]$MaxClaudePasses = 3,

    [int]$ReleaseWorkflowDiscoveryTimeoutSeconds = 600,

    [switch]$AllowNoChecks,
    [switch]$SkipMerge,
    [switch]$SkipReleasePublication
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$ReleaseTags = @{
    55 = "v0.2.0-beta.1"
    57 = "v0.2.0-rc.1"
    58 = "v0.2.0"
}

$AutomationRoot = if (-not [string]::IsNullOrWhiteSpace($env:LOCALAPPDATA)) {
    Join-Path $env:LOCALAPPDATA "TiaPortalCodeAgent\agent-automation"
}
else {
    Join-Path ([System.IO.Path]::GetTempPath()) "TiaPortalCodeAgent\agent-automation"
}

$RepositoryKey = Split-Path -Path $RepoPath -Leaf
$AutomationDirectory = Join-Path $AutomationRoot $RepositoryKey
$LogDirectory = Join-Path $AutomationDirectory "logs"
$PromptDirectory = Join-Path $AutomationDirectory "prompts"

function Write-Step {
    param([Parameter(Mandatory)][string]$Message)

    Write-Host ""
    Write-Host "============================================================"
    Write-Host $Message
    Write-Host "============================================================"
}

function Test-CommandAvailable {
    param([Parameter(Mandatory)][string]$Name)

    return $null -ne (Get-Command $Name -ErrorAction SilentlyContinue)
}

function Invoke-NativeCapture {
    param(
        [Parameter(Mandatory)][string]$FilePath,
        [Parameter(Mandatory)][string[]]$Arguments,
        [string]$FailureMessage = "Command failed."
    )

    $output = & $FilePath @Arguments 2>&1
    $exitCode = $LASTEXITCODE
    $text = ($output | ForEach-Object { "$_" }) -join "`n"

    if ($exitCode -ne 0) {
        throw "$FailureMessage`nCommand: $FilePath $($Arguments -join ' ')`n$text"
    }

    return $text
}

function Invoke-NativeStreaming {
    param(
        [Parameter(Mandatory)][string]$FilePath,
        [Parameter(Mandatory)][string[]]$Arguments,
        [string]$FailureMessage = "Command failed."
    )

    & $FilePath @Arguments
    $exitCode = $LASTEXITCODE

    if ($exitCode -ne 0) {
        throw "$FailureMessage`nCommand: $FilePath $($Arguments -join ' ')"
    }
}

function Ensure-Prerequisites {
    Write-Step "Validating prerequisites"

    if (-not (Test-Path -LiteralPath $RepoPath -PathType Container)) {
        throw "Repository directory not found: $RepoPath"
    }

    Set-Location -LiteralPath $RepoPath

    if (-not (Test-Path -LiteralPath (Join-Path $RepoPath ".git"))) {
        throw "The directory is not a Git repository: $RepoPath"
    }

    foreach ($command in @("git", "gh", "pwsh", $AgyExecutable, $ClaudeExecutable)) {
        if (-not (Test-CommandAvailable -Name $command)) {
            throw "Required command not found in PATH: $command"
        }
    }

    Invoke-NativeStreaming `
        -FilePath "gh" `
        -Arguments @("auth", "status") `
        -FailureMessage "GitHub CLI authentication is not available."

    New-Item -ItemType Directory -Force -Path $AutomationDirectory | Out-Null
    New-Item -ItemType Directory -Force -Path $LogDirectory | Out-Null
    New-Item -ItemType Directory -Force -Path $PromptDirectory | Out-Null

    $status = Invoke-NativeCapture `
        -FilePath "git" `
        -Arguments @("status", "--porcelain") `
        -FailureMessage "Could not inspect the Git working tree."

    if (-not [string]::IsNullOrWhiteSpace($status)) {
        throw "The repository contains uncommitted changes. Commit, stash, or discard them before running the automation."
    }

    Invoke-NativeStreaming `
        -FilePath "git" `
        -Arguments @("fetch", "--prune", "origin") `
        -FailureMessage "Could not fetch the origin remote."
}

function Ensure-AutomationLabels {
    Write-Step "Ensuring automation labels exist"

    $labels = @(
        @{ Name = "agent-running";   Color = "FBCA04"; Description = "Issue currently being handled by the autonomous agent workflow" },
        @{ Name = "agent-failed";    Color = "D73A4A"; Description = "Autonomous agent workflow failed" },
        @{ Name = "agent-completed"; Color = "0E8A16"; Description = "Autonomous agent workflow completed successfully" }
    )

    foreach ($label in $labels) {
        & gh label create $label.Name `
            --color $label.Color `
            --description $label.Description `
            --force 2>&1 | Out-Null

        if ($LASTEXITCODE -ne 0) {
            throw "Could not create or update GitHub label '$($label.Name)'."
        }
    }
}

function Update-IssueLabels {
    param(
        [Parameter(Mandatory)][int]$IssueNumber,
        [string[]]$Add = @(),
        [string[]]$Remove = @()
    )

    $arguments = @("issue", "edit", "$IssueNumber")

    foreach ($label in $Add) {
        $arguments += @("--add-label", $label)
    }

    foreach ($label in $Remove) {
        $arguments += @("--remove-label", $label)
    }

    Invoke-NativeStreaming `
        -FilePath "gh" `
        -Arguments $arguments `
        -FailureMessage "Could not update labels for issue #$IssueNumber."
}

function Add-IssueComment {
    param(
        [Parameter(Mandatory)][int]$IssueNumber,
        [Parameter(Mandatory)][string]$Body
    )

    & gh issue comment $IssueNumber --body $Body 2>&1 | Out-Null

    if ($LASTEXITCODE -ne 0) {
        Write-Warning "Could not add a comment to issue #$IssueNumber."
    }
}

function Update-MainBranch {
    Write-Step "Updating $BaseBranch"

    Invoke-NativeStreaming `
        -FilePath "git" `
        -Arguments @("switch", $BaseBranch) `
        -FailureMessage "Could not switch to $BaseBranch."

    Invoke-NativeStreaming `
        -FilePath "git" `
        -Arguments @("pull", "--ff-only", "origin", $BaseBranch) `
        -FailureMessage "Could not update $BaseBranch using a fast-forward pull."
}

function Get-Issue {
    param([Parameter(Mandatory)][int]$IssueNumber)

    $json = Invoke-NativeCapture `
        -FilePath "gh" `
        -Arguments @("issue", "view", "$IssueNumber", "--json", "number,title,body,state,url") `
        -FailureMessage "Could not read issue #$IssueNumber."

    return $json | ConvertFrom-Json
}

function Switch-ToIssueBranch {
    param([Parameter(Mandatory)][string]$BranchName)

    Write-Step "Preparing branch $BranchName"

    & git show-ref --verify --quiet "refs/heads/$BranchName"
    $localExists = $LASTEXITCODE -eq 0

    if ($localExists) {
        Invoke-NativeStreaming `
            -FilePath "git" `
            -Arguments @("switch", $BranchName) `
            -FailureMessage "Could not switch to local branch $BranchName."

        return
    }

    & git ls-remote --exit-code --heads origin $BranchName 2>&1 | Out-Null
    $remoteExists = $LASTEXITCODE -eq 0

    if ($remoteExists) {
        Invoke-NativeStreaming `
            -FilePath "git" `
            -Arguments @("switch", "--track", "-c", $BranchName, "origin/$BranchName") `
            -FailureMessage "Could not create a local tracking branch for $BranchName."

        return
    }

    Invoke-NativeStreaming `
        -FilePath "git" `
        -Arguments @("switch", "-c", $BranchName) `
        -FailureMessage "Could not create branch $BranchName."
}

function Write-PromptFile {
    param(
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][string]$Content
    )

    $path = Join-Path $PromptDirectory $Name
    [System.IO.File]::WriteAllText($path, $Content, [System.Text.UTF8Encoding]::new($false))
    return $path
}

function Invoke-AgyAgent {
    param(
        [Parameter(Mandatory)][int]$IssueNumber,
        [Parameter(Mandatory)][string]$Prompt
    )

    Write-Step "Running Agy for issue #$IssueNumber"

    $promptPath = Write-PromptFile `
        -Name "issue-$IssueNumber-agy.txt" `
        -Content $Prompt

    $logPath = Join-Path $LogDirectory "issue-$IssueNumber-agy.log"

    switch ($AgyMode) {
        "Stdin" {
            $output = Get-Content -LiteralPath $promptPath -Raw |
                & $AgyExecutable 2>&1
        }

        "Argument" {
            $output = & $AgyExecutable $AgyPromptArgument $Prompt 2>&1
        }

        "File" {
            $output = & $AgyExecutable $AgyPromptArgument $promptPath 2>&1
        }
    }

    $exitCode = $LASTEXITCODE
    $output | Tee-Object -FilePath $logPath | Out-Null

    if ($exitCode -ne 0) {
        throw "Agy failed for issue #$IssueNumber. See: $logPath"
    }
}

function Invoke-ClaudeAgent {
    param(
        [Parameter(Mandatory)][int]$IssueNumber,
        [Parameter(Mandatory)][int]$Pass,
        [Parameter(Mandatory)][string]$Prompt
    )

    Write-Step "Running Claude Code review for issue #$IssueNumber - pass $Pass"

    $promptPath = Write-PromptFile `
        -Name "issue-$IssueNumber-claude-pass-$Pass.txt" `
        -Content $Prompt

    $logPath = Join-Path $LogDirectory "issue-$IssueNumber-claude-pass-$Pass.log"

    $arguments = @(
        "-p", $Prompt,
        "--max-turns", "$ClaudeMaxTurns",
        "--dangerously-skip-permissions"
    )

    $output = & $ClaudeExecutable @arguments 2>&1
    $exitCode = $LASTEXITCODE
    $output | Tee-Object -FilePath $logPath | Out-Null

    if ($exitCode -ne 0) {
        throw "Claude Code failed for issue #$IssueNumber on pass $Pass. See: $logPath"
    }
}

function Test-Project {
    param(
        [Parameter(Mandatory)][int]$IssueNumber,
        [Parameter(Mandatory)][string]$Stage
    )

    Write-Step "Running project validation for issue #$IssueNumber - $Stage"

    $safeStage = $Stage -replace "[^a-zA-Z0-9_-]", "-"
    $logPath = Join-Path $LogDirectory "issue-$IssueNumber-tests-$safeStage.log"

    $output = & pwsh -NoProfile -Command $TestCommand 2>&1
    $exitCode = $LASTEXITCODE
    $output | Tee-Object -FilePath $logPath | Out-Null

    return @{
        Passed = ($exitCode -eq 0)
        LogPath = $logPath
    }
}

function Commit-AndPushChanges {
    param(
        [Parameter(Mandatory)][string]$BranchName,
        [Parameter(Mandatory)][string]$CommitMessage
    )

    $status = Invoke-NativeCapture `
        -FilePath "git" `
        -Arguments @("status", "--porcelain") `
        -FailureMessage "Could not inspect pending changes."

    if ([string]::IsNullOrWhiteSpace($status)) {
        Write-Host "No new changes to commit."
    }
    else {
        Invoke-NativeStreaming `
            -FilePath "git" `
            -Arguments @("add", "--all") `
            -FailureMessage "Could not stage changes."

        Invoke-NativeStreaming `
            -FilePath "git" `
            -Arguments @("commit", "-m", $CommitMessage) `
            -FailureMessage "Could not create commit."
    }

    Invoke-NativeStreaming `
        -FilePath "git" `
        -Arguments @("push", "--set-upstream", "origin", $BranchName) `
        -FailureMessage "Could not push branch $BranchName."
}

function Get-OpenPullRequest {
    param([Parameter(Mandatory)][string]$BranchName)

    $json = Invoke-NativeCapture `
        -FilePath "gh" `
        -Arguments @("pr", "list", "--head", $BranchName, "--state", "open", "--json", "number,url,isDraft") `
        -FailureMessage "Could not search for an existing pull request."

    $pullRequests = @($json | ConvertFrom-Json)

    if ($pullRequests.Count -eq 0) {
        return $null
    }

    return $pullRequests[0]
}

function Ensure-PullRequest {
    param(
        [Parameter(Mandatory)][object]$Issue,
        [Parameter(Mandatory)][string]$BranchName
    )

    $existing = Get-OpenPullRequest -BranchName $BranchName

    if ($null -ne $existing) {
        Write-Host "Using existing PR #$($existing.number): $($existing.url)"
        return $existing
    }

    Write-Step "Creating pull request for issue #$($Issue.number)"

    $isPublicationIssue = $ReleaseTags.ContainsKey([int]$Issue.number)
    $referenceLine = if ($isPublicationIssue) {
        "Refs #$($Issue.number)"
    }
    else {
        "Closes #$($Issue.number)"
    }

    $body = @"
Automated implementation and autonomous review for issue #$($Issue.number).

$referenceLine
"@

    $url = Invoke-NativeCapture `
        -FilePath "gh" `
        -Arguments @(
            "pr", "create",
            "--base", $BaseBranch,
            "--head", $BranchName,
            "--title", $Issue.title,
            "--body", $body,
            "--draft"
        ) `
        -FailureMessage "Could not create the pull request for issue #$($Issue.number)."

    $created = Get-OpenPullRequest -BranchName $BranchName

    if ($null -eq $created) {
        throw "The pull request was created but could not be retrieved."
    }

    Write-Host "Created PR #$($created.number): $url"
    return $created
}

function Set-PullRequestReady {
    param([Parameter(Mandatory)][int]$PullRequestNumber)

    $json = Invoke-NativeCapture `
        -FilePath "gh" `
        -Arguments @("pr", "view", "$PullRequestNumber", "--json", "isDraft") `
        -FailureMessage "Could not inspect PR #$PullRequestNumber."

    $pullRequest = $json | ConvertFrom-Json

    if ($pullRequest.isDraft) {
        Invoke-NativeStreaming `
            -FilePath "gh" `
            -Arguments @("pr", "ready", "$PullRequestNumber") `
            -FailureMessage "Could not mark PR #$PullRequestNumber as ready."
    }
}

function Wait-PullRequestChecks {
    param(
        [Parameter(Mandatory)][int]$IssueNumber,
        [Parameter(Mandatory)][int]$PullRequestNumber,
        [Parameter(Mandatory)][int]$Pass
    )

    Write-Step "Waiting for GitHub checks on PR #$PullRequestNumber"

    $logPath = Join-Path $LogDirectory "issue-$IssueNumber-ci-pass-$Pass.log"

    & gh pr checks $PullRequestNumber --watch 2>&1 |
        Tee-Object -FilePath $logPath

    $exitCode = $LASTEXITCODE
    $content = if (Test-Path -LiteralPath $logPath) {
        Get-Content -LiteralPath $logPath -Raw
    }
    else {
        ""
    }

    if ($exitCode -eq 0) {
        return @{
            Passed = $true
            NoChecks = $false
            LogPath = $logPath
            Output = $content
        }
    }

    $noChecks = $content -match "(?i)no checks reported|no checks"

    if ($noChecks -and $AllowNoChecks) {
        Write-Warning "No checks were reported for PR #$PullRequestNumber, but -AllowNoChecks was supplied."

        return @{
            Passed = $true
            NoChecks = $true
            LogPath = $logPath
            Output = $content
        }
    }

    return @{
        Passed = $false
        NoChecks = $noChecks
        LogPath = $logPath
        Output = $content
    }
}

function Merge-PullRequest {
    param([Parameter(Mandatory)][int]$PullRequestNumber)

    if ($SkipMerge) {
        Write-Warning "Skipping merge because -SkipMerge was supplied."
        return
    }

    Write-Step "Squash-merging PR #$PullRequestNumber"

    Invoke-NativeStreaming `
        -FilePath "gh" `
        -Arguments @("pr", "merge", "$PullRequestNumber", "--squash", "--delete-branch") `
        -FailureMessage "Could not merge PR #$PullRequestNumber."
}

function Publish-AndValidateRelease {
    param(
        [Parameter(Mandatory)][int]$IssueNumber,
        [Parameter(Mandatory)][string]$Tag
    )

    if ($SkipReleasePublication) {
        Write-Warning "Skipping release publication for $Tag because -SkipReleasePublication was supplied."
        return
    }

    if ($SkipMerge) {
        Write-Warning "Skipping release publication for $Tag because the PR was not merged."
        return
    }

    Write-Step "Publishing release tag $Tag"

    Update-MainBranch

    & git ls-remote --exit-code --tags origin "refs/tags/$Tag" 2>&1 | Out-Null
    $remoteTagExists = $LASTEXITCODE -eq 0

    if (-not $remoteTagExists) {
        Invoke-NativeStreaming `
            -FilePath "git" `
            -Arguments @("tag", "-a", $Tag, "-m", "Release $Tag") `
            -FailureMessage "Could not create tag $Tag."

        Invoke-NativeStreaming `
            -FilePath "git" `
            -Arguments @("push", "origin", $Tag) `
            -FailureMessage "Could not push tag $Tag."
    }
    else {
        Write-Host "Tag $Tag already exists on origin."
    }

    Write-Step "Waiting for a release workflow triggered by $Tag"

    $deadline = (Get-Date).AddSeconds($ReleaseWorkflowDiscoveryTimeoutSeconds)
    $run = $null

    while ((Get-Date) -lt $deadline -and $null -eq $run) {
        $json = & gh run list `
            --branch $Tag `
            --limit 10 `
            --json databaseId,status,conclusion,workflowName,headBranch,createdAt 2>&1

        if ($LASTEXITCODE -eq 0) {
            $runs = @((($json | ForEach-Object { "$_" }) -join "`n") | ConvertFrom-Json)

            if ($runs.Count -gt 0) {
                $run = $runs |
                    Sort-Object { [datetime]$_.createdAt } -Descending |
                    Select-Object -First 1
            }
        }

        if ($null -eq $run) {
            Start-Sleep -Seconds 10
        }
    }

    if ($null -eq $run) {
        throw "No GitHub Actions run was found for tag $Tag."
    }

    Write-Host "Watching workflow '$($run.workflowName)' (run $($run.databaseId))."

    Invoke-NativeStreaming `
        -FilePath "gh" `
        -Arguments @("run", "watch", "$($run.databaseId)", "--exit-status") `
        -FailureMessage "The release workflow failed for tag $Tag."

    Write-Step "Validating GitHub Release $Tag"

    Invoke-NativeStreaming `
        -FilePath "gh" `
        -Arguments @("release", "view", $Tag) `
        -FailureMessage "GitHub Release $Tag was not found after the workflow completed."

    & gh issue close $IssueNumber `
        --comment "Release $Tag was published and validated automatically." 2>&1 | Out-Null

    if ($LASTEXITCODE -ne 0) {
        throw "Release $Tag succeeded, but issue #$IssueNumber could not be closed."
    }
}

function Process-Issue {
    param([Parameter(Mandatory)][int]$IssueNumber)

    $issue = Get-Issue -IssueNumber $IssueNumber

    if ($issue.state -eq "CLOSED") {
        Write-Host "Issue #$IssueNumber is already closed. Skipping."
        return
    }

    $branchName = "agent/issue-$IssueNumber"

    Update-IssueLabels `
        -IssueNumber $IssueNumber `
        -Add @("agent-running") `
        -Remove @("agent-failed", "agent-completed")

    try {
        Update-MainBranch
        Switch-ToIssueBranch -BranchName $branchName

        $agyPrompt = @"
You are the primary implementation agent working autonomously in the current repository.

Implement GitHub issue #$IssueNumber completely.

Issue title:
$($issue.title)

Issue description:
$($issue.body)

Repository:
$RepoPath

Base branch:
$BaseBranch

Instructions:
- Inspect the repository architecture and relevant documentation before editing.
- Review previously completed REL issues and preserve their established conventions.
- Implement every acceptance criterion in the issue.
- Prefer a complete, maintainable implementation over temporary workarounds.
- Add or update automated tests.
- Update user, maintainer, installation, release, and architecture documentation when affected.
- Run the relevant validation commands, including:
  $TestCommand
- Fix failures caused by your changes.
- Do not commit, push, create or update pull requests, merge, create tags, or publish releases.
- Leave the working tree containing the completed implementation.
"@

        Invoke-AgyAgent `
            -IssueNumber $IssueNumber `
            -Prompt $agyPrompt

        $agyValidation = Test-Project `
            -IssueNumber $IssueNumber `
            -Stage "after-agy"

        if (-not $agyValidation.Passed) {
            Write-Warning "Validation failed after Agy. Claude Code will receive the failure log and attempt to fix it."
        }

        $pendingChanges = Invoke-NativeCapture `
            -FilePath "git" `
            -Arguments @("status", "--porcelain") `
            -FailureMessage "Could not inspect the implementation produced by Agy."

        $commitsAheadText = Invoke-NativeCapture `
            -FilePath "git" `
            -Arguments @("rev-list", "--count", "$BaseBranch..HEAD") `
            -FailureMessage "Could not determine whether the issue branch contains commits."

        $commitsAhead = [int]$commitsAheadText.Trim()

        if ([string]::IsNullOrWhiteSpace($pendingChanges) -and $commitsAhead -eq 0) {
            throw "Agy produced no repository changes for issue #$IssueNumber."
        }

        Commit-AndPushChanges `
            -BranchName $branchName `
            -CommitMessage "feat: implement issue #$IssueNumber"

        $pullRequest = Ensure-PullRequest `
            -Issue $issue `
            -BranchName $branchName

        $lastFailureContext = if ($agyValidation.Passed) {
            "The validation command passed after the initial implementation."
        }
        else {
            "The validation command failed after the initial implementation. Read and address this log: $($agyValidation.LogPath)"
        }

        $completed = $false

        for ($pass = 1; $pass -le $MaxClaudePasses; $pass++) {
            $claudePrompt = @"
Act as the final autonomous maintainer and reviewer for the current branch.

The branch implements GitHub issue #${IssueNumber}:

$($issue.title)

Issue description:
$($issue.body)

Pull request:
#$($pullRequest.number)

Previous validation context:
$lastFailureContext

Your responsibilities:
- Read the issue and inspect the complete diff against origin/$BaseBranch.
- Verify that every acceptance criterion is implemented.
- Find and directly fix correctness, architecture, lifecycle, concurrency, security, packaging, installation, update, rollback, and release problems.
- Check compatibility with all previously completed REL issues.
- Remove temporary, duplicated, obsolete, legacy, or workaround code.
- Add missing tests and improve weak tests.
- Update documentation affected by the implementation.
- Run the complete validation command:
  $TestCommand
- Fix every build, test, formatting, and static-analysis failure.
- Do not only write a review report: edit the repository and resolve the problems.
- Do not commit, push, create another pull request, merge, create tags, or publish releases.
- Leave the working tree ready to be committed.
"@

            Invoke-ClaudeAgent `
                -IssueNumber $IssueNumber `
                -Pass $pass `
                -Prompt $claudePrompt

            $validation = Test-Project `
                -IssueNumber $IssueNumber `
                -Stage "claude-pass-$pass"

            if (-not $validation.Passed) {
                Commit-AndPushChanges `
                    -BranchName $branchName `
                    -CommitMessage "fix: automated review pass $pass for issue #$IssueNumber"

                $lastFailureContext = "Local validation failed on Claude pass $pass. Read and fix this log: $($validation.LogPath)"
                continue
            }

            Commit-AndPushChanges `
                -BranchName $branchName `
                -CommitMessage "fix: automated review pass $pass for issue #$IssueNumber"

            Set-PullRequestReady `
                -PullRequestNumber ([int]$pullRequest.number)

            $checks = Wait-PullRequestChecks `
                -IssueNumber $IssueNumber `
                -PullRequestNumber ([int]$pullRequest.number) `
                -Pass $pass

            if ($checks.Passed) {
                $completed = $true
                break
            }

            if ($checks.NoChecks) {
                $lastFailureContext = "No GitHub checks were reported. Configure required CI checks or rerun with -AllowNoChecks. Log: $($checks.LogPath)"
            }
            else {
                $lastFailureContext = "GitHub checks failed on Claude pass $pass. Read and fix this log: $($checks.LogPath)"
            }
        }

        if (-not $completed) {
            throw "Issue #$IssueNumber did not pass validation after $MaxClaudePasses Claude Code passes."
        }

        Merge-PullRequest `
            -PullRequestNumber ([int]$pullRequest.number)

        if ($ReleaseTags.ContainsKey($IssueNumber)) {
            Publish-AndValidateRelease `
                -IssueNumber $IssueNumber `
                -Tag $ReleaseTags[$IssueNumber]
        }

        Update-IssueLabels `
            -IssueNumber $IssueNumber `
            -Add @("agent-completed") `
            -Remove @("agent-running", "agent-failed")

        Write-Step "Issue #$IssueNumber completed successfully"
    }
    catch {
        $message = $_.Exception.Message

        try {
            Update-IssueLabels `
                -IssueNumber $IssueNumber `
                -Add @("agent-failed") `
                -Remove @("agent-running", "agent-completed")
        }
        catch {
            Write-Warning "Could not update failure labels for issue #$IssueNumber."
        }

        Add-IssueComment `
            -IssueNumber $IssueNumber `
            -Body "Autonomous processing failed and the release plan was stopped.`n`n``````text`n$message`n``````"

        throw
    }
    finally {
        try {
            Set-Location -LiteralPath $RepoPath
        }
        catch {
            # Keep the original exception.
        }
    }
}

try {
    Ensure-Prerequisites
    Ensure-AutomationLabels

    $queue = @($IssueNumbers | Sort-Object -Unique)

    if ($FromIssue -gt 0) {
        $queue = @($queue | Where-Object { $_ -ge $FromIssue })
    }

    if ($ToIssue -gt 0) {
        $queue = @($queue | Where-Object { $_ -le $ToIssue })
    }

    if ($queue.Count -eq 0) {
        throw "The selected issue queue is empty."
    }

    Write-Step "Starting autonomous release plan"
    Write-Host "Repository: $RepoPath"
    Write-Host "Issues:     $($queue -join ', ')"
    Write-Host "Test:       $TestCommand"
    Write-Host "Agy mode:   $AgyMode"
    Write-Host "Merge:      $(-not $SkipMerge)"

    foreach ($issueNumber in $queue) {
        Process-Issue -IssueNumber $issueNumber
    }

    Update-MainBranch
    Write-Step "Release plan completed successfully"
}
catch {
    Write-Error $_
    exit 1
}
