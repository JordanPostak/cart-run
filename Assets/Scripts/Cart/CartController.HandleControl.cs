using System.Collections.Generic;
using UnityEngine;

public partial class CartController
{
    public bool TryGrab(PlayerController player)
    {
        CartController grabLeader = GetRowGrabLeader();
        if (grabLeader != this)
        {
            return grabLeader.TryGrab(player);
        }

        if (player == null || isTipped || grabbedPlayer != null || !IsPlayerOnHandleSide(player.GetGrabberPosition()))
        {
            return false;
        }

        grabbedPlayer = player;
        grabbedInput = Vector2.zero;
        hasGrabberFollowTarget = false;
        isTipped = false;
        keepCartUpright = true;
        keepCartPlanted = true;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.linearDamping = linearDamping;
        rb.angularDamping = angularDamping;
        ApplyRotationConstraints();
        UpdateGrabbedPlayerPose(rb.position, rb.rotation);
        grabbedPlayer.AttachToCart(this);
        return true;
    }

    public bool CanGrabFrom(Vector3 playerPosition)
    {
        CartController grabLeader = GetRowGrabLeader();
        return !grabLeader.isTipped && grabLeader.IsPlayerOnHandleSide(playerPosition);
    }

    public Vector3 GetCartGrabPointWorldPosition()
    {
        return GetRowGrabLeader().GetCartGrabPointPosition();
    }

    public void ReleaseGrab(PlayerController player)
    {
        if (grabbedPlayer == player)
        {
            grabbedPlayer.DetachFromCart(this);
            grabbedPlayer = null;
            grabbedInput = Vector2.zero;
            hasGrabberFollowTarget = false;
        }
    }

    public bool IsGrabbedBy(PlayerController player)
    {
        return grabbedPlayer == player;
    }

    public void SetGrabInput(PlayerController player, Vector2 input)
    {
        if (grabbedPlayer == player)
        {
            grabbedInput = Vector2.ClampMagnitude(input, 1f);
        }
    }

    public void SetGrabberFollowTarget(PlayerController player, Vector3 grabberPosition, Quaternion grabberRotation, Vector2 input)
    {
        if (grabbedPlayer != player || isTipped)
        {
            return;
        }

        grabbedInput = Vector2.ClampMagnitude(input, 1f);
        grabberFollowPosition = grabberPosition;
        grabberFollowRotation = grabberRotation;
        hasGrabberFollowTarget = true;
    }

    private void ApplyGrabberFollowTarget()
    {
        if (!hasGrabberFollowTarget)
        {
            return;
        }

        Vector3 grabberForward = Vector3.ProjectOnPlane(grabberFollowRotation * Vector3.forward, Vector3.up);
        if (grabberForward.sqrMagnitude < 0.001f)
        {
            grabberForward = transform.forward;
        }

        grabberForward.Normalize();
        Quaternion nextRotation = Quaternion.LookRotation(grabberForward, Vector3.up);
        Vector3 grabPointOffset = GetCartGrabPointOffset(nextRotation);
        Vector3 nextPosition = grabberFollowPosition - grabPointOffset;
        nextPosition.y = plantedY;

        List<CartController> row = GetExplicitRow();
        if (row.Count > 1)
        {
            ApplyRowGrabberFollowTarget(row, nextPosition, nextRotation);
        }
        else
        {
            rb.MoveRotation(nextRotation);
            rb.MovePosition(nextPosition);
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        UpdateGrabbedPlayerPose(nextPosition, nextRotation);
    }

    private void ApplyRowGrabberFollowTarget(List<CartController> row, Vector3 nextLeaderPosition, Quaternion nextLeaderRotation)
    {
        // The visual row aim below can temporarily rotate the whole row for presentation.
        // Reset to the authored row layout before doing real steering so that visual offset
        // never feeds back into physics or row-center math on the next frame.
        RebuildRowLayout(row);

        Vector3 rowCenter = GetExplicitRowCenter(row);
        Quaternion deltaRotation = GetRowCenterHandleDeltaRotation(rowCenter, row.Count);
        Vector3 rotatedGrabPoint = rowCenter + deltaRotation * (GetCartGrabPointPosition() - rowCenter);
        Vector3 targetGrabPoint = grabberFollowPosition;
        targetGrabPoint.y = rotatedGrabPoint.y;
        Vector3 deltaPosition = targetGrabPoint - rotatedGrabPoint;
        LimitRowMotionByCollision(row, rowCenter, ref deltaRotation, ref deltaPosition);

        foreach (CartController cart in row)
        {
            if (cart == null || cart.isTipped)
            {
                continue;
            }

            Vector3 nextCartPosition = rowCenter + deltaRotation * (cart.rb.position - rowCenter) + deltaPosition;
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
                cart.rb.MoveRotation(uprightRotation);
                cart.rb.MovePosition(nextCartPosition);
            }

            cart.rb.linearVelocity = Vector3.zero;
            cart.rb.angularVelocity = Vector3.zero;
            cart.forwardRollMomentum = 0f;
            cart.sidePivotMomentum = 0f;
            cart.hasSidePivotWorldPoint = false;
        }

        UpdateRowTransform(row);
        RebuildRowLayout(row);
        ApplyGrabbedRowVisualAim(row);
        UpdateGrabbedPlayerPose(rb.position, rb.rotation);
    }

