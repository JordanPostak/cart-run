using UnityEngine;
using UnityEngine.Serialization;

public class ParkingLotLayoutGenerator : MonoBehaviour
{
    [Header("Layout Counts")]
    [SerializeField] private int rows = 4;
    [SerializeField] private int spacesPerRow = 8;

    [Header("Parking Space Dimensions")]
    [SerializeField] private float spaceWidth = 6f;
    [SerializeField] private float spaceLength = 10f;

    [Header("Aisles and Walkways")]
    [SerializeField] private float drivingAisleWidth = 7.5f;
    [SerializeField] private float pedestrianWalkwayDepth = 8.0f;

    [Header("Cart Corrals")]
    [SerializeField] private int cartCorralCount = 2;
    [SerializeField] private float cartCorralOffset = 6.0f;

    [Header("Surface Offset")]
    [SerializeField] private float surfaceOffset = 0.01f;

    [Header("Parking Space Prefab")]
    [FormerlySerializedAs("doubleParkingDividerPrefab")]
    [SerializeField] private GameObject parkingSpacePrefab;

    private const string LayoutRootName = "LayoutRoot";

    [ContextMenu("Generate Layout")]
    public void GenerateLayout()
    {
        if (Application.isPlaying)
        {
            Debug.LogWarning("ParkingLotLayoutGenerator: Layout generation is disabled while the game is running.");
            return;
        }

        if (rows <= 0 || spacesPerRow <= 0)
        {
            Debug.LogWarning("ParkingLotLayoutGenerator: Rows and spaces per row must be greater than zero.");
            return;
        }

        ClearLayout();

        GameObject layoutRoot = CreateLayoutRoot();
        CreateRows(layoutRoot.transform);
        CreateAisles(layoutRoot.transform);
        CreateWalkway(layoutRoot.transform);
        CreateCartCorralLocations(layoutRoot.transform);
    }

    [ContextMenu("Clear Layout")]
    public void ClearLayout()
    {
        if (Application.isPlaying)
        {
            Debug.LogWarning("ParkingLotLayoutGenerator: Clearing the layout is disabled while the game is running.");
            return;
        }

        Transform layoutRoot = transform.Find(LayoutRootName);
        if (layoutRoot != null)
        {
            DestroyImmediate(layoutRoot.gameObject);
        }
    }

    private GameObject CreateLayoutRoot()
    {
        GameObject root = new GameObject(LayoutRootName);
        root.transform.SetParent(transform, false);
        root.transform.localPosition = Vector3.zero;
        root.transform.localRotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;
        return root;
    }

    private void CreateRows(Transform layoutRoot)
    {
        GameObject rowsParent = CreateEmptyMarker("ParkingRows", layoutRoot, Vector3.zero);

        for (int rowIndex = 0; rowIndex < rows; rowIndex++)
        {
            int pairIndex = rowIndex / 2;
            bool isSecondRowInPair = rowIndex % 2 == 1;

            float firstRowZ = pairIndex * ((spaceLength * 2f) + drivingAisleWidth);
            float rowZ = isSecondRowInPair ? firstRowZ + spaceLength + drivingAisleWidth : firstRowZ;

            float rowRotationY = isSecondRowInPair ? 180f : 0f;

            Vector3 rowPosition = new Vector3(0f, 0f, rowZ);
            GameObject rowParent = CreateEmptyMarker($"ParkingRow_{rowIndex + 1}", rowsParent.transform, rowPosition);
            rowParent.transform.localRotation = Quaternion.Euler(0f, rowRotationY, 0f);

            CreateParkingSpaces(rowParent.transform);
        }
    }

    private void CreateParkingSpaces(Transform rowParent)
    {
        float startX = -((spacesPerRow - 1) * spaceWidth) / 2f;

        for (int spaceIndex = 0; spaceIndex < spacesPerRow; spaceIndex++)
        {
            float spaceX = startX + (spaceIndex * spaceWidth);
            Vector3 spacePosition = new Vector3(spaceX, surfaceOffset, 0f);

            CreateParkingSpace(rowParent, spacePosition, $"ParkingSpace_{spaceIndex + 1}");
        }
    }

    private void CreateParkingSpace(Transform parent, Vector3 localPosition, string objectName)
    {
        if (parkingSpacePrefab != null)
        {
            GameObject parkingSpace = Instantiate(parkingSpacePrefab, parent, false);
            parkingSpace.name = objectName;
            parkingSpace.transform.localPosition = localPosition;
            parkingSpace.transform.localRotation = Quaternion.identity;
            parkingSpace.transform.localScale = Vector3.one;
            return;
        }

        GameObject fallbackSpace = new GameObject(objectName);
        fallbackSpace.transform.SetParent(parent, false);
        fallbackSpace.transform.localPosition = localPosition;
        fallbackSpace.transform.localRotation = Quaternion.identity;
        fallbackSpace.transform.localScale = Vector3.one;
    }

    private void CreateAisles(Transform layoutRoot)
    {
        GameObject aislesParent = CreateEmptyMarker("DrivingAisles", layoutRoot, Vector3.zero);

        int pairCount = Mathf.CeilToInt(rows / 2f);

        for (int pairIndex = 0; pairIndex < pairCount; pairIndex++)
        {
            float firstRowZ = pairIndex * ((spaceLength * 2f) + drivingAisleWidth);
            float aisleZ = firstRowZ + (spaceLength + drivingAisleWidth) / 2f;

            CreateEmptyMarker($"DrivingAisle_{pairIndex + 1}", aislesParent.transform, new Vector3(0f, 0f, aisleZ));
        }
    }

    private void CreateWalkway(Transform layoutRoot)
    {
        GameObject walkwayParent = CreateEmptyMarker("PedestrianWalkway", layoutRoot, Vector3.zero);

        Vector3 walkwayPosition = new Vector3(0f, 0f, -pedestrianWalkwayDepth);
        CreateEmptyMarker("Walkway_Marker", walkwayParent.transform, walkwayPosition);
    }

    private void CreateCartCorralLocations(Transform layoutRoot)
    {
        GameObject cartCorralsParent = CreateEmptyMarker("CartCorralLocations", layoutRoot, Vector3.zero);

        for (int corralIndex = 0; corralIndex < cartCorralCount; corralIndex++)
        {
            float zOffset = -cartCorralOffset - (corralIndex * (cartCorralOffset * 0.5f));
            Vector3 corralPosition = new Vector3(0f, 0f, zOffset);

            CreateEmptyMarker($"CartCorral_{corralIndex + 1}", cartCorralsParent.transform, corralPosition);
        }
    }

    private GameObject CreateEmptyMarker(string name, Transform parent, Vector3 localPosition)
    {
        GameObject marker = new GameObject(name);
        marker.transform.SetParent(parent, false);
        marker.transform.localPosition = localPosition;
        marker.transform.localRotation = Quaternion.identity;
        marker.transform.localScale = Vector3.one;
        return marker;
    }
}