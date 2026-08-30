# Raider 단일 앱 컨테이너 배포.

현재 릴리스 버전은 `v2.0.0`이다.

## 권장 환경.

- Linux `amd64` 또는 호환 컨테이너 호스트.
- Docker Engine `29.5.2` 이상.
- Docker Compose v2.
- 초기 자원 상한은 CPU `1`, 메모리 `256MB`, PID `128`이다.
- 공용 즐겨찾기 SQLite를 위한 서버 호스트 데이터 디렉터리를 사용한다.

실제 CHZZK·SOOP 전체 수집과 홈 부하를 동시에 실행한 최종 결과 메모리는 약 `73MB`였고, 수집 중 홈 p95는 약 `35ms`였다. 최초 cold-start 홈 요청은 약 `90ms`였다.

## Docker Compose 배포.

다른 컴퓨터에서는 다음 파일만 준비한다.

- `docker-compose.yml`.
- `.env.example`을 복사해 실제 값을 입력한 `.env`.
- 공용 즐겨찾기 SQLite를 저장할 서버 호스트 데이터 디렉터리.

`.env`의 `RAIDER_IMAGE`에는 레지스트리에 게시한 이미지 주소를 입력한다.

```text
RAIDER_IMAGE=ghcr.io/astrataraxia/raider:2.0.0
RAIDER_BIND_ADDRESS=127.0.0.1
RAIDER_PORT=8080
RAIDER_DATA_PATH=./data
RAIDER__CHZZK__CLIENTID=실제-client-id
RAIDER__CHZZK__CLIENTSECRET=실제-client-secret
```

실행과 업데이트.

```text
docker compose pull
docker compose up -d
docker compose ps
```

종료.

```text
docker compose down
```

`RAIDER_BIND_ADDRESS=127.0.0.1`은 로컬 호스트에서만 접근 가능하다. 외부 네트워크에 직접 공개해야 할 때만 `0.0.0.0`으로 변경한다.

## GHCR 이미지 게시.

`.github/workflows/publish-container.yml`은 `v2.0.0` 형태의 Git 태그가 GitHub에 push되면 실행된다.

1. 전체 테스트, 포맷 검사, 경고 오류 빌드를 실행한다.
2. 검증이 통과하면 저장소의 `GITHUB_TOKEN`으로 GHCR에 로그인한다.
3. 예를 들어 `v2.0.0`이면 `ghcr.io/astrataraxia/raider:2.0.0`, `:2.0`, `:latest` 태그를 게시한다.

별도 Personal Access Token을 GitHub Actions Secret에 저장하지 않는다. 워크플로의 `packages: write` 권한만 사용한다.

GHCR 이미지를 수동으로 먼저 게시했다면 GitHub package의 `Package settings`에서 저장소 `astrataraxia/raider`를 연결하고 Actions 접근 권한을 부여한다. 이 연결이 없으면 워크플로의 `GITHUB_TOKEN`이 기존 Private package를 갱신하지 못할 수 있다.

릴리스 게시 명령.

```text
VERSION=2.0.0
git tag -a "v$VERSION" -m "Raider v$VERSION"
git push origin "v$VERSION"
```

첫 게시 후 GitHub의 package 설정에서 이미지 공개 범위가 Private인지 확인한다. 다른 컴퓨터에서 Private 이미지를 받을 때만 `read:packages` 권한의 토큰으로 `docker login ghcr.io`를 수행한다.

## 로컬 이미지 빌드.

```text
docker compose -f docker-compose.yml -f docker-compose.build.yml up -d --build
```

이미지는 .NET 10 SDK 빌드 단계와 ASP.NET Core runtime 단계를 분리하고, runtime에서 비루트 `app` 사용자로 실행한다.

## 비밀값 주입.

저장소 파일과 이미지에는 비밀값을 넣지 않는다. 운영 환경의 secret manager 또는 서비스 관리자가 다음 환경 변수를 컨테이너에 주입해야 한다.

```text
RAIDER__CHZZK__CLIENTID
RAIDER__CHZZK__CLIENTSECRET
RAIDER__SOOP__CLIENTID
```

SOOP은 SOOP Developers 공식 API를 사용하며 `RAIDER__SOOP__CLIENTID`가 필요하다.

## 시간 표시.

컨테이너와 호스트 운영 타임존은 별도로 강제하지 않는다. 앱은 수집 시각을 `DateTimeOffset`으로 보관하고, 홈 화면의 마지막 갱신 시각만 서버 로컬 타임존과 무관하게 `Asia/Seoul` 기준으로 표시한다.

## 실행 계약.

