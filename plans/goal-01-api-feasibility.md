# Goal 1. CHZZK과 SOOP 수집 가능성 검증.

## 목표.

본 구현을 시작하기 전에 CHZZK과 SOOP의 현재 라이브 전체 목록을 반복 가능하게 수집할 수 있는지 증명한다. 플랫폼별 응답 fixture, 필드 매핑, 페이지네이션, 호출 제한, 실패 형태를 확보한다.

## 선행 조건.

- CHZZK 개발자 애플리케이션과 Client 인증 정보가 준비되어야 한다.
- SOOP 파트너 API 사용 가능 여부를 확인할 수 있어야 한다.

## 산출물.

- CHZZK과 SOOP의 민감 정보가 제거된 응답 fixture.
- 플랫폼별 필드와 태그 매핑표.
- 플랫폼별 수집 경로와 페이지네이션 규칙.
- 호출량, 응답 시간, 실패 형태 측정 결과.
- SOOP 수집 방식 채택 또는 중단 결정.

## Task.

### Task 1.1. 필수 공통 필드를 정의한다.

- [x] 공통 필드를 플랫폼, 방송 ID, 채널 ID, 방송인 이름, 제목, 시청자 수, 썸네일 URL, 원본 방송 URL, 태그로 확정한다.
- [x] 필수 필드와 선택 필드를 구분한다.
- [x] 필수 필드 누락 시 해당 방송을 제외하고 오류를 기록하는 정책을 확정한다.

검증.

- 두 플랫폼의 필드 매핑표에서 모든 필수 공통 필드의 출처를 설명할 수 있어야 한다.

#### 공통 필드 계약.

| 공통 필드 | 필수 여부 | 규칙 |
| --- | --- | --- |
| `platform` | 필수 | 어댑터가 `CHZZK` 또는 `SOOP`으로 지정한다. |
| `broadcast_id` | 필수 | 플랫폼 내 현재 방송 식별자다. 플랫폼과 방송 ID 조합은 스냅샷 내에서 유일해야 한다. |
| `channel_id` | 필수 | 플랫폼 채널 또는 방송인 계정 식별자다. |
| `streamer_name` | 필수 | 앞뒤 공백 제거 후 빈 문자열이면 유효하지 않다. |
| `title` | 필수 | 앞뒤 공백 제거 후 빈 문자열이면 유효하지 않다. |
| `viewer_count` | 필수 | 0 이상의 정수여야 한다. |
| `thumbnail_url` | 선택 | 유효한 HTTP 또는 HTTPS URL일 때만 보존한다. 누락 또는 오류 시 기본 썸네일을 사용한다. |
| `watch_url` | 필수 | 플랫폼 식별자로 조립한 원본 방송 페이지의 유효한 HTTP 또는 HTTPS URL이어야 한다. |
| `tags` | 선택 | 플랫폼 태그와 카테고리를 정규화하고 중복 제거한 목록이다. 태그가 없어도 방송은 유효하다. |

`thumbnail_url`은 타일의 표시 항목이지만 외부 응답에서 누락되거나 잘못될 수 있으므로 방송 자체를 제외하는 필수 조건으로 삼지 않는다. 나머지 필수 필드가 누락되거나 유효하지 않으면 해당 방송만 제외하고 `WARN` 로그에 `platform`, `operation`, `error_kind`를 기록한다. 로그에는 원본 응답 본문, 인증 정보, 방송 제목, 방송인 이름을 기록하지 않는다. 제외된 방송 수는 수집 성공 요약에 포함하며, 필수 필드 오류를 플랫폼 전체의 정상 빈 목록으로 처리하지 않는다.

#### 플랫폼 필드 매핑.

| 공통 필드 | CHZZK 라이브 목록 | SOOP 방송 목록 |
| --- | --- | --- |
| `platform` | 어댑터 상수 `CHZZK`. | 어댑터 상수 `SOOP`. |
| `broadcast_id` | `liveId`. | `broad_no`. |
| `channel_id` | `channelId`. | `user_id`. |
| `streamer_name` | `channelName`. | `user_nick`. |
| `title` | `liveTitle`. | `broad_title`. |
| `viewer_count` | `concurrentUserCount`. | 채택한 공개 웹 JSON 경로의 `current_view_cnt`를 0 이상의 정수로 변환. 공식 Open API `broad/list`를 사용할 수 있게 되면 `total_view_cnt` 의미를 다시 검증한다. |
| `thumbnail_url` | `liveThumbnailImageUrl`. | `broad_thumb`. 스킴 상대 URL이면 HTTPS URL로 정규화. |
| `watch_url` | `https://chzzk.naver.com/live/{channelId}`로 조립. | `https://play.sooplive.co.kr/{user_id}/{broad_no}`로 조립. |
| `tags` | 공식 API의 `tags`, `liveCategoryValue`. | 공개 웹 JSON의 `auto_hashtags`, `category_tags`, `hash_tags`, `lang_tags`, `category_name`. |

