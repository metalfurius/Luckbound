[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('EditMode', 'PlayMode')]
    [string]$Mode,
    [string]$Filter = 'Luckbound',
    [string]$EvidenceName = '',
    [string]$EditorPath = '',
    [ValidateRange(60, 7200)]
    [int]$TimeoutSeconds = 1200
)

$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$projectVersion = [IO.File]::ReadAllText((Join-Path $projectRoot 'ProjectSettings/ProjectVersion.txt'))
$editorVersion = [regex]::Match($projectVersion, 'm_EditorVersion: ([^\r\n]+)').Groups[1].Value.Trim()
if (-not $editorVersion) { throw 'The pinned Unity version could not be read.' }
if (Get-Process -Name Unity -ErrorAction SilentlyContinue) {
    throw 'Another Unity process is running. Keep one Unity execution per machine.'
}
if (-not $EditorPath) {
    $editorCandidates = @(
        (Join-Path $env:LOCALAPPDATA "Unity/Hub/Editor/$editorVersion/Editor/Unity.exe"),
        (Join-Path $env:ProgramFiles "Unity/Hub/Editor/$editorVersion/Editor/Unity.exe")
    )
    $EditorPath = $editorCandidates | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1
}
if (-not $EditorPath -or -not (Test-Path -LiteralPath $EditorPath -PathType Leaf)) {
    throw 'The pinned Unity executable is unavailable. Do not open Hub or a licensing dialog.'
}
if (-not $EvidenceName) { $EvidenceName = "coordinator-$(Get-Date -Format 'yyyyMMdd-HHmmss')-$($Mode.ToLowerInvariant())" }
if ($EvidenceName -notmatch '^[A-Za-z0-9_.-]+$') { throw 'EvidenceName must be a filename, without directories.' }
$logDirectory = Join-Path $projectRoot 'Logs'
[IO.Directory]::CreateDirectory($logDirectory) | Out-Null
$resultPath = Join-Path $logDirectory "$EvidenceName.xml"
$logPath = Join-Path $logDirectory "$EvidenceName.log"
$auditPath = Join-Path $logDirectory "$EvidenceName.execution.json"
foreach ($evidencePath in @($resultPath, $logPath, $auditPath)) {
    if (Test-Path -LiteralPath $evidencePath) { throw "Evidence already exists: $evidencePath" }
}

$sourceCommit = (& git -C $projectRoot rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0 -or $sourceCommit -notmatch '^[0-9a-f]{40}$') { throw 'Could not resolve the source commit.' }
$sourceStatus = @(& git -C $projectRoot status --porcelain --untracked-files=normal)
if ($LASTEXITCODE -ne 0) { throw 'Could not inspect source changes.' }
$startInfo = [Diagnostics.ProcessStartInfo]::new()
$startInfo.FileName = $EditorPath
$startInfo.WorkingDirectory = $projectRoot
$startInfo.UseShellExecute = $false
$startInfo.CreateNoWindow = $true
foreach ($argument in @('-batchmode', '-nographics', '-projectPath', $projectRoot,
    '-runTests', '-testPlatform', $Mode, '-testResults', $resultPath, '-logFile', $logPath)) {
    $startInfo.ArgumentList.Add($argument)
}
if ($Filter) { $startInfo.ArgumentList.Add('-testFilter'); $startInfo.ArgumentList.Add($Filter) }
$startInfo.Environment['LUCKBOUND_SOURCE_COMMIT'] = $sourceCommit
$startInfo.Environment['LUCKBOUND_SOURCE_STATE'] = $(if ($sourceStatus.Count) { 'modified' } else { 'clean' })
$startInfo.Environment['LUCKBOUND_EVIDENCE_DIR'] = Join-Path $logDirectory 'Scenarios'
$startInfo.Environment['GIT_TERMINAL_PROMPT'] = '0'
$startedUtc = [DateTime]::UtcNow
$process = [Diagnostics.Process]::new()
$process.StartInfo = $startInfo
$process.Start() | Out-Null
$timedOut = $false
while (-not $process.WaitForExit(1000)) {
    if (([DateTime]::UtcNow - $startedUtc).TotalSeconds -gt $TimeoutSeconds) {
        $timedOut = $true
        $process.Kill($true)
        $process.WaitForExit()
        break
    }
}
$audit = [ordered]@{
    startedUtc = $startedUtc.ToString('O')
    completedUtc = [DateTime]::UtcNow.ToString('O')
    unityVersion = $editorVersion
    executable = $EditorPath
    arguments = @($startInfo.ArgumentList)
    sourceCommit = $sourceCommit
    sourceStatus = $sourceStatus
    exitCode = $process.ExitCode
    timedOut = $timedOut
    visibleInterfaceRequested = $false
    createNoWindow = $true
    resultPath = $resultPath
    logPath = $logPath
}
[IO.File]::WriteAllText($auditPath, ($audit | ConvertTo-Json -Depth 5), [Text.UTF8Encoding]::new($false))
if ($timedOut) { throw "Unity exceeded $TimeoutSeconds seconds. See $auditPath" }
if ($process.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $resultPath)) {
    throw "Unity tests did not finish successfully (exit $($process.ExitCode)). See $logPath"
}
[xml]$results = [IO.File]::ReadAllText($resultPath)
$testRun = $results.'test-run'
if ($testRun.result -ne 'Passed' -or [int]$testRun.failed -gt 0 -or [int]$testRun.total -eq 0) {
    throw "Test acceptance failed: $($testRun.result), $($testRun.failed) failures, $($testRun.total) tests."
}

