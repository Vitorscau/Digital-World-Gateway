using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Dados da batalha que vai começar: quem é o parceiro do jogador e quem é o Digimon selvagem.
/// Preenchido no mapa ao tocar num Digimon e lido pela cena de batalha.
/// </summary>
public class Encontro
{
    public DigimonSalvo Parceiro;
    public DigimonBase FichaParceiro;
    public DigimonBase FichaInimigo;
    public string EspecieInimigo;
    public int NivelInimigo;
    public HabitatDigimon Habitat;
    /// <summary>Modelo usado no mapa (tem os materiais certos, ex.: Tsukaimon roxo).</summary>
    public GameObject ModeloInimigo;

    /// <summary>A batalha atual (null se a cena de batalha foi aberta direto, para testes).</summary>
    public static Encontro Atual { get; private set; }

    /// <summary>
    /// true (padrão): a batalha acontece em AR (AR_Battle_Scene), como no jogo original.
    /// false: usa a Battle_Scene comum, sem câmera (útil para testar rápido).
    /// </summary>
    public static bool UsarAR = true;

    public static void Limpar() => Atual = null;

    /// <summary>Começa uma batalha contra um Digimon selvagem do mapa.</summary>
    public static bool Iniciar(WildDigimon selvagem, DigimonBase fichaInimigo, GameObject modelo)
    {
        var perfil = PerfilJogador.Atual;
        var parceiro = perfil.Parceiro;
        var fichaParceiro = parceiro?.Ficha;
        if (fichaParceiro == null)
        {
            AvisoNaTela.Mostrar("Seu parceiro não foi encontrado no catálogo de Digimons.");
            return false;
        }
        if (fichaInimigo == null)
        {
            AvisoNaTela.Mostrar($"{selvagem.Especie} ainda não tem ficha de batalha (Resources/Digimons).");
            return false;
        }

        Atual = new Encontro
        {
            Parceiro = parceiro,
            FichaParceiro = fichaParceiro,
            FichaInimigo = fichaInimigo,
            EspecieInimigo = selvagem.Especie,
            NivelInimigo = selvagem.Nivel,
            Habitat = selvagem.Habitat,
            ModeloInimigo = modelo
        };

        SceneManager.LoadScene(UsarAR ? Cenas.BatalhaAR : Cenas.Batalha);
        return true;
    }

    /// <summary>Encontro de teste (abrir a cena de batalha direto no Editor).</summary>
    public static Encontro Teste()
    {
        var perfil = PerfilJogador.Atual;
        return new Encontro
        {
            Parceiro = perfil.Parceiro,
            FichaParceiro = perfil.Parceiro?.Ficha,
            FichaInimigo = CatalogoDigimon.Buscar("Agumon_Black"),
            EspecieInimigo = "Agumon_Black",
            NivelInimigo = 5,
            Habitat = HabitatDigimon.Urbano
        };
    }
}
