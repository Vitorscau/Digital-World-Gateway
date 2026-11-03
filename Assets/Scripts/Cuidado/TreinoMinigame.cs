using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Treino do V-Pet: escolhe o status (Offense, Defense, Speed, Brains) e faz 5 rodadas de
/// "toque quando o anel encostar no círculo". Cada acerto vira pontos de treino no status escolhido
/// (que deixam o parceiro mais forte na batalha). O Digimon faz o golpe de cada rodada no AR.
/// </summary>
public class TreinoMinigame : MonoBehaviour
{
    private const int Rodadas = 5;
    private const float TamanhoAlvo = 230f;
    private const float CentroX = FigmaUI.Largura / 2f, CentroY = 1110f;

    private DigimonVirtual _pet;
    private DigimonSalvo _d;
    private string _nome;
    private Action _aoFechar;
    private RectTransform _raiz;
    private TMP_Text _titulo, _texto, _retorno;
    private GameObject _escolhas;
    private RectTransform _anel, _alvo;
    private StatusTreino _status;
    private bool _tocou;

    public static TreinoMinigame Abrir(RectTransform canvas, DigimonVirtual pet, DigimonSalvo d, string nome, Action aoFechar)
    {
        var fundo = FigmaUI.Ret(canvas, "Treino", 0, 0, FigmaUI.Largura, FigmaUI.Altura);
        var img = FigmaUI.Imagem(fundo, null, new Color(0f, 0f, 0f, 0.35f));
        img.raycastTarget = true;
        var t = fundo.gameObject.AddComponent<TreinoMinigame>();
        t._raiz = fundo;
        t._pet = pet;
        t._d = d;
        t._nome = nome;
        t._aoFechar = aoFechar;
        t.Montar();
        return t;
    }

    private void Montar()
    {
        FigmaUI.CaixaVidro(_raiz, 87f, 560f, 855f, 1080f);
        _titulo = FigmaUI.TextoEm(_raiz, 87f, 590f, 855f, 70f, "Training", 56f, FigmaUI.Peso.Negrito);
        _texto = FigmaUI.TextoEm(_raiz, 127f, 680f, 775f, 90f, $"What will {_nome} train?", 36f, FigmaUI.Peso.Normal);
        _texto.textWrappingMode = TextWrappingModes.Normal;

        _escolhas = new GameObject("Escolhas", typeof(RectTransform));
        _escolhas.transform.SetParent(_raiz, false);
        FigmaUI.Esticar((RectTransform)_escolhas.transform);
        Escolha(StatusTreino.Offense, 107f, 800f);
        Escolha(StatusTreino.Defense, 527f, 800f);
        Escolha(StatusTreino.Speed, 107f, 960f);
        Escolha(StatusTreino.Brains, 527f, 960f);
        FigmaUI.BotaoBack(_escolhas.transform, (FigmaUI.Largura - 300f) / 2f, 1440f, Fechar, "Cancel", 300f / 395f);
    }

    private void Escolha(StatusTreino s, float x, float y)
    {
        var b = FigmaUI.BotaoBack(_escolhas.transform, x, y, () => StartCoroutine(Treinar(s)),
            $"{CuidadoDigimon.Nome(s)} ({CuidadoDigimon.Treino(_d, s)})", 395f / 395f);
        b.GetComponentInChildren<TMP_Text>().fontSize = 34f;
    }

    private IEnumerator Treinar(StatusTreino s)
    {
        _status = s;
        Destroy(_escolhas);
        _titulo.text = $"{CuidadoDigimon.Nome(s)} Training";
        _texto.text = "Tap when the ring touches the circle!";

        _alvo = Circulo("Alvo", new Color(1f, 1f, 1f, 0.9f));
        _anel = Circulo("Anel", new Color(1f, 0.85f, 0.2f));
        Tamanho(_alvo, TamanhoAlvo);
        _retorno = FigmaUI.TextoEm(_raiz, 87f, 1350f, 855f, 80f, "", 54f, FigmaUI.Peso.Negrito);

        // A tela inteira vira o botão de "agora!".
        var botao = gameObject.AddComponent<Button>();
        botao.targetGraphic = GetComponent<Image>();
        botao.transition = Selectable.Transition.None;
        botao.onClick.AddListener(() => _tocou = true);

        var pontos = 0;
        var perfeitos = 0;
        for (var r = 0; r < Rodadas; r++)
        {
            _texto.text = $"Round {r + 1} of {Rodadas} - tap when the ring touches the circle!";
            yield return new WaitForSeconds(0.5f);
            _tocou = false;
            var duracao = 1.35f - r * 0.1f;
            var tam = 640f;
            var nota = 0;
            for (float t = 0; t < duracao * 1.25f; t += Time.deltaTime)
            {
                tam = Mathf.Lerp(640f, 0f, t / duracao);
                Tamanho(_anel, tam);
                if (_tocou) break;
                yield return null;
            }
            var erro = Mathf.Abs(tam - TamanhoAlvo);
            if (_tocou && erro < 25f) { nota = 2; perfeitos++; Retorno("Perfect!", new Color(1f, 0.85f, 0.2f)); }
            else if (_tocou && erro < 70f) { nota = 1; Retorno("Good!", new Color(0.5f, 1f, 0.6f)); }
            else Retorno(_tocou ? "Miss..." : "Too slow!", new Color(1f, 0.5f, 0.5f));
            pontos += nota;
            _pet.AnimarTreino(_status, nota > 0);
            yield return new WaitForSeconds(0.9f);
        }

        Destroy(botao);
        Destroy(_anel.gameObject);
        Destroy(_alvo.gameObject);
        var ganho = CuidadoDigimon.Treinar(_d, _status, pontos);
        var total = CuidadoDigimon.Treino(_d, _status);
        var nivel = perfeitos == Rodadas ? "Perfect training!" : pontos >= 6 ? "Great training!" : pontos >= 3 ? "Good training." : "Weak training...";
        _titulo.text = nivel;
        _retorno.text = "";
        _texto.text = ganho > 0
            ? $"{CuidadoDigimon.Nome(_status)} +{ganho}  (total {total})\nEXP +{5 + pontos * 2}"
            : pontos > 0
                ? $"{CuidadoDigimon.Nome(_status)} is already maxed out for level {_d.nivel}.\nLevel up to train more."
                : "No points this time. Try again!";
        _texto.rectTransform.sizeDelta = new Vector2(_texto.rectTransform.sizeDelta.x, 300f);
        if (pontos >= 6) _pet.Comemorar();
        FigmaUI.BotaoBack(_raiz, (FigmaUI.Largura - 395f) / 2f, 1440f, Fechar, "OK");
    }

    private RectTransform Circulo(string nome, Color cor)
    {
        var rt = FigmaUI.Ret(_raiz, nome, 0, 0, 10, 10);
        rt.pivot = new Vector2(0.5f, 0.5f);
        FigmaUI.Imagem(rt, Formas.Anel(), cor);
        return rt;
    }

    private static void Tamanho(RectTransform rt, float t)
    {
        rt.anchoredPosition = new Vector2(CentroX - FigmaUI.Largura / 2f, -CentroY);
        rt.sizeDelta = new Vector2(t, t);
    }

    private void Retorno(string texto, Color cor)
    {
        _retorno.text = texto;
        _retorno.color = cor;
    }

    private void Fechar()
    {
        if (_pet != null) _pet.FimDoTreino();
        _aoFechar?.Invoke();
        Destroy(gameObject);
    }
}
