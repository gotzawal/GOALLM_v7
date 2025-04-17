// https://github.com/gotzawal/GOALLM_v7

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RhythmManager : MonoBehaviour
{
    public static RhythmManager instance;
    public GOAPManager goapManager;
    public Animator characterAnimator;

    [HideInInspector]
    public bool IsCommunicatingWithServer = false;

    // 이벤트 누적을 위한 버퍼 (기존의 Queue 대신 사용)
    private string eventBuffer = "";

    private void Awake()
    {
        if (instance != null)
        {
            Debug.LogWarning("RhythmManager: Duplicate instance found and will be destroyed.");
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        //StartCoroutine(WaitOneSecond());
        //TriggerEvent("Player encounter inside.");
    }


    IEnumerator WaitOneSecond()
    {
        //Debug.Log("Start waiting...");
        yield return new WaitForSeconds(2f); // 1초 대기
        //Debug.Log("1 second later...");
    }

    /// <summary>
    /// Handles server responses
    /// </summary>
    public void HandleServerResponse(GameManager.ServerResponse response)
    {
        // Update NPC expression
        if (!string.IsNullOrEmpty(response.Expression))
            characterAnimator.SetTrigger(response.Expression);

        // Update GOAP goals
        if (goapManager != null)
            goapManager.SetGoal(
                response.Action
            );

        IsCommunicatingWithServer = false;

        // 만약 누적된 이벤트 문자열이 있다면 전송
        if (!string.IsNullOrEmpty(eventBuffer))
        {
            ProcessNextEvent();
        }
    }


    /// <summary>
    /// 이벤트 문자열을 누적하는 방식으로 수정
    /// </summary>
    /// <param name="eventContent">전송할 이벤트 문자열</param>
    public void TriggerEvent(string eventContent)
    {
        // 기존 큐 대신, 이벤트 문자열을 eventBuffer에 누적
        eventBuffer += eventContent;
        Debug.Log($"RhythmManager: Event appended to buffer - {eventContent}");

        // 서버와 통신 중이 아니라면 바로 처리 시도
        if (!IsCommunicatingWithServer)
        {
            ProcessNextEvent();
        }
    }

    /// <summary>
    /// 누적된 이벤트 문자열을 서버로 전송
    /// </summary>
    private void ProcessNextEvent()
    {
        if (IsCommunicatingWithServer)
            return;

        if (string.IsNullOrEmpty(eventBuffer))
            return;

        string nextEvent = eventBuffer;
        eventBuffer = ""; // 전송 후 버퍼 초기화
        SendEventToServer(nextEvent);
    }

    /// <summary>
    /// 이벤트 문자열을 서버로 전송
    /// </summary>
    /// <param name="eventContent">전송할 이벤트 문자열</param>
    private void SendEventToServer(string eventContent)
    {
        if (GameManager.instance != null)
        {
            GameManager.instance.SendEmptyInput(eventContent);
            IsCommunicatingWithServer = true;
            Debug.Log($"RhythmManager: Event sent to server - {eventContent}");
        }
        else
        {
            Debug.LogError("RhythmManager: GameManager instance does not exist.");
        }
    }
}