    private void ApplyGrabbedRowVisualAim(List<CartController> row)
    {
        if (row == null || row.Count <= 1 || nestedRowVisualAimMaxYaw <= 0f)
        {
            currentNestedRowVisualAimYaw = Mathf.MoveTowards(currentNestedRowVisualAimYaw, 0f, nestedRowVisualAimResponse * Time.fixedDeltaTime);
            return;
        }

        Vector3 steerDirection = new Vector3(grabbedInput.x, 0f, grabbedInput.y);
        float targetYaw = 0f;
        if (steerDirection.sqrMagnitude > 0.001f)
        {
            Vector3 grabberForward = Vector3.ProjectOnPlane(grabberFollowRotation * Vector3.forward, Vector3.up);
            if (grabberForward.sqrMagnitude < 0.001f)
            {
                grabberForward = GetCartForward();
            }

            // Visual lead should only appear when the steering direction differs from the
            // player's current body direction. Straight movement keeps the row visually neutral.
            targetYaw = Mathf.Clamp(Vector3.SignedAngle(grabberForward.normalized, steerDirection.normalized, Vector3.up), -nestedRowVisualAimMaxYaw, nestedRowVisualAimMaxYaw);
        }

        float visualAimResponse = IsReturningRowVisualAimToCenter(targetYaw) ? nestedRowVisualAimReturnResponse : nestedRowVisualAimResponse;
        currentNestedRowVisualAimYaw = Mathf.Lerp(currentNestedRowVisualAimYaw, targetYaw, 1f - Mathf.Exp(-visualAimResponse * Time.fixedDeltaTime));
        Vector3 grabPointPivot = GetCartGrabPointPosition();
        Quaternion visualOffset = Quaternion.AngleAxis(currentNestedRowVisualAimYaw, Vector3.up);
        if (!CanApplyRowVisualMotion(row, grabPointPivot, visualOffset))
        {
            currentNestedRowVisualAimYaw = Mathf.MoveTowards(currentNestedRowVisualAimYaw, 0f, nestedRowVisualAimResponse * Time.fixedDeltaTime);
            visualOffset = Quaternion.AngleAxis(currentNestedRowVisualAimYaw, Vector3.up);
            if (!CanApplyRowVisualMotion(row, grabPointPivot, visualOffset))
            {
                currentNestedRowVisualAimYaw = 0f;
                return;
            }
        }

        Vector3 visualDeltaPosition = Vector3.zero;

        for (int i = 0; i < row.Count; i++)
        {
            CartController cart = row[i];
            if (cart == null || cart.isTipped)
            {
                continue;
            }

            // Rotate the row as one stiff visual piece around the real cart grab point. This
            // presentation offset is reset before the next steering step, so it does not become
            // part of the row's actual movement.
            Vector3 visualPosition = grabPointPivot + visualOffset * (cart.rb.position - grabPointPivot) + visualDeltaPosition;
            visualPosition.y = cart.plantedY;
            Quaternion visualRotation = visualOffset * cart.rb.rotation;
            Vector3 euler = visualRotation.eulerAngles;
            Quaternion uprightRotation = Quaternion.Euler(0f, euler.y, 0f);

            if (cart.rb.isKinematic)
            {
                cart.rb.position = visualPosition;
                cart.rb.rotation = uprightRotation;
            }
            else
            {
                cart.rb.MovePosition(visualPosition);
                cart.rb.MoveRotation(uprightRotation);
            }

            cart.transform.position = visualPosition;
            cart.transform.rotation = uprightRotation;
        }
    }

