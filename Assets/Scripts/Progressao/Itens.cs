using System.Collections.Generic;
using UnityEngine;

/// <summary>Um tipo de item da Bag (nome, descrição, ícone e o que ele faz).</summary>
public class DefinicaoItem
{
    public string Id;
    public string Nome;
    public string Descricao;
    /// <summary>Sprite em Resources/UI/Figma.</summary>
    public string Icone;
    /// <summary>Usa o item. Retorna a mensagem para mostrar, ou null se não deu para usar.</summary>
    public System.Func<PerfilJogador, string> Usar;
}

/// <summary>Itens do jogo (os mesmos da tela Bag do Figma).</summary>
public static class Itens
{
    public const string Meat = "meat";
    public const string Bandage = "bandage";

    public static readonly IReadOnlyList<DefinicaoItem> Todos = new List<DefinicaoItem>
    {
        new DefinicaoItem
        {
            Id = Meat, Nome = "Meat", Descricao = "Makes Digimon a bit full", Icone = "item_meat",
            Usar = p =>
            {
                var parceiro = p.Parceiro;
                if (parceiro == null) return null;
                var subiu = PerfilJogador.GanharXp(parceiro, 15);
                var nome = parceiro.Ficha != null ? parceiro.Ficha.Name.Trim() : parceiro.especie;
                return subiu > 0 ? $"{nome} comeu e subiu para o nível {parceiro.nivel}!" : $"{nome} comeu! +15 EXP";
            }
        },
        new DefinicaoItem
        {
            Id = Bandage, Nome = "Bandage", Descricao = "Cures wounds + some sickness", Icone = "item_bandage",
            Usar = p =>
            {
                if (p.bandagemAtiva) return null;
                p.bandagemAtiva = true;
                return "Parceiro curado: começa a próxima batalha com +30% de vida.";
            }
        },
    };

    public static DefinicaoItem Buscar(string id) => ((List<DefinicaoItem>)Todos).Find(i => i.Id == id);

    public static Sprite Icone(string id)
    {
        var def = Buscar(id);
        return def != null ? Resources.Load<Sprite>("UI/Figma/" + def.Icone) : null;
    }
}
