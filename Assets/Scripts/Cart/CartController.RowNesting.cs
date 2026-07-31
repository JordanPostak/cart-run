using System.Collections.Generic;
using UnityEngine;

public enum CartRowEnd
{
    Back,
    Front
}

public partial class CartController
{
    public bool TryAttachCartAheadToRow()
    {
        if (!enableNestedCartRows || isTipped)
        {
            return false;
        }

        CartController leader = rowLeader != null ? rowLeader : this;
        leader.NormalizeRowState();
        CartController candidate = leader.FindAttachableCartAhead();
        if (candidate == null)
        {
            Debug.Log($"{nameof(CartController)} on {leader.name} did not find an attachable cart near the row front.", leader);
            return false;
        }

        if (leader.AttachCartToRow(candidate))
        {
            Debug.Log($"{nameof(CartController)} attached {candidate.name} to row led by {leader.name}.", leader);
            return true;
        }

        return false;
    }

    public bool TryStealBackCartFromNearbyRow()
    {
        if (!enableNestedCartRows || isTipped)
        {
            return false;
        }

        CartController leader = rowLeader != null ? rowLeader : this;
        leader.NormalizeRowState();
        CartController sourceRow = leader.FindStealableRowByRearDockingZone();
        if (sourceRow == null)
        {
            return false;
        }

        if (!sourceRow.TryDetachBackCartFromRow(out CartController stolenCart) || stolenCart == null)
        {
            return false;
        }

        if (leader.AttachCartToRow(stolenCart))
        {
            Debug.Log($"{nameof(CartController)} stole {stolenCart.name} from row led by {sourceRow.name} into row led by {leader.name}.", leader);
            return true;
        }

        return false;
    }

    public bool TryDetachCartFromRowEnd(CartRowEnd rowEnd, out CartController detachedCart)
    {
        CartController leader = rowLeader != null ? rowLeader : this;
        return leader.TryDetachCartFromRowEndAsLeader(rowEnd, out detachedCart);
    }

    public bool TryDetachFrontCartFromRow(out CartController detachedCart)
    {
        return TryDetachCartFromRowEnd(CartRowEnd.Front, out detachedCart);
    }

    public bool TryDetachBackCartFromRow(out CartController detachedCart)
    {
        return TryDetachCartFromRowEnd(CartRowEnd.Back, out detachedCart);
    }

    public bool TryAppendCartToRow(CartController cart)
    {
        CartController leader = rowLeader != null ? rowLeader : this;
        leader.NormalizeRowState();
        return leader.AttachCartToRow(cart);
    }

    public bool HasRowMembers()
    {
        CartController leader = rowLeader != null ? rowLeader : this;
        leader.NormalizeRowState();
        return leader.explicitRowCarts.Count > 0;
    }

    public CartController GetRowGrabLeader()
    {
        return rowLeader != null ? rowLeader : this;
    }

    private bool TryDetachCartFromRowEndAsLeader(CartRowEnd rowEnd, out CartController detachedCart)
    {
        detachedCart = null;
        if (rowLeader != null && rowLeader != this)
        {
            return rowLeader.TryDetachCartFromRowEndAsLeader(rowEnd, out detachedCart);
        }

        NormalizeRowState();
        if (explicitRowCarts.Count == 0)
        {
            return false;
        }

        if (rowEnd == CartRowEnd.Front)
        {
            return TryDetachFrontCartFromRowAsLeader(out detachedCart);
        }

        return TryDetachBackCartFromRowAsLeader(out detachedCart);
    }

    private CartController FindAttachableCartAhead()
    {
        List<CartController> row = GetExplicitRow();
        Vector3 rowForward = GetCartForward();
        Vector3 rowFrontPoint = GetFrontRowAttachPoint(row, rowForward);
        CartController closestCart = null;
        float closestScore = float.PositiveInfinity;
        int availableCartCount = 0;

        foreach (CartController candidate in ActiveCarts)
        {
            if (candidate == null || candidate == this || candidate.isTipped || candidate.rowLeader != null || row.Contains(candidate))
            {
                continue;
            }

            availableCartCount++;

            if (!candidate.TryGetAttachZoneScoreForIncomingRow(rowFrontPoint, rowForward, out float score))
            {
                continue;
            }

            if (score >= closestScore)
            {
                continue;
            }

            closestScore = score;
            closestCart = candidate;
        }

        if (closestCart == null)
        {
            Debug.Log($"{nameof(CartController)} on {name} saw {availableCartCount} available carts, but none were inside the row attach zone behind the cart.", this);
        }

        return closestCart;
    }

