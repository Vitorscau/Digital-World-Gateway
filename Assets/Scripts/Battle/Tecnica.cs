using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Como o ataque aparece na arena (efeito, partículas e como ele acerta). Vem da coluna "Estilo" do
/// tecnicas.txt, escolhida pela descrição real de cada ataque (ex.: Terra Force = EsferaGigante).
/// </summary>
public enum EstiloTecnica
{
    Padrao,        // sem estilo próprio: disparo simples (mágica), golpe (física) ou aura (suporte)
    Projetil,      // uma bola de energia
    Rajada,        // vários disparos pequenos em sequência
    EsferaGigante, // esfera enorme carregada acima da cabeça e arremessada
    Raio,          // feixe contínuo em linha reta
    Sopro,         // jato em cone (fogo, gelo, gás, água)
    Relampago,     // descarga elétrica em zigue-zague
    Bolhas,        // bolhas lentas
    Chuva,         // coisas caindo do céu no alvo
    Tornado,       // redemoinho que vai até o alvo
    Pilar,         // coluna que explode do chão embaixo do alvo
    Onda,          // onda de choque que se espalha
    Golpe,         // soco, chute, cabeçada, mordida
    Corte,         // garra, espada, lâmina
    Investida,     // o corpo todo avança atravessando o alvo
    Tremor,        // bate no chão e o chão treme
    Aura           // fortalece o próprio Digimon
}

public enum TipoTecnica
{
    /// <summary>Golpe corpo a corpo: Offense contra Defense.</summary>
    Fisica,
    /// <summary>Disparo à distância: Brains contra Defense/Brains. Pode ser desviado.</summary>
    Magica,
    /// <summary>Melhora o próprio Digimon por um tempo (ex.: Attack Charge).</summary>
    Suporte
}

/// <summary>
/// Uma técnica usada na arena. Vem dos golpes da ficha (MoveBase) ou das técnicas do elemento do Digimon,
/// para todo Digimon ter um repertório de luta mesmo com poucos golpes cadastrados.
/// </summary>
public class Tecnica
{
    public string Nome;
    public int Poder;
    public int CustoMP;
    public TipoTecnica Tipo;
    public DigimonAttribute Atributo;
    /// <summary>Golpe básico (sem custo, sem nome na tela).</summary>
    public bool Basica;
    /// <summary>Efeito visual do ataque na arena.</summary>
    public EstiloTecnica Estilo;
    /// <summary>Cor principal do efeito (null = cor do elemento).</summary>
    public Color? Cor;

    public bool ADistancia => Tipo == TipoTecnica.Magica;

    public static Tecnica Basico(DigimonAttribute atributo) =>
        new Tecnica { Nome = "Attack", Poder = 30, CustoMP = 0, Tipo = TipoTecnica.Fisica, Atributo = DigimonAttribute.Neutral, Basica = true };

    public static Tecnica DeGolpe(MoveBase m)
    {
        if (m == null) return null;
        var nome = string.IsNullOrWhiteSpace(m.Name) ? m.name : m.Name.Trim();
        TipoTecnica tipo;
        switch (m.Type)
        {
            case MoveType.Support: tipo = TipoTecnica.Suporte; break;
            case MoveType.Magic: tipo = TipoTecnica.Magica; break;
            default: tipo = TipoTecnica.Fisica; break;
        }
        return new Tecnica { Nome = nome, Poder = m.Power, CustoMP = Mathf.Max(0, m.Cost), Tipo = tipo, Atributo = m.Atribute };
    }

    /// <summary>Técnicas do elemento: uma básica para todos e uma forte a partir do estágio Champion.</summary>
    public static IEnumerable<Tecnica> DoElemento(DigimonAttribute a, DigimonStage estagio)
    {
        var forte = estagio >= DigimonStage.Champion && estagio != DigimonStage.Fresh && estagio != DigimonStage.InTraining;
        switch (a)
        {
            case DigimonAttribute.Fire:
                yield return M("Fire Ball", 60, 4, a);
                if (forte) yield return M("Mega Flame", 95, 8, a);
                break;
            case DigimonAttribute.Water:
                yield return M("Bubble Shot", 55, 3, a);
                if (forte) yield return M("Ice Storm", 90, 8, a);
                break;
            case DigimonAttribute.Plant:
                yield return F("Poison Ivy", 65, 4, a);
                if (forte) yield return M("Thorn Whip", 90, 8, a);
                break;
            case DigimonAttribute.Electric:
                yield return M("Spark Wave", 60, 4, a);
                if (forte) yield return M("Thunder Justice", 95, 9, a);
                break;
            case DigimonAttribute.Earth:
                yield return F("Rock Crusher", 70, 4, a);
                if (forte) yield return F("Earth Quake", 95, 8, a);
                break;
            case DigimonAttribute.Wind:
                yield return M("Air Blade", 55, 3, a);
                if (forte) yield return M("Tornado", 90, 8, a);
                break;
            case DigimonAttribute.Light:
                yield return M("Holy Light", 65, 5, a);
                if (forte) yield return M("Heaven's Knuckle", 100, 9, a);
                break;
            case DigimonAttribute.Dark:
                yield return M("Dark Shot", 65, 5, a);
                if (forte) yield return M("Death Claw", 100, 9, a);
                break;
            default:
                yield return F("Heavy Strike", 60, 3, a);
                if (forte) yield return F("Power Crane", 90, 8, a);
                break;
        }
    }

