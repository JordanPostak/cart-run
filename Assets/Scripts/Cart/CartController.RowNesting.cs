using System.Collections.Generic;
using UnityEngine;

public partial class CartController
{
    public bool TryAttachCartAheadToRow()
    {
        if (!enableNestedCartRows || isTipped)
        {
            return false;
        }

        CartController leader = rowLeader != null ? rowLeader : this;
        CartController candidate = leader.FindAttachableCartAhead();
        if (candidate == null)
        {
            Debug.Log($"{nameof(CartController)} on {leader.name} did not find an attachable cart near the row front.", leader);
            return false;
        }

        leader.AttachCartToRow(candidate);
        Debug.Log($"{nameof(CartController)} attached {candidate.name} to row led by {leader.name}.", leader);
        return true;
    }

    private CartController FindAttachableCartAhead()
    {
        List<CartController> row = GetExplicitRow();
        Vector3 rowForward = GetCartForward();
        Vector3 rowFrontPoint = GetFrontRowAttachPoint(row, rowForward);
        CartController closestCart = null;
        float closestDistance = nestedRowScanDistance;
        int availableCartCount = 0;

        CartController[] carts = FindObjectsByType<CartController>(FindObjectsInactive.Exclude);
        foreach (CartController candidate in carts)
        {
            if (candidate == null || candidate == this || candidate.isTipped || candidate.rowLeader != null || row.Contains(candidate))
            {
                continue;
            }

            availableCartCount++;

            float distance = candidate.GetClosestPlanarDistanceTo(rowFrontPoint);
            if (distance > closestDistance)
            {
                continue;
            }

            closestDistance = distance;
            closestCart = candidate;
        }

        if (closestCart == null)
        {
            Debug.Log($"{nameof(CartController)} on {name} saw {availableCartCount} available carts, but none were within {nestedRowScanDistance:0.00} of the row front.", this);
        }

        return closestCart;
    }

    private float GetClosestPlanarDistanceTo(Vector3 targetPoint)
    {
        float frontDistance = Vector3.ProjectOnPlane(GetFrontWheelCenter() - targetPoint, Vector3.up).magnitude;
        float rearDistance = Vector3.ProjectOnPlane(GetRearWheelCenter() - targetPoint, Vector3.up).magnitude;
        float centerDistance = Vector3.ProjectOnPlane(rb.position - targetPoint, Vector3.up).magnitude;
        return Mathf.Min(frontDistance, rearDistance, centerDistance);
    }

    private float GetAttachForwardGap(CartController candidate, float frontEdge, Vector3 rowForward, float allowedDistance)
    {
        float frontGap = GetForwardGapToPoint(candidate.GetFrontWheelCenter(), frontEdge, rowForward, allowedDistance);
        float rearGap = GetForwardGapToPoint(candidate.GetRearWheelCenter(), frontEdge, rowForward, allowedDistance);
        float centerGap = GetForwardGapToPoint(candidate.rb.position, frontEdge, rowForward, allowedDistance);
        return Mathf.Min(frontGap, rearGap, centerGap);
    }

    private float GetForwardGapToPoint(Vector3 point, float frontEdge, Vector3 rowForward, float allowedDistance)
    {
        Vector3 offset = Vector3.ProjectOnPlane(point - rb.position, Vector3.up);
        float forwardGap = Vector3.Dot(offset, rowForward) - frontEdge;
        return forwardGap < -(allowedDistance * 0.5f) ? float.PositiveInfinity : Mathf.Abs(forwardGap);
    }

    private float GetAttachLateralDistance(CartController candidate, Vector3 rowRight)
    {
        float frontLateral = GetLateralDistanceToPoint(candidate.GetFrontWheelCenter(), rowRight);
        float rearLateral = GetLateralDistanceToPoint(candidate.GetRearWheelCenter(), rowRight);
        float centerLateral = GetLateralDistanceToPoint(candidate.rb.position, rowRight);
        return Mathf.Min(frontLateral, rearLateral, centerLateral);
    }

    private float GetLateralDistanceToPoint(Vector3 point, Vector3 rowRight)
    {
        Vector3 offset = Vector3.ProjectOnPlane(point - rb.position, Vector3.up);
        return Mathf.Abs(Vector3.Dot(offset, rowRight));
    }

