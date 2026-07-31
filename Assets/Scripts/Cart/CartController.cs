using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public partial class CartController : MonoBehaviour
{
    [Header("Rigidbody Setup")]
    [SerializeField] private bool configureRigidbodyOnAwake = true;
    [SerializeField] private float cartMass = 35f;
    [SerializeField] private Vector3 centerOfMassOffset = new Vector3(-1.05f, -0.2f, 0.72f);
    [SerializeField] private float linearDamping = 0.015f;
    [SerializeField] private float angularDamping = 0.025f;
    [SerializeField] private bool keepCartUpright = true;
    [SerializeField] private bool keepCartPlanted = true;

    [Header("Collision Shape")]
    [SerializeField] private bool configureMeshCollidersOnAwake = true;
    [SerializeField] private bool forceConvexMeshColliders = true;
    [SerializeField] private Vector3 fallbackHandleLocalPoint = new Vector3(-1.05f, 0.45f, 0.72f);

    [Header("Rolling Feel")]
    [SerializeField] private float maxRollingSpeed = 8f;
    [SerializeField] private float rollingResistance = 0.05f;
    [SerializeField] private float lowSpeedBrake = 0.012f;
    [SerializeField] private float rearWheelSideGrip = 9f;
    [SerializeField] private float frontCasterSideGrip = 0.35f;
    [SerializeField] private float casterYawAssist = 0.4f;
    [SerializeField] private float forwardPushMultiplier = 1.65f;
    [SerializeField] private float sidePivotMomentumGain = 0.004f;
    [SerializeField] private float sidePivotMaxMomentum = 3.2f;
    [SerializeField] private float sidePivotTurnSpeed = 95f;
    [SerializeField] private float sidePivotRollSpeed = 1.15f;
    [SerializeField] private float sidePivotAlignmentToRoll = 0.72f;
    [SerializeField] private float sidePivotMomentumDamping = 0.9f;
    [SerializeField] private float forwardRollMomentumGain = 0.0035f;
    [SerializeField] private float forwardRollMaxMomentum = 4.2f;
    [SerializeField] private float forwardRollSpeed = 1.35f;
    [SerializeField] private float forwardRollMomentumDamping = 0.75f;
    [SerializeField] private float rollingAlignmentMinSpeed = 0.25f;
    [SerializeField] private float rollingAlignmentTurnSpeed = 55f;
    [SerializeField] private float rollingAlignmentSideGrip = 5f;
    [SerializeField] private float uprightStrength = 14f;
    [SerializeField] private float uprightDamping = 2.8f;
    [SerializeField] private float idleSleepDelay = 3.5f;
    [SerializeField] private float idleSpeedThreshold = 0.01f;
    [SerializeField] private float idleAngularSpeedThreshold = 0.01f;

    [Header("Nested Cart Rows")]
    [SerializeField] private bool enableNestedCartRows = true;
    [SerializeField] private float nestedRowScanDistance = 2f;
    [SerializeField] private float nestedRowLateralTolerance = 0.9f;
    [SerializeField] private float nestedRowAlignmentDot = 0.82f;
    [SerializeField] private float nestedRowAttachDistance = 0.25f;
    [SerializeField] private float nestedRowDetachDistance = 0.25f;
    [SerializeField] private bool pullNestedCartIntoPlace = true;
    [SerializeField] private float nestedRowStepDistance = 0.38f;
    [SerializeField] private float nestedRowSlotSpacing = 0.28f;
    [SerializeField] private float nestedRowOverlapDistance = 1.25f;
    [SerializeField] private float nestedRowPullForwardOffset = 0.55f;
    [SerializeField] private float nestedRowExtraCartWeight = 0.8f;
    [SerializeField] private float nestedRowCenterPivotBlend = 1f;
    [SerializeField] private bool ignoreNestedCartCollisions = true;
    [SerializeField] private float nestedCollisionRefreshInterval = 0.25f;

    [Header("Tipping")]
    [SerializeField] private bool allowTipOver = true;
    [SerializeField] private float playerTipPushForce = 520f;
    [SerializeField] private float playerTipPushSpeed = 5.5f;
    [SerializeField] private float playerTipPushTime = 1.4f;
    [SerializeField] private float impactTipSpeed = 14f;
    [SerializeField] private float impactTipMomentum = 80f;
    [SerializeField] private float tipTorqueImpulse = 32f;
    [SerializeField] private float tippedLinearDamping = 1.4f;
    [SerializeField] private float tippedAngularDamping = 1.2f;
    [SerializeField] private float tippedRollingResistanceMultiplier = 6f;

    [Header("Handle Control")]
    [SerializeField] private Transform handleAnchor;
    [SerializeField] private Transform cartGrabPoint;
    [SerializeField] private float grabDistance = 3f;
    [SerializeField] private float grabSideDot = -0.2f;
    [SerializeField] private float grabbedDriveSpeed = 3.8f;
    [SerializeField] private float grabbedTurnSpeed = 85f;
    [SerializeField] private float playerHandleSpacing = 0.72f;
    [SerializeField] private float playerHandleLateralOffset = 0f;
    [SerializeField] private float playerTurnSideStep = 0.35f;
    [SerializeField] private float handleGripWidth = 0.55f;

    [Header("Wheel Visuals")]
    [SerializeField] private Transform frontWheelAxle;
    [SerializeField] private Transform rearWheelAxle;
    [SerializeField] private Transform[] frontCasterPivots;
    [SerializeField] private Transform[] rearWheelPivots;
    [SerializeField] private float maxCasterAngle = 180f;
    [SerializeField] private float casterResponse = 12f;
    [SerializeField] private float casterTrailYawOffset = 180f;
    [SerializeField] private float wheelVisualSpeedThreshold = 0.15f;

    private Rigidbody rb;
    private Renderer[] handleRenderers;
    private float currentCasterAngle;
    private float plantedY;
    private bool isTipped;
    private PlayerController grabbedPlayer;
    private Vector2 grabbedInput;
    private bool hasGrabberFollowTarget;
    private Vector3 grabberFollowPosition;
    private Quaternion grabberFollowRotation = Quaternion.identity;
    private Vector3 currentGrabbedPlayerPosition;
    private Quaternion currentGrabbedPlayerRotation = Quaternion.identity;
    private Quaternion[] frontCasterBaseRotations;
    private Quaternion[] rearWheelBaseRotations;
    private float lastExternalPushTime = -999f;
    private Vector3 sidePivotPushDirection;
    private float sidePivotMomentum;
    private Vector3 sidePivotWorldPoint;
    private bool hasSidePivotWorldPoint;
    private float forwardRollMomentum;
    private Collider[] cartColliders;
    private CartController rowLeader;
    private GameObject rowObject;
    private Transform originalParent;
    private bool wasKinematicBeforeRow;
    private Vector3 rowStartPosition;
    private Quaternion rowCartLocalRotation = Quaternion.identity;
    private readonly List<CartController> explicitRowCarts = new List<CartController>();
    private readonly List<CartController> ignoredNestedCollisionCarts = new List<CartController>();
    private float nextNestedCollisionRefreshTime;

    public bool IsTipped => isTipped;
    public bool IsGrabbed => grabbedPlayer != null;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        originalParent = transform.parent;
        plantedY = rb.position.y;

        if (configureRigidbodyOnAwake)
        {
            rb.mass = cartMass;
            rb.centerOfMass = centerOfMassOffset;
            rb.linearDamping = linearDamping;
            rb.angularDamping = angularDamping;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.maxAngularVelocity = 8f;
        }

        ApplyRotationConstraints();
        if (configureMeshCollidersOnAwake)
        {
            ConfigureMeshColliders();
        }

        cartColliders = GetComponentsInChildren<Collider>();

        if (frontWheelAxle == null)
        {
            frontWheelAxle = transform.Find("Wheel Front") ?? transform.Find("Wheel Front (1)");
        }

        if (rearWheelAxle == null)
        {
            rearWheelAxle = transform.Find("Wheel Rear") ?? transform.Find("Wheel Rear(1)");
        }

        if (handleAnchor == null)
        {
            handleAnchor = transform.Find("Handle");
        }

        if (cartGrabPoint == null)
        {
            cartGrabPoint = CartTransformUtility.FindChildRecursive(transform, "CartGrabPoint");
            if (cartGrabPoint == null)
            {
                Debug.LogWarning($"{nameof(CartController)} on {name} could not find a child named CartGrabPoint. Cart grabbing will fall back to the handle center.", this);
            }
        }

        if (handleAnchor != null)
        {
            handleRenderers = handleAnchor.GetComponentsInChildren<Renderer>();
        }

        if (frontCasterPivots == null || frontCasterPivots.Length == 0)
        {
            frontCasterPivots = CartTransformUtility.FindChildrenStartingWith(transform, "Wheel Front");
        }

        if (rearWheelPivots == null || rearWheelPivots.Length == 0)
        {
            rearWheelPivots = CartTransformUtility.FindChildrenStartingWith(transform, "Wheel Rear");
        }

        frontCasterBaseRotations = CartTransformUtility.CacheLocalRotations(frontCasterPivots);
        rearWheelBaseRotations = CartTransformUtility.CacheLocalRotations(rearWheelPivots);
    }

    private void OnDisable()
    {
        RestoreNestedCartCollisions();
    }

    private void ConfigureMeshColliders()
    {
        MeshCollider[] meshColliders = GetComponentsInChildren<MeshCollider>();
        foreach (MeshCollider meshCollider in meshColliders)
        {
            if (meshCollider == null)
            {
                continue;
            }

            meshCollider.enabled = true;
            meshCollider.isTrigger = false;
            if (forceConvexMeshColliders)
            {
                meshCollider.convex = true;
            }
        }
    }

    private void FixedUpdate()
    {
        if (rowLeader != null && rowLeader != this)
        {
            forwardRollMomentum = 0f;
            sidePivotMomentum = 0f;
            hasSidePivotWorldPoint = false;
            if (keepCartUpright)
            {
                EnforceUprightPose();
            }

            if (keepCartPlanted)
            {
                EnforcePlantedHeight();
            }

            return;
        }

        if (grabbedPlayer != null && !isTipped)
        {
            ApplyGrabberFollowTarget();
            if (keepCartUpright)
            {
                EnforceUprightPose();
            }

            if (keepCartPlanted)
            {
                EnforcePlantedHeight();
            }

            UpdateGrabbedPlayerPose(rb.position, rb.rotation);
            return;
        }

        if (!isTipped)
        {
            UpdateNestedCartCollisionIgnores();
            ApplySidePivotMotion();
            ApplyForwardRollMotion();
            ApplyCartWheelFriction();
            ApplyRollingAlignment();
        }

        ApplyRollingResistance();
        ApplySpeedLimit();
        if (!isTipped && keepCartUpright)
        {
            EnforceUprightPose();
        }
        else if (!isTipped)
        {
            ApplyUprightStability();
        }

        if (!isTipped && keepCartPlanted)
        {
            EnforcePlantedHeight();
        }

        StabilizeIdleCart();
    }

    private Vector3 GetFrontWheelCenter()
    {
        return CartPositionUtility.GetAveragePosition(frontCasterPivots, frontWheelAxle, transform.position + transform.forward * 0.55f);
    }

    private Vector3 GetRearWheelCenter()
    {
        return CartPositionUtility.GetAveragePosition(rearWheelPivots, rearWheelAxle, transform.position - transform.forward * 0.55f);
    }

    private void UpdateRowTransform(List<CartController> row)
    {
        if (rowObject == null)
        {
            return;
        }

        int rowCount = row != null ? row.Count : 0;
        Vector3[] worldPositions = new Vector3[rowCount];
        Quaternion[] worldRotations = new Quaternion[rowCount];
        for (int i = 0; i < rowCount; i++)
        {
            CartController cart = row[i];
            if (cart == null)
            {
                continue;
            }

            worldPositions[i] = cart.transform.position;
            worldRotations[i] = cart.transform.rotation;
        }

        rowStartPosition = rb.position;
        Vector3 rowForward = GetRowForward();
        if (rowForward.sqrMagnitude < 0.001f)
        {
            rowForward = rowObject.transform.forward;
        }

        rowObject.transform.SetPositionAndRotation(rowStartPosition, Quaternion.LookRotation(rowForward, Vector3.up));

        for (int i = 0; i < rowCount; i++)
        {
            CartController cart = row[i];
            if (cart == null)
            {
                continue;
            }

            cart.transform.SetPositionAndRotation(worldPositions[i], worldRotations[i]);
            cart.rb.position = worldPositions[i];
            cart.rb.rotation = worldRotations[i];
        }
    }

    private void ApplyRotationConstraints()
    {
        rb.constraints = keepCartUpright ? RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ : RigidbodyConstraints.None;
        if (keepCartPlanted)
        {
            rb.constraints |= RigidbodyConstraints.FreezePositionY;
        }
    }

    private void EnforceUprightPose()
    {
        ApplyRotationConstraints();

        Vector3 angularVelocity = rb.angularVelocity;
        rb.angularVelocity = new Vector3(0f, angularVelocity.y, 0f);

        float yaw = rb.rotation.eulerAngles.y;
        Quaternion uprightRotation = Quaternion.Euler(0f, yaw, 0f);
        if (Quaternion.Angle(rb.rotation, uprightRotation) > 0.01f)
        {
            rb.MoveRotation(uprightRotation);
        }
    }

    private void EnforcePlantedHeight()
    {
        Vector3 velocity = rb.linearVelocity;
        rb.linearVelocity = new Vector3(velocity.x, 0f, velocity.z);

        Vector3 position = rb.position;
        if (Mathf.Abs(position.y - plantedY) > 0.001f)
        {
            rb.position = new Vector3(position.x, plantedY, position.z);
        }
    }

}
