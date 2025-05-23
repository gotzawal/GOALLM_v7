// https://github.com/gotzawal/GOALLM_v7

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using System.Linq;

public class InteractionControl : MonoBehaviour
{
    [Header("References")]
    public GOAPManager goapManager; // Inspector에 할당
    public CharacterControl characterControl;
    public Transform handTransform;

    [Header("Items")]
    public List<ItemObject> items; // Inspector에서 할당

    [Header("Settings")]
    [Tooltip("자동으로 Place 할당 시 최대 거리 (미터)")]
    public float autoAssignDistanceThreshold = 5f;

    private Dictionary<string, ItemObject> itemDict;
    private NPCState npcState;


    void Start()
    {
        if (goapManager == null)
        {
            goapManager = FindObjectOfType<GOAPManager>();
            if (goapManager == null)
            {
                Debug.LogError("GOAPManager를 찾을 수 없습니다. InteractionControl에 할당해주세요.");
                return;
            }
        }
        if (handTransform == null)
        {
            Debug.LogError("handTransform이 할당되지 않았습니다.");
            return;
        }

        npcState = goapManager.NpcState;
        if (npcState == null)
        {
            Debug.LogError("GOAPManager의 NpcState가 null입니다.");
            return;
        }
        Debug.Log("InteractionControl: npcState 로드 성공.");

        InitializeItemDictionary();
        // 아이템들은 초기에는 GOAPManager.InitializeItems()에서 기본 Place에 할당되어 있다가
        // 아래의 재할당 로직을 통해 실제 거리를 고려하여 가까운 Place로 재할당됩니다.
        // 1초마다 AssignItemsToPlaces() 실행
        StartCoroutine(AssignItemsPeriodically());
    }

