using System.Collections.Generic;
using cowsins;
using EmeraldAI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectFPS.Integrations
{
    [DisallowMultipleComponent, RequireComponent(typeof(PlayerStats), typeof(PlayerControl))]
    public sealed class FPSAllyCommandController : MonoBehaviour
    {
        [SerializeField] private List<FPSEmeraldCompanion> companions = new List<FPSEmeraldCompanion>();
        [SerializeField] private Camera commandCamera;
        [SerializeField] private LayerMask commandLayers = ~0;
        [SerializeField, Min(0)] private float commandDistance = 50;
        [SerializeField] private InputActionReference follow, guardHere, moveAndGuard, attack;
        private PlayerStats player;
        public event System.Action<FPSCommandResult> CommandCompleted;
        private void Awake() => player = GetComponent<PlayerStats>();
        private void Start()
        {
            if (commandCamera == null) Debug.LogWarning("FPSAllyCommandController: Command Camera가 없어 조준 입력 명령이 실패합니다. Execute API는 사용할 수 있습니다.", this);
            foreach (var reference in new[] { follow, guardHere, moveAndGuard, attack })
                if (reference != null && reference.action == null) Debug.LogError("FPSAllyCommandController: 실제 액션이 누락된 입력 참조는 무시합니다.", this);
        }
        private void Update()
        {
            if (Pressed(follow)) Execute(FPSAllyCommand.Follow);
            if (Pressed(guardHere)) Execute(FPSAllyCommand.GuardHere);
            if (Pressed(moveAndGuard)) AimCommand(FPSAllyCommand.MoveAndGuard);
            if (Pressed(attack)) AimCommand(FPSAllyCommand.Attack);
        }
        private static bool Pressed(InputActionReference reference) => reference != null && reference.action != null && reference.action.WasPressedThisFrame();
        private void AimCommand(FPSAllyCommand command)
        {
            if (!TryGetAim(out var point, out var target))
            { CommandCompleted?.Invoke(new FPSCommandResult(null, FPSCommandFailure.InvalidTarget)); return; }
            Execute(command, point, target);
        }
        public bool TryGetAim(out Vector3 point, out Transform target)
        {
            point = default; target = null;
            // Emerald's detection collider can be a trigger; the command mask excludes irrelevant triggers.
            if (commandCamera == null || !Physics.Raycast(commandCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f)),
                out var hit, commandDistance, commandLayers, QueryTriggerInteraction.Collide)) return false;
            point = hit.point;
            var ai = hit.collider.GetComponentInParent<EmeraldSystem>();
            target = ai != null ? ai.transform : null;
            return true;
        }
        public IReadOnlyList<FPSCommandResult> Execute(FPSAllyCommand command, Vector3 point = default, Transform target = null)
        {
            var results = new List<FPSCommandResult>();
            if (companions.Count == 0)
            {
                var empty = new FPSCommandResult(null, FPSCommandFailure.Unavailable);
                results.Add(empty); CommandCompleted?.Invoke(empty); return results;
            }
            var seen = new HashSet<FPSEmeraldCompanion>();
            foreach (var companion in companions)
            {
                if (companion != null && !seen.Add(companion)) continue;
                var result = companion != null ? companion.Execute(player, command, point, target) :
                    new FPSCommandResult(null, FPSCommandFailure.Unavailable);
                results.Add(result); CommandCompleted?.Invoke(result);
            }
            return results;
        }
    }
}
