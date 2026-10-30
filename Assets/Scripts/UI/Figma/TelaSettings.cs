using TMPro;
using UnityEngine;

/// <summary>
/// Tela "Settings" (botão de engrenagem da HUD). Não há tela desenhada no Figma para ela,
/// então segue o mesmo padrão de Storage e Bag: som, nome do Tamer e recomeçar o progresso.
/// </summary>
public class TelaSettings : TelaFigma
{
    private TMP_Text _rotuloSom, _rotuloReset;
    private bool _confirmarReset;

    /// <summary>Aplica o volume salvo (chamado ao abrir o mapa).</summary>
    public static void AplicarSom() => AudioListener.volume = PerfilJogador.Atual.somLigado ? 1f : 0f;

    protected override void Construir()
    {
        MontarMolduraComTitulo("Settings");

        FigmaUI.TextoEm(Raiz, 0, 260f, FigmaUI.Largura, 58f, "Tamer", 48f, FigmaUI.Peso.Negrito);
        var campo = TelaStorage.CampoBusca(Raiz, 340f, null);
        campo.placeholder.GetComponent<TMP_Text>().text = "Nome do Tamer";
        campo.text = PerfilJogador.Atual.nome;
        campo.characterLimit = 16;
        campo.onEndEdit.AddListener(v =>
        {
            v = v.Trim();
            if (string.IsNullOrEmpty(v)) return;
            PerfilJogador.Atual.nome = v;
            PerfilJogador.Atual.Salvar();
        });
        FigmaUI.Divisor(Raiz, 480f);

        _rotuloSom = BotaoComRotulo(560f, AlternarSom);
        _rotuloReset = BotaoComRotulo(740f, Resetar);
        FigmaUI.TextoEm(Raiz, 87f, 900f, 855f, 120f, "O progresso fica salvo neste aparelho.", 32f, FigmaUI.Peso.Normal)
            .color = new Color(1, 1, 1, 0.75f);

        FigmaUI.BotaoBack(Raiz, (FigmaUI.Largura - 395f) / 2f, 2061.5f, Fechar);
        AtualizarRotulos();
    }

    private TMP_Text BotaoComRotulo(float y, System.Action aoTocar) =>
        FigmaUI.BotaoLargo(Raiz, 87f, y, "", aoTocar).GetComponentInChildren<TMP_Text>();

    private void AtualizarRotulos()
    {
        _rotuloSom.text = PerfilJogador.Atual.somLigado ? "Sound: On" : "Sound: Off";
        _rotuloReset.text = _confirmarReset ? "Tem certeza? Toque de novo para apagar tudo" : "Reset progress";
    }

    private void AlternarSom()
    {
        var p = PerfilJogador.Atual;
        p.somLigado = !p.somLigado;
        p.Salvar();
        AplicarSom();
        AtualizarRotulos();
    }

    private void Resetar()
    {
        if (!_confirmarReset)
        {
            _confirmarReset = true;
            AtualizarRotulos();
            return;
        }
        PerfilJogador.Resetar();
        PlayerPrefs.DeleteKey("DigimonNexus.SpawnsVistos");
        Fechar();
        // Jogo novo: começa de novo pela escolha do Digitama.
        UnityEngine.SceneManagement.SceneManager.LoadScene(Cenas.Digitama);
    }
}
