# FPS Engine / Emerald AI 전투 연동

## 캐릭터에 적용

1. `PlayerStats`가 있는 **동일한 GameObject**에 `FPSEmeraldPlayerAdapter`를 추가한다. `FactionExtension`과 `TargetPositionModifier`가 함께 필요하다. 후자의 TransformSource와 조준 높이를 지정한다.
2. `EmeraldSystem`이 있는 **동일한 GameObject**에 `FPSEmeraldAIAdapter`를 추가한다. 같은 캐릭터에 `EnemyHealth` 등의 별도 체력 컴포넌트를 중복 추가하지 않는다.
3. AI의 PlayerTag, DetectionLayerMask, 플레이어 진영 관계, FPS 무기 hitLayer와 투사체 projectileHitLayer를 실제 캐릭터 레이어에 맞춘다. Emerald의 `ObstructionDetectionLayerMask`는 코드에서 반전되어 **시야 검사에서 제외하는 레이어**로 사용된다.
4. 부위별 명중을 쓰는 AI는 Location Based Damage Collider 목록을 설정한다. 머리 Collider에는 `Critical`, 몸에는 `BodyShot` 태그를 사용한다. `SetCollidersLayerAndTag`가 시작 시 머리 태그를 덮어쓰지 않도록 설정한다.
5. 기존 HUD를 쓸 때 PlayerDependencies의 UIManager와 Crosshair 참조를 실제 씬 객체에 연결하고, 씬에 Input System EventSystem을 구성한다.

## 데미지 규칙

- FPS 공격은 기존 `DamageService.Router`를 계속 거친다. 기존 `IDamageRouter` 구현과 3인자 RequestDamage 호출은 유지된다.
- 새 호출은 `RequestDamage(target, amount, critical, new DamageContext(attacker, collider, kind))`이다. 수신 어댑터는 동기 호출 중 `DamageService.CurrentContext`를 읽는다. 비동기 라우터는 이 구조체를 호출 중 복사해 저장하고, 나중에 전달할 때 새 RequestDamage 호출로 문맥을 다시 설정해야 한다.
- 공격자는 실제 플레이어 컴포넌트의 Transform이다. 투사체는 발사자의 기존 Player 참조를 사용한다. 환경 폭발은 공격자 없이 Environmental 종류로 전달한다.
- 헤드샷 배율은 FPS 무기에서 한 번 적용한다. AI 어댑터는 DamageArea를 호출하지 않아 Emerald의 부위 배율을 중복 적용하지 않는다. 위치는 피격 효과와 래그돌에 전달한다.
- AI 데미지는 최종 계산 후 양수 사사오입한다. 0.5 미만, 음수, NaN, 무한대는 AI 데미지를 발생시키지 않는다.
- 플레이어 체력·방어막은 PlayerStats가 유일하게 관리한다. Emerald가 보는 체력은 실시간으로 읽으며 살아 있는 소수 체력은 최소 1, 사망은 0이다. 방어막은 체력에 합산하지 않는다.
- 한 폭발에 같은 캐릭터의 여러 Collider가 잡혀도 데미지는 한 번만 전달한다. 가장 가까운 Collider를 사용한다. 투사체 직격과 후속 폭발은 각각 별도 데미지다.
- 어댑터는 플레이어 이동용 Collider를 끄지 않는다. 리스폰은 기존 PlayerStats.Respawn API를 사용한다. 프로젝트의 사망 화면과 리스폰 트리거는 사용하는 게임 흐름에 맞춰 연결한다.
- 기존 PlayerStats.Respawn은 이동 상태를 복구한 뒤 CheckIfCanGrantControl도 호출하도록 보완했다. 일시정지 중에는 기존 FPS 규칙대로 조작을 복구하지 않는다.

## 확장 범위와 설정 위치

