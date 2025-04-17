// https://github.com/gotzawal/GOALLM_v7

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(Animator), typeof(NavMeshAgent))]
public class CharacterControl : MonoBehaviour
{
    // LocomotionSimpleAgent 기반 변수
    private Animator anim;
    private NavMeshAgent agent;
    private Vector2 smoothDeltaPosition = Vector2.zero;
    private Vector2 velocity = Vector2.zero;

    // Low-pass filter 시간 상수 (인스펙터에서 조절 가능)
    [SerializeField]
    private float smoothingTime = 0.3f;

    // 위치 보정 계수 (인스펙터에서 조절 가능)
    [SerializeField]
    private float positionCorrectionFactor = 0.9f;

    // Idle 상태에서 바라볼 오브젝트 (Inspector에서 할당)
    [SerializeField]
    private Transform lookAtTarget;

    // 회전 스무스 업데이트용 변수
    // currentTurnAngle: 현재 상태의 회전 오프셋 (목표 각도까지의 차이)
    // turnVelocity: SmoothDampAngle이 내부적으로 계산한 각속도 (deg/s)
    private float currentTurnAngle = 0f;
    private float turnVelocity = 0f;

    // Sit 관련
    private float previousHeight;

    // 픽업 시퀀스 진행 여부를 판단하는 플래그
    private bool isPickupSequenceActive = false;

    // 제스처 리스트 (애니메이터에 Trigger로 등록되어 있어야 함)
    private List<string> validGestures = new List<string>
    {
        "Bashful", "Happy", "Crying", "Thinking", "Talking", "Looking",
        "No", "Fist Pump", "Agreeing", "Arguing", "Thankful", "Excited",
        "Clapping", "Rejected", "Look Around"
    };

    void Start()
    {
        anim = GetComponent<Animator>();
        agent = GetComponent<NavMeshAgent>();

        // 자동 위치 업데이트 비활성화 (LocomotionSimpleAgent 방식)
        agent.updatePosition = false;
        previousHeight = transform.position.y;
    }

    void Update()
    {
        // 픽업 시퀀스가 진행 중이면 Update의 이동/회전 갱신을 건너뛰기
        if (!isPickupSequenceActive)
        {
            if (IsMoving)
            {
                // (기존 이동 및 회전 업데이트 코드)
                Vector3 worldDeltaPosition = agent.nextPosition - transform.position;

                float dx = Vector3.Dot(transform.right, worldDeltaPosition);
                float dy = Vector3.Dot(transform.forward, worldDeltaPosition);
                Vector2 deltaPosition = new Vector2(dx, dy);

                float smooth = Mathf.Min(1.0f, Time.deltaTime / smoothingTime);
                smoothDeltaPosition = Vector2.Lerp(smoothDeltaPosition, deltaPosition, smooth);

                if (Time.deltaTime > 1e-5f)
                    velocity = smoothDeltaPosition / Time.deltaTime;

                Vector3 rawDesiredVelocityWorld = agent.desiredVelocity;
                Vector3 rawDesiredVelocityLocal3 = transform.InverseTransformDirection(rawDesiredVelocityWorld);
                Vector2 rawDesiredVelocityLocal = new Vector2(rawDesiredVelocityLocal3.x, rawDesiredVelocityLocal3.z);

                Vector2 blendedVelocity = Vector2.Lerp(velocity, rawDesiredVelocityLocal, 0.5f);

                bool shouldMove = blendedVelocity.magnitude > 0.1f && agent.remainingDistance > agent.stoppingDistance;
                anim.SetBool("move", shouldMove);

                anim.SetFloat("velx", blendedVelocity.x);
                anim.SetFloat("vely", blendedVelocity.y);

                Vector3 desiredDir = rawDesiredVelocityWorld.normalized;
                if (desiredDir.sqrMagnitude > 0.001f)
                {
                    float targetAngle = Vector3.SignedAngle(transform.forward, desiredDir, Vector3.up);
                    currentTurnAngle = Mathf.SmoothDampAngle(currentTurnAngle, targetAngle, ref turnVelocity, smoothingTime);
                    anim.SetFloat("turn", turnVelocity);
                }
                else
                {
                    currentTurnAngle = Mathf.SmoothDampAngle(currentTurnAngle, 0, ref turnVelocity, smoothingTime);
                    anim.SetFloat("turn", turnVelocity);
                }

                if (worldDeltaPosition.magnitude > agent.radius)
                    transform.position = agent.nextPosition - positionCorrectionFactor * worldDeltaPosition;
            }
            else
            {
                // 이동 중이 아니라면 idle 상태로 전환
                anim.SetBool("move", false);
                anim.SetFloat("velx", 0f);
                anim.SetFloat("vely", 0f);
                anim.SetFloat("turn", 0f);
            }
        }
    }

