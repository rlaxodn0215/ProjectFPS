# GoogleSpeadSheetReader 사용 안내

Google Sheets의 표를 읽어 **행마다 Unity ScriptableObject(SO) 자산을 만들고, 그 참조를 Catalog에 모으는 도구**입니다.
데이터를 가져오는 작업은 Unity Editor에서 실행합니다. 게임 실행 중에는 미리 생성한 데이터 SO와 Catalog를 사용합니다.

처음 사용하는 경우 **시트 작성 → GSTU 연결 → Reader 설정 → 코드 생성 → 데이터 가져오기** 순서로 읽고 따라가세요.
API Key 또는 OAuth 인증 정보가 준비되어 있다고 가정합니다. 인증 정보 발급은 아래 공식 문서를 참고하세요.

## 1. 도구 이해하기

### 용어

| 용어 | 뜻 | 이 문서의 예 |
| --- | --- | --- |
| Spreadsheet | Google Sheets 문서 전체 | Sound와 Prefab 탭이 들어 있는 문서 |
| worksheet | 문서 아래쪽의 개별 시트 탭 | `Sound`, `Prefab` |
| ScriptableObject(SO) | Unity에서 파일로 저장하고 다른 자산에서 참조할 수 있는 데이터 객체 | `Sound/1.asset` |
| 데이터 클래스 | 한 행에 어떤 필드가 있는지 정의하는 C# 코드 | `SoundData.cs` |
| Catalog SO | 같은 시트에서 만든 데이터 SO들의 참조 목록 | `SoundCatalog.asset` |
| Reader SO | 문서 ID, 인증 모드, 출력 경로와 가져오기 기록을 저장하는 설정 자산 | `GoogleSpeadSheetReader.asset` |
| Key | 같은 시트 안에서 한 행을 식별하는 정수 | `BGM_1` 행의 `1` |

예를 들어 Sound 시트의 `BGM_1` 행을 가져오면 다음과 같이 연결됩니다.

```text
Google Sheets 문서
└─ Sound worksheet
   ├─ 1행: 헤더 → SoundData.cs의 필드 정의
   └─ BGM_1 데이터 행, Key=1
      └─ SoundData SO: Sound/1.asset
         └─ SoundCatalog.asset의 참조 목록에 등록
```

도구 본체와 인스펙터는 `GoogleSpeadSheetReader.cs` 한 파일에 들어 있습니다.
시트별 데이터 클래스와 Catalog 클래스는 도구가 별도 `.cs` 파일로 생성합니다.
**코드 생성은 데이터 자산 생성과 다른 작업**이며, 새 코드가 컴파일된 후에 데이터를 가져와야 합니다.

로컬 `.xlsx` 파일을 직접 읽는 기능은 없습니다. `BingoRoulette.xlsx`를 사용하려면 그 내용을 Google Sheets 문서로 준비하세요.

## 2. Google Sheets 작성하기

### 2-1. 문서와 시트 구성

한 문서 안에 가져올 worksheet를 만듭니다. 첨부 예제는 `Sound`, `Prefab` 두 탭입니다.
각 시트의 **1행은 헤더**, **2행부터는 데이터**입니다. 표 위에 제목이나 설명 행을 추가하지 마세요.

**문서의 모든 worksheet가 대상입니다.** 가져올 탭을 골라 제외하는 기능은 없습니다.
헤더 없는 빈 탭이나 설명용 탭이 하나라도 있으면 전체 검증·코드 생성·가져오기가 실패합니다.
설명은 별도 문서에 두고, 이 문서의 각 탭에는 아래 데이터 형식을 적용하세요.

시트명은 생성될 클래스와 폴더 이름에도 사용됩니다. 처음에는 `Sound`, `Prefab`, `ItemData`처럼 영문자와 숫자로 구성하고,
숫자로 시작하지 않는 이름을 사용하면 편합니다. 공백·하이픈·괄호, C# 예약어, 기존 타입과 충돌하는 이름은 피하세요.
`Sound Data`, `1Sound`, `class`, `Editor` 같은 이름은 사용할 수 없습니다.

### 2-2. 헤더 작성 규칙

