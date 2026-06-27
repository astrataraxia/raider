# Raider 엔지니어링 운영 계약.

## 1. 목적.

Raider는 CHZZK과 SOOP의 현재 라이브 방송을 수집하고 태그와 검색이 가능한 빠른 타일 화면으로 제공하는 작은 ASP.NET Core 애플리케이션이다. 이 문서는 구현자가 바뀌어도 단순성, 정확성, 실패 격리, 검증 방식이 흔들리지 않게 한다.

## 2. 최우선 원칙.

우선순위.

1. 관찰 가능한 동작의 정확성.
2. 단순하고 우아한 코드.
3. 반복 가능하고 빠른 테스트.
4. 한 플랫폼 장애가 전체 앱에 전파되지 않는 구조.
5. 잠금과 외부 호출이 없는 빠른 화면 요청.
6. 측정 근거가 있는 최적화.

단순하고 우아한 코드의 판단 기준.

- 데이터가 어디서 와서 어디로 가는지 파일 몇 개만 읽어도 이해할 수 있다.
- 같은 동작을 더 적은 타입, 계층, 분기, 설정으로 명확하게 표현할 수 있으면 단순한 쪽을 선택한다.
- 이름이 역할을 설명하며 `Manager`, `Helper`, `Util`, `Common`, `Base` 같은 포괄적 이름을 피한다.
- 메서드는 한 수준의 추상화를 유지하고 불필요한 래퍼, 전달 전용 계층, 범용 인터페이스를 만들지 않는다.
- 미래 가능성을 위한 플러그인 시스템, CQRS, MediatR, Repository, 범용 이벤트 버스는 현재 요구가 증명되기 전까지 금지한다.
- 우아함을 짧은 코드나 영리한 코드로 착각하지 않는다. 명시적인 평범한 코드가 압축된 기교보다 우선한다.

## 3. 실행 방식.

- WIP 제한은 1이다.
- 모든 구현 Task는 Red, Green, Refactor, Verify 순서를 따른다.
- 실패 테스트는 구현 세부가 아니라 관찰 가능한 동작을 검증한다.
- 최소 구현으로 테스트를 통과한 뒤에만 이름, 중복, 구조를 개선한다.
- Task 완료 전 변경 범위, 누락 테스트, 비밀값, 불필요한 개념을 검토한다.

Definition of Ready.

- 기대 동작과 범위 밖 동작이 명확하다.
- 먼저 작성할 실패 테스트가 구체적이다.
- 필요한 fixture와 외부 계약이 준비됐다.
- 미확정 제품 결정이 구현을 좌우하지 않는다.

Definition of Done.

- Red 실패를 의도한 이유로 확인했다.
- 최소 구현과 필요한 리팩터링을 완료했다.
- 관련 테스트와 정적 검사를 실행했다.
- `dotnet test`, `dotnet format --verify-no-changes`, `dotnet build --no-restore -warnaserror`가 통과했다.
- 문서와 Task 체크리스트를 갱신했다.

## 4. 기술 스택.

- .NET 10과 ASP.NET Core 단일 애플리케이션.
- Razor Pages 서버 렌더링과 직접 작성한 CSS.
- `BackgroundService` 기반 플랫폼별 독립 수집 루프.
- `IHttpClientFactory` 기반 플랫폼별 typed client.
- `System.Text.Json` 경계 DTO. Source generation은 측정 또는 trimming 필요가 확인될 때만 사용한다.
- `ImmutableArray<T>` 기반 읽기 모델과 `Interlocked.Exchange` 기반 원자 스냅샷 교체.
- `FrozenDictionary<string, ImmutableArray<int>>` 기반 태그 인덱스.
- xUnit, ASP.NET Core `TestServer` 또는 로컬 HTTP 서버, Playwright for .NET.
- 단일 Linux 컨테이너. 공용 즐겨찾기 SQLite 외 영구 데이터베이스와 영구 볼륨 없음.

도입하지 않는 기본 기술.

