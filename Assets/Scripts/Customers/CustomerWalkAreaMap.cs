using System.Collections.Generic;
using UnityEngine;

public class CustomerWalkAreaMap : MonoBehaviour
{
    [SerializeField] private float connectionTolerance = 2f;
    [SerializeField] private float edgePadding = 0.15f;

    private readonly List<WalkArea> areas = new List<WalkArea>();
    private bool initialized;

    public bool HasAreas
    {
        get
        {
            EnsureInitialized();
            return areas.Count > 0;
        }
    }

    public Vector3 ClampToWalkAreas(Vector3 position)
    {
        EnsureInitialized();
        if (areas.Count == 0)
        {
            return position;
        }

        int areaIndex = FindContainingAreaIndex(position);
        if (areaIndex >= 0)
        {
            return areas[areaIndex].Clamp(position, edgePadding);
        }

        int closestAreaIndex = FindClosestAreaIndex(position);
        return closestAreaIndex >= 0 ? areas[closestAreaIndex].Clamp(position, edgePadding) : position;
    }

    public Vector3 ConstrainMovement(Vector3 currentPosition, Vector3 desiredPosition)
    {
        return ConstrainMovement(currentPosition, desiredPosition, desiredPosition);
    }

    public Vector3 ConstrainMovement(Vector3 currentPosition, Vector3 desiredPosition, Vector3 movementTarget)
    {
        EnsureInitialized();
        if (areas.Count == 0)
        {
            return desiredPosition;
        }

        List<int> currentAreaIndexes = FindAreaCandidates(currentPosition);
        if (currentAreaIndexes.Count == 0)
        {
            return desiredPosition;
        }

        List<int> desiredAreaIndexes = FindContainingAreaIndexes(desiredPosition);
        int connectedDesiredAreaIndex = FindConnectedAreaIndex(currentAreaIndexes, desiredAreaIndexes);
        if (connectedDesiredAreaIndex >= 0)
        {
            return desiredPosition;
        }

        List<int> targetAreaIndexes = FindContainingAreaIndexes(movementTarget);
        if (FindConnectedAreaIndex(currentAreaIndexes, targetAreaIndexes) >= 0)
        {
            return desiredPosition;
        }

        // Keep customers inside their current walk rectangle unless they are crossing
        // directly into another connected walk rectangle.
        return areas[FindBestClampAreaIndex(currentAreaIndexes, desiredPosition)].Clamp(desiredPosition, edgePadding);
    }

    public Vector3[] BuildRoute(Vector3 start, IReadOnlyList<Vector3> requestedRoute)
    {
        EnsureInitialized();
        if (areas.Count == 0 || requestedRoute == null || requestedRoute.Count == 0)
        {
            return requestedRoute != null ? CopyRoute(requestedRoute) : new Vector3[0];
        }

        List<Vector3> route = new List<Vector3>();
        Vector3 current = ClampToWalkAreas(start);
        for (int i = 0; i < requestedRoute.Count; i++)
        {
            Vector3 target = ClampToWalkAreas(requestedRoute[i]);
            AddPathSegment(route, current, target);
            current = target;
        }

        return route.ToArray();
    }

    private void EnsureInitialized()
    {
        if (initialized)
        {
            return;
        }

        initialized = true;
        areas.Clear();

        BoxCollider[] boxColliders = GetComponentsInChildren<BoxCollider>(true);
        foreach (BoxCollider boxCollider in boxColliders)
        {
            if (boxCollider == null || boxCollider.transform == transform || !boxCollider.enabled)
            {
                continue;
            }

            if (boxCollider.size.x <= 0.01f || boxCollider.size.z <= 0.01f)
            {
                continue;
            }

            // The walk zones are authored as trigger box colliders on the child Area objects.
            // Trigger state does not matter here; we read the box geometry directly.
            areas.Add(new WalkArea(boxCollider));
        }

        if (areas.Count > 0)
        {
            return;
        }

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        foreach (Renderer areaRenderer in renderers)
        {
            if (areaRenderer == null || areaRenderer.transform == transform)
            {
                continue;
            }

            Bounds bounds = areaRenderer.bounds;
            if (bounds.size.x <= 0.01f || bounds.size.z <= 0.01f)
            {
                continue;
            }

            areas.Add(new WalkArea(bounds));
        }
    }

