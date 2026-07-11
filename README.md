# Raider.

CHZZK과 SOOP의 현재 라이브 방송을 한 화면에서 검색하고 탐색하는 개인용 스트리밍 레이더다.

## 현재 릴리스.

- 버전: `v1.3.8`.
- 런타임: .NET 10과 ASP.NET Core Razor Pages.
- 저장 방식: 라이브는 불변 메모리 스냅샷, 공용 즐겨찾기는 서버 호스트 SQLite.
- 배포 방식: 단일 앱 컨테이너와 일회성 데이터 권한 초기화 서비스.
- 시간 표시: 홈 화면의 스냅샷 갱신 시각은 서버 타임존과 무관하게 `Asia/Seoul` 기준으로 표시.
- 새로고침 방식: 앱 내부 새로고침과 수집 완료 반영은 전체 문서 reload 없이 라이브 영역만 부분 갱신한다.
- 수집 진단: 브라우저 개발자 도구 Console에서 플랫폼별 성공 또는 실패, 소요 시간, 안전한 오류 종류를 확인할 수 있다.

## 시작하기.

로컬 CHZZK 인증정보는 .NET User Secrets에 저장한다.

```text
dotnet user-secrets set "Raider:Chzzk:ClientId" "<client-id>" --project src/Raider.Web
dotnet user-secrets set "Raider:Chzzk:ClientSecret" "<client-secret>" --project src/Raider.Web
dotnet run --project src/Raider.Web
```

앱 실행 후 `http://localhost:5094` 또는 실행 로그에 표시된 주소를 연다. SOOP은 별도 인증정보 없이 공개 웹 JSON을 사용한다.

## Docker Compose 실행.

Docker Compose v2를 사용한다. 이미지가 레지스트리에 게시된 후 다른 컴퓨터에는 `docker-compose.yml`과 `.env`만 있으면 된다. `.env.example`을 `.env`로 복사하고 이미지 주소와 CHZZK 인증정보를 입력한다.

```text
docker compose pull
docker compose up -d
docker compose ps
```

기본 접속 주소는 `http://localhost:8080`이다. 같은 저장소에서 이미지를 직접 빌드해 실행할 때는 다음 override를 사용한다.

모든 접속 기기는 하나의 공용 즐겨찾기 목록을 사용한다. SQLite 파일은 `RAIDER_DATA_PATH`로 지정한 서버 호스트 디렉터리에 저장되며 컨테이너 재생성과 이미지 업데이트 뒤에도 유지된다. Compose는 앱 시작 전에 해당 디렉터리의 쓰기 권한을 자동으로 준비한다.

```text
docker compose -f docker-compose.yml -f docker-compose.build.yml up -d --build
```

Traefik을 사용하는 운영 환경에서는 앱을 외부 proxy 네트워크에 연결하고 호스트 포트 공개를 생략할 수 있다. 이 경우에도 `raider-data-init`, `/data` 볼륨, `RAIDER__FAVORITES__DATABASEPATH=/data/raider.db` 설정은 유지해야 한다.

`v1.3.8` 형태의 Git 태그를 push하면 GitHub Actions가 테스트 후 Private GHCR 이미지를 자동 게시한다. 자세한 일반 Compose와 Traefik 배포 절차는 [DEPLOYMENT.md](.agent/DEPLOYMENT.md)를 따른다.

## 검증.

```text
dotnet test
dotnet format --verify-no-changes
dotnet build --no-restore -warnaserror
```

## 문서.

| 문서 | 역할 |
| --- | --- |
| [PLAN.md](.agent/PLAN.md) | 현재 제품 범위, 기술과 운영 계약, 후속 작업 원칙. |
| [ENGINEERING.md](.agent/ENGINEERING.md) | 아키텍처, 코드, 테스트, 보안 규칙. |
| [DESIGN.md](.agent/DESIGN.md) | 현재 화면의 디자인 계약. |
| [DEPLOYMENT.md](.agent/DEPLOYMENT.md) | 일반 Compose와 Traefik 배포 및 운영 절차. |
| [RELEASES.md](RELEASES.md) | 완료된 릴리스 범위, 결정, 검증 결과, 알려진 위험. |
| [AGENTS.md](.agent/AGENTS.md) | 저장소에서 작업하는 AI 에이전트 지침. |
