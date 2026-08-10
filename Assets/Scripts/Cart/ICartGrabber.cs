using UnityEngine;

public interface ICartGrabber
{
    Vector3 GetGrabberPosition();
    Quaternion GetGrabberRotation();
    void AttachToCart(CartController cart);
    void DetachFromCart(CartController cart);
}