총성·발소리 감지, 보상·드롭, 아군 명령, 플레이어와 AI의 방어·회피·스턴을 지원한다. 로컬 단일 플레이어용 연동 코드이며 씬·프리팹·입력 에셋은 자동 변경하지 않는다. 신규 HUD·애니메이션 제작, 저장·불러오기, 멀티플레이 동기화는 포함하지 않는다.

설정은 컴포넌트 Inspector에서 변경한다. 기존 FPS 이동·대시·체력·성장·픽업과 Emerald의 이동·진영·전투 행동·애니메이션을 사용하며 새 AI 컨트롤러나 중복 체력 시스템을 만들지 않는다.

## 플레이어 설정

기존 `PlayerStats`, `PlayerControl`, `PlayerMovement`, `WeaponController`, `FPSEmeraldPlayerAdapter`가 있는 **같은 GameObject**에 다음 컴포넌트를 추가한다. 컨트롤러 프리팹의 외부 컨테이너와 실제 Player GameObject를 구분한다.

| 컴포넌트 | 역할과 주요 설정 |
| --- | --- |
| FPSPlayerCombat | 유지 방어 입력, 피해 감소, 스턴 및 제어 제한 |
| FPSNoiseEmitter | 실제 발사·발소리 이벤트와 무기별 소음 설정 |
| FPSProjectileBridge | FPS 투사체를 Emerald IAvoidable 감지에 연결 |
| FPSAllyCommandController | 소유 아군 목록·명령 카메라·레이어·입력 연결 |

### 입력과 회피

Button 타입 액션을 만들고 방어·추종·현재 위치 방어·이동 후 방어·공격 필드의 `InputActionReference`에 연결한다. 해당 Action Map은 기존 PlayerInput 또는 입력 관리 코드에서 활성화한다. 컴포넌트는 공유 액션을 임의로 Enable/Disable하지 않으며 기본 키를 강제하지 않는다. 방어는 `IsPressed`, 명령은 `WasPressedThisFrame`을 사용한다. 입력이 없는 경우 공개 API를 직접 호출할 수 있다.

회피는 기존 FPS 대시다. PlayerMovement의 `canDash`, `damageProtectionWhileDashing`을 켜고 방향·지속시간·횟수·회복시간·입력은 기존 대시 설정을 사용한다. 대시 시작 시 방어를 해제하고 실제 대시 상태와 피해 보호 설정을 `ICombat.IsDodging`에 반영한다.

### 방어와 스턴

플레이어 방어의 기본값은 정면 좌우 60°·피해 감소 50%다. 직접 공격 종류와 공격자가 있는 피해만 감소시킨 뒤 기존 방어막·체력 계산에 전달한다. 폭발·환경·공격자 또는 공격 종류가 불명인 피해는 감소시키지 않는다. 방어 중 이동·시점은 유지하고 공격·재장전·상호작용·무기 교체를 제한한다.

AI→플레이어 스턴은 Emerald 능력의 Stunned Settings를 사용한다. 스턴 중에는 방어를 해제하고 이동 입력·대시·공격·재장전·상호작용·명령을 제한한다. 진행 중 대시·그랩플·재장전과 아직 명중하지 않은 지연 근접/연발 공격도 중단한다. 시점·중력·외부 물리력은 유지한다. 반복 스턴은 새 요청 시점부터 요청 지속시간으로 갱신하며 누적하지 않는다. 사망·컴포넌트 비활성화 시 토큰·타이머를 정리하며 리스폰은 기존 PlayerStats.Respawn을 사용한다.

`PlayerControl.AddRestriction(token, flags)`는 기존 제어 플래그 위에 제한을 겹친다. 각 기능은 자기 토큰만 제거한다. `GrantControl`이 호출되어도 다른 토큰은 유지되고, 토큰을 제거해도 기존 LoseControl/LoseActionsControl 제한은 해제되지 않는다.

## 총성·발소리 감지

FPSNoiseEmitter의 기본 반경은 총성 40m·걷기 8m·달리기 16m·앉기 이동 3m다. `Weapons` 목록에서 무기별 일반 반경과 소음기 여부·반경(기본 12m)을 지정한다. 소음기 여부는 이 목록으로 설정하며 부착물 장착 상태를 자동 추측하지 않는다.

