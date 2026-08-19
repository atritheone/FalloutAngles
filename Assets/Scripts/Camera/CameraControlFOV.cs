// imports
using UnityEngine;
using Unity.Cinemachine;



// methods
public class CameraControlFOV : MonoBehaviour
{
    // Reference to the Cinemachine Camera we will modify.
    [SerializeField] private CinemachineCamera virtualCamera;

    // Default FOV used when not aiming.
    [SerializeField] private float normalFOV = 60.0f;

    // How smooth the FOV blend feels (lower = snappier).
    [SerializeField] private float fOVSmoothTime = 0.08f;

    // When we are within this value, snap and stop smoothing.
    [SerializeField] private float fOVSnapEpsilon = 0.05f;

    // The target FOV we want to reach (normal mode).
    private float targetFov;

    // Velocity for SmoothDamp.
    private float fovVelocity;

    // Whether something (ADS) is overriding FOV.
    private bool isFovOverridden;

    // The FOV we want while overridden.
    private float overriddenFov;

    // The exact FOV we were at when override began.
    private float storedFovBeforeOverride;



    private void Awake()
    {
        CinemachineCamera vcam = virtualCamera;

        // Stop if we don't have a Cinemachine camera assigned.
        if (!vcam) return;

        // Initialize the target FOV to the current camera FOV.
        targetFov = vcam.Lens.FieldOfView;

        // If the current FOV is zero or invalid, force it to normal.
        if (targetFov <= 0.01f)
            targetFov = normalFOV;

        // Apply the starting FOV.
        vcam.Lens.FieldOfView = targetFov;
    }


    private void LateUpdate()
    {
        // Smoothly move toward the active target FOV.
        SmoothFovTowardTarget();
    }



    private void SmoothFovTowardTarget()
    {
        CinemachineCamera vcam = virtualCamera;

        // Stop if we don't have a camera.
        if (!vcam) return;

        // Read the current FOV.
        float currentFov = vcam.Lens.FieldOfView;

        // Choose which target we are aiming for.
        float activeTargetFov = isFovOverridden ? overriddenFov : targetFov;

        // Work out how far we are from the target.
        float toTarget = Mathf.Abs(currentFov - activeTargetFov);

        // Snap when close enough to prevent micro-updates.
        if (toTarget <= fOVSnapEpsilon)
        {
            // Apply exact value only when needed.
            if (!Mathf.Approximately(currentFov, activeTargetFov))
                vcam.Lens.FieldOfView = activeTargetFov;

            // Clear velocity for clean future blends.
            if (!Mathf.Approximately(fovVelocity, 0f))
                fovVelocity = 0f;

            // Stop here.
            return;
        }

        // Smoothly damp toward the active target.
        float smooth = Mathf.SmoothDamp(currentFov, activeTargetFov, ref fovVelocity, fOVSmoothTime);

        // Apply the smoothed value.
        vcam.Lens.FieldOfView = smooth;
    }



    // Normal API: set the normal target FOV.
    public void SetTargetFov(float newTargetFov)
    {
        // Store the target FOV.
        targetFov = newTargetFov;
    }


    // Normal API: read the current camera FOV.
    public float GetCurrentFov()
    {
        CinemachineCamera vcam = virtualCamera;

        // Stop if we have no camera.
        if (!vcam) return 0f;

        // Return the live FOV.
        return vcam.Lens.FieldOfView;
    }


    // ADS API: begin an override once (stores pre-ADS FOV only on first begin).
    public void BeginFovOverride(float fixedFov)
    {
        CinemachineCamera vcam = virtualCamera;

        // Stop if we have no camera.
        if (!vcam) return;

        // Store pre-override FOV only if not already overridden.
        if (!isFovOverridden)
            storedFovBeforeOverride = vcam.Lens.FieldOfView;

        // Store the override target.
        overriddenFov = fixedFov;

        // Mark override active.
        isFovOverridden = true;

        // Clear velocity so it feels crisp.
        fovVelocity = 0f;
    }


    // ADS API: update override FOV while active.
    public void SetOverrideFov(float fixedFov)
    {
        // Stop if not overridden.
        if (!isFovOverridden) return;

        // Update the override target.
        overriddenFov = fixedFov;
    }


    // ADS API: end override and restore to exact pre-ADS FOV.
    public void EndFovOverride()
    {
        CinemachineCamera vcam = virtualCamera;

        // Stop if we have no camera.
        if (!vcam) return;

        // Disable override so we use normal target again.
        isFovOverridden = false;

        // Set the normal target to the FOV we had before ADS.
        targetFov = storedFovBeforeOverride;

        // Clear velocity so the return blend uses the same feel as zoom-in.
        fovVelocity = 0f;
    }
}
