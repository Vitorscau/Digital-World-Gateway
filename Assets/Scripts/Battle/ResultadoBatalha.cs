using UnityEngine;

/// <summary>
/// Aplica o resultado de uma batalha no perfil do jogador (XP, bits, captura, missões).
/// Usado pela arena do AR e pela Battle_Scene.
/// </summary>
public static class ResultadoBatalha
{
    public class Resumo
    {
        public bool Venceu;
        public int XpJogador, XpDigimon, Bits;
        public string NomeParceiro, NomeInimigo;
        public int NivelInimigo;
        public bool Capturado;
        public int NivelParceiroNovo, NivelJogadorNovo; // > 0 se subiu de nível

        /// <summary>Texto completo (usado pela Battle_Scene, que não tem o painel desenhado).</summary>
        public string Texto =>
            Venceu
                ? $"<size=90><b>VITÓRIA!</b></size>\n\n" +
                  $"{NomeInimigo} (nv. {NivelInimigo}) entrou para a sua coleção!\n\n" +
                  $"{NomeParceiro}: +{XpDigimon} XP" + (NivelParceiroNovo > 0 ? $"  <color=#FFE066>subiu para o nível {NivelParceiroNovo}!</color>" : "") + "\n" +
                  $"Você: +{XpJogador} XP" + (NivelJogadorNovo > 0 ? $"  <color=#FFE066>nível {NivelJogadorNovo}!</color>" : "") + "\n" +
                  $"+{Bits} bits"
                : $"<size=90><b>DERROTA</b></size>\n\n{NomeInimigo} fugiu.\n\n{NomeParceiro}: +{XpDigimon} XP";
    }

    public static Resumo Aplicar(Encontro encontro, bool venceu)
    {
        var perfil = PerfilJogador.Atual;
        var nivelInimigo = Mathf.Max(1, encontro.NivelInimigo);
        var parceiro = encontro.Parceiro;
        var r = new Resumo
        {
            Venceu = venceu,
            NivelInimigo = nivelInimigo,
            NomeParceiro = encontro.FichaParceiro != null ? encontro.FichaParceiro.Name.Trim() : "Parceiro",
            NomeInimigo = encontro.FichaInimigo != null ? encontro.FichaInimigo.Name.Trim() : encontro.EspecieInimigo
        };

        if (venceu)
        {
            r.XpDigimon = 10 + nivelInimigo * 6;
            r.XpJogador = 50 + nivelInimigo * 10;
            r.Bits = 10 + nivelInimigo * 2;
            if (PerfilJogador.GanharXp(parceiro, r.XpDigimon) > 0 && parceiro != null) r.NivelParceiroNovo = parceiro.nivel;
            if (perfil.GanharXp(r.XpJogador) > 0) r.NivelJogadorNovo = perfil.nivel;
            perfil.bits += r.Bits;
            perfil.vitorias++;
            perfil.AdicionarNaColecao(encontro.EspecieInimigo, nivelInimigo);
            r.Capturado = true;
            perfil.Salvar();
            Missoes.RegistrarVitoria(encontro.FichaInimigo);
            Missoes.RegistrarCaptura();
        }
        else
        {
            r.XpDigimon = 5 + nivelInimigo;
            r.XpJogador = 10;
            if (PerfilJogador.GanharXp(parceiro, r.XpDigimon) > 0 && parceiro != null) r.NivelParceiroNovo = parceiro.nivel;
            if (perfil.GanharXp(r.XpJogador) > 0) r.NivelJogadorNovo = perfil.nivel;
            perfil.derrotas++;
            perfil.Salvar();
        }
        return r;
    }
}