    private void AddPathSegment(List<Vector3> route, Vector3 start, Vector3 target)
    {
        List<int> startIndexes = FindAreaCandidates(start);
        List<int> targetIndexes = FindAreaCandidates(target);
        List<int> areaPath = FindBestAreaPath(startIndexes, targetIndexes);
        if (areaPath.Count == 0)
        {
            route.Add(target);
            return;
        }

        if (areaPath.Count <= 1)
        {
            route.Add(target);
            return;
        }

        for (int i = 0; i < areaPath.Count - 1; i++)
        {
            WalkArea fromArea = areas[areaPath[i]];
            WalkArea toArea = areas[areaPath[i + 1]];

            // Move to the edge of the current area, then to the entry point of the next
            // area. This keeps customers following the authored Area path instead of aiming
            // straight at the final destination through parking stalls or rails.
            AddRoutePointIfUseful(route, fromArea.GetTransitionPointTo(toArea, target.y, edgePadding));
            AddRoutePointIfUseful(route, toArea.GetTransitionPointTo(fromArea, target.y, edgePadding));
        }

        AddRoutePointIfUseful(route, target);
    }

    private void AddRoutePointIfUseful(List<Vector3> route, Vector3 point)
    {
        if (route.Count > 0 && Vector3.ProjectOnPlane(route[route.Count - 1] - point, Vector3.up).sqrMagnitude <= 0.04f)
        {
            return;
        }

        route.Add(point);
    }

    private List<int> FindAreaPath(int startIndex, int targetIndex)
    {
        if (startIndex == targetIndex)
        {
            return new List<int> { startIndex };
        }

        int[] previous = new int[areas.Count];
        Queue<int> frontier = new Queue<int>();
        for (int i = 0; i < previous.Length; i++)
        {
            previous[i] = -1;
        }

        previous[startIndex] = startIndex;
        frontier.Enqueue(startIndex);

        while (frontier.Count > 0)
        {
            int current = frontier.Dequeue();
            for (int i = 0; i < areas.Count; i++)
            {
                if (previous[i] >= 0 || !areas[current].CanConnectTo(areas[i], connectionTolerance))
                {
                    continue;
                }

                previous[i] = current;
                if (i == targetIndex)
                {
                    return RebuildPath(previous, startIndex, targetIndex);
                }

                frontier.Enqueue(i);
            }
        }

        return new List<int>();
    }

    private List<int> RebuildPath(int[] previous, int startIndex, int targetIndex)
    {
        List<int> path = new List<int>();
        int current = targetIndex;
        while (current != startIndex)
        {
            path.Add(current);
            current = previous[current];
            if (current < 0)
            {
                return new List<int>();
            }
        }

        path.Add(startIndex);
        path.Reverse();
        return path;
    }

    private int FindContainingAreaIndex(Vector3 position)
    {
        for (int i = 0; i < areas.Count; i++)
        {
            if (areas[i].Contains(position))
            {
                return i;
            }
        }

        return -1;
    }

    private List<int> FindContainingAreaIndexes(Vector3 position)
    {
        List<int> indexes = new List<int>();
        for (int i = 0; i < areas.Count; i++)
        {
            if (areas[i].Contains(position))
            {
                indexes.Add(i);
            }
        }

        return indexes;
    }

    private List<int> FindAreaCandidates(Vector3 position)
    {
        List<int> candidates = FindContainingAreaIndexes(position);
        if (candidates.Count > 0)
        {
            return candidates;
        }

        int closestAreaIndex = FindClosestAreaIndex(position);
        if (closestAreaIndex >= 0)
        {
            candidates.Add(closestAreaIndex);
        }

        return candidates;
    }

    private int FindConnectedAreaIndex(IReadOnlyList<int> fromAreaIndexes, IReadOnlyList<int> toAreaIndexes)
    {
        for (int i = 0; i < toAreaIndexes.Count; i++)
        {
            int toAreaIndex = toAreaIndexes[i];
            for (int j = 0; j < fromAreaIndexes.Count; j++)
            {
                int fromAreaIndex = fromAreaIndexes[j];
                if (FindAreaPath(fromAreaIndex, toAreaIndex).Count > 0)
                {
                    return toAreaIndex;
                }
            }
        }

        return -1;
    }

    private int FindBestClampAreaIndex(IReadOnlyList<int> areaIndexes, Vector3 desiredPosition)
    {
        int bestAreaIndex = areaIndexes[0];
        float bestSqrDistance = float.PositiveInfinity;
        for (int i = 0; i < areaIndexes.Count; i++)
        {
            int areaIndex = areaIndexes[i];
            Vector3 clampedPosition = areas[areaIndex].Clamp(desiredPosition, edgePadding);
            float sqrDistance = Vector3.ProjectOnPlane(clampedPosition - desiredPosition, Vector3.up).sqrMagnitude;
            if (sqrDistance < bestSqrDistance)
            {
                bestSqrDistance = sqrDistance;
                bestAreaIndex = areaIndex;
            }
        }

        return bestAreaIndex;
    }