기본 형식은 **`필드명(타입)`**입니다. 필드명은 생성되는 C# 변수 이름이고, 타입은 셀 값을 어떻게 읽을지 정합니다.

| 헤더 | 생성되는 필드 | 필수 여부 |
| --- | --- | --- |
| `Name` 또는 `Name(string)` | 문자열 이름 | 필수 |
| `Key(int)` | 정수 식별자 | 필수 |
| `Loop(bool)` | 참/거짓 값 | 해당 필드가 필요할 때 추가 |
| `Volume(float)` | 소수 값 | 해당 필드가 필요할 때 추가 |
| `AssetPath(string)` | 경로 문자열 | 해당 필드가 필요할 때 추가 |

첫 열에 `Name`, 다음 열에 `Key(int)`를 두는 예제 배열을 권장합니다.
같은 시트의 필드 이름을 중복해서 사용하지 말고, **값이 있는 열에는 반드시 헤더를 작성**하세요.
표 중간에 헤더 없는 열을 끼워 넣으면 오류가 납니다. 헤더에서 타입만 바꾸거나 열 순서를 바꿔도 코드 갱신이 필요합니다.

필드명 역시 C# 식별자여야 합니다. `CoolTime(float)`은 가능하지만 `Cool Time(float)`이나 `class(string)`은 불가능합니다.
`name`처럼 ScriptableObject의 기존 멤버와 충돌하는 필드명도 사용할 수 없습니다. 이름 열은 대문자 `Name`을 사용하세요.

### 2-3. Sound 시트: 첨부 파일의 실제 예제

탭 이름을 `Sound`로 지정하고 아래 표의 헤더와 값을 각 셀에 입력합니다.
Markdown 표의 `|`와 구분선은 셀 내용이 아닙니다.

| Name | Key(int) | SoundType(ESoundType) | Loop(bool) | CoolTime(float) | Volume(float) | Pitch(float) | AssetPath(string) |
| --- | --- | --- | --- | --- | --- | --- | --- |
| BGM_1 | 1 | BGM | TRUE | 0 | 1 | 1 | Audio/BGM/BGM_1 |
| ButtonClick | 2 | UI | FALSE | 0 | 1 | 1 | Audio/UI/GenericButton14 |
| SlotClick | 3 | SFX | FALSE | 0 | 1 | 1 | Audio/SFX/GenericButton15 |
| DeathSlotClick | 4 | SFX | FALSE | 0 | 0.3 | 1 | Audio/SFX/cat_2_meow_02 |
| Bingo | 5 | SFX | FALSE | 0 | 1 | 1 | Audio/SFX/GenericButton13 |
| GameOver | 6 | SFX | FALSE | 0 | 1 | 1 | Audio/SFX/Popup1 |

이 표를 가져오려면 프로젝트에 **`ESoundType` enum이 먼저 정의되고 컴파일되어 있어야 합니다.**
위 값에 대응하는 `BGM`, `UI`, `SFX` 항목이 필요합니다. enum의 전체 항목과 숫자 값은 프로젝트에서 직접 정하세요.
도구가 시트 문자열을 보고 enum을 자동으로 만들거나 항목을 추가하지는 않습니다.

`CoolTime`, `Volume`, `Pitch`는 위 예제에서는 float 필드입니다.
이 도구는 타입 변환을 수행하며, 각 필드의 게임 내 의미·단위·허용 범위를 별도로 정하지 않습니다.

### 2-4. Prefab 시트: 첨부 파일의 실제 예제

탭 이름을 `Prefab`으로 지정합니다.

| Name | Key(int) | AssetPath(string) |
| --- | --- | --- |
| Slot | 1 | Prefab/UI/Slot |

`Sound`와 `Prefab`에서 각각 `Key=1`을 쓰는 것은 가능합니다. Key의 중복 검사는 **시트별**로 적용됩니다.
`AssetPath`는 문자열 그대로 저장됩니다. `AudioClip`이나 `GameObject` 참조를 자동 연결하거나 경로의 자산 존재 여부를 검사하지 않습니다.

### 2-5. 타입별 셀 입력 방법

아래 예제는 **셀 하나에 입력하는 내용**입니다. 일반 string 셀에는 JSON 따옴표를 붙이지 않습니다.

