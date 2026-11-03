using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Instruções das telas de AR, mostradas só na primeira vez em cada tela: mexer o celular devagar
/// para a câmera reconhecer o chão e tocar duas vezes no lugar onde o Digimon vai aparecer.
/// Depois de fechar, fica uma dica pequena no alto até o Digimon ser colocado.
/// </summary>
public class InstrucoesAR : MonoBehaviour
{
    /// <summary>Enquanto a janela está aberta (ou acabou de fechar), os toques não colocam o Digimon.</summary>
    public static bool BloqueiaToques => _aberta || Time.unscaledTime - _fechouEm < 0.4f;

    private static bool _aberta;
    private static float _fechouEm = -10f;
    private static InstrucoesAR _instancia;

    private string _chave;
    private GameObject _janela, _dica;
    private RectTransform _celular, _dedo, _anel1, _anel2;
    private Image _imgAnel1, _imgAnel2;

    /// <summary>Monta as instruções na cena; a janela só abre se o jogador ainda não a viu nesta tela.</summary>
    public static void Mostrar(string chave)
    {
        if (_instancia != null) return;
        _instancia = new GameObject("InstrucoesAR").AddComponent<InstrucoesAR>();
        _instancia._chave = "AR:" + chave;
        _instancia.Montar(!PerfilJogador.Atual.instrucoesVistas.Contains(_instancia._chave));
    }

    /// <summary>Avisa que o Digimon já foi colocado: some a dica.</summary>
    public static void Posicionado()
    {
        if (_instancia != null && _instancia._dica != null) _instancia._dica.SetActive(false);
    }

    private void OnDestroy()
    {
        if (_instancia == this) _instancia = null;
        _aberta = false;
    }

    private void Montar(bool abrirJanela)
    {
        var raiz = FigmaUI.Tela(transform, "UI_InstrucoesAR", 500);

        // Dica fixa no alto (aparece quando a janela fecha).
        _dica = new GameObject("Dica", typeof(RectTransform));
        _dica.transform.SetParent(raiz, false);
        FigmaUI.Esticar((RectTransform)_dica.transform);
        FigmaUI.PainelAzul(FigmaUI.Ret(_dica.transform, "Fundo", 90f, 330f, 849f, 120f));
        var textoDica = FigmaUI.TextoEm(_dica.transform, 110f, 330f, 809f, 120f, "Move your phone slowly, then double-tap the ground", 34f, FigmaUI.Peso.Normal);
        textoDica.textWrappingMode = TextWrappingModes.Normal;
        _dica.SetActive(!abrirJanela);

        if (!abrirJanela) return;
        _aberta = true;

        _janela = new GameObject("Janela", typeof(RectTransform));
        _janela.transform.SetParent(raiz, false);
        FigmaUI.Esticar((RectTransform)_janela.transform);
        // Escurece a câmera atrás e bloqueia os toques.
        var veu = FigmaUI.Ret(_janela.transform, "Veu", 0, 0, 1, 1);
        FigmaUI.TelaCheia(veu);
        FigmaUI.Imagem(veu, null, new Color(0f, 0f, 0f, 0.6f)).raycastTarget = true;

        // Painel no padrão das outras telas (azul com textura de pixels e borda branca).
        FigmaUI.PainelAzul(FigmaUI.Ret(_janela.transform, "Painel", 60f, 330f, 909f, 1500f));
        FigmaUI.TextoEm(_janela.transform, 60f, 380f, 909f, 100f, "Call your Digimon", 60f, FigmaUI.Peso.Negrito);

        // Passo 1: mexer a câmera.
        Passo(1, 520f, "Move your phone slowly, pointing at the ground, until the camera recognizes the area.");
        var area1 = FigmaUI.Ret(_janela.transform, "Area1", 60f, 640f, 909f, 330f);
        _celular = Celular(area1);
        Seta(area1, -1);
        Seta(area1, 1);

        // Passo 2: tocar duas vezes.
        Passo(2, 1010f, "Double-tap the ground where you want your Digimon to appear.");
        var area2 = FigmaUI.Ret(_janela.transform, "Area2", 60f, 1130f, 909f, 400f);
        Chao(area2);
        _anel1 = Anel(area2, out _imgAnel1);
        _anel2 = Anel(area2, out _imgAnel2);
        _dedo = Dedo(area2);

        FigmaUI.BotaoBack(_janela.transform, (FigmaUI.Largura - 395f) / 2f, 1650f, Fechar, "Got it");
        StartCoroutine(Animar());
    }

    private void Passo(int numero, float y, string texto)
    {
        var bola = FigmaUI.Imagem(FigmaUI.Ret(_janela.transform, "Numero" + numero, 110f, y, 80f, 80f), Formas.Circulo(), FigmaUI.AzulBarraFundo);
        bola.raycastTarget = false;
        FigmaUI.TextoEm(bola.transform, 0f, 0f, 80f, 80f, numero.ToString(), 46f, FigmaUI.Peso.Negrito);
        Esticar(bola.transform.GetChild(0));
        var t = FigmaUI.TextoEm(_janela.transform, 220f, y - 15f, 700f, 115f, texto, 36f, FigmaUI.Peso.Normal, TextAlignmentOptions.MidlineLeft);
        t.textWrappingMode = TextWrappingModes.Normal;
    }

    private static void Esticar(Transform t) => FigmaUI.Esticar((RectTransform)t);