- Entity Framework Core, PostgreSQL, Redis.
- Elasticsearch, OpenSearch, 외부 검색 서비스.
- SignalR, 메시지 브로커, 마이크로서비스.
- 별도 DI 컨테이너와 별도 로깅 프레임워크.

## 5. 아키텍처.

작은 모듈식 모놀리스를 사용한다. 프로젝트를 여러 assembly로 나누지 않고 폴더와 namespace로 책임을 구분한다.

```text
Program
├── Live
│   ├── LiveStream
│   ├── LiveSnapshot
│   └── LiveSearch
├── Platforms
│   ├── Chzzk
│   └── Soop
├── Collection
│   ├── PlatformCollector
│   └── SnapshotStore
└── Web
    ├── Pages
    └── wwwroot
```

| 영역 | 책임 | 금지 |
| --- | --- | --- |
| `Live` | 공통 모델, 태그 정규화, 검색 텍스트, 중복 제거, 정렬, 스냅샷. | HTTP DTO, Razor, 플랫폼별 필드. |
| `Platforms.Chzzk` | CHZZK HTTP 계약과 공통 모델 변환. | SOOP 규칙, 화면 표현. |
| `Platforms.Soop` | SOOP HTTP 계약과 공통 모델 변환. | CHZZK 규칙, 화면 표현. |
| `Collection` | 독립 주기 실행, 실패 격리, 병합, 스냅샷 교체. | HTML 렌더링, 플랫폼 응답 직접 해석. |
| `Web` | 스냅샷 한 번 읽기, 필터, 검색, Razor 렌더링, 헬스 엔드포인트. | 외부 플랫폼 호출, 수집 실행. |
| `Favorites` | 공용 즐겨찾기 SQLite 저장과 현재 스냅샷 기반 라이브 상태 결합. | 사용자 계정, 외부 플랫폼 호출, 수집 worker 쓰기. |
| `Program` | 설정과 의존성 조립, 시작, 정상 종료. | 도메인 규칙과 응답 파싱. |

인터페이스는 안정적인 테스트 경계가 필요하거나 두 구현이 실제 존재할 때만 만든다. 플랫폼 어댑터의 공통 수집 계약은 필요하지만 모든 클래스에 인터페이스를 붙이지 않는다.

## 6. C# 코드 규칙.

- .NET 명명 규칙을 따른다. 타입과 public 멤버는 `PascalCase`, 지역 변수와 private 필드는 `camelCase`를 사용한다.
- nullable reference types를 활성화한다.
- 불변 데이터에는 `sealed record` 또는 생성 후 변경되지 않는 `sealed class`를 사용한다.
- 공개 API를 최소화하고 기본 가시성은 가능한 한 `internal` 또는 `private`로 둔다.
- `async` 메서드는 실제 비동기 I/O나 조정에만 사용하고 이름에 `Async`를 붙인다.
- 취소 가능한 비동기 경계는 `CancellationToken`을 마지막 인자로 받는다.
- 예외는 예외 상황에 사용한다. 정상 분기와 필드 검증에는 명시적인 결과를 사용한다.
- 메서드 인자가 많아지면 호출 맥락을 나타내는 입력 타입이 실제로 더 명확한지 검토한다.
- LINQ가 데이터 흐름을 명확하게 할 때 사용하고, 복잡한 다중 열거와 숨은 할당을 만들면 평범한 반복문을 사용한다.

권장 이름.

```text
FetchLiveStreamsAsync
ParseLivePage
BuildSnapshot
ReplaceSnapshot
NormalizeTag
SearchStreams
IsRetryable
```

피할 이름.

```text
ProcessData
HandleStuff
LiveManager
CommonHelper
BaseService
```

## 7. 도메인과 검색 계약.

`LiveStream` 의미.

- `Platform`: CHZZK 또는 SOOP.
- `BroadcastId`, `ChannelId`, `StreamerName`, `Title`, `ViewerCount`.
- 선택 `ThumbnailUrl`, 검증된 `WatchUrl`.
- 정규화되고 중복 제거된 `ImmutableArray<string> Tags`.
- 방송인 이름, 제목, 태그로 미리 만든 정규화 `SearchText`.
- `ObservedAt`.

