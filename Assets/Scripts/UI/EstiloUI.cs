using TMPro;
using UnityEngine;

/// <summary>
/// Padrão visual das telas do jogo (fontes, sprites e cores), num asset só:
/// Resources/UI/EstiloUI. As telas criadas por código (Digivice, avisos, batalha) usam este padrão
/// para ficarem iguais às telas desenhadas (Missões, "Stay Aware", HUD).
/// Para recriar: Tools > Digimon Nexus > Criar estilo de UI.
/// </summary>
[CreateAssetMenu(menuName = "Digimon Nexus/Estilo de UI", fileName = "EstiloUI")]
public class EstiloUI : ScriptableObject
{
    [Header("Fontes (Lato, as mesmas das telas)")]
    public TMP_FontAsset FonteNegrito;
    public TMP_FontAsset FonteNormal;
    public TMP_FontAsset FontePesada;

    [Header("Mapa (azul)")]
    [Tooltip("Fundo azul com textura de pixels (Stay/Rectangle 59).")]
    public Sprite Fundo;
    [Tooltip("Moldura branca de cantos cortados, por cima do fundo.")]
    public Sprite Moldura;
    [Tooltip("Botão octogonal azul com borda branca (como 'Daily').")]
    public Sprite Botao;
    [Tooltip("Botão selecionado (azul escuro, como 'Daily' clicado).")]
    public Sprite BotaoSelecionado;
    [Tooltip("Cartão de lista (como o cartão de missão).")]
    public Sprite Cartao;
    [Tooltip("Botão da HUD com o ícone do Digivice.")]
    public Sprite IconeDigivice;

    [Header("Batalha (verde-água)")]
    public Sprite BotaoBatalha;
    public Sprite BotaoOkBatalha;

    [Header("Cores")]
    public Color Azul = new Color32(0x00, 0xA8, 0xF3, 0xFF);
    public Color AzulSelecionado = new Color32(0x2E, 0x74, 0xCA, 0xFF);
    public Color AzulCartao = new Color32(0x00, 0xB6, 0xFF, 0xFF);
    public Color VerdeBatalha = new Color32(0x00, 0xCF, 0xCC, 0xFF);
    public Color Destaque = new Color32(0xFF, 0xE0, 0x66, 0xFF);
    public Color TextoClaro = Color.white;

    private static EstiloUI _atual;

    /// <summary>O estilo do projeto (ou um padrão com as mesmas cores, se o asset não existir).</summary>
    public static EstiloUI Atual
    {
        get
        {
            if (_atual == null) _atual = Resources.Load<EstiloUI>("UI/EstiloUI");
            if (_atual == null) _atual = CreateInstance<EstiloUI>();
            return _atual;
        }
    }
}
