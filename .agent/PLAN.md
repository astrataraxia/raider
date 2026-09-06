# Raider 제품 계획.

## 현재 상태.

Raider `v2.0.1`은 CHZZK과 SOOP의 현재 라이브 방송을 수집하고 검색 가능한 타일 화면, 서버 공용 즐겨찾기, 빠른 수동 새로고침과 전체 문서 reload 없는 부분 갱신을 제공한다. 한쪽 플랫폼 인증 설정이 없어도 프로세스는 시작되고, 키가 없는 플랫폼만 Configuration 오류로 화면에 표시한다. 브라우저 Console에서 플랫폼별 수집 결과와 소요 시간을 진단할 수 있다. SOOP 수집은 SOOP Developers 공식 API를 사용한다. 현재 릴리스 구현과 출시 검증은 완료됐다. 완료 범위와 검증 결과는 [RELEASES.md](../RELEASES.md)에 보관한다.

현재 진행 중인 Goal은 없다. 후속 작업은 사용자 가치나 운영 문제로 필요성이 확인된 항목만 별도 계획 문서로 만든다.

## 제품 계약.

- CHZZK과 SOOP의 현재 라이브 방송만 수집한다.
- 과거 방송, 시청 기록, 사용자 계정 데이터는 저장하지 않는다.
- 서버 공용 방송인 즐겨찾기만 SQLite에 영구 저장한다.
- 화면 요청은 외부 플랫폼을 호출하지 않고 현재 메모리 스냅샷만 읽는다.
- 플랫폼, 방송인 이름, 제목, 태그로 검색하고 필터링한다.
- 방송 타일은 플랫폼, 방송인 이름, 시청자 수, 제목, 태그, 썸네일을 표시하고 원본 방송 페이지로 이동한다.
- 홈 화면의 스냅샷 갱신 시각은 서버 또는 컨테이너 로컬 타임존과 무관하게 `Asia/Seoul` 기준으로 표시한다.
- 단일 ASP.NET Core 앱 컨테이너와 일회성 데이터 권한 초기화 서비스로 운영한다.
- 모든 접속 기기는 사용자 구분 없이 하나의 공용 즐겨찾기 목록을 사용한다.
- 즐겨찾기 SQLite 파일만 서버 호스트 bind mount에 영구 저장한다.

## 기술 계약.

- .NET 10, ASP.NET Core, Razor Pages.
- `BackgroundService`, `IHttpClientFactory`, `System.Text.Json`.
- 불변 메모리 스냅샷과 원자 교체.
- 직접 작성한 CSS와 서버 렌더링 HTML.
- xUnit과 Playwright for .NET.

구현과 운영 규칙은 [ENGINEERING.md](ENGINEERING.md), 화면 계약은 [DESIGN.md](DESIGN.md), 배포 절차는 [DEPLOYMENT.md](DEPLOYMENT.md)를 따른다.

## 운영 계약.

- CHZZK은 공식 Open API를 사용한다.
- SOOP은 SOOP Developers 공식 `broad/list`와 `broad/category/list` API를 사용한다. Client ID를 운영 설정으로 주입하며 계약 변경은 응답 계약 오류로 드러낸다.
- SOOP의 `total_view_cnt`는 현재 시청자 수로 공통 모델에 매핑하고, 기존 `broad_no` 기반 `channel_id`는 유지한다.
- 플랫폼별 폴링 주기는 10분이다.
- HTTP 요청은 전체 수집 취소 토큰으로만 제한하고, 전체 수집 timeout은 CHZZK과 SOOP 모두 180초다.
- 한 플랫폼 실패는 다른 플랫폼 수집과 화면 응답을 막지 않는다.
- 플랫폼 인증 설정이 없으면 해당 플랫폼만 Configuration 오류로 표시하고 프로세스 시작과 다른 플랫폼 수집을 막지 않는다.
- 실패한 플랫폼은 마지막 정상 목록을 유지하며 외부 장애를 정상 빈 목록으로 처리하지 않는다.
- 초기 컨테이너 상한은 CPU 1, 메모리 256MB, PID 128이다.

## 후속 작업 원칙.

1. 관찰된 문제나 명확한 사용자 가치가 있는 작업만 시작한다.
2. 비범한 변경은 `plans/`에 활성 계획 문서 하나를 만들고 WIP를 1로 제한한다.
3. 제품 범위나 계약 변경은 구현 전에 이 문서 또는 관련 계약 문서를 수정한다.
4. 완료된 계획은 핵심 결정과 검증 결과만 `RELEASES.md`에 압축하고 활성 계획 문서를 제거한다.
