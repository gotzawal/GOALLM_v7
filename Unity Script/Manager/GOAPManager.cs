// https://github.com/gotzawal/GOALLM_v7

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Inspector에서 Place 설정 정보를 담는 클래스들
[Serializable]
public class PlaceData {
    public GameObject placeObject;

    [Tooltip("이 Place에 적용할 상태 정의 목록")]
    public List<PlaceStateData> stateDefinitions;

    [Tooltip("이 Place에서 앉을 수 있으면 체크")]
    public bool canSit = false;
}


[Serializable]
public class PlaceStateData {
    [Tooltip("상태의 키 (예: tv_state, door_state)")]
    public string stateKey;

    [Tooltip("해당 상태가 가질 수 있는 모든 값의 목록 (예: on, off)")]
    public List<string> possibleValues;

    [Tooltip("초기 상태 값 (possibleValues에 포함된 값이어야 함)")]
    public string initialValue;
}



public class GOAPManager : MonoBehaviour
{
    // Inspector에서 할당: GOAP 관련 캐릭터 제어
    public CharacterControl characterControl; 

    // Inspector에서 Place를 설정하는 리스트 (PlaceData를 통해 GameObject, 상태, 앉기 가능 여부를 지정)
    [Header("Place Settings")]
    public List<PlaceData> placesFromInspector;

    // NPC 초기 위치 지정 (이 값은 placesFromInspector의 placeObject 이름 중 하나여야 함)
    [Header("NPC Settings")]
    public string npcInitialLocation;

    // 참조할 InteractionControl (아이템 초기화를 위해)
    [Header("Item & Interaction Settings")]
    public InteractionControl interactionControl;

    // 내부에서 관리할 Place, Item, Actions 등
    public Dictionary<string, Place> Places { get; private set; }
    private Dictionary<string, List<string>> placeConnections;

    // Item 객체는 GOAPManager 내에서 사용할 Item 인스턴스로 재정의
    public Dictionary<string, Item> items { get; private set; }

    // GOAP 액션들
    private List<GOAPAction> actions;

    // NPC와 World 상태
    [SerializeField]
    private NPCState npcState;
    public NPCState NpcState { get { return npcState; } }

    private WorldState worldState;
    public WorldStatus CurrentWorldStatus { get { return new WorldStatus(worldState); } }
    public NPCStatus CurrentNPCStatus
    {
        get
        {
            string location = npcState.LowerBody.ContainsKey("location") 
                ? npcState.LowerBody["location"].ToString() 
                : "unknown";
            string inventory = (npcState.Inventory != null && npcState.Inventory.Count > 0)
                ? string.Join(", ", npcState.Inventory) 
                : "none";
            string pose = npcState.LowerBody.ContainsKey("pose")
                ? npcState.LowerBody["pose"].ToString()
                : "unknown";
            string holding = npcState.UpperBody.ContainsKey("hold")
                ? npcState.UpperBody["hold"].ToString()
                : "none";
            return new NPCStatus(location, inventory, pose, holding);
        }
    }

    // 실행 관련 상태 및 plan queue
    private bool isExecutingPlan = false;
    private Queue<List<GOAPAction>> planQueue = new Queue<List<GOAPAction>>();

    [Header("GOAP Execution Settings")]
    public bool discardExistingPlansOnNewGoal = true;

    // 기본 제스처 목록
    private List<string> gestureNames = new List<string>
    {
        "bashful", "happy", "crying", "thinking", "talking",
        "looking", "no", "fist pump", "agreeing", "arguing",
        "thankful", "excited", "clapping", "rejected", "look around"
    };

