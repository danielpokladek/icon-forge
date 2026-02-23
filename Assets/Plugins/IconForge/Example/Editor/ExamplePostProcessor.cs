using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "IconForge Example Post Processor",
    menuName = "IconForge/Example Post Processor"
)]
public class ExamplePostProcessor : IconForgePostProcessor
{
    public override void OnGenerationComplete(List<GeneratedSpriteData> results)
    {
        foreach (var result in results)
        {
            // Log out GUID, prefab path, and sprite path for each generated sprite.
            Debug.Log($"Generated sprite for GUID: {result.GUID}.");
            Debug.Log($"Prefab path: {result.PrefabPath}");
            Debug.Log($"Sprite path: {result.SpritePath}");
        }
    }
}
