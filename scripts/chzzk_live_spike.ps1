# CHZZK 전체 라이브 수집 경로를 측정하고 익명화된 fixture를 생성한다.
[CmdletBinding()]
param(
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '..\.codex-work\chzzk-live-spike'),
    [uri]$BaseUri = 'https://openapi.chzzk.naver.com/open/v1/lives',
    [ValidateRange(1, 300)]
    [int]$TimeoutSeconds = 30
)

$ErrorActionPreference = 'Stop'

$clientId = [Environment]::GetEnvironmentVariable('RAIDER_CHZZK_CLIENT_ID')
$clientSecret = [Environment]::GetEnvironmentVariable('RAIDER_CHZZK_CLIENT_SECRET')

if ([string]::IsNullOrWhiteSpace($clientId) -or [string]::IsNullOrWhiteSpace($clientSecret)) {
    throw 'RAIDER_CHZZK_CLIENT_ID and RAIDER_CHZZK_CLIENT_SECRET must be set.'
}

$headers = @{
    'Client-Id' = $clientId
    'Client-Secret' = $clientSecret
    'Content-Type' = 'application/json'
}

$pages = [System.Collections.Generic.List[object]]::new()
$liveIds = [System.Collections.Generic.List[string]]::new()
$streamsWithTags = 0
$streamsWithCategory = 0
$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
$next = $null

do {
    $uri = "$($BaseUri.AbsoluteUri)?size=20"
    if (-not [string]::IsNullOrEmpty($next)) {
        $uri += "&next=$([uri]::EscapeDataString($next))"
    }

    $response = Invoke-RestMethod -Uri $uri -Method Get -Headers $headers -TimeoutSec $TimeoutSeconds
    if ($response.code -ne 200 -or $null -eq $response.content -or $null -eq $response.content.data -or $null -eq $response.content.page) {
        throw "Unexpected CHZZK response contract from $($BaseUri.Host)."
    }

    $pages.Add($response)

    foreach ($live in $response.content.data) {
        $liveIds.Add([string]$live.liveId)
        if (@($live.tags).Count -gt 0) {
            $streamsWithTags++
        }
        if (-not [string]::IsNullOrWhiteSpace([string]$live.liveCategoryValue)) {
            $streamsWithCategory++
        }
    }

    $next = $response.content.page.next
} while (-not [string]::IsNullOrEmpty($next))

$stopwatch.Stop()
$duplicateCount = $liveIds.Count - ($liveIds | Sort-Object -Unique).Count

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null

$measurement = [ordered]@{
    measuredAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
    endpoint = $BaseUri.AbsoluteUri
    pageSize = 20
    pageCount = $pages.Count
    streamCount = $liveIds.Count
    duplicateBroadcastCount = $duplicateCount
    streamsWithTags = $streamsWithTags
    streamsWithCategory = $streamsWithCategory
    elapsedMilliseconds = $stopwatch.ElapsedMilliseconds
}

$measurement |
    ConvertTo-Json -Depth 10 |
    Set-Content -Encoding utf8 (Join-Path $OutputDirectory 'measurement.json')

$fixtureIndex = 0
$sanitizedData = foreach ($page in $pages) {
    foreach ($live in $page.content.data) {
        $fixtureIndex++
        [ordered]@{
            liveId = 1000 + $fixtureIndex
            liveTitle = "Fixture live $fixtureIndex"
            liveThumbnailImageUrl = "https://example.invalid/chzzk/thumbnail-$fixtureIndex.jpg"
            concurrentUserCount = [int]$live.concurrentUserCount
            tags = @(
                for ($tagIndex = 0; $tagIndex -lt @($live.tags).Count; $tagIndex++) {
                    "Fixture tag $($tagIndex + 1)"
                }
            )
            categoryType = [string]$live.categoryType
            liveCategory = if ([string]::IsNullOrWhiteSpace([string]$live.liveCategory)) { $null } else { "fixture-category-$fixtureIndex" }
            liveCategoryValue = if ([string]::IsNullOrWhiteSpace([string]$live.liveCategoryValue)) { $null } else { "Fixture category $fixtureIndex" }
            channelId = "fixture-channel-$fixtureIndex"
            channelName = "Fixture streamer $fixtureIndex"
        }
    }
}

$normalFixture = [ordered]@{
    code = 200
    message = $null
    content = [ordered]@{
        data = @($sanitizedData | Select-Object -First 2)
        page = [ordered]@{ next = $null }
    }
}

$normalFixture |
    ConvertTo-Json -Depth 10 |
    Set-Content -Encoding utf8 (Join-Path $OutputDirectory 'normal.json')

$measurement | Format-List