발소리는 기존 OnFootstepPlayed에서 실제 클립이 재생된 경우에만 발행한다. 총성은 실제 발사 완료 이벤트를 사용하며 빈 탄창 클릭·근접·Custom Shoot Style은 제외한다. 동시 산탄은 발사당 한 번, 시간 간격이 있는 점사는 각 발사마다 발행한다.

적 AI의 EmeraldSystem GameObject에 `FPSEmeraldHearing`을 추가한다. 활성 NavMeshAgent와 베이크된 NavMesh가 필요하며 agent type·area mask를 사용해 도달 가능한 경로를 찾는다. `Obstruction Layers`에는 벽·지형만 포함하고 캐릭터·무기·투사체 레이어는 제외한다. 사이에 차폐물이 있으면 감지 반경에 기본 0.5 배율을 적용한다.

기존 `EmeraldSoundDetector` 컴포넌트는 비활성화하거나 `DisableSoundDetector()`를 호출해 이동 기반 감지의 중복을 막는다. 새 컴포넌트는 이를 자동 변경하지 않는다.

적대 플레이어의 소리만 감지한다. 전투·사망·추종 중이거나 외부에서 기본 이동을 일시정지한 AI는 조사에 들어가지 않는다. 발생 당시 위치의 2m 이내 NavMesh 지점으로 이동하고 도착 후 5초 조사한 뒤 기존 목적지·이동 상태로 복귀한다. 이동 제한시간은 15초다. 경로가 없으면 조사하지 않고 새 소리가 들어오면 위치·타이머를 갱신한다. 시야로 적을 발견하면 기존 전투에 목적지 제어를 넘기며, 전투 중 소리로 대상을 변경하지 않는다.

## AI 방어·회피·스턴

AI에 기존 FPSEmeraldAIAdapter와 `FPSEmeraldCombatActions`를 추가한다. 사용할 Emerald Animation Profile의 무기 타입에 BlockIdle·DodgeLeft·DodgeRight·DodgeBack·Stunned를 연결하고 Emerald 방식으로 Animator Controller를 생성·갱신한다. 해당 클립이나 Controller가 없으면 기능을 적용하지 않고 원인을 표시한다.

| 행동 | 확률 | 피해 감소 | 유지 | 쿨다운 | 감지 각도 |
| --- | --- | --- | --- | --- | --- |
| 방어 | 40% | 50% | 1초 | 3초 | 총 120° |
| 회피 | 25% | 100% | 기존 회피 클립 | 4초 | 총 120° |

FPSEmeraldCombatActions는 사용하는 무기 타입의 목록에 행동이 없으면 런타임 전용 BlockAction/DodgeAction을 추가한다. 기존 행동 에셋이 있으면 그 설정을 유지하므로, 기존 행동을 쓰면서 위 기본값을 원하면 해당 에셋에서 직접 지정한다. 다른 전투 행동 목록을 지우지 않는다.

`Projectile Layers`에 실제 FPS 투사체 Collider 레이어를 포함한다(기본 Ignore Raycast). 플레이어의 FPSProjectileBridge가 생성 이벤트를 구독해 투사체와 자식 Collider에 FPSAvoidableProjectile을 연결한다. 진행 방향의 가까운 충돌 대상을 갱신해 IAvoidable.AbilityTarget에 전달한다. 별도의 투사체 프리팹 편집은 필요하지 않다.

히트스캔 대응은 실제 발사 후 LateUpdate에 실행하므로 이미 받은 피해·같은 프레임 산탄 피해를 취소하지 않는다. 현재 전투 대상이 시야 안에 있고 각도·행동 조건·쿨다운을 만족하면 후속 발사에 대비해 기존 행동을 시도한다. 피해 감소는 EmeraldHealth가 한 번 계산한다. 감지 총 각도와 피해 감소 범위는 양쪽 모두 총 각도의 절반을 좌우 한쪽 한계로 사용한다.

