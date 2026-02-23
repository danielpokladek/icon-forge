using System.Collections.Generic;
using UnityEngine;

public abstract class IconForgePostProcessor : ScriptableObject
{
    public abstract void OnGenerationComplete(List<GeneratedSpriteData> results);
}
