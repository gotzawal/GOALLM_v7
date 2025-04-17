
// https://github.com/gotzawal/GOALLM_v7

using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using System.Text.RegularExpressions; // 파일 상단에 추가

/// <summary>
/// Operates in the Cafe scene (_30_Cafe).
/// Retrieves serverUrl and apiKey stored in LoginInfo,
/// communicates with the server, and updates the UI.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager instance;

    public string Url;
    public string Api;

    [Header("UI References")]
    public TMP_InputField userInputField; // InputField for chat
    public TMP_Text NPCtalk;

    [Header("Managers")]
    public GOAPManager goapManager;
    public RhythmManager rhythmManager;

    // Server URL, API Key
    private string serverUrl;
    private string apiKey;

    // clientId to be received from the server
    private string clientId;

    #region ===== Server Communication DTO =====

    [System.Serializable]
    public class ServerRequest
    {
        public string client_id;
        public string api_key;
        public NPCStatus npc_status;
        public string userInput;
        public WorldStatus world_status;
        public string request_situation;

        public ServerRequest(
            string clientId,
            string apiKey,
            NPCStatus status,
            string input,
            WorldStatus worldStatus,
            string situation
        )
        {
            this.client_id = clientId;
            this.api_key = apiKey;
            this.npc_status = status;
            this.userInput = input;
            this.world_status = worldStatus;
            this.request_situation = situation;
        }
    }

    [System.Serializable]
    public class ServerResponse
    {
        public string client_id;
        public string audio_file;
        public string Expression;
        public string Talk;
        public string Action;
        public int remaining_tokens;
    }

    [System.Serializable]
    public class Quest
    {
        public string questName;
        public string isCompleted;
    }

    #endregion

    private void Awake()
    {
        // Singleton
        if (instance != null)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        serverUrl = Url;
        apiKey = Api;

        Debug.Log("GameManager: serverUrl: [" + serverUrl + "]");
        Debug.Log("GameManager: apiKey   : [" + apiKey+ "]");

        // Validate (may be empty if login scene is skipped)
        if (string.IsNullOrEmpty(serverUrl) || string.IsNullOrEmpty(apiKey))
        {
            Debug.LogWarning(
                "GameManager: serverUrl or apiKey is empty. Did you skip the login scene?"
            );
        }

        // Register InputField listener
        if (userInputField != null)
        {
            // Start() 등 초기화 시점에 추가
            userInputField.textComponent.richText = false;

            userInputField.onEndEdit.AddListener(OnInputFieldSubmit);
            userInputField.placeholder.GetComponent<TMP_Text>().text = "Enter your message...";
        }

        // Check RhythmManager / CafeUIManager / AudioManager
        if (rhythmManager == null)
            Debug.LogError("GameManager: RhythmManager is not assigned.");

        if (AudioManager.instance == null)
            Debug.LogError("GameManager: AudioManager instance is not found.");
    }


    /// <summary>
    /// When Enter is pressed after entering chat
    /// </summary>
    public void OnInputFieldSubmit(string input)
    {
        // IME 입력 중인 경우, 아직 조합 중이면 바로 처리하지 않고 지연시킵니다.
        if (!string.IsNullOrEmpty(Input.compositionString))
        {
            StartCoroutine(DelayedSubmit());
            return;
        }
        
        ProcessInput();
    }

    private IEnumerator DelayedSubmit()
    {
        // IME 조합이 끝날 때까지 대기합니다.
        while (!string.IsNullOrEmpty(Input.compositionString))
        {
            yield return null;
        }
        // 입력이 완전히 끝난 후, 한 프레임 더 대기합니다.
        yield return new WaitForEndOfFrame();
        
        ProcessInput();
    }

    private void ProcessInput()
    {
        string finalInput = userInputField.text;

        // 서버 응답 대기 중이면 처리하지 않음
        if (rhythmManager != null && rhythmManager.IsCommunicatingWithServer)
        {
            Debug.Log("서버 응답 대기 중입니다. 잠시 후에 입력해주세요.");
            return;
        }

        if (string.IsNullOrWhiteSpace(finalInput))
            return;

        // 서버와 통신
        StartCoroutine(CommunicateWithServer(finalInput, "User talk to NPC"));


        // 입력 필드 초기화
        userInputField.text = "";
        
        // TMP_InputField에 다시 포커스를 유지하도록 함
        // 반드시 UnityEngine.EventSystems 네임스페이스를 using 해야 합니다.
        userInputField.Select();
        userInputField.ActivateInputField();
        // 또는 EventSystem을 사용해서 강제로 선택할 수 있음
        // EventSystem.current.SetSelectedGameObject(userInputField.gameObject);
    }



    /// <summary>
    /// Send situation to server without user input, e.g., when NPC starts conversation
    /// </summary>
    public void SendEmptyInput(string situation)
    {
        StartCoroutine(CommunicateWithServer("...", situation));
    }

    IEnumerator CommunicateWithServer(string userInput, string situation)
    {
        // Check if serverUrl and apiKey are valid
        if (string.IsNullOrEmpty(serverUrl) || string.IsNullOrEmpty(apiKey))
        {
            Debug.LogWarning(
                "GameManager: serverUrl/apiKey is not set. Cannot communicate with server."
            );
            yield break;
        }

        // Indicate server communication in progress
        if (rhythmManager != null)
            rhythmManager.IsCommunicatingWithServer = true;

        // Current world status
        WorldStatus currentWorldStatus =
            goapManager != null ? goapManager.CurrentWorldStatus : null;

        // Request object
        ServerRequest request = new ServerRequest(
            clientId,
            apiKey,
            goapManager != null ? goapManager.CurrentNPCStatus : null,
            userInput,
            currentWorldStatus,
            situation
        );

        // Serialize to JSON
        string jsonRequest = JsonConvert.SerializeObject(request);

        using (UnityWebRequest webRequest = new UnityWebRequest(serverUrl + "/api/game", "POST"))
        {
            byte[] jsonToSend = System.Text.Encoding.UTF8.GetBytes(jsonRequest);
            webRequest.uploadHandler = new UploadHandlerRaw(jsonToSend);
            webRequest.downloadHandler = new DownloadHandlerBuffer();
            webRequest.SetRequestHeader("Content-Type", "application/json");

            yield return webRequest.SendWebRequest();

            if (webRequest.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("GameManager: Error => " + webRequest.error);
            }
            else
            {
                string jsonResponse = webRequest.downloadHandler.text;
                Debug.Log("GameManager: Received response => " + jsonResponse);

                ServerResponse response = null;
                try
                {
                    response = JsonConvert.DeserializeObject<ServerResponse>(jsonResponse);
                }
                catch (Exception ex)
                {
                    Debug.LogError("GameManager: Failed to parse response => " + ex.Message);

                    // 서버 통신은 완료된 것으로 처리
                    if (rhythmManager != null)
                        rhythmManager.IsCommunicatingWithServer = false;

                    if (NPCtalk != null)
                        NPCtalk.text = "Server Response Parsing Error";

                    yield break;
                }

                // Update client_id
                if (!string.IsNullOrEmpty(response.client_id))
                    clientId = response.client_id;

                // NPC dialogue UI
                if (NPCtalk != null)
                    NPCtalk.text = response.Talk;

                // Handle response in RhythmManager
                if (rhythmManager != null)
                    rhythmManager.HandleServerResponse(response);

                // Play audio
                if (!string.IsNullOrEmpty(response.audio_file))
                {
                    if (AudioManager.instance != null)
                        AudioManager.instance.PlayAudioFromBase64(response.audio_file);
                    else
                        Debug.LogError("GameManager: AudioManager not found, cannot play audio.");
                }
            }
        }

        if (rhythmManager != null)
            rhythmManager.IsCommunicatingWithServer = false;
    }

    
}
