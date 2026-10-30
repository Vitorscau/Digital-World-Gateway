using System;
using Niantic.Lightship.Maps.MapLayers.Components;
using UnityEngine;

/// <summary>Onde um Digimon pode aparecer.</summary>
[Flags]
public enum HabitatDigimon
{
    Nenhum = 0,
    /// <summary>Ruas, calçadas, prédios, terrenos.</summary>
    Urbano = 1,
    /// <summary>Parques, gramados, matas, areia.</summary>
    Parque = 2,
    /// <summary>Rios, lagos, mar e as margens.</summary>
    Agua = 4,
    Todos = Urbano | Parque | Agua
}

public enum PeriodoDoDia
{
    Sempre,
    Dia,
    Noite
}

/// <summary>Uma entrada da tabela de spawn: qual Digimon, quão comum, onde e quando aparece.</summary>
[Serializable]
public class GameObjectProbability
{
    [Tooltip("Camada do mapa que coloca o modelo deste Digimon.")]
    public LayerGameObjectPlacement gameObjectPlacement;

    [Tooltip("Peso relativo: 0.5 aparece 10x mais que 0.05. Não precisa somar 1.")]
    [Range(0f, 1f)]
    public float probability = 0.1f;

    [Tooltip("Onde pode aparecer.")]
    public HabitatDigimon habitats = HabitatDigimon.Todos;

    [Tooltip("Quando pode aparecer (pelo relógio do celular).")]
    public PeriodoDoDia periodo = PeriodoDoDia.Sempre;

    [Tooltip("Faixa de nível (mínimo e máximo).")]
    public Vector2Int nivel = new Vector2Int(1, 10);

    [Tooltip("Para especialistas (não marcados em todos os habitats): multiplica o peso no habitat deles.")]
    public float bonusHabitat = 3f;
}
