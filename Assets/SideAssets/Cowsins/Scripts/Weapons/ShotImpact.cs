using System;
using UnityEngine;

namespace cowsins
{
    internal static class ShotImpact
    {
        internal static event Action<Transform, Vector3, Collider> Occurred;

        internal static void Report(Transform shooter, Vector3 position, Collider collider)
        {
            if (shooter && collider) Occurred?.Invoke(shooter, position, collider);
        }
    }
}