| 타입 | 헤더 예 | 올바른 셀 값 | 잘못된 셀 값 또는 주의점 |
| --- | --- | --- | --- |
| `string` | `Description(string)` | `버튼 클릭 소리`, 빈 셀 | `"버튼 클릭 소리"`로 쓰면 따옴표도 문자열에 포함됨 |
| `bool` | `Loop(bool)` | `TRUE`, `FALSE`, `true`, `false` | `1`, `0`, `예`, 빈 셀 |
| `int` | `Key(int)` | `1`, `0`, `-1` | `1.5`, `1,000`, `2147483648`, 빈 셀 |
| `long` | `Count(long)` | `123456789` | 소수, long 범위 초과, 빈 셀 |
| `float` | `Volume(float)` | `0.3`, `1`, `1e-2` | `0,3`, `30%`, `NaN`, `Infinity`, 빈 셀 |
| `double` | `Ratio(double)` | `0.125`, `2.5` | 숫자 외 문자, 무한대, 빈 셀 |
| 기존 enum | `SoundType(ESoundType)` | 정의된 항목 이름 `BGM` | `0`, 정의에 없는 이름, `bgm`처럼 대소문자가 다른 이름 |
| `List<int>` | `Keys(List<int>)` | `[1,2,3]`, `[]` | `1,2,3`, `["1","2"]`, 빈 셀 |
| `List<string>` | `Tags(List<string>)` | `["UI","Button"]`, `[]` | `[UI,Button]`, 작은따옴표, 빈 셀 |
| `List<bool>` | `Flags(List<bool>)` | `[true,false]` | `[TRUE,FALSE]`, `["true"]` |
| `List<float>` | `Weights(List<float>)` | `[0.3,1.0]` | `["0.3"]`, `[null]` |
| `List<enum>` | `Types(List<ESoundType>)` | `["BGM","SFX"]` | `[BGM,SFX]`, `[0,2]` |

`List<long>`와 `List<double>`도 같은 JSON 배열 형식을 사용합니다.
`List<T>`의 T는 위 기본 타입 또는 기존 enum이어야 합니다. `List<List<int>>`, 객체, 배열 타입, `DateTime` 등은 지원하지 않습니다.

숫자는 소수점 `.`을 사용하고, 셀의 표시 형식에도 주의하세요. GSTU에서 읽은 문자열을 변환하므로
천 단위 구분 기호나 `%`가 붙은 값은 위 파서 규칙에 맞지 않습니다. 큰 정수를 입력했다면 시트가 값을 반올림하지 않았는지도 확인하세요.

enum은 런타임에서 접근 가능한 `public` 타입이어야 하며 `Editor` 전용 코드에 두면 안 됩니다.
같은 이름의 enum이 여러 개라 모호하면 `SoundType(프로젝트네임스페이스.ESoundType)`처럼 전체 이름을 적습니다.

### 2-6. JSON 리스트 작성 예

리스트는 **여러 셀로 나누지 않고 한 셀 안에** 씁니다. 대괄호 `[]`가 리스트이고, 쉼표가 원소를 구분합니다.
문자열과 enum 원소는 큰따옴표 `"`로 감싸고 숫자와 bool 원소는 감싸지 않습니다.

```json
["UI","Button"]
```

문자열 안의 쉼표는 그대로 보존됩니다. 아래는 두 원소입니다.

```json
["A,B","C"]
```

문자열 안에 큰따옴표를 넣으려면 `\"`로 씁니다.

```json
["버튼 \"확인\"","취소"]
```

빈 리스트는 `[]`로 명시합니다. `[null]`, 마지막에 쉼표가 남은 `[1,2,]`, 중첩 배열 `[[1,2]]`는 오류입니다.
빈 문자열을 원소로 넣는 `List<string>` 값 `[""]`는 허용됩니다.

### 2-7. Name, Key와 빈 셀

