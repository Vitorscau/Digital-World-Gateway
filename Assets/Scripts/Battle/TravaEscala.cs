using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Segura a escala dos objetos que só agrupam a malha (ex.: "GRP_mesh" e a raiz do modelo), que não são ossos.
/// Na transição entre algumas animações (ex.: move → attack01 do Dorumon) o Mecanim mistura a curva de escala
/// desses objetos com zero por alguns quadros, e o Digimon encolhia até sumir ou piscava gigante.
/// Roda depois da animação (LateUpdate) e devolve a escala original; ossos continuam livres.
/// </summary>
public class TravaEscala : MonoBehaviour
{
    private readonly List<Transform> _alvos = new List<Transform>();
    private readonly List<Vector3> _escalas = new List<Vector3>();

    public static void Aplicar(GameObject modelo)
    {
        var trava = modelo.GetComponent<TravaEscala>();
        if (trava == null) trava = modelo.AddComponent<TravaEscala>();
        trava.Capturar();
    }

    private void Capturar()
    {
        _alvos.Clear();
        _escalas.Clear();
        var ossos = new HashSet<Transform>();
        var renderers = GetComponentsInChildren<Renderer>(true);
        foreach (var r in renderers)
            if (r is SkinnedMeshRenderer s)
                foreach (var b in s.bones) if (b != null) ossos.Add(b);
        foreach (var r in renderers)
            for (var t = r.transform; t != null; t = t.parent)
            {
                if (!ossos.Contains(t) && !_alvos.Contains(t)) { _alvos.Add(t); _escalas.Add(t.localScale); }
                if (t == transform) break;
            }
    }

    private void LateUpdate()
    {
        for (var i = 0; i < _alvos.Count; i++)
            if (_alvos[i] != null && _alvos[i].localScale != _escalas[i]) _alvos[i].localScale = _escalas[i];
    }
}
