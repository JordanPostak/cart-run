using System.Collections.Generic;
using UnityEngine;

public class CustomerCartSpawner : MonoBehaviour
{
    [Header("Prefabs")]
    [SerializeField] private CustomerCartPusher customerPrefab;
    [SerializeField] private CartController cartPrefab;
    [SerializeField] private Vector3 spawnedCartScale = Vector3.one;

    [Header("Spawn Points")]
    [SerializeField] private Transform[] doorwaySpawnPoints;
    [SerializeField] private bool snapSpawnPointToGround;
    [SerializeField] private float initialCartSpawnClearance = 2f;
    [SerializeField] private LayerMask spawnGroundLayers = -1;
    [SerializeField] private float spawnGroundRayHeight = 5f;
    [SerializeField] private float spawnGroundRayDistance = 20f;
    [SerializeField] private Transform parkingLotDestinationCenter;
    [SerializeField] private Vector2 parkingLotDestinationSize = new Vector2(40f, 40f);
    [SerializeField] private Transform[] explicitDestinationPoints;
    [SerializeField] private Transform roadWaypointRoot;

    [Header("Walk Areas")]
    [SerializeField] private Transform walkAreaRoot;
    [SerializeField] private CustomerWalkAreaMap walkAreaMap;

    [Header("Timing")]
    [SerializeField] private bool spawnOnStart = true;
    [SerializeField] private bool keepSpawning = true;
    [SerializeField] private int maxActiveCustomers = 3;
    [SerializeField] private float spawnInterval = 8f;

    [Header("Parked Cart Stacking")]
    [SerializeField] private bool stackParkedCartsAtDropoff = true;
    [SerializeField] private float parkedCartStackRadius = 2.2f;
    [SerializeField] private float parkedCartStackAlignmentDot = 0.65f;

    private readonly List<CustomerCartPusher> activeCustomers = new List<CustomerCartPusher>();
    private readonly List<CartController> parkedCustomerCarts = new List<CartController>();
    private float nextSpawnTime;

    private void Start()
    {
        ResolveWalkAreaMap();
        nextSpawnTime = Time.time + spawnInterval;
        if (spawnOnStart)
        {
            SpawnCustomer();
        }
    }

    private void Update()
    {
        PruneInactiveCustomers();
        if (!keepSpawning || Time.time < nextSpawnTime || activeCustomers.Count >= maxActiveCustomers)
        {
            return;
        }

        SpawnCustomer();
        nextSpawnTime = Time.time + spawnInterval;
    }

    [ContextMenu("Spawn Customer")]
    public void SpawnCustomer()
    {
        if (customerPrefab == null || cartPrefab == null || doorwaySpawnPoints == null || doorwaySpawnPoints.Length == 0)
        {
            Debug.LogWarning($"{nameof(CustomerCartSpawner)} needs a customer prefab, cart prefab, and at least one doorway spawn point.", this);
            return;
        }

        Transform spawnPoint = doorwaySpawnPoints[Random.Range(0, doorwaySpawnPoints.Length)];
        if (spawnPoint == null)
        {
            return;
        }

        Vector3 spawnPosition = GetGroundedPosition(spawnPoint.position);
        CustomerCartPusher customer = Instantiate(customerPrefab, spawnPosition, spawnPoint.rotation);
        Vector3 cartSpawnPosition = spawnPosition + spawnPoint.forward * initialCartSpawnClearance;
        CartController cart = Instantiate(cartPrefab, cartSpawnPosition, spawnPoint.rotation);
        cart.transform.localScale = spawnedCartScale;

        customer.Initialize(cart, GetConstrainedRoute(spawnPosition, GetRandomDestinationRoute(spawnPoint)), this, walkAreaMap);
        activeCustomers.Add(customer);
    }

    public void RegisterParkedCustomerCart(CartController parkedCart)
    {
        if (parkedCart == null)
        {
            return;
        }

        PruneInactiveParkedCarts();
        TryStackWithNearbyParkedCart(parkedCart);
        parkedCustomerCarts.Add(parkedCart);
    }