태그 규칙.

- CHZZK의 `tags`와 카테고리, SOOP의 `auto_hashtags`, `category_tags`, `hash_tags`, `lang_tags`를 공통 태그로 변환한다.
- 앞뒤 공백 제거, Unicode Form C 정규화, 빈 값 제거, 대소문자 무시 중복 제거를 적용한다.
- UI 표시용 원문 태그와 검색 비교용 정규화 키를 구분하되 별도 범용 값 타입은 만들지 않는다.

`LiveSnapshot`은 다음 읽기 모델을 한 번에 포함한다.

- 시청자 수 내림차순이며 동점 정렬이 결정적인 `ImmutableArray<LiveStream> Streams`.
- 태그 키에서 `Streams` 배열 인덱스로 연결되는 `FrozenDictionary<string, ImmutableArray<int>> StreamsByTag`.
- 플랫폼별 마지막 성공 시각과 오류 상태.
- 스냅샷 관측 시각.

검색 규칙.

- 검색 대상은 방송인 이름, 제목, 태그다.
- 검색어는 trim, Unicode Form C, invariant 대소문자 정규화를 적용한다.
- 초기 구현은 `SearchText.Contains` 부분 문자열 검색을 사용한다.
- 태그 필터는 `StreamsByTag` 인덱스를 사용한다.
- 플랫폼과 태그 필터를 먼저 적용하고 검색어를 적용한다.
- 초성 검색, 오타 교정, 유사어, 외부 전문 검색 엔진은 실제 필요와 측정 결과가 나오기 전까지 범위 밖이다.

## 8. HTTP와 외부 계약.

- 플랫폼별 typed `HttpClient`를 등록하고 요청마다 새 `HttpClient`를 만들지 않는다.
- 고정 User-Agent, connect timeout, 전체 요청 timeout을 설정한다.
- 플랫폼 응답 DTO는 해당 플랫폼 namespace 밖으로 노출하지 않는다.
- 성공 상태라도 응답 필수 구조가 다르면 계약 오류다.
- 페이지 중 하나라도 실패하면 완성 결과로 인정하지 않는다.
- 외부 장애와 계약 오류를 정상 빈 목록으로 바꾸지 않는다.
- 원본 응답 전체, 인증 헤더, 쿠키, 토큰은 저장하거나 일반 로그에 출력하지 않는다.
- 자동 테스트는 외부 네트워크를 호출하지 않고 최소 익명 fixture와 로컬 HTTP 서버를 사용한다.

CHZZK은 공식 Open API를 운영 경로로 사용한다. SOOP은 공개 웹 JSON을 사용하며 변경과 차단 위험을 계약 테스트와 오류 상태로 드러낸다.

## 9. 수집과 동시성.

- 플랫폼마다 독립 `BackgroundService` 하나를 사용한다.
- 동일 플랫폼 수집은 겹치지 않는다.
- 시작 직후 첫 수집을 시도하고 이후 주기에 따라 실행한다.
- 재시도는 명시적으로 재시도 가능한 오류에만 적용하고 상한과 지터를 둔다.
- 제한 초과 오류는 즉시 재시도하지 않고 다음 기본 폴링 주기에 다시 시도한다. 현재 `Retry-After` 헤더는 반영하지 않는다.
- 성공한 플랫폼 결과만 교체하며 실패 플랫폼의 마지막 정상 목록은 유지한다.
- 플랫폼 상태를 병합해 완성된 `LiveSnapshot`을 만든 뒤 `Interlocked.Exchange`로 참조를 교체한다.
- 웹 요청은 현재 스냅샷 참조를 한 번 읽고 외부 I/O나 잠금을 기다리지 않는다.
- 즐겨찾기 API의 SQLite I/O는 라이브 수집 및 기본 홈 화면과 실패를 격리한다.
- 종료 토큰을 존중하고 진행 중 요청에 제한 시간을 적용한다.

## 10. 오류와 로그.

오류 종류.

- HTTP, 인증, 제한 초과, 차단, 타임아웃, 응답 계약, 도메인 필드, 시작 설정.