매핑 근거.

- CHZZK 공식 Live API 문서: `GET /open/v1/lives` 응답 필드와 전체 라이브 목록 조회 계약. <https://chzzk.gitbook.io/chzzk/chzzk-api/live>
- SOOP 공식 Open API 문서: `broad/list` 응답 필드와 원본 시청 URL 조립 계약. <https://openapi.sooplive.co.kr/apidoc>
- SOOP 파트너 API 접근 가능 여부와 실제 응답 형태는 Task 1.3에서 별도로 검증한다.

### Task 1.2. CHZZK 실제 수집 경로를 검증한다.

- [x] Client 인증으로 첫 페이지를 호출한다.
- [x] `page.next`가 끝날 때까지 전체 페이지를 순회한다.
- [x] 전체 방송 수, 페이지 수, 총 응답 시간, 중복 방송 수를 기록한다.
- [x] `401`, `403`, `429`, `5xx` 응답 형태를 문서에서 확인한다.
- [x] 정상, 빈 목록, 페이지네이션 fixture를 만든다.

검증.

- 동일한 수집 절차를 다시 실행해 전체 목록을 얻을 수 있어야 한다.
- fixture에는 인증 정보와 개인 정보가 없어야 한다.
- 공식 API fixture에서 태그 필드 출처와 포함률을 확인해야 한다.

#### CHZZK 수집 계약과 현재 검증 상태.

- Endpoint: `GET https://openapi.chzzk.naver.com/open/v1/lives`.
- 인증 헤더: `Client-Id`, `Client-Secret`, `Content-Type: application/json`.
- 페이지 크기: `size=20`. 공식 문서가 허용하는 최대값이다.
- 페이지네이션: 응답의 `content.page.next`가 비어 있지 않으면 다음 요청의 `next` query parameter로 전달하고, 비어 있으면 종료한다.
- 정렬: API 응답은 시청자 수 높은 순이다.
- 성공 응답: `{"code": 200, "message": null, "content": {...}}`.
- 오류 응답: `{"code": integer, "message": string}`.
- 공식 오류 계약: `401 UNAUTHORIZED` 또는 `INVALID_CLIENT`, `403 FORBIDDEN`, `429 TOO_MANY_REQUESTS`, `500 INTERNAL_SERVER_ERROR`.
- 2026-06-12 무인증 실제 요청은 HTTP `401`과 `code: 401`, `message: "클라이언트 인증이 필요한 API 입니다."` 응답을 반환했다.
- 2026-06-12 합성 Client 인증 헤더를 사용한 실제 요청은 HTTP `401`과 `code: 401`, `message: "INVALID_CLIENT"` 응답을 반환했다.
- 2026-06-13 유효 Client 인증으로 공식 API 전체 페이지 순회를 두 번 실행했다.

반복 실행 스파이크는 `scripts/chzzk_live_spike.ps1`이다. 환경 변수 `RAIDER_CHZZK_CLIENT_ID`, `RAIDER_CHZZK_CLIENT_SECRET`을 읽고 최대 페이지 크기로 전체 목록을 순회하며 페이지 수, 방송 수, 중복 방송 수, 태그와 카테고리 포함 수, 총 응답 시간을 기록한다. 출력 fixture는 식별자, 방송인 이름, 제목, 썸네일 URL, 태그, 카테고리를 합성값으로 교체하며 인증 헤더와 원본 응답은 파일에 저장하지 않는다. 운영 endpoint가 기본값이며, 로컬 HTTP 서버 검증을 위해 `BaseUri`와 timeout만 주입할 수 있다.

2026-06-13 반복 측정.

| 실행 | 페이지 항목 수 | 고유 방송 수 | 페이지 수 | 중복 방송 수 | 태그 포함 | 카테고리 포함 | 총 응답 시간 |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 1 | 3,240 | 3,208 | 163 | 32 | 2,808, 86.7% | 3,090, 95.4% | 5,161ms |
| 2 | 3,225 | 3,225 | 163 | 0 | 2,802, 86.9% | 3,075, 95.3% | 4,981ms |

