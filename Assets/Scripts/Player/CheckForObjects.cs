using UnityEngine;
using System.Collections.Generic;

public class CheckForObjects : MonoBehaviour
{
    private float stepOffset;
    [SerializeField] private CharacterController CC;
    [SerializeField] private float blockedStepOffset = 0f;
    [SerializeField] private bool pushCarts = true;
    [SerializeField] private float cartPushStrength = 85f;
    [SerializeField] private float maxCartPushSpeed = 3.2f;
    [SerializeField] private float minimumCartPushSpeed = 0.08f;

    private readonly HashSet<Collider> blockingObjects = new HashSet<Collider>();

    private void Awake()
    {
        if (CC == null)
        {
            CC = GetComponentInParent<CharacterController>();
        }
    }

    private void Start()
    {
        if (CC == null)
        {
            Debug.LogWarning($"{nameof(CheckForObjects)} needs a CharacterController assigned.", this);
            enabled = false;
            return;
        }

        stepOffset = CC.stepOffset;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (IsBlockingObject(other))
        {
            blockingObjects.Add(other);
            CC.stepOffset = blockedStepOffset;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (blockingObjects.Remove(other) && blockingObjects.Count == 0)
        {
            CC.stepOffset = stepOffset;
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (!pushCarts || CC == null)
        {
            return;
        }

        CartController cart = GetCart(other);
        if (cart == null || cart.IsGrabbed)
        {
            return;
        }

        Vector3 pushVelocity = Vector3.ProjectOnPlane(CC.velocity, Vector3.up);
        if (pushVelocity.magnitude < minimumCartPushSpeed)
        {
            return;
        }

        pushVelocity = Vector3.ClampMagnitude(pushVelocity, maxCartPushSpeed);
        Vector3 pushPoint = other.ClosestPoint(transform.position);
        if ((pushPoint - transform.position).sqrMagnitude < 0.0001f)
        {
            pushPoint = other.bounds.center;
        }

        cart.ReceivePlayerPush(pushVelocity * cartPushStrength, pushPoint);
    }

    private void OnDisable()
    {
        if (CC != null)
        {
            CC.stepOffset = stepOffset;
        }

        blockingObjects.Clear();
    }

    private bool IsBlockingObject(Collider other)
    {
        return other.attachedRigidbody != null && !other.attachedRigidbody.isKinematic;
    }

    private CartController GetCart(Collider other)
    {
        if (other.attachedRigidbody != null)
        {
            CartController cart = other.attachedRigidbody.GetComponent<CartController>();
            if (cart != null)
            {
                return cart;
            }
        }

        return other.GetComponentInParent<CartController>();
    }

    private void OnValidate()
    {
        cartPushStrength = Mathf.Max(0f, cartPushStrength);
        maxCartPushSpeed = Mathf.Max(0f, maxCartPushSpeed);
        minimumCartPushSpeed = Mathf.Max(0f, minimumCartPushSpeed);
    }
}
