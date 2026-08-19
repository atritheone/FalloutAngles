// imports
using UnityEngine;
using Unity.Cinemachine;



// methods
public class ADSAimLockExtension : CinemachineExtension
{
    private const float MinDirectionSqr = 0.0001f;

    // The world-space point we want the camera to aim at during ADS.
    [SerializeField] private Transform aimTarget;

    // Whether the lock is currently active.
    [SerializeField] private bool isActive;


    public void SetAimTarget(Transform newAimTarget)
    {
        // Store the new aim target reference.
        aimTarget = newAimTarget;
    }


    public void SetActive(bool active)
    {
        // Store whether the extension should run.
        isActive = active;
    }


    protected override void PostPipelineStageCallback(
        CinemachineVirtualCameraBase vcam,
        CinemachineCore.Stage stage,
        ref CameraState state,
        float deltaTime)
    {
        // Only override during the Aim stage.
        if (stage != CinemachineCore.Stage.Aim) return;

        // Stop if we are not active.
        if (!isActive) return;

        // Stop if we have no target to lock to.
        if (!aimTarget) return;

        // Read the resolved camera position.
        Vector3 camPos = state.RawPosition;

        // Compute direction from camera to ADS target.
        Vector3 toTarget = aimTarget.position - camPos;

        // Stop if target is effectively at camera position.
        if (toTarget.sqrMagnitude < MinDirectionSqr) return;

        // Use Cinemachine reference up to avoid roll.
        Vector3 up = state.ReferenceUp;

        // Build rotation that looks exactly at the target.
        Quaternion lookRotation = Quaternion.LookRotation(toTarget, up);

        // Hard override final orientation.
        state.RawOrientation = lookRotation;
    }
}
