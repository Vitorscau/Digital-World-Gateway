using System;
using System.Collections.Generic;
using Niantic.Lightship.Maps.Core.Coordinates;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Um Encrypted Data no mapa (colocado pelo CoverageManager).
/// Tocar nele abre a tela para decifrar. Enquanto recarrega, fica roxo e gira devagar.
/// </summary>
public class PontoDados : MonoBehaviour
{
    private static readonly int CorBase = Shader.PropertyToID("_BaseColor");
    private static readonly Color Roxo = new Color(0.72f, 0.45f, 1f);

    public string Chave { get; private set; }
    public string Nome { get; private set; }
    /// <summary>Tipo do lugar no OpenStreetMap (ex.: "place_of_worship"). Vazio nos pontos extras.</summary>
    public string Tipo { get; private set; }
    public LatLng Local { get; private set; }

    private CoverageManager _gerente;
    private readonly List<(Renderer r, Color[] cores)> _renderers = new List<(Renderer, Color[])>();
    private readonly List<(Rotator rot, float velocidade)> _giros = new List<(Rotator, float)>();
    private MaterialPropertyBlock _bloco;
    private bool? _disponivelMostrado;

    public bool Disponivel => PerfilJogador.Atual.RecargaAte(Chave) == 0;

    public TimeSpan Recarga
    {
        get
        {
            var ate = PerfilJogador.Atual.RecargaAte(Chave);
            return ate == 0 ? TimeSpan.Zero : DateTimeOffset.FromUnixTimeSeconds(ate) - DateTimeOffset.UtcNow;
        }
    }

    public void Configurar(CoverageManager gerente, string chave, string nome, string tipo, LatLng local)
    {
        _gerente = gerente;
        Chave = chave;
        Nome = nome;
        Tipo = tipo;
        Local = local;

        foreach (var r in GetComponentsInChildren<Renderer>())
        {
            var cores = new Color[r.sharedMaterials.Length];
            for (var i = 0; i < cores.Length; i++)
            {
                var m = r.sharedMaterials[i];
                cores[i] = m != null && m.HasProperty(CorBase) ? m.GetColor(CorBase) : Color.white;
            }
            _renderers.Add((r, cores));
        }
        foreach (var rot in GetComponentsInChildren<Rotator>()) _giros.Add((rot, rot.rotationSpeed));
        CriarAreaDeToque();
        AtualizarVisual();
    }

    /// <summary>Caixa de toque em volta do modelo inteiro (o modelo não tem collider).</summary>
    private void CriarAreaDeToque()
    {
        if (GetComponent<Collider>() != null) return;
        var ok = false;
        var limites = new Bounds();
        foreach (var mf in GetComponentsInChildren<MeshFilter>())
        {
            if (mf.sharedMesh == null) continue;
            var b = mf.sharedMesh.bounds;
            for (var i = 0; i < 8; i++)
            {
                var canto = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                var local = transform.InverseTransformPoint(mf.transform.TransformPoint(canto));
                if (!ok) { limites = new Bounds(local, Vector3.zero); ok = true; }
                else limites.Encapsulate(local);
            }
        }
        if (!ok) return;
        var caixa = gameObject.AddComponent<BoxCollider>();
        caixa.center = limites.center;
        caixa.size = limites.size * 1.2f; // um pouco maior: mais fácil de acertar com o dedo
    }

    /// <summary>Azul quando pode decifrar; roxo e mais lento enquanto recarrega (como os PokéStops).</summary>
    public void AtualizarVisual()
    {
        var disponivel = Disponivel;
        if (_disponivelMostrado == disponivel) return;
        _disponivelMostrado = disponivel;

        _bloco ??= new MaterialPropertyBlock();
        foreach (var (r, cores) in _renderers)
        {
            if (r == null) continue;
            for (var i = 0; i < cores.Length; i++)
            {
                r.GetPropertyBlock(_bloco, i);
                _bloco.SetColor(CorBase, disponivel ? cores[i] : cores[i] * Roxo);
                r.SetPropertyBlock(_bloco, i);
            }
        }
        foreach (var (rot, velocidade) in _giros)
            if (rot != null) rot.rotationSpeed = disponivel ? velocidade : velocidade * 0.25f;
    }

    private void OnMouseDown()
    {
        // Toque em cima de um botão da HUD não conta.
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
        if (Input.touchCount > 0 && EventSystem.current != null &&
            EventSystem.current.IsPointerOverGameObject(Input.GetTouch(0).fingerId)) return;
        if (_gerente != null) _gerente.Tocar(this);
    }
}
