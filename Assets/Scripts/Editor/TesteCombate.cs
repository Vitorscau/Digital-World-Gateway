using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>Confere o equilíbrio do combate: status, técnicas e quantos golpes cada luta leva.</summary>
public static class TesteCombate
{
    [MenuItem("Tools/Digimon Nexus/Testar números do combate")]
    public static void Executar()
    {
        var log = new StringBuilder();
        void Luta(string a, int na, string b, int nb)
        {
            var fa = CatalogoDigimon.Buscar(a);
            var fb = CatalogoDigimon.Buscar(b);
            if (fa == null || fb == null) { log.AppendLine($"faltou ficha: {a} / {b}"); return; }
            var da = new Digimon(fa, na);
            var db = new Digimon(fb, nb);
            log.AppendLine($"{a} Lv{na}: HP {da.MaxHp} MP {da.MaxMp} Off {da.Offense} Def {da.Defense} Spd {da.Speed} Brains {da.Brains} esperteza {da.Esperteza:0.00} {da.Personalidade}");
            log.AppendLine("   técnicas: " + string.Join(", ", Tecnica.Repertorio(da).Select(t => $"{t.Nome}({t.Tipo},{t.Poder},MP{t.CustoMP}) ~{CalculoDano.Estimar(da, db, t):0.0}")));
            log.AppendLine($"{b} Lv{nb}: HP {db.MaxHp} MP {db.MaxMp} Off {db.Offense} Def {db.Defense} Spd {db.Speed} Brains {db.Brains} esperteza {db.Esperteza:0.00} {db.Personalidade}");
            log.AppendLine("   técnicas: " + string.Join(", ", Tecnica.Repertorio(db).Select(t => $"{t.Nome}({t.Tipo},{t.Poder},MP{t.CustoMP}) ~{CalculoDano.Estimar(db, da, t):0.0}")));
            var golpeA = CalculoDano.Estimar(da, db, Tecnica.Basico(fa.Atribute));
            var golpeB = CalculoDano.Estimar(db, da, Tecnica.Basico(fb.Atribute));
            log.AppendLine($"   golpes básicos para derrubar: {a} precisa de {db.MaxHp / Mathf.Max(0.1f, golpeA):0.0}, {b} precisa de {da.MaxHp / Mathf.Max(0.1f, golpeB):0.0}");
            log.AppendLine();
        }
        Luta("Gabumon", 5, "Agumon", 4);
        Luta("Gabumon", 5, "Tsukaimon", 6);
        Luta("Palmon", 8, "Gomamon", 8);
        Luta("Greymon", 20, "Devimon", 20);
        Luta("WarGreymon", 50, "MetalGarurumon", 50);
        Luta("Gabumon", 5, "Greymon", 10);
        Debug.Log("[TesteCombate]\n" + log);
        var saida = System.Environment.GetEnvironmentVariable("DIGIMON_LINKS_SAIDA");
        if (!string.IsNullOrEmpty(saida)) System.IO.File.WriteAllText(System.IO.Path.Combine(saida, "combate.txt"), log.ToString());
    }

    public static void ExecutarBatch()
    {
        Executar();
        EditorApplication.Exit(0);
    }
}
