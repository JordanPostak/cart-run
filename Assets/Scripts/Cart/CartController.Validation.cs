using UnityEngine;

public partial class CartController
{
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
        nestedRowAttachZoneDepth = Mathf.Max(0f, nestedRowAttachZoneDepth);
        nestedRowAttachZoneWidth = Mathf.Max(0f, nestedRowAttachZoneWidth);
        nestedRowLateralTolerance = Mathf.Max(0f, nestedRowLateralTolerance);
        nestedRowAlignmentDot = Mathf.Clamp01(nestedRowAlignmentDot);
        nestedRowAttachDistance = Mathf.Max(0f, nestedRowAttachDistance);
        nestedRowAttachOverlapAllowance = Mathf.Max(0f, nestedRowAttachOverlapAllowance);
        nestedRowDetachDistance = Mathf.Max(nestedRowAttachDistance, nestedRowDetachDistance);
        nestedRowStepDistance = Mathf.Max(0.01f, nestedRowStepDistance);
        nestedRowSlotSpacing = Mathf.Max(0f, nestedRowSlotSpacing);
        nestedRowOverlapDistance = Mathf.Max(0f, nestedRowOverlapDistance);
        nestedRowPullForwardOffset = Mathf.Max(0f, nestedRowPullForwardOffset);
        nestedRowExtraCartWeight = Mathf.Max(0f, nestedRowExtraCartWeight);
        nestedRowCenterPivotBlend = Mathf.Clamp01(nestedRowCenterPivotBlend);
        nestedRowGrabbedTurnSpeedMultiplier = Mathf.Clamp01(nestedRowGrabbedTurnSpeedMultiplier);
        nestedRowGrabbedTurnSlowdownPerExtraCart = Mathf.Max(0f, nestedRowGrabbedTurnSlowdownPerExtraCart);
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
