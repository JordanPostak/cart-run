using UnityEngine;

[RequireComponent(typeof(Animator))]
public class PlayerCartHandleIK : MonoBehaviour
{
    [SerializeField] private float handWeight = 1f;
    [SerializeField] private bool reachForNearbyCarts = true;
    [SerializeField] private float fullReachDistance = 1.05f;
    [SerializeField] private float releaseDistance = 1.65f;
    [SerializeField] private float weightLerpSpeed = 7f;
    [SerializeField] private float cartSearchInterval = 0.15f;
    [SerializeField] private Vector3 leftHandOffset = Vector3.zero;
    [SerializeField] private Vector3 rightHandOffset = Vector3.zero;

    private Animator animator;
    private CartController forcedCart;
    private CartController nearbyCart;
    private float currentWeight;
    private float nextSearchTime;

    private void Awake()
    {
        animator = GetComponent<Animator>();
    }

    private void Update()
    {
        if (forcedCart != null)
        {
            nearbyCart = null;
        }
        else if (reachForNearbyCarts && Time.time >= nextSearchTime)
        {
            nearbyCart = FindNearestReachableCart();
            nextSearchTime = Time.time + cartSearchInterval;
        }

        CartController activeCart = GetActiveCart();
        float targetWeight = activeCart != null ? GetReachWeight(activeCart) : 0f;
        currentWeight = Mathf.MoveTowards(currentWeight, targetWeight, weightLerpSpeed * Time.deltaTime);
    }

    public void SetCart(CartController grabbedCart)
    {
        forcedCart = grabbedCart;
    }

    private void OnAnimatorIK(int layerIndex)
    {
        CartController activeCart = GetActiveCart();
        if (animator == null || activeCart == null || currentWeight <= 0f)
        {
            SetHandWeight(AvatarIKGoal.LeftHand, 0f);
            SetHandWeight(AvatarIKGoal.RightHand, 0f);
            return;
        }

        activeCart.GetHandleGripTargets(out Vector3 leftHandPosition, out Vector3 rightHandPosition, out Quaternion handRotation);
        SetHandTarget(AvatarIKGoal.LeftHand, leftHandPosition + leftHandOffset, handRotation);
        SetHandTarget(AvatarIKGoal.RightHand, rightHandPosition + rightHandOffset, handRotation);
    }

    private void SetHandTarget(AvatarIKGoal hand, Vector3 position, Quaternion rotation)
    {
        SetHandWeight(hand, handWeight * currentWeight);
        animator.SetIKPosition(hand, position);
        animator.SetIKRotation(hand, rotation);
    }

    private void SetHandWeight(AvatarIKGoal hand, float weight)
    {
        animator.SetIKPositionWeight(hand, weight);
        animator.SetIKRotationWeight(hand, weight);
    }

    private CartController GetActiveCart()
    {
        if (forcedCart != null)
        {
            return forcedCart;
        }

        return reachForNearbyCarts ? nearbyCart : null;
    }

    private CartController FindNearestReachableCart()
    {
        CartController closestCart = null;
        float closestDistance = releaseDistance;

        foreach (CartController candidate in CartController.ActiveCarts)
        {
            if (candidate == null)
            {
                continue;
            }

            float distance = GetDistanceToHandle(candidate);
            if (distance <= closestDistance)
            {
                closestDistance = distance;
                closestCart = candidate;
            }
        }

        return closestCart;
    }

    private float GetReachWeight(CartController targetCart)
    {
        if (targetCart == null)
        {
            return 0f;
        }

        float distance = GetDistanceToHandle(targetCart);
        if (distance >= releaseDistance)
        {
            return 0f;
        }

        if (distance <= fullReachDistance)
        {
            return 1f;
        }

        return Mathf.InverseLerp(releaseDistance, fullReachDistance, distance);
    }

    private float GetDistanceToHandle(CartController targetCart)
    {
        targetCart.GetHandleGripTargets(out Vector3 leftHandPosition, out Vector3 rightHandPosition, out Quaternion _);
        Vector3 handleCenter = (leftHandPosition + rightHandPosition) * 0.5f;
        return Vector3.Distance(transform.position, handleCenter);
    }

    private void OnValidate()
    {
        handWeight = Mathf.Clamp01(handWeight);
        fullReachDistance = Mathf.Max(0.05f, fullReachDistance);
        releaseDistance = Mathf.Max(fullReachDistance, releaseDistance);
        weightLerpSpeed = Mathf.Max(0f, weightLerpSpeed);
        cartSearchInterval = Mathf.Max(0.02f, cartSearchInterval);
    }
}