    private bool IsReturningRowVisualAimToCenter(float targetYaw)
    {
        if (Mathf.Abs(targetYaw) < 0.01f)
        {
            return Mathf.Abs(currentNestedRowVisualAimYaw) > 0.01f;
        }

        return Mathf.Sign(targetYaw) == Mathf.Sign(currentNestedRowVisualAimYaw) && Mathf.Abs(targetYaw) < Mathf.Abs(currentNestedRowVisualAimYaw);
    }

    private Quaternion GetRowCenterHandleDeltaRotation(Vector3 rowCenter, int rowCount)
    {
        Vector3 currentHandleOffset = Vector3.ProjectOnPlane(GetCartGrabPointPosition() - rowCenter, Vector3.up);
        Vector3 targetHandleOffset = Vector3.ProjectOnPlane(grabberFollowPosition - rowCenter, Vector3.up);
        if (currentHandleOffset.sqrMagnitude < 0.001f || targetHandleOffset.sqrMagnitude < 0.001f)
        {
            return Quaternion.identity;
        }

        // A row turns when the player moves the rear handle around the row center. This preserves
        // the Starter player movement while making the row read like a longer, center-pivoting body.
        float targetTurn = Vector3.SignedAngle(currentHandleOffset, targetHandleOffset, Vector3.up);
        float rowTurnMultiplier = GetGrabbedRowTurnMultiplier(rowCount);
        float maxTurn = grabbedTurnSpeed * rowTurnMultiplier * Time.fixedDeltaTime;
        float turnStep = Mathf.Clamp(targetTurn, -maxTurn, maxTurn);
        return Quaternion.AngleAxis(turnStep, Vector3.up);
    }

    private float GetGrabbedRowTurnMultiplier(int rowCount)
    {
        int extraCartCount = Mathf.Max(0, rowCount - 1);
        float slowdown = 1f + extraCartCount * nestedRowGrabbedTurnSlowdownPerExtraCart;
        return slowdown > 0f ? nestedRowGrabbedTurnSpeedMultiplier / slowdown : nestedRowGrabbedTurnSpeedMultiplier;
    }

    public float GetGrabbedRowTurnResponseMultiplier()
    {
        List<CartController> row = GetExplicitRow();
        return row.Count > 1 ? GetGrabbedRowTurnMultiplier(row.Count) : 1f;
    }

    public Vector3 GetPlayerGrabPosition()
    {
        RefreshGrabbedPlayerPose();
        return currentGrabbedPlayerPosition;
    }

    public Quaternion GetPlayerGrabRotation()
    {
        RefreshGrabbedPlayerPose();
        return currentGrabbedPlayerRotation;
    }

    public void GetHandleGripTargets(out Vector3 leftHandPosition, out Vector3 rightHandPosition, out Quaternion handRotation)
    {
        Vector3 handlePosition = GetHandleCenterPosition();
        Vector3 right = transform.right;
        leftHandPosition = handlePosition - right * (handleGripWidth * 0.5f);
        rightHandPosition = handlePosition + right * (handleGripWidth * 0.5f);
        handRotation = Quaternion.LookRotation(transform.forward, Vector3.up);
    }

    private bool IsPlayerOnHandleSide(Vector3 playerPosition)
    {
        Vector3 grabPointToPlayer = Vector3.ProjectOnPlane(playerPosition - GetCartGrabPointPosition(), Vector3.up);
        return grabPointToPlayer.magnitude <= grabDistance;
    }

    private Vector3 GetCartGrabPointPosition()
    {
        return cartGrabPoint != null ? cartGrabPoint.position : GetHandleCenterPosition();
    }

    private Vector3 GetCartGrabPointOffset(Quaternion cartRotation)
    {
        if (cartGrabPoint == null)
        {
            return GetHandleCenterOffset(cartRotation);
        }

        Quaternion fromCurrentToTarget = cartRotation * Quaternion.Inverse(transform.rotation);
        Vector3 currentWorldOffset = GetCartGrabPointPosition() - rb.position;
        return fromCurrentToTarget * currentWorldOffset;
    }

    private Vector3 GetHandlePosition()
    {
        return handleAnchor != null ? handleAnchor.position : transform.TransformPoint(fallbackHandleLocalPoint);
    }

    private Vector3 GetHandleCenterPosition()
    {
        if (TryGetHandleRendererCenter(out Vector3 center))
        {
            return center;
        }

        return GetHandlePosition();
    }

