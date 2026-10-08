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

총성·발소리 감지, 보상·드롭·아군 명령, 신규 방어·회피·스턴 기능은 이번 연동 범위에 포함하지 않는다.
