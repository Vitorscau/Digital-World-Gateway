using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Aplica o resultado de uma batalha no perfil do jogador.
/// Vitória: XP, bits, itens (Meat e Bandage) e dados de Scan da espécie enfrentada.
/// Derrota: um pouco de XP e de dados de Scan.
/// O Digimon NÃO entra direto na coleção: com 100% dos dados, ele é baixado na tela Scan.
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
        public int ScanAntes, ScanDepois;
        public readonly List<string> Itens = new List<string>();
        public int NivelParceiroNovo, NivelJogadorNovo; // > 0 se subiu de nível

        public bool ScanCompleto => ScanDepois >= 100;

        public string LinhaScan =>
            ScanCompleto
                ? $"Dados de {NomeInimigo} completos! Abra o Scan para baixar."
                : $"Dados de {NomeInimigo}: {ScanAntes}% → {ScanDepois}%";

        /// <summary>Texto completo (usado pela Battle_Scene, que não tem o painel desenhado).</summary>
        public string Texto =>
            (Venceu ? "<size=90><b>VITÓRIA!</b></size>\n\n" : $"<size=90><b>DERROTA</b></size>\n\n{NomeInimigo} fugiu.\n\n") +
            LinhaScan + "\n\n" +
            $"{NomeParceiro}: +{XpDigimon} EXP" + (NivelParceiroNovo > 0 ? $"  <color=#FFE066>nível {NivelParceiroNovo}!</color>" : "") + "\n" +
            $"Tamer: +{XpJogador} EXP" + (NivelJogadorNovo > 0 ? $"  <color=#FFE066>nível {NivelJogadorNovo}!</color>" : "") +
            (Bits > 0 ? $"\n+{Bits} BIT" : "") +
            (Itens.Count > 0 ? "\n" + string.Join("  ", Itens.ConvertAll(i => "+1 " + NomeDoItem(i))) : "");

        private static string NomeDoItem(string id) => global::Itens.Buscar(id)?.Nome ?? id;
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
            NomeInimigo = encontro.FichaInimigo != null ? encontro.FichaInimigo.Name.Trim() : encontro.EspecieInimigo,
            ScanAntes = perfil.Scan(encontro.EspecieInimigo)
        };

        if (venceu)
        {
            r.XpDigimon = 10 + nivelInimigo * 6;
            r.XpJogador = 50 + nivelInimigo * 10;
            r.Bits = 10 + nivelInimigo * 2;
            perfil.bits += r.Bits;
            perfil.vitorias++;
            // Os itens que aparecem no popup de recompensa da arena.
            perfil.AdicionarItem(Itens.Bandage);
            perfil.AdicionarItem(Itens.Meat);
            r.Itens.Add(Itens.Bandage);
            r.Itens.Add(Itens.Meat);
            r.ScanDepois = perfil.AdicionarScan(encontro.EspecieInimigo, 20 + nivelInimigo * 2);
            Missoes.RegistrarVitoria(encontro.FichaInimigo);
        }
        else
        {
            r.XpDigimon = 5 + nivelInimigo;
            r.XpJogador = 10;
            perfil.derrotas++;
            r.ScanDepois = perfil.AdicionarScan(encontro.EspecieInimigo, 5);
        }

        if (PerfilJogador.GanharXp(parceiro, r.XpDigimon) > 0 && parceiro != null) r.NivelParceiroNovo = parceiro.nivel;
        if (perfil.GanharXp(r.XpJogador) > 0) r.NivelJogadorNovo = perfil.nivel;
        CuidadoDigimon.DepoisDaBatalha(parceiro, venceu); // cansa, dá fome e mexe no humor
        perfil.bandagemAtiva = false; // a bandagem vale para uma batalha
        perfil.Salvar();
        return r;
    }
}