    private CartController FindStealableRowByRearDockingZone()
    {
        List<CartController> row = GetExplicitRow();
        Vector3 rowForward = GetCartForward();
        Vector3 rowFrontPoint = GetFrontRowAttachPoint(row, rowForward);
        CartController closestRow = null;
        float closestScore = float.PositiveInfinity;
        HashSet<CartController> checkedLeaders = new HashSet<CartController>();

        foreach (CartController candidate in ActiveCarts)
        {
            if (candidate == null || candidate.isTipped || row.Contains(candidate))
            {
                continue;
            }

            CartController candidateLeader = candidate.GetRowGrabLeader();
            if (candidateLeader == null || candidateLeader == this || candidateLeader == GetRowGrabLeader() || !checkedLeaders.Add(candidateLeader))
            {
                continue;
            }

            if (candidateLeader.IsGrabbed || !candidateLeader.HasRowMembers())
            {
                continue;
            }

            // Stealing uses the same docking pocket as normal row adding, but aimed at the
            // source row's rear cart. That makes the player line up with the row before pulling.
            if (!candidateLeader.TryGetAttachZoneScoreForIncomingRow(rowFrontPoint, rowForward, out float score))
            {
                continue;
            }

            if (score >= closestScore)
            {
                continue;
            }

            closestScore = score;
            closestRow = candidateLeader;
        }

        return closestRow;
    }

