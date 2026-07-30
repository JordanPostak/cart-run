using UnityEngine;

public class BasicRigidBodyPush : MonoBehaviour
{
	public LayerMask pushLayers;
	public bool canPush;
	[Range(0.5f, 5f)] public float strength = 1.1f;
	[SerializeField] private float cartPushStrength = 320f;

	private void OnControllerColliderHit(ControllerColliderHit hit)
	{
		if (TryPushCart(hit))
		{
			return;
		}

		if (canPush) PushRigidBodies(hit);
	}

	private bool TryPushCart(ControllerColliderHit hit)
	{
		Rigidbody body = hit.collider.attachedRigidbody;
		if (body == null || body.isKinematic) return false;

		CartController cart = body.GetComponent<CartController>() ?? hit.collider.GetComponentInParent<CartController>();
		if (cart == null || cart.IsGrabbed) return false;
		if (hit.moveDirection.y < -0.3f) return false;

		Vector3 pushDirection = new Vector3(hit.moveDirection.x, 0.0f, hit.moveDirection.z);
		if (pushDirection.sqrMagnitude < 0.001f) return false;

		cart.ReceivePlayerPush(pushDirection.normalized * cartPushStrength, hit.point);
		return true;
	}

	private void PushRigidBodies(ControllerColliderHit hit)
	{
		// https://docs.unity3d.com/ScriptReference/CharacterController.OnControllerColliderHit.html

		// make sure we hit a non kinematic rigidbody
		Rigidbody body = hit.collider.attachedRigidbody;
		if (body == null || body.isKinematic) return;

		// make sure we only push desired layer(s)
		var bodyLayerMask = 1 << body.gameObject.layer;
		if ((bodyLayerMask & pushLayers.value) == 0) return;

		// We dont want to push objects below us
		if (hit.moveDirection.y < -0.3f) return;

		// Calculate push direction from move direction, horizontal motion only
		Vector3 pushDir = new Vector3(hit.moveDirection.x, 0.0f, hit.moveDirection.z);

		// Apply the push and take strength into account
		body.AddForce(pushDir * strength, ForceMode.Impulse);
	}

	private void OnValidate()
	{
		cartPushStrength = Mathf.Max(0f, cartPushStrength);
	}
}
