using System.Collections.Generic;
using UnityEngine;

public partial class CartController
{
    private void LimitRowMotionByCollision(List<CartController> row, Vector3 pivot, ref Quaternion deltaRotation, ref Vector3 deltaPosition)
    {
        if (!blockRowMovementOnCollision || row == null || row.Count == 0)
        {
            return;
        }

        float allowedFraction = 1f;
        for (int i = 0; i < row.Count; i++)
        {
            CartController cart = row[i];
            if (cart == null || cart.isTipped || cart.rb == null)
            {
                continue;
            }

            Vector3 targetPosition = pivot + deltaRotation * (cart.rb.position - pivot) + deltaPosition;
            targetPosition.y = cart.plantedY;
            Vector3 displacement = targetPosition - cart.rb.position;
            float distance = displacement.magnitude;
            if (distance < 0.001f)
            {
                continue;
            }

            float blockerDistance = GetNearestRowMotionBlockerDistance(cart, row, displacement / distance, distance + rowCollisionSkin);
            if (blockerDistance < 0f)
            {
                continue;
            }

            allowedFraction = Mathf.Min(allowedFraction, Mathf.Clamp01((blockerDistance - rowCollisionSkin) / distance));
            if (allowedFraction <= 0f)
            {
                break;
            }
        }

        if (allowedFraction >= 0.999f)
        {
            return;
        }

        deltaPosition *= allowedFraction;
        deltaRotation = Quaternion.Slerp(Quaternion.identity, deltaRotation, allowedFraction);
    }

    private float GetNearestRowMotionBlockerDistance(CartController cart, List<CartController> row, Vector3 direction, float distance)
    {
        RaycastHit[] hits = cart.rb.SweepTestAll(direction, distance, QueryTriggerInteraction.Ignore);
        float nearestDistance = -1f;
        for (int i = 0; i < hits.Length; i++)
        {
            Collider hitCollider = hits[i].collider;
            if (!IsRowMotionBlocker(hitCollider, row))
            {
                continue;
            }

            if (nearestDistance < 0f || hits[i].distance < nearestDistance)
            {
                nearestDistance = hits[i].distance;
            }
        }

        return nearestDistance;
    }

    private bool IsRowMotionBlocker(Collider hitCollider, List<CartController> row)
    {
        if (hitCollider == null || hitCollider.isTrigger)
        {
            return false;
        }

        CartController hitCart = hitCollider.attachedRigidbody != null
            ? hitCollider.attachedRigidbody.GetComponent<CartController>()
            : hitCollider.GetComponentInParent<CartController>();
        if (hitCart != null && row.Contains(hitCart))
        {
            return false;
        }

        // The player is intentionally attached to the rear handle while steering a row, so
        // player colliders should not be treated as world obstacles for the row sweep.
        if (hitCollider.GetComponentInParent<PlayerController>() != null)
        {
            return false;
        }

        return true;
    }

    private void UpdateNestedCartCollisionIgnores()
    {
        if (!ignoreNestedCartCollisions)
        {
            RestoreNestedCartCollisions();
            return;
        }

        if (Time.time < nextNestedCollisionRefreshTime)
        {
            return;
        }

        nextNestedCollisionRefreshTime = Time.time + nestedCollisionRefreshInterval;
        if (rowLeader != null && rowLeader != this)
        {
            return;
        }

        List<CartController> row = GetExplicitRow();
        foreach (CartController rowCart in row)
        {
            if (rowCart == null || rowCart == this || ignoredNestedCollisionCarts.Contains(rowCart))
            {
                continue;
            }

            SetCartCollisionIgnored(rowCart, true);
            ignoredNestedCollisionCarts.Add(rowCart);
        }
    }

    private void SetCartCollisionIgnored(CartController otherCart, bool ignore)
    {
        if (otherCart == null || cartColliders == null)
        {
            return;
        }

        Collider[] otherColliders = otherCart.cartColliders != null && otherCart.cartColliders.Length > 0
            ? otherCart.cartColliders
            : otherCart.GetComponentsInChildren<Collider>();

        foreach (Collider cartCollider in cartColliders)
        {
            if (cartCollider == null)
            {
                continue;
            }

            foreach (Collider otherCollider in otherColliders)
            {
                if (otherCollider != null && otherCollider != cartCollider)
                {
                    Physics.IgnoreCollision(cartCollider, otherCollider, ignore);
                }
            }
        }
    }

    private void RestoreNestedCartCollisions()
    {
        for (int i = ignoredNestedCollisionCarts.Count - 1; i >= 0; i--)
        {
            SetCartCollisionIgnored(ignoredNestedCollisionCarts[i], false);
        }

        ignoredNestedCollisionCarts.Clear();
    }

    private void IgnoreFormerRowCollisionsUntilSeparated(List<CartController> formerRow)
    {
        if (formerRow == null)
        {
            return;
        }

        foreach (CartController rowCart in formerRow)
        {
            if (rowCart == null || rowCart == this)
            {
                continue;
            }

            SetCartCollisionIgnored(rowCart, true);
            rowCart.SetCartCollisionIgnored(this, true);
            AddDetachedRowCollisionCart(rowCart);
            rowCart.AddDetachedRowCollisionCart(this);
        }
    }

    private void AddDetachedRowCollisionCart(CartController cart)
    {
        if (cart != null && !detachedRowCollisionCarts.Contains(cart))
        {
            detachedRowCollisionCarts.Add(cart);
        }
    }

    private void UpdateDetachedRowCollisionIgnores()
    {
        for (int i = detachedRowCollisionCarts.Count - 1; i >= 0; i--)
        {
            CartController cart = detachedRowCollisionCarts[i];
            if (cart == null || HasClearedFormerRowCart(cart))
            {
                SetCartCollisionIgnored(cart, false);
                detachedRowCollisionCarts.RemoveAt(i);
            }
        }
    }

    private bool HasClearedFormerRowCart(CartController cart)
    {
        float clearDistance = nestedRowOverlapDistance + nestedRowPullForwardOffset + nestedRowDetachDistance;
        return GetClosestPlanarDistanceTo(cart.rb.position) > clearDistance;
    }
}