    private Vector3 GetHandlePosition(Vector3 cartPosition, Quaternion cartRotation)
    {
        if (handleAnchor == null)
        {
            return cartPosition + cartRotation * ScaledLocalPoint(fallbackHandleLocalPoint);
        }

        return cartPosition + cartRotation * ScaledLocalPoint(handleAnchor.localPosition);
    }

    private Vector3 GetHandleCenterOffset(Quaternion cartRotation)
    {
        Vector3 localCenter = handleAnchor != null ? GetLocalHandleCenter() : fallbackHandleLocalPoint;
        return cartRotation * ScaledLocalPoint(localCenter);
    }

    private Vector3 GetLocalHandleCenter()
    {
        if (TryGetHandleRendererCenter(out Vector3 center))
        {
            return transform.InverseTransformPoint(center);
        }

        return handleAnchor.localPosition;
    }

    private bool TryGetHandleRendererCenter(out Vector3 center)
    {
        center = Vector3.zero;
        if (handleRenderers == null || handleRenderers.Length == 0)
        {
            return false;
        }

        Bounds bounds = new Bounds();
        bool hasBounds = false;
        foreach (Renderer handleRenderer in handleRenderers)
        {
            if (handleRenderer == null || !handleRenderer.enabled)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = handleRenderer.bounds;
                hasBounds = true;
                continue;
            }

            bounds.Encapsulate(handleRenderer.bounds);
        }

        if (!hasBounds)
        {
            return false;
        }

        center = bounds.center;
        return true;
    }

    private Vector3 GetHandleSideDirection()
    {
        Vector3 direction = GetRearWheelCenter() - GetFrontWheelCenter();
        direction.y = 0f;
        return direction.sqrMagnitude > 0.001f ? direction.normalized : -transform.forward;
    }

    private Vector3 GetHandleSideDirection(Vector3 cartPosition, Quaternion cartRotation)
    {
        return -(cartRotation * Vector3.forward);
    }

    private Vector3 ScaledLocalPoint(Vector3 localPoint)
    {
        return Vector3.Scale(localPoint, transform.localScale);
    }

    private void RefreshGrabbedPlayerPose()
    {
        if (grabbedPlayer == null)
        {
            return;
        }

        UpdateGrabbedPlayerPose(GetHandleCenterPosition(), GetHandleSideDirection(), transform.rotation);
    }

    private void ApplyGrabbedControl()
    {
        Vector3 pivot = GetHandlePosition();
        Vector3 desiredMove = new Vector3(grabbedInput.x, 0f, grabbedInput.y);
        float moveAmount = Mathf.Clamp01(desiredMove.magnitude) * grabbedDriveSpeed * Time.fixedDeltaTime;
        Quaternion nextRotation = rb.rotation;
        Vector3 nextPosition = rb.position;

        if (moveAmount > 0.001f)
        {
            desiredMove.Normalize();
            float targetYaw = Quaternion.LookRotation(desiredMove, Vector3.up).eulerAngles.y;
            float currentYaw = nextRotation.eulerAngles.y;
            float turnStep = Mathf.Clamp(Mathf.DeltaAngle(currentYaw, targetYaw), -grabbedTurnSpeed * Time.fixedDeltaTime, grabbedTurnSpeed * Time.fixedDeltaTime);
            Quaternion deltaRotation = Quaternion.AngleAxis(turnStep, Vector3.up);
            nextRotation = deltaRotation * nextRotation;
            nextPosition = pivot + deltaRotation * (nextPosition - pivot);
            nextPosition += desiredMove * moveAmount;
        }

        rb.MoveRotation(nextRotation);
        rb.MovePosition(nextPosition);
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
    }

    private void UpdateGrabbedPlayerPose(Vector3 cartPosition, Quaternion cartRotation)
    {
        UpdateGrabbedPlayerPose(GetHandlePosition(cartPosition, cartRotation), GetHandleSideDirection(cartPosition, cartRotation), cartRotation);
    }

    private void UpdateGrabbedPlayerPose(Vector3 handlePosition, Vector3 playerSide, Quaternion cartRotation)
    {
        currentGrabbedPlayerPosition = handlePosition + playerSide * playerHandleSpacing;
        currentGrabbedPlayerPosition.y = plantedY;

        Vector3 cartDirection = handlePosition - currentGrabbedPlayerPosition;
        cartDirection.y = 0f;
        if (cartDirection.sqrMagnitude < 0.001f)
        {
            cartDirection = cartRotation * Vector3.forward;
        }

        currentGrabbedPlayerRotation = Quaternion.LookRotation(cartDirection.normalized, Vector3.up);
    }
}