운영 실행은 다음 제한을 적용한다.

```text
--read-only
--tmpfs /tmp:rw,noexec,nosuid,size=64m
--memory 256m
--cpus 1
--pids-limit 128
--security-opt no-new-privileges
--publish 127.0.0.1:8080:8080
```

외부 공개가 필요하면 호스트의 reverse proxy가 `127.0.0.1:8080`으로 전달한다. 즐겨찾기 DB를 위해 서버 호스트의 `RAIDER_DATA_PATH`만 컨테이너 `/data`에 bind mount하며 나머지 루트 파일시스템은 읽기 전용을 유지한다.

## Traefik 배포.

Traefik과 Raider가 동일한 외부 Docker 네트워크를 사용하면 Raider의 호스트 `ports` 설정은 생략할 수 있다. 앱 서비스에는 다음 설정을 유지한다.

- 외부 proxy 네트워크 연결과 `traefik.http.services.raider.loadbalancer.server.port=8080` 라벨.
- `${RAIDER_DATA_PATH}:/data` 볼륨.
- `RAIDER__FAVORITES__DATABASEPATH=/data/raider.db` 환경 변수.
- `raider-data-init` 서비스와 `service_completed_successfully` 의존성.

`RAIDER_DATA_PATH`는 호스트 볼륨 경로를 지정하는 Compose 변수이며 앱 환경 변수가 아니다. 앱 컨테이너에 `RAIDER_DATA_PATH`를 전달하는 것으로 `RAIDER__FAVORITES__DATABASEPATH`를 대체할 수 없다.

Traefik용 앱 서비스의 핵심 차이는 다음과 같다.

```yaml
services:
  raider:
    depends_on:
      raider-data-init:
        condition: service_completed_successfully
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      RAIDER__FAVORITES__DATABASEPATH: /data/raider.db
      RAIDER__CHZZK__CLIENTID: ${RAIDER__CHZZK__CLIENTID}
      RAIDER__CHZZK__CLIENTSECRET: ${RAIDER__CHZZK__CLIENTSECRET}
    networks:
      - proxy
    volumes:
      - "${RAIDER_DATA_PATH}:/data"
    labels:
      traefik.enable: "true"
      traefik.http.services.raider.loadbalancer.server.port: "8080"

networks:
  proxy:
    external: true
```

`raider-data-init` 서비스 정의는 저장소의 `docker-compose.yml`과 동일하게 유지하며 Traefik 네트워크에는 연결하지 않는다.

## 즐겨찾기 데이터.

- 기본 서버 경로는 Compose 파일 기준 `./data`다.
- 운영에서는 백업 위치가 명확한 절대 경로를 `RAIDER_DATA_PATH`로 지정할 수 있다.
- Compose의 일회성 `raider-data-init` 서비스가 앱 시작 전에 데이터 디렉터리와 기존 DB 파일의 쓰기 권한을 자동으로 맞춘다.
- 실행 중 단순 파일 복사 대신 SQLite 일관성이 보장되는 백업 절차를 사용한다.
- 외부 공개 시 reverse proxy 인증을 적용하고 Raider 직접 포트 접근을 차단한다.

가장 단순한 일관성 백업은 짧게 컨테이너를 중지한 뒤 DB 파일을 복사하는 것이다.

```text
docker compose stop raider
cp "$RAIDER_DATA_PATH/raider.db" "/secure/backup/raider-$(date +%Y%m%d).db"
docker compose start raider
```

복구 시 컨테이너를 중지하고 백업 DB를 `RAIDER_DATA_PATH/raider.db`로 되돌린 뒤 Compose를 시작하면 권한 초기화 서비스가 파일 권한을 자동으로 맞춘다.

## 상태 확인.

- `/health/live`: 프로세스가 HTTP 요청을 처리할 수 있는지 확인한다.
- `/health/ready`: CHZZK과 SOOP의 첫 수집 시도가 모두 완료됐는지 확인한다.
- 이미지 healthcheck는 `/health/live`를 사용한다.

## 출시 검증.

Windows에서는 WSL2 Ubuntu Docker Engine에서 다음을 실행한다.

```text
bash scripts/test_container_image.sh raider:local
bash scripts/test_container_release.sh raider:local /secure/path/raider-container.env
```

`test_container_release.sh`의 env 파일은 검증 중에만 사용하고 즉시 제거한다. 스크립트는 비루트, 읽기 전용 루트, 즐겨찾기 데이터 bind mount, 상태 전환, 정상 종료, 재생성 복구, 자원 제한, 실제 API, 성능, 로그와 HTML의 비밀값 비노출을 검증한다.
