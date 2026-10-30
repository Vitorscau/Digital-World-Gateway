using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Digivolução no estilo Digimon World, seguindo as árvores oficiais da franquia.
///
/// - As linhas vêm de Resources/Digivolucao/arvore.txt ("Origem -> Destino1, Destino2..."), montado a partir
///   das listas "Evolves To" do Wikimon, só com os Digimon que existem no jogo. O primeiro destino é a linha
///   principal (anime / V-Pet). O arquivo pode ser editado à mão.
/// - Fresh -> In-Training -> Rookie -> Champion -> Ultimate -> Mega, por nível (bebês também pela idade).
/// - Bebês seguem a linha principal, a menos que o Tamer treine o status de outra opção.
/// - Do Rookie em diante, cada caminho pede treino no status que combina com o Digimon de destino
///   (o status mais forte dele), amizade, vitórias e poucos erros de cuidado; vence o caminho que cumprir
///   a maioria das condições (como no Digimon World 1).
/// - Formas de "mau cuidado" (Numemon, Sukamon, SkullGreymon...) só saem com muitos erros de cuidado.
///   Se nenhum caminho de Champion for cumprido, vira Numemon (ou Sukamon, com sujeira e erros demais).
/// - Quem não tiver linha no arquivo digivolve para um Digimon do próximo estágio com o mesmo elemento.
/// </summary>
public static class Digivolucao
{
    private enum Tipo { Livre, Bom, Ruim }

    private class Caminho
    {
        public string Para;
        public Tipo Tipo;
        public StatusTreino? Foco;
        public int Ordem; // 0 = linha principal
    }

