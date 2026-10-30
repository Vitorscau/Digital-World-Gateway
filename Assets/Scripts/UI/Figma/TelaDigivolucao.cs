using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Animação de digivolução: o Digimon atual aparece, as silhuetas da forma antiga e da nova piscam cada vez
/// mais rápido e um clarão branco revela o novo Digimon. Ao terminar, a espécie do parceiro já mudou.
/// </summary>
public class TelaDigivolucao : TelaFigma
{
    private DigimonSalvo _d;
    private DigimonBase _velha, _nova;
    private Action<DigimonBase> _aoTerminar;
    private RetratoVivo _retratoVelho, _retratoNovo;
    private RawImage _imagem;
    private Image _clarao;
    private TMP_Text _texto;

    /// <summary>Só uma digivolução por vez (evita abrir duas se o perfil mudar no meio).</summary>
    public static bool Acontecendo { get; private set; }

    public static void Abrir(DigimonSalvo d, DigimonBase nova, Action<DigimonBase> aoTerminar = null)
    {
        if (d == null || nova == null || Acontecendo) return;
        var tela = Abrir<TelaDigivolucao>();
        tela._d = d;
        tela._velha = d.Ficha;
        tela._nova = nova;
        tela._aoTerminar = aoTerminar;
        Acontecendo = true;
        tela.StartCoroutine(tela.Animar());
    }

    protected override void Construir()
    {
        FigmaUI.Fundo(Raiz);
        FigmaUI.TextoEm(Raiz, 0, 160f, FigmaUI.Largura, 90f, "Digivolução", 80f, FigmaUI.Peso.Negrito);
        FigmaUI.Cantos(Raiz, 114.5f, 380f, 800f, 800f);
        _imagem = FigmaUI.Ret(Raiz, "Digimon", 164.5f, 430f, 700f, 700f).gameObject.AddComponent<RawImage>();
        _imagem.raycastTarget = false;
        _texto = FigmaUI.TextoEm(Raiz, 60f, 1260f, 909f, 260f, "", 54f, FigmaUI.Peso.Negrito);
        _texto.textWrappingMode = TextWrappingModes.Normal;
        var clarao = FigmaUI.Ret(Raiz, "Clarao", 0, 0, 1, 1);
        FigmaUI.Esticar(clarao);
        _clarao = FigmaUI.Imagem(clarao, null, new Color(1, 1, 1, 0));
    }

    private static string Nome(DigimonBase f) => f != null ? f.Name.Trim() : "?";

    private IEnumerator Animar()
    {
        _retratoVelho = RetratoVivo.Criar(_velha, 512, true);
        _retratoNovo = RetratoVivo.Criar(_nova, 512, true);
        yield return null; // um quadro para as câmeras enquadrarem
        Mostrar(_retratoVelho, Color.white);
        _texto.text = $"{Nome(_velha)} está digivolvendo!";
        yield return new WaitForSecondsRealtime(1.6f);

        // Silhuetas alternando, cada vez mais rápido.
        var intervalo = 0.35f;
        for (var i = 0; i < 18; i++)
        {
            Mostrar(i % 2 == 0 ? _retratoNovo : _retratoVelho, Color.black);
            yield return new WaitForSecondsRealtime(intervalo);
            intervalo = Mathf.Max(0.04f, intervalo * 0.82f);
        }

        // Clarão: no auge, a espécie muda.
        for (float t = 0; t < 1f; t += Time.unscaledDeltaTime * 4f)
        {
            _clarao.color = new Color(1, 1, 1, t);
            yield return null;
        }
        Digivolucao.Aplicar(_d, _nova);
        Mostrar(_retratoNovo, Color.white);
        _texto.text = $"{Nome(_velha)} digivolveu para\n<size=72>{Nome(_nova)}!</size>";
        for (float t = 1; t > 0f; t -= Time.unscaledDeltaTime * 1.5f)
        {
            _clarao.color = new Color(1, 1, 1, t);
            yield return null;
        }
        _clarao.color = new Color(1, 1, 1, 0);

        FigmaUI.BotaoBack(Raiz, (FigmaUI.Largura - 395f) / 2f, 1700f, Concluir, "OK");
    }

    private void Mostrar(RetratoVivo r, Color cor)
    {
        _imagem.texture = r != null ? r.Textura : null;
        _imagem.color = cor;
    }

    private void Concluir()
    {
        var nova = _nova;
        var aoTerminar = _aoTerminar;
        Fechar();
        aoTerminar?.Invoke(nova);
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        Acontecendo = false;
        if (_retratoVelho != null) Destroy(_retratoVelho.gameObject);
        if (_retratoNovo != null) Destroy(_retratoNovo.gameObject);
    }
}
