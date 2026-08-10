using System.Collections.Generic;
using UnityEngine;

public partial class CartController
{
    public void ReceivePlayerPush(Vector3 force, Vector3 point)
    {
        if (rowLeader != null && rowLeader != this)
        {
            rowLeader.ReceivePlayerPush(force, point);
            return;
        }

        WakeDormantCart();

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
        LimitRowMotionByCollision(row, pivot, ref deltaRotation, ref deltaPosition);

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
}
