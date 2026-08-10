using UnityEngine;

public partial class CartController
{
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
        bool hitByCustomer = collision.transform.GetComponentInParent<CustomerCartPusher>() != null;

        if (hitByPlayer || hitByCustomer)
        {
            WakeDormantCart();
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
}
