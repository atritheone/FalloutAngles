using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(menuName = "Lockpicking/Lock Definition", fileName = "LockDefinition")]
public class LockDefinition : ScriptableObject
{
    public enum Difficulty
    {
        VeryEasy,
        Easy,
        Medium,
        Hard,
        VeryHard
    }

    public Difficulty difficulty = Difficulty.Easy;
    [FormerlySerializedAs("maxPickAngle")]
    [Min(0.1f)] public float maxBobbyPinAngle = 90.0f;
    [Min(0.1f)] public float sweetSpotAngleRange = 22.0f;
    [Min(0.1f)] public float maxCylinderRotation = 90.0f;
    [Min(0.01f)] public float pinBreakThreshold = 1.0f;
    [Min(0.0f)] public float stressIncreaseRate = 1.1f;
    [Min(0.0f)] public float stressRecoveryRate = 1.5f;

    private void OnValidate()
    {
        maxBobbyPinAngle = Mathf.Max(0.1f, maxBobbyPinAngle);
        sweetSpotAngleRange = Mathf.Max(0.1f, sweetSpotAngleRange);
        maxCylinderRotation = Mathf.Max(0.1f, maxCylinderRotation);
        pinBreakThreshold = Mathf.Max(0.01f, pinBreakThreshold);
        stressIncreaseRate = Mathf.Max(0.0f, stressIncreaseRate);
        stressRecoveryRate = Mathf.Max(0.0f, stressRecoveryRate);
    }
}
