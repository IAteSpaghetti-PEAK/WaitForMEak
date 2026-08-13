using UnityEngine;

namespace WaitForMEak
{
    /// <summary>
    /// "Is this scout somewhere you could actually put another player down?" and, once the
    /// answer is yes, where exactly to put them.
    ///
    /// Everything here is evaluated on the host. That works because <see cref="CharacterMovement"/>
    /// runs its ground checks for every character on every client, not just the local one, so
    /// <c>data.isGrounded</c> / <c>groundedFor</c> / <c>groundNormal</c> are meaningful for
    /// remote scouts too.
    /// </summary>
    internal static class Arrival
    {
        /// <summary>
        /// True when <paramref name="c"/> has been standing on sane, non-vertical ground long
        /// enough to drop someone next to them. <paramref name="groundPoint"/> is the spot they
        /// are standing on.
        /// </summary>
        internal static bool IsStandable(Character c, out Vector3 groundPoint)
        {
            groundPoint = Vector3.zero;
            if (c == null) return false;
            if (c.data.dead || c.data.fullyPassedOut || c.warping) return false;
            if (c.data.isClimbing || c.data.isRopeClimbing || c.data.isVineClimbing) return false;
            if (c.data.isCarried) return false;

            if (!c.data.isGrounded) return false;
            if (c.data.groundedFor < WaitConfig.GroundedSecondsRequired.Value) return false;

            // Standing on a near-vertical face (or wedged in a crack) doesn't count.
            Vector3 normal = c.data.groundNormal;
            if (normal.sqrMagnitude < 0.01f) return false;
            if (Vector3.Angle(Vector3.up, normal) > WaitConfig.MaxGroundSlope.Value) return false;

            if (c.data.avarageVelocity.magnitude > WaitConfig.MaxTargetSpeed.Value) return false;

            Vector3 ground = c.data.groundPos;
            // groundPos is only refreshed while grounded; if it's stale/nonsense fall back to
            // the scout's own position.
            if (ground == Vector3.zero || Vector3.Distance(ground, c.Center) > 4f)
                ground = c.Center;

            groundPoint = ground;
            return true;
        }

        /// <summary>
        /// A spot beside <paramref name="groundPoint"/> to warp the joiner to: a short step to
        /// one side, re-settled onto whatever ground is actually there, lifted clear of it.
        /// </summary>
        internal static Vector3 PickArrivalPosition(Vector3 groundPoint)
        {
            Vector2 circle = Random.insideUnitCircle.normalized;
            if (circle.sqrMagnitude < 0.01f) circle = Vector2.right;
            Vector3 offset = new Vector3(circle.x, 0f, circle.y) * WaitConfig.ArrivalOffset.Value;

            Vector3 probeTop = groundPoint + offset + Vector3.up * 2f;
            RaycastHit hit = HelperFunctions.LineCheck(probeTop, probeTop + Vector3.down * 5f,
                                                       HelperFunctions.LayerType.TerrainMap);

            // No ground to the side (ledge, narrow platform) - land on the target's own footing.
            Vector3 landing = hit.transform != null ? hit.point : groundPoint;
            return landing + Vector3.up * 1.1f;
        }
    }
}
