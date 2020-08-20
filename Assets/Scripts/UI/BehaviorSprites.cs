using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BehaviorSprites : MonoBehaviour
{
    public Sprite[] Sprites;
    public Sprite DefaultSprite;

    public Sprite GetSprite(string name)
    {
        if (string.IsNullOrEmpty(name))
            return DefaultSprite;

        foreach (var sprite in Sprites)
            if (sprite.name == name)
                return sprite;
        return DefaultSprite;
    }
}
