

using Niantic.Lightship.Maps.MapLayers.Components;
using System;
using System.Collections;
using UnityEngine;
using Random = UnityEngine.Random;

[Serializable]
public class GameObjectProbability
{
    public LayerGameObjectPlacement gameObjectPlacement;
    [Range(0f, 1f)]
    public float probability;
}
