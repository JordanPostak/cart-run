using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class CartController : MonoBehaviour
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
    [SerializeField] private float nestedRowDetachDistance = 0.15f;
    [SerializeField] private bool pullNestedCartIntoPlace = true;
    [SerializeField] private float nestedRowStepDistance = 0.05f;
    [SerializeField] private float nestedRowSlotSpacing = 0.05f;
    [SerializeField] private float nestedRowOverlapDistance = 1.25f;
    [SerializeField] private float nestedRowPullForwardOffset = 0.05f;
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
            cartGrabPoint = FindChildRecursive(transform, "CartGrabPoint");
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
            frontCasterPivots = FindChildTransformsStartingWith("Wheel Front");
        }

        if (rearWheelPivots == null || rearWheelPivots.Length == 0)
        {
            rearWheelPivots = FindChildTransformsStartingWith("Wheel Rear");
        }

        frontCasterBaseRotations = CacheLocalRotations(frontCasterPivots);
        rearWheelBaseRotations = CacheLocalRotations(rearWheelPivots);
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

    private Transform[] FindChildTransformsStartingWith(string namePrefix)
    {
        int count = 0;
        foreach (Transform child in transform)
        {
            if (child.name.StartsWith(namePrefix))
            {
                count++;
            }
        }

        Transform[] matches = new Transform[count];
        int index = 0;
        foreach (Transform child in transform)
        {
            if (child.name.StartsWith(namePrefix))
            {
                matches[index] = child;
                index++;
            }
        }

        return matches;
    }

    private Transform FindChildRecursive(Transform parent, string childName)
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

    private Quaternion[] CacheLocalRotations(Transform[] pivots)
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

    public bool TryGrab(PlayerController player)
    {
        if (player == null || isTipped || grabbedPlayer != null || !IsPlayerOnHandleSide(player.GetGrabberPosition()))
        {
            return false;
        }

        grabbedPlayer = player;
        grabbedInput = Vector2.zero;
        hasGrabberFollowTarget = false;
        isTipped = false;
        keepCartUpright = true;
        keepCartPlanted = true;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.linearDamping = linearDamping;
        rb.angularDamping = angularDamping;
        ApplyRotationConstraints();
        UpdateGrabbedPlayerPose(rb.position, rb.rotation);
        grabbedPlayer.AttachToCart(this);
        return true;
    }

    public bool CanGrabFrom(Vector3 playerPosition)
    {
        return !isTipped && IsPlayerOnHandleSide(playerPosition);
    }

    public Vector3 GetCartGrabPointWorldPosition()
    {
        return GetCartGrabPointPosition();
    }

    public void ReleaseGrab(PlayerController player)
    {
        if (grabbedPlayer == player)
        {
            grabbedPlayer.DetachFromCart(this);
            grabbedPlayer = null;
            grabbedInput = Vector2.zero;
            hasGrabberFollowTarget = false;
        }
    }

    public bool IsGrabbedBy(PlayerController player)
    {
        return grabbedPlayer == player;
    }

    public void SetGrabInput(PlayerController player, Vector2 input)
    {
        if (grabbedPlayer == player)
        {
            grabbedInput = Vector2.ClampMagnitude(input, 1f);
        }
    }

    public void SetGrabberFollowTarget(PlayerController player, Vector3 grabberPosition, Quaternion grabberRotation, Vector2 input)
    {
        if (grabbedPlayer != player || isTipped)
        {
            return;
        }

        grabbedInput = Vector2.ClampMagnitude(input, 1f);
        grabberFollowPosition = grabberPosition;
        grabberFollowRotation = grabberRotation;
        hasGrabberFollowTarget = true;
    }

    private void ApplyGrabberFollowTarget()
    {
        if (!hasGrabberFollowTarget)
        {
            return;
        }

        Vector3 grabberForward = Vector3.ProjectOnPlane(grabberFollowRotation * Vector3.forward, Vector3.up);
        if (grabberForward.sqrMagnitude < 0.001f)
        {
            grabberForward = transform.forward;
        }

        grabberForward.Normalize();
        Quaternion nextRotation = Quaternion.LookRotation(grabberForward, Vector3.up);
        Vector3 grabPointOffset = GetCartGrabPointOffset(nextRotation);
        Vector3 nextPosition = grabberFollowPosition - grabPointOffset;
        nextPosition.y = plantedY;

        List<CartController> row = GetExplicitRow();
        if (row.Count > 1)
        {
            ApplyRowGrabberFollowTarget(row, nextPosition, nextRotation);
        }
        else
        {
            rb.MoveRotation(nextRotation);
            rb.MovePosition(nextPosition);
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        UpdateGrabbedPlayerPose(nextPosition, nextRotation);
    }

    private void ApplyRowGrabberFollowTarget(List<CartController> row, Vector3 nextLeaderPosition, Quaternion nextLeaderRotation)
    {
        Vector3 rowCenter = GetExplicitRowCenter(row);
        Quaternion deltaRotation = nextLeaderRotation * Quaternion.Inverse(rb.rotation);
        Vector3 rotatedLeaderPosition = rowCenter + deltaRotation * (rb.position - rowCenter);
        Vector3 deltaPosition = nextLeaderPosition - rotatedLeaderPosition;

        foreach (CartController cart in row)
        {
            if (cart == null || cart.isTipped)
            {
                continue;
            }

            Vector3 nextCartPosition = rowCenter + deltaRotation * (cart.rb.position - rowCenter) + deltaPosition;
            nextCartPosition.y = cart.plantedY;
            Quaternion nextCartRotation = deltaRotation * cart.rb.rotation;
            Vector3 euler = nextCartRotation.eulerAngles;
            Quaternion uprightRotation = Quaternion.Euler(0f, euler.y, 0f);

            if (cart.rb.isKinematic)
            {
                cart.rb.position = nextCartPosition;
                cart.rb.rotation = uprightRotation;
            }
            else
            {
                cart.rb.MoveRotation(uprightRotation);
                cart.rb.MovePosition(nextCartPosition);
            }

            cart.rb.linearVelocity = Vector3.zero;
            cart.rb.angularVelocity = Vector3.zero;
            cart.forwardRollMomentum = 0f;
            cart.sidePivotMomentum = 0f;
            cart.hasSidePivotWorldPoint = false;
        }

        UpdateRowTransform(row);
        RebuildRowLayout(row);
    }

    public Vector3 GetPlayerGrabPosition()
    {
        RefreshGrabbedPlayerPose();
        return currentGrabbedPlayerPosition;
    }

    public Quaternion GetPlayerGrabRotation()
    {
        RefreshGrabbedPlayerPose();
        return currentGrabbedPlayerRotation;
    }

    public void GetHandleGripTargets(out Vector3 leftHandPosition, out Vector3 rightHandPosition, out Quaternion handRotation)
    {
        Vector3 handlePosition = GetHandleCenterPosition();
        Vector3 right = transform.right;
        leftHandPosition = handlePosition - right * (handleGripWidth * 0.5f);
        rightHandPosition = handlePosition + right * (handleGripWidth * 0.5f);
        handRotation = Quaternion.LookRotation(transform.forward, Vector3.up);
    }

    private bool IsPlayerOnHandleSide(Vector3 playerPosition)
    {
        Vector3 grabPointToPlayer = Vector3.ProjectOnPlane(playerPosition - GetCartGrabPointPosition(), Vector3.up);
        return grabPointToPlayer.magnitude <= grabDistance;
    }

    private Vector3 GetCartGrabPointPosition()
    {
        return cartGrabPoint != null ? cartGrabPoint.position : GetHandleCenterPosition();
    }

    private Vector3 GetCartGrabPointOffset(Quaternion cartRotation)
    {
        if (cartGrabPoint == null)
        {
            return GetHandleCenterOffset(cartRotation);
        }

        Quaternion fromCurrentToTarget = cartRotation * Quaternion.Inverse(transform.rotation);
        Vector3 currentWorldOffset = cartGrabPoint.position - rb.position;
        return fromCurrentToTarget * currentWorldOffset;
    }

    private Vector3 GetHandlePosition()
    {
        return handleAnchor != null ? handleAnchor.position : transform.TransformPoint(fallbackHandleLocalPoint);
    }

    private Vector3 GetHandleCenterPosition()
    {
        if (TryGetHandleRendererCenter(out Vector3 center))
        {
            return center;
        }

        return GetHandlePosition();
    }

    private Vector3 GetHandlePosition(Vector3 cartPosition, Quaternion cartRotation)
    {
        if (handleAnchor == null)
        {
            return cartPosition + cartRotation * ScaledLocalPoint(fallbackHandleLocalPoint);
        }

        return cartPosition + cartRotation * ScaledLocalPoint(handleAnchor.localPosition);
    }

    private Vector3 GetHandleCenterOffset(Quaternion cartRotation)
    {
        Vector3 localCenter = handleAnchor != null ? GetLocalHandleCenter() : fallbackHandleLocalPoint;
        return cartRotation * ScaledLocalPoint(localCenter);
    }

    private Vector3 GetLocalHandleCenter()
    {
        if (TryGetHandleRendererCenter(out Vector3 center))
        {
            return transform.InverseTransformPoint(center);
        }

        return handleAnchor.localPosition;
    }

    private bool TryGetHandleRendererCenter(out Vector3 center)
    {
        center = Vector3.zero;
        if (handleRenderers == null || handleRenderers.Length == 0)
        {
            return false;
        }

        Bounds bounds = new Bounds();
        bool hasBounds = false;
        foreach (Renderer handleRenderer in handleRenderers)
        {
            if (handleRenderer == null || !handleRenderer.enabled)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = handleRenderer.bounds;
                hasBounds = true;
                continue;
            }

            bounds.Encapsulate(handleRenderer.bounds);
        }

        if (!hasBounds)
        {
            return false;
        }

        center = bounds.center;
        return true;
    }

    private Vector3 GetHandleSideDirection()
    {
        Vector3 direction = GetRearWheelCenter() - GetFrontWheelCenter();
        direction.y = 0f;
        return direction.sqrMagnitude > 0.001f ? direction.normalized : -transform.forward;
    }

    private Vector3 GetHandleSideDirection(Vector3 cartPosition, Quaternion cartRotation)
    {
        return -(cartRotation * Vector3.forward);
    }

    private Vector3 GetFrontWheelCenter()
    {
        return GetAveragePosition(frontCasterPivots, frontWheelAxle, transform.position + transform.forward * 0.55f);
    }

    private Vector3 GetRearWheelCenter()
    {
        return GetAveragePosition(rearWheelPivots, rearWheelAxle, transform.position - transform.forward * 0.55f);
    }

    private Vector3 ScaledLocalPoint(Vector3 localPoint)
    {
        return Vector3.Scale(localPoint, transform.localScale);
    }

    private void RefreshGrabbedPlayerPose()
    {
        if (grabbedPlayer == null)
        {
            return;
        }

        UpdateGrabbedPlayerPose(GetHandleCenterPosition(), GetHandleSideDirection(), transform.rotation);
    }

    private void ApplyGrabbedControl()
    {
        Vector3 pivot = GetHandlePosition();
        Vector3 desiredMove = new Vector3(grabbedInput.x, 0f, grabbedInput.y);
        float moveAmount = Mathf.Clamp01(desiredMove.magnitude) * grabbedDriveSpeed * Time.fixedDeltaTime;
        Quaternion nextRotation = rb.rotation;
        Vector3 nextPosition = rb.position;

        if (moveAmount > 0.001f)
        {
            desiredMove.Normalize();
            float targetYaw = Quaternion.LookRotation(desiredMove, Vector3.up).eulerAngles.y;
            float currentYaw = nextRotation.eulerAngles.y;
            float turnStep = Mathf.Clamp(Mathf.DeltaAngle(currentYaw, targetYaw), -grabbedTurnSpeed * Time.fixedDeltaTime, grabbedTurnSpeed * Time.fixedDeltaTime);
            Quaternion deltaRotation = Quaternion.AngleAxis(turnStep, Vector3.up);
            nextRotation = deltaRotation * nextRotation;
            nextPosition = pivot + deltaRotation * (nextPosition - pivot);
            nextPosition += desiredMove * moveAmount;
        }

        rb.MoveRotation(nextRotation);
        rb.MovePosition(nextPosition);
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
    }

    private void UpdateGrabbedPlayerPose(Vector3 cartPosition, Quaternion cartRotation)
    {
        UpdateGrabbedPlayerPose(GetHandlePosition(cartPosition, cartRotation), GetHandleSideDirection(cartPosition, cartRotation), cartRotation);
    }

    private void UpdateGrabbedPlayerPose(Vector3 handlePosition, Vector3 playerSide, Quaternion cartRotation)
    {
        Vector3 sideStep = (cartRotation * Vector3.right) * (grabbedInput.x * playerTurnSideStep);

        currentGrabbedPlayerPosition = handlePosition + playerSide * playerHandleSpacing + sideStep;
        currentGrabbedPlayerPosition.y = plantedY;

        Vector3 cartDirection = handlePosition - currentGrabbedPlayerPosition;
        cartDirection.y = 0f;
        if (cartDirection.sqrMagnitude < 0.001f)
        {
            cartDirection = cartRotation * Vector3.forward;
        }

        currentGrabbedPlayerRotation = Quaternion.LookRotation(cartDirection.normalized, Vector3.up);
    }

    private void OnCollisionStay(Collision collision)
    {
        TryTipFromCollision(collision);
    }

    private void OnCollisionEnter(Collision collision)
    {
        TryTipFromCollision(collision);
    }

    private void TryTipFromCollision(Collision collision)
    {
        if (!allowTipOver || isTipped || collision.contactCount == 0)
        {
            return;
        }

        Vector3 horizontalRelativeVelocity = Vector3.ProjectOnPlane(collision.relativeVelocity, Vector3.up);
        bool hitByPlayer = collision.transform.GetComponentInParent<PlayerController>() != null || collision.transform.name.Contains("Player");

        if (hitByPlayer)
        {
            return;
        }

        Rigidbody otherBody = collision.rigidbody;
        float otherMass = otherBody != null ? otherBody.mass : 1f;
        float impactMomentum = horizontalRelativeVelocity.magnitude * otherMass;
        if (horizontalRelativeVelocity.magnitude >= impactTipSpeed && impactMomentum >= impactTipMomentum)
        {
            ReleaseForTip(collision, horizontalRelativeVelocity);
        }
    }

    public void ReceivePlayerPush(Vector3 force, Vector3 point)
    {
        if (rowLeader != null && rowLeader != this)
        {
            rowLeader.ReceivePlayerPush(force, point);
            return;
        }

        if (isTipped)
        {
            rb.AddForceAtPosition(force / tippedRollingResistanceMultiplier, point, ForceMode.Force);
            return;
        }

        List<CartController> row = GetExplicitRow();
        if (enableNestedCartRows && row.Count > 1)
        {
            float rowWeight = GetNestedRowWeight(row.Count);
            Vector3 sharedForce = force / rowWeight;
            Vector3 rowCenter = GetExplicitRowCenter(row);
            ReceiveSharedRowPush(sharedForce, point, row.Count, rowCenter);
            return;
        }

        ReceiveSharedRowPush(force, point, 1, Vector3.zero);

    }

    private void ReceiveSharedRowPush(Vector3 force, Vector3 point, int rowCount, Vector3 rowCenter)
    {
        lastExternalPushTime = Time.time;
        rb.WakeUp();
        ApplyWheelBiasedPush(force, point, rowCount, rowCenter);
    }

    private void ApplyWheelBiasedPush(Vector3 force, Vector3 point, int rowCount, Vector3 rowCenter)
    {
        Vector3 planarForce = Vector3.ProjectOnPlane(force, Vector3.up);
        if (planarForce.sqrMagnitude < 0.001f)
        {
            rb.AddForce(force, ForceMode.Force);
            return;
        }

        Vector3 rearPosition = GetRearWheelCenter();
        Vector3 frontPosition = GetFrontWheelCenter();
        Vector3 cartForward = Vector3.ProjectOnPlane(frontPosition - rearPosition, Vector3.up);
        if (cartForward.sqrMagnitude < 0.001f)
        {
            cartForward = transform.forward;
        }

        cartForward.Normalize();
        Vector3 cartRight = Vector3.Cross(Vector3.up, cartForward).normalized;
        Vector3 forwardForce = Vector3.Project(planarForce, cartForward) * forwardPushMultiplier;
        Vector3 sideForce = Vector3.Project(planarForce, cartRight);
        float frontBlend = GetFrontPushBlend(point, rearPosition, frontPosition);

        QueueForwardRollPush(forwardForce, cartForward);

        if (sideForce.sqrMagnitude < 0.001f)
        {
            return;
        }

        QueueSidePivotPush(planarForce, sideForce, frontBlend, rowCount, rowCenter);
    }

    private void QueueForwardRollPush(Vector3 forwardForce, Vector3 cartForward)
    {
        float signedForce = Vector3.Dot(forwardForce, cartForward);
        if (Mathf.Abs(signedForce) < 0.001f)
        {
            return;
        }

        float addedMomentum = signedForce * forwardRollMomentumGain;
        forwardRollMomentum = Mathf.Clamp(forwardRollMomentum + addedMomentum, -forwardRollMaxMomentum, forwardRollMaxMomentum);
    }

    private void QueueSidePivotPush(Vector3 planarForce, Vector3 sideForce, float frontBlend, int rowCount, Vector3 rowCenter)
    {
        if (sideForce.sqrMagnitude < 0.001f || planarForce.sqrMagnitude < 0.001f)
        {
            return;
        }

        sidePivotPushDirection = planarForce.normalized;
        sidePivotMomentum = Mathf.Min(sidePivotMaxMomentum, sidePivotMomentum + sideForce.magnitude * Mathf.Max(0.15f, frontBlend) * sidePivotMomentumGain);
        hasSidePivotWorldPoint = rowCount > 1;
        if (hasSidePivotWorldPoint)
        {
            Vector3 rearPivot = GetRearWheelCenter();
            sidePivotWorldPoint = Vector3.Lerp(rearPivot, rowCenter, nestedRowCenterPivotBlend);
            sidePivotWorldPoint.y = keepCartPlanted ? plantedY : sidePivotWorldPoint.y;
        }
    }

    private void ApplySidePivotMotion()
    {
        if (sidePivotMomentum <= 0.001f || sidePivotPushDirection.sqrMagnitude < 0.001f)
        {
            return;
        }

        Vector3 rearPosition = GetRearWheelCenter();
        Vector3 rotationPivot = hasSidePivotWorldPoint ? sidePivotWorldPoint : rearPosition;
        Vector3 frontPosition = GetFrontWheelCenter();
        Vector3 cartForward = Vector3.ProjectOnPlane(frontPosition - rearPosition, Vector3.up);
        if (cartForward.sqrMagnitude < 0.001f)
        {
            cartForward = transform.forward;
        }

        cartForward.Normalize();
        Vector3 targetDirection = sidePivotPushDirection.normalized;
        float signedAngle = Vector3.SignedAngle(cartForward, targetDirection, Vector3.up);
        float maxTurn = sidePivotTurnSpeed * Mathf.Clamp01(sidePivotMomentum) * Time.fixedDeltaTime;
        float turnStep = Mathf.Clamp(signedAngle, -maxTurn, maxTurn);
        Quaternion deltaRotation = Quaternion.AngleAxis(turnStep, Vector3.up);

        Vector3 nextPosition = rotationPivot + deltaRotation * (rb.position - rotationPivot);
        Quaternion nextRotation = deltaRotation * rb.rotation;

        float facingPush = Mathf.Clamp01(Vector3.Dot(deltaRotation * cartForward, targetDirection));
        float rollBlend = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(sidePivotAlignmentToRoll, 1f, facingPush));
        nextPosition += targetDirection * (sidePivotMomentum * sidePivotRollSpeed * rollBlend * Time.fixedDeltaTime);

        nextPosition.y = keepCartPlanted ? plantedY : nextPosition.y;
        List<CartController> row = GetExplicitRow();
        if (row.Count > 1)
        {
            ApplyRowMotion(row, rotationPivot, deltaRotation, nextPosition - (rotationPivot + deltaRotation * (rb.position - rotationPivot)), targetDirection, rollBlend);
        }
        else
        {
            rb.MovePosition(nextPosition);
            rb.MoveRotation(nextRotation);
            rb.linearVelocity = Vector3.Project(rb.linearVelocity, targetDirection) * rollBlend;
            rb.angularVelocity = new Vector3(0f, rb.angularVelocity.y, 0f);
        }

        sidePivotMomentum = Mathf.MoveTowards(sidePivotMomentum, 0f, sidePivotMomentumDamping * Time.fixedDeltaTime);
        if (sidePivotMomentum <= 0.001f)
        {
            hasSidePivotWorldPoint = false;
        }
    }

    private void ApplyForwardRollMotion()
    {
        if (Mathf.Abs(forwardRollMomentum) <= 0.001f)
        {
            return;
        }

        Vector3 cartForward = Vector3.ProjectOnPlane(GetFrontWheelCenter() - GetRearWheelCenter(), Vector3.up);
        if (cartForward.sqrMagnitude < 0.001f)
        {
            cartForward = transform.forward;
        }

        cartForward.Normalize();
        Vector3 nextPosition = rb.position + cartForward * (forwardRollMomentum * forwardRollSpeed * Time.fixedDeltaTime);
        nextPosition.y = keepCartPlanted ? plantedY : nextPosition.y;

        List<CartController> row = GetExplicitRow();
        if (row.Count > 1)
        {
            ApplyRowMotion(row, rb.position, Quaternion.identity, nextPosition - rb.position, cartForward, 1f);
        }
        else
        {
            rb.MovePosition(nextPosition);

            Vector3 planarVelocity = Vector3.ProjectOnPlane(rb.linearVelocity, Vector3.up);
            Vector3 alignedVelocity = Vector3.Project(planarVelocity, cartForward);
            rb.linearVelocity = alignedVelocity + Vector3.Project(rb.linearVelocity, Vector3.up);
        }

        forwardRollMomentum = Mathf.MoveTowards(forwardRollMomentum, 0f, forwardRollMomentumDamping * Time.fixedDeltaTime);
    }

    private void ApplyRowMotion(List<CartController> row, Vector3 pivot, Quaternion deltaRotation, Vector3 deltaPosition, Vector3 velocityDirection, float velocityBlend)
    {
        foreach (CartController cart in row)
        {
            if (cart == null || cart.isTipped)
            {
                continue;
            }

            Vector3 nextCartPosition = pivot + deltaRotation * (cart.rb.position - pivot) + deltaPosition;
            nextCartPosition.y = cart.plantedY;
            Quaternion nextCartRotation = deltaRotation * cart.rb.rotation;
            Vector3 euler = nextCartRotation.eulerAngles;
            Quaternion uprightRotation = Quaternion.Euler(0f, euler.y, 0f);

            if (cart.rb.isKinematic)
            {
                cart.rb.position = nextCartPosition;
                cart.rb.rotation = uprightRotation;
            }
            else
            {
                cart.rb.MovePosition(nextCartPosition);
                cart.rb.MoveRotation(uprightRotation);
            }

            cart.rb.linearVelocity = Vector3.Project(cart.rb.linearVelocity, velocityDirection) * velocityBlend;
            cart.rb.angularVelocity = Vector3.zero;
            if (cart != this)
            {
                cart.forwardRollMomentum = 0f;
                cart.sidePivotMomentum = 0f;
                cart.hasSidePivotWorldPoint = false;
            }
        }

        UpdateRowTransform(row);
        RebuildRowLayout(row);
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

    private float GetFrontPushBlend(Vector3 point, Vector3 rearPosition, Vector3 frontPosition)
    {
        Vector3 rearToFront = Vector3.ProjectOnPlane(frontPosition - rearPosition, Vector3.up);
        float cartLength = rearToFront.magnitude;
        if (cartLength < 0.001f)
        {
            return 1f;
        }

        Vector3 rearToPoint = Vector3.ProjectOnPlane(point - rearPosition, Vector3.up);
        return Mathf.Clamp01(Vector3.Dot(rearToPoint, rearToFront / cartLength) / cartLength);
    }

    public bool TryAttachCartAheadToRow()
    {
        if (!enableNestedCartRows || isTipped)
        {
            return false;
        }

        CartController leader = rowLeader != null ? rowLeader : this;
        CartController candidate = leader.FindAttachableCartAhead();
        if (candidate == null)
        {
            Debug.Log($"{nameof(CartController)} on {leader.name} did not find an attachable cart near the row front.", leader);
            return false;
        }

        leader.AttachCartToRow(candidate);
        Debug.Log($"{nameof(CartController)} attached {candidate.name} to row led by {leader.name}.", leader);
        return true;
    }

    private CartController FindAttachableCartAhead()
    {
        List<CartController> row = GetExplicitRow();
        Vector3 rowForward = GetCartForward();
        Vector3 rowFrontPoint = GetFrontRowAttachPoint(row, rowForward);
        CartController closestCart = null;
        float closestDistance = nestedRowScanDistance;
        int availableCartCount = 0;

        CartController[] carts = FindObjectsByType<CartController>(FindObjectsInactive.Exclude);
        foreach (CartController candidate in carts)
        {
            if (candidate == null || candidate == this || candidate.isTipped || candidate.rowLeader != null || row.Contains(candidate))
            {
                continue;
            }

            availableCartCount++;

            float distance = candidate.GetClosestPlanarDistanceTo(rowFrontPoint);
            if (distance > closestDistance)
            {
                continue;
            }

            closestDistance = distance;
            closestCart = candidate;
        }

        if (closestCart == null)
        {
            Debug.Log($"{nameof(CartController)} on {name} saw {availableCartCount} available carts, but none were within {nestedRowScanDistance:0.00} of the row front.", this);
        }

        return closestCart;
    }

    private float GetClosestPlanarDistanceTo(Vector3 targetPoint)
    {
        float frontDistance = Vector3.ProjectOnPlane(GetFrontWheelCenter() - targetPoint, Vector3.up).magnitude;
        float rearDistance = Vector3.ProjectOnPlane(GetRearWheelCenter() - targetPoint, Vector3.up).magnitude;
        float centerDistance = Vector3.ProjectOnPlane(rb.position - targetPoint, Vector3.up).magnitude;
        return Mathf.Min(frontDistance, rearDistance, centerDistance);
    }

    private float GetAttachForwardGap(CartController candidate, float frontEdge, Vector3 rowForward, float allowedDistance)
    {
        float frontGap = GetForwardGapToPoint(candidate.GetFrontWheelCenter(), frontEdge, rowForward, allowedDistance);
        float rearGap = GetForwardGapToPoint(candidate.GetRearWheelCenter(), frontEdge, rowForward, allowedDistance);
        float centerGap = GetForwardGapToPoint(candidate.rb.position, frontEdge, rowForward, allowedDistance);
        return Mathf.Min(frontGap, rearGap, centerGap);
    }

    private float GetForwardGapToPoint(Vector3 point, float frontEdge, Vector3 rowForward, float allowedDistance)
    {
        Vector3 offset = Vector3.ProjectOnPlane(point - rb.position, Vector3.up);
        float forwardGap = Vector3.Dot(offset, rowForward) - frontEdge;
        return forwardGap < -(allowedDistance * 0.5f) ? float.PositiveInfinity : Mathf.Abs(forwardGap);
    }

    private float GetAttachLateralDistance(CartController candidate, Vector3 rowRight)
    {
        float frontLateral = GetLateralDistanceToPoint(candidate.GetFrontWheelCenter(), rowRight);
        float rearLateral = GetLateralDistanceToPoint(candidate.GetRearWheelCenter(), rowRight);
        float centerLateral = GetLateralDistanceToPoint(candidate.rb.position, rowRight);
        return Mathf.Min(frontLateral, rearLateral, centerLateral);
    }

    private float GetLateralDistanceToPoint(Vector3 point, Vector3 rowRight)
    {
        Vector3 offset = Vector3.ProjectOnPlane(point - rb.position, Vector3.up);
        return Mathf.Abs(Vector3.Dot(offset, rowRight));
    }

    private void AttachCartToRow(CartController cart)
    {
        if (cart == null || cart == this || explicitRowCarts.Contains(cart))
        {
            return;
        }

        EnsureRowObject();
        cart.rowLeader = this;
        cart.rowObject = rowObject;
        cart.transform.SetParent(rowObject.transform, true);
        cart.EnterRowMemberState();
        explicitRowCarts.Add(cart);
        if (pullNestedCartIntoPlace)
        {
            RebuildRowLayout(GetExplicitRow());
        }

        SetCartCollisionIgnored(cart, true);
        if (!ignoredNestedCollisionCarts.Contains(cart))
        {
            ignoredNestedCollisionCarts.Add(cart);
        }

        cart.SetCartCollisionIgnored(this, true);
        if (!cart.ignoredNestedCollisionCarts.Contains(this))
        {
            cart.ignoredNestedCollisionCarts.Add(this);
        }

        foreach (CartController rowCart in explicitRowCarts)
        {
            if (rowCart != null && rowCart != cart)
            {
                cart.SetCartCollisionIgnored(rowCart, true);
                if (!cart.ignoredNestedCollisionCarts.Contains(rowCart))
                {
                    cart.ignoredNestedCollisionCarts.Add(rowCart);
                }

                rowCart.SetCartCollisionIgnored(cart, true);
                if (!rowCart.ignoredNestedCollisionCarts.Contains(cart))
                {
                    rowCart.ignoredNestedCollisionCarts.Add(cart);
                }
            }
        }
    }

    private void EnsureRowObject()
    {
        if (rowLeader != null && rowLeader != this)
        {
            rowLeader.EnsureRowObject();
            rowObject = rowLeader.rowObject;
            return;
        }

        if (rowObject != null)
        {
            return;
        }

        Vector3 rowForward = GetRowForward();
        rowStartPosition = rb.position;
        rowObject = new GameObject($"Cart Row - {name}");
        rowObject.transform.SetPositionAndRotation(rowStartPosition, Quaternion.LookRotation(rowForward, Vector3.up));
        rowObject.transform.SetParent(originalParent, true);
        transform.SetParent(rowObject.transform, true);
        rowCartLocalRotation = transform.localRotation;
    }

    private void EnterRowMemberState()
    {
        wasKinematicBeforeRow = rb.isKinematic;
        rb.isKinematic = true;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        forwardRollMomentum = 0f;
        sidePivotMomentum = 0f;
        hasSidePivotWorldPoint = false;
        EnforcePlantedHeight();
        EnforceUprightPose();
    }

    private void ExitRowMemberState()
    {
        rb.isKinematic = wasKinematicBeforeRow;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rowObject = null;
    }

    private void PullCartIntoRowPlace(CartController previousCart, CartController cart)
    {
        if (previousCart == null || cart == null)
        {
            return;
        }

        List<CartController> row = GetExplicitRow();
        UpdateRowTransform(row);

        Vector3 localSlot = GetNextRowEndSlot(row, cart);
        Quaternion targetRotation = rowObject != null ? rowObject.transform.rotation * rowCartLocalRotation : previousCart.rb.rotation;
        Vector3 targetPosition = rowObject != null ? rowObject.transform.TransformPoint(localSlot) : previousCart.rb.position + previousCart.GetCartForward() * (nestedRowSlotSpacing + nestedRowPullForwardOffset);

        cart.rb.position = targetPosition;
        cart.rb.rotation = targetRotation;
        cart.transform.SetPositionAndRotation(targetPosition, targetRotation);
        if (rowObject != null)
        {
            cart.transform.localPosition = localSlot;
            cart.transform.localRotation = rowCartLocalRotation;
            targetPosition = cart.transform.position;
            targetRotation = cart.transform.rotation;
            cart.rb.position = targetPosition;
            cart.rb.rotation = targetRotation;
        }

        cart.rb.linearVelocity = Vector3.zero;
        cart.rb.angularVelocity = Vector3.zero;
        cart.forwardRollMomentum = 0f;
        cart.sidePivotMomentum = 0f;
        cart.hasSidePivotWorldPoint = false;
        Physics.SyncTransforms();
    }

    private void RebuildRowLayout(List<CartController> row)
    {
        if (rowObject == null || row == null)
        {
            return;
        }

        for (int i = 0; i < row.Count; i++)
        {
            CartController cart = row[i];
            if (cart == null || cart.isTipped)
            {
                continue;
            }

            Vector3 localSlot = new Vector3(0f, cart.plantedY - rowObject.transform.position.y, GetRowSlotForwardOffset(i));
            Quaternion targetRotation = rowObject.transform.rotation * rowCartLocalRotation;
            Vector3 targetPosition = rowObject.transform.TransformPoint(localSlot);

            cart.transform.localPosition = localSlot;
            cart.transform.localRotation = rowCartLocalRotation;
            if (cart.rb.isKinematic)
            {
                cart.rb.position = targetPosition;
                cart.rb.rotation = targetRotation;
            }
            else
            {
                cart.rb.MovePosition(targetPosition);
                cart.rb.MoveRotation(targetRotation);
            }

            cart.rb.linearVelocity = Vector3.zero;
            cart.rb.angularVelocity = Vector3.zero;
            if (cart != this)
            {
                cart.forwardRollMomentum = 0f;
                cart.sidePivotMomentum = 0f;
                cart.hasSidePivotWorldPoint = false;
            }
        }

        Physics.SyncTransforms();
    }

    private float GetRowSlotForwardOffset(int slotIndex)
    {
        return slotIndex <= 0 ? 0f : slotIndex * GetNestedRowSlotStep();
    }

    private float GetNestedRowSlotStep()
    {
        return Mathf.Max(0.01f, nestedRowStepDistance);
    }

    private Vector3 GetNextRowEndSlot(List<CartController> row, CartController cart)
    {
        if (rowObject == null)
        {
            return Vector3.zero;
        }

        float furthestForwardSlot = 0f;
        foreach (CartController rowCart in row)
        {
            if (rowCart == null || rowCart == cart)
            {
                continue;
            }

            Vector3 localPosition = rowObject.transform.InverseTransformPoint(rowCart.rb.position);
            furthestForwardSlot = Mathf.Max(furthestForwardSlot, localPosition.z);
        }

        return new Vector3(
            0f,
            cart.plantedY - rowObject.transform.position.y,
            furthestForwardSlot + GetNestedRowSlotStep());
    }

    private Vector3 GetClosestAttachPointTo(Vector3 targetPoint, Vector3 rowForward, Vector3 rowRight)
    {
        Vector3 frontPoint = GetFrontWheelCenter();
        Vector3 rearPoint = GetRearWheelCenter();
        Vector3 centerPoint = rb.position;

        Vector3 closestPoint = frontPoint;
        float closestScore = GetAttachPointScore(frontPoint, targetPoint, rowForward, rowRight);

        float rearScore = GetAttachPointScore(rearPoint, targetPoint, rowForward, rowRight);
        if (rearScore < closestScore)
        {
            closestScore = rearScore;
            closestPoint = rearPoint;
        }

        float centerScore = GetAttachPointScore(centerPoint, targetPoint, rowForward, rowRight);
        if (centerScore < closestScore)
        {
            closestPoint = centerPoint;
        }

        return closestPoint;
    }

    private float GetAttachPointScore(Vector3 point, Vector3 targetPoint, Vector3 rowForward, Vector3 rowRight)
    {
        Vector3 offset = Vector3.ProjectOnPlane(point - targetPoint, Vector3.up);
        float forwardError = Mathf.Abs(Vector3.Dot(offset, rowForward));
        float lateralError = Mathf.Abs(Vector3.Dot(offset, rowRight));
        return forwardError + lateralError;
    }

    private List<CartController> GetExplicitRow()
    {
        if (rowLeader != null && rowLeader != this)
        {
            return rowLeader.GetExplicitRow();
        }

        List<CartController> row = new List<CartController> { this };
        for (int i = 0; i < explicitRowCarts.Count; i++)
        {
            CartController cart = explicitRowCarts[i];
            if (cart == null || cart.isTipped)
            {
                DetachRowFromIndex(i);
                continue;
            }

            row.Add(cart);
        }

        return row;
    }

    private void DetachRowFromIndex(int startIndex)
    {
        for (int i = explicitRowCarts.Count - 1; i >= startIndex; i--)
        {
            DetachCartFromRow(explicitRowCarts[i]);
            explicitRowCarts.RemoveAt(i);
        }
    }

    private bool IsPhysicallyStackedWith(CartController previousCart, CartController nextCart, float allowedDistance)
    {
        if (previousCart == null || nextCart == null)
        {
            return false;
        }

        Vector3 rowForward = previousCart.GetCartForward();
        Vector3 rowRight = Vector3.Cross(Vector3.up, rowForward).normalized;
        if (Mathf.Abs(Vector3.Dot(rowForward, nextCart.GetCartForward())) < nestedRowAlignmentDot)
        {
            return false;
        }

        Vector3 targetPoint = previousCart.GetFrontWheelCenter() - rowForward * nestedRowOverlapDistance + rowForward * nestedRowPullForwardOffset;
        Vector3 rearOffset = Vector3.ProjectOnPlane(nextCart.GetRearWheelCenter() - targetPoint, Vector3.up);
        float forwardGap = Mathf.Abs(Vector3.Dot(rearOffset, rowForward));
        float lateralDistance = previousCart.GetAttachLateralDistance(nextCart, rowRight);
        return forwardGap <= allowedDistance && lateralDistance <= nestedRowLateralTolerance;
    }

    private void DetachCartFromRow(CartController cart)
    {
        if (cart == null)
        {
            return;
        }

        SetCartCollisionIgnored(cart, false);
        ignoredNestedCollisionCarts.Remove(cart);
        cart.SetCartCollisionIgnored(this, false);
        cart.ignoredNestedCollisionCarts.Remove(this);
        foreach (CartController rowCart in explicitRowCarts)
        {
            if (rowCart == null || rowCart == cart)
            {
                continue;
            }

            cart.SetCartCollisionIgnored(rowCart, false);
            cart.ignoredNestedCollisionCarts.Remove(rowCart);
            rowCart.SetCartCollisionIgnored(cart, false);
            rowCart.ignoredNestedCollisionCarts.Remove(cart);
        }

        cart.rowLeader = null;
        cart.transform.SetParent(cart.originalParent, true);
        cart.ExitRowMemberState();
    }

    private Vector3 GetExplicitRowCenter(List<CartController> row)
    {
        Vector3 center = Vector3.zero;
        int count = 0;
        foreach (CartController cart in row)
        {
            if (cart == null)
            {
                continue;
            }

            center += cart.rb.position;
            count++;
        }

        center = count > 0 ? center / count : rb.position;
        center.y = plantedY;
        return center;
    }

    private float GetFrontRowEdge(List<CartController> row, Vector3 rowForward)
    {
        float frontEdge = float.MinValue;
        foreach (CartController cart in row)
        {
            if (cart == null)
            {
                continue;
            }

            Vector3 offset = Vector3.ProjectOnPlane(cart.GetFrontWheelCenter() - rb.position, Vector3.up);
            frontEdge = Mathf.Max(frontEdge, Vector3.Dot(offset, rowForward));
        }

        return frontEdge == float.MinValue ? 0f : frontEdge;
    }

    private Vector3 GetFrontRowAttachPoint(List<CartController> row, Vector3 rowForward)
    {
        CartController frontCart = null;
        float frontEdge = float.MinValue;
        foreach (CartController cart in row)
        {
            if (cart == null)
            {
                continue;
            }

            Vector3 frontPoint = cart.GetFrontWheelCenter();
            Vector3 offset = Vector3.ProjectOnPlane(frontPoint - rb.position, Vector3.up);
            float edge = Vector3.Dot(offset, rowForward);
            if (edge > frontEdge)
            {
                frontEdge = edge;
                frontCart = cart;
            }
        }

        return frontCart != null ? frontCart.GetFrontWheelCenter() : GetFrontWheelCenter();
    }

    private float GetNestedRowWeight(int rowCount)
    {
        return 1f + Mathf.Max(0, rowCount - 1) * nestedRowExtraCartWeight;
    }

    private Vector3 GetCartForward()
    {
        Vector3 cartForward = Vector3.ProjectOnPlane(GetFrontWheelCenter() - GetRearWheelCenter(), Vector3.up);
        return cartForward.sqrMagnitude > 0.001f ? cartForward.normalized : transform.forward;
    }

    private Vector3 GetRowForward()
    {
        Vector3 rowForward = Vector3.zero;
        if (grabbedPlayer != null)
        {
            rowForward = Vector3.ProjectOnPlane(grabbedPlayer.GetGrabberRotation() * Vector3.forward, Vector3.up);
        }

        if (rowForward.sqrMagnitude < 0.001f && hasGrabberFollowTarget)
        {
            rowForward = Vector3.ProjectOnPlane(grabberFollowRotation * Vector3.forward, Vector3.up);
        }

        if (rowForward.sqrMagnitude < 0.001f)
        {
            rowForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        }

        return rowForward.sqrMagnitude > 0.001f ? rowForward.normalized : Vector3.forward;
    }

    private float GetCartLength()
    {
        return Mathf.Max(0.1f, Vector3.ProjectOnPlane(GetFrontWheelCenter() - GetRearWheelCenter(), Vector3.up).magnitude);
    }

    private void UpdateNestedCartCollisionIgnores()
    {
        if (!ignoreNestedCartCollisions)
        {
            RestoreNestedCartCollisions();
            return;
        }

        if (Time.time < nextNestedCollisionRefreshTime)
        {
            return;
        }

        nextNestedCollisionRefreshTime = Time.time + nestedCollisionRefreshInterval;
        if (rowLeader != null && rowLeader != this)
        {
            return;
        }

        List<CartController> row = GetExplicitRow();
        foreach (CartController rowCart in row)
        {
            if (rowCart == null || rowCart == this || ignoredNestedCollisionCarts.Contains(rowCart))
            {
                continue;
            }

            SetCartCollisionIgnored(rowCart, true);
            ignoredNestedCollisionCarts.Add(rowCart);
        }
    }

    private void SetCartCollisionIgnored(CartController otherCart, bool ignore)
    {
        if (otherCart == null || cartColliders == null)
        {
            return;
        }

        Collider[] otherColliders = otherCart.cartColliders != null && otherCart.cartColliders.Length > 0
            ? otherCart.cartColliders
            : otherCart.GetComponentsInChildren<Collider>();

        foreach (Collider cartCollider in cartColliders)
        {
            if (cartCollider == null)
            {
                continue;
            }

            foreach (Collider otherCollider in otherColliders)
            {
                if (otherCollider != null && otherCollider != cartCollider)
                {
                    Physics.IgnoreCollision(cartCollider, otherCollider, ignore);
                }
            }
        }
    }

    private void RestoreNestedCartCollisions()
    {
        for (int i = ignoredNestedCollisionCarts.Count - 1; i >= 0; i--)
        {
            SetCartCollisionIgnored(ignoredNestedCollisionCarts[i], false);
        }

        ignoredNestedCollisionCarts.Clear();
    }

    private void ReleaseForTip(Collision collision, Vector3 horizontalRelativeVelocity)
    {
        ContactPoint contact = collision.GetContact(0);
        Vector3 pushDirection = horizontalRelativeVelocity.sqrMagnitude > 0.01f ? horizontalRelativeVelocity.normalized : -contact.normal;
        ReleaseForTip(pushDirection, contact.point);
    }

    private void ReleaseForTip(Vector3 pushDirection, Vector3 contactPoint)
    {
        isTipped = true;
        keepCartUpright = false;
        keepCartPlanted = false;

        rb.constraints = RigidbodyConstraints.None;
        rb.linearDamping = tippedLinearDamping;
        rb.angularDamping = tippedAngularDamping;
        rb.WakeUp();

        Vector3 tipAxis = Vector3.Cross(Vector3.up, pushDirection).normalized;
        if (tipAxis.sqrMagnitude < 0.01f)
        {
            tipAxis = transform.forward;
        }

        rb.AddTorque(tipAxis * tipTorqueImpulse, ForceMode.Impulse);
        rb.AddForceAtPosition(pushDirection * (tipTorqueImpulse * 0.35f), contactPoint + Vector3.up * 0.25f, ForceMode.Impulse);
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

    private void ApplyCartWheelFriction()
    {
        Vector3 rearPosition = GetAveragePosition(rearWheelPivots, rearWheelAxle, transform.position - transform.forward * 0.55f);
        Vector3 frontPosition = GetAveragePosition(frontCasterPivots, frontWheelAxle, transform.position + transform.forward * 0.55f);
        Vector3 cartForward = Vector3.ProjectOnPlane(frontPosition - rearPosition, Vector3.up);
        if (cartForward.sqrMagnitude < 0.001f)
        {
            cartForward = transform.forward;
        }

        cartForward.Normalize();
        Vector3 cartRight = Vector3.Cross(Vector3.up, cartForward).normalized;

        ApplySideGripAtPoint(rearPosition, cartRight, rearWheelSideGrip);
        ApplySideGripAtPoint(frontPosition, cartRight, frontCasterSideGrip);

        Vector3 planarVelocity = Vector3.ProjectOnPlane(rb.linearVelocity, Vector3.up);
        if (planarVelocity.sqrMagnitude < 0.01f)
        {
            return;
        }

        float lateralSpeed = Vector3.Dot(planarVelocity, cartRight);
        if (Mathf.Abs(lateralSpeed) < idleSpeedThreshold)
        {
            return;
        }

        float forwardSpeed = Mathf.Abs(Vector3.Dot(planarVelocity, cartForward));
        float yawDirection = Mathf.Sign(Vector3.Dot(planarVelocity, cartForward));
        float yawAssist = lateralSpeed * forwardSpeed * casterYawAssist * yawDirection;

        rb.AddTorque(Vector3.up * yawAssist, ForceMode.Acceleration);
    }

    private void ApplyRollingAlignment()
    {
        Vector3 planarVelocity = Vector3.ProjectOnPlane(rb.linearVelocity, Vector3.up);
        if (planarVelocity.magnitude < rollingAlignmentMinSpeed && Mathf.Abs(forwardRollMomentum) < rollingAlignmentMinSpeed)
        {
            return;
        }

        Vector3 cartForward = Vector3.ProjectOnPlane(GetFrontWheelCenter() - GetRearWheelCenter(), Vector3.up);
        if (cartForward.sqrMagnitude < 0.001f)
        {
            cartForward = transform.forward;
        }

        cartForward.Normalize();
        Vector3 cartRight = Vector3.Cross(Vector3.up, cartForward).normalized;
        float forwardSpeed = Vector3.Dot(planarVelocity, cartForward);
        float sideSpeed = Vector3.Dot(planarVelocity, cartRight);

        if (Mathf.Abs(forwardRollMomentum) >= rollingAlignmentMinSpeed)
        {
            forwardSpeed = forwardRollMomentum;
        }

        if (Mathf.Abs(forwardSpeed) < rollingAlignmentMinSpeed)
        {
            return;
        }

        Vector3 cleanedVelocity = cartForward * forwardSpeed;
        rb.linearVelocity = cleanedVelocity + Vector3.Project(rb.linearVelocity, Vector3.up);

        if (Mathf.Abs(forwardSpeed) <= Mathf.Abs(sideSpeed) && Mathf.Abs(forwardRollMomentum) < rollingAlignmentMinSpeed)
        {
            return;
        }

        Vector3 travelDirection = cleanedVelocity.normalized;
        Vector3 desiredForward = forwardSpeed >= 0f ? travelDirection : -travelDirection;
        float signedAngle = Vector3.SignedAngle(cartForward, desiredForward, Vector3.up);
        float turnStep = Mathf.Clamp(signedAngle, -rollingAlignmentTurnSpeed * Time.fixedDeltaTime, rollingAlignmentTurnSpeed * Time.fixedDeltaTime);
        if (Mathf.Abs(turnStep) > 0.01f)
        {
            rb.MoveRotation(Quaternion.AngleAxis(turnStep, Vector3.up) * rb.rotation);
        }

        Vector3 sideVelocity = cartRight * sideSpeed;
        rb.AddForce(-sideVelocity * rollingAlignmentSideGrip, ForceMode.Acceleration);
    }

    private Vector3 GetAveragePosition(Transform[] transforms, Transform fallback, Vector3 fallbackPosition)
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

    private void ApplySideGripAtPoint(Vector3 point, Vector3 sideDirection, float grip)
    {
        if (keepCartUpright)
        {
            point.y = rb.worldCenterOfMass.y;
        }

        Vector3 pointVelocity = Vector3.ProjectOnPlane(rb.GetPointVelocity(point), Vector3.up);
        Vector3 sidewaysVelocity = Vector3.Project(pointVelocity, sideDirection);

        rb.AddForceAtPosition(-sidewaysVelocity * grip, point, ForceMode.Acceleration);
    }

    private void ApplyRollingResistance()
    {
        Vector3 planarVelocity = Vector3.ProjectOnPlane(rb.linearVelocity, Vector3.up);
        if (planarVelocity.sqrMagnitude < 0.0001f)
        {
            return;
        }

        float speed = planarVelocity.magnitude;
        float slowBlend = Mathf.InverseLerp(0.05f, 1.25f, speed);
        float resistance = Mathf.Lerp(lowSpeedBrake, rollingResistance, slowBlend);
        if (isTipped)
        {
            resistance *= tippedRollingResistanceMultiplier;
        }

        rb.AddForce(-planarVelocity * resistance, ForceMode.Acceleration);
        rb.AddTorque(-Vector3.up * rb.angularVelocity.y * (resistance * 0.035f), ForceMode.Acceleration);
    }

    private void StabilizeIdleCart()
    {
        if (isTipped || grabbedPlayer != null || Time.time - lastExternalPushTime < idleSleepDelay)
        {
            return;
        }

        Vector3 planarVelocity = Vector3.ProjectOnPlane(rb.linearVelocity, Vector3.up);
        Vector3 angularVelocity = rb.angularVelocity;
        float yawSpeed = Mathf.Abs(angularVelocity.y);
        if (planarVelocity.magnitude > idleSpeedThreshold || yawSpeed > idleAngularSpeedThreshold)
        {
            return;
        }

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.Sleep();
    }

    private void ApplySpeedLimit()
    {
        Vector3 verticalVelocity = Vector3.Project(rb.linearVelocity, Vector3.up);
        Vector3 planarVelocity = Vector3.ProjectOnPlane(rb.linearVelocity, Vector3.up);

        if (planarVelocity.magnitude <= maxRollingSpeed)
        {
            return;
        }

        rb.linearVelocity = planarVelocity.normalized * maxRollingSpeed + verticalVelocity;
    }

    private void ApplyUprightStability()
    {
        Vector3 tiltAxis = Vector3.Cross(transform.up, Vector3.up);
        rb.AddTorque(tiltAxis * uprightStrength, ForceMode.Acceleration);

        Vector3 planarAngularVelocity = Vector3.ProjectOnPlane(rb.angularVelocity, Vector3.up);
        rb.AddTorque(-planarAngularVelocity * uprightDamping, ForceMode.Acceleration);
    }

    private void UpdateWheelVisuals()
    {
        if (frontCasterPivots == null || frontCasterPivots.Length == 0)
        {
            return;
        }

        Vector3 frontPosition = GetAveragePosition(frontCasterPivots, frontWheelAxle, transform.position + transform.forward * 0.55f);
        Vector3 frontVelocity = Vector3.ProjectOnPlane(rb.GetPointVelocity(frontPosition), Vector3.up);

        float targetCasterAngle = 0f;
        if (frontVelocity.magnitude > wheelVisualSpeedThreshold)
        {
            targetCasterAngle = Vector3.SignedAngle(transform.forward, frontVelocity.normalized, Vector3.up);
            targetCasterAngle += casterTrailYawOffset;
            targetCasterAngle = Mathf.DeltaAngle(0f, targetCasterAngle);
            targetCasterAngle = Mathf.Clamp(targetCasterAngle, -maxCasterAngle, maxCasterAngle);
        }

        currentCasterAngle = Mathf.Lerp(currentCasterAngle, targetCasterAngle, casterResponse * Time.fixedDeltaTime);
        for (int i = 0; i < frontCasterPivots.Length; i++)
        {
            if (frontCasterPivots[i] == null)
            {
                continue;
            }

            Quaternion baseRotation = i < frontCasterBaseRotations.Length ? frontCasterBaseRotations[i] : Quaternion.identity;
            frontCasterPivots[i].localRotation = baseRotation * Quaternion.Euler(0f, currentCasterAngle, 0f);
        }

        if (rearWheelPivots == null)
        {
            return;
        }

        for (int i = 0; i < rearWheelPivots.Length; i++)
        {
            if (rearWheelPivots[i] == null)
            {
                continue;
            }

            Quaternion baseRotation = i < rearWheelBaseRotations.Length ? rearWheelBaseRotations[i] : Quaternion.identity;
            rearWheelPivots[i].localRotation = baseRotation;
        }
    }

    private void OnValidate()
    {
        cartMass = Mathf.Max(0.1f, cartMass);
        linearDamping = Mathf.Max(0f, linearDamping);
        angularDamping = Mathf.Max(0f, angularDamping);
        maxRollingSpeed = Mathf.Max(0.1f, maxRollingSpeed);
        rollingResistance = Mathf.Max(0f, rollingResistance);
        lowSpeedBrake = Mathf.Max(0f, lowSpeedBrake);
        rearWheelSideGrip = Mathf.Max(0f, rearWheelSideGrip);
        frontCasterSideGrip = Mathf.Max(0f, frontCasterSideGrip);
        casterYawAssist = Mathf.Max(0f, casterYawAssist);
        forwardPushMultiplier = Mathf.Max(0f, forwardPushMultiplier);
        sidePivotMomentumGain = Mathf.Max(0f, sidePivotMomentumGain);
        sidePivotMaxMomentum = Mathf.Max(0f, sidePivotMaxMomentum);
        sidePivotTurnSpeed = Mathf.Max(0f, sidePivotTurnSpeed);
        sidePivotRollSpeed = Mathf.Max(0f, sidePivotRollSpeed);
        sidePivotAlignmentToRoll = Mathf.Clamp01(sidePivotAlignmentToRoll);
        sidePivotMomentumDamping = Mathf.Max(0f, sidePivotMomentumDamping);
        forwardRollMomentumGain = Mathf.Max(0f, forwardRollMomentumGain);
        forwardRollMaxMomentum = Mathf.Max(0f, forwardRollMaxMomentum);
        forwardRollSpeed = Mathf.Max(0f, forwardRollSpeed);
        forwardRollMomentumDamping = Mathf.Max(0f, forwardRollMomentumDamping);
        rollingAlignmentMinSpeed = Mathf.Max(0f, rollingAlignmentMinSpeed);
        rollingAlignmentTurnSpeed = Mathf.Max(0f, rollingAlignmentTurnSpeed);
        rollingAlignmentSideGrip = Mathf.Max(0f, rollingAlignmentSideGrip);
        uprightStrength = Mathf.Max(0f, uprightStrength);
        uprightDamping = Mathf.Max(0f, uprightDamping);
        idleSleepDelay = Mathf.Max(0f, idleSleepDelay);
        idleSpeedThreshold = Mathf.Max(0f, idleSpeedThreshold);
        idleAngularSpeedThreshold = Mathf.Max(0f, idleAngularSpeedThreshold);
        nestedRowScanDistance = Mathf.Max(0f, nestedRowScanDistance);
        nestedRowLateralTolerance = Mathf.Max(0f, nestedRowLateralTolerance);
        nestedRowAlignmentDot = Mathf.Clamp01(nestedRowAlignmentDot);
        nestedRowAttachDistance = Mathf.Max(0f, nestedRowAttachDistance);
        nestedRowDetachDistance = Mathf.Max(nestedRowAttachDistance, nestedRowDetachDistance);
        nestedRowStepDistance = Mathf.Max(0.01f, nestedRowStepDistance);
        nestedRowSlotSpacing = Mathf.Max(0f, nestedRowSlotSpacing);
        nestedRowOverlapDistance = Mathf.Max(0f, nestedRowOverlapDistance);
        nestedRowPullForwardOffset = Mathf.Max(0f, nestedRowPullForwardOffset);
        nestedRowExtraCartWeight = Mathf.Max(0f, nestedRowExtraCartWeight);
        nestedRowCenterPivotBlend = Mathf.Clamp01(nestedRowCenterPivotBlend);
        nestedCollisionRefreshInterval = Mathf.Max(0.02f, nestedCollisionRefreshInterval);
        playerTipPushForce = Mathf.Max(0f, playerTipPushForce);
        playerTipPushSpeed = Mathf.Max(0f, playerTipPushSpeed);
        playerTipPushTime = Mathf.Max(0f, playerTipPushTime);
        impactTipSpeed = Mathf.Max(0f, impactTipSpeed);
        impactTipMomentum = Mathf.Max(0f, impactTipMomentum);
        tipTorqueImpulse = Mathf.Max(0f, tipTorqueImpulse);
        tippedLinearDamping = Mathf.Max(0f, tippedLinearDamping);
        tippedAngularDamping = Mathf.Max(0f, tippedAngularDamping);
        tippedRollingResistanceMultiplier = Mathf.Max(1f, tippedRollingResistanceMultiplier);
        grabDistance = Mathf.Max(0f, grabDistance);
        grabSideDot = Mathf.Clamp(grabSideDot, -1f, 1f);
        grabbedDriveSpeed = Mathf.Max(0f, grabbedDriveSpeed);
        grabbedTurnSpeed = Mathf.Max(0f, grabbedTurnSpeed);
        playerHandleSpacing = Mathf.Max(0f, playerHandleSpacing);
        playerHandleLateralOffset = Mathf.Clamp(playerHandleLateralOffset, -2f, 2f);
        playerTurnSideStep = Mathf.Max(0f, playerTurnSideStep);
        handleGripWidth = Mathf.Max(0f, handleGripWidth);
        maxCasterAngle = Mathf.Clamp(maxCasterAngle, 0f, 180f);
        casterResponse = Mathf.Max(0f, casterResponse);
        casterTrailYawOffset = Mathf.DeltaAngle(0f, casterTrailYawOffset);
        wheelVisualSpeedThreshold = Mathf.Max(0f, wheelVisualSpeedThreshold);
    }
}