두 실행 모두 공식 API의 `tags`, `liveCategoryValue`를 확인했다. 첫 실행은 동적 시청자 수 정렬로 페이지 경계가 움직여
중복 32건이 발생했고 두 번째 실행은 중복이 없었다. 어댑터는 `liveId` 중복을 제거하고 완성된 전체 순회만 새 스냅샷
후보로 사용한다. 측정 결과와 익명 fixture는 `.codex-work/chzzk-live-spike-run-1/`,
`.codex-work/chzzk-live-spike-run-2/`에 저장했다.

CHZZK 웹 앱의 내부 JSON `GET https://api.chzzk.naver.com/service/v1/lives`도 로그인, 쿠키, Client 인증 없이 호출 가능하고 태그와 카테고리를 제공한다. 그러나 동일 목적의 공식 Open API가 존재하므로 최종 운영 경로는 공식 API로 유지한다. 내부 JSON은 공식 인증정보 준비 전 계약 조사와 fixture 보강에만 사용하며, 공식 API가 필수 태그를 제공하지 않는 경우에만 위험을 다시 평가한다.

`scripts/test_chzzk_live_spike.ps1` 로컬 검증은 합성 Client 인증 헤더를 확인하는 HTTP 서버에서 `page.next`가 있는 첫 페이지와 마지막 페이지를 제공한다. 검증 결과 스파이크는 두 페이지, 방송 3건, 중복 방송 1건을 측정했고 생성 fixture에서 원본 식별자, 제목, 채널 정보, 인증값을 제거했다. 이 검증은 수집 절차의 반복 가능성을 증명하지만 실제 CHZZK의 유효 Client 인증 성공과 운영 호출량·응답 시간 측정을 대신하지 않는다.

fixture 목록.

- `normal.json`: 정상 단일 페이지.
- `empty.json`: 정상 빈 목록.
- `pagination-first.json`, `pagination-last.json`: `page.next`가 있는 첫 페이지와 마지막 페이지.
- `missing-required-field.json`: 필수 제목 누락.
- `error-401.json`: 실제 무인증 오류 응답.
- `error-invalid-client.json`: 실제 잘못된 Client 인증 오류 응답.

### Task 1.3. SOOP 실제 수집 경로를 검증한다.

- [x] 파트너 Open API 사용 가능 여부를 확인한다.
- [x] 사용할 수 없다면 SOOP 웹의 라이브 목록 네트워크 요청을 조사한다.
- [x] 공개 JSON 요청, 서버 렌더링 데이터, HTML 파싱 순으로 유지 가능한 경로를 평가한다.
- [x] 페이지네이션, 필수 헤더, 쿠키 필요 여부, 차단 가능성을 기록한다.
- [x] 정상, 빈 목록, 페이지네이션 fixture를 만든다.

검증.

- 로그인 세션 없이 개인 서버 컨테이너에서 반복 호출 가능한 경로여야 한다.
- 이용 조건과 유지보수 위험을 명시적으로 수용하거나 구현 중단을 결정해야 한다.

#### 공식 Open API 평가.

- 공식 `GET https://openapi.sooplive.com/broad/list`는 전체 또는 카테고리별 현재 방송 목록을 페이지당 60건 제공한다.
- `client_id`가 필수이며 현재 환경에는 발급된 SOOP 개발자 Client ID가 없다.
- 2026-06-12 무인증 실제 요청은 HTTP `200`, `result: -1100`, 인자 오류를 반환했다.
- 합성 Client ID 실제 요청은 HTTP `200`, `result: -1104`, `This is an invalid client.(163)`을 반환했다.
- 공식 API는 Client ID를 확보하기 전까지 사용할 수 없는 경로로 판단한다.

#### 공개 웹 JSON 경로 평가.

채택 경로.

```text
GET https://live.sooplive.com/api/main_broad_list_api.php
    ?selectType=action
    &selectValue=all
    &orderType=broad_start
    &pageNo={1부터 시작하는 페이지 번호}
    &lang=ko_KR
```