    private void AttachCartToRow(CartController cart)
    {
        if (cart == null || cart == this || explicitRowCarts.Contains(cart))
        {
            return;
        }

        EnsureRowObject();
        cart.rowLeader = this;
        cart.rowObject = rowObject;
        cart.transform.SetParent(rowObject.transform, true);
        cart.EnterRowMemberState();
        explicitRowCarts.Add(cart);
        if (pullNestedCartIntoPlace)
        {
            RebuildRowLayout(GetExplicitRow());
        }

        SetCartCollisionIgnored(cart, true);
        if (!ignoredNestedCollisionCarts.Contains(cart))
        {
            ignoredNestedCollisionCarts.Add(cart);
        }

        cart.SetCartCollisionIgnored(this, true);
        if (!cart.ignoredNestedCollisionCarts.Contains(this))
        {
            cart.ignoredNestedCollisionCarts.Add(this);
        }

        foreach (CartController rowCart in explicitRowCarts)
        {
            if (rowCart != null && rowCart != cart)
            {
                cart.SetCartCollisionIgnored(rowCart, true);
                if (!cart.ignoredNestedCollisionCarts.Contains(rowCart))
                {
                    cart.ignoredNestedCollisionCarts.Add(rowCart);
                }

                rowCart.SetCartCollisionIgnored(cart, true);
                if (!rowCart.ignoredNestedCollisionCarts.Contains(cart))
                {
                    rowCart.ignoredNestedCollisionCarts.Add(cart);
                }
            }
        }
    }

    private void EnsureRowObject()
    {
        if (rowLeader != null && rowLeader != this)
        {
            rowLeader.EnsureRowObject();
            rowObject = rowLeader.rowObject;
            return;
        }

        if (rowObject != null)
        {
            return;
        }

        Vector3 rowForward = GetRowForward();
        rowStartPosition = rb.position;
        rowObject = new GameObject($"Cart Row - {name}");
        rowObject.transform.SetPositionAndRotation(rowStartPosition, Quaternion.LookRotation(rowForward, Vector3.up));
        rowObject.transform.SetParent(originalParent, true);
        transform.SetParent(rowObject.transform, true);
        rowCartLocalRotation = transform.localRotation;
    }

    private void EnterRowMemberState()
    {
        wasKinematicBeforeRow = rb.isKinematic;
        rb.isKinematic = true;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        forwardRollMomentum = 0f;
        sidePivotMomentum = 0f;
        hasSidePivotWorldPoint = false;
        EnforcePlantedHeight();
        EnforceUprightPose();
    }

    private void ExitRowMemberState()
    {
        rb.isKinematic = wasKinematicBeforeRow;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rowObject = null;
    }

    private void PullCartIntoRowPlace(CartController previousCart, CartController cart)
    {
        if (previousCart == null || cart == null)
        {
            return;
        }

        List<CartController> row = GetExplicitRow();
        UpdateRowTransform(row);

        Vector3 localSlot = GetNextRowEndSlot(row, cart);
        Quaternion targetRotation = rowObject != null ? rowObject.transform.rotation * rowCartLocalRotation : previousCart.rb.rotation;
        Vector3 targetPosition = rowObject != null ? rowObject.transform.TransformPoint(localSlot) : previousCart.rb.position + previousCart.GetCartForward() * (nestedRowSlotSpacing + nestedRowPullForwardOffset);

        cart.rb.position = targetPosition;
        cart.rb.rotation = targetRotation;
        cart.transform.SetPositionAndRotation(targetPosition, targetRotation);
        if (rowObject != null)
        {
            cart.transform.localPosition = localSlot;
            cart.transform.localRotation = rowCartLocalRotation;
            targetPosition = cart.transform.position;
            targetRotation = cart.transform.rotation;
            cart.rb.position = targetPosition;
            cart.rb.rotation = targetRotation;
        }

        cart.rb.linearVelocity = Vector3.zero;
        cart.rb.angularVelocity = Vector3.zero;
        cart.forwardRollMomentum = 0f;
        cart.sidePivotMomentum = 0f;
        cart.hasSidePivotWorldPoint = false;
        Physics.SyncTransforms();
    }

