using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    private enum CameraOffsetMode
    {
        World,
        TargetLocal
    }

    // Each preset is serialized so new views can be added or tuned in the Inspector.
    [System.Serializable]
    private class CameraPreset
    {
        [SerializeField] private string name;
        [SerializeField] private KeyCode activationKey;
        [SerializeField] private CameraOffsetMode offsetMode;
        [SerializeField] private Vector3 offset;
        [SerializeField] private Vector3 rotationEuler;
        [SerializeField] private bool lookAtTarget;
        [SerializeField] private Vector3 lookAtOffset = new Vector3(0f, 1.5f, 0f);
        [SerializeField] private bool orthographic = true;
        [SerializeField] private float orthographicSize = 10f;
        [SerializeField] private float fieldOfView = 60f;

        public string Name => name;
        public KeyCode ActivationKey => activationKey;
        public CameraOffsetMode OffsetMode => offsetMode;
        public Vector3 Offset => offset;
        public Quaternion Rotation => Quaternion.Euler(rotationEuler);
        public bool LookAtTarget => lookAtTarget;
        public Vector3 LookAtOffset => lookAtOffset;
        public bool Orthographic => orthographic;
        public float OrthographicSize => orthographicSize;
        public float FieldOfView => fieldOfView;

        public CameraPreset(string name, KeyCode activationKey, CameraOffsetMode offsetMode, Vector3 offset, Vector3 rotationEuler, bool lookAtTarget, Vector3 lookAtOffset, bool orthographic, float orthographicSize, float fieldOfView)
        {
            this.name = name;
            this.activationKey = activationKey;
            this.offsetMode = offsetMode;
            this.offset = offset;
            this.rotationEuler = rotationEuler;
            this.lookAtTarget = lookAtTarget;
            this.lookAtOffset = lookAtOffset;
            this.orthographic = orthographic;
            this.orthographicSize = orthographicSize;
            this.fieldOfView = fieldOfView;
        }
    }

    [SerializeField] private Transform target;
    [SerializeField] private float followSpeed = 5f;

    [Header("Camera Presets")]
    [SerializeField] private CameraPreset[] presets =
    {
        new CameraPreset("Gameplay Top Down", KeyCode.F1, CameraOffsetMode.World, new Vector3(0f, 15f, 0f), new Vector3(90f, 0f, 0f), false, Vector3.zero, true, 10f, 60f),
        new CameraPreset("Developer Chase", KeyCode.F2, CameraOffsetMode.TargetLocal, new Vector3(0f, 4f, -8f), new Vector3(25f, 0f, 0f), true, new Vector3(0f, 1.5f, 0f), false, 10f, 60f)
    };

    [SerializeField] private KeyCode startingPresetKey = KeyCode.F2;
    [SerializeField] private int startingPresetIndex = 1;
    [SerializeField] private float transitionSpeed = 6f;
    [SerializeField] private float targetLocalTurnSpeed = 2.5f;

    // Kept for scenes that already serialized the old single-offset camera setup.
    [SerializeField, HideInInspector] private Vector3 offset = new Vector3(0f, 10f, -10f);

    private Camera controlledCamera;
    private int activePresetIndex;
    private bool hasSmoothedTargetRotation;
    private Quaternion smoothedTargetRotation = Quaternion.identity;

    private void Awake()
    {
        controlledCamera = GetComponent<Camera>();
        EnsureDefaultPresets();
        activePresetIndex = GetStartingPresetIndex();
        ApplyProjection(GetActivePreset());
    }

    private void LateUpdate()
    {
        Transform followTarget = ResolveTarget();
        if (followTarget == null)
        {
            return;
        }

        UpdateActivePresetFromInput();

        CameraPreset activePreset = GetActivePreset();
        if (activePreset == null)
        {
            return;
        }

        Quaternion targetBasis = GetTargetBasis(activePreset, followTarget);
        Vector3 desiredPosition = GetPresetWorldPosition(activePreset, followTarget, targetBasis);
        Quaternion desiredRotation = GetPresetWorldRotation(activePreset, followTarget, targetBasis, desiredPosition);
        float lerpFactor = Mathf.Clamp01(followSpeed * Time.deltaTime);
        float transitionFactor = Mathf.Clamp01(transitionSpeed * Time.deltaTime);

        // Position and rotation are both interpolated so switching presets never snaps.
        transform.position = Vector3.Lerp(transform.position, desiredPosition, lerpFactor);
        transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, transitionFactor);
        ApplyProjection(activePreset);
    }

    private CameraPreset GetActivePreset()
    {
        if (presets == null || presets.Length == 0)
        {
            return null;
        }

        activePresetIndex = Mathf.Clamp(activePresetIndex, 0, presets.Length - 1);
        return presets[activePresetIndex];
    }

    private int GetStartingPresetIndex()
    {
        if (presets == null || presets.Length == 0)
        {
            return 0;
        }

        if (startingPresetKey != KeyCode.None)
        {
            for (int i = 0; i < presets.Length; i++)
            {
                CameraPreset preset = presets[i];
                if (preset != null && preset.ActivationKey == startingPresetKey)
                {
                    return i;
                }
            }
        }

        return Mathf.Clamp(startingPresetIndex, 0, presets.Length - 1);
    }

    private void UpdateActivePresetFromInput()
    {
        // The activation key lives on the preset, making future presets easy to add.
        for (int i = 0; i < presets.Length; i++)
        {
            CameraPreset preset = presets[i];
            if (preset != null && Input.GetKeyDown(preset.ActivationKey))
            {
                activePresetIndex = i;
                ApplyProjection(preset);
                return;
            }
        }
    }

    private Quaternion GetTargetBasis(CameraPreset preset, Transform followTarget)
    {
        Quaternion desiredBasis = GetTargetYawRotation(followTarget);
        if (preset.OffsetMode != CameraOffsetMode.TargetLocal)
        {
            hasSmoothedTargetRotation = false;
            smoothedTargetRotation = desiredBasis;
            return desiredBasis;
        }

        if (!hasSmoothedTargetRotation)
        {
            hasSmoothedTargetRotation = true;
            smoothedTargetRotation = desiredBasis;
            return smoothedTargetRotation;
        }

        // Smooth the behind-the-player direction separately to reduce chase-camera motion sickness.
        float turnFactor = Mathf.Clamp01(targetLocalTurnSpeed * Time.deltaTime);
        smoothedTargetRotation = Quaternion.Slerp(smoothedTargetRotation, desiredBasis, turnFactor);
        return smoothedTargetRotation;
    }

    private Quaternion GetTargetYawRotation(Transform followTarget)
    {
        Vector3 forward = Vector3.ProjectOnPlane(followTarget.forward, Vector3.up);
        if (forward.sqrMagnitude < 0.001f)
        {
            return Quaternion.identity;
        }

        return Quaternion.LookRotation(forward.normalized, Vector3.up);
    }

    private Vector3 GetPresetWorldPosition(CameraPreset preset, Transform followTarget, Quaternion targetBasis)
    {
        if (preset.OffsetMode == CameraOffsetMode.TargetLocal)
        {
            // Target-local offsets let chase-style presets stay behind the player as they turn.
            return followTarget.position + targetBasis * preset.Offset;
        }

        return followTarget.position + preset.Offset;
    }

    private Quaternion GetPresetWorldRotation(CameraPreset preset, Transform followTarget, Quaternion targetBasis, Vector3 cameraPosition)
    {
        if (preset.LookAtTarget)
        {
            Vector3 lookTarget = followTarget.position + targetBasis * preset.LookAtOffset;
            Vector3 lookDirection = lookTarget - cameraPosition;
            if (lookDirection.sqrMagnitude > 0.001f)
            {
                return Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
            }
        }

        return preset.Rotation;
    }

    private void ApplyProjection(CameraPreset preset)
    {
        if (controlledCamera == null || preset == null)
        {
            return;
        }

        // Projection is stored per preset: gameplay can stay orthographic while dev view uses perspective depth.
        controlledCamera.orthographic = preset.Orthographic;

        float transitionFactor = Mathf.Clamp01(transitionSpeed * Time.deltaTime);
        controlledCamera.orthographicSize = Mathf.Lerp(controlledCamera.orthographicSize, preset.OrthographicSize, transitionFactor);
        controlledCamera.fieldOfView = Mathf.Lerp(controlledCamera.fieldOfView, preset.FieldOfView, transitionFactor);
    }

    private void EnsureDefaultPresets()
    {
        if (presets != null && presets.Length > 0)
        {
            return;
        }

        // The first preset uses the legacy offset so older scene values remain useful.
        presets = new[]
        {
            new CameraPreset("Gameplay Top Down", KeyCode.F1, CameraOffsetMode.World, offset, new Vector3(90f, 0f, 0f), false, Vector3.zero, true, 10f, 60f),
            new CameraPreset("Developer Chase", KeyCode.F2, CameraOffsetMode.TargetLocal, new Vector3(0f, 4f, -8f), new Vector3(25f, 0f, 0f), true, new Vector3(0f, 1.5f, 0f), false, 10f, 60f)
        };
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

    private void OnValidate()
    {
        followSpeed = Mathf.Max(0f, followSpeed);
        transitionSpeed = Mathf.Max(0f, transitionSpeed);
        targetLocalTurnSpeed = Mathf.Max(0f, targetLocalTurnSpeed);
        EnsureDefaultPresets();
        startingPresetIndex = Mathf.Clamp(startingPresetIndex, 0, presets.Length - 1);
    }
}