    void OnAnimatorMove()
    {
        // 애니메이터의 root motion 사용, Y축은 NavMeshAgent의 높이에 맞춤
        Vector3 position = anim.rootPosition;
        position.y = agent.nextPosition.y;
        transform.position = position;

        // rootRotation 적용
        transform.rotation = anim.rootRotation;
    }

    /// <summary>
    /// 이동 목표 지점을 설정 (GOAPManager에서 호출)
    /// </summary>
    public void SetDestination(Vector3 destination)
    {
        if (agent != null)
        {
            anim.SetTrigger("Standing_Idle");
            anim.SetBool("Sit", false);
            agent.stoppingDistance = 0.1f;
            agent.SetDestination(destination);
            Debug.Log($"SetDestination: {destination}");
        }
        else
        {
            Debug.LogWarning("CharacterControl: NavMeshAgent component is missing.");
        }
    }

    /// <summary>
    /// 현재 이동 중인지 여부를 반환 (GOAPManager에서 이동 종료 판단에 사용)
    /// </summary>
    public bool IsMoving
    {
        get
        {
            if (agent == null)
                return false;
            return agent.pathPending || agent.remainingDistance > agent.stoppingDistance;
        }
    }

    /// <summary>
    /// 제스처 애니메이션을 트리거 (GOAPManager에서 호출)
    /// </summary>
    public void PerformGesture(string gestureName)
    {
        if (string.IsNullOrEmpty(gestureName))
        {
            Debug.LogError("CharacterControl: Gesture name is null or empty.");
            return;
        }

        string capitalizedGesture = CapitalizeFirstLetter(gestureName);
        if (validGestures.Contains(capitalizedGesture))
        {
            anim.SetTrigger(capitalizedGesture);
            Debug.Log($"CharacterControl: Performing gesture '{capitalizedGesture}'.");

            // 3초 후 기본 자세로 전환하는 코루틴 실행
            StartCoroutine(ResetToIdleAfterDelay(3f));
        }
        else
        {
            Debug.LogError($"CharacterControl: Invalid gesture '{capitalizedGesture}'.");
        }
    }

    private IEnumerator ResetToIdleAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        anim.SetTrigger("Standing_Idle");
        Debug.Log("CharacterControl: Returning to idle stance.");
    }

    /// <summary>
    /// 앉기 동작 실행 (GOAPManager의 sit_chair 액션에서 호출)
    /// </summary>
    public void SitDown()
    {
        anim.SetBool("Sit", true);
        previousHeight = transform.position.y;
        Vector3 pos = transform.position;
        pos.y = 2.8f; // 원하는 앉은 높이 (애니메이션에 맞게 조정)
        transform.position = pos;
        Debug.Log("CharacterControl: SitDown executed.");
    }

    /// <summary>
    /// 일어서기 동작 실행 (GOAPManager의 stand_chair 액션에서 호출)
    /// </summary>
    public void StandUp()
    {
        anim.SetBool("Sit", false);
        Vector3 pos = transform.position;
        pos.y = previousHeight;
        transform.position = pos;
        Debug.Log("CharacterControl: StandUp executed.");
    }


    /// <summary>
    /// pickup 트리거를 실행하는 코루틴.
    /// </summary>
    public void PickupAnimation(Transform target)
    {
        float heightDiff = target.position.y - transform.position.y;
        anim.SetFloat("hight", heightDiff);
        anim.SetTrigger("pickup");

        Debug.Log($"Pickup: 높이 차이: {heightDiff}. Pickup 트리거 실행됨.");
    }




    /// <summary>
    /// 문자열의 첫 글자를 대문자로 변환
    /// </summary>
    private string CapitalizeFirstLetter(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;
        return char.ToUpper(input[0]) + input.Substring(1);
    }
}