    private void RebuildRowLayout(List<CartController> row)
    {
        if (rowObject == null || row == null)
        {
            return;
        }

        for (int i = 0; i < row.Count; i++)
        {
            CartController cart = row[i];
            if (cart == null || cart.isTipped)
            {
                continue;
            }

            Vector3 localSlot = new Vector3(0f, cart.plantedY - rowObject.transform.position.y, GetRowSlotForwardOffset(i));
            Quaternion targetRotation = rowObject.transform.rotation * rowCartLocalRotation;
            Vector3 targetPosition = rowObject.transform.TransformPoint(localSlot);

            cart.transform.localPosition = localSlot;
            cart.transform.localRotation = rowCartLocalRotation;
            if (cart.rb.isKinematic)
            {
                cart.rb.position = targetPosition;
                cart.rb.rotation = targetRotation;
            }
            else
            {
                cart.rb.MovePosition(targetPosition);
                cart.rb.MoveRotation(targetRotation);
            }

            cart.rb.linearVelocity = Vector3.zero;
            cart.rb.angularVelocity = Vector3.zero;
            if (cart != this)
            {
                cart.forwardRollMomentum = 0f;
                cart.sidePivotMomentum = 0f;
                cart.hasSidePivotWorldPoint = false;
            }
        }

        Physics.SyncTransforms();
    }

    private float GetRowSlotForwardOffset(int slotIndex)
    {
        return slotIndex <= 0 ? 0f : slotIndex * GetNestedRowSlotStep();
    }

    private float GetNestedRowSlotStep()
    {
        return Mathf.Max(0.01f, nestedRowStepDistance);
    }

    private Vector3 GetNextRowEndSlot(List<CartController> row, CartController cart)
    {
        if (rowObject == null)
        {
            return Vector3.zero;
        }

        float furthestForwardSlot = 0f;
        foreach (CartController rowCart in row)
        {
            if (rowCart == null || rowCart == cart)
            {
                continue;
            }

            Vector3 localPosition = rowObject.transform.InverseTransformPoint(rowCart.rb.position);
            furthestForwardSlot = Mathf.Max(furthestForwardSlot, localPosition.z);
        }

        return new Vector3(
            0f,
            cart.plantedY - rowObject.transform.position.y,
            furthestForwardSlot + GetNestedRowSlotStep());
    }

    private Vector3 GetClosestAttachPointTo(Vector3 targetPoint, Vector3 rowForward, Vector3 rowRight)
    {
        Vector3 frontPoint = GetFrontWheelCenter();
        Vector3 rearPoint = GetRearWheelCenter();
        Vector3 centerPoint = rb.position;

        Vector3 closestPoint = frontPoint;
        float closestScore = GetAttachPointScore(frontPoint, targetPoint, rowForward, rowRight);

        float rearScore = GetAttachPointScore(rearPoint, targetPoint, rowForward, rowRight);
        if (rearScore < closestScore)
        {
            closestScore = rearScore;
            closestPoint = rearPoint;
        }

        float centerScore = GetAttachPointScore(centerPoint, targetPoint, rowForward, rowRight);
        if (centerScore < closestScore)
        {
            closestPoint = centerPoint;
        }

        return closestPoint;
    }

    private float GetAttachPointScore(Vector3 point, Vector3 targetPoint, Vector3 rowForward, Vector3 rowRight)
    {
        Vector3 offset = Vector3.ProjectOnPlane(point - targetPoint, Vector3.up);
        float forwardError = Mathf.Abs(Vector3.Dot(offset, rowForward));
        float lateralError = Mathf.Abs(Vector3.Dot(offset, rowRight));
        return forwardError + lateralError;
    }

    private List<CartController> GetExplicitRow()
    {
        if (rowLeader != null && rowLeader != this)
        {
            return rowLeader.GetExplicitRow();
        }

        List<CartController> row = new List<CartController> { this };
        for (int i = 0; i < explicitRowCarts.Count; i++)
        {
            CartController cart = explicitRowCarts[i];
            if (cart == null || cart.isTipped)
            {
                DetachRowFromIndex(i);
                continue;
            }

            row.Add(cart);
        }

        return row;
    }

    private void DetachRowFromIndex(int startIndex)
    {
        for (int i = explicitRowCarts.Count - 1; i >= startIndex; i--)
        {
            DetachCartFromRow(explicitRowCarts[i]);
            explicitRowCarts.RemoveAt(i);
        }
    }

    private bool IsPhysicallyStackedWith(CartController previousCart, CartController nextCart, float allowedDistance)
    {
        if (previousCart == null || nextCart == null)
        {
            return false;
        }

        Vector3 rowForward = previousCart.GetCartForward();
        Vector3 rowRight = Vector3.Cross(Vector3.up, rowForward).normalized;
        if (Mathf.Abs(Vector3.Dot(rowForward, nextCart.GetCartForward())) < nestedRowAlignmentDot)
        {
            return false;
        }

        Vector3 targetPoint = previousCart.GetFrontWheelCenter() - rowForward * nestedRowOverlapDistance + rowForward * nestedRowPullForwardOffset;
        Vector3 rearOffset = Vector3.ProjectOnPlane(nextCart.GetRearWheelCenter() - targetPoint, Vector3.up);
        float forwardGap = Mathf.Abs(Vector3.Dot(rearOffset, rowForward));
        float lateralDistance = previousCart.GetAttachLateralDistance(nextCart, rowRight);
        return forwardGap <= allowedDistance && lateralDistance <= nestedRowLateralTolerance;
    }