    private static Tecnica M(string n, int p, int c, DigimonAttribute a) =>
        new Tecnica { Nome = n, Poder = p, CustoMP = c, Tipo = TipoTecnica.Magica, Atributo = a };

    private static Tecnica F(string n, int p, int c, DigimonAttribute a) =>
        new Tecnica { Nome = n, Poder = p, CustoMP = c, Tipo = TipoTecnica.Fisica, Atributo = a };

    /// <summary>
    /// Tudo o que o Digimon sabe usar na arena: golpe básico + golpes da ficha + os ataques característicos da
    /// espécie (Resources/Digimons/tecnicas.txt, ex.: Agumon → Pepper Breath). Sem ataques na tabela, usa as
    /// técnicas genéricas do elemento.
    /// </summary>
    public static List<Tecnica> Repertorio(Digimon d)
    {
        var lista = new List<Tecnica> { Basico(d.Base.Atribute) };
        var nomes = new HashSet<string>();
        // Ataque que também está na tabela (ex.: Pepper Breath): vale a versão da tabela, com estilo e cor.
        foreach (var t in DaEspecie(d.Base)) nomes.Add(t.Nome);
        if (d.Moves != null)
            foreach (var m in d.Moves)
            {
                var t = DeGolpe(m?.Base);
                if (t == null || string.IsNullOrEmpty(t.Nome) || !nomes.Add(t.Nome)) continue;
                if (t.Tipo != TipoTecnica.Suporte && t.Poder <= 0) continue;
                lista.Add(t);
            }
        var proprias = DaEspecie(d.Base);
        if (proprias.Count > 0) lista.AddRange(proprias);
        else
            foreach (var t in DoElemento(d.Base.Atribute, d.Base.Stage))
                if (nomes.Add(t.Nome)) lista.Add(t);
        return lista;
    }

    // ------------------------------------------------------------ Ataques característicos (tecnicas.txt)

    private static Dictionary<string, List<Tecnica>> _daEspecie;

    /// <summary>Ataques característicos da espécie (vazio se ela não estiver na tabela).</summary>
    public static List<Tecnica> DaEspecie(DigimonBase ficha)
    {
        if (ficha == null) return new List<Tecnica>();
        if (_daEspecie == null) CarregarTabela();
        return _daEspecie.TryGetValue(CatalogoDigimon.Normalizar(ficha.name), out var l) ? l : new List<Tecnica>();
    }

    /// <summary>Lê "Especie|Nome|Tipo|Elemento|Poder|Custo|Estilo|Cor" (Tipo: Physical, Magic ou Support; Estilo e Cor opcionais).</summary>
    private static void CarregarTabela()
    {
        _daEspecie = new Dictionary<string, List<Tecnica>>();
        var texto = Resources.Load<TextAsset>("Digimons/tecnicas");
        if (texto == null) return;
        foreach (var linha in texto.text.Split('\n'))
        {
            var l = linha.Trim();
            if (l.Length == 0 || l.StartsWith("#")) continue;
            var c = l.Split('|');
            if (c.Length < 6) continue;
            TipoTecnica tipo;
            switch (c[2].Trim().ToLowerInvariant())
            {
                case "magic": tipo = TipoTecnica.Magica; break;
                case "support": tipo = TipoTecnica.Suporte; break;
                default: tipo = TipoTecnica.Fisica; break;
            }
            if (!System.Enum.TryParse(c[3].Trim(), true, out DigimonAttribute elemento)) elemento = DigimonAttribute.Neutral;
            int.TryParse(c[4].Trim(), out var poder);
            int.TryParse(c[5].Trim(), out var custo);
            if (tipo != TipoTecnica.Suporte && poder <= 0) continue;
            var chave = CatalogoDigimon.Normalizar(c[0]);
            if (!_daEspecie.TryGetValue(chave, out var lista)) _daEspecie[chave] = lista = new List<Tecnica>();
            var estilo = EstiloTecnica.Padrao;
            if (c.Length > 6 && !System.Enum.TryParse(c[6].Trim(), true, out estilo)) estilo = EstiloTecnica.Padrao;
            Color? cor = null;
            if (c.Length > 7 && ColorUtility.TryParseHtmlString(c[7].Trim(), out var lida)) cor = lida;
            lista.Add(new Tecnica { Nome = c[1].Trim(), Poder = poder, CustoMP = Mathf.Max(0, custo), Tipo = tipo, Atributo = elemento, Estilo = estilo, Cor = cor });
        }
    }
}