규칙.

- 오류 종류에서 재시도 가능 여부를 명시한다.
- 사용자 화면에는 안전한 상태 요약만 표시한다.
- 로그에는 원인과 안전한 구조화 필드를 남기되 비밀값, 전체 응답, 방송 제목, 방송인 이름, 사용자 검색어를 기록하지 않는다.
- 예상 가능한 플랫폼 실패를 애플리케이션 전체 예외로 확장하지 않는다.

구조화 로그 필드.

```text
platform
operation
result
stream_count
page_count
duration_ms
error_kind
retry_attempt
```

## 11. 웹 계약.

초기 route.

| Method | 경로 | 책임 |
| --- | --- | --- |
| `GET` | `/` | 현재 라이브 타일, 플랫폼·태그·검색 필터. |
| `GET` | `/health/live` | 프로세스가 HTTP 요청을 처리할 수 있는지 확인. |
| `GET` | `/health/ready` | 첫 수집 시도가 완료됐는지 확인. |

- 필터와 검색은 URL query parameter로 표현한다.
- 요청은 스냅샷을 한 번만 읽는다.
- Razor 기본 HTML encoding을 유지한다.
- 외부 링크 URL은 도메인 생성 시 검증한다.
- 화면에는 비밀값, 원본 응답, 내부 오류 상세를 포함하지 않는다.
- 스냅샷 시각은 수집과 저장 경계에서는 `DateTimeOffset` 그대로 유지하고, 홈 화면 표시 직전에 `Asia/Seoul` 기준으로 변환한다.
- 즐겨찾기 PUT은 현재 스냅샷에 존재하는 canonical `(Platform, ChannelId)`만 허용한다.
- 즐겨찾기 쓰기 API는 antiforgery 검증을 적용한다.

## 11.1. 공용 즐겨찾기 계약.

- 단일 SQLite 파일을 서버 호스트 bind mount에 저장한다.
- 기본 키는 `(platform, channel_id)`이며 방송별 `BroadcastId`는 저장 식별자로 사용하지 않는다.
- 오프라인 표시를 위해 마지막 확인 방송인 이름만 저장한다.
- 라이브 여부와 방송 URL은 현재 메모리 스냅샷에서 결합한다.
- 최신 수집 성공 목록에 있으면 라이브, 없으면 오프라인이다.
- 플랫폼 수집 실패 또는 오래된 상태에서는 라이브 여부를 `상태 확인 지연`으로 표시한다.
- 연결은 작업마다 짧게 열고 닫으며 busy timeout과 제한된 재시도를 사용한다.
- 초기화, 잠금, 손상, 권한 오류는 즐겨찾기 기능에만 격리한다.

## 12. 설정과 비밀값.

일반 설정은 `appsettings.json`에 안전한 기본값만 둔다. 비밀값은 로컬 개발에서 .NET User Secrets를 사용하고 운영과
Docker에서는 환경 변수를 사용한다. 실제 비밀값이 담긴 `.env` 파일과 별도 dotenv 패키지는 사용하지 않는다.

로컬 User Secrets 키.

```text
Raider:Chzzk:ClientId
Raider:Chzzk:ClientSecret
```

운영 환경 변수는 ASP.NET Core 계층형 설정 규칙에 맞춰 `RAIDER__` 접두사와 이중 밑줄을 사용한다.

초기 후보.

```text
RAIDER__BIND_ADDR
RAIDER__CHZZK__CLIENTID
RAIDER__CHZZK__CLIENTSECRET
RAIDER__CHZZK__POLLINTERVAL
RAIDER__SOOP__POLLINTERVAL
RAIDER__REQUESTTIMEOUT
RAIDER__COLLECTIONTIMEOUT
RAIDER__LOGLEVEL
```

- 필수 설정 누락과 잘못된 값은 시작 시 명확히 실패한다.
- 비밀값은 옵션 객체의 `ToString`, 로그, 오류에 노출하지 않는다.
- User Secrets는 사용자 프로필 외부에 저장되며 저장소 파일이나 배포 수단으로 취급하지 않는다.
- 환경 변수의 이중 밑줄은 `:`로 변환되고 설정 키 비교는 대소문자를 구분하지 않는다. 예를 들어
  `RAIDER__CHZZK__CLIENTID`는 `Raider:Chzzk:ClientId`를 덮어쓴다.
