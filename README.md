# TayoPinball

타요의 종겜핀볼은 SOOP 라이브 채팅에서 종합 게임 후보를 수집하고, 핀볼 추첨에 사용할 목록을 관리하는 Windows 데스크톱 프로그램입니다.

## 버전

이 저장소에는 두 가지 Tayo 버전이 들어 있습니다.

| 프로젝트 | 배포 파일 | 설명 |
|---|---|---|
| `TayoPinball` | `타요의 종겜핀볼.exe` | 현재 안정 버전입니다. 핀볼 사이트를 열고, 긴 목록은 클립보드 복사 방식으로 안내합니다. |
| `TayoPinballAuto` | `타요의 종겜핀볼(자동).exe` | 목록이 있으면 Chrome을 우선으로 사용해 핀볼 사이트 입력칸에 자동 반영을 시도하고, Chrome이 없을 때만 Edge를 fallback으로 사용합니다. |

## 주요 기능

- SOOP 라이브 채팅 연결
- 별풍선 또는 채팅 조건에 맞는 게임 후보 수집
- 닉네임/채팅 내용 기준 핀볼 목록 생성
- 수집 목록 검색, 복사, 저장
- 핀볼 사이트 열기
- 자동 버전의 핀볼 사이트 입력 자동 반영

## 빌드 환경

- Windows
- Visual Studio 2022 또는 Visual Studio Build Tools
- .NET Framework 4.8 Targeting Pack
- MSBuild

## 빌드 방법

Visual Studio에서 `TayoPinball.sln`을 열고 `Release` 구성으로 빌드합니다.

명령줄에서는 아래처럼 번호 붙은 Release 빌드를 만들 수 있습니다.

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\tools\Build-Release.ps1" -Project All -Configuration Release
```

특정 버전만 빌드하려면 `-Project TayoPinball` 또는 `-Project TayoPinballAuto`를 사용합니다.

빌드 결과는 아래 경로에 생성됩니다.

```text
dist\Release\TayoPinball\TayoPinball-001.exe
dist\Release\TayoPinballAuto\TayoPinballAuto-001.exe
```

## 프로젝트 구조

```text
TayoPinball/
TayoPinballAuto/
tools/
TayoPinball.sln
```

## 배포 파일

저장소 루트의 배포용 실행 파일은 아래와 같습니다.

| 파일 | SHA256 |
|---|---|
| `타요의 종겜핀볼.exe` | `E34DBC96B801EB71846F335D29BBDAD912D48EA39817D6ABF0B1BF46FE2646CB` |
| `타요의 종겜핀볼(자동).exe` | `ED7333E044440BC481E4E9CABFE896BE0301F98BD1F8C8C8F7FD207A378C6F9B` |

## 버전 정보

- Company: Gyeona
- Version: 1.0.0.0
- Target framework: .NET Framework 4.8

## 보안 메모

`TayoPinballAuto`는 목록이 있을 때 URL 길이와 상관없이 Chrome 우선 자동 반영을 시도합니다. 32비트 실행 환경에서도 64비트 Chrome 설치 경로를 먼저 확인하며, Chrome이 설치되어 있으면 Edge fallback을 사용하지 않습니다. 이 과정에서 브라우저의 원격 디버깅 포트와 DevTools WebSocket을 사용하므로 일부 보안 엔진이나 VirusTotal ML 판정에서 더 민감하게 보일 수 있습니다.