# A cached scenario snapshot is valid only if the frozen sources still match when the run finishes.
$scenarioDirectory = Join-Path $logDirectory 'Scenarios'
$manifests = @(if (Test-Path -LiteralPath $scenarioDirectory) {
    Get-ChildItem -LiteralPath $scenarioDirectory -Filter source-manifest.sha256 -Recurse -File |
        Where-Object { $_.LastWriteTimeUtc -ge $startedUtc }
})
$audit['sourceSnapshotVerified'] = 'not applicable: no scenario manifests'
$verifiedManifests = @{}
$sourceVerificationWatch = [Diagnostics.Stopwatch]::StartNew()
$currentSourcePaths = @(foreach ($sourceDirectory in @('Assets/Luckbound', 'Assets/Resources/Luckbound',
        'Assets/Settings', 'Packages', 'ProjectSettings')) {
    $absoluteDirectory = Join-Path $projectRoot $sourceDirectory
    if (Test-Path -LiteralPath $absoluteDirectory) {
        Get-ChildItem -LiteralPath $absoluteDirectory -Recurse -File -Force |
            ForEach-Object { $_.FullName.Substring($projectRoot.Length + 1).Replace('\', '/') }
    }
})
try {
    foreach ($manifest in $manifests) {
        $manifestHash = (Get-FileHash -LiteralPath $manifest.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($verifiedManifests.ContainsKey($manifestHash)) { continue }
        $entries = [IO.File]::ReadAllLines($manifest.FullName)
        $listedSourcePaths = [Collections.Generic.List[string]]::new()
        foreach ($entry in $entries) {
            $separator = $entry.LastIndexOf(':')
            if ($separator -le 0) { throw 'Invalid source manifest entry.' }
            $relativeSourcePath = $entry.Substring(0, $separator)
            $listedSourcePaths.Add($relativeSourcePath)
            $expectedHash = $entry.Substring($separator + 1)
            if ($expectedHash -notmatch '^[0-9a-f]{64}$') { throw 'Invalid source hash in manifest.' }
            $absoluteSourcePath = [IO.Path]::GetFullPath((Join-Path $projectRoot $relativeSourcePath))
            if (-not $absoluteSourcePath.StartsWith($projectRoot + [IO.Path]::DirectorySeparatorChar,
                    [StringComparison]::OrdinalIgnoreCase)) { throw 'Source manifest escapes the project.' }
            $actualHash = (Get-FileHash -LiteralPath $absoluteSourcePath -Algorithm SHA256).Hash.ToLowerInvariant()
            if ($actualHash -ne $expectedHash) { throw "Source changed during tests: $relativeSourcePath" }
        }
        if (Compare-Object -ReferenceObject $listedSourcePaths.ToArray() -DifferenceObject $currentSourcePaths -CaseSensitive) {
            throw 'Source files were added or removed during tests.'
        }
        $verifiedManifests[$manifestHash] = $entries.Length
    }
    if ($manifests.Count) {
        $audit['sourceSnapshotVerified'] = $true
        $audit['verifiedSourceManifests'] = $verifiedManifests
        $audit['scenarioManifestCount'] = $manifests.Count
    }
} catch {
    $audit['sourceSnapshotVerified'] = $false
    $audit['sourceSnapshotError'] = $_.Exception.Message
    throw
} finally {
    $sourceVerificationWatch.Stop()
    $audit['sourceVerificationSeconds'] = [Math]::Round($sourceVerificationWatch.Elapsed.TotalSeconds, 3)
    $audit['sourceVerifiedUtc'] = [DateTime]::UtcNow.ToString('O')
    [IO.File]::WriteAllText($auditPath, ($audit | ConvertTo-Json -Depth 5), [Text.UTF8Encoding]::new($false))
}
[pscustomobject]@{ Mode = $Mode; Result = $testRun.result; Total = $testRun.total; Passed = $testRun.passed;
    Failed = $testRun.failed; Skipped = $testRun.skipped; Results = $resultPath; Audit = $auditPath }