    private bool TryGetAttachZoneScoreForIncomingRow(Vector3 incomingRowFrontPoint, Vector3 incomingRowForward, out float score)
    {
        score = float.PositiveInfinity;
        Vector3 targetForward = GetCartForward();
        // Docking should feel generous: rows can be a little crooked while the snap-to-row
        // placement cleans up the final alignment.
        float effectiveAlignmentDot = Mathf.Clamp01(nestedRowAlignmentDot - 0.18f);
        if (Mathf.Abs(Vector3.Dot(targetForward, incomingRowForward)) < effectiveAlignmentDot)
        {
            return false;
        }

        Vector3 zoneOrigin = GetRearWheelCenter();
        Vector3 zoneDirection = -targetForward;
        Vector3 zoneRight = Vector3.Cross(Vector3.up, targetForward).normalized;
        float halfWidth = Mathf.Max(nestedRowAttachZoneWidth * 0.5f, nestedRowLateralTolerance);
        float attachReadyDepth = Mathf.Min(nestedRowAttachZoneDepth, Mathf.Max(nestedRowAttachDistance, nestedRowStepDistance));
        Vector3 offset = Vector3.ProjectOnPlane(incomingRowFrontPoint - zoneOrigin, Vector3.up);
        float forwardDistance = Vector3.Dot(offset, zoneDirection);

        // Negative forward distance means the incoming cart/row has already overlapped the
        // target's rear. Allow that, because the actual row layout intentionally nests carts.
        if (forwardDistance < -nestedRowAttachOverlapAllowance || forwardDistance > attachReadyDepth)
        {
            return false;
        }

        float lateralDistance = Mathf.Abs(Vector3.Dot(offset, zoneRight));
        if (lateralDistance > halfWidth)
        {
            return false;
        }

        score = Mathf.Abs(forwardDistance) + lateralDistance;
        return true;
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

    private bool AttachCartToRow(CartController cart)
    {
        if (cart == null || cart == this || explicitRowCarts.Contains(cart))
        {
            return false;
        }

        cart.NormalizeRowState();
        if (cart.rowLeader != null || cart.explicitRowCarts.Count > 0 || cart.rowObject != null)
        {
            return false;
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

        NormalizeRowState();
        return true;
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

        NormalizeRowState();
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

        CleanupEmptyRowObject();
    }

    private bool TryDetachFrontCartFromRowAsLeader(out CartController detachedCart)
    {
        detachedCart = explicitRowCarts[explicitRowCarts.Count - 1];
        DetachCartFromRow(detachedCart);
        explicitRowCarts.RemoveAt(explicitRowCarts.Count - 1);
        RefreshRowAfterEndDetach();
        return detachedCart != null;
    }

    private bool TryDetachBackCartFromRowAsLeader(out CartController detachedCart)
    {
        detachedCart = this;
        GameObject previousRowObject = rowObject;
        List<CartController> remainingRow = new List<CartController>(explicitRowCarts);

        foreach (CartController rowCart in remainingRow)
        {
            DetachCartFromRow(rowCart);
        }

        explicitRowCarts.Clear();
        RestoreNestedCartCollisions();
        transform.SetParent(originalParent, true);
        rowObject = null;

        DestroyRowObject(previousRowObject);
        RebuildRowFromDetachedMembers(remainingRow);
        IgnoreFormerRowCollisionsUntilSeparated(remainingRow);
        return true;
    }

    private void RebuildRowFromDetachedMembers(List<CartController> rowMembers)
    {
        if (rowMembers == null || rowMembers.Count == 0)
        {
            return;
        }

        CartController newLeader = rowMembers[0];
        if (newLeader == null)
        {
            return;
        }

        if (rowMembers.Count == 1)
        {
            newLeader.RestoreStandaloneRowState();
            return;
        }

        newLeader.EnsureRowObject();
        for (int i = 1; i < rowMembers.Count; i++)
        {
            CartController rowMember = rowMembers[i];
            if (rowMember != null)
            {
                newLeader.AttachCartToRow(rowMember);
            }
        }

        newLeader.NormalizeRowState();
    }

    private void RefreshRowAfterEndDetach()
    {
        RestoreNestedCartCollisions();
        if (explicitRowCarts.Count > 0)
        {
            RebuildRowLayout(GetExplicitRow());
            return;
        }

        CleanupEmptyRowObject();
    }

    private void CleanupEmptyRowObject()
    {
        if (explicitRowCarts.Count > 0 || rowObject == null)
        {
            return;
        }

        GameObject emptyRowObject = rowObject;
        transform.SetParent(originalParent, true);
        rowObject = null;
        DestroyRowObject(emptyRowObject);
    }

    private void RestoreStandaloneRowState()
    {
        RestoreNestedCartCollisions();
        rowLeader = null;
        explicitRowCarts.Clear();

        if (rowObject != null)
        {
            GameObject staleRowObject = rowObject;
            transform.SetParent(originalParent, true);
            rowObject = null;
            DestroyRowObject(staleRowObject);
        }
        else
        {
            transform.SetParent(originalParent, true);
        }
    }

    private void NormalizeRowState()
    {
        if (rowLeader != null && rowLeader != this)
        {
            return;
        }

        for (int i = explicitRowCarts.Count - 1; i >= 0; i--)
        {
            CartController rowCart = explicitRowCarts[i];
            if (rowCart == null || rowCart == this || explicitRowCarts.IndexOf(rowCart) != i)
            {
                explicitRowCarts.RemoveAt(i);
                continue;
            }

            if (rowCart.rowLeader != null && rowCart.rowLeader != this)
            {
                explicitRowCarts.RemoveAt(i);
                continue;
            }

            if (rowCart.explicitRowCarts.Count > 0)
            {
                explicitRowCarts.RemoveAt(i);
                continue;
            }

            rowCart.rowLeader = this;
            rowCart.rowObject = rowObject;
        }

        if (explicitRowCarts.Count == 0)
        {
            RestoreStandaloneRowState();
            return;
        }

        EnsureRowObject();
        foreach (CartController rowCart in explicitRowCarts)
        {
            if (rowCart == null)
            {
                continue;
            }

            rowCart.rowLeader = this;
            rowCart.rowObject = rowObject;
        }
    }

    private void DestroyRowObject(GameObject rowObjectToDestroy)
    {
        if (rowObjectToDestroy == null)
        {
            return;
        }

        // Row objects are temporary runtime grouping transforms. Once a row has fewer than two
        // carts, remove the grouping so the remaining cart behaves like a normal standalone cart.
        if (Application.isPlaying)
        {
            Destroy(rowObjectToDestroy);
        }
        else
        {
            DestroyImmediate(rowObjectToDestroy);
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