- `Name`과 `Key`는 각 데이터 행에 반드시 입력합니다. 같은 시트 안에서 Name과 Key는 각각 고유해야 합니다.
- 가져온 뒤 이름만 바꾸려면 **Key를 유지하고 Name만 변경**합니다. Key를 바꾸면 다른 행으로 취급합니다.
- 행 순서를 바꿔도 기존 행의 Key를 다시 번호 매기지 마세요. Key가 같으면 같은 SO를 갱신합니다.
- 일반 string 필드는 빈 셀을 허용하지만 Name은 비어 있거나 공백뿐이면 안 됩니다.
- 숫자·bool·enum·list는 빈 셀을 허용하지 않습니다. 필요한 값이나 빈 리스트 `[]`를 명시하세요.
- 완전히 빈 데이터 행은 건너뜁니다. 일부 필드만 채워진 행은 검증 대상입니다.
- 헤더만 있고 데이터 행이 없는 시트는 유효합니다. 이미 가져온 시트라면 재가져올 때 기존 관리 행 SO가 모두 삭제될 수 있습니다.

## 3. Unity에서 처음 가져오기

### 3-1. 준비 확인

프로젝트에 GSTU(Google Sheets to Unity)가 설치되어 있어야 합니다. 이 프로젝트에는 이미 포함되어 있습니다.
Play Mode를 종료하고 Console의 컴파일 오류를 해결하세요. enum을 사용하는 시트라면 해당 타입의 컴파일도 완료해야 합니다.