    private void DetachCartFromRow(CartController cart)
    {
        if (cart == null)
        {
            return;
        }

        SetCartCollisionIgnored(cart, false);
        ignoredNestedCollisionCarts.Remove(cart);
        cart.SetCartCollisionIgnored(this, false);
        cart.ignoredNestedCollisionCarts.Remove(this);
        foreach (CartController rowCart in explicitRowCarts)
        {
            if (rowCart == null || rowCart == cart)
            {
                continue;
            }

            cart.SetCartCollisionIgnored(rowCart, false);
            cart.ignoredNestedCollisionCarts.Remove(rowCart);
            rowCart.SetCartCollisionIgnored(cart, false);
            rowCart.ignoredNestedCollisionCarts.Remove(cart);
        }

        cart.rowLeader = null;
        cart.transform.SetParent(cart.originalParent, true);
        cart.ExitRowMemberState();
    }

    private Vector3 GetExplicitRowCenter(List<CartController> row)
    {
        Vector3 center = Vector3.zero;
        int count = 0;
        foreach (CartController cart in row)
        {
            if (cart == null)
            {
                continue;
            }

            center += cart.rb.position;
            count++;
        }

        center = count > 0 ? center / count : rb.position;
        center.y = plantedY;
        return center;
    }

    private float GetFrontRowEdge(List<CartController> row, Vector3 rowForward)
    {
        float frontEdge = float.MinValue;
        foreach (CartController cart in row)
        {
            if (cart == null)
            {
                continue;
            }

            Vector3 offset = Vector3.ProjectOnPlane(cart.GetFrontWheelCenter() - rb.position, Vector3.up);
            frontEdge = Mathf.Max(frontEdge, Vector3.Dot(offset, rowForward));
        }

        return frontEdge == float.MinValue ? 0f : frontEdge;
    }

    private Vector3 GetFrontRowAttachPoint(List<CartController> row, Vector3 rowForward)
    {
        CartController frontCart = null;
        float frontEdge = float.MinValue;
        foreach (CartController cart in row)
        {
            if (cart == null)
            {
                continue;
            }

            Vector3 frontPoint = cart.GetFrontWheelCenter();
            Vector3 offset = Vector3.ProjectOnPlane(frontPoint - rb.position, Vector3.up);
            float edge = Vector3.Dot(offset, rowForward);
            if (edge > frontEdge)
            {
                frontEdge = edge;
                frontCart = cart;
            }
        }

        return frontCart != null ? frontCart.GetFrontWheelCenter() : GetFrontWheelCenter();
    }

    private float GetNestedRowWeight(int rowCount)
    {
        return 1f + Mathf.Max(0, rowCount - 1) * nestedRowExtraCartWeight;
    }

    private Vector3 GetCartForward()
    {
        Vector3 cartForward = Vector3.ProjectOnPlane(GetFrontWheelCenter() - GetRearWheelCenter(), Vector3.up);
        return cartForward.sqrMagnitude > 0.001f ? cartForward.normalized : transform.forward;
    }

    private Vector3 GetRowForward()
    {
        Vector3 rowForward = Vector3.zero;
        if (grabbedPlayer != null)
        {
            rowForward = Vector3.ProjectOnPlane(grabbedPlayer.GetGrabberRotation() * Vector3.forward, Vector3.up);
        }

        if (rowForward.sqrMagnitude < 0.001f && hasGrabberFollowTarget)
        {
            rowForward = Vector3.ProjectOnPlane(grabberFollowRotation * Vector3.forward, Vector3.up);
        }

        if (rowForward.sqrMagnitude < 0.001f)
        {
            rowForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        }

        return rowForward.sqrMagnitude > 0.001f ? rowForward.normalized : Vector3.forward;
    }

    private float GetCartLength()
    {
        return Mathf.Max(0.1f, Vector3.ProjectOnPlane(GetFrontWheelCenter() - GetRearWheelCenter(), Vector3.up).magnitude);
    }
}
