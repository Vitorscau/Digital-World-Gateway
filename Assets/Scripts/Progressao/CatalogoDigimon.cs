using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Encontra a ficha (DigimonBase) de uma espécie pelo nome.
/// As fichas ficam em Resources/Digimons. Aceita variações: "Agumon_Black", "Agumon black", "agumonblack".
/// </summary>
public static class CatalogoDigimon
{
    private static Dictionary<string, DigimonBase> _porNome;

    public static IEnumerable<DigimonBase> Todos
    {
        get
        {
            Carregar();
            return _porNome.Values;
        }
    }

    public static DigimonBase Buscar(string especie)
    {
        if (string.IsNullOrEmpty(especie)) return null;
        Carregar();
        return _porNome.TryGetValue(Normalizar(especie), out var d) ? d : null;
    }

    private static void Carregar()
    {
        if (_porNome != null) return;
        _porNome = new Dictionary<string, DigimonBase>();
        foreach (var d in Resources.LoadAll<DigimonBase>("Digimons"))
        {
            _porNome[Normalizar(d.name)] = d;                 // nome do arquivo
            if (!string.IsNullOrEmpty(d.Name) && !_porNome.ContainsKey(Normalizar(d.Name)))
                _porNome[Normalizar(d.Name)] = d;             // nome dentro da ficha
        }
    }

    public static string Normalizar(string s) =>
        s.Trim().ToLowerInvariant().Replace(" ", "").Replace("_", "").Replace("-", "");
}