    private List<int> FindBestAreaPath(IReadOnlyList<int> startIndexes, IReadOnlyList<int> targetIndexes)
    {
        List<int> bestPath = new List<int>();
        for (int i = 0; i < startIndexes.Count; i++)
        {
            for (int j = 0; j < targetIndexes.Count; j++)
            {
                List<int> path = FindAreaPath(startIndexes[i], targetIndexes[j]);
                if (path.Count == 0)
                {
                    continue;
                }

                if (bestPath.Count == 0 || path.Count < bestPath.Count)
                {
                    bestPath = path;
                }
            }
        }

        return bestPath;
    }

    private int FindClosestAreaIndex(Vector3 position)
    {
        int closestIndex = -1;
        float closestSqrDistance = float.PositiveInfinity;
        for (int i = 0; i < areas.Count; i++)
        {
            Vector3 closestPoint = areas[i].Clamp(position, 0f);
            float sqrDistance = Vector3.ProjectOnPlane(closestPoint - position, Vector3.up).sqrMagnitude;
            if (sqrDistance < closestSqrDistance)
            {
                closestSqrDistance = sqrDistance;
                closestIndex = i;
            }
        }

        return closestIndex;
    }

    private Vector3[] CopyRoute(IReadOnlyList<Vector3> requestedRoute)
    {
        Vector3[] route = new Vector3[requestedRoute.Count];
        for (int i = 0; i < requestedRoute.Count; i++)
        {
            route[i] = requestedRoute[i];
        }

        return route;
    }

    private readonly struct WalkArea
    {
        private readonly Bounds bounds;
        private readonly Transform boxTransform;
        private readonly Vector3 boxCenter;
        private readonly Vector3 boxHalfSize;
        private readonly bool usesBoxCollider;

        public WalkArea(Bounds bounds)
        {
            this.bounds = bounds;
            boxTransform = null;
            boxCenter = Vector3.zero;
            boxHalfSize = Vector3.zero;
            usesBoxCollider = false;
        }

        public WalkArea(BoxCollider boxCollider)
        {
            bounds = boxCollider.bounds;
            boxTransform = boxCollider.transform;
            boxCenter = boxCollider.center;
            boxHalfSize = boxCollider.size * 0.5f;
            usesBoxCollider = true;
        }

        public bool Contains(Vector3 position)
        {
            if (usesBoxCollider)
            {
                Vector3 localPosition = boxTransform.InverseTransformPoint(position) - boxCenter;
                return Mathf.Abs(localPosition.x) <= boxHalfSize.x && Mathf.Abs(localPosition.z) <= boxHalfSize.z;
            }

            return position.x >= bounds.min.x && position.x <= bounds.max.x && position.z >= bounds.min.z && position.z <= bounds.max.z;
        }

        public Vector3 Clamp(Vector3 position, float padding)
        {
            if (usesBoxCollider)
            {
                float paddedHalfX = Mathf.Max(0f, boxHalfSize.x - padding);
                float paddedHalfZ = Mathf.Max(0f, boxHalfSize.z - padding);
                Vector3 localPosition = boxTransform.InverseTransformPoint(position) - boxCenter;
                localPosition.x = Mathf.Clamp(localPosition.x, -paddedHalfX, paddedHalfX);
                localPosition.z = Mathf.Clamp(localPosition.z, -paddedHalfZ, paddedHalfZ);

                Vector3 clampedPosition = boxTransform.TransformPoint(localPosition + boxCenter);
                clampedPosition.y = position.y;
                return clampedPosition;
            }

            return new Vector3(
                Mathf.Clamp(position.x, bounds.min.x + padding, bounds.max.x - padding),
                position.y,
                Mathf.Clamp(position.z, bounds.min.z + padding, bounds.max.z - padding));
        }

        public Vector3 CenterAtHeight(float y)
        {
            return new Vector3(bounds.center.x, y, bounds.center.z);
        }

        public Vector3 GetTransitionPointTo(WalkArea other, float y, float padding)
        {
            Vector3 point = Clamp(other.CenterAtHeight(y), padding);
            point.y = y;
            return point;
        }

        public bool CanConnectTo(WalkArea other, float tolerance)
        {
            return OverlapsOrTouches(bounds.min.x, bounds.max.x, other.bounds.min.x, other.bounds.max.x, tolerance)
                && OverlapsOrTouches(bounds.min.z, bounds.max.z, other.bounds.min.z, other.bounds.max.z, tolerance);
        }

        private bool OverlapsOrTouches(float minA, float maxA, float minB, float maxB, float tolerance)
        {
            return minA <= maxB + tolerance && maxA + tolerance >= minB;
        }
    }
}