    private bool TryStackWithNearbyParkedCart(CartController parkedCart)
    {
        if (!stackParkedCartsAtDropoff)
        {
            return false;
        }

        CartController targetLeader = FindNearbyParkedStackLeader(parkedCart);
        if (targetLeader == null || !targetLeader.TryAppendCartToRow(parkedCart))
        {
            return false;
        }

        targetLeader.ParkAsDormant();
        return true;
    }

    private CartController FindNearbyParkedStackLeader(CartController parkedCart)
    {
        CartController closestLeader = null;
        float closestSqrDistance = parkedCartStackRadius * parkedCartStackRadius;
        Vector3 parkedPosition = parkedCart.transform.position;
        Vector3 parkedForward = Vector3.ProjectOnPlane(parkedCart.transform.forward, Vector3.up).normalized;

        foreach (CartController candidate in parkedCustomerCarts)
        {
            if (candidate == null || candidate == parkedCart || candidate.IsGrabbed)
            {
                continue;
            }

            CartController candidateLeader = candidate.GetRowGrabLeader();
            if (candidateLeader == null || candidateLeader == parkedCart || candidateLeader.IsGrabbed)
            {
                continue;
            }

            Vector3 candidateForward = Vector3.ProjectOnPlane(candidate.transform.forward, Vector3.up).normalized;
            if (parkedForward.sqrMagnitude > 0.001f && candidateForward.sqrMagnitude > 0.001f && Mathf.Abs(Vector3.Dot(parkedForward, candidateForward)) < parkedCartStackAlignmentDot)
            {
                continue;
            }

            Vector3 offset = Vector3.ProjectOnPlane(candidate.transform.position - parkedPosition, Vector3.up);
            float sqrDistance = offset.sqrMagnitude;
            if (sqrDistance >= closestSqrDistance)
            {
                continue;
            }

            closestSqrDistance = sqrDistance;
            closestLeader = candidateLeader;
        }

        return closestLeader;
    }

    private Vector3[] GetRandomDestinationRoute(Transform spawnPoint)
    {
        if (explicitDestinationPoints != null && explicitDestinationPoints.Length > 0)
        {
            Transform destination = explicitDestinationPoints[Random.Range(0, explicitDestinationPoints.Length)];
            if (destination != null)
            {
                return BuildRoute(spawnPoint, destination);
            }
        }

        Vector3 center = parkingLotDestinationCenter != null ? parkingLotDestinationCenter.position : transform.position;
        Vector2 randomOffset = new Vector2(
            Random.Range(parkingLotDestinationSize.x * -0.5f, parkingLotDestinationSize.x * 0.5f),
            Random.Range(parkingLotDestinationSize.y * -0.5f, parkingLotDestinationSize.y * 0.5f));

        Vector3 destinationPosition = center + new Vector3(randomOffset.x, 0f, randomOffset.y);
        List<Vector3> route = new List<Vector3>();
        AddRoadRoute(route, spawnPoint.position, destinationPosition);
        route.Add(destinationPosition);
        return route.ToArray();
    }

    private Vector3[] GetConstrainedRoute(Vector3 spawnPosition, Vector3[] route)
    {
        if (walkAreaMap == null || !walkAreaMap.HasAreas)
        {
            return route;
        }

        return walkAreaMap.BuildRoute(spawnPosition, route);
    }

    private Vector3[] BuildRoute(Transform spawnPoint, Transform destination)
    {
        List<Vector3> route = new List<Vector3>();

        // Add child empties under a spawn point for "leave the doorway/hallway" steps.
        AddChildWaypoints(route, spawnPoint);

        // Add child empties under Road Waypoint Root to keep customers on the driving lanes
        // instead of taking a straight shortcut across parking spaces or corral rails.
        AddRoadRoute(route, spawnPoint.position, destination.position);

        // Add child empties under each destination for "enter the corral through the mouth" steps.
        AddChildWaypoints(route, destination);
        route.Add(destination.position);
        return route.ToArray();
    }

    private void AddChildWaypoints(List<Vector3> route, Transform waypointParent)
    {
        if (waypointParent == null)
        {
            return;
        }

        for (int i = 0; i < waypointParent.childCount; i++)
        {
            route.Add(waypointParent.GetChild(i).position);
        }
    }