- SOOP 웹 앱의 `/live/all` 화면이 사용하는 공개 JSON 요청이다.
- 로그인 세션, API 키, 요청 쿠키 없이 HTTP `200` JSON 응답을 반복해서 반환했다.
- `Accept: application/json`과 고정 `User-Agent`만 보낸다.
- 응답은 `AbroadChk`, `AbroadVod` 지역 판별 쿠키를 설정하지만 수집기는 쿠키를 저장하거나 다음 요청에 보내지 않는다.
- 페이지 크기는 60건이다. 첫 응답의 `total_cnt`를 기준으로 `ceil(total_cnt / 60)` 페이지를 순회한다.
- 응답의 `cnt`는 현재 페이지 건수이며 마지막 페이지 또는 범위 밖 페이지는 빈 `broad` 배열을 반환한다.
- 현재 시청자 수는 `current_view_cnt`다. 실제 첫 페이지 60건에서 `total_view_cnt`와 모두 달랐으므로 `total_view_cnt`를 현재 시청자 수로 사용하지 않는다.
- 수집 정렬은 `broad_start`를 사용하고 화면의 시청자 수 정렬은 수집 후 수행한다. 실제 비교 측정에서 `view_cnt` 정렬은 순회당 중복 5건에서 7건, `broad_start` 정렬은 1건에서 4건이어서 방송 시작순이 더 안정적이었다.
- `broad_start`도 새 방송 시작과 종료로 페이지 경계가 움직여 중복 또는 누락이 발생할 수 있다. `broad_no` 중복은 제거하되, 한 번의 순회에서 발생한 누락 가능성은 다음 폴링으로 복구한다.

2026-06-12 반복 측정.

| 실행 | 보고 방송 수 | 페이지 항목 수 | 고유 방송 수 | 페이지 수 | 중복 방송 수 | 필수 필드 누락 | 총 응답 시간 |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 1 | 1,274 | 1,274 | 1,274 | 22 | 0 | 0 | 994ms |
| 2 | 1,274 | 1,274 | 1,274 | 22 | 0 | 0 | 921ms |
| 3 | 1,274 | 1,274 | 1,273 | 22 | 1 | 0 | 887ms |

세 실행은 `broad_start` 정렬과 별도 HTTP 세션을 사용했고 모두 전체 페이지를 순회했다. 페이지별 응답 시간은 25ms에서 112ms 범위였다. 첫 두 실행은 API 보고 수와 고유 방송 수가 일치했고, 세 번째 실행은 중복 1건 때문에 고유 방송 수가 보고 수보다 1건 적었다. 이는 방송 시작순이 시청자 수 순보다 안정적이지만 한 번의 페이지 순회가 완전히 일관된 스냅샷을 보장하지는 않음을 실제로 확인한 결과다. 측정 결과는 `.codex-work/soop-live-spike-run-1/measurement.json`, `.codex-work/soop-live-spike-run-2/measurement.json`, `.codex-work/soop-live-spike-final/measurement.json`에 저장했다.

#### 대안 평가와 위험 수용.

| 후보 | 평가 | 결정 |
| --- | --- | --- |
| 공식 Open API `broad/list` | 문서화된 계약이지만 Client ID가 필수다. | Client ID 확보 전까지 보류한다. |
| 공개 웹 JSON `main_broad_list_api.php` | 전체 목록, 필수 필드, 페이지네이션을 로그인 없이 제공한다. | MVP SOOP 수집 경로로 채택한다. |
| 정적 JSON `main_broad_list_json.js` | 로그인 없이 빠르지만 실제 확인 결과 상위 80건만 포함한다. | 전체 목록 요구를 만족하지 않아 제외한다. |
| 서버 렌더링 데이터와 HTML 파싱 | `/live/all` HTML에 방송 목록이 없고 클라이언트가 공개 JSON을 호출한다. | 공개 JSON보다 취약하고 불필요하므로 제외한다. |

공개 웹 JSON은 공식 Open API 계약이 아니므로 사전 고지 없이 필드, query parameter, 응답 크기, 접근 정책이 바뀌거나 차단될 수 있다. `www.sooplive.com/robots.txt`는 전체 경로를 허용하지만 이는 API 안정성 보장이나 이용 약관의 명시적 수집 허가가 아니다. 개인 서버 MVP에서 낮은 빈도로 호출하고, 계약 오류를 빈 목록으로 처리하지 않으며, 변경 감지 시 수집 실패로 노출하는 조건으로 이 유지보수 위험을 수용한다.

#### 반복 실행과 fixture.

반복 실행 스파이크는 `scripts/soop_live_spike.ps1`이다. 전체 페이지를 순회해 페이지 수, 보고 방송 수, 페이지 항목 수, 고유 방송 수, 중복 수, 보고 수와 고유 수의 차이, 필수 필드 누락 수, 응답 시간을 기록한다. 원본 응답은 저장하지 않고 합성 식별자와 텍스트를 사용한 fixture만 생성한다. `scripts/test_soop_live_spike.ps1`은 로컬 HTTP 서버로 무쿠키 요청, 페이지 순회, 중복 측정, `current_view_cnt` 사용, 익명화를 검증한다.

