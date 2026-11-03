using UnityEngine;

/// <summary>
/// Câmera da batalha sem AR (Battle_Scene): fica atrás e acima do parceiro, olhando para o meio da luta, e segue os
/// dois Digimons enquanto eles andam, desviam e investem. Afasta quando eles se separam, para os dois caberem na tela.
/// </summary>
public class CameraArena : MonoBehaviour
{
    private Transform _jogador, _inimigo;
    private float _altura;
    private Vector3 _direcao;   // do meio da luta para a câmera (horizontal), suavizada
    private Vector3 _olhar;
    private float _distancia;

    public static CameraArena Seguir(Camera cam, Transform jogador, Transform inimigo, float altura)
    {
        var c = cam.GetComponent<CameraArena>();
        if (c == null) c = cam.gameObject.AddComponent<CameraArena>();
        c._jogador = jogador;
        c._inimigo = inimigo;
        c._altura = Mathf.Max(0.1f, altura);
        c._direcao = c.DirecaoAlvo();
        c._olhar = c.OlharAlvo();
        c._distancia = c.DistanciaAlvo();
        c.Posicionar();
        return c;
    }

    private Vector3 Meio() => (_jogador.position + _inimigo.position) * 0.5f;

    private Vector3 OlharAlvo() => Meio() + Vector3.up * _altura * 0.45f;

    /// <summary>Atrás do parceiro (na linha inimigo → parceiro), um pouco de lado para não tampar o inimigo.</summary>
    private Vector3 DirecaoAlvo()
    {
        var deLa = _jogador.position - _inimigo.position;
        deLa.y = 0f;
        deLa = deLa.sqrMagnitude > 1e-6f ? deLa.normalized : -_jogador.forward;
        return (deLa + Vector3.Cross(Vector3.up, deLa) * 0.25f).normalized;
    }

    /// <summary>A menor distância em que os dois Digimons (pés e cabeça) cabem na tela, com folga nas bordas.</summary>
    private float DistanciaAlvo()
    {
        var cam = GetComponent<Camera>();
        var tanV = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        var tanH = tanV * Mathf.Max(0.1f, cam.aspect);
        var olhar = OlharAlvo();
        var d = _altura * 2.2f;
        for (var n = 0; n < 40 && !Cabe(olhar, d, tanH * 0.65f, tanV * 0.7f); n++) d *= 1.08f;
        return d;
    }

    private bool Cabe(Vector3 olhar, float d, float tanH, float tanV)
    {
        var pos = Posicao(olhar, _direcao, d);
        var giro = Quaternion.Inverse(Quaternion.LookRotation(olhar - pos));
        foreach (var alvo in new[] { _jogador.position, _inimigo.position })
            for (var h = 0; h <= 1; h++)
            {
                var p = giro * (alvo + Vector3.up * _altura * h - pos);
                if (p.z < _altura * 0.5f || Mathf.Abs(p.x) > p.z * tanH || Mathf.Abs(p.y) > p.z * tanV) return false;
            }
        return true;
    }

    private void LateUpdate()
    {
        if (_jogador == null || _inimigo == null) return;
        var k = 1f - Mathf.Exp(-Time.unscaledDeltaTime * 3f);
        _direcao = Vector3.Slerp(_direcao, DirecaoAlvo(), k * 0.6f); // gira devagar (não enjoa)
        _olhar = Vector3.Lerp(_olhar, OlharAlvo(), k);
        var alvo = DistanciaAlvo();
        _distancia = Mathf.Lerp(_distancia, alvo, alvo > _distancia ? k * 2f : k * 0.5f); // afasta rápido, aproxima devagar
        Posicionar();
    }

    private Vector3 Posicao(Vector3 olhar, Vector3 direcao, float d) =>
        olhar + direcao * d + Vector3.up * (d * 0.45f + _altura * 0.4f);

    private void Posicionar()
    {
        transform.position = Posicao(_olhar, _direcao, _distancia);
        transform.LookAt(_olhar);
    }
}
