// https://github.com/gotzawal/GOALLM_v7

using UnityEngine;

public abstract class PlaceInteraction : MonoBehaviour
{
    /// <summary>
    /// Called when the state of a specific key changes.
    /// </summary>
    /// <param name="key">The key that changed</param>
    /// <param name="value">The new value</param>
    public abstract void OnStateChanged(string key, object value);
}
