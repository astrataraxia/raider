# Goal 2. .NET 기반과 공통 도메인 구축.

## 목표.

.NET 10과 ASP.NET Core 단일 애플리케이션의 최소 기반을 만들고, 플랫폼과 무관한 라이브·태그·검색 모델을 TDD로 구축한다.

## 산출물.

- 실행 가능한 ASP.NET Core 애플리케이션과 `/health/live`.
- 공통 `LiveStream`, `LiveSnapshot`, `Platform`, `PlatformError`.
- 태그 정규화, 검색 텍스트, 태그 인덱스.
- 테스트, 포맷, 경고 오류 빌드가 동작하는 솔루션.

## Task.

### Task 2.1. ASP.NET Core 프로젝트와 품질 게이트를 만든다.

Red.

- [x] 아직 존재하지 않는 `/health/live`가 `200 OK`를 반환해야 한다는 통합 테스트를 작성하고 실패를 확인한다.

Green.

- [x] 최소 ASP.NET Core 앱과 xUnit 테스트 프로젝트를 만든다.
- [x] `/health/live`를 구현한다.
- [x] 웹 프로젝트에 User Secrets를 초기화하고 CHZZK 로컬 비밀 키 이름을 문서화한다.
- [x] `dotnet test`, `dotnet format --verify-no-changes`, `dotnet build --no-restore -warnaserror`를 통과시킨다.

### Task 2.2. 공통 라이브와 태그 모델을 만든다.

Red.

- [x] 필수 필드, URL, 시청자 수 불변 조건 테스트.
- [x] 태그 trim, Unicode Form C, 빈 값 제거, 대소문자 무시 중복 제거 테스트.
- [x] 플랫폼과 방송 ID 중복 제거, 시청자 수 정렬, 결정적 동점 정렬 테스트.

Green.

- [x] `Platform`, `LiveStream`, 검증과 정렬 함수를 구현한다.
- [x] 정규화된 `Tags`와 `SearchText` 생성을 구현한다.

### Task 2.3. 검색 스냅샷을 만든다.

Red.

- [x] 방송인 이름, 제목, 태그 부분 문자열 검색 테스트.
- [x] 플랫폼과 태그 필터 조합 테스트.
- [x] 빈 검색어와 존재하지 않는 태그 테스트.

Green.

- [x] `ImmutableArray<LiveStream>`과 `FrozenDictionary<string, ImmutableArray<int>>`를 포함한 `LiveSnapshot`을 구현한다.
- [x] 단순하고 직접적인 검색 함수를 구현한다.

### Task 2.4. 플랫폼 오류와 설정 경계를 만든다.

Red.

- [x] 인증, 제한 초과, 차단, 타임아웃, 계약 오류의 재시도 가능 여부 테스트.
- [x] User Secrets와 `RAIDER__` 환경 변수 바인딩, 필수 설정 누락, 비밀값 출력 방지 테스트.

Green.

- [x] 최소 플랫폼 수집 계약, 오류 분류, 옵션 검증을 구현한다.

2026-06-13 기준 모든 Task와 완료 조건을 충족했다. Goal 3과 Goal 4 어댑터 구현을 시작할 수 있다.

## 검증 명령.

```text
dotnet test
dotnet format --verify-no-changes
dotnet build --no-restore -warnaserror
```

## 완료 조건.

- 외부 네트워크 없이 모든 테스트가 통과한다.
- `/health/live`가 응답한다.
- Goal 3과 Goal 4가 공통 모델과 오류 계약만 의존해 구현 가능하다.
- 검색 구조가 외부 검색 엔진이나 데이터베이스 없이 동작한다.
