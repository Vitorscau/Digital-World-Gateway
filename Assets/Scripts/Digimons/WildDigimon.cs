using System;
using Niantic.Lightship.Maps.Core.Coordinates;
using UnityEngine;

/// <summary>
/// Colocado automaticamente em cada Digimon selvagem do mapa pelo spawner (_DigimonGOP).
/// Guarda de onde ele veio, o nível e até quando fica no mapa.
/// </summary>
public class WildDigimon : MonoBehaviour
{
    public string SpawnId { get; private set; }
    public string Especie { get; private set; }
    public int Nivel { get; private set; }
    public DateTime DespawnUtc { get; private set; }
    public LatLng Local { get; private set; }
    public HabitatDigimon Habitat { get; private set; }

    private _DigimonGOP _spawner;

    public TimeSpan TempoRestante => DespawnUtc - DateTime.UtcNow;

    /// <summary>O jogador está perto o bastante para batalhar?</summary>
    public bool PodeEncontrar => _spawner == null || _spawner.PodeEncontrar(this);

    public void Configurar(_DigimonGOP spawner, string spawnId, string especie, int nivel, DateTime despawnUtc,
        LatLng local, HabitatDigimon habitat)
    {
        _spawner = spawner;
        SpawnId = spawnId;
        Especie = especie;
        Nivel = nivel;
        DespawnUtc = despawnUtc;
        Local = local;
        Habitat = habitat;
    }

    /// <summary>Chame ao começar a batalha: o Digimon some do mapa para este jogador.</summary>
    public void RegistrarEncontro()
    {
        if (_spawner != null) _spawner.RegistrarEncontro(this);
    }
}
