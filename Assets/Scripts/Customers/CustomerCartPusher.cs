using UnityEngine;
using StarterAssets;

[RequireComponent(typeof(CharacterController))]
public class CustomerCartPusher : MonoBehaviour, ICartGrabber
{
    [Header("Movement")]
    [SerializeField] private float walkSpeed = 1.8f;
    [SerializeField] private float turnSpeed = 540f;
    [SerializeField] private float destinationStopDistance = 0.7f;
    [SerializeField] private float despawnDelayAfterDropoff = 1.5f;

    [Header("Blocked Cart Recovery")]
    [SerializeField] private float blockedProgressEpsilon = 0.03f;
    [SerializeField] private float blockedSecondsBeforeDropoff = 1.25f;
    [SerializeField] private float blockedDropoffDistance = 3f;

    [Header("Cart Hold")]
    [SerializeField] private Vector3 grabberLocalOffset = new Vector3(0f, 0f, 0.35f);
    [SerializeField] private float cartSpawnForwardDistance = 0.05f;
    [SerializeField] private Vector3 spawnedCartScale = Vector3.one;
    [SerializeField] private LayerMask cartGroundLayers = -1;
    [SerializeField] private float cartGroundRayHeight = 3f;
    [SerializeField] private float cartGroundRayDistance = 8f;
    [SerializeField] private float cartRootGroundOffset = 0.013f;

    [Header("Animation")]
    [SerializeField] private Animator characterAnimator;
    [SerializeField] private RuntimeAnimatorController fallbackAnimatorController;
    [SerializeField] private float animationSpeedSmoothing = 10f;

    private CharacterController characterController;
    private CartController cart;
    private CustomerCartSpawner spawner;
    private PlayerCartHandleIK handleIK;
    private Vector3 destination;
    private Vector3[] routePoints;
    private int routeIndex;
    private bool hasDestination;
    private bool initialized;
    private float dropoffTime = -999f;
    private float currentAnimationSpeed;
    private float previousCartDistanceToDestination = float.PositiveInfinity;
    private float blockedTimer;

    private static readonly int AnimIDSpeed = Animator.StringToHash("Speed");
    private static readonly int AnimIDGrounded = Animator.StringToHash("Grounded");
    private static readonly int AnimIDMotionSpeed = Animator.StringToHash("MotionSpeed");

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();

        // A scene/template reference can point at the original character instead of the spawned
        // clone. Always prefer the Animator that belongs to this customer instance.
        if (characterAnimator == null || !characterAnimator.transform.IsChildOf(transform))
        {
            characterAnimator = GetComponentInChildren<Animator>();
        }

