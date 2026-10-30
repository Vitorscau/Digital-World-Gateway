/// <summary>
/// Nomes das cenas do jogo, num lugar só. Use sempre estas constantes em vez de digitar o nome
/// (um nome errado, como "BattleScene", só dá erro quando o jogo tenta abrir a cena).
/// </summary>
public static class Cenas
{
    public const string MenuPrincipal = "Main_Menu_Scene";
    public const string Mapa = "Map_Scene";
    public const string AR = "AR_Scene";
    public const string Batalha = "Battle_Scene";
    public const string BatalhaAR = "AR_Battle_Scene";
    public const string Cuidado = "Care_Scene";
    public const string Digitama = "Digitama_Scene";

    /// <summary>Primeira cena do jogo: a escolha do Digitama num jogo novo, senão o mapa.</summary>
    public static string Inicio => PerfilJogador.Atual.inicioFeito ? Mapa : Digitama;
}
