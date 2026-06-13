# Raider.

CHZZK과 SOOP의 현재 라이브 방송을 한 화면에서 검색하고 탐색하는 개인용 스트리밍 레이더다.

## 현재 릴리스.

- 버전: `v1.0.0`.
- 런타임: .NET 10과 ASP.NET Core Razor Pages.
- 저장 방식: 영구 저장소 없는 불변 메모리 스냅샷.
- 배포 방식: 단일 Docker 컨테이너.

## 시작하기.

로컬 CHZZK 인증정보는 .NET User Secrets에 저장한다.

```text
dotnet user-secrets set "Raider:Chzzk:ClientId" "<client-id>" --project src/Raider.Web
dotnet user-secrets set "Raider:Chzzk:ClientSecret" "<client-secret>" --project src/Raider.Web
dotnet run --project src/Raider.Web
```

앱 실행 후 `http://localhost:5094` 또는 실행 로그에 표시된 주소를 연다. SOOP은 별도 인증정보 없이 공개 웹 JSON을 사용한다.

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
| [DEPLOYMENT.md](.agent/DEPLOYMENT.md) | 단일 컨테이너 배포와 운영 절차. |
| [RELEASES.md](RELEASES.md) | 완료된 릴리스 범위, 결정, 검증 결과, 알려진 위험. |
| [AGENTS.md](.agent/AGENTS.md) | 저장소에서 작업하는 AI 에이전트 지침. |
