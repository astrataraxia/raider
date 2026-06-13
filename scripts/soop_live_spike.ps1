# SOOP 공개 웹 경로의 전체 라이브 목록을 측정하고 익명화된 fixture를 생성한다.
[CmdletBinding()]
param(
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '..\.codex-work\soop-live-spike'),
    [uri]$BaseUri = 'https://live.sooplive.com/api/main_broad_list_api.php',
    [ValidateSet('broad_start', 'view_cnt')]
    [string]$OrderType = 'broad_start',
    [ValidateRange(1, 300)]
    [int]$TimeoutSeconds = 30,
    [ValidateRange(1, 100)]
    [int]$MaximumPages = 100
)

$ErrorActionPreference = 'Stop'

$headers = @{
    'Accept' = 'application/json'
    'User-Agent' = 'Raider-feasibility-spike/0.1'
}

$pages = [System.Collections.Generic.List[object]]::new()
$broadcastIds = [System.Collections.Generic.List[string]]::new()
$pageDurations = [System.Collections.Generic.List[long]]::new()
$missingRequiredFieldCount = 0
$streamsWithTags = 0
$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
$pageNumber = 1
$expectedPageCount = $null
$pageSize = 60

do {
    $uri = "$($BaseUri.AbsoluteUri)?selectType=action&selectValue=all&orderType=$OrderType&pageNo=$pageNumber&lang=ko_KR"
    $pageStopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    $response = Invoke-RestMethod -Uri $uri -Method Get -Headers $headers -TimeoutSec $TimeoutSeconds
    $pageStopwatch.Stop()
    $pageDurations.Add($pageStopwatch.ElapsedMilliseconds)

    if ($null -eq $response.total_cnt -or $null -eq $response.cnt -or $null -eq $response.broad) {
        throw "Unexpected SOOP response contract from $($BaseUri.Host)."
    }

    $pages.Add($response)
    if ($null -eq $expectedPageCount) {
        $expectedPageCount = [Math]::Ceiling([int]$response.total_cnt / $pageSize)
        if ($expectedPageCount -gt $MaximumPages) {
            throw "SOOP response requires $expectedPageCount pages, exceeding MaximumPages $MaximumPages."
        }
    }

    foreach ($broadcast in $response.broad) {
        $broadcastIds.Add([string]$broadcast.broad_no)
        if (@($broadcast.auto_hashtags).Count + @($broadcast.category_tags).Count + @($broadcast.hash_tags).Count + @($broadcast.lang_tags).Count -gt 0 -or
            -not [string]::IsNullOrWhiteSpace([string]$broadcast.category_name)) {
            $streamsWithTags++
        }
        foreach ($field in 'broad_no', 'user_id', 'user_nick', 'broad_title', 'current_view_cnt') {
            if ([string]::IsNullOrWhiteSpace([string]$broadcast.$field)) {
                $missingRequiredFieldCount++
                break
            }
        }
    }

    $pageNumber++
} while ($pageNumber -le $expectedPageCount)

$stopwatch.Stop()
$uniqueBroadcastCount = ($broadcastIds | Sort-Object -Unique).Count
$duplicateCount = $broadcastIds.Count - $uniqueBroadcastCount
$reportedUniqueGap = [int]$pages[0].total_cnt - $uniqueBroadcastCount

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null

$measurement = [ordered]@{
    measuredAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
    endpoint = $BaseUri.AbsoluteUri
    orderType = $OrderType
    pageSize = $pageSize
    pageCount = $pages.Count
    reportedStreamCount = [int]$pages[0].total_cnt
    pageEntryCount = $broadcastIds.Count
    uniqueStreamCount = $uniqueBroadcastCount
    duplicateBroadcastCount = $duplicateCount
    reportedUniqueGap = $reportedUniqueGap
    missingRequiredFieldCount = $missingRequiredFieldCount
    streamsWithTags = $streamsWithTags
    elapsedMilliseconds = $stopwatch.ElapsedMilliseconds
    minimumPageMilliseconds = ($pageDurations | Measure-Object -Minimum).Minimum
    maximumPageMilliseconds = ($pageDurations | Measure-Object -Maximum).Maximum
    sendsRequestCookies = $false
}

$measurement |
    ConvertTo-Json -Depth 10 |
    Set-Content -Encoding utf8 (Join-Path $OutputDirectory 'measurement.json')

$fixtureIndex = 0
$sanitizedData = foreach ($page in $pages) {
    foreach ($broadcast in $page.broad) {
        $fixtureIndex++
        [ordered]@{
            broad_no = 2000 + $fixtureIndex
            user_id = "fixture-user-$fixtureIndex"
            user_nick = "Fixture streamer $fixtureIndex"
            broad_title = "Fixture live $fixtureIndex"
            broad_thumb = "//example.invalid/soop/thumbnail-$fixtureIndex.jpg"
            current_view_cnt = [int]$broadcast.current_view_cnt
            total_view_cnt = [int]$broadcast.total_view_cnt
            auto_hashtags = @(
                for ($tagIndex = 0; $tagIndex -lt @($broadcast.auto_hashtags).Count; $tagIndex++) {
                    "Fixture auto tag $($tagIndex + 1)"
                }
            )
            category_tags = @(
                for ($tagIndex = 0; $tagIndex -lt @($broadcast.category_tags).Count; $tagIndex++) {
                    "Fixture category tag $($tagIndex + 1)"
                }
            )
            hash_tags = @(
                for ($tagIndex = 0; $tagIndex -lt @($broadcast.hash_tags).Count; $tagIndex++) {
                    "Fixture hash tag $($tagIndex + 1)"
                }
            )
            lang_tags = @(
                for ($tagIndex = 0; $tagIndex -lt @($broadcast.lang_tags).Count; $tagIndex++) {
                    "Fixture language tag $($tagIndex + 1)"
                }
            )
            category_name = if ([string]::IsNullOrWhiteSpace([string]$broadcast.category_name)) { $null } else { "Fixture category $fixtureIndex" }
            broad_grade = [string]$broadcast.broad_grade
            is_password = [string]$broadcast.is_password
        }
    }
}

$normalFixture = [ordered]@{
    total_cnt = 2
    cnt = 2
    broad = @($sanitizedData | Select-Object -First 2)
    time = 0
    is_wp = 0
}

$normalFixture |
    ConvertTo-Json -Depth 10 |
    Set-Content -Encoding utf8 (Join-Path $OutputDirectory 'normal.json')

$measurement | Format-List
