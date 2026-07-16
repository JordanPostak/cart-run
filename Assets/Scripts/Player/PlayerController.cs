using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5f;

    private Rigidbody rb;
    private Vector3 movementInput;

    private void Awake()
    {
        // Get the Rigidbody attached to this GameObject
        rb = GetComponent<Rigidbody>();
    }

    private void Update()
    {
        // Read input from WASD or arrow keys
        float horizontalInput = Input.GetAxisRaw("Horizontal");
        float verticalInput = Input.GetAxisRaw("Vertical");

        // Build a movement vector on the XZ plane
        movementInput = new Vector3(horizontalInput, 0f, verticalInput);

        // Normalize so diagonal movement is not faster
        if (movementInput.sqrMagnitude > 1f)
        {
            movementInput.Normalize();
        }
    }

    private void FixedUpdate()
    {
        // Move the Rigidbody in FixedUpdate
        if (movementInput == Vector3.zero)
        {
            return;
        }

        Vector3 movement = movementInput * moveSpeed * Time.fixedDeltaTime;
        rb.MovePosition(rb.position + movement);
    }
}
