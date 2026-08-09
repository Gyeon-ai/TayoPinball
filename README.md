# TayoPinball

타요의 종겜핀볼은 SOOP 라이브 채팅에서 핀볼 추첨에 사용할 목록을 수집하고 관리하는 Windows Forms 프로그램입니다.

## Versions

| Project | Release file | Description |
|---|---|---|
| `TayoPinball` | `타요의 종겜핀볼.exe` | 현재 버전입니다. 기존 UI와 동작을 유지합니다. |
| `TayoPinballAuto` | `타요의 종겜핀볼(자동).exe` | 목록이 있으면 Chrome 우선으로 핀볼 사이트를 열고 입력칸 자동 반영을 시도합니다. |

## Features

- SOOP 라이브 채팅 연결
- 기준 별풍선 이상 후원자의 다음 채팅 1회 수집
- 도전미션 후원 패킷(`CHALLENGE_GIFT`) 수집 지원
- 애드벌룬 후원 패킷(`serviceCommand == 87`) 수집 지원
- 닉네임 또는 채팅 내용 기준 핀볼 목록 생성
- 수집 목록 검색, 복사, 저장
- 핀볼 사이트 열기
- 자동 버전의 Chrome 우선 핀볼 사이트 입력 반영

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
| `타요의 종겜핀볼.exe` | `D62655FCA9D77BE15BD68C1481148DE0AD51532A35D5EA81B20D89E6B9DF65CB` |
| `타요의 종겜핀볼(자동).exe` | `677B7DDE4AD5F8C478A357B6069E4BA2F2FB2CB71123C3AFC071D455DFDAE436` |

## Version Info

- Company: Gyeona
- Version: 1.0.0.0
- Target framework: .NET Framework 4.8

## Security Note

`TayoPinballAuto`는 Chrome DevTools WebSocket을 사용해 핀볼 사이트 입력칸에 목록을 자동 반영합니다. 이 동작은 일부 보안 엔진이나 VirusTotal ML 판정에서 민감하게 보일 수 있습니다.
