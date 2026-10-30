using System;
using Niantic.Lightship.Maps.Core.Coordinates;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Colocado automaticamente em cada Digimon selvagem do mapa pelo spawner (_DigimonGOP).
/// Guarda de onde ele veio, o nível e até quando fica no mapa. Tocar nele começa a batalha.
/// </summary>
public class WildDigimon : MonoBehaviour
{
    public string SpawnId { get; private set; }
    public string Especie { get; private set; }
    public int Nivel { get; private set; }
    public DateTime DespawnUtc { get; private set; }
    public LatLng Local { get; private set; }
    public HabitatDigimon Habitat { get; private set; }
    public DigimonBase Ficha { get; private set; }
    /// <summary>Prefab usado no mapa (levado para a batalha, com os materiais certos).</summary>
    public GameObject Modelo { get; private set; }

    private _DigimonGOP _spawner;

    public TimeSpan TempoRestante => DespawnUtc - DateTime.UtcNow;

    /// <summary>O jogador está perto o bastante para batalhar?</summary>
    public bool PodeEncontrar => _spawner == null || _spawner.PodeEncontrar(this);

    public void Configurar(_DigimonGOP spawner, string spawnId, string especie, int nivel, DateTime despawnUtc,
        LatLng local, HabitatDigimon habitat, DigimonBase ficha, GameObject modelo)
    {
        _spawner = spawner;
        SpawnId = spawnId;
        Especie = especie;
        Nivel = nivel;
        DespawnUtc = despawnUtc;
        Local = local;
        Habitat = habitat;
        Ficha = ficha;
        Modelo = modelo;
    }

    private void OnMouseDown()
    {
        // Toque em cima de um botão da HUD não conta.
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
        if (Input.touchCount > 0 && EventSystem.current != null &&
            EventSystem.current.IsPointerOverGameObject(Input.GetTouch(0).fingerId)) return;
        TentarEncontro();
    }

    /// <summary>Começa a batalha se o jogador estiver perto; senão mostra um aviso.</summary>
    public void TentarEncontro()
    {
        if (!PodeEncontrar)
        {
            var raio = _spawner != null ? _spawner.RaioEncontroMetros : 40f;
            AvisoNaTela.Mostrar($"{NomeExibicao} está longe demais. Chegue a {raio:0} m para batalhar.");
            return;
        }

        Missoes.RegistrarEncontro(Habitat);
        if (Encontro.Iniciar(this, Ficha, Modelo)) RegistrarEncontro();
    }

    public string NomeExibicao => Ficha != null ? Ficha.Name.Trim() : Especie?.Replace("_", " ");

    /// <summary>O Digimon some do mapa para este jogador.</summary>
    public void RegistrarEncontro()
    {
        if (_spawner != null) _spawner.RegistrarEncontro(this);
    }
}