        ConfigureAnimatorController();
        ConfigureHandleIK();
    }

    public void Initialize(CartController spawnedCart, Vector3 parkingLotDestination)
    {
        Initialize(spawnedCart, new[] { parkingLotDestination });
    }

    public void Initialize(CartController spawnedCart, Vector3[] parkingLotRoute)
    {
        Initialize(spawnedCart, parkingLotRoute, null);
    }

    public void Initialize(CartController spawnedCart, Vector3[] parkingLotRoute, CustomerCartSpawner owningSpawner)
    {
        cart = spawnedCart;
        spawner = owningSpawner;
        routePoints = parkingLotRoute;
        routeIndex = 0;
        hasDestination = TrySetCurrentDestination();
        initialized = true;

        if (cart == null)
        {
            return;
        }

        PlaceCartAtHands();
        cart.TryGrab(this);
        SetHandleIKCart(cart);
        cart.SetGrabberFollowTarget(this, GetGrabberPosition(), GetGrabberRotation(), Vector2.zero);
    }

    private void Update()
    {
        if (!initialized)
        {
            UpdateAnimation(0f);
            return;
        }

        if (!hasDestination)
        {
            UpdateAfterDropoff();
            return;
        }

        Vector3 toDestination = Vector3.ProjectOnPlane(destination - transform.position, Vector3.up);
        float distance = toDestination.magnitude;
        if (distance <= destinationStopDistance)
        {
            routeIndex++;
            if (!TrySetCurrentDestination())
            {
                DropCart();
            }

            return;
        }

        Vector3 moveDirection = toDestination.normalized;
        TurnToward(moveDirection);
        Move(moveDirection);

        if (cart != null)
        {
            cart.SetGrabberFollowTarget(this, GetGrabberPosition(), GetGrabberRotation(), new Vector2(moveDirection.x, moveDirection.z));
        }

        UpdateBlockedCartRecovery(distance);

        UpdateAnimation(walkSpeed);
    }

    public Vector3 GetGrabberPosition()
    {
        return transform.TransformPoint(grabberLocalOffset);
    }

    public Quaternion GetGrabberRotation()
    {
        return transform.rotation;
    }

    public void AttachToCart(CartController attachedCart)
    {
        cart = attachedCart;
        SetHandleIKCart(attachedCart);
    }

    public void DetachFromCart(CartController detachedCart)
    {
        if (cart == detachedCart)
        {
            SetHandleIKCart(null);
            cart = null;
        }
    }

    private void PlaceCartAtHands()
    {
        Transform cartTransform = cart.transform;
        cartTransform.localScale = spawnedCartScale;

        Vector3 cartPosition = transform.position + transform.forward * cartSpawnForwardDistance;
        cartPosition.y = transform.position.y;
        cartTransform.SetPositionAndRotation(cartPosition, transform.rotation);
        Physics.SyncTransforms();

        Vector3 grabPointOffset = cart.GetCartGrabPointWorldPosition() - cartTransform.position;
        Vector3 alignedCartPosition = GetGrabberPosition() - grabPointOffset;
        // Match the handle position horizontally only. Vertical placement is corrected from
        // the cart bounds below so handle height can never lift the whole cart into the air.
        alignedCartPosition.y = cartTransform.position.y;
        cartTransform.position = alignedCartPosition;
        Physics.SyncTransforms();
        PlantCartOnCustomerGround();

        Rigidbody cartBody = cart.GetComponent<Rigidbody>();
        if (cartBody != null)
        {
            cartBody.position = cartTransform.position;
            cartBody.rotation = cartTransform.rotation;
            cartBody.linearVelocity = Vector3.zero;
            cartBody.angularVelocity = Vector3.zero;
        }

        cart.RefreshPlantedHeight();
    }

    private void PlantCartOnCustomerGround()
    {
        Transform cartTransform = cart.transform;
        Vector3 plantedPosition = cartTransform.position;
        plantedPosition.y = GetGroundHeightBelowCart() + cartRootGroundOffset;
        cartTransform.position = plantedPosition;
        Physics.SyncTransforms();
    }

    private float GetGroundHeightBelowCart()
    {
        Vector3 rayStart = cart.transform.position + Vector3.up * cartGroundRayHeight;
        RaycastHit[] hits = Physics.RaycastAll(rayStart, Vector3.down, cartGroundRayHeight + cartGroundRayDistance, cartGroundLayers, QueryTriggerInteraction.Ignore);
        float bestHeight = transform.position.y;
        float closestDistance = float.PositiveInfinity;

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == null || hit.collider.transform.IsChildOf(cart.transform) || hit.collider.transform.IsChildOf(transform))
            {
                continue;
            }

            if (hit.distance < closestDistance)
            {
                closestDistance = hit.distance;
                bestHeight = hit.point.y;
            }
        }

        return bestHeight;
    }

    private void Move(Vector3 moveDirection)
    {
        Vector3 velocity = moveDirection * walkSpeed;
        if (characterController != null && characterController.enabled)
        {
            characterController.SimpleMove(velocity);
            return;
        }

        transform.position += velocity * Time.deltaTime;
    }

    private bool TrySetCurrentDestination()
    {
        if (routePoints == null)
        {
            return false;
        }

        while (routeIndex < routePoints.Length)
        {
            destination = routePoints[routeIndex];
            ResetBlockedCartTracking();
            return true;
        }

        return false;
    }

    private void UpdateBlockedCartRecovery(float customerDistanceToDestination)
    {
        if (cart == null || routePoints == null || routeIndex < routePoints.Length - 1 || customerDistanceToDestination > blockedDropoffDistance)
        {
            ResetBlockedCartTracking();
            return;
        }

        float cartDistance = Vector3.ProjectOnPlane(destination - cart.transform.position, Vector3.up).magnitude;
        if (previousCartDistanceToDestination < float.PositiveInfinity && cartDistance >= previousCartDistanceToDestination - blockedProgressEpsilon)
        {
            blockedTimer += Time.deltaTime;
        }
        else
        {
            blockedTimer = 0f;
        }

        previousCartDistanceToDestination = cartDistance;
        if (blockedTimer >= blockedSecondsBeforeDropoff)
        {
            DropCart();
        }
    }

    private void ResetBlockedCartTracking()
    {
        blockedTimer = 0f;
        previousCartDistanceToDestination = float.PositiveInfinity;
    }

    private void ConfigureAnimatorController()
    {
        if (characterAnimator == null || characterAnimator.runtimeAnimatorController != null)
        {
            return;
        }

        // Customer models sometimes have an Avatar but no controller after being used as
        // swappable visuals. Copy the Starter Assets controller so Speed/Grounded/MotionSpeed
        // parameters actually drive walk animation instead of leaving the body in bind pose.
        if (fallbackAnimatorController != null)
        {
            characterAnimator.runtimeAnimatorController = fallbackAnimatorController;
            return;
        }

        RuntimeAnimatorController starterRuntimeController = FindStarterAssetsRuntimeController();
        if (starterRuntimeController != null)
        {
            characterAnimator.runtimeAnimatorController = starterRuntimeController;
        }
    }

    private void ConfigureHandleIK()
    {
        if (characterAnimator == null)
        {
            return;
        }

        // Customers use the same hand IK as the player so their arms reach to the real cart
        // handle instead of only playing a generic walk animation while attached.
        handleIK = characterAnimator.GetComponent<PlayerCartHandleIK>();
        if (handleIK == null)
        {
            handleIK = characterAnimator.gameObject.AddComponent<PlayerCartHandleIK>();
        }
    }

    private void SetHandleIKCart(CartController targetCart)
    {
        if (handleIK != null)
        {
            handleIK.SetCart(targetCart);
        }
    }

    private RuntimeAnimatorController FindStarterAssetsRuntimeController()
    {
        ThirdPersonController starterController = FindAnyObjectByType<ThirdPersonController>();
        Animator starterAnimator = starterController != null ? starterController.GetComponent<Animator>() : null;
        if (starterAnimator != null && starterAnimator.runtimeAnimatorController != null)
        {
            return starterAnimator.runtimeAnimatorController;
        }

        // The Player may have moved the Starter Assets controller onto a swapped child model.
        // Use any active Animator with a controller as a fallback so customers are animated too.
        Animator[] animators = FindObjectsByType<Animator>(FindObjectsInactive.Exclude);
        foreach (Animator animator in animators)
        {
            if (animator != null && animator != characterAnimator && animator.runtimeAnimatorController != null)
            {
                return animator.runtimeAnimatorController;
            }
        }

        return null;
    }

    private void TurnToward(Vector3 direction)
    {
        if (direction.sqrMagnitude < 0.001f)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, turnSpeed * Time.deltaTime);
    }

    private void DropCart()
    {
        hasDestination = false;
        dropoffTime = Time.time;
        if (cart != null)
        {
            CartController droppedCart = cart;
            droppedCart.ReleaseGrab(this);
            SettleDroppedCart(droppedCart);
            if (spawner != null)
            {
                spawner.RegisterParkedCustomerCart(droppedCart);
            }

            cart = null;
        }

        UpdateAnimation(0f);
    }

    private void SettleDroppedCart(CartController droppedCart)
    {
        if (droppedCart == null)
        {
            return;
        }

        droppedCart.ParkAsDormant();
    }

    private void UpdateAfterDropoff()
    {
        UpdateAnimation(0f);
        if (Time.time - dropoffTime >= despawnDelayAfterDropoff)
        {
            Destroy(gameObject);
        }
    }

    private void UpdateAnimation(float targetSpeed)
    {
        if (characterAnimator == null || !characterAnimator.isActiveAndEnabled)
        {
            return;
        }

        currentAnimationSpeed = Mathf.Lerp(currentAnimationSpeed, targetSpeed, 1f - Mathf.Exp(-animationSpeedSmoothing * Time.deltaTime));
        characterAnimator.SetBool(AnimIDGrounded, true);
        characterAnimator.SetFloat(AnimIDSpeed, currentAnimationSpeed);
        characterAnimator.SetFloat(AnimIDMotionSpeed, targetSpeed > 0.05f ? 1f : 0f);
    }
}