    void Awake()
    {
        // InteractionControl 참조가 없으면 씬에서 찾아봅니다.
        if (interactionControl == null)
        {
            interactionControl = FindObjectOfType<InteractionControl>();
        }
        
        // Place 초기화 (각 PlaceData를 통해)
        InitializePlaces();

        // 초기에는 각 Place에 아이템들을 모두 몰아넣는다.
        // 이후 InteractionControl에서 실제 위치 기반으로 재할당할 예정입니다.

        // Item 초기화: InteractionControl에 등록된 아이템을 기준으로 생성
        InitializeItems();

        // NPC 상태 초기화: 인벤토리는 비어있고, 기본 pose "stand"로, 초기 위치는 inspector에서 지정한 값 사용
        InitializeNPCState();

        // WorldState 초기화 (Places와 items 사용)
        worldState = new WorldState(Places, items);

        // Action 초기화: 여기에서는 Place의 상태 변경, move, gesture, sit/stand, pick/use/drop 액션 등을 생성합니다.
        InitializeActions();
    }
    private void InitializePlaces()
    {
        Places = new Dictionary<string, Place>(StringComparer.OrdinalIgnoreCase);
        placeConnections = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        // Inspector에서 입력한 PlaceData 리스트를 순회합니다.
        foreach (PlaceData pdata in placesFromInspector)
        {
            if (pdata.placeObject == null)
            {
                Debug.LogWarning("GOAPManager: PlaceData에 지정된 GameObject가 없습니다.");
                continue;
            }

            // Place 이름은 객체 이름을 소문자로 사용
            string placeName = pdata.placeObject.name.ToLower();

            // stateDefinitions가 있으면 각 stateKey의 초기값을 사용
            Dictionary<string, object> state = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            if (pdata.stateDefinitions != null)
            {
                foreach (PlaceStateData psd in pdata.stateDefinitions)
                {
                    if (!string.IsNullOrEmpty(psd.stateKey))
                    {
                        // 초기값을 저장 (대소문자 구분은 필요에 따라 처리)
                        state[psd.stateKey.ToLower()] = psd.initialValue;
                    }
                }
            }

            // Inventory는 초기엔 빈 리스트로 설정
            List<string> itemsOnPlace = new List<string>();

            // Place 인스턴스 생성
            Place newPlace = new Place(pdata.placeObject.name, pdata.placeObject, itemsOnPlace, state);
            newPlace.CanSit = pdata.canSit; // canSit 값 설정

            Places.Add(placeName, newPlace);
        }

        // 모든 Place 간 완전 연결 그래프 구성
        List<string> placeNames = Places.Keys.ToList();
        foreach (string from in placeNames)
        {
            List<string> connections = new List<string>();
            foreach (string to in placeNames)
            {
                if (!from.Equals(to, StringComparison.OrdinalIgnoreCase))
                    connections.Add(to);
            }
            placeConnections[from] = connections;
        }

        Debug.Log("GOAPManager: Inspector 기반 Place 초기화 완료.");
    }



    private void InitializeItems()
    {
        items = new Dictionary<string, Item>(StringComparer.OrdinalIgnoreCase);
        if (interactionControl != null && interactionControl.items != null)
        {
            foreach (var itemObj in interactionControl.items)
            {
                if (itemObj == null || string.IsNullOrEmpty(itemObj.itemName))
                    continue;

                string key = itemObj.itemName.ToLower();
                var effects = new Dictionary<string, Dictionary<string, object>>();

                // 예시로 각 아이템에 맞는 use 효과를 지정. 필요에 따라 조건문 등으로 처리합니다.
                if(key.Equals("snack"))
                {
                    effects.Add("use", new Dictionary<string, object>{{"health", 10}});
                }
                else if(key.Equals("lance"))
                {
                    effects.Add("use", new Dictionary<string, object>{{"health", 10}});
                }
                else
                {
                    // 기본적으로 use 가능하게 하려면 기본 효과를 넣거나, 빈 딕셔너리 대신 미리 정의된 효과를 넣어야 합니다.
                    effects.Add("use", new Dictionary<string, object>());
                }

                Item newItem = new Item(key, effects);
                items[key] = newItem;

                // 기본 Place에 할당 (예시)
                if (Places.Count > 0)
                {
                    string defaultPlaceKey = Places.Keys.First();
                    Place defaultPlace = Places[defaultPlaceKey];
                    defaultPlace.Inventory.Add(key);
                    itemObj.currentPlace = defaultPlace;
                }
            }
            Debug.Log("GOAPManager: InteractionControl의 아이템으로 Items 초기화 완료.");
        }
        else
        {
            Debug.LogWarning("GOAPManager: InteractionControl 혹은 items 리스트가 없음.");
        }
    }