- 프로젝트 생성 전 PowerShell API 스파이크는 기존 `RAIDER_CHZZK_CLIENT_ID`, `RAIDER_CHZZK_CLIENT_SECRET` 환경 변수를
  사용한다.
- 운영 기본값은 `PLAN.md`와 `DEPLOYMENT.md`를 따른다.

## 13. 테스트 계약.

테스트 우선순위.

1. 순수 도메인과 검색 단위 테스트.
2. 플랫폼 fixture 변환과 HTTP 계약 테스트.
3. 수집과 스냅샷 통합 테스트.
4. Razor Page와 HTML fragment 계약 테스트.
5. 소수의 핵심 Playwright E2E.
6. 기본 테스트와 분리된 실제 API 스모크 테스트.

규칙.

- 테스트 이름은 `Given_When_Then` 형태를 사용한다.
- 테스트 하나는 실패 이유 하나를 갖는다.
- 시간은 실제 장시간 대기 대신 짧은 주기 또는 주입 가능한 시간 경계를 사용한다.
- 자체 타입을 과도하게 mock하지 않고 작은 fake와 실제 `HttpClient` 경계를 선호한다.
- 큰 HTML 전체 snapshot보다 안정적인 작은 fragment를 검증한다.

## 14. 품질과 의존성.

최소 검증.

```text
dotnet test
dotnet format --verify-no-changes
dotnet build --no-restore -warnaserror
```

- SDK와 ASP.NET Core 기본 기능을 우선한다.
- 새 NuGet 패키지는 현재 Task에 필요성이 증명될 때만 추가한다.
- 같은 역할의 패키지를 둘 이상 사용하지 않는다.
- 패키지 추가 전 유지보수 상태, 라이선스, 전이 의존성, 제거 가능성을 확인한다.
- 중앙 패키지 버전 관리나 복잡한 빌드 구조는 실제 프로젝트 수가 늘기 전까지 도입하지 않는다.

## 15. 보안과 성능.

- 외부 텍스트와 URL을 신뢰하지 않는다.
- Razor HTML encoding을 우회하지 않는다.
- 비루트 사용자와 읽기 전용 루트 파일시스템으로 컨테이너를 실행한다.
- 원본 JSON, 과거 방송, 이미지 파일을 메모리에 보관하지 않는다.
- 화면 요청은 스냅샷 읽기, 필터, 검색, 렌더링만 수행한다.
- 수천 건 규모에서는 메모리 내 부분 문자열 검색과 태그 인덱스를 우선한다.
- `FrozenDictionary`, source generation, pooling 등은 읽기 경로 또는 측정 근거가 있을 때만 사용한다. 태그 인덱스는 현재 읽기 중심 계약 때문에 허용한다.
- 확정 운영 값은 플랫폼별 10분 폴링, CPU 1, 메모리 256MB, PID 128이며 실제 측정 결과는 `DEPLOYMENT.md`와 `RELEASES.md`를 따른다.

## 16. 코드 리뷰 체크리스트.

- [ ] 동작이 정확하고 테스트로 증명됐는가.
- [ ] 데이터 흐름이 직접적이고 이해하기 쉬운가.
- [ ] 불필요한 타입, 계층, 인터페이스, 설정, 패키지가 없는가.
- [ ] 더 단순한 구현이 같은 명확성과 정확성을 제공하지 않는가.
- [ ] 외부 실패와 정상 빈 목록을 구분하는가.
- [ ] 한 플랫폼 실패가 다른 플랫폼에 전파되지 않는가.
- [ ] 화면 요청이 외부 I/O나 잠금을 기다리지 않는가.
- [ ] 태그와 검색 정규화가 일관적인가.
- [ ] 비밀값과 원본 응답이 노출되지 않는가.
- [ ] 관련 문서와 체크리스트가 갱신됐는가.