플레이어 근접·빠른 근접 공격이 AI 체력을 실제 줄이고 대상이 살아 있으면 `Melee Stun Seconds`(기본 1초)를 적용한다. 무적·완전 회피·사망 대상에는 이 스턴을 적용하지 않는다. AI 스턴의 반복 요청도 지속시간을 새 요청부터 갱신하고 사망·비활성화 시 정리한다.

## 보상과 드롭

적 AI에 `FPSEmeraldRewards`를 추가하고 기준 `Player`를 지정한다. `Progression`이 비어 있으면 해당 플레이어의 PlayerDependencies.ProgressionManager를 사용한다. 기존 서비스의 UseCoins/UseExperience와 경험치 요구량을 구성한다. 성장 서비스가 없으면 보상을 생략하고 경고하며 드롭은 독립적으로 처리한다.

실제 체력을 줄인 마지막 공격자가 플레이어 또는 그 플레이어 소유 아군이면 적 처치에 코인 10·경험치 25를 한 생명당 한 번 지급한다. 무적·완전 회피로 피해가 없으면 공격자 기록을 덮어쓰지 않는다. 마지막 유효 피해가 환경 피해이거나 KillAI로 죽인 경우 지급하지 않는다. Grenade 공격자는 투척 소유자로 전달한다. 아군 사망에는 보상·드롭을 적용하지 않는다.

드롭은 탄약 30%·체력팩 15%·없음 55% 중 하나다. 적 환경사에도 적용한다. `Ammo Prefab`, `Health Prefab`에 기존 AmmoBoxPrefab·체력팩 또는 원하는 기존 픽업 프리팹을 지정한다. 프리팹이 없는 결과는 드롭 없음으로 처리한다. 확률 합이 100%를 넘으면 탄약 확률을 먼저 적용하고 체력팩 확률을 남은 범위로 제한한다. 기존 획득 방식으로 동작하며 60초 후 제거한다.

중복 사망 통지는 한 번만 처리한다. AI를 재사용할 때는 체력 필드를 직접 변경해 부활시키는 대신 EmeraldSystem.ResetAI/EmeraldAPI.Combat.ResetAI를 사용한다. 이 API가 생명 시작 이벤트를 발행해 지급 상태·공격자 기록을 초기화한다.

## 아군 명령

아군의 EmeraldSystem GameObject에 `FPSEmeraldCompanion`을 추가하고 Owner를 지정한다. 진영 관계에서 플레이어를 Friendly·적을 Enemy로 지정한다. 활성 NavMeshAgent가 필요하며 시작 시 플레이어를 추종하고 전투 행동은 Aggressive로 연결한다.

FPSAllyCommandController의 Companions에 명령할 아군을 명시적으로 등록한다. Command Camera와 Command Layers를 지정한다. 레이어에는 명령 대상 지면·AI·몸 Collider를 포함하고 플레이어·무기·UI·불필요한 Trigger는 제외한다. Emerald의 대상 Collider가 Trigger인 경우도 선택할 수 있도록 레이캐스트는 Trigger를 포함한다.

추종은 플레이어를 따라가고, 현재 위치 방어는 각 아군 자신의 위치를 지킨다. 이동 후 방어는 카메라 중심 50m 이내 레이캐스트 지점의 도달 가능한 NavMesh 위치로 이동한다. 공격은 선택한 살아 있는 적 Emerald AI를 대상으로 한다. 종료·대상 소멸·대상 변경 시 이전 추종/방어 명령으로 돌아간다. 명령으로 바꾼 무한 추격 설정도 이전 값으로 복구한다.

비활성·사망·스턴 아군, 다른 소유자, 비적대 대상, 이동 불가 위치를 거부한다. 플레이어 방어·스턴·사망·일시정지 등으로 행동이 제한된 동안 명령을 거부한다. 중복 등록된 아군에는 한 번만 실행하며 빈 목록·누락 아군도 실패 결과로 보고한다.

