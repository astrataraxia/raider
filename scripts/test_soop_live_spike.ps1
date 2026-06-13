# SOOP 수집 스파이크의 무쿠키 페이지 순회, 측정, 익명화를 로컬 서버로 검증한다.
$ErrorActionPreference = 'Stop'

$port = 18766
$prefix = "http://127.0.0.1:$port/"
$outputDirectory = Join-Path $PSScriptRoot '..\.codex-work\soop-live-spike-test'

$server = Start-Job -ScriptBlock {
    param($Prefix)

    $listener = [System.Net.HttpListener]::new()
    $listener.Prefixes.Add($Prefix)
    $listener.Start()

    try {
        for ($requestIndex = 0; $requestIndex -lt 2; $requestIndex++) {
            $context = $listener.GetContext()
            if (-not [string]::IsNullOrEmpty($context.Request.Headers['Cookie'])) {
                throw 'SOOP spike sent a request cookie.'
            }

            $pageNumber = [int]$context.Request.QueryString['pageNo']
            if ($pageNumber -eq 1) {
                $body = @{
                    total_cnt = 120
                    cnt = 2
                    broad = @(
                        @{
                            broad_no = 9001
                            user_id = 'private-user-one'
                            user_nick = 'Private streamer one'
                            broad_title = 'Private title one'
                            broad_thumb = '//private.example/one.jpg'
                            current_view_cnt = 30
                            total_view_cnt = 300
                            broad_grade = '0'
                            is_password = '0'
                        },
                        @{
                            broad_no = 9002
                            user_id = 'private-user-two'
                            user_nick = 'Private streamer two'
                            broad_title = 'Private title two'
                            broad_thumb = '//private.example/two.jpg'
                            current_view_cnt = 20
                            total_view_cnt = 200
                            broad_grade = '0'
                            is_password = '0'
                        }
                    )
                    time = 0
                    is_wp = 0
                }
            }
            elseif ($pageNumber -eq 2) {
                $body = @{
                    total_cnt = 120
                    cnt = 1
                    broad = @(
                        @{
                            broad_no = 9002
                            user_id = 'private-user-two'
                            user_nick = 'Private streamer two'
                            broad_title = 'Private duplicate title'
                            broad_thumb = '//private.example/duplicate.jpg'
                            current_view_cnt = 10
                            total_view_cnt = 100
                            broad_grade = '0'
                            is_password = '0'
                        }
                    )
                    time = 0
                    is_wp = 0
                }
            }
            else {
                throw "Unexpected page number: $pageNumber"
            }

            $bytes = [Text.Encoding]::UTF8.GetBytes(($body | ConvertTo-Json -Depth 10 -Compress))
            $context.Response.Headers.Add('Set-Cookie', 'AbroadChk=fixture; Path=/')
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

    & (Join-Path $PSScriptRoot 'soop_live_spike.ps1') `
        -BaseUri "${prefix}api/main_broad_list_api.php" `
        -OutputDirectory $outputDirectory `
        -TimeoutSeconds 5 | Out-Null

    $measurement = Get-Content -Raw (Join-Path $outputDirectory 'measurement.json') | ConvertFrom-Json
    $fixtureText = Get-Content -Raw (Join-Path $outputDirectory 'normal.json')
    $fixture = $fixtureText | ConvertFrom-Json

    if ($measurement.pageCount -ne 2) { throw "Expected 2 pages, got $($measurement.pageCount)." }
    if ($measurement.pageEntryCount -ne 3) { throw "Expected 3 page entries, got $($measurement.pageEntryCount)." }
    if ($measurement.uniqueStreamCount -ne 2) { throw "Expected 2 unique streams, got $($measurement.uniqueStreamCount)." }
    if ($measurement.duplicateBroadcastCount -ne 1) { throw "Expected 1 duplicate, got $($measurement.duplicateBroadcastCount)." }
    if ($measurement.reportedUniqueGap -ne 118) { throw "Expected a reported unique gap of 118, got $($measurement.reportedUniqueGap)." }
    if ($measurement.missingRequiredFieldCount -ne 0) { throw 'Expected no missing required fields.' }
    if ($measurement.sendsRequestCookies) { throw 'Expected sendsRequestCookies to be false.' }
    if ($fixture.broad[0].current_view_cnt -ne 30) { throw 'Expected current viewer count in fixture.' }
    if ($fixtureText -match 'Private|private|9001|9002') {
        throw 'Generated fixture contains source identifiers.'
    }

    'SOOP live spike verification: ok'
}
finally {
    Stop-Job $server -ErrorAction SilentlyContinue
    Remove-Job $server -Force -ErrorAction SilentlyContinue
}