    private void InitializeNPCState()
    {
        // NPC 인벤토리는 비어있고, UpperBody hold는 "none", LowerBody pose는 "stand"
        npcState = new NPCState(
            upperBody: new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase) { { "hold", "none" } },
            lowerBody: new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase) { { "pose", "stand" } },
            resources: new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase) { { "time", 0f }, { "health", 100f }, { "mental", 100f } },
            inventory: new List<string>(),
            stateData: new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        );
        npcState.GameObject = this.gameObject;

        // 초기 위치: Inspector에서 지정한 npcInitialLocation이 유효하면 사용, 아니면 Places의 첫 번째 값 사용
        string initLocation = !string.IsNullOrEmpty(npcInitialLocation) && Places.ContainsKey(npcInitialLocation.ToLower())
            ? npcInitialLocation.ToLower()
            : (Places.Keys.Count > 0 ? Places.Keys.First() : "unknown");
        npcState.LowerBody["location"] = initLocation;
        Debug.Log($"GOAPManager: NPC 초기 상태 설정 (location: {initLocation}, pose: stand, empty inventory).");
    }
    private void InitializeActions()
    {
        actions = new List<GOAPAction>();

        // Item 관련 액션 (pickup/use/drop)
        foreach (var itemKey in items.Keys)
        {
            string itemLower = itemKey.ToLower();
            actions.Add(ActionFactory.CreatePickAction(itemLower));
            actions.Add(ActionFactory.CreateDropAction(itemLower));
            actions.Add(ActionFactory.CreateUseAction(itemLower));
        }

        // Place 상태 변경 액션: 각 Place에 대해 stateDefinitions의 가능한 값들을 사용하여 액션 생성
        foreach (var pdata in placesFromInspector)
        {
            string placeName = pdata.placeObject.name.ToLower();
            if (pdata.stateDefinitions != null)
            {
                foreach (PlaceStateData psd in pdata.stateDefinitions)
                {
                    string key = psd.stateKey.ToLower();
                    // 현재 초기값을 가져옵니다.
                    string initialValue = psd.initialValue.ToLower();

                    // possibleValues 리스트에 포함된 각 값에 대해
                    foreach (var possibleValue in psd.possibleValues)
                    {
                        actions.Add(ActionFactory.CreatePlaceStateChangeAction(placeName, key, possibleValue));
                    }
                }
            }
        }


        // Move 액션: 모든 Place 간 연결에 대해 생성
        foreach (var place in placeConnections)
        {
            string fromPlace = place.Key.ToLower();
            foreach (var toPlace in place.Value)
            {
                actions.Add(ActionFactory.CreateMoveAction(fromPlace, toPlace.ToLower(), 1.5f, 1.5f));
            }
        }

        // 제스처 액션 생성
        foreach (var gesture in gestureNames)
        {
            actions.Add(ActionFactory.CreateGestureAction(gesture.ToLower()));
        }

        // Sit/Stand 액션: canSit가 true인 Place에 대해서만 생성
        foreach (var place in Places.Values)
        {
            if (place.CanSit)
            {
                actions.Add(ActionFactory.CreateSitAction(place.Name.ToLower()));
                actions.Add(ActionFactory.CreateStandAction(place.Name.ToLower()));
            }
        }

        Debug.Log("GOAPManager: Action 초기화 완료 (Item, Place 상태 변경, move, gesture, sit/stand 포함).");
    }



    public void SetGoal(string goal)
    {
        // 새로운 목표가 들어오면 기존 계획을 삭제할지 여부에 따라 큐를 비웁니다.
        if (discardExistingPlansOnNewGoal)
        {
            planQueue.Clear();
            Debug.Log("GOAPManager: Existing plans cleared due to 'discardExistingPlansOnNewGoal' setting.");
        }

        // 단일 goal 문자열을 GoalParser에 넘겨서 Goal 객체로 변환합니다.
        // 유효성 체크는 GoalParser 내부에서 처리합니다.
        Goal newGoal = GoalParser.ParseSentenceToGoal(goal, actions, worldState, weight: 1f);

        if (newGoal == null)
        {
            Debug.LogWarning("GOAPManager: Provided goal could not be parsed.");
            return;
        }

        // 단일 Goal만을 포함하는 리스트 생성
        List<Goal> parsedGoals = new List<Goal> { newGoal };

        // GOAPPlanner를 초기화한 후 계획을 생성합니다.
        GOAPPlanner planner = new GOAPPlanner(parsedGoals, actions);
        var planResult = planner.Plan(npcState, worldState);

        if (planResult != null && planResult.Count > 0)
        {
            Debug.Log("GOAPManager: Plan successfully created:");
            foreach (var action in planResult)
            {
                Debug.Log($"- {action.Name}");
            }

            // 생성한 계획을 큐에 추가하고 실행합니다.
            planQueue.Enqueue(planResult);
            Debug.Log("GOAPManager: New plan enqueued.");

            if (!isExecutingPlan)
            {
                StartCoroutine(ExecutePlanQueue());
            }
        }
        else
        {
            Debug.Log("GOAPManager: Failed to create a plan for the given goal.");
        }
    }

    /// <summary>
    /// Coroutine to execute plans from the queue sequentially
    /// </summary>
    private IEnumerator ExecutePlanQueue()
    {
        while (planQueue.Count > 0)
        {
            List<GOAPAction> currentPlan = planQueue.Dequeue();
            yield return StartCoroutine(ExecutePlan(currentPlan));
        }
    }

    // Add sit/stand logic to the existing ExecutePlan coroutine
    private IEnumerator ExecutePlan(List<GOAPAction> plan)
    {
        isExecutingPlan = true;

        foreach (var action in plan)
        {
            Debug.Log($"GOAPManager: Starting action '{action.Name}'.");

            if (IsMoveAction(action.Name))
            {
                string targetPlace = ExtractTargetPlaceFromMoveAction(action.Name);
                if (!string.IsNullOrEmpty(targetPlace))
                {
                    // Update NPC location and start moving
                    UpdateNPCLocation(targetPlace);

                    // Wait until NPC reaches the destination
                    yield return new WaitUntil(() => !characterControl.IsMoving);

                    // Confirm arrival
                    Debug.Log($"GOAPManager: Arrived at '{targetPlace}'.");

                    // Update location
                    if (npcState.LowerBody.ContainsKey("location"))
                    {
                        npcState.LowerBody["location"] = targetPlace;
                        Debug.Log($"GOAPManager: NPCState 'location' updated to '{targetPlace}'.");
                    }
                    else
                    {
                        Debug.LogWarning($"GOAPManager: NPCState 'location' key not found.");
                    }
                }
                else
                {
                    Debug.LogWarning(
                        $"GOAPManager: Failed to extract target place from action '{action.Name}'."
                    );
                }
            }
            else if (IsGestureAction(action.Name))
            {
                string gestureName = ExtractGestureName(action.Name);
                if (!string.IsNullOrEmpty(gestureName))
                {
                    // Call CharacterControl's PerformGesture method
                    if (characterControl != null)
                    {
                        characterControl.PerformGesture(gestureName);
                        Debug.Log(
                            $"GOAPManager: Requested CharacterControl to perform gesture '{gestureName}'."
                        );

                        // Apply gesture effect (set flag)
                        string gestureFlag = $"did_{gestureName.ToLower()}";
                        if (action.Effects.ContainsKey(gestureFlag))
                        {
                            npcState.StateData[gestureFlag] = true;
                            Debug.Log($"GOAPManager: NPCState '{gestureFlag}' set to true.");
                        }
                        yield return new WaitForSeconds(3f);
                    }
                    else
                    {
                        Debug.LogError("GOAPManager: CharacterControl reference is missing.");
                    }
                }
                else
                {
                    Debug.LogWarning(
                        $"GOAPManager: Failed to extract gesture name from action '{action.Name}'."
                    );
                }
            }
            // ─────────────────────────────────────────────────────────────────────
            // (Newly added) Handle Sit/Stand actions
            // ─────────────────────────────────────────────────────────────────────
            else if (action.Name.Equals("sit_chair", StringComparison.OrdinalIgnoreCase))
            {
                Debug.Log("GOAPManager: Executing 'sit_chair' action.");

                if (characterControl != null)
                {
                    // Call SitDown() in CharacterControl to set Animator Sit = true, adjust height, etc.
                    characterControl.SitDown();
                }
                else
                {
                    Debug.LogError("GOAPManager: CharacterControl reference is missing.");
                }

                // Wait for the duration of the action
                if (action.Cost.ContainsKey("time"))
                {
                    yield return new WaitForSeconds(action.Cost["time"]);
                }
                else
                {
                    yield return null;
                }

                // Update pose (LowerBody)
                if (action.Effects.TryGetValue("pose", out var newPose))
                {
                    npcState.LowerBody["pose"] = newPose;
                    Debug.Log($"GOAPManager: NPCState pose changed to '{newPose}'.");
                }
            }
            else if (action.Name.Equals("stand_chair", StringComparison.OrdinalIgnoreCase))
            {
                Debug.Log("GOAPManager: Executing 'stand_chair' action.");

                if (characterControl != null)
                {
                    // Call StandUp() in CharacterControl to set Animator Sit = false, restore height, etc.
                    characterControl.StandUp();
                }
                else
                {
                    Debug.LogError("GOAPManager: CharacterControl reference is missing.");
                }

                // Wait for the duration of the action
                if (action.Cost.ContainsKey("time"))
                {
                    yield return new WaitForSeconds(action.Cost["time"]);
                }
                else
                {
                    yield return null;
                }

                // Update pose (LowerBody)
                if (action.Effects.TryGetValue("pose", out var newPose))
                {
                    npcState.LowerBody["pose"] = newPose;
                    Debug.Log($"GOAPManager: NPCState pose changed to '{newPose}'.");
                }
            }
            // ─────────────────────────────────────────────────────────────────────
            else if (IsPickAction(action.Name) || IsDropAction(action.Name))
            {
                string itemName = ExtractItemNameFromAction(action.Name);
                if (!string.IsNullOrEmpty(itemName))
                {
                    InteractionControl interaction = FindObjectOfType<InteractionControl>();
                    if (interaction != null)
                    {
                        if (IsPickAction(action.Name))
                        {
                            //interaction.PickUpItem(itemName, npcState, worldState);
                            yield return StartCoroutine(interaction.PickUpItem(itemName, npcState, worldState));
                            Debug.Log($"GOAPManager: Picking up item '{itemName}'.");
                        }
                        else if (IsDropAction(action.Name))
                        {
                            interaction.DropItem(itemName, npcState, worldState);
                            Debug.Log($"GOAPManager: Dropping item '{itemName}'.");
                        }

                        // Wait for the duration of the action
                        if (action.Cost.ContainsKey("time"))
                        {
                            yield return new WaitForSeconds(action.Cost["time"]);
                        }
                        else
                        {
                            yield return null;
                        }
                    }
                    else
                    {
                        Debug.LogError(
                            "GOAPManager: InteractionControl script not found in the scene."
                        );
                        yield break;
                    }
                }
                else
                {
                    Debug.LogWarning(
                        $"GOAPManager: Failed to extract item name from action '{action.Name}'."
                    );
                }
            }
            else if (IsUseAction(action.Name))
            {
                string itemName = ExtractItemNameFromUseAction(action.Name);
                if (!string.IsNullOrEmpty(itemName))
                {
                    InteractionControl interaction = FindObjectOfType<InteractionControl>();
                    if (interaction != null)
                    {
                        interaction.UseItem(itemName, npcState, worldState);
                        Debug.Log($"GOAPManager: Using item '{itemName}'.");

                        // Wait for the duration of the action
                        if (action.Cost.ContainsKey("time"))
                        {
                            yield return new WaitForSeconds(action.Cost["time"]);
                        }
                        else
                        {
                            yield return null;
                        }
                    }
                    else
                    {
                        Debug.LogError(
                            "GOAPManager: InteractionControl script not found in the scene."
                        );
                        yield break;
                    }
                }
                else
                {
                    Debug.LogWarning(
                        $"GOAPManager: Failed to extract item name from action '{action.Name}'."
                    );
                }
            }
            else
            {
                // Handle place interactions based on 'place_state' key (e.g., TV On/Off)
                bool isPlaceInteraction = false;
                foreach (var effect in action.Effects)
                {
                    if (effect.Key.StartsWith("place_state:", StringComparison.OrdinalIgnoreCase))
                    {
                        isPlaceInteraction = true;
                        break;
                    }
                }

                if (isPlaceInteraction)
                {
                    foreach (var effect in action.Effects)
                    {
                        if (
                            effect.Key.StartsWith(
                                "place_state:",
                                StringComparison.OrdinalIgnoreCase
                            )
                        )
                        {
                            var parts = effect.Key.Split(':');
                            if (parts.Length == 3)
                            {
                                string placeName = parts[1].ToLower();
                                string stateKey = parts[2].ToLower();
                                object value = effect.Value;

                                UpdatePlaceState(placeName, stateKey, value);
                                Debug.Log(
                                    $"GOAPManager: Updated place state '{placeName}.{stateKey}' to '{value}'."
                                );

                                // Wait for the duration of the action
                                if (action.Cost.ContainsKey("time"))
                                {
                                    yield return new WaitForSeconds(action.Cost["time"]);
                                }
                                else
                                {
                                    yield return null;
                                }
                            }
                            else
                            {
                                Debug.LogWarning(
                                    $"GOAPManager: Invalid place_state format in effect key '{effect.Key}'."
                                );
                            }
                        }
                    }
                }
                else
                {
                    // Handle other actions (e.g., Think)
                    Debug.Log($"GOAPManager: Executing action '{action.Name}'.");
                    if (action.Cost.ContainsKey("time"))
                    {
                        yield return new WaitForSeconds(action.Cost["time"]);
                    }
                    else
                    {
                        yield return null;
                    }

                    // Apply action effects
                    foreach (var effect in action.Effects)
                    {
                        npcState.StateData[effect.Key] = effect.Value;
                        Debug.Log($"GOAPManager: NPCState '{effect.Key}' set to '{effect.Value}'.");
                    }
                }
            }
        }

        isExecutingPlan = false;
        Debug.Log("GOAPManager: Plan execution completed.");
    }

    /// <summary>
    /// Checks if the action is a move action based on its name
    /// </summary>
    private bool IsMoveAction(string actionName)
    {
        return actionName.Contains("_to_", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Checks if the action is a gesture action based on its name
    /// </summary>
    private bool IsGestureAction(string actionName)
    {
        return gestureNames.Any(g => g.Equals(actionName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Checks if the action is a use action based on its name
    /// </summary>
    private bool IsUseAction(string actionName)
    {
        return actionName.StartsWith("use_", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Checks if the action is a pick action based on its name
    /// </summary>
    private bool IsPickAction(string actionName)
    {
        return actionName.StartsWith("pick_", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Checks if the action is a drop action based on its name
    /// </summary>
    private bool IsDropAction(string actionName)
    {
        return actionName.StartsWith("drop_", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Extracts the target place from a move action name
    /// Example: "sofa_to_meja" -> "meja"
    /// </summary>
    private string ExtractTargetPlaceFromMoveAction(string actionName)
    {
        string[] parts = actionName.Split(new string[] { "_to_" }, StringSplitOptions.None);
        if (parts.Length == 2)
        {
            return parts[1].ToLower(); // Return the part after "_to_"
        }
        return null;
    }

    /// <summary>
    /// Extracts the gesture name from a gesture action name
    /// Example: "thinking" -> "thinking"
    /// </summary>
    private string ExtractGestureName(string actionName)
    {
        // Since gesture action names are the same as gesture names
        return actionName.ToLower();
    }

    /// <summary>
    /// Extracts the item name from a pick or drop action name
    /// Example: "pick_map" -> "map", "drop_snack" -> "snack"
    /// </summary>
    private string ExtractItemNameFromAction(string actionName)
    {
        string[] parts = actionName.Split('_');
        if (parts.Length >= 2)
        {
            return string.Join("_", parts, 1, parts.Length - 1).ToLower();
        }
        return null;
    }

    /// <summary>
    /// Extracts the item name from a use action name
    /// Example: "use_snack" -> "snack"
    /// </summary>
    private string ExtractItemNameFromUseAction(string actionName)
    {
        string[] parts = actionName.Split('_');
        if (parts.Length >= 2)
        {
            return string.Join("_", parts, 1, parts.Length - 1).ToLower();
        }
        return null;
    }

    /// <summary>
    /// Updates the NPC's destination based on the target object name
    /// </summary>
private void UpdateNPCLocation(string targetPlaceName)
{
    if (Places.TryGetValue(targetPlaceName.ToLower(), out Place targetPlace))
    {
        Debug.Log($"GOAPManager: Found target Place '{targetPlace.Name}'. Setting destination.");
        if (characterControl != null)
        {
            characterControl.SetDestination(targetPlace.GameObject.transform.position);
        }
        else
        {
            Debug.LogWarning("GOAPManager: CharacterControl reference is not assigned.");
        }
    }
    else
    {
        Debug.LogWarning($"GOAPManager: Could not find Place '{targetPlaceName}'.");
    }
}


    /// <summary>
    /// Updates the Place's state and calls the corresponding PlaceInteraction
    /// </summary>
    /// <param name="placeName">Name of the Place</param>
    /// <param name="key">State key</param>
    /// <param name="value">New value</param>
    public void UpdatePlaceState(string placeName, string key, object value)
    {
        if (Places.ContainsKey(placeName))
        {
            Place place = Places[placeName];
            place.State[key] = value;
            Debug.Log($"GOAPManager: Updated place '{placeName}' state '{key}' to '{value}'.");

            // Find and call the PlaceInteraction script
            PlaceInteraction interaction = place.GameObject.GetComponent<PlaceInteraction>();
            if (interaction != null)
            {
                interaction.OnStateChanged(key, value);
                Debug.Log(
                    $"GOAPManager: PlaceInteraction '{interaction.GetType().Name}' state change handled."
                );
            }
            else
            {
                Debug.LogWarning(
                    $"GOAPManager: Place '{placeName}' does not have a PlaceInteraction script attached."
                );
            }
        }
        else
        {
            Debug.LogError($"GOAPManager: Could not find place '{placeName}'.");
        }
    }
}
