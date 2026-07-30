using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PushObj : MonoBehaviour
{
    [SerializeField] private float pushPower = 4f;
    [SerializeField] private ForceMode forceMode = ForceMode.Impulse;

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        Rigidbody body = hit.collider.attachedRigidbody;
        if (body == null || body.isKinematic)
        {
            return;
        }

        if (hit.moveDirection.y < -0.3f)
        {
            return;
        }

        CartController cart = body.GetComponent<CartController>();
        if (cart != null && cart.IsGrabbed)
        {
            return;
        }

        Vector3 pushDirection = new Vector3(hit.moveDirection.x, 0f, hit.moveDirection.z);
        if (pushDirection.sqrMagnitude < 0.001f)
        {
            return;
        }

        body.AddForceAtPosition(pushDirection.normalized * pushPower, hit.point, forceMode);
    }

    private void OnValidate()
    {
        pushPower = Mathf.Max(0f, pushPower);
    }
}
