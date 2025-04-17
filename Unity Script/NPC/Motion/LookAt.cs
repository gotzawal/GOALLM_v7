// https://github.com/gotzawal/GOALLM_v7

using UnityEngine;

[RequireComponent(typeof(Animator))]
public class HeadNeckLookAtIK : MonoBehaviour
{
    // Target to look at (camera or other object)
    public Transform lookTarget;

    // Head/Neck IK weight (0~1)
    [Range(0f, 1f)]
    public float lookWeight = 1.0f;

    // Detailed weights (bodyWeight, headWeight, eyesWeight, clampWeight)
    [Range(0f, 1f)]
    public float bodyWeight = 0.0f; // Degree of body rotation

    [Range(0f, 1f)]
    public float headWeight = 1.0f; // Degree of head rotation

    [Range(0f, 1f)]
    public float eyesWeight = 1.0f; // Whether to rotate eyes as well

    [Range(0f, 1f)]
    public float clampWeight = 0.5f; // Joint bending limit (larger value means less bending)

    // Delay related variables
    public float lerpSpeed = 5.0f; // Speed to slowly follow the target

    private Animator animator;
    private Vector3 currentLookPosition; // Current look position

    void Start()
    {
        animator = GetComponent<Animator>();

        // Set initial position to character's position
        if (lookTarget != null)
        {
            currentLookPosition = lookTarget.position;
        }
        else if (Camera.main != null)
        {
            currentLookPosition = Camera.main.transform.position;
        }
        else
        {
            currentLookPosition = transform.position + transform.forward * 10f; // Default forward position
        }
    }

    void OnAnimatorIK(int layerIndex)
    {
        // If no target, set to main camera
        if (lookTarget == null)
        {
            if (Camera.main != null)
                lookTarget = Camera.main.transform;
            else
                return;
        }

        // Smoothly follow the target position with Lerp
        currentLookPosition = Vector3.Lerp(
            currentLookPosition,
            lookTarget.position,
            Time.deltaTime * lerpSpeed
        );

        // Set weights for applying IK
        // Parameter order: (lookWeight, bodyWeight, headWeight, eyesWeight, clampWeight)
        animator.SetLookAtWeight(lookWeight, bodyWeight, headWeight, eyesWeight, clampWeight);

        // Position for the head/neck chain to look at
        animator.SetLookAtPosition(currentLookPosition);
    }
}