    /// <summary>Celular desenhado: corpo escuro arredondado com a tela azul e um pontinho de chão.</summary>
    private static RectTransform Celular(RectTransform area)
    {
        var corpo = Centro(area, "Celular", 150f, 270f);
        var img = FigmaUI.Imagem(corpo, FigmaUI.Arredondado(), new Color(0.08f, 0.1f, 0.14f));
        img.type = Image.Type.Sliced;
        var tela = FigmaUI.Local(corpo, "Tela", 12f, 22f, 126f, 226f);
        var imgTela = FigmaUI.Imagem(tela, FigmaUI.Arredondado(), new Color(0.47f, 0.97f, 0.6f));
        imgTela.type = Image.Type.Sliced;
        // Grade de pontos na tela (o chão sendo reconhecido).
        for (var i = 0; i < 3; i++)
        for (var j = 0; j < 2; j++)
            FigmaUI.Imagem(FigmaUI.Local(tela, "Ponto", 28f + j * 50f, 120f + i * 30f, 18f, 18f), Formas.Circulo(), new Color(0.03f, 0.55f, 0.5f));
        return corpo;
    }

    private static void Seta(RectTransform area, int lado)
    {
        var t = FigmaUI.Texto(area, lado < 0 ? "‹" : "›", 120f, FigmaUI.Peso.Negrito, TextAlignmentOptions.Center);
        var rt = t.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(100f, 160f);
        rt.anchoredPosition = new Vector2(lado * 260f, 0f);
        t.color = new Color(1f, 1f, 1f, 0.8f);
    }

    /// <summary>Faixa de chão em perspectiva onde o dedo toca.</summary>
    private static void Chao(RectTransform area)
    {
        var chao = Centro(area, "Chao", 520f, 120f);
        chao.anchoredPosition = new Vector2(0f, -110f);
        var img = FigmaUI.Imagem(chao, Formas.Circulo(), new Color(0.47f, 0.97f, 0.6f, 0.55f));
        img.raycastTarget = false;
    }

    private static RectTransform Anel(RectTransform area, out Image img)
    {
        var anel = Centro(area, "Anel", 120f, 50f);
        anel.anchoredPosition = new Vector2(0f, -110f);
        img = FigmaUI.Imagem(anel, Formas.Anel(), new Color(1f, 1f, 1f, 0f));
        return anel;
    }

    /// <summary>Dedo apontando para baixo; o pivô fica na ponta, que encosta no chão.</summary>
    private static RectTransform Dedo(RectTransform area)
    {
        var dedo = Centro(area, "Dedo", 70f, 200f);
        dedo.pivot = new Vector2(0.5f, 0f);
        var haste = FigmaUI.Local(dedo, "Haste", 0f, 0f, 70f, 200f);
        FigmaUI.Imagem(haste, FigmaUI.Arredondado(), new Color(1f, 0.86f, 0.74f)).type = Image.Type.Sliced;
        var unha = FigmaUI.Local(dedo, "Unha", 14f, 140f, 42f, 50f);
        FigmaUI.Imagem(unha, FigmaUI.Arredondado(), new Color(1f, 0.95f, 0.92f)).type = Image.Type.Sliced;
        return dedo;
    }

    private static RectTransform Centro(RectTransform pai, string nome, float w, float h)
    {
        var rt = new GameObject(nome, typeof(RectTransform)).GetComponent<RectTransform>();
        rt.SetParent(pai, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(w, h);
        rt.anchoredPosition = Vector2.zero;
        return rt;
    }

    private IEnumerator Animar()
    {
        while (_janela != null && _janela.activeSelf)
        {
            var t = Time.unscaledTime;
            // Celular indo de um lado para o outro e inclinando de leve.
            _celular.anchoredPosition = new Vector2(Mathf.Sin(t * 1.6f) * 150f, 0f);
            _celular.localRotation = Quaternion.Euler(0f, 0f, -Mathf.Sin(t * 1.6f) * 10f);

            // Dedo: dois toques rápidos e uma pausa (ciclo de 1,6 s).
            var c = t % 1.6f;
            var desce = Toque(c, 0f) + Toque(c, 0.35f);
            _dedo.anchoredPosition = new Vector2(0f, -110f + (1f - desce) * 55f);
            Pulso(_anel1, _imgAnel1, c, 0.15f);
            Pulso(_anel2, _imgAnel2, c, 0.5f);
            yield return null;
        }
    }

    /// <summary>0 a 1: o dedo encostando no chão perto do instante "inicio".</summary>
    private static float Toque(float c, float inicio)
    {
        var x = (c - inicio) / 0.3f;
        return x < 0f || x > 1f ? 0f : Mathf.Sin(x * Mathf.PI);
    }

    /// <summary>Anel que se espalha no chão logo depois de cada toque.</summary>
    private static void Pulso(RectTransform anel, Image img, float c, float inicio)
    {
        var x = (c - inicio) / 0.6f;
        if (x < 0f || x > 1f) { img.color = new Color(1f, 1f, 1f, 0f); return; }
        anel.localScale = Vector3.one * Mathf.Lerp(0.5f, 2.2f, x);
        img.color = new Color(1f, 1f, 1f, 1f - x);
    }

    public void Fechar()
    {
        _aberta = false;
        _fechouEm = Time.unscaledTime;
        if (_janela != null) _janela.SetActive(false);
        if (_dica != null) _dica.SetActive(true);
        var p = PerfilJogador.Atual;
        if (!p.instrucoesVistas.Contains(_chave))
        {
            p.instrucoesVistas.Add(_chave);
            p.Salvar();
        }
    }
}
