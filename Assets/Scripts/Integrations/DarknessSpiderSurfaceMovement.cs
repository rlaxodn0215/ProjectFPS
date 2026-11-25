using UnityEngine;
using UnityEngine.AI;

namespace ProjectFPS
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class DarknessSpiderSurfaceMovement : MonoBehaviour
    {
        [SerializeField] private LayerMask surfaceLayers = Physics.DefaultRaycastLayers;

        private const float Clearance = 0.035f;
        private const float BodyRadius = 0.14f;
        private const float StepLength = 0.1f;
        private const float CornerReach = 0.27f;
        private NavMeshPath wallPath;
        private readonly RaycastHit[] hits = new RaycastHit[32];
        private NavMeshAgent agent;
        private Vector3 normal = Vector3.up;
        private Vector3 surfaceHeading;
        private Vector3 wallPoint;
        private Vector3 wallNormal;
        private Vector3 lastGround;
        private float nextWallSearch;
        private float nextPathUpdate;
        private float surfaceUntil;
        private float fallSpeed;
        private bool approachingWall;
        private bool detached;

        internal bool OnSurface { get; private set; }
        internal bool Falling { get; private set; }
        internal bool Grounded => !detached && agent.isActiveAndEnabled && agent.isOnNavMesh;
        internal Vector3 Normal => normal;
        internal bool Moving { get; private set; }

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            wallPath = new NavMeshPath();
        }

        internal void Tick(Transform target, float speed, bool allowClimb, float stopDistance, float wanderRadius, Vector3 home)
        {
            if (Falling)
            {
                Fall();
                return;
            }

            if (OnSurface)
            {
                if (!HasSupport()) { BeginFall(); return; }
                Vector3 heading = surfaceHeading;
                if (Time.time >= surfaceUntil)
                {
                    Vector3 down = Vector3.ProjectOnPlane(Vector3.down, normal);
                    if (down.sqrMagnitude > 0.01f) heading = down;
                    else
                    {
                        // Return across the ceiling toward the wall used to climb.
                        Vector3 wallward = Vector3.ProjectOnPlane(wallPoint - wallNormal * 0.3f - transform.position, normal);
                        if (wallward.sqrMagnitude > 0.01f) heading = wallward;
                    }
                }
                else if (target)
                {
                    Vector3 toward = Vector3.ProjectOnPlane(target.position - transform.position, normal);
                    if (toward.sqrMagnitude > 0.04f)
                        heading = Vector3.Dot(normal, Vector3.up) < -0.5f
                            ? toward
                            : (toward.normalized + Vector3.up * 0.65f);
                }

                Vector3 side = Vector3.Cross(normal, heading);
                Moving = Move(heading, speed * Time.deltaTime, true) ||
                    Move(side, speed * Time.deltaTime, true) ||
                    Move(-side, speed * Time.deltaTime, true) ||
                    Move(-heading, speed * Time.deltaTime, true);
                return;
            }

            if (!Grounded)
            {
                BeginFall();
                return;
            }

            lastGround = agent.nextPosition;
            if (!allowClimb) approachingWall = false;
            if (allowClimb && !approachingWall && Time.time >= nextWallSearch)
            {
                nextWallSearch = Time.time + 2f;
                approachingWall = FindWall(speed);
            }

            if (approachingWall)
            {
                if (Vector3.Distance(transform.position, wallPoint) < 0.65f)
                {
                    if (TryRay(wallPoint + wallNormal * 0.4f, -wallNormal, 0.6f, out var wall) &&
                        TryPose(wall.point, wall.normal, Vector3.up, out var point, out var rotation))
                    {
                        agent.ResetPath();
                        agent.isStopped = true;
                        agent.updatePosition = false;
                        detached = true;
                        OnSurface = true;
                        normal = wall.normal;
                        surfaceHeading = rotation * Vector3.forward;
                        transform.SetPositionAndRotation(point, rotation);
                        surfaceUntil = Time.time + 10f;
                        Moving = true;
                        approachingWall = false;
                        return;
                    }
                    approachingWall = false;
                }
                else if (!agent.pathPending && (!agent.hasPath || agent.pathStatus != NavMeshPathStatus.PathComplete))
                    approachingWall = false;
            }

            if (!approachingWall && Time.time >= nextPathUpdate)
            {
                nextPathUpdate = Time.time + 0.2f;
                agent.isStopped = false;
                agent.stoppingDistance = target ? stopDistance : 0.2f;
                if (target)
                {
                    if (NavMesh.SamplePosition(target.position, out var point, 2f, Filter))
                        agent.SetDestination(point.position);
                }
                else if (!agent.pathPending && (!agent.hasPath || agent.remainingDistance <= 0.25f))
                {
                    Vector2 offset = Random.insideUnitCircle * wanderRadius;
                    if (NavMesh.SamplePosition(home + new Vector3(offset.x, 0f, offset.y), out var point, 3f, Filter))
                        agent.SetDestination(point.position);
                }
            }

            agent.speed = speed;
            Moving = agent.velocity.sqrMagnitude > 0.01f;
            if (Moving) Face(agent.velocity);
        }

        internal void Stop()
        {
            Moving = false;
            if (Grounded) agent.isStopped = true;
        }

        internal void InterruptClimb() => approachingWall = false;

        internal void Face(Vector3 direction)
        {
            direction = Vector3.ProjectOnPlane(direction, normal);
            if (direction.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation,
                    Quaternion.LookRotation(direction, normal), 720f * Time.deltaTime);
        }

        internal bool HasGroundPath(Vector3 destination)
        {
            return Grounded && NavMesh.SamplePosition(destination, out var hit, 0.8f, Filter) &&
                !NavMesh.Raycast(agent.nextPosition, hit.position, out _, Filter);
        }

        internal bool CanMove(Vector3 heading, float distance)
        {
            if (Grounded) return TryGroundStep(heading, distance, out _);
            if (!OnSurface) return false;
            Vector3 start = transform.position;
            Vector3 direction = Vector3.ProjectOnPlane(heading, normal).normalized;
            for (float walked = 0; walked < distance; walked += StepLength)
            {
                if (!TrySurfaceStep(start, direction, Mathf.Min(StepLength, distance - walked), false, out var point, out _))
                    return false;
                start = point;
            }
            return true;
        }

        internal bool Move(Vector3 heading, float distance, bool allowCorners)
        {
            if (Grounded)
            {
                if (!TryGroundStep(heading, distance, out var destination)) return false;
                agent.Move(destination - agent.nextPosition);
                return true;
            }
            if (!OnSurface) return false;

            bool moved = false;
            while (distance > 0.001f)
            {
                Vector3 direction = Vector3.ProjectOnPlane(heading, normal).normalized;
                if (direction == Vector3.zero) direction = surfaceHeading;
                float step = Mathf.Min(distance, StepLength);
                if (!TrySurfaceStep(transform.position, direction, step, allowCorners, out var point, out var rotation)) break;
                transform.SetPositionAndRotation(point, rotation);
                normal = rotation * Vector3.up;
                surfaceHeading = rotation * Vector3.forward;
                heading = surfaceHeading;
                moved = true;
                distance -= step;
                if (Vector3.Dot(normal, Vector3.up) > 0.75f && AttachGround(point)) break;
            }
            return moved;
        }

        private bool FindWall(float speed)
        {
            float closest = 4f;
            Vector3 destination = default;
            for (int i = 0; i < 8; i++)
            {
                Vector3 direction = Quaternion.AngleAxis(45f * i, Vector3.up) * transform.forward;
                if (!TryRay(transform.position + Vector3.up * 0.25f, direction, closest, out var hit) ||
                    Mathf.Abs(hit.normal.y) > 0.35f) continue;
                Vector3 stand = hit.point + hit.normal * 0.45f;
                if (!NavMesh.SamplePosition(stand, out var ground, 0.6f, Filter) ||
                    !agent.CalculatePath(ground.position, wallPath) || wallPath.status != NavMeshPathStatus.PathComplete) continue;
                closest = hit.distance;
                wallPoint = hit.point;
                wallNormal = hit.normal;
                destination = ground.position;
            }
            if (closest >= 4f) return false;
            agent.isStopped = false;
            agent.speed = speed;
            agent.stoppingDistance = 0.05f;
            return agent.SetDestination(destination);
        }

        private bool TrySurfaceStep(Vector3 from, Vector3 direction, float step, bool corners,
            out Vector3 point, out Quaternion rotation)
        {
            point = from;
            rotation = transform.rotation;
            if (direction.sqrMagnitude < 0.01f) return false;
            Vector3 next = from + direction * step;
            Vector3 probe = from + normal * 0.13f;
            if (TryRay(probe, direction, step + (corners ? BodyRadius + 0.08f : 0f), out var bend) &&
                Vector3.Dot(bend.normal, normal) < 0.75f)
            {
                return corners && TryPose(bend.point, bend.normal, normal, out point, out rotation) &&
                    ClearSegment(from, normal, point, bend.normal);
            }

            if (TryRay(next + normal * 0.16f, -normal, CornerReach, out var support) &&
                (corners || Vector3.Dot(support.normal, normal) > 0.95f))
            {
                return TryPose(support.point, support.normal, direction, out point, out rotation) &&
                    ClearSegment(from, normal, point, support.normal);
            }

            // After passing a convex lip, probe backward around the edge.
            if (corners && TryRay(next + direction * 0.04f - normal * 0.1f, -direction,
                CornerReach, out var edge))
                return TryPose(edge.point, edge.normal, -normal, out point, out rotation) &&
                    ClearSegment(from, normal, point, edge.normal);
            return false;
        }

        private bool TryPose(Vector3 contact, Vector3 up, Vector3 forward, out Vector3 point, out Quaternion rotation)
        {
            point = contact + up * Clearance;
            Vector3 tangent = Vector3.ProjectOnPlane(forward, up).normalized;
            rotation = tangent == Vector3.zero ? transform.rotation : Quaternion.LookRotation(tangent, up);
            return tangent != Vector3.zero && !Physics.CheckSphere(point + up * (BodyRadius + 0.05f),
                BodyRadius, surfaceLayers & ~(1 << gameObject.layer) & ~(1 << LayerMask.NameToLayer("Player")),
                QueryTriggerInteraction.Ignore);
        }

        private bool ClearSegment(Vector3 from, Vector3 fromNormal, Vector3 to, Vector3 toNormal)
        {
            Vector3 center = from + fromNormal * (BodyRadius + 0.05f);
            Vector3 end = to + toNormal * (BodyRadius + 0.05f);
            Vector3 delta = end - center;
            return delta.sqrMagnitude < 0.0001f || !Physics.SphereCast(center, BodyRadius, delta.normalized,
                out _, delta.magnitude, surfaceLayers & ~(1 << gameObject.layer) & ~(1 << LayerMask.NameToLayer("Player")),
                QueryTriggerInteraction.Ignore);
        }

        private bool TryGroundStep(Vector3 direction, float distance, out Vector3 point)
        {
            Vector3 from = agent.nextPosition;
            Vector3 wanted = from + Vector3.ProjectOnPlane(direction, Vector3.up).normalized * distance;
            point = from;
            if (!NavMesh.SamplePosition(wanted, out var hit, 0.8f, Filter) ||
                Vector3.ProjectOnPlane(hit.position - wanted, Vector3.up).sqrMagnitude > 0.0025f ||
                NavMesh.Raycast(from, hit.position, out _, Filter)) return false;
            point = hit.position;
            return ClearSegment(from, Vector3.up, point, Vector3.up);
        }

        private bool TryRay(Vector3 origin, Vector3 direction, float distance, out RaycastHit result)
        {
            result = default;
            float closest = float.PositiveInfinity;
            int count = Physics.RaycastNonAlloc(origin, direction, hits, distance, surfaceLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var hit = hits[i];
                if (hit.distance >= closest || hit.transform.IsChildOf(transform) ||
                    hit.collider.attachedRigidbody || hit.collider.GetComponentInParent<EmeraldAI.EmeraldSystem>() ||
                    hit.collider.GetComponentInParent<cowsins.PlayerStats>()) continue;
                result = hit;
                closest = hit.distance;
            }
            return result.collider;
        }

        internal bool HasSupport() => !OnSurface || TryRay(transform.position + normal * 0.12f, -normal, CornerReach, out _);

        internal void BeginFall()
        {
            if (Grounded)
            {
                lastGround = agent.nextPosition;
                agent.ResetPath();
                agent.isStopped = true;
                agent.updatePosition = false;
            }
            OnSurface = false;
            Falling = detached = true;
            fallSpeed = 0f;
            Moving = false;
        }

        private void Fall()
        {
            fallSpeed += 9.81f * Time.deltaTime;
            float step = fallSpeed * Time.deltaTime;
            if (TryRay(transform.position + Vector3.up * 0.06f, Vector3.down, step + 0.1f, out var hit) &&
                hit.normal.y > 0.5f && AttachGround(hit.point)) return;

            transform.position += Vector3.down * step;
            if (transform.position.y < Mathf.Min(lastGround.y, 0f) - 25f)
                AttachGround(lastGround);
        }

        private bool AttachGround(Vector3 point)
        {
            if (!agent.isActiveAndEnabled || !NavMesh.SamplePosition(point, out var hit, 0.55f, Filter) ||
                !agent.Warp(hit.position)) return false;
            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            transform.SetPositionAndRotation(hit.position,
                Quaternion.LookRotation(forward == Vector3.zero ? Vector3.forward : forward, Vector3.up));
            agent.updatePosition = true;
            agent.isStopped = false;
            OnSurface = Falling = detached = false;
            normal = Vector3.up;
            nextWallSearch = Time.time + 2f;
            nextPathUpdate = 0f;
            return true;
        }

        internal void ResetMotion()
        {
            if (detached && GroundedAfterReset())
                AttachGround(lastGround);
            OnSurface = Falling = detached = approachingWall = Moving = false;
            normal = Vector3.up;
            if (agent)
            {
                agent.updatePosition = true;
                if (agent.isActiveAndEnabled && agent.isOnNavMesh) agent.isStopped = false;
            }
        }

        private bool GroundedAfterReset() => agent && agent.isActiveAndEnabled && agent.isOnNavMesh;

        private NavMeshQueryFilter Filter => new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
    }
}