    IEnumerator AssignItemsPeriodically()
    {
        while (true)
        {
            AssignItemsToPlaces();
            yield return new WaitForSeconds(1f);
        }
    }
    IEnumerator WaitOneSecond()
    {
        //Debug.Log("Start waiting...");
        yield return new WaitForSeconds(1f); // 1초 대기
        //Debug.Log("1 second later...");
    }
    private void InitializeItemDictionary()
    {
        itemDict = new Dictionary<string, ItemObject>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in items)
        {
            if (string.IsNullOrEmpty(item.itemName))
            {
                Debug.LogWarning("InteractionControl: itemName이 비어있습니다.");
                continue;
            }
            if (item.itemGameObject == null)
            {
                Debug.LogWarning($"InteractionControl: '{item.itemName}'에 GameObject가 할당되지 않음.");
                continue;
            }
            if (!itemDict.ContainsKey(item.itemName))
                itemDict.Add(item.itemName, item);
            else
                Debug.LogWarning($"InteractionControl: 중복된 itemName '{item.itemName}' 발견.");
        }
        Debug.Log("InteractionControl: 아이템 딕셔너리 초기화 완료.");
    }

    private void AssignItemsToPlaces()
    {
        foreach (var item in itemDict.Values)
        {
            // 0. 새로 배정할 Place 찾기
            Place newPlace = FindClosestPlace(
                item.itemGameObject.transform.position,
                autoAssignDistanceThreshold);

            if (newPlace == null)
            {
                Debug.LogWarning(
                    $"InteractionControl: '{item.itemName}'의 할당 Place를 찾지 못함.");
                continue;
            }

            // 1.  이전 Place 인벤토리에서 제거
            if (item.currentPlace != null && item.currentPlace != newPlace)
            {
                item.currentPlace.Inventory.Remove(item.itemName.ToLower());
            }

            // 2. 새 Place 인벤토리에 중복 없이 추가
            if (!newPlace.Inventory.Contains(item.itemName.ToLower()))
            {
                newPlace.Inventory.Add(item.itemName.ToLower());
            }

            // 3. currentPlace 갱신
            item.currentPlace = newPlace;

            // 4. 계층 정리(루트로 빼기 — 필요 없다면 삭제 가능)
            //item.itemGameObject.transform.SetParent(null);
        }
    }


    private Place FindClosestPlace(Vector3 position, float distanceThreshold)
    {
        float minDistance = Mathf.Infinity;
        Place closestPlace = null;
        foreach (var place in goapManager.Places.Values)
        {
            if (place.GameObject == null) continue;
            float distance = Vector3.Distance(position, place.GameObject.transform.position);
            if (distance < minDistance && distance <= distanceThreshold)
            {
                minDistance = distance;
                closestPlace = place;
            }
        }
        //if (closestPlace != null)
        //    Debug.Log($"AssignItems: '{closestPlace.Name}'이(가) 가장 가까운 Place입니다 (거리: {minDistance}).");
        return closestPlace;
    }

    /// <summary>
    /// Handles the action of NPC picking up an item.
    /// </summary>
    /// <param name="itemName">Name of the item to pick up</param>
    /// <param name="npcState">NPC's state</param>
    /// <param name="worldState">World state</param>
    public IEnumerator PickUpItem(string itemName, NPCState npcState, WorldState worldState)
    {
        Debug.Log($"PickUpItem: Attempting to pick up item '{itemName}'.");

        if (string.IsNullOrEmpty(itemName))
        {
            Debug.LogWarning("PickUpItem called with an empty itemName.");
            yield break;
        }

        if (!itemDict.TryGetValue(itemName, out ItemObject item))
        {
            Debug.LogError($"PickUpItem: Could not find item '{itemName}' in InteractionControl.");
            yield break;
        }

        if (npcState.Inventory.Contains(itemName))
        {
            Debug.LogWarning($"PickUpItem: NPC already has item '{itemName}' in inventory.");
            yield break;
        }

        if (item.currentPlace == null)
        {
            Debug.LogWarning($"PickUpItem: Item '{itemName}' is not assigned to any Place.");
            yield break;
        }

        // Remove item from Place's inventory
        Place place = item.currentPlace;
        if (place.Inventory.Contains(itemName))
        {
            place.Inventory.Remove(itemName);
            Debug.Log($"PickUpItem: Removed item '{itemName}' from Place '{place.Name}'.");
        }
        else
        {
            Debug.LogWarning($"PickUpItem: Item '{itemName}' does not exist in Place '{place.Name}'.");
        }

        // Add item to NPC's inventory and update NPC state
        npcState.Inventory.Add(itemName);
        npcState.UpperBody["hold"] = itemName;
        Debug.Log($"PickUpItem: NPC added item '{itemName}' to inventory.");

        // 업데이트: NPC가 아이템을 소유하므로 currentPlace를 null로 설정
        item.currentPlace = null;
        Debug.Log($"PickUpItem: Set currentPlace of item '{itemName}' to null.");

        // 픽업 시퀀스를 실행하는 코루틴 시작
        //StartCoroutine(ExecutePickupSequence(item));
        yield return StartCoroutine(ExecutePickupSequence(item));
    }

    /// <summary>
    /// 전체 픽업 시퀀스를 실행하는 코루틴:
    /// 1. NPC가 item을 향해 점진적으로 회전하고 pickup 트리거를 실행하는 (TurnTowardsTargetAndPickup) 코루틴 실행
    /// 2. 회전 완료 후 1.6초 대기
    /// 3. 아이템을 NPC의 손에 붙임
    /// 4. 부착 후 추가로 1초 대기
    /// </summary>
    private IEnumerator ExecutePickupSequence(ItemObject item)
    {
        if (characterControl == null)
        {
            Debug.LogWarning("ExecutePickupSequence: CharacterControl 컴포넌트를 찾을 수 없음.");
            yield break;
        }

        //characterControl.SetDestination(item.itemGameObject.transform.localPosition);

        NavMeshHit hit;
        Vector3 targetPosition = item.itemGameObject.transform.position; // localPosition 말고, worldPosition 사용!

        if (NavMesh.SamplePosition(targetPosition, out hit, 5f, NavMesh.AllAreas))
        {
            characterControl.SetDestination(hit.position);
        }
        else
        {
            Debug.Log("목표 근처에 NavMesh가 없습니다.");
        }


        // Wait until NPC reaches the destination
        yield return new WaitUntil(() => !characterControl.IsMoving);

        // Rigidbody & Collider 비활성화
        Rigidbody itemRb = item.itemGameObject.GetComponent<Rigidbody>();
        Collider itemCollider = item.itemGameObject.GetComponent<Collider>();

        if (itemRb != null) itemRb.isKinematic = true;
        if (itemCollider != null) itemCollider.enabled = false;

        // 픽업 시퀀스 시작 
        characterControl.PickupAnimation(item.itemGameObject.transform);

        // 회전 완료 후 1.6초 대기
        yield return new WaitForSeconds(1.6f);

        // 아이템을 NPC의 손에 부착
        item.itemGameObject.transform.SetParent(handTransform);
        item.itemGameObject.transform.localPosition = Vector3.zero;
        item.itemGameObject.transform.localRotation = Quaternion.identity;
        Debug.Log($"ExecutePickupSequence: {item.itemName}을(를) NPC 손에 부착함.");

        // 부착 후 추가 1초 대기
        yield return new WaitForSeconds(1f);
        Debug.Log("ExecutePickupSequence: 부착 후 추가 딜레이 완료.");
    }


    /// <summary>
    /// Handles the action of NPC dropping an item.
    /// </summary>
    /// <param name="itemName">Name of the item to drop</param>
    /// <param name="npcState">NPC's state</param>
    /// <param name="worldState">World state</param>
    public void DropItem(string itemName, NPCState npcState, WorldState worldState)
    {
        Debug.Log($"DropItem: Attempting to drop item '{itemName}'.");

        if (string.IsNullOrEmpty(itemName))
        {
            Debug.LogWarning("DropItem called with an empty itemName.");
            return;
        }

        if (!itemDict.TryGetValue(itemName, out ItemObject item))
        {
            Debug.LogError($"DropItem: Could not find item '{itemName}' in InteractionControl.");
            return;
        }

        if (!npcState.Inventory.Contains(itemName))
        {
            Debug.LogWarning($"DropItem: NPC does not have item '{itemName}' in inventory.");
            return;
        }

        // Remove item from NPC's inventory
        npcState.Inventory.Remove(itemName);
        npcState.UpperBody["hold"] = "none";
        Debug.Log($"DropItem: Removed item '{itemName}' from NPC's inventory.");

        // Detach item's GameObject from hand
        item.itemGameObject.transform.SetParent(null);
        Debug.Log($"DropItem: Detached item '{itemName}' from NPC's hand.");

        // Rigidbody & Collider 활성화
        Rigidbody itemRb = item.itemGameObject.GetComponent<Rigidbody>();
        Collider itemCollider = item.itemGameObject.GetComponent<Collider>();



        // Determine Place based on NPC's current location
        string npcLocation = npcState.LowerBody.ContainsKey("location")
            ? npcState.LowerBody["location"].ToString()
            : "unknown";
        if (!worldState.Places.TryGetValue(npcLocation, out Place currentPlace))
        {
            Debug.LogWarning(
                $"DropItem: NPC's current location '{npcLocation}' does not exist in WorldState.Places."
            );
            return;
        }

        if (itemRb != null)
        {
            itemRb.isKinematic = false;        // 키네마틱 해제
            itemRb.useGravity = true;          // 중력 적용
            itemRb.collisionDetectionMode = CollisionDetectionMode.Continuous; // 충돌 누수를 줄이려면 옵션
        }
        if (itemCollider != null)
        {
            itemCollider.enabled = true;       // 콜라이더 온
        }

        // ② 드롭 위치를 약간 띄워서 설정 (바닥에 박히지 않도록)
        Vector3 dropPos;
        if (npcLocation != "unknown")
            dropPos = currentPlace.GameObject.transform.position + Vector3.up * 0.5f;
        else
            dropPos = item.itemGameObject.transform.position + Vector3.up * 0.5f;
        item.itemGameObject.transform.position = dropPos;
    }

    /// <summary>
    /// Handles the action of NPC using an item.
    /// </summary>
    /// <param name="itemName">Name of the item to use</param>
    /// <param name="npcState">NPC's state</param>
    /// <param name="worldState">World state</param>
    public void UseItem(string itemName, NPCState npcState, WorldState worldState)
    {
        Debug.Log($"UseItem: Attempting to use item '{itemName}'.");

        if (string.IsNullOrEmpty(itemName))
        {
            Debug.LogWarning("UseItem called with an empty itemName.");
            return;
        }

        if (!itemDict.TryGetValue(itemName, out ItemObject item))
        {
            Debug.LogError($"UseItem: Could not find item '{itemName}' in InteractionControl.");
            return;
        }

        if (!npcState.Inventory.Contains(itemName))
        {
            Debug.LogWarning($"UseItem: NPC does not have item '{itemName}' in inventory.");
            return;
        }

        // Get IUsableItem component from item's GameObject
        IUsableItem usableItem = item.itemGameObject.GetComponent<IUsableItem>();
        if (usableItem == null)
        {
            Debug.LogError($"UseItem: Item '{itemName}' does not have a usable script attached.");
            return;
        }

        // Execute Use method of the item
        usableItem.Use();
        Debug.Log($"UseItem: Executed Use() method of item '{itemName}'.");

        // Handle effects of item use (e.g., updating NPC's resources)
        // Assume item use effects are handled by separate scripts or logic

        // Do not remove item from inventory after use (adjust based on requirements)
    }
}
