using UnityEngine;

internal static class CartTransformUtility
{
    public static Transform[] FindChildrenStartingWith(Transform parent, string namePrefix)
    {
        int count = 0;
        foreach (Transform child in parent)
        {
            if (child.name.StartsWith(namePrefix))
            {
                count++;
            }
        }

        Transform[] matches = new Transform[count];
        int index = 0;
        foreach (Transform child in parent)
        {
            if (child.name.StartsWith(namePrefix))
            {
                matches[index] = child;
                index++;
            }
        }

        return matches;
    }

    public static Transform FindChildRecursive(Transform parent, string childName)
    {
        foreach (Transform child in parent)
        {
            if (child.name == childName)
            {
                return child;
            }

            Transform match = FindChildRecursive(child, childName);
            if (match != null)
            {
                return match;
            }
        }

        return null;
    }

    public static Quaternion[] CacheLocalRotations(Transform[] pivots)
    {
        if (pivots == null)
        {
            return new Quaternion[0];
        }

        Quaternion[] rotations = new Quaternion[pivots.Length];
        for (int i = 0; i < pivots.Length; i++)
        {
            rotations[i] = pivots[i] != null ? pivots[i].localRotation : Quaternion.identity;
        }

        return rotations;
    }
}
