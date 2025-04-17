// https://github.com/gotzawal/GOALLM_v7

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Newtonsoft.Json;

public class DebugGameManager : MonoBehaviour
{
    [Header("UI References")]
    public TMP_InputField debugInputField; // 인스펙터에 할당
    [Header("Managers")]
    public GOAPManager goapManager;

    private void Start()
    {
        if (debugInputField != null)
        {

            // Start() 등 초기화 시점에 추가
            //debugInputField.textComponent.richText = false;

            debugInputField.onEndEdit.AddListener(OnDebugInputSubmit);
            debugInputField.placeholder.GetComponent<TMP_Text>().text = "Enter debug command...";
        }
    }

    private void OnDestroy()
    {
        if (debugInputField != null)
            debugInputField.onEndEdit.RemoveListener(OnDebugInputSubmit);
    }

    /// <summary>
    /// InputField 입력 완료시 호출
    /// </summary>
    /// <param name="input">예: "Gesture:Happy,ItemGoal:Pick up lance."</param>
    public void OnDebugInputSubmit(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return;

        Debug.Log("Debug Input Received: " + input);
        DebugCommunicateWithServer(input);

        debugInputField.text = "";
        debugInputField.ActivateInputField();
    }

    /// <summary>
    /// 입력 문자열을 파싱하여 GameManager.ServerResponse 객체 생성 후 RhythmManager에 전달
    /// </summary>
    /// <param name="input">예: "Gesture:Happy,ItemGoal:Pick up lance."</param>
    void DebugCommunicateWithServer(string input)
    {
        // Update GOAP goals
        if (goapManager != null)
            goapManager.SetGoal(
                input
            );
        //characterAnimator.SetTrigger(response.Expression);
    }
}
