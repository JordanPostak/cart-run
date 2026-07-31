using UnityEngine;

internal static class CartPositionUtility
{
    public static Vector3 GetAveragePosition(Transform[] transforms, Transform fallback, Vector3 fallbackPosition)
    {
        if (transforms == null || transforms.Length == 0)
        {
            return fallback != null ? fallback.position : fallbackPosition;
        }

        Vector3 position = Vector3.zero;
        int count = 0;
        foreach (Transform point in transforms)
        {
            if (point == null)
            {
                continue;
            }

            position += point.position;
            count++;
        }

        return count > 0 ? position / count : fallbackPosition;
    }
}
