using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private float followSpeed = 5f;
    [SerializeField] private Vector3 offset = new Vector3(0f, 10f, -10f);

    private void LateUpdate()
    {
        Transform followTarget = ResolveTarget();
        if (followTarget == null)
        {
            return;
        }

        Vector3 desiredPosition = followTarget.position + offset;
        float lerpFactor = Mathf.Clamp01(followSpeed * Time.deltaTime);
        transform.position = Vector3.Lerp(transform.position, desiredPosition, lerpFactor);
    }

    private Transform ResolveTarget()
    {
        if (target != null)
        {
            if (target.name == "Player" || target.name == "PlayerArmature")
            {
                Transform armature = target.Find("PlayerArmature");
                if (armature != null)
                {
                    return armature;
                }
            }

            return target;
        }

        GameObject armatureObject = GameObject.Find("PlayerArmature");
        if (armatureObject != null)
        {
            return armatureObject.transform;
        }

        GameObject playerObject = GameObject.Find("Player");
        return playerObject != null ? playerObject.transform : null;
    }
}