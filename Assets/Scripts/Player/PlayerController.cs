using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
public class PlayerController : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float capsuleHeight = 1.8f;
    [SerializeField] private float capsuleRadius = 0.35f;
    [SerializeField] private Vector3 capsuleCenter = new Vector3(0f, 0.9f, 0f);
    [SerializeField] private bool disableRootCapsuleWithNestedMovementController = true;
    [SerializeField] private LayerMask movementCollisionMask = -1;
    [SerializeField] private float pushForce = 190f;
    [SerializeField] private float skinWidth = 0.04f;
    [SerializeField] private bool enableCartGrab = false;
    [SerializeField] private bool enableCartGrabToggle = true;
    [SerializeField] private KeyCode cartGrabToggleKey = KeyCode.E;
    [SerializeField] private float handleGrabSearchRadius = 2.6f;
    [SerializeField] private float grabbedInputDeadZone = 0.12f;
    [SerializeField] private bool mouseControlsGrabbedCart = true;
    [SerializeField] private bool rightClickTogglesCartGrab = true;
    [SerializeField] private bool quickLeftClickReleasesCart = true;
    [SerializeField] private float leftClickReleaseMaxHoldTime = 0.18f;
    [SerializeField] private float rightClickReleaseDoubleClickWindow = 0.35f;
    [SerializeField] private bool enableGrabbedCartRowDetachKey = true;
    [SerializeField] private KeyCode detachGrabbedCartFromRowKey = KeyCode.Q;
    [SerializeField] private float mouseControlStopDistance = 0.35f;
    [SerializeField] private float mouseControlFullSpeedDistance = 2.4f;
    [SerializeField] private float mouseControlInputLerpSpeed = 10f;

    private Rigidbody rb;
    private CapsuleCollider capsule;
    private Vector3 movementInput;
    private Vector3 keyboardMovementInput;
    private Vector3 mouseMovementInput;
    private float leftMouseDownTime = -999f;
    private float lastRightClickTime = -999f;
    private float lockedY;
    private CartController grabbedCart;
    private Collider[] playerColliders;
    private Collider[] ignoredCartColliders;
    private PlayerCartHandleIK handleIK;
    private Animator playerAnimator;
    private Transform hipsBone;
    private Transform visualRoot;
    private Vector3 visualRootLocalPosition;
    private Quaternion visualRootLocalRotation;
    private bool useNestedMovementController;
    private Behaviour[] disabledGrabPushBehaviours;

    public bool IsPushingCart => grabbedCart != null;
    public bool IsPushingCartRow => grabbedCart != null && grabbedCart.HasRowMembers();

    public float GetPushedCartRowTurnResponseMultiplier()
    {
        return grabbedCart != null ? grabbedCart.GetGrabbedRowTurnResponseMultiplier() : 1f;
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        capsule = GetComponent<CapsuleCollider>();
        handleIK = GetComponentInChildren<PlayerCartHandleIK>();
        playerAnimator = GetComponentInChildren<Animator>();
        if (handleIK == null)
        {
            if (playerAnimator != null)
            {
                handleIK = playerAnimator.gameObject.AddComponent<PlayerCartHandleIK>();
            }
        }

        lockedY = transform.position.y;
        if (capsule == null)
        {
            capsule = gameObject.AddComponent<CapsuleCollider>();
        }

        rb.isKinematic = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        rb.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        CacheVisualRoot();
        ConfigureRootCapsule();
        playerColliders = GetComponentsInChildren<Collider>();
    }

    private void ConfigureRootCapsule()
    {
        capsule.height = capsuleHeight;
        capsule.radius = capsuleRadius;
        capsule.center = capsuleCenter;
        capsule.isTrigger = false;

        // Starter Assets uses a CharacterController on the nested armature. When that controller
        // is active, the root capsule should not stay behind as an invisible blocker.
        capsule.enabled = !useNestedMovementController || !disableRootCapsuleWithNestedMovementController;
    }

    private void Update()
    {
        // Read input from WASD or arrow keys
        float horizontalInput = Input.GetAxisRaw("Horizontal");
        float verticalInput = Input.GetAxisRaw("Vertical");

        // Build a movement vector on the XZ plane
        keyboardMovementInput = new Vector3(horizontalInput, 0f, verticalInput);

        // Normalize so diagonal movement is not faster
        if (keyboardMovementInput.sqrMagnitude > 1f)
        {
            keyboardMovementInput.Normalize();
        }

        if (enableCartGrabToggle && Input.GetKeyDown(cartGrabToggleKey))
        {
            ToggleCartGrab();
        }

        if (rightClickTogglesCartGrab && Input.GetMouseButtonDown(1))
        {
            HandleRightClickCartAction();
        }

        HandleGrabbedCartRowDetachInput();

        if (Input.GetMouseButtonDown(0))
        {
            leftMouseDownTime = Time.time;
        }

        if (ShouldReleaseCartFromQuickLeftClick())
        {
            grabbedCart.ReleaseGrab(this);
        }

        movementInput = GetDesiredMovementInput();
    }

    private void FixedUpdate()
    {
        if (grabbedCart != null)
        {
            HandleGrabbedCartMovement();
            return;
        }

        if (TryGrabNearbyCart())
        {
            return;
        }

        if (useNestedMovementController)
        {
            return;
        }

        Vector3 movement = movementInput * moveSpeed * Time.fixedDeltaTime;
        Vector3 allowedMovement = GetAllowedMovement(movement);
        Vector3 nextPosition = rb.position + allowedMovement;
        nextPosition.y = lockedY;
        rb.MovePosition(nextPosition);
    }

    private void LateUpdate()
    {
        if (grabbedCart != null)
        {
            grabbedCart.SetGrabberFollowTarget(this, GetGrabberPosition(), GetGrabberRotation(), new Vector2(movementInput.x, movementInput.z));
        }
    }

    public void AttachToCart(CartController cart)
    {
        if (grabbedCart == cart)
        {
            return;
        }

        RestoreIgnoredCartCollisions();
        DisableGrabPushBehaviours();
        grabbedCart = cart;
        ignoredCartColliders = cart.GetComponentsInChildren<Collider>();
        foreach (Collider playerCollider in playerColliders)
        {
            foreach (Collider cartCollider in ignoredCartColliders)
            {
                if (playerCollider != null && cartCollider != null)
                {
                    Physics.IgnoreCollision(playerCollider, cartCollider, true);
                }
            }
        }

        if (handleIK != null)
        {
            handleIK.SetCart(cart);
        }

        cart.SetGrabberFollowTarget(this, GetGrabberPosition(), GetGrabberRotation(), new Vector2(movementInput.x, movementInput.z));
    }

    public void DetachFromCart(CartController cart)
    {
        if (grabbedCart != cart)
        {
            return;
        }

        RestoreGrabPushBehaviours();
        RestoreIgnoredCartCollisions();
        if (handleIK != null)
        {
            handleIK.SetCart(null);
        }

        grabbedCart = null;
    }

    public Vector3 GetGrabberPosition()
    {
        if (hipsBone != null)
        {
            Vector3 position = hipsBone.position;
            position.y = visualRoot != null ? visualRoot.position.y : transform.position.y;
            return position;
        }

        return visualRoot != null ? visualRoot.position : transform.position;
    }

    public Quaternion GetGrabberRotation()
    {
        return visualRoot != null ? visualRoot.rotation : transform.rotation;
    }

    public void SnapToCartGrabPose(Vector3 position, Quaternion rotation)
    {
        position.y = lockedY;
        rb.MovePosition(position);
        rb.MoveRotation(rotation);
        transform.SetPositionAndRotation(position, rotation);
        ResetVisualRoot();
    }

    private void CacheVisualRoot()
    {
        visualRoot = playerAnimator != null && playerAnimator.transform != transform ? playerAnimator.transform : null;
        if (playerAnimator != null && playerAnimator.isHuman)
        {
            hipsBone = playerAnimator.GetBoneTransform(HumanBodyBones.Hips);
        }

        foreach (Behaviour behaviour in GetComponentsInChildren<Behaviour>(true))
        {
            if (behaviour == null || behaviour == this || !IsNestedMovementBehaviour(behaviour))
            {
                continue;
            }

            useNestedMovementController = true;
            if (visualRoot == null && behaviour.transform != transform)
            {
                visualRoot = behaviour.transform;
            }
        }

        if (visualRoot != null)
        {
            visualRootLocalPosition = visualRoot.localPosition;
            visualRootLocalRotation = visualRoot.localRotation;
        }
    }

    private bool IsNestedMovementBehaviour(Behaviour behaviour)
    {
        string typeName = behaviour.GetType().FullName;
        return typeName == "StarterAssets.ThirdPersonController" || IsPushBehaviour(behaviour);
    }

    private void DisableGrabPushBehaviours()
    {
        List<Behaviour> pushBehaviours = new List<Behaviour>();
        foreach (Behaviour behaviour in GetComponentsInChildren<Behaviour>(true))
        {
            if (behaviour == null || !behaviour.enabled || !IsPushBehaviour(behaviour))
            {
                continue;
            }

            pushBehaviours.Add(behaviour);
            behaviour.enabled = false;
        }

        disabledGrabPushBehaviours = pushBehaviours.ToArray();
    }

    private bool IsPushBehaviour(Behaviour behaviour)
    {
        string typeName = behaviour.GetType().FullName;
        return typeName == "BasicRigidBodyPush" || typeName == "PushObj";
    }

    private void RestoreGrabPushBehaviours()
    {
        if (disabledGrabPushBehaviours == null)
        {
            return;
        }

        foreach (Behaviour behaviour in disabledGrabPushBehaviours)
        {
            if (behaviour != null)
            {
                behaviour.enabled = true;
            }
        }

        disabledGrabPushBehaviours = null;
    }

    private void ResetVisualRoot()
    {
        if (visualRoot == null)
        {
            return;
        }

        visualRoot.localPosition = visualRootLocalPosition;
        visualRoot.localRotation = visualRootLocalRotation;
    }

    private void RestoreIgnoredCartCollisions()
    {
        if (ignoredCartColliders == null || playerColliders == null)
        {
            return;
        }

        foreach (Collider playerCollider in playerColliders)
        {
            foreach (Collider cartCollider in ignoredCartColliders)
            {
                if (playerCollider != null && cartCollider != null)
                {
                    Physics.IgnoreCollision(playerCollider, cartCollider, false);
                }
            }
        }

        ignoredCartColliders = null;
    }

    private bool TryGrabNearbyCart()
    {
        if (!enableCartGrab)
        {
            return false;
        }

        foreach (CartController cart in CartController.ActiveCarts)
        {
            if (cart == null || cart.IsGrabbed || !cart.CanGrabFrom(GetGrabberPosition()))
            {
                continue;
            }

            if (cart.TryGrab(this))
            {
                return true;
            }
        }

        return false;
    }

    private void ToggleCartGrab()
    {
        if (grabbedCart != null)
        {
            CartController cartToRelease = grabbedCart;
            if (TryFindClosestGrabbableCart(out CartController nextCart, cartToRelease.GetRowGrabLeader()))
            {
                cartToRelease.ReleaseGrab(this);
                nextCart.TryGrab(this);
                return;
            }

            grabbedCart.ReleaseGrab(this);
            return;
        }

        TryGrabClosestCart();
    }

    private void HandleRightClickCartAction()
    {
        if (grabbedCart == null)
        {
            TryGrabClosestCart();
            lastRightClickTime = -999f;
            return;
        }

        bool doubleClick = Time.time - lastRightClickTime <= rightClickReleaseDoubleClickWindow;
        lastRightClickTime = Time.time;
        if (doubleClick)
        {
            grabbedCart.ReleaseGrab(this);
            return;
        }

        if (grabbedCart.TryStealBackCartFromNearbyRow())
        {
            return;
        }

        grabbedCart.TryAttachCartAheadToRow();
    }

    private void HandleGrabbedCartRowDetachInput()
    {
        if (!enableGrabbedCartRowDetachKey || grabbedCart == null || !Input.GetKeyDown(detachGrabbedCartFromRowKey))
        {
            return;
        }

        // Q only splits one cart from the row the player is currently holding.
        // It does not pull carts from nearby rows; that keeps row stealing as a later explicit action.
        CartController grabbedLeader = grabbedCart.GetRowGrabLeader();
        if (!grabbedLeader.TryDetachBackCartFromRow(out CartController detachedCart) || detachedCart == null)
        {
            return;
        }

        if (detachedCart == grabbedCart)
        {
            return;
        }

        grabbedLeader.ReleaseGrab(this);
        detachedCart.TryGrab(this);
    }

    private bool TryGrabClosestCart()
    {
        return TryFindClosestGrabbableCart(out CartController closestCart) && closestCart.TryGrab(this);
    }

    private bool TryFindClosestGrabbableCart(out CartController closestCart, CartController excludedGrabLeader = null)
    {
        closestCart = null;
        float closestDistance = handleGrabSearchRadius;
        Vector3 grabberPosition = GetGrabberPosition();

        foreach (CartController cart in CartController.ActiveCarts)
        {
            if (cart == null || cart.GetRowGrabLeader() == excludedGrabLeader || cart.IsGrabbed || !cart.CanGrabFrom(grabberPosition))
            {
                continue;
            }

            float distance = Vector3.Distance(grabberPosition, cart.GetCartGrabPointWorldPosition());
            if (distance <= closestDistance)
            {
                closestDistance = distance;
                closestCart = cart;
            }
        }

        return closestCart != null;
    }

    private bool ShouldReleaseCartFromQuickLeftClick()
    {
        if (!quickLeftClickReleasesCart || grabbedCart == null || !Input.GetMouseButtonUp(0))
        {
            return false;
        }

        // Left mouse is also used for click-to-move and double-click run while pushing carts.
        // Keep cart release on the explicit grab controls in that mode so running never drops the handle.
        if (mouseControlsGrabbedCart)
        {
            return false;
        }

        return Time.time - leftMouseDownTime <= leftClickReleaseMaxHoldTime;
    }

    private Vector3 GetDesiredMovementInput()
    {
        if (grabbedCart == null || !mouseControlsGrabbedCart)
        {
            return keyboardMovementInput;
        }

        Vector3 targetMouseInput = Input.GetMouseButton(0) && TryGetMouseMovementInput(out Vector3 mouseInput) ? mouseInput : Vector3.zero;
        mouseMovementInput = Vector3.MoveTowards(mouseMovementInput, targetMouseInput, mouseControlInputLerpSpeed * Time.deltaTime);
        return mouseMovementInput.sqrMagnitude > keyboardMovementInput.sqrMagnitude ? mouseMovementInput : keyboardMovementInput;
    }

    private bool TryGetMouseMovementInput(out Vector3 input)
    {
        input = Vector3.zero;
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            return false;
        }

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        Plane groundPlane = new Plane(Vector3.up, new Vector3(0f, lockedY, 0f));
        if (!groundPlane.Raycast(ray, out float rayDistance))
        {
            return false;
        }

        Vector3 mouseWorldPoint = ray.GetPoint(rayDistance);
        Vector3 toMouse = Vector3.ProjectOnPlane(mouseWorldPoint - GetGrabberPosition(), Vector3.up);
        float distance = toMouse.magnitude;
        if (distance <= mouseControlStopDistance)
        {
            return true;
        }

        float speedBlend = Mathf.InverseLerp(mouseControlStopDistance, mouseControlFullSpeedDistance, distance);
        input = toMouse.normalized * speedBlend;
        return true;
    }

    private void HandleGrabbedCartMovement()
    {
        if (grabbedCart.IsTipped || !grabbedCart.IsGrabbedBy(this))
        {
            RestoreGrabPushBehaviours();
            RestoreIgnoredCartCollisions();
            if (handleIK != null)
            {
                handleIK.SetCart(null);
            }

            grabbedCart = null;
            return;
        }

        Vector2 cartInput = new Vector2(movementInput.x, movementInput.z);
        if (cartInput.magnitude < grabbedInputDeadZone)
        {
            cartInput = Vector2.zero;
        }

        grabbedCart.SetGrabInput(this, cartInput);
    }

    private Vector3 GetAllowedMovement(Vector3 movement)
    {
        float distance = movement.magnitude;
        if (distance <= 0f)
        {
            return Vector3.zero;
        }

        Vector3 direction = movement / distance;
        Vector3 worldCenter = transform.TransformPoint(capsule.center);
        float halfHeight = Mathf.Max(0f, (capsule.height * 0.5f) - capsule.radius);
        Vector3 bottom = worldCenter + Vector3.down * halfHeight;
        Vector3 top = worldCenter + Vector3.up * halfHeight;

        if (!Physics.CapsuleCast(bottom, top, capsule.radius, direction, out RaycastHit hit, distance + skinWidth, movementCollisionMask, QueryTriggerInteraction.Ignore))
        {
            return movement;
        }

        CartController cart = hit.rigidbody != null ? hit.rigidbody.GetComponent<CartController>() : hit.collider.GetComponentInParent<CartController>();
        if (cart != null)
        {
            if (enableCartGrab && cart.TryGrab(this))
            {
                return Vector3.zero;
            }

            cart.ReceivePlayerPush(direction * pushForce, hit.point);
        }

        float allowedDistance = Mathf.Max(0f, hit.distance - skinWidth);
        return direction * allowedDistance;
    }

    private void OnValidate()
    {
        moveSpeed = Mathf.Max(0f, moveSpeed);
        capsuleHeight = Mathf.Max(0.1f, capsuleHeight);
        capsuleRadius = Mathf.Max(0.01f, capsuleRadius);
        pushForce = Mathf.Max(0f, pushForce);
        skinWidth = Mathf.Max(0f, skinWidth);
        handleGrabSearchRadius = Mathf.Max(0f, handleGrabSearchRadius);
        grabbedInputDeadZone = Mathf.Clamp01(grabbedInputDeadZone);
        leftClickReleaseMaxHoldTime = Mathf.Max(0f, leftClickReleaseMaxHoldTime);
        rightClickReleaseDoubleClickWindow = Mathf.Max(0f, rightClickReleaseDoubleClickWindow);
        mouseControlStopDistance = Mathf.Max(0f, mouseControlStopDistance);
        mouseControlFullSpeedDistance = Mathf.Max(mouseControlStopDistance, mouseControlFullSpeedDistance);
        mouseControlInputLerpSpeed = Mathf.Max(0f, mouseControlInputLerpSpeed);
    }
}
