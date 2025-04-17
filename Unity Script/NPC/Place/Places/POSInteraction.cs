// https://github.com/gotzawal/GOALLM_v7

using UnityEngine;

public class POSInteraction : PlaceInteraction
{
    private Renderer tvRenderer;
    private Material tvMaterial; // Material responsible for TV color

    void Start()
    {
        tvRenderer = GetComponent<Renderer>();
        if (tvRenderer == null)
        {
            Debug.LogError("TVInteraction: Cannot find Renderer component.");
        }
        else
        {
            // Select the second Material from the materials array (index starts from 0)
            tvMaterial = tvRenderer.materials[0];
            Debug.Log("TVInteraction: TV Material initialized successfully.");
        }
    }

    /// <summary>
    /// Changes the TV color based on the change of the tv_state key.
    /// </summary>
    /// <param name="key">Key name</param>
    /// <param name="value">New value</param>
    public override void OnStateChanged(string key, object value)
    {
        if (key.Equals("state", System.StringComparison.OrdinalIgnoreCase))
        {
            string state = value.ToString().ToLower();
            switch (state)
            {
                case "on":
                    ChangeColor(Color.green); //  on: green
                    break;
                case "off":
                    ChangeColor(Color.red); // off: red
                    break;
                default:
                    Debug.LogWarning($"TVInteraction: Unknown tv_state '{state}'");
                    break;
            }
        }
    }

    /// <summary>
    /// Changes the TV color.
    /// </summary>
    /// <param name="color">New color</param>
    private void ChangeColor(Color color)
    {
        if (tvMaterial != null)
        {
            tvMaterial.color = color;
            Debug.Log($"TVInteraction: Changed TV color to {color}.");
        }
        else
        {
            Debug.LogError("TVInteraction: TV Material is not initialized.");
        }
    }
}
