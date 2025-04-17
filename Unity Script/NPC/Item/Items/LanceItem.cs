// https://github.com/gotzawal/GOALLM_v7

using System.Collections; // Required for IEnumerator
using UnityEngine;

// Add namespace if IUsableItem is within one
// using YourProject.Interfaces;

public class LanceItem : MonoBehaviour, IUsableItem
{
    /// <summary>
    /// Executes the lance usage behavior: moves the item up and then back down.
    /// </summary>
    public void Use()
    {
        StartCoroutine(MoveUpAndDown());
    }

    private IEnumerator MoveUpAndDown()
    {
        Vector3 originalPosition = transform.position;
        Vector3 targetPosition = originalPosition + Vector3.up * 2f; // Move up by 2 units
        float duration = 0.5f; // Time to move up
        float elapsed = 0f;

        // Move Up
        while (elapsed < duration)
        {
            transform.position = Vector3.Lerp(originalPosition, targetPosition, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }
        transform.position = targetPosition;
        Debug.Log("LanceItem: Moved up.");

        // Reset for moving down
        elapsed = 0f;

        // Move Down
        while (elapsed < duration)
        {
            transform.position = Vector3.Lerp(targetPosition, originalPosition, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }
        transform.position = originalPosition;
        Debug.Log("LanceItem: Moved back to original position.");
    }
}
