// https://github.com/gotzawal/GOALLM_v7

using UnityEngine;

[System.Serializable]
public class ItemObject
{
    [Tooltip("Unique name of the item.")]
    public string itemName;

    [Tooltip("GameObject representing the item in the scene.")]
    public GameObject itemGameObject;

    [Tooltip("Initial Place where the item is located.")]
    public Place currentPlace;
}