fixture 목록.

- `normal.json`: 정상 단일 페이지.
- `empty.json`: 정상 빈 목록.
- `pagination-first.json`, `pagination-last.json`: 60건을 넘는 목록의 첫 페이지와 마지막 페이지.
- `missing-required-field.json`: 필수 제목 누락.
- `error-invalid-client.json`: 공식 Open API의 실제 잘못된 Client ID 오류 응답.

### Task 1.4. 호출 정책을 결정한다.

- [x] 플랫폼별 전체 수집 호출 수를 계산한다.
- [x] 초기 폴링 주기 후보를 정한다.
- [x] 타임아웃, 재시도 대상, 백오프 상한을 정한다.
- [x] 한 플랫폼 장애가 다른 플랫폼 수집에 영향을 주지 않는 정책을 확정한다.

#### 초기 호출 정책.

플랫폼별 수집기는 서로 독립적으로 시작 직후 한 번 수집하고 이후 10분마다 실행한다. 한 플랫폼의 실행 중에는 같은
플랫폼의 다음 실행을 겹쳐 시작하지 않는다. 공개된 quota 수치가 없고 CHZZK 전체 순회가 현재 163회 호출을 요구하므로
초기값은 보수적으로 정한다.

| 항목 | CHZZK | SOOP |
| --- | ---: | ---: |
| 한 번의 전체 순회 호출 수 | 측정값 163회. `ceil(전체 방송 수 / 20)` | 2026-06-13 측정값 41회. `ceil(total_cnt / 60)` |
| 초기 폴링 주기 | 10분 | 10분 |
| 하루 예상 호출 수 | 현재 규모 기준 약 23,472회 | 현재 규모 기준 약 5,904회 |
| 요청별 timeout | 5초 | 5초 |
| 전체 순회 timeout | 60초 | 30초 |

재시도 정책.

- 네트워크 연결 실패, HTTP `408`, `5xx`는 현재 페이지 요청을 한 번만 재시도한다.
- 재시도 전 1초에서 3초 사이 지터를 둔다. 한 번의 재시도 실패 후 전체 순회를 실패 처리한다.
- `401`, `403`, 기타 계약 오류와 필수 구조 누락은 재시도하지 않는다.
- `429`는 즉시 재시도하지 않고 `Retry-After`가 있으면 따르며 최대 30분까지 다음 수집을 늦춘다. 헤더가 없으면 다음
  기본 폴링 주기까지 기다린다.
- 페이지 하나라도 실패하거나 전체 timeout을 넘으면 부분 결과를 폐기하고 이전 정상 스냅샷을 유지한다.
- CHZZK과 SOOP 수집기는 별도 `BackgroundService`, 취소 토큰, 상태를 사용한다. 한 플랫폼 실패는 다른 플랫폼 실행,
  스냅샷, 화면 응답을 막지 않는다.
- 실제 운영에서 `429`, 수집 시간, 페이지 수를 관찰해 플랫폼별 폴링 주기를 독립적으로 늘리거나 줄인다.

## TDD 적용.

이 Goal은 탐색 스파이크이므로 실제 호출 자체를 자동 테스트로 만들지 않는다. 대신 확보한 fixture로 다음 Goal에서 먼저 실패하는 계약 테스트를 작성할 수 있어야 한다.

fixture 승인 조건.

- 정상 목록, 빈 목록, 다음 페이지, 필드 누락, 오류 응답을 재현할 수 있다.
- 민감 정보가 제거되어 저장소에 커밋 가능하다.
- 필드 매핑과 기대 결과가 문서화되어 있다.

## 완료 조건.

- CHZZK과 SOOP 양쪽의 현재 라이브 목록을 반복 수집할 수 있다.
- 양쪽 fixture와 필드 매핑이 준비되었다.
- 양쪽 태그 출처와 정규화 가능한 필드가 확인되었다.
- SOOP 수집 위험을 수용하기로 결정했다.
- 폴링 주기, 타임아웃, 재시도 정책의 초기값이 정해졌다.
- 하나라도 충족하지 못하면 Goal 2 구현을 시작하지 않는다.

2026-06-13 기준 위 완료 조건을 모두 충족했다. Goal 2 구현을 시작할 수 있다.
