# CHZZK 수집 스파이크의 페이지 순회, 측정, 익명화를 로컬 서버로 검증한다.
$ErrorActionPreference = 'Stop'

$port = 18765
$prefix = "http://127.0.0.1:$port/"
$outputDirectory = Join-Path $PSScriptRoot '..\.codex-work\chzzk-live-spike-test'

$server = Start-Job -ScriptBlock {
    param($Prefix)

    $listener = [System.Net.HttpListener]::new()
    $listener.Prefixes.Add($Prefix)
    $listener.Start()

    try {
        for ($requestIndex = 0; $requestIndex -lt 2; $requestIndex++) {
            $context = $listener.GetContext()
            if ($context.Request.Headers['Client-Id'] -ne 'fixture-client-id' -or $context.Request.Headers['Client-Secret'] -ne 'fixture-client-secret') {
                $context.Response.StatusCode = 401
                $context.Response.Close()
                continue
            }

            $next = $context.Request.QueryString['next']
            if ([string]::IsNullOrEmpty($next)) {
                $body = @{
                    code = 200
                    message = $null
                    content = @{
                        data = @(
                            @{
                                liveId = 9001
                                liveTitle = 'Private title one'
                                liveThumbnailImageUrl = 'https://private.example/one.jpg'
                                concurrentUserCount = 30
                                tags = @('Game', 'Challenge')
                                categoryType = 'GAME'
                                liveCategory = 'fixture-game'
                                liveCategoryValue = 'Fixture Game'
                                channelId = 'private-channel-one'
                                channelName = 'Private streamer one'
                            },
                            @{
                                liveId = 9002
                                liveTitle = 'Private title two'
                                liveThumbnailImageUrl = 'https://private.example/two.jpg'
                                concurrentUserCount = 20
                                tags = @()
                                categoryType = 'ETC'
                                liveCategory = 'talk'
                                liveCategoryValue = 'Talk'
                                channelId = 'private-channel-two'
                                channelName = 'Private streamer two'
                            }
                        )
                        page = @{ next = 'second page' }
                    }
                }
            }
            elseif ($next -eq 'second page') {
                $body = @{
                    code = 200
                    message = $null
                    content = @{
                        data = @(
                            @{
                                liveId = 9002
                                liveTitle = 'Private duplicate title'
                                liveThumbnailImageUrl = 'https://private.example/duplicate.jpg'
                                concurrentUserCount = 10
                                tags = @('Game')
                                categoryType = 'GAME'
                                liveCategory = 'fixture-game'
                                liveCategoryValue = 'Fixture Game'
                                channelId = 'private-channel-two'
                                channelName = 'Private streamer two'
                            }
                        )
                        page = @{ next = $null }
                    }
                }
            }
            else {
                throw "Unexpected next token: $next"
            }

            $bytes = [Text.Encoding]::UTF8.GetBytes(($body | ConvertTo-Json -Depth 10 -Compress))
            $context.Response.ContentType = 'application/json'
            $context.Response.ContentLength64 = $bytes.Length
            $context.Response.OutputStream.Write($bytes, 0, $bytes.Length)
            $context.Response.Close()
        }
    }
    finally {
        $listener.Stop()
    }
} -ArgumentList $prefix

try {
    Start-Sleep -Milliseconds 300
    $env:RAIDER_CHZZK_CLIENT_ID = 'fixture-client-id'
    $env:RAIDER_CHZZK_CLIENT_SECRET = 'fixture-client-secret'

    & (Join-Path $PSScriptRoot 'chzzk_live_spike.ps1') `
        -BaseUri "${prefix}open/v1/lives" `
        -OutputDirectory $outputDirectory `
        -TimeoutSeconds 5 | Out-Null

    $measurement = Get-Content -Raw (Join-Path $outputDirectory 'measurement.json') | ConvertFrom-Json
    $fixtureText = Get-Content -Raw (Join-Path $outputDirectory 'normal.json')
    $fixture = $fixtureText | ConvertFrom-Json

    if ($measurement.pageCount -ne 2) { throw "Expected 2 pages, got $($measurement.pageCount)." }
    if ($measurement.streamCount -ne 3) { throw "Expected 3 streams, got $($measurement.streamCount)." }
    if ($measurement.duplicateBroadcastCount -ne 1) { throw "Expected 1 duplicate, got $($measurement.duplicateBroadcastCount)." }
    if ($measurement.streamsWithTags -ne 2) { throw "Expected 2 streams with tags, got $($measurement.streamsWithTags)." }
    if ($measurement.streamsWithCategory -ne 3) { throw "Expected 3 streams with category, got $($measurement.streamsWithCategory)." }
    if ($fixture.content.data.Count -ne 2) { throw "Expected 2 fixture streams, got $($fixture.content.data.Count)." }
    if ($fixture.content.data[0].tags.Count -ne 2) { throw 'Expected sanitized tags in fixture.' }
    if ($fixture.content.data[0].liveCategoryValue -ne 'Fixture category 1') { throw 'Expected sanitized category in fixture.' }
    if ($fixtureText -match 'Private|private|fixture-client-secret|fixture-client-id|9001|9002') {
        throw 'Generated fixture contains source identifiers or credentials.'
    }

    'CHZZK live spike verification: ok'
}
finally {
    Remove-Item Env:RAIDER_CHZZK_CLIENT_ID -ErrorAction SilentlyContinue
    Remove-Item Env:RAIDER_CHZZK_CLIENT_SECRET -ErrorAction SilentlyContinue
    Stop-Job $server -ErrorAction SilentlyContinue
    Remove-Job $server -Force -ErrorAction SilentlyContinue
}
