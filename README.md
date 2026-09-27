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

## Update Release

최초 한 번 업데이트 서명키를 생성합니다. 개인키는 Git 저장소가 아닌 현재 Windows 사용자 계정의 DPAPI로 보호된 상태로 저장되고, 공개키만 저장소와 프로그램에 포함됩니다.

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\tools\New-UpdateSigningKey.ps1"
```

개인키 기본 경로는 `%LOCALAPPDATA%\Gyeona\TayoPinball\Signing\update-signing-key.dat`입니다. 이 파일을 잃으면 이미 배포된 프로그램에 새 자동 업데이트를 제공할 수 없으므로 안전한 별도 위치에 백업해야 합니다.

다른 개발 컴퓨터에서도 같은 키로 배포하려면 개발자 전용 스크립트로 비밀번호가 설정된 휴대용 `.tayokey` 백업을 만듭니다. 이 백업은 특정 Windows 계정에 묶이지 않으며 비밀번호는 명령줄에 적지 않고 프롬프트에 직접 입력합니다.

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\tools\Export-UpdateSigningKey.ps1" -OutputPath "E:\TayoPinball-UpdateKey.tayokey"
```

새 개발 컴퓨터에서는 저장소를 받은 뒤 같은 `.tayokey` 파일을 가져옵니다. 스크립트는 백업의 공개키가 저장소의 `update-public-key.xml`과 정확히 일치할 때만 새 Windows 사용자 계정의 DPAPI로 개인키를 다시 보호합니다.

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\tools\Import-UpdateSigningKey.ps1" -InputPath "E:\TayoPinball-UpdateKey.tayokey"
```

`.tayokey` 파일과 비밀번호는 서로 다른 위치에 보관하며 백업 파일을 GitHub에 커밋하지 않습니다. 백업은 PBKDF2-HMAC-SHA256으로 키를 만들고 AES-256-CBC로 암호화한 뒤 HMAC-SHA256으로 변조를 검출합니다. 이 스크립트들은 개발자용 도구이며 방송 사용자에게 배포되는 EXE에는 포함되지 않습니다.

자동 업데이트용 배포를 준비할 때는 다음 명령을 사용합니다.

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\tools\Prepare-UpdateRelease.ps1" -ReleaseNotes "변경 내용"
```

이 스크립트는 일반판과 Auto의 네 번째 버전 숫자를 하나 올리고, 두 프로젝트를 Release로 빌드한 뒤 아래 항목을 함께 갱신합니다. 네 번째 숫자는 `0~9`를 사용하며 `1.4.4.9` 다음 버전은 `1.4.5.0`입니다.

아직 배포하지 않은 버전을 키 교체 등의 이유로 다시 빌드·서명할 때만 `-RebuildCurrentVersion`을 사용합니다. 이미 배포한 버전에 이 옵션을 사용하면 실행 중인 프로그램이 새 파일을 업데이트로 인식하지 못하므로 사용하지 않습니다.

- `타요의 종겜핀볼.exe`
- `타요의 종겜핀볼(자동).exe`
- `update.json`의 버전, 파일 크기, SHA256
- `update.json.sig`의 RSA-SHA256 디지털 서명
- README의 배포 파일 SHA256과 버전

빌드나 파일 갱신에 실패하면 기존 버전과 배포 파일을 복구합니다. 스크립트는 Git 커밋이나 푸시를 실행하지 않으므로 결과를 검증한 후 별도로 배포해야 합니다.

업데이트 기능이 포함된 버전을 사용자가 한 번 직접 설치하면 이후 실행부터 GitHub의 `update.json`을 백그라운드에서 확인합니다. 앱에 내장된 공개키로 매니페스트 서명을 먼저 확인하므로 GitHub의 EXE와 SHA256이 함께 변조돼도 업데이트를 거부합니다. 새 버전이 있으면 `업데이트 / 나중에` 창을 표시하며, 다운로드한 EXE의 제품명, 버전, 크기, SHA256을 모두 확인한 뒤 기존 파일을 교체합니다.

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
| `타요의 종겜핀볼.exe` | `A4C8D461259A47FFFB1B2C7039F70059D0818F340ABB0A0DEFBBF7CBB574E3C3` |
| `타요의 종겜핀볼(자동).exe` | `010AACA6D10E5E64A136F5F664AE4E5879EFA840AB824318280E5419A9931C59` |

## Version Info

- Company: Gyeona
- Version: 1.4.4.3
- Target framework: .NET Framework 4.8

## Security Note

`TayoPinballAuto`는 Chrome DevTools WebSocket을 사용해 핀볼 사이트 입력칸에 목록을 자동 반영합니다. 이 동작은 일부 보안 엔진이나 VirusTotal ML 판정에서 민감하게 보일 수 있습니다.