## 공개 API와 서드파티 확장 지점

- FPSNoiseEmitter.Emitted는 FPSNoise의 Source·Kind·Position·Radius·Weapon을 전달한다.
- FPSPlayerCombat.SetBlocking(bool), TriggerStun(float), IsBlocking·IsDodging·IsStunned를 제공하고 기존 ICombat 어댑터가 이 상태를 읽는다.
- FPSAllyCommandController.Execute(command, point, target)는 아군별 FPSCommandResult 목록을 반환하고 CommandCompleted를 발행한다. TryGetAim으로 현재 조준 위치·대상을 조회할 수 있다. Companion.Execute(caller, ...)는 단일 아군 API다.
- FPSCommandFailure는 Unavailable·NotOwned·InvalidTarget·Unreachable·PlayerRestricted를 구분한다.
- WeaponControllerEvents의 OnWeaponFired·OnProjectileCreated는 기존 발사 이벤트를 바꾸지 않는 추가 확장 지점이다.
- Emerald의 IContextualDamageable과 EmeraldDamageDispatch는 선택적으로 공격 종류를 전달한다. 기존 IDamageable만 구현한 대상에는 기존 호출로 전달한다. FPS 어댑터가 EmeraldAttackKind를 DamageKind로 변환하므로 서드파티 코드가 ProjectFPS 클래스를 직접 참조하지 않는다.
- EmeraldHealth.OnDamageResolved는 실제 양수 피해·공격자를 전달한다. KillAI는 처치 기여 초기화를 위한 `(0, null)`을 전달한다. OnLifeStarted는 초기 시작/ResetAI의 생명 초기화 이벤트다.

DamageService.Router와 RequestDamage 오버로드는 유지한다. PlayerStats는 선택적 IPlayerDamageFilter를 통해 방어를 적용한다. 비동기 사용자 라우터는 기존 README의 피해 문맥 복사 규칙을 따라야 한다.

서드파티 업데이트 시 유지할 변경: Cowsins의 제어 토큰·선택적 피해 필터, 실제 발사/투사체 이벤트와 지연 공격 취소·스턴 이동 입력 차단; Emerald의 선택적 피해 전달 계약과 능력 호출, 실제 피해/생명 이벤트, 반복 스턴 갱신, BlockAction/DodgeAction의 후속 발사 대응 진입점·각도 일치. 기본 인터페이스 서명과 기존 직렬화 필드는 유지한다.

## 수동 플레이 확인

실제 씬·프리팹을 연결하지 않았으므로 다음 플레이 검증은 별도로 수행해야 한다.

1. 일반/소음기 발사·걷기/달리기/앉기·차폐·경계에서 감지한다. 빈 탄창·근접·산탄/점사 이벤트 수와 컴포넌트 재활성화 후 중복 구독을 확인한다.
2. AI의 발생 위치 이동·도착 후 5초 조사·기존 순찰 복귀·시야 전투 전환과 전투 중 대상 유지를 확인한다.
3. 정면/후면 방어와 방어막·폭발 피해, 기존 대시 보호, AI 투사체 감지·후속 히트스캔 대응의 피해 중복 여부를 확인한다.
4. 반복 스턴, 스턴 중 사망·일시정지·리스폰, 이동 상태 전환, 외부 물리력과 다른 제어 제한의 복구를 확인한다.
5. 플레이어·소유 아군·환경·KillAI 처치, 무적/완전 회피, 중복 사망과 ResetAI 재사용의 보상을 확인한다. 픽업 획득과 60초 제거를 확인한다.
6. 아군 명령 4종과 공격 종료 복귀, 다른 소유자·사망/스턴·비적대 대상·이동 불가 위치의 실패 결과를 확인한다.

Unity 컴파일 결과와 연결 후 실제 플레이 확인 결과를 구분해서 기록한다.
