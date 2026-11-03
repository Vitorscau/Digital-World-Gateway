using UnityEngine;

/// <summary>
/// Base das telas desenhadas no Figma (Profile, Storage, Bag, Scan, Level Up, Settings).
/// Cada tela é montada por código ao abrir e destruída ao fechar. Só uma fica aberta por vez.
/// </summary>
public abstract class TelaFigma : MonoBehaviour
{
    private static TelaFigma _aberta;

    /// <summary>Raiz da tela, na escala do Figma (1029 x 2228).</summary>
    protected RectTransform Raiz { get; private set; }

    public static bool AlgumaAberta => _aberta != null;

    public static T Abrir<T>() where T : TelaFigma
    {
        if (_aberta != null) _aberta.Fechar();
        var go = new GameObject(typeof(T).Name);
        var tela = go.AddComponent<T>();
        _aberta = tela;
        tela.Raiz = FigmaUI.Tela(go.transform, "Canvas", 200);
        tela.Construir();
        PerfilJogador.Mudou += tela.AoMudarPerfil;
        return tela;
    }

    /// <summary>Monta a tela (chamado uma vez ao abrir).</summary>
    protected abstract void Construir();

    /// <summary>Chamado quando o perfil muda (ex.: usou um item). Atualize os dados mostrados.</summary>
    protected virtual void AoMudarPerfil() { }

    /// <summary>Fecha a tela (as telas podem animar a saída antes de chamar a base).</summary>
    public virtual void Fechar() => Destroy(gameObject);

    protected virtual void OnDestroy()
    {
        PerfilJogador.Mudou -= AoMudarPerfil;
        if (_aberta == this) _aberta = null;
    }

    /// <summary>Fundo azul + moldura branca de tela cheia + título (padrão de Storage e Bag).</summary>
    protected void MontarMolduraComTitulo(string titulo)
    {
        FigmaUI.Fundo(Raiz);
        FigmaUI.Imagem(FigmaUI.Ret(Raiz, "Moldura", 24f, 23f, 991f, 2222f), FigmaUI.S("moldura_tela"));
        FigmaUI.TextoEm(Raiz, 0, 105f, FigmaUI.Largura, 42f, titulo, 48f, FigmaUI.Peso.Negrito, sombra: false);
        FigmaUI.Divisor(Raiz, 183f);
    }
}