이후 단계는 Google Sheets API를 사용할 인증 정보와 시트 접근 권한이 준비된 상태에서 진행합니다.
발급·인증 준비가 필요하면 Google 공식 [API Key 안내](https://developers.google.com/workspace/guides/create-credentials#api_key_credentials)와
[OAuth client ID 안내](https://developers.google.com/workspace/guides/create-credentials#oauth_client_id_credentials)를 참고하세요.
아래에서는 그 정보를 기존 GSTU 설정에 연결하는 방법을 설명합니다.

### 3-2. GSTU 연결 설정

Unity 상단 메뉴에서 **`Window > GSTU > Open Config`**를 엽니다.
공개 모드와 비공개 모드 중 사용할 모드에 해당하는 항목을 설정합니다.

| Reader에서 선택할 모드 | GSTU 설정 탭 | 준비할 내용 |
| --- | --- | --- |
| `Public` | `Public` | 공개 접근 가능한 시트와 API Key |
| `Private` | `Private` | 시트에 접근할 계정의 OAuth 인증 정보 |

**Public 모드**:

1. API Key로 익명 조회할 수 있도록 준비된 시트인지 확인합니다. API Key 자체가 비공개 문서 접근 권한을 부여하지는 않습니다.
2. GSTU 설정의 **Public** 탭을 선택합니다.
3. **API Key** 필드에 준비된 키를 입력합니다.

공개 데이터와 API Key의 관계는 [Google 공식 인증 안내](https://developers.google.com/workspace/guides/create-credentials#api_key_credentials)를 참고하세요.

**Private 모드**:

1. GSTU 설정의 **Private** 탭을 선택합니다. `Private (Legacy)` 탭은 이 안내에서 사용하지 않습니다.
2. 준비된 **Client ID**, **Client Secret Code**, **Port number**를 입력합니다.
3. **Build Connection**을 누릅니다.
4. 브라우저에서 대상 시트에 접근할 수 있는 Google 계정으로 로그인하고 인증을 완료합니다.
5. Unity로 돌아와 Reader에서 시트 검색을 실행해 연결을 확인합니다.

Port number는 준비한 OAuth 리디렉션 설정과 일치해야 합니다.
현재 GSTU가 사용하는 콜백 주소는 `http://127.0.0.1:<Port number>`입니다.
Reader에 API Key나 Client Secret을 따로 입력하는 칸은 없으며, 기존 `GSTU_Config` 설정을 사용합니다.

### 3-3. Reader SO 만들기

1. Unity **Project** 창에서 Reader를 보관할 폴더를 선택합니다.
2. 우클릭한 뒤 **`Create > Data > Google Spreadsheet Reader`**를 선택합니다.
3. 만들어진 `GoogleSpeadSheetReader.asset`을 선택해 Inspector를 엽니다.

Reader는 설정과 자산 소유 기록을 보관하는 파일입니다. 가져온 데이터를 계속 갱신할 때는 **같은 Reader SO를 사용**하세요.
새 Reader를 만들면 이전 Reader가 관리하던 파일을 이어받아 덮어쓰지 않습니다.

### 3-4. Spreadsheet ID 찾기

브라우저에서 Google Sheets 문서 주소를 확인합니다. 아래 주소는 형식을 보여주는 예제입니다.

```text
https://docs.google.com/spreadsheets/d/YOUR_SPREADSHEET_ID/edit#gid=0
```

Reader의 **Spreadsheet Id**에는 `/d/`와 `/edit` 사이의 ID만 넣습니다.
URL 전체나 뒤쪽의 `gid=0`을 넣지 마세요. `gid`는 개별 탭을 나타내며 문서 ID가 아닙니다.
`YOUR_SPREADSHEET_ID`는 예제 표시이므로 실제 문서 ID로 바꿔야 합니다.

### 3-5. 인스펙터 설정

괄호 안은 스크립트에서 사용하는 필드 이름입니다.

| 설정 | 입력할 내용 | 기본값 |
| --- | --- | --- |
| Spreadsheet Id (`spreadsheetId`) | Google Sheets 문서 ID | 비어 있음 |
| Access Mode (`accessMode`) | GSTU에서 준비한 인증 방식과 같은 `Public` 또는 `Private` | `Public` |
| Generated Code Folder (`generatedCodeFolder`) | 시트별 Data/Catalog `.cs` 파일 저장 위치 | `Assets/01_Scripts/Data/Generated` |
| Data Folder (`dataFolder`) | 행 SO가 저장될 루트 폴더 | `Assets/04_Data` |
| Catalog Folder (`catalogFolder`) | Catalog SO를 모아 저장할 폴더 | `Assets/04_Data/Catalogs` |
| Request Timeout Seconds (`requestTimeoutSeconds`) | 요청을 기다리는 제한 시간(초) | `30` |

처음에는 경로 기본값을 사용하세요. 출력 경로는 프로젝트의 `Assets` 내부여야 하고, `Editor` 또는 이름이 `~`로 끝나는 폴더는 사용할 수 없습니다.
Data Folder 바로 아래에는 도구가 시트명 폴더를 만들므로, 설정에 시트명을 덧붙일 필요가 없습니다.

### 3-6. 네 버튼을 순서대로 실행하기

| 순서 | 버튼 | 수행 작업 | 실행 후 확인할 것 |
| --- | --- | --- | --- |
| 1 | **시트 검색** | 문서의 전체 worksheet 이름과 크기를 조회 | `Worksheet / 생성 타입`에 Sound, Prefab과 생성 타입 이름이 표시되는지 |
| 2 | **전체 검증** | 전체 시트를 읽고 헤더·타입·각 행의 값을 검사 | 데이터 검증 성공 메시지 또는 고칠 셀의 오류 |
| 3 | **코드 생성 / 갱신** | 시트별 Data/Catalog 클래스 파일을 생성 또는 갱신 | Generated 폴더의 `.cs` 파일과 Unity 컴파일 완료 |
| 4 | **전체 데이터 가져오기** | 행 SO와 Catalog를 생성·갱신하고 삭제된 행을 반영 | 완료 행 수, Project 창의 `.asset`, `Catalog 참조` 목록 |

첫 사용에서는 **전체 검증**에 데이터 검증 성공과 함께 “코드 생성/갱신을 먼저 실행하세요”가 표시될 수 있습니다.
아직 생성 클래스가 없다는 뜻이므로, 코드 생성으로 넘어가면 됩니다.

**코드 생성 후 컴파일 완료를 기다린 다음 전체 데이터 가져오기를 누르세요.**
코드를 생성했다고 행 데이터까지 저장된 것은 아닙니다. 버튼은 Play Mode, 컴파일 중 또는 컴파일 오류가 있는 동안 비활성화됩니다.
작업 중에는 설정 변경과 다른 버튼 실행도 비활성화됩니다.

각 작업은 시트 목록을 다시 조회합니다. 전체 데이터 가져오기는 모든 시트를 다운로드하고 검증한 후에 데이터 자산을 변경합니다.
시트 하나라도 요청 실패·시간 초과·검증 오류가 나면 데이터 저장과 삭제를 진행하지 않습니다.

### 3-7. 생성 결과 확인

예제 두 시트와 기본 경로를 사용하면 다음 구조가 만들어집니다.

```text
Assets/
├─ 01_Scripts/Data/
│  ├─ GoogleSpeadSheetReader.cs
│  └─ Generated/
│     ├─ SoundData.cs
│     ├─ SoundCatalog.cs
│     ├─ PrefabData.cs
│     └─ PrefabCatalog.cs
└─ 04_Data/
   ├─ Sound/
   │  ├─ 1.asset  ← Name=BGM_1
   │  ├─ 2.asset  ← Name=ButtonClick
   │  └─ ... 6.asset까지
   ├─ Prefab/
   │  └─ 1.asset  ← Name=Slot
   └─ Catalogs/
      ├─ SoundCatalog.asset
      └─ PrefabCatalog.asset
```

행 SO 파일 이름은 **Key** 기준입니다. SO의 이름과 `Name` 필드에는 시트의 Name 값이 반영됩니다.
Sound는 데이터 SO 6개, Prefab은 1개가 생성되어야 합니다.
Catalog 자산을 선택하면 저장된 참조 목록을 확인할 수 있습니다. 목록 순서는 시트의 데이터 행 순서입니다.
생성 클래스의 네임스페이스는 `ProjectFPS`입니다.

## 4. 데이터 변경과 문제 해결

### 4-1. 변경 종류별 작업 순서

| 변경 | 실행 순서 | 결과 |
| --- | --- | --- |
| 셀 값 또는 Name만 수정 | 전체 검증 → 전체 데이터 가져오기 | 같은 Key의 기존 SO를 갱신 |
| 데이터 행 추가 | 새 고유 Key/Name 입력 → 전체 검증 → 전체 데이터 가져오기 | 새 SO 생성 후 Catalog 등록 |
| 데이터 행 삭제 | 시트에서 행 삭제 → 전체 검증 → 전체 데이터 가져오기 | 해당 관리 SO 삭제 후 Catalog에서 제외 |
| 행 순서 변경 | 기존 Key 유지 → 전체 데이터 가져오기 | SO의 식별자는 유지하고 Catalog 순서를 변경 |
| 열 추가·제거·필드명·타입·열 순서 변경 | 전체 검증 → 코드 생성 / 갱신 → 컴파일 완료 → 전체 데이터 가져오기 | 변경된 클래스 구조로 가져오기 |
| 새 worksheet 추가 | 필수 헤더와 데이터 작성 → 전체 검증 → 코드 생성 / 갱신 → 컴파일 완료 → 전체 데이터 가져오기 | 새 시트의 Data/Catalog 클래스와 자산 생성 |

예를 들어 `BGM_1`을 `MainBGM`으로 바꾸고 Key를 `1`로 유지하면 기존 `Sound/1.asset`을 갱신합니다.
이때 GUID(Unity가 자산 참조에 사용하는 식별자)를 유지하므로 다른 자산에서 연결한 참조도 유지됩니다.

Key를 `1`에서 `10`으로 바꾸면 새 Key의 SO를 생성하고, 시트에서 사라진 Key `1`의 관리 SO는 삭제합니다.
단순히 이름을 바꾸려는 경우에는 Key를 바꾸지 마세요.

### 4-2. 삭제와 관리 기록

**전체 데이터 가져오기는 시트에서 사라진 행의 SO 파일도 삭제합니다.**
삭제된 SO를 게임 오브젝트나 다른 자산이 참조하고 있었다면 그 참조는 유실됩니다.
필요한 행을 실수로 지우지 않았는지 가져오기 전에 확인하세요.

삭제 대상은 이 Reader가 해당 문서·worksheet·출력 위치에서 관리하던 행 SO입니다.
폴더 안의 파일을 전부 삭제하는 방식은 아니므로 도구가 관리하지 않는 파일은 삭제하지 않습니다.
worksheet 자체를 제거한 경우에는 그 시트의 기존 SO·Catalog·생성 코드를 자동 정리하지 않습니다.

Reader SO에는 각 Key와 자산 GUID 등의 관리 기록이 저장됩니다.
Reader를 삭제하거나 새로 만드는 대신 기존 Reader를 계속 사용하고,
생성된 행 SO의 Key·타입·위치와 Catalog를 임의로 변경하지 마세요. 관리 기록과 달라지면 오류로 중단됩니다.

### 4-3. 헤더와 출력 경로 변경

헤더를 바꾼 뒤 바로 가져오면 “헤더 변경 감지” 오류와 변경 전/후 스키마(필드 이름과 타입 목록)가 표시됩니다.
**코드 생성 / 갱신 → 컴파일 완료 → 전체 데이터 가져오기**로 반영하세요.
기존 `.cs` 파일의 `.meta`는 유지하지만, 제거되거나 타입이 바뀐 필드의 이전 값은 보존되지 않을 수 있습니다.
게임 코드에서 사용하는 필드명도 함께 확인해야 합니다.

출력 경로 설정은 기존 자산을 자동으로 옮기거나 지우지 않습니다.
Data Folder와 Catalog Folder를 새 위치로 바꾸면 기존 소유 경로와 겹치지 않는 위치를 사용해야 합니다.
예를 들어 Data Folder만 바꾸고 기존 Catalog Folder를 그대로 쓰면 기존 소유 경로 충돌로 중단될 수 있습니다.
새 위치에 생성한 데이터는 새 자산이므로 기존 게임 참조가 자동으로 새 위치의 SO를 가리키지는 않습니다.

Generated Code Folder를 바꾸려면 같은 클래스가 두 곳에 생기지 않도록 **기존 생성 코드를 `.meta`와 함께 수동 이동**한 후
경로를 설정하고 컴파일을 완료하세요. 도구가 생성하지 않은 코드나 관리하지 않는 SO를 덮어쓰지는 않습니다.

### 4-4. 오류 읽는 방법

오류의 `Sound!C1`은 **Sound 시트의 C열 1행**을 뜻합니다.
예를 들어 `Sound!C1 [기대 타입: 유효한 헤더 타입] ... ESoundType ...`이 표시되면,
Sound 시트 C1의 타입 이름과 Unity 프로젝트의 enum 정의를 확인하세요.

| 증상 또는 오류 | 확인·해결 방법 |
| --- | --- |
| 유효한 Spreadsheet ID를 입력하라는 오류 | URL 전체나 `gid`가 아닌 `/d/` 뒤 문서 ID만 입력 |
| GSTU_Config가 없다는 오류 | 기존 GSTU 설치와 Resources의 GSTU_Config 자산 확인 |
| API Key가 비어 있음 | GSTU 설정 Public 탭의 API Key 입력 및 Reader의 Access Mode 확인 |
| 비공개 인증 필요·토큰 갱신 실패 | GSTU Private 탭에서 Build Connection으로 다시 인증하고 계정의 시트 접근 권한 확인 |
| HTTP 오류·GSTU 응답 시간 초과 | ID, 인증 모드, 시트 접근 권한, 연결 상태 확인. 느린 요청이면 Request Timeout Seconds 조정 |
| ESoundType을 찾을 수 없거나 타입이 모호함 | 실제 public enum 정의·컴파일 확인. 같은 이름이 여러 개면 헤더에 네임스페이스 포함 |
| 헤더가 없거나 필수 헤더가 없음 | 모든 탭의 1행에 Name, Key(int)와 올바른 헤더 작성 |
| 중복 Name 또는 Key | 오류가 가리키는 시트 내 중복을 해결. 다른 시트의 같은 Key는 허용 |
| 숫자·bool 변환 실패 | 빈 셀, 소수점, 표시 형식, TRUE/FALSE와 타입별 입력 표 확인 |
| JSON 배열 오류 | 대괄호, 큰따옴표, 원소 타입, 마지막 쉼표 확인. 빈 리스트는 [] 입력 |
| 코드 생성/갱신 필요·헤더 변경 감지 | 코드 생성 / 갱신 후 Unity 컴파일 완료, 이어서 전체 데이터 가져오기 |
| 버튼이 비활성화됨 | Play Mode 종료, 진행 중 작업·컴파일 완료 대기, Console 컴파일 오류 해결 |
| 관리 중인 SO 또는 Catalog가 변경됨 | 동일 Reader 사용 여부와 기존 자산 Key·타입·위치를 확인. 자동 덮어쓰기 대신 원래 상태 복원 필요 |
| 다른 소스가 관리하는 경로·관리하지 않는 자산 | 기존 자산의 Reader·문서·출력 경로 확인. 새 가져오기는 겹치지 않는 경로 사용 |
| 같은 생성 타입이 다른 경로에 있음 | 중복 코드 확인. 생성 코드를 복제하지 말고 .meta와 함께 이동 |

## 5. 게임 코드에서 Catalog 사용하기

### 5-1. Key로 데이터 조회

**SoundData/SoundCatalog 생성과 컴파일을 완료한 후** 아래 예제를 사용할 수 있습니다.
클래스가 아직 생성되지 않은 상태에서 이 예제 코드를 먼저 추가하면 컴파일 오류가 납니다.

예제는 데이터를 읽고 출력합니다. 사운드 재생이나 AssetPath의 자산 로드는 포함하지 않습니다.

```csharp
using UnityEngine;
using ProjectFPS;

public sealed class SoundCatalogExample : MonoBehaviour
{
    [SerializeField] private SoundCatalog catalog;

    private void Start()
    {
        if (catalog == null)
        {
            Debug.LogWarning("Inspector에서 SoundCatalog를 연결하세요.");
            return;
        }

        if (catalog.TryGetByKey(1, out SoundData sound))
        {
            Debug.Log($"{sound.Name}: {sound.AssetPath}");
        }
        else
        {
            Debug.LogWarning("Key 1의 사운드 데이터가 없습니다.");
        }

        foreach (SoundData entry in catalog.Entries)
        {
            if (entry != null)
                Debug.Log($"Key={entry.Key}, Name={entry.Name}");
        }
    }
}
```

1. 위 코드를 `SoundCatalogExample.cs`로 저장합니다.
2. 장면의 GameObject에 해당 컴포넌트를 추가합니다.
3. Project 창의 `SoundCatalog.asset`을 컴포넌트의 Catalog 필드에 드래그해 연결합니다.
4. Play Mode에서 Console의 결과를 확인합니다.

`TryGetByKey`는 찾으면 `true`와 해당 SO를 반환하고, 없으면 `false`와 `null`을 반환합니다.
`Entries`는 Catalog의 참조 목록을 읽는 API입니다. Prefab도 `PrefabCatalog`와 `PrefabData`로 같은 방식으로 조회합니다.
런타임에서는 Google Sheets에 다시 접속하지 않아도 저장한 데이터 SO를 사용할 수 있습니다.

### 5-2. 개발자를 위한 자동 검증

일반적인 도구 사용에는 이 단계가 필요하지 않습니다. 도구 코드를 수정했을 때 검증하려면
프로젝트 루트에서 PowerShell을 열고 다음 명령을 실행합니다. 따옴표 안의 경로는 설치된 **Unity Editor의 Unity.exe 전체 경로**로 바꾸세요.

```powershell
.\Tests\Run-GoogleSpeadSheetReaderChecks.ps1 -UnityEditorPath 'D:\Program Files\Unity\Hub\Editor\6000.3.9f1\Editor\Unity.exe'
```

Windows 플레이어 빌드를 생략할 때는 다음과 같이 실행합니다.

```powershell
.\Tests\Run-GoogleSpeadSheetReaderChecks.ps1 -UnityEditorPath 'D:\Program Files\Unity\Hub\Editor\6000.3.9f1\Editor\Unity.exe' -SkipPlayerBuild
```

검증은 `Tests/Fixtures/BingoRoulette.json`의 첨부 데이터 구조를 사용하며,
미정 enum 오류, 타입·JSON 리스트 파싱, 100행 초과 데이터, 코드 생성, SO/Catalog 가져오기,
GUID·외부 참조 유지, 행 삭제, 경로 변경, 에디터 재시작과 Windows 플레이어 빌드를 확인합니다.
테스트용 enum과 자산은 분리된 임시 프로젝트에 생성하고 실제 프로젝트의 Assets에는 추가하지 않습니다.
검증 프로젝트와 로그는 `Library/GoogleSheetsReaderChecks/Run-...`에 남습니다.

이 검증은 실제 Google Sheets 접속을 확인하는 테스트는 아닙니다.
온라인 연결은 준비한 문서 ID와 인증 설정으로 Reader의 **시트 검색 → 전체 검증**을 실행해 확인하세요.