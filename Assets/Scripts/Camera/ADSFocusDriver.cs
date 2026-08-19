// imports
using UnityEngine;



// methods
public class ADSFocusDriver : MonoBehaviour
{
    private const float MinSlideDirectionSqr = 0.0001f;

    // The transform we consider the "normal" follow origin (usually CameraRig or a shoulder pivot).
    [SerializeField] private Transform baseFollow;

    // The aim target that follows the mouse (your 'Target' object).
    [SerializeField] private Transform aimTarget;

    // How far toward the aim target the follow pivot is allowed to move.
    [SerializeField] private float maxSlideDistance = 3.0f;

    // How quickly the pivot slides into position.
    [SerializeField] private float slideSpeed = 14.0f;

    // How quickly the pivot returns to base when ADS ends.
    [SerializeField] private float returnSpeed = 18.0f;

    // How quickly the ADS blend weight changes (prevents snapping).
    [SerializeField] private float adsBlendSpeed = 12.0f;

    // Current blend between baseFollow (0) and slid position (1).
    private float adsWeight;

    // Target weight we are trying to reach (0 or 1).
    private float targetAdsWeight;

    // Runtime override so ADS can lock to a snapshot target.
    private Transform runtimeAimTarget;



    // Exposed so other scripts can switch ADS on/off.
    public void SetAdsActive(bool active)
    {
        // Set the target blend weight (we will smooth toward it).
        targetAdsWeight = active ? 1.0f : 0.0f;
    }


    // Exposed so other scripts can provide a locked aim target during ADS.
    public void SetRuntimeAimTarget(Transform newAimTarget)
    {
        // Store the runtime target (can be null to fall back to serialized aimTarget).
        runtimeAimTarget = newAimTarget;
    }


    private void LateUpdate()
    {
        // Stop if we have no base follow.
        if (!baseFollow) return;

        Transform self = transform;
        Vector3 basePos = baseFollow.position;
        Quaternion baseRot = baseFollow.rotation;
        float dt = Time.deltaTime;

        // Smoothly move adsWeight toward its target (prevents camera “jump”).
        adsWeight = Mathf.MoveTowards(adsWeight, targetAdsWeight, dt * adsBlendSpeed);

        // Choose which aim target we should use (runtime override wins).
        Transform activeAimTarget = runtimeAimTarget ? runtimeAimTarget : aimTarget;

        // If we have no aim target, just stick to base follow.
        if (!activeAimTarget)
        {
            // Copy base position.
            self.position = basePos;

            // Copy base rotation.
            self.rotation = baseRot;

            // Stop here.
            return;
        }

        // Build a direction from base follow toward the aim target.
        Vector3 toAim = activeAimTarget.position - basePos;

        // Flatten vertical so the pivot slides on the horizontal plane.
        toAim.y = 0.0f;

        // If the direction is basically zero, don't slide.
        if (toAim.sqrMagnitude <= MinSlideDirectionSqr)
        {
            // Copy base position.
            self.position = basePos;

            // Copy base rotation.
            self.rotation = baseRot;

            // Stop here.
            return;
        }

        // Normalize the direction.
        Vector3 dir = toAim.normalized;

        // Compute the desired slid position (clamped).
        Vector3 slidPos = basePos + (dir * maxSlideDistance);

        // Pick speed based on whether we're moving toward slidPos or returning.
        float speed = (adsWeight >= 0.5f) ? slideSpeed : returnSpeed;

        // Smoothly move toward either baseFollow or slidPos based on adsWeight.
        Vector3 targetPos = Vector3.Lerp(basePos, slidPos, adsWeight);

        // Interpolate position.
        self.position = Vector3.Lerp(self.position, targetPos, dt * speed);

        // Keep rotation matching base follow (so we don’t twist the rig).
        self.rotation = baseRot;
    }
}
