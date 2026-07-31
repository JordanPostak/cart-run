using UnityEngine;

public partial class CartController
{
    private void ApplyCartWheelFriction()
    {
        Vector3 rearPosition = CartPositionUtility.GetAveragePosition(rearWheelPivots, rearWheelAxle, transform.position - transform.forward * 0.55f);
        Vector3 frontPosition = CartPositionUtility.GetAveragePosition(frontCasterPivots, frontWheelAxle, transform.position + transform.forward * 0.55f);
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

        Vector3 frontPosition = CartPositionUtility.GetAveragePosition(frontCasterPivots, frontWheelAxle, transform.position + transform.forward * 0.55f);
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
}