    /// <summary>Formas que aparecem por descuido (os Digimon "errados" dos V-Pets e do Digimon World).</summary>
    private static readonly HashSet<string> Ruins = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Numemon", "Sukamon", "Nanimon", "Geremon", "PlatinumSukamon", "GoldNumemon", "SkullGreymon",
        "BlackKingNumemon", "ShellNumemon"
    };

    // ------------------------------------------------------------ Árvore (arquivo)

    private static Dictionary<string, List<string>> _arvore;

    private static Dictionary<string, List<string>> Arvore
    {
        get
        {
            if (_arvore != null) return _arvore;
            _arvore = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            var texto = Resources.Load<TextAsset>("Digivolucao/arvore");
            if (texto == null) return _arvore;
            foreach (var bruta in texto.text.Split('\n'))
            {
                var linha = bruta.Trim();
                if (linha.Length == 0 || linha.StartsWith("#")) continue;
                var partes = linha.Split(new[] { "->" }, 2, StringSplitOptions.None);
                if (partes.Length < 2) continue;
                var destinos = partes[1].Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
                _arvore[CatalogoDigimon.Normalizar(partes[0])] = destinos;
            }
            return _arvore;
        }
    }

    /// <summary>Os destinos oficiais de uma espécie (para mostrar numa tela de evolução, por exemplo).</summary>
    public static IReadOnlyList<string> DestinosOficiais(DigimonBase ficha) =>
        ficha != null && Arvore.TryGetValue(CatalogoDigimon.Normalizar(ficha.name), out var l) ? l : (IReadOnlyList<string>)Array.Empty<string>();

    // ------------------------------------------------------------ Regras

    /// <summary>Nível que o Digimon precisa ter para sair do estágio atual (0 = não digivolve mais).</summary>
    public static int NivelParaDigivolver(DigimonStage atual)
    {
        switch (atual)
        {
            case DigimonStage.Fresh: return 3;
            case DigimonStage.InTraining: return 6;
            case DigimonStage.Rookie: return 14;
            case DigimonStage.Champion:
            case DigimonStage.Armor: return 26;
            case DigimonStage.Ultimate: return 40;
            default: return 0;
        }
    }

    private static DigimonStage? Proximo(DigimonStage e)
    {
        switch (e)
        {
            case DigimonStage.Fresh: return DigimonStage.InTraining;
            case DigimonStage.InTraining: return DigimonStage.Rookie;
            case DigimonStage.Rookie: return DigimonStage.Champion;
            case DigimonStage.Champion:
            case DigimonStage.Armor: return DigimonStage.Ultimate;
            case DigimonStage.Ultimate: return DigimonStage.Mega;
            default: return null;
        }
    }

    private static bool MesmoNivel(DigimonStage a, DigimonStage b) =>
        a == b || (a == DigimonStage.Armor && b == DigimonStage.Champion) || (a == DigimonStage.Champion && b == DigimonStage.Armor);

    // Exigências dos caminhos "bons", pelo estágio de destino.
    private static int TreinoMin(DigimonStage e) => e <= DigimonStage.Champion || e == DigimonStage.Armor ? 15 : e == DigimonStage.Ultimate ? 35 : 60;
    private static int AmizadeMin(DigimonStage e) => e <= DigimonStage.Champion || e == DigimonStage.Armor ? 20 : e == DigimonStage.Ultimate ? 45 : 75;
    private static int VitoriasMin(DigimonStage e) => e <= DigimonStage.Champion || e == DigimonStage.Armor ? 3 : e == DigimonStage.Ultimate ? 10 : 25;
    private static int ErrosMax(DigimonStage e) => e <= DigimonStage.Champion || e == DigimonStage.Armor ? 3 : e == DigimonStage.Ultimate ? 5 : 8;
    private const int ErrosParaRuim = 5;

    /// <summary>Hora de digivolver? Nível (ou idade, nos bebês).</summary>
    public static bool ChegouAHora(DigimonSalvo d)
    {
        var ficha = d?.Ficha;
        if (ficha == null) return false;
        var nivel = NivelParaDigivolver(ficha.Stage);
        if (nivel <= 0) return false;
        if (d.nivel >= nivel) return true;
        // Bebês crescem com o tempo, como num V-Pet.
        var idadeMin = (DateTimeOffset.UtcNow.ToUnixTimeSeconds() - (d.nasceuEm > 0 ? d.nasceuEm : d.capturadoEm)) / 60f;
        return ficha.Stage == DigimonStage.Fresh && idadeMin >= 15f ||
               ficha.Stage == DigimonStage.InTraining && idadeMin >= 120f;
    }

    /// <summary>Para quem o Digimon digivolve agora, ou null se ainda não é hora / nenhum caminho foi cumprido.</summary>
    public static DigimonBase Verificar(DigimonSalvo d)
    {
        if (!ChegouAHora(d)) return null;
        return Escolher(d, out _);
    }

    private static DigimonBase Escolher(DigimonSalvo d, out string motivo)
    {
        motivo = null;
        var ficha = d.Ficha;
        var alvo = Proximo(ficha.Stage);
        if (alvo == null) return null;

        DigimonBase melhor = null;
        var melhorNota = -1f;
        foreach (var c in Caminhos(ficha, alvo.Value))
        {
            var destino = CatalogoDigimon.Buscar(c.Para);
            if (destino == null || destino.Prefab == null || !MesmoNivel(destino.Stage, alvo.Value)) continue;
            var nota = Nota(d, c, destino.Stage);
            if (nota > melhorNota) { melhorNota = nota; melhor = destino; }
        }

        if (melhor != null && melhorNota >= 0.66f) return melhor;

        if (alvo == DigimonStage.Champion)
        {
            var falha = d.errosCuidado >= 8 ? CatalogoDigimon.Buscar("Sukamon") : CatalogoDigimon.Buscar("Numemon");
            if (falha != null) return falha;
        }
        motivo = "precisa de mais treino, amizade ou vitórias";
        return null;
    }

    private static float Nota(DigimonSalvo d, Caminho c, DigimonStage destino)
    {
        var desempate = -c.Ordem * 0.001f; // na dúvida, a linha principal
        var principal = MaisTreinado(d, out var maiorTreino);
        // Caminho alternativo "especializado": o status dele é o mais treinado do parceiro.
        var especializado = c.Ordem > 0 && c.Foco.HasValue && c.Foco.Value == principal;
        var treinoFoco = c.Foco.HasValue ? CuidadoDigimon.Treino(d, c.Foco.Value) : 0;
        switch (c.Tipo)
        {
            case Tipo.Livre:
                // Bebês: seguem a linha principal; treinar bastante o status de outra opção muda o caminho.
                if (c.Ordem == 0) return 0.7f + 0.01f;
                return especializado && treinoFoco >= 10 ? 0.7f + 0.01f + treinoFoco / 1000f + desempate : 0.7f + desempate;
            case Tipo.Ruim:
                return d.errosCuidado >= ErrosParaRuim ? 0.8f + desempate : 0f;
            default:
                // Maltratado demais: nenhum caminho "bom" (vira a forma de descuido ou Numemon/Sukamon).
                if (d.errosCuidado > ErrosMax(destino) + 2) return 0f;
                // Condições do Digimon World: amizade, vitórias, poucos erros e treino.
                var ok = 0;
                if (d.amizade >= AmizadeMin(destino)) ok++;
                if (d.vitorias >= VitoriasMin(destino)) ok++;
                if (d.errosCuidado <= ErrosMax(destino)) ok++;
                // Linha principal: qualquer treino serve. Alternativas: o status delas tem que ser o mais treinado.
                if (c.Ordem == 0 ? maiorTreino >= TreinoMin(destino) : especializado && treinoFoco >= TreinoMin(destino)) ok++;
                var nota = ok / 4f + desempate;
                // Quem se especializou muito num status desvia da linha principal para a alternativa dele.
                if (especializado && treinoFoco >= TreinoMin(destino) * 2) nota += 0.01f;
                return nota;
        }
    }

    /// <summary>Caminhos: os da árvore oficial; sem linha, os do próximo estágio com o mesmo elemento.</summary>
    private static IEnumerable<Caminho> Caminhos(DigimonBase ficha, DigimonStage alvo)
    {
        var bebe = alvo <= DigimonStage.Rookie;
        if (Arvore.TryGetValue(CatalogoDigimon.Normalizar(ficha.name), out var lista) && lista.Count > 0)
        {
            // Cada alternativa fica com um status diferente (o que mais combina com ela que ainda estiver livre),
            // para que treinar qualquer status leve a algum desvio da linha principal.
            var usados = new HashSet<StatusTreino>();
            var caminhos = new List<Caminho>();
            for (var i = 0; i < lista.Count; i++)
            {
                var nome = lista[i];
                var ruim = Ruins.Contains(nome.Replace(" ", "")) && !bebe;
                StatusTreino? foco = null;
                if (i > 0 && !ruim)
                {
                    var preferido = FocoDe(CatalogoDigimon.Buscar(nome)) ?? StatusTreino.Offense;
                    foco = preferido;
                    if (usados.Contains(preferido))
                        foreach (StatusTreino s in Enum.GetValues(typeof(StatusTreino)))
                            if (!usados.Contains(s)) { foco = s; break; }
                    usados.Add(foco.Value);
                }
                caminhos.Add(new Caminho { Para = nome, Ordem = i, Tipo = ruim ? Tipo.Ruim : bebe ? Tipo.Livre : Tipo.Bom, Foco = foco });
            }
            return caminhos;
        }

        var mesmos = CatalogoDigimon.Todos.Distinct()
            .Where(f => f != null && f.Prefab != null && MesmoNivel(f.Stage, alvo) && f.Atribute == ficha.Atribute && !Ruins.Contains(f.name))
            .OrderBy(f => f.name, StringComparer.Ordinal).ToList();
        if (mesmos.Count == 0)
            mesmos = CatalogoDigimon.Todos.Distinct().Where(f => f != null && f.Prefab != null && MesmoNivel(f.Stage, alvo) && !Ruins.Contains(f.name))
                .OrderBy(f => f.name, StringComparer.Ordinal).ToList();
        if (mesmos.Count == 0) return Array.Empty<Caminho>();
        var inicio = (int)((uint)ficha.name.GetHashCode() % (uint)mesmos.Count);
        return Enumerable.Range(0, Mathf.Min(3, mesmos.Count)).Select(i =>
        {
            var f = mesmos[(inicio + i) % mesmos.Count];
            return new Caminho { Para = f.name, Ordem = i, Tipo = bebe ? Tipo.Livre : Tipo.Bom, Foco = FocoDe(f) };
        });
    }

    /// <summary>O status que o Tamer mais treinou neste Digimon.</summary>
    private static StatusTreino MaisTreinado(DigimonSalvo d, out int valor)
    {
        var melhor = StatusTreino.Offense;
        valor = -1;
        foreach (StatusTreino s in Enum.GetValues(typeof(StatusTreino)))
        {
            var v = CuidadoDigimon.Treino(d, s);
            if (v > valor) { valor = v; melhor = s; }
        }
        return melhor;
    }

    /// <summary>O status que combina com um Digimon: o mais forte dele, comparado com valores típicos.</summary>
    private static StatusTreino? FocoDe(DigimonBase f)
    {
        if (f == null) return null;
        var atk = f.Attack / 46f;
        var def = f.Defense / 36f;
        var inte = f.Inteligence / 32f;
        var spd = f.SpecialDefense / 42f;
        var maior = Mathf.Max(atk, def, inte, spd);
        if (maior == atk) return StatusTreino.Offense;
        if (maior == spd) return StatusTreino.Speed;
        if (maior == inte) return StatusTreino.Brains;
        return StatusTreino.Defense;
    }

    /// <summary>Dica para a tela de cuidado: quando e como vai digivolver.</summary>
    public static string Dica(DigimonSalvo d)
    {
        var ficha = d?.Ficha;
        if (ficha == null) return "";
        var nivel = NivelParaDigivolver(ficha.Stage);
        if (nivel <= 0) return "Estágio final";
        if (!ChegouAHora(d)) return $"Digivolui no nível {nivel}";
        return Escolher(d, out var motivo) != null ? "Pronto para digivolver!" : $"Nível certo, mas {motivo}";
    }

    /// <summary>Aplica a digivolução: a espécie muda, o resto (nível, treino, amizade) continua.</summary>
    public static void Aplicar(DigimonSalvo d, DigimonBase nova)
    {
        if (d == null || nova == null) return;
        d.especie = nova.name;
        d.nasceuEm = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        CuidadoDigimon.Mudar(ref d.humor, 20f);
        CuidadoDigimon.Mudar(ref d.energia, 30f);
        PerfilJogador.Atual.Salvar();
    }
}
