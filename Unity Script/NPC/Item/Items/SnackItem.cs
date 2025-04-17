// https://github.com/gotzawal/GOALLM_v7

using System.Collections; // Required for IEnumerator
using UnityEngine;

// If IUsableItem is within a namespace, include it here
// using YourProject.Interfaces;

public class SnackItem : MonoBehaviour, IUsableItem
{
    /// <summary>
    /// Executes the snack usage behavior: scales down the item.
    /// </summary>
    public void Use()
    {
        StartCoroutine(ScaleDown());
    }

    private IEnumerator ScaleDown()
    {
        Vector3 targetScale = Vector3.zero;
        float duration = 1f; // Duration of the scaling animation
        float elapsed = 0.5f;
        Vector3 initialScale = transform.localScale;

        while (elapsed < duration)
        {
            transform.localScale = Vector3.Lerp(initialScale, targetScale, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.localScale = targetScale;
        Debug.Log("SnackItem: Scale down completed.");
    }
}
