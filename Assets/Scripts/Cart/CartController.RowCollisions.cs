using System.Collections.Generic;
using UnityEngine;

public partial class CartController
{
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
}