    private void AddRoadRoute(List<Vector3> route, Vector3 routeStart, Vector3 routeEnd)
    {
        if (roadWaypointRoot == null || roadWaypointRoot.childCount == 0)
        {
            return;
        }

        int startIndex = GetClosestRoadWaypointIndex(routeStart);
        int endIndex = GetClosestRoadWaypointIndex(routeEnd);
        if (startIndex < 0 || endIndex < 0)
        {
            return;
        }

        int step = startIndex <= endIndex ? 1 : -1;
        for (int i = startIndex; i != endIndex + step; i += step)
        {
            route.Add(roadWaypointRoot.GetChild(i).position);
        }
    }

    private int GetClosestRoadWaypointIndex(Vector3 position)
    {
        int closestIndex = -1;
        float closestSqrDistance = float.PositiveInfinity;
        for (int i = 0; i < roadWaypointRoot.childCount; i++)
        {
            Transform waypoint = roadWaypointRoot.GetChild(i);
            Vector3 offset = Vector3.ProjectOnPlane(waypoint.position - position, Vector3.up);
            float sqrDistance = offset.sqrMagnitude;
            if (sqrDistance < closestSqrDistance)
            {
                closestSqrDistance = sqrDistance;
                closestIndex = i;
            }
        }

        return closestIndex;
    }

    private Vector3 GetGroundedPosition(Vector3 position)
    {
        if (!snapSpawnPointToGround)
        {
            return position;
        }

        Vector3 rayStart = position + Vector3.up * spawnGroundRayHeight;
        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, spawnGroundRayDistance, spawnGroundLayers, QueryTriggerInteraction.Ignore))
        {
            position.y = hit.point.y;
        }

        return position;
    }

    private void ResolveWalkAreaMap()
    {
        if (walkAreaMap != null)
        {
            return;
        }

        if (walkAreaRoot == null)
        {
            GameObject walkAreas = GameObject.Find("CustomerWalkAreas");
            if (walkAreas != null)
            {
                walkAreaRoot = walkAreas.transform;
            }
        }

        if (walkAreaRoot == null)
        {
            Debug.LogWarning($"{nameof(CustomerCartSpawner)} could not find CustomerWalkAreas, so spawned customers will not be confined to walk areas.", this);
            return;
        }

        walkAreaMap = walkAreaRoot.GetComponent<CustomerWalkAreaMap>();
        if (walkAreaMap == null)
        {
            walkAreaMap = walkAreaRoot.gameObject.AddComponent<CustomerWalkAreaMap>();
        }

        if (!walkAreaMap.HasAreas)
        {
            Debug.LogWarning($"{nameof(CustomerWalkAreaMap)} on {walkAreaRoot.name} did not find any child BoxColliders/Renderers to use as customer walk areas.", walkAreaRoot);
        }
    }

    private void PruneInactiveCustomers()
    {
        for (int i = activeCustomers.Count - 1; i >= 0; i--)
        {
            if (activeCustomers[i] == null)
            {
                activeCustomers.RemoveAt(i);
            }
        }
    }

    private void PruneInactiveParkedCarts()
    {
        for (int i = parkedCustomerCarts.Count - 1; i >= 0; i--)
        {
            if (parkedCustomerCarts[i] == null)
            {
                parkedCustomerCarts.RemoveAt(i);
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 center = parkingLotDestinationCenter != null ? parkingLotDestinationCenter.position : transform.position;
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.25f);
        Gizmos.DrawCube(center, new Vector3(parkingLotDestinationSize.x, 0.1f, parkingLotDestinationSize.y));
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 1f);
        Gizmos.DrawWireCube(center, new Vector3(parkingLotDestinationSize.x, 0.1f, parkingLotDestinationSize.y));

        if (explicitDestinationPoints == null)
        {
            return;
        }

        Gizmos.color = new Color(1f, 0.65f, 0.1f, 1f);
        foreach (Transform destination in explicitDestinationPoints)
        {
            if (destination == null)
            {
                continue;
            }

            Vector3 previous = transform.position;
            Vector3[] route = BuildRoute(transform, destination);
            for (int i = 0; i < route.Length; i++)
            {
                Gizmos.DrawLine(previous, route[i]);
                Gizmos.DrawSphere(route[i], 0.25f);
                previous = route[i];
            }
        }
    }
}
