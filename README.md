# TayoPinball

타요의 종겜핀볼은 SOOP 라이브 채팅에서 핀볼 추첨에 사용할 목록을 수집하고 관리하는 Windows Forms 프로그램입니다.

**바로 다운로드:** [타요의 종겜핀볼(자동)](https://github.com/Gyeon-ai/TayoPinball/raw/refs/heads/main/%ED%83%80%EC%9A%94%EC%9D%98%20%EC%A2%85%EA%B2%9C%ED%95%80%EB%B3%BC%28%EC%9E%90%EB%8F%99%29.exe) / [타요의 종겜핀볼](https://github.com/Gyeon-ai/TayoPinball/raw/refs/heads/main/%ED%83%80%EC%9A%94%EC%9D%98%20%EC%A2%85%EA%B2%9C%ED%95%80%EB%B3%BC.exe)

## Versions

| Project | Release file | Description |
|---|---|---|
| `TayoPinball` | `타요의 종겜핀볼.exe` | 새 수집 UI를 사용하며 핀볼 사이트를 일반 방식으로 엽니다. |
| `TayoPinballAuto` | `타요의 종겜핀볼(자동).exe` | 목록이 있으면 Chrome 우선으로 핀볼 사이트를 열고 입력칸 자동 반영을 시도합니다. |

## Features

- SOOP 라이브 채팅 연결
- 방송국 주소, 플레이 주소, 순수 SOOP ID 입력 지원
- 기준 별풍선 이상 후원자의 다음 채팅 1회 수집
- 도전미션 후원 패킷(`CHALLENGE_GIFT`) 수집 지원
- 애드벌룬 후원 패킷(`serviceCommand == 87`) 수집 지원
- 별풍선, 애드벌룬, 도전미션 수집 대상 선택
- 정확히 N개 또는 N개 이상 수집 조건 선택
- 닉네임 또는 채팅 내용 기준 핀볼 목록 생성
- 닉네임 기준에서는 조건을 충족한 후원을 채팅 대기 없이 즉시 반영
- 방송 변경, 반영 방식 변경, 전체 삭제 시 이전 대기 후원 정리
- 수집 목록 검색, 복사, 저장
- 신규 후원의 핀볼 문자열과 목록 행을 실시간 증분 갱신
- 창 높이와 너비에 맞춘 반응형 수집 목록
- [타요 핀볼](https://gyeon-ai.github.io/TayoPinball-Web/) 사이트 열기
- 자동 버전의 Chrome 우선 핀볼 사이트 입력 반영
- 일반판과 자동판에 동일하게 적용된 수집 목록·핀볼 콘솔 UI

## Build Requirements

- Windows
- Visual Studio 2022 또는 Visual Studio Build Tools
- .NET Framework 4.8 Targeting Pack
- MSBuild

## Build

Visual Studio에서 `TayoPinball.sln`을 열고 `Release` 구성으로 빌드할 수 있습니다.

명령줄에서는 다음처럼 번호가 붙은 Release 빌드를 만들 수 있습니다.

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\tools\Build-Release.ps1" -Project All -Configuration Release
```

특정 버전만 빌드하려면 `-Project TayoPinball` 또는 `-Project TayoPinballAuto`를 사용합니다.

빌드 결과는 아래 경로에 생성됩니다.

```text
dist\Release\TayoPinball\TayoPinball-001.exe
dist\Release\TayoPinballAuto\TayoPinballAuto-001.exe
```

## Repository Layout

```text
TayoPinball/
TayoPinballAuto/
tools/
TayoPinball.sln
```

## Release Files

| File | SHA256 |
|---|---|
| `타요의 종겜핀볼.exe` | `4EC8FF42C0E7F0632A0B85B8D9C4D8C4C44862EEFEB4989D33B4ED258C9BBD06` |
| `타요의 종겜핀볼(자동).exe` | `1FE991C784F816531545CD17EAD0470EAB2A085BCEBCF073BE1D0964646895D0` |

## Version Info

- Company: Gyeona
- Version: 1.4.4.2
- Target framework: .NET Framework 4.8

## Security Note

`TayoPinballAuto`는 Chrome DevTools WebSocket을 사용해 핀볼 사이트 입력칸에 목록을 자동 반영합니다. 이 동작은 일부 보안 엔진이나 VirusTotal ML 판정에서 민감하게 보일 수 있습니다.
