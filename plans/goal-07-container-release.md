# Goal 7. 단일 컨테이너 출시 준비.

## 목표.

ASP.NET Core 단일 애플리케이션을 안전한 Linux 컨테이너로 배포하고 재시작, 장애, 자원 제한, 검색과 화면 성능을 검증한다.

## 산출물.

- .NET 다단계 Dockerfile.
- 헬스체크, 정상 종료, 비루트, 읽기 전용 파일시스템 설정.
- 메모리와 CPU 제한 권장값.
- 자동 검사와 실제 API 스모크 테스트 절차.

## Task.

### Task 7.1. 컨테이너 이미지를 만든다.

- [x] 이미지 시작과 `/health/live`, 비루트 실행, 무볼륨 실행 실패 검증을 먼저 작성한다.
- [x] SDK 빌드와 ASP.NET Core runtime을 분리한 Dockerfile을 작성한다.
- [x] Razor Pages와 정적 assets 포함을 검증한다.

### Task 7.2. 시작, 종료, 무상태 복구를 검증한다.

- [x] 시작 직후 수집, readiness 변화, 제한 시간 정상 종료를 검증한다.
- [x] 컨테이너 재생성 후 영구 볼륨 없이 현재 방송이 복구되는지 확인한다.

### Task 7.3. 자원 제한과 성능을 검증한다.

- [x] 유휴와 전체 수집 중 메모리를 측정한다.
- [x] 수집과 검색·화면 부하를 동시에 실행해 p50과 p95를 기록한다.
- [x] 안정적인 메모리와 CPU 제한을 확정한다.

### Task 7.4. 보안과 관측성을 검증한다.

- [x] 비밀값, 쿠키, 원본 응답, 사용자 검색어가 로그와 HTML에 없는지 검사한다.
- [x] 읽기 전용 루트 파일시스템과 플랫폼별 수집 상태 로그를 검증한다.

### Task 7.5. 출시 회귀 검증을 실행한다.

```text
dotnet test
dotnet format --verify-no-changes
dotnet build --no-restore -warnaserror
playwright test
docker build
container smoke test
actual API smoke test
```

저장소의 Playwright E2E는 `dotnet test --filter HomePagePlaywright`로 실행한다.

## 검증 결과.

- WSL2 Ubuntu 24.04와 Docker Engine `29.5.2`에서 .NET 10 다단계 이미지 `raider:local`을 빌드했다. 최종 이미지 크기는 `98,355,855` bytes다.
- 기본 이미지 스모크에서 비루트 `app`, 읽기 전용 루트, 무볼륨, `/health/live`, Razor 홈, 정적 CSS, healthcheck, 1초 이내 정상 종료를 확인했다.
- 실제 CHZZK 공식 API와 SOOP 공개 JSON을 사용하는 출시 스모크에서 readiness `503 → 200`, 플랫폼별 성공 로그, 컨테이너 재생성 후 무상태 복구를 확인했다.
- 초기 운영 상한은 CPU `1`, 메모리 `256MB`, PID `128`로 확정했다. 유휴 메모리는 약 `33MB`, 최종 실제 전체 수집 후 메모리는 약 `73MB`였다.
- 최종 측정에서 cold-start 홈은 `89.553ms`, 수집 중 홈 p50/p95는 `4.361ms`/`35.321ms`, 준비 완료 후 홈 p50/p95는 `6.459ms`/`10.034ms`였다.
- 실제 자격 증명 값, 쿠키, 원본 JSON, 사용자 검색어가 로그와 HTML에 없음을 확인했다.
- 별도 실제 어댑터 스모크에서 CHZZK `5,818`건 중 태그 포함 `5,709`건을 `8,559ms`, SOOP `4,123`건 모두 태그 포함을 `5,185ms`에 수집했다.
- 실제 Kestrel과 Chromium을 사용하는 Playwright E2E를 전체 `dotnet test` 회귀에 포함했다.

운영 명령과 비밀값 주입, 상태 확인, 출시 검증 절차는 루트 `DEPLOYMENT.md`에 기록했다.

## 완료 조건.

- 단일 컨테이너가 영구 저장소 없이 재시작 후 자동 복구한다.
- 한 플랫폼 장애가 다른 플랫폼과 화면을 막지 않는다.
- 메모리 상한과 화면·검색 성능이 실제 컨테이너에서 검증됐다.
- 모든 자동 테스트, 시각 검수, 실제 API 스모크 테스트가 통과한다.
