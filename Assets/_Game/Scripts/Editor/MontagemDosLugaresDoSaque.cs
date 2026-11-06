using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using ZombieCityCleaner.Mundo;

namespace ZombieCityCleaner.EditorTools
{
    /// <summary>
    /// Os lugares do saque nos móveis da mobília nova (o Plano_Saque.md da pasta de arte, a parte 2, 01/11/2026), medidos
    /// nas malhas dos prefabs (as do Mobilia_Nova.fbx, no tamanho real, com as partes fechadas) e escritos na tabela
    /// <c>Mundo/Mobilia/LugaresDoSaque.cs</c>, que o gerador do mapa lê em outras threads:
    ///  - as superfícies: as faces viradas para cima do corpo, das gavetas e das portas (a prateleira da porta da
    ///    geladeira), juntas pela altura; cada uma vira uma grade de células de 2 cm;
    ///  - cada célula: precisa estar em cima da face, com pelo menos 6 cm livres em cima (um raio para cima, que acha o
    ///    resto do móvel, as partes e o que vai em cima dele no conjunto: a TV no rack, o computador na mesa); é "dentro"
    ///    da tampa que o raio para cima acha ou da parte que o raio para a frente (+z) acha (a porta, a gaveta, o forno),
    ///    "à vista" se em cima dela não há nada ou se a frente ou um dos lados está livre (a prateleira aberta), e fica de
    ///    fora se o próprio móvel fecha tudo; o chão da gaveta só conta onde sai do móvel quando ela abre;
    ///  - os lugares: os maiores retângulos de células iguais (até 4 por superfície, o primeiro de pelo menos 8 × 10 cm e
    ///    os outros de 10 cm de lado, sem as frestas), com a altura livre menor das células, até o máximo de cada móvel;
    ///  - de fora, o que fica alto demais no jogo, contando a altura dos pendurados: acima de <see cref="AteOOlho"/>, sem
    ///    nada em cima (a cama de cima do beliche: o olho fica a 1,62 m, e o item deitado não se vê), e de
    ///    <see cref="AteAMao"/>, coberto (a prateleira de cima do guarda-roupa, a 2,05 m).
    /// Os móveis e o que vale em cada um (em cima, dentro ou no chão) estão em <see cref="Moveis"/>. Menu Zombie City
    /// Cleaner > Mobília nova > Escrever os lugares do saque (a montagem da mobília nova também chama, no fim).
    /// Escreve também a tabela dos itens que podem ser achados, com o tamanho de cada um (<see cref="EscreverItens"/>, a
    /// parte 3: Zombie City Cleaner > Itens > Escrever os itens do saque), que o catálogo do saque usa, e a dos lugares dos
    /// móveis antigos da farmácia e do hospital (<see cref="EscreverAntigos"/>, 05/11/2026: Zombie City Cleaner > Mobília
    /// nova > Escrever os lugares dos móveis antigos).
    /// </summary>
    public static class MontagemDosLugaresDoSaque
    {
        const string Arquivo = "Assets/_Game/Scripts/Mundo/Mobilia/LugaresDoSaque.cs";

        [System.Flags]
        enum Onde { Cima = 1, Dentro = 2, Chao = 4, Ambos = Cima | Dentro }

        /// <summary>Os móveis com saque (o Plano_Saque.md, "Onde o saque fica"), o que vale em cada um e quantos lugares no máximo.</summary>
        static readonly (Onde onde, int maximo, string[] modelos)[] Moveis =
        {
            // o quarto
            (Onde.Cima, 2, new[] { "Beliche" }),
            (Onde.Cima, 1, new[] { "CamaCasal", "CamaSolteiro", "ColchaoNoChao" }),
            (Onde.Dentro, 8, new[] { "GuardaRoupa2", "GuardaRoupa3", "GuardaRoupa4" }),
            (Onde.Ambos, 8, new[] { "CriadoMudo", "Comoda3", "Comoda6", "Penteadeira", "MesaEscritorio" }),
            (Onde.Dentro, 2, new[] { "Bau" }),
            // a sala
            (Onde.Ambos, 6, new[] { "RackSimples", "RackArmario", "RackEstante", "MesaDeCanto" }),
            (Onde.Cima, 2, new[] { "MesaDeCentro" }),
            (Onde.Cima, 3, new[] { "Lareira" }),
            (Onde.Cima, 6, new[] { "EstanteLivros", "EstanteBaixa" }),
            // a cozinha
            (Onde.Ambos, 8, new[] { "ArmarioChao40", "ArmarioChao60", "ArmarioChao80", "Gaveteiro40", "ArmarioCanto", "Ilha" }),
            (Onde.Dentro, 4, new[] { "Pia", "ArmarioParede40", "ArmarioParede60", "ArmarioParede80", "Fogao" }),
            (Onde.Cima, 4, new[] { "ArmarioParedeAberto60", "ArmarioParedeAberto80" }),
            (Onde.Dentro, 8, new[] { "Geladeira", "GeladeiraGrande" }),
            (Onde.Cima, 1, new[] { "Mesa4", "Mesa6", "MesaRedonda" }),
            // o banheiro
            (Onde.Dentro, 4, new[] { "PiaGabinete", "PiaDupla", "EspelhoArmario" }),
            // o escritório, a garagem e o porão
            (Onde.Cima, 6, new[] { "Bancada", "PrateleiraMadeira90", "PrateleiraMadeira150", "EstanteAco90", "EstanteAco120" }),
            (Onde.Dentro, 4, new[] { "FreezerHorizontal", "Arquivo" }),
            // o acampamento: em cima do colchonete e do saco de dormir, e no chão entre as latas, o fogareiro e as garrafas
            (Onde.Cima, 1, new[] { "Colchonete", "SacoDeDormir" }),
            (Onde.Chao, 3, new[] { "Latas", "Fogareiro", "Garrafas" }),
        };

        /// <summary>A célula da grade, a altura livre que um lugar precisa e até onde ela conta (no tamanho real).</summary>
        const float Celula = 0.02f, AlturaMinima = 0.08f, AlturaMaxima = 0.6f;
        /// <summary>
        /// O lado menor e o maior de um lugar, no mínimo (no tamanho real: 10 × 13 cm no jogo); os outros de uma superfície,
        /// depois do maior, o lado menor e a área (sem as frestas entre as coisas do tampo).
        /// </summary>
        const float LadoMenor = 0.08f, LadoMaior = 0.10f, LadoDosOutros = 0.10f, AreaDosOutros = 0.016f;
        const int PorSuperficie = 4;
        /// <summary>
        /// No jogo, até onde a superfície de um lugar pode ficar do chão: com nada em cima dela (a cama de cima do beliche,
        /// o alto da estante), até um pouco abaixo do olho (a 1,62 m), senão o item deitado não se vê; coberta (as
        /// prateleiras, o que fica atrás de uma porta), até a mão alcançar e a frente do item aparecer.
        /// </summary>
        public const float AteOOlho = 1.55f, AteAMao = 1.95f;
        /// <summary>As superfícies mais baixas que isso (no tamanho real) ficam de fora, menos o chão do acampamento.</summary>
        const float RenteAoChao = 0.04f;

        // o dono de um triângulo: o corpo, o que vai em cima no conjunto (a TV no rack) ou a parte k
        const int Corpo = -1, EmCima = -2;
        // a classe de uma célula: nada (não serve), à vista, ou a parte k que abre
        const int Nada = int.MinValue, AVista = -1;

        [MenuItem("Zombie City Cleaner/Mobília nova/Escrever os lugares do saque")]
        public static void Menu() => Debug.Log(Escrever());

        /// <summary>Mede os lugares de todos os móveis com saque e escreve a tabela; devolve o relatório.</summary>
        public static string Escrever()
        {
            var prefabs = MontagemDaMobiliaNova.Carregar().ToDictionary(p => p.name);
            var log = new StringBuilder();
            var tabela = new SortedDictionary<string, List<LugarDoSaque>>(System.StringComparer.Ordinal);
            int total = 0;
            foreach (var (onde, maximo, modelos) in Moveis)
                foreach (var modelo in modelos)
                {
                    if (!prefabs.TryGetValue(modelo, out var prefab)) { log.AppendLine($"  ATENÇÃO: {modelo} não tem prefab"); continue; }
                    var lugares = Medir(prefab, onde, maximo, prefabs);
                    tabela[modelo] = lugares;
                    total += lugares.Count;
                    int dentro = lugares.Count(l => l.Dentro);
                    log.AppendLine($"  {modelo}: {lugares.Count} ({lugares.Count - dentro} à vista, {dentro} dentro)" +
                                   (lugares.Count == 0 ? "  ATENÇÃO: nenhum" : ": " + string.Join(", ", lugares.Select(Resumo))));
                }
            string arquivo = EscreverTabela(tabela);
            return $"[Lugares do saque] {total} lugares em {tabela.Count} móveis; {arquivo}\n{log}";
        }

        // ------------------------------------------------------------------ os móveis antigos (a farmácia e o hospital)

        const string ArquivoDosAntigos = "Assets/_Game/Scripts/Mundo/Mobilia/LugaresDoSaque.Antigos.cs";

        /// <summary>
        /// Os móveis antigos (os do Cidade_Mobilia.fbx, desenhados na malha da obra) com saque na farmácia e no hospital
        /// (05/11/2026, a etapa 2 do sistema de vida): o salão e o estoque da farmácia, as salas, a rouparia e o depósito do
        /// hospital, e o banheiro dos dois. Só em cima, à vista: eles não abrem.
        /// </summary>
        static readonly string[] Antigos =
        {
            "Prateleira_Farmacia", "Gondola", "EstanteDeAco", "Caixas_A", "Caixas_B", "PiaDeBanheiro",
            "ArmarioDeRemedios", "CriadoMudo", "MesaDeEscritorio", "Balcao", "Prateleira_Rouparia", "Prateleira",
        };

        const int MaximoNosAntigos = 8;

        [MenuItem("Zombie City Cleaner/Mobília nova/Escrever os lugares dos móveis antigos")]
        public static void MenuDosAntigos() => Debug.Log(EscreverAntigos());

        /// <summary>
        /// Mede os lugares dos móveis antigos com saque (<see cref="Antigos"/>) nas malhas do Cidade_Mobilia.fbx, no tamanho
        /// em que cada um foi feito (a escala 1: o jogo escala cada um pelo ponto, Mobilia.Feitos), e escreve a tabela
        /// <c>Mundo/Mobilia/LugaresDoSaque.Antigos.cs</c> (<see cref="LugaresDoSaque.DoAntigo"/>), separada da dos prefabs
        /// (o LugaresDoSaqueTestes confere cada nome dela contra um prefab). Cada malha é medida num objeto temporário
        /// "Antigo.&lt;nome&gt;": com o prefixo, o catálogo da mobília nova não o confunde com o modelo novo do mesmo nome (o
        /// CriadoMudo novo levaria o abajur em cima). Devolve o relatório.
        /// </summary>
        public static string EscreverAntigos()
        {
            var malhas = new Dictionary<string, Mesh>();
            foreach (var m in AssetDatabase.LoadAllAssetsAtPath(MontagemDoMundo.FbxDaMobilia).OfType<Mesh>())
                if (!malhas.ContainsKey(m.name)) malhas[m.name] = m;
            var log = new StringBuilder();
            var tabela = new SortedDictionary<string, List<LugarDoSaque>>(System.StringComparer.Ordinal);
            var semConjunto = new Dictionary<string, GameObject>();
            int total = 0;
            foreach (var nome in Antigos)
            {
                if (!malhas.TryGetValue(nome, out var malha)) { log.AppendLine($"  ATENÇÃO: {nome} não está no {MontagemDoMundo.FbxDaMobilia}"); continue; }
                var go = new GameObject("Antigo." + nome) { hideFlags = HideFlags.HideAndDontSave };
                try
                {
                    go.AddComponent<MeshFilter>().sharedMesh = malha;
                    var lugares = Medir(go, Onde.Cima, MaximoNosAntigos, semConjunto, 1f);
                    // a malha antiga é uma só: o topo do abajur do criado-mudo vira "superfície". Fora o que passa da altura
                    // em que o móvel foi feito, e o lugar que fica em cima de outro maior (a 3 cm de altura dele)
                    if (Mobilia.Feitos.TryGetValue(nome, out var alto)) lugares.RemoveAll(l => l.meio.y > alto.y + 0.02f);
                    lugares.Sort((a, b) => (b.tamanho.x * b.tamanho.z).CompareTo(a.tamanho.x * a.tamanho.z));
                    for (int i = lugares.Count - 1; i > 0; i--)
                        for (int j = 0; j < i; j++)
                        {
                            var a = lugares[j];
                            var b = lugares[i];
                            if (Mathf.Abs(a.meio.y - b.meio.y) > 0.03f) continue;
                            if (Mathf.Abs(a.meio.x - b.meio.x) * 2f < a.tamanho.x + b.tamanho.x && Mathf.Abs(a.meio.z - b.meio.z) * 2f < a.tamanho.z + b.tamanho.z)
                            {
                                lugares.RemoveAt(i);
                                break;
                            }
                        }
                    tabela[nome] = lugares;
                    total += lugares.Count;
                    string feito = Mobilia.Feitos.TryGetValue(nome, out var f) ? $" (feito em {f.x:0.##} × {f.y:0.##} × {f.z:0.##} m)" : "  ATENÇÃO: fora do Mobilia.Feitos";
                    log.AppendLine($"  {nome}{feito}: {lugares.Count}" +
                                   (lugares.Count == 0 ? "  ATENÇÃO: nenhum" : ": " + string.Join(", ", lugares.Select(Resumo))));
                }
                finally
                {
                    Object.DestroyImmediate(go);
                }
            }
            string arquivo = EscreverTabelaDosAntigos(tabela);
            return $"[Lugares do saque, os móveis antigos] {total} lugares em {tabela.Count} móveis; {arquivo}\n{log}";
        }

        static string EscreverTabelaDosAntigos(SortedDictionary<string, List<LugarDoSaque>> tabela)
        {
            var ci = CultureInfo.InvariantCulture;
            string F(float v) => v.ToString("0.###", ci) + "f";
            var s = new StringBuilder();
            s.AppendLine("// Escrito pela Zombie City Cleaner > Mobília nova > Escrever os lugares dos móveis antigos (MontagemDosLugaresDoSaque): não mude à mão.");
            s.AppendLine("using System.Collections.Generic;");
            s.AppendLine();
            s.AppendLine("namespace ZombieCityCleaner.Mundo");
            s.AppendLine("{");
            s.AppendLine("    public static partial class LugaresDoSaque");
            s.AppendLine("    {");
            s.AppendLine("        /// <summary>");
            s.AppendLine("        /// Os lugares de um móvel antigo (o do Cidade_Mobilia.fbx, no tamanho em que ele foi feito: Mobilia.Feitos), só à");
            s.AppendLine("        /// vista (os antigos não abrem); nenhum, se ele não está na tabela. Só a farmácia e o hospital usam (SaqueDaObra).");
            s.AppendLine("        /// </summary>");
            s.AppendLine("        public static LugarDoSaque[] DoAntigo(string modelo) => modelo != null && _antigos.TryGetValue(modelo, out var l) ? l : Nenhum;");
            s.AppendLine();
            s.AppendLine("        public static IEnumerable<string> NomesDosAntigos => _antigos.Keys;");
            s.AppendLine();
            s.AppendLine("        // o modelo antigo: os lugares (o meio, a largura, a altura livre e o fundo), no tamanho em que ele foi feito");
            s.AppendLine("        static readonly Dictionary<string, LugarDoSaque[]> _antigos = new Dictionary<string, LugarDoSaque[]>");
            s.AppendLine("        {");
            foreach (var kv in tabela)
            {
                if (kv.Value.Count == 0) continue;
                s.AppendLine($"            [\"{kv.Key}\"] = new[]");
                s.AppendLine("            {");
                foreach (var l in kv.Value)
                {
                    string parte = l.parte == null ? "" : $", \"{l.parte}\"{(l.naParte ? ", true" : "")}";
                    s.AppendLine($"                L({F(l.meio.x)}, {F(l.meio.y)}, {F(l.meio.z)}, {F(l.tamanho.x)}, {F(l.tamanho.y)}, {F(l.tamanho.z)}{parte}),");
                }
                s.AppendLine("            },");
            }
            s.AppendLine("        };");
            s.AppendLine("    }");
            s.AppendLine("}");
            string texto = s.ToString().Replace("\r\n", "\n").Replace("\n", "\r\n");
            string velho = File.Exists(ArquivoDosAntigos) ? File.ReadAllText(ArquivoDosAntigos) : null;
            if (velho == texto) return "a tabela não mudou";
            File.WriteAllText(ArquivoDosAntigos, texto, new UTF8Encoding(true));
            AssetDatabase.ImportAsset(ArquivoDosAntigos);
            return $"escritos em {ArquivoDosAntigos}";
        }

        // ------------------------------------------------------------------ os itens do saque (a parte 3)

        const string ArquivoDosItens = "Assets/_Game/Scripts/Mundo/Saque/ItensDoSaque.cs";

        [MenuItem("Zombie City Cleaner/Itens/Escrever os itens do saque")]
        public static void MenuDosItens() => Debug.Log(EscreverItens());

        /// <summary>
        /// O <c>Mundo/Saque/ItensDoSaque.cs</c>: de cada item que pode ser achado, o número (o do enum Item), o tamanho como
        /// ele fica num móvel (o do chão do jogo: as armas de fogo e as brancas deitadas de lado, o resto como é; as roupas, a
        /// malha parada delas) e, nas roupas, o lugar no corpo. As malhas são as de Art/Characters/Jogador/Malhas (as dos
        /// itens no chão) e as das roupas como itens (Prefabs/Roupas/Itens). O catálogo do saque usa o nome de cada um.
        /// </summary>
        public static string EscreverItens()
        {
            var lista = new SortedDictionary<int, (string nome, Vector3 tamanho, string lugar)>();
            string[] prefixos = { "Arma", "Item", "Loot", "Equip", "Mun", "Peca", "Sinuca" };
            foreach (var guid in AssetDatabase.FindAssets("t:Mesh", new[] { "Assets/_Game/Art/Characters/Jogador/Malhas" }))
            {
                var m = AssetDatabase.LoadAssetAtPath<Mesh>(AssetDatabase.GUIDToAssetPath(guid));
                int i = m != null ? m.name.IndexOf('_') : -1;
                if (i < 0 || !prefixos.Contains(m.name.Substring(0, i))) continue;
                string modelo = m.name.Substring(i + 1);
                Loot.Item item;
                if (modelo == "Lata") item = Loot.Item.Comida;
                else if (modelo == "Garrafa") item = Loot.Item.Agua;
                else if (!System.Enum.TryParse(modelo, out item)) continue;
                if (item == Loot.Item.Nada || lista.ContainsKey((int)item)) continue;
                var q = Loot.Itens.EhArma(item) || Loot.Itens.EhBranca(item) ? Quaternion.Euler(0f, 0f, 90f) : Quaternion.identity;
                lista[(int)item] = (item.ToString(), Girado(m.bounds, q).size, "");
            }
            var (roupas, _) = MontagemDasRoupas.CarregarItens();
            foreach (var p in roupas)
            {
                var r = p.GetComponent<Roupas.RoupaComoItem>();
                var mf = p.GetComponent<MeshFilter>();
                var item = r != null ? Loot.Itens.RoupaDaPeca(r.peca) : Loot.Item.Nada;
                if (item == Loot.Item.Nada || mf == null || mf.sharedMesh == null) continue;
                lista[(int)item] = ("Roupa." + r.peca, mf.sharedMesh.bounds.size, r.lugar);
            }

            var ci = CultureInfo.InvariantCulture;
            string F(float v) => v.ToString("0.###", ci) + "f";
            var s = new StringBuilder();
            s.AppendLine("// Escrito pela Zombie City Cleaner > Itens > Escrever os itens do saque (MontagemDosLugaresDoSaque): não mude à mão.");
            s.AppendLine("using UnityEngine;");
            s.AppendLine();
            s.AppendLine("namespace ZombieCityCleaner.Mundo");
            s.AppendLine("{");
            s.AppendLine("    public static partial class ItensDoSaque");
            s.AppendLine("    {");
            s.AppendLine("        // o nome, o número do item, o tamanho no móvel (largura em x, altura, fundo em z, em metros do jogo) e o lugar da roupa");
            s.AppendLine("        static readonly (string nome, byte item, Vector3 tamanho, string lugar)[] _itens =");
            s.AppendLine("        {");
            foreach (var kv in lista)
                s.AppendLine($"            (\"{kv.Value.nome}\", {kv.Key}, new Vector3({F(kv.Value.tamanho.x)}, {F(kv.Value.tamanho.y)}, {F(kv.Value.tamanho.z)}), \"{kv.Value.lugar}\"),");
            s.AppendLine("        };");
            s.AppendLine("    }");
            s.AppendLine("}");
            string texto = s.ToString().Replace("\r\n", "\n").Replace("\n", "\r\n");
            string velho = File.Exists(ArquivoDosItens) ? File.ReadAllText(ArquivoDosItens) : null;
            if (velho == texto) return $"[Itens do saque] os {lista.Count} itens não mudaram";
            File.WriteAllText(ArquivoDosItens, texto, new UTF8Encoding(true));
            AssetDatabase.ImportAsset(ArquivoDosItens);
            return $"[Itens do saque] {lista.Count} itens em {ArquivoDosItens}";
        }

        static Bounds Girado(Bounds b, Quaternion q)
        {
            var r = new Bounds(q * b.center, Vector3.zero);
            for (int i = 0; i < 8; i++)
                r.Encapsulate(q * (b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1))));
            return r;
        }

        /// <summary>
        /// As fotos dos lugares do saque (a cena das variações aberta), em Imgs/Renders/Saque_Lugares_&lt;grupo&gt;.png da pasta
        /// de arte: cada móvel do mostruário que tem lugares, de três quartos, com as portas, as gavetas e as tampas
        /// abertas, as caixas e os itens de teste (e _Itens.png, só os itens), da esquerda para a direita do mostruário.
        /// </summary>
        [MenuItem("Zombie City Cleaner/Exemplos/Fotos dos lugares do saque")]
        public static void Fotos() => Debug.Log(FazerAsFotos());

        public static string FazerAsFotos()
        {
            var cena = Object.FindAnyObjectByType<CenaDasVariacoes>();
            if (cena == null) return "Abra a cena das variações antes.";
            var moveis = cena.GetComponentsInChildren<MovelDoMostruario>(true).Where(m => LugaresDoSaque.Tem(m.name)).ToList();
            var partes = moveis.SelectMany(m => m.GetComponentsInChildren<ParteQueAbre>(true)).ToList();
            var log = new StringBuilder();
            try
            {
                foreach (var p in partes) p.Pousar(true);
                foreach (var grupo in moveis.GroupBy(m => CatalogoDaMobilia.DoModelo(m.name)?.grupo ?? CatalogoDaMobilia.Grupo.Servico).OrderBy(g => g.Key))
                {
                    var vistas = new List<(Vector3, Vector3)>();
                    foreach (var m in grupo.OrderBy(m => m.transform.position.x))
                    {
                        var b = new Bounds(m.transform.position, Vector3.zero);
                        foreach (var r in m.GetComponentsInChildren<MeshRenderer>(true))
                            if (!r.name.EndsWith("_Longe")) b.Encapsulate(r.bounds);
                        var lado = Quaternion.Euler(0f, 25f, 0f) * m.transform.forward;
                        float raio = Mathf.Max(b.size.x, b.size.y * 0.8f, 0.8f);
                        vistas.Add((b.center + lado * (raio * 1.05f + 0.5f) + Vector3.up * (raio * 0.45f + 0.3f), b.center));
                    }
                    string nome = grupo.Key.ToString();
                    cena.MostrarOSaqueDoMostruario(true, true);
                    MontagemDoExemploDasCasas.FotosEmGrade(PastaDasFotos + $"Saque_Lugares_{nome}.png", 50f, 480, 360, 5, vistas.ToArray());
                    cena.MostrarOSaqueDoMostruario(false, true);
                    MontagemDoExemploDasCasas.FotosEmGrade(PastaDasFotos + $"Saque_Lugares_{nome}_Itens.png", 50f, 480, 360, 5, vistas.ToArray());
                    log.AppendLine($"  Saque_Lugares_{nome}.png: {string.Join(", ", grupo.OrderBy(m => m.transform.position.x).Select(m => m.name))}");
                }
            }
            finally
            {
                foreach (var p in partes) if (p != null) p.Pousar(false);
                cena.MostrarOSaqueDoMostruario(false, false);
            }
            return $"[Lugares do saque] as fotos em Imgs/Renders/Saque_Lugares_*.png\n{log}";
        }

        const string PastaDasFotos = @"C:\Users\gusta\OneDrive\Documentos\Projetos\Projeto Zombie City Cleaner\Imgs\Renders\";

        static string Resumo(LugarDoSaque l) =>
            $"{l.tamanho.x * 100f:0}×{l.tamanho.z * 100f:0}×{l.tamanho.y * 100f:0} cm a {l.meio.y:0.00} m{(l.Dentro ? $" ({(l.naParte ? "na " : "atrás de ")}{l.parte})" : "")}";

        static string EscreverTabela(SortedDictionary<string, List<LugarDoSaque>> tabela)
        {
            var ci = CultureInfo.InvariantCulture;
            string F(float v) => v.ToString("0.###", ci) + "f";
            var s = new StringBuilder();
            s.AppendLine("// Escrito pela Zombie City Cleaner > Mobília nova > Escrever os lugares do saque (MontagemDosLugaresDoSaque): não mude à mão.");
            s.AppendLine("using System.Collections.Generic;");
            s.AppendLine();
            s.AppendLine("namespace ZombieCityCleaner.Mundo");
            s.AppendLine("{");
            s.AppendLine("    public static partial class LugaresDoSaque");
            s.AppendLine("    {");
            s.AppendLine("        // o modelo: os lugares (o meio, a largura, a altura livre e o fundo, a parte que abre e se o lugar é dela), no tamanho real");
            s.AppendLine("        static readonly Dictionary<string, LugarDoSaque[]> _lugares = new Dictionary<string, LugarDoSaque[]>");
            s.AppendLine("        {");
            foreach (var kv in tabela)
            {
                s.AppendLine($"            [\"{kv.Key}\"] = new[]");
                s.AppendLine("            {");
                foreach (var l in kv.Value)
                {
                    string parte = l.parte == null ? "" : $", \"{l.parte}\"{(l.naParte ? ", true" : "")}";
                    s.AppendLine($"                L({F(l.meio.x)}, {F(l.meio.y)}, {F(l.meio.z)}, {F(l.tamanho.x)}, {F(l.tamanho.y)}, {F(l.tamanho.z)}{parte}),");
                }
                s.AppendLine("            },");
            }
            s.AppendLine("        };");
            s.AppendLine("    }");
            s.AppendLine("}");
            string texto = s.ToString().Replace("\r\n", "\n").Replace("\n", "\r\n");
            string velho = File.Exists(Arquivo) ? File.ReadAllText(Arquivo) : null;
            if (velho == texto) return "a tabela não mudou";
            File.WriteAllText(Arquivo, texto, new UTF8Encoding(true));
            AssetDatabase.ImportAsset(Arquivo);
            return $"escritos em {Arquivo}";
        }

        // ------------------------------------------------------------------ as malhas do móvel

        /// <summary>Os triângulos do móvel (no espaço dele), com o dono de cada um, e os baldes para os raios.</summary>
        sealed class Geometria
        {
            public readonly List<Vector3> a = new List<Vector3>(), b = new List<Vector3>(), c = new List<Vector3>(), n = new List<Vector3>();
            public readonly List<int> dono = new List<int>();
            public readonly List<(string nome, ParteQueAbre parte)> partes = new List<(string, ParteQueAbre)>();
            public Bounds caixa;
            bool _temCaixa;
            const float Balde = 0.05f;
            // os baldes de cada eixo: os triângulos pelos quadrados de 5 cm que eles cobrem no plano dos outros dois
            readonly Dictionary<long, List<int>>[] _baldes = { new Dictionary<long, List<int>>(), new Dictionary<long, List<int>>(), new Dictionary<long, List<int>>() };

            public void Por(Mesh m, Matrix4x4 M, int d)
            {
                var vs = m.vertices;
                var ns = m.normals;
                var ts = m.triangles;
                for (int i = 0; i < ts.Length; i += 3)
                {
                    Vector3 p0 = M.MultiplyPoint3x4(vs[ts[i]]), p1 = M.MultiplyPoint3x4(vs[ts[i + 1]]), p2 = M.MultiplyPoint3x4(vs[ts[i + 2]]);
                    var nn = ns.Length == vs.Length
                        ? M.MultiplyVector(ns[ts[i]] + ns[ts[i + 1]] + ns[ts[i + 2]]).normalized
                        : Vector3.Cross(p1 - p0, p2 - p0).normalized;
                    a.Add(p0); b.Add(p1); c.Add(p2); n.Add(nn); dono.Add(d);
                    if (d == EmCima) continue;
                    foreach (var p in new[] { p0, p1, p2 })
                    {
                        if (!_temCaixa) { caixa = new Bounds(p, Vector3.zero); _temCaixa = true; }
                        else caixa.Encapsulate(p);
                    }
                }
            }

            static long Chave(int i, int j) => ((long)i << 32) ^ (uint)j;

            // os outros dois eixos de um eixo (o plano em que os raios dele furam)
            static (int u, int v) Outros(int eixo) => eixo == 0 ? (2, 1) : eixo == 1 ? (0, 2) : (0, 1);

            public void Indexar()
            {
                for (int t = 0; t < a.Count; t++)
                {
                    Vector3 mn = Vector3.Min(a[t], Vector3.Min(b[t], c[t])), mx = Vector3.Max(a[t], Vector3.Max(b[t], c[t]));
                    for (int e = 0; e < 3; e++)
                    {
                        var (u, v) = Outros(e);
                        for (int i = Mathf.FloorToInt(mn[u] / Balde); i <= Mathf.FloorToInt(mx[u] / Balde); i++)
                            for (int j = Mathf.FloorToInt(mn[v] / Balde); j <= Mathf.FloorToInt(mx[v] / Balde); j++)
                            {
                                long k = Chave(i, j);
                                if (!_baldes[e].TryGetValue(k, out var l)) _baldes[e][k] = l = new List<int>();
                                l.Add(t);
                            }
                    }
                }
            }

            /// <summary>
            /// O primeiro triângulo que o raio de o acha ao longo do eixo (0 x, 1 y, 2 z), para o lado do sinal: a distância
            /// (infinita sem nenhum) e o dono. Os do dono 'fora' não contam.
            /// </summary>
            public (float t, int dono) Raio(Vector3 o, int eixo, float sinal, int fora = int.MaxValue)
            {
                float melhor = float.PositiveInfinity;
                int quem = Nada;
                var (u, v) = Outros(eixo);
                if (!_baldes[eixo].TryGetValue(Chave(Mathf.FloorToInt(o[u] / Balde), Mathf.FloorToInt(o[v] / Balde)), out var l)) return (melhor, quem);
                foreach (int t in l)
                {
                    if (dono[t] == fora) continue;
                    if (!Bari(o[u], o[v], a[t][u], a[t][v], b[t][u], b[t][v], c[t][u], c[t][v], out var l1, out var l2, out var l3)) continue;
                    float s = (l1 * a[t][eixo] + l2 * b[t][eixo] + l3 * c[t][eixo] - o[eixo]) * sinal;
                    if (s > 1e-4f && s < melhor) { melhor = s; quem = dono[t]; }
                }
                return (melhor, quem);
            }

            public (float t, int dono) Cima(Vector3 o, int fora = int.MaxValue) => Raio(o, 1, 1f, fora);

            /// <summary>O ponto (em x e z) está em cima de um dos triângulos da lista.</summary>
            public bool Apoia(List<int> tris, float x, float z)
            {
                foreach (int t in tris)
                    if (Bari(x, z, a[t].x, a[t].z, b[t].x, b[t].z, c[t].x, c[t].z, out _, out _, out _)) return true;
                return false;
            }

            public bool Abre(int d) => d >= 0 && partes[d].parte != null;
        }

        static bool Bari(float px, float py, float ax, float ay, float bx, float by, float cx, float cy, out float l1, out float l2, out float l3)
        {
            l1 = l2 = l3 = 0f;
            float d = (by - cy) * (ax - cx) + (cx - bx) * (ay - cy);
            if (Mathf.Abs(d) < 1e-10f) return false;
            l1 = ((by - cy) * (px - cx) + (cx - bx) * (py - cy)) / d;
            l2 = ((cy - ay) * (px - cx) + (ax - cx) * (py - cy)) / d;
            l3 = 1f - l1 - l2;
            const float e = -1e-5f;
            return l1 >= e && l2 >= e && l3 >= e;
        }

        // o corpo, as partes (fechadas) e o que vai em cima do móvel no conjunto (o primeiro do catálogo, como no
        // MobiliaNova: no meio da caixa dele, ou na frente do vão da mesa de escritório)
        static Geometria Juntar(GameObject prefab, Dictionary<string, GameObject> prefabs)
        {
            var g = new Geometria();
            var raiz = prefab.transform;
            foreach (var f in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                if (f.sharedMesh == null || f.name.EndsWith("_Longe")) continue;
                int d = Corpo;
                var M = Matrix4x4.identity;
                if (f.transform != raiz)
                {
                    M = raiz.worldToLocalMatrix * f.transform.localToWorldMatrix;
                    int ponto = f.name.IndexOf('.');
                    g.partes.Add((ponto >= 0 ? f.name.Substring(ponto + 1) : f.name, f.GetComponent<ParteQueAbre>()));
                    d = g.partes.Count - 1;
                }
                g.Por(f.sharedMesh, M, d);
            }
            var t = CatalogoDaMobilia.DoModelo(prefab.name);
            var sobre = CatalogoDaMobilia.Tipos.FirstOrDefault(s => s.lugar == CatalogoDaMobilia.Lugar.Sobre && s.sobre != null && s.sobre.Any(x => x.modelo == prefab.name));
            if (sobre != null && prefabs.TryGetValue(sobre.modelo, out var cima) && MedidasDaMobilia.Tem(prefab.name) && MedidasDaMobilia.Tem(sobre.modelo))
            {
                float altura = sobre.sobre.First(x => x.modelo == prefab.name).altura;
                var meio = MedidasDaMobilia.Meio(prefab.name);
                float vx = t != null && t.vao != 0f ? t.vao - meio.x : 0f;
                var mc = MedidasDaMobilia.Meio(sobre.modelo);
                var origem = new Vector3(meio.x + vx - mc.x, altura, meio.y - mc.y);
                foreach (var f in cima.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (f.sharedMesh == null || f.name.EndsWith("_Longe")) continue;
                    var M = Matrix4x4.Translate(origem) * (f.transform == cima.transform ? Matrix4x4.identity : cima.transform.worldToLocalMatrix * f.transform.localToWorldMatrix);
                    g.Por(f.sharedMesh, M, EmCima);
                }
            }
            g.Indexar();
            return g;
        }

        // ------------------------------------------------------------------ as superfícies e os lugares

        // 'escala': a do jogo (a mobília nova, ×1,30; os móveis antigos, 1: o jogo os escala pelo ponto)
        static List<LugarDoSaque> Medir(GameObject prefab, Onde onde, int maximo, Dictionary<string, GameObject> prefabs, float escala = MedidasDaMobilia.Escala)
        {
            var g = Juntar(prefab, prefabs);
            // as superfícies: as faces para cima do corpo, das gavetas e das portas, pela altura (a tampa e o forno não:
            // em cima deles, o item cairia quando abrem; o que vai em cima no conjunto também não)
            var superficies = new Dictionary<(int dono, int nivel), List<int>>();
            if ((onde & (Onde.Cima | Onde.Dentro)) != 0)
                for (int t = 0; t < g.a.Count; t++)
                {
                    int d = g.dono[t];
                    if (g.n[t].y < 0.97f || d == EmCima) continue;
                    if (d >= 0 && (g.partes[d].parte == null || g.partes[d].parte.tipo == ParteQueAbre.Tipo.Tampa)) continue;
                    if (Vector3.Cross(g.b[t] - g.a[t], g.c[t] - g.a[t]).magnitude * 0.5f < 2e-5f) continue;
                    var k = (d, Mathf.RoundToInt((g.a[t].y + g.b[t].y + g.c[t].y) / 3f / 0.004f));
                    if (!superficies.TryGetValue(k, out var l)) superficies[k] = l = new List<int>();
                    l.Add(t);
                }

            var tipo = CatalogoDaMobilia.DoModelo(prefab.name);
            float pendurado = tipo != null && tipo.lugar == CatalogoDaMobilia.Lugar.Alto ? tipo.altura : 0f;
            var achados = new List<(float area, LugarDoSaque lugar)>();
            foreach (var kv in superficies)
            {
                var tris = kv.Value;
                float y = tris.Max(t => Mathf.Max(g.a[t].y, Mathf.Max(g.b[t].y, g.c[t].y)));
                // (rente ao chão, só o que caiu do móvel: os livros na frente da estante)
                if ((pendurado + y) * escala > AteAMao || pendurado + y < RenteAoChao) continue;
                float x0 = tris.Min(t => Mathf.Min(g.a[t].x, Mathf.Min(g.b[t].x, g.c[t].x))), x1 = tris.Max(t => Mathf.Max(g.a[t].x, Mathf.Max(g.b[t].x, g.c[t].x)));
                float z0 = tris.Min(t => Mathf.Min(g.a[t].z, Mathf.Min(g.b[t].z, g.c[t].z))), z1 = tris.Max(t => Mathf.Max(g.a[t].z, Mathf.Max(g.b[t].z, g.c[t].z)));
                Superficie(g, kv.Key.dono, y, x0, x1, z0, z1, (x, z) => g.Apoia(tris, x, z), onde, pendurado, escala, achados);
            }
            // o chão em volta das latas, do fogareiro e das garrafas, dentro da caixa do modelo
            if ((onde & Onde.Chao) != 0)
                Superficie(g, Corpo, 0f, g.caixa.min.x + 0.01f, g.caixa.max.x - 0.01f, g.caixa.min.z + 0.01f, g.caixa.max.z - 0.01f, (x, z) => true, onde, 0f, escala, achados);
            return achados.OrderByDescending(x => x.area).Take(maximo).Select(x => x.lugar)
                .OrderBy(l => l.parte ?? "").ThenBy(l => l.meio.y).ThenBy(l => l.meio.x).ToList();
        }

        // a grade de uma superfície (do dono, na altura y, dentro de x0..x1 e z0..z1), a classe de cada célula e os
        // maiores retângulos de cada classe
        static void Superficie(Geometria g, int dono, float y, float x0, float x1, float z0, float z1, System.Func<float, float, bool> apoia,
                               Onde onde, float pendurado, float escala, List<(float area, LugarDoSaque lugar)> achados)
        {
            int nx = Mathf.FloorToInt((x1 - x0) / Celula), nz = Mathf.FloorToInt((z1 - z0) / Celula);
            int menor = Mathf.CeilToInt(LadoMenor / Celula - 0.01f), maior = Mathf.CeilToInt(LadoMaior / Celula - 0.01f);
            if (nx < menor || nz < menor) return;
            // as células ficam no meio da superfície (o que sobra da divisão fica nas beiradas, por igual)
            x0 += (x1 - x0 - nx * Celula) / 2f;
            z0 += (z1 - z0 - nz * Celula) / 2f;
            var classe = new int[nx, nz];
            var livre = new float[nx, nz];
            var ceu = new bool[nx, nz];
            var parte = dono >= 0 ? g.partes[dono].parte : null;
            bool chao = (onde & Onde.Chao) != 0 && dono == Corpo && y == 0f;
            for (int i = 0; i < nx; i++)
                for (int j = 0; j < nz; j++)
                {
                    classe[i, j] = Nada;
                    float x = x0 + (i + 0.5f) * Celula, z = z0 + (j + 0.5f) * Celula;
                    if (!apoia(x, z)) continue;
                    var p = new Vector3(x, y + 0.002f, z);
                    var (t, quem) = g.Cima(p);
                    livre[i, j] = Mathf.Min(t, AlturaMaxima);
                    ceu[i, j] = float.IsInfinity(t);
                    if (t < AlturaMinima) continue;
                    int k;
                    if (parte != null)
                    {
                        // o chão da gaveta: só onde sai do móvel quando ela abre (os outros fechados)
                        if (parte.tipo == ParteQueAbre.Tipo.Gaveta && g.Cima(p + Vector3.forward * parte.quanto, dono).t < AlturaMaxima) continue;
                        // a prateleira da porta (a da geladeira): só a de dentro, coberta com ela fechada
                        if (parte.tipo == ParteQueAbre.Tipo.Porta && float.IsInfinity(t)) continue;
                        k = dono;
                    }
                    else if (float.IsInfinity(t)) k = AVista;
                    else if (g.Abre(quem) && g.partes[quem].parte.tipo == ParteQueAbre.Tipo.Tampa && g.partes[quem].parte.rotulo != "o forno") k = quem;
                    else if (chao) continue;
                    else
                    {
                        // coberta: a porta, a gaveta ou o forno na frente; senão, à vista se a frente ou um dos lados está
                        // livre (a prateleira aberta; a cama de baixo do beliche, pelo lado); senão, fechada pelo móvel
                        var o = new Vector3(x, y + Mathf.Min(t, 0.2f) / 2f, z);
                        var (_, frente) = g.Raio(o, 2, 1f);
                        if (g.Abre(frente)) k = frente;
                        else if (frente == Nada || g.Raio(o, 0, 1f).dono == Nada || g.Raio(o, 0, -1f).dono == Nada) k = AVista;
                        else continue;
                    }
                    if (k == AVista ? (onde & (Onde.Cima | Onde.Chao)) == 0 : (onde & Onde.Dentro) == 0) continue;
                    classe[i, j] = k;
                }

            for (int vez = 0; vez < PorSuperficie; vez++)
            {
                var (area, ri, rj, rw, rd, rk) = (0, 0, 0, 0, 0, Nada);
                int lado = vez == 0 ? menor : Mathf.CeilToInt(LadoDosOutros / Celula - 0.01f);
                foreach (int k in Classes(classe))
                {
                    var r = MaiorRetangulo(classe, k, lado, Mathf.Max(lado, maior));
                    if (vez > 0 && r.area * Celula * Celula < AreaDosOutros) continue;
                    if (r.area > area) (area, ri, rj, rw, rd, rk) = (r.area, r.i, r.j, r.w, r.d, k);
                }
                if (rk == Nada) break;
                float h = AlturaMaxima;
                bool aberto = true;
                for (int i = ri; i < ri + rw; i++)
                    for (int j = rj; j < rj + rd; j++) { h = Mathf.Min(h, livre[i, j]); aberto &= ceu[i, j]; }
                // as células do lugar e uma em volta saem (um lugar não encosta no outro)
                for (int i = Mathf.Max(0, ri - 1); i < Mathf.Min(nx, ri + rw + 1); i++)
                    for (int j = Mathf.Max(0, rj - 1); j < Mathf.Min(nz, rj + rd + 1); j++) classe[i, j] = Nada;
                if ((pendurado + y) * escala > (aberto ? AteOOlho : AteAMao)) continue;
                var lugar = new LugarDoSaque
                {
                    meio = new Vector3(x0 + (ri + rw / 2f) * Celula, y, z0 + (rj + rd / 2f) * Celula),
                    tamanho = new Vector3(rw * Celula, h, rd * Celula),
                    parte = rk >= 0 ? g.partes[rk].nome : null,
                    naParte = rk >= 0 && rk == dono,
                };
                achados.Add((rw * rd * Celula * Celula, lugar));
            }
        }

        static IEnumerable<int> Classes(int[,] classe)
        {
            var vistas = new HashSet<int>();
            foreach (int k in classe)
                if (k != Nada && vistas.Add(k)) yield return k;
        }

        // o maior retângulo de células da classe k com o lado menor de pelo menos 'menor' células e o maior de 'maior'
        // (pelas colunas de células seguidas, linha a linha: os retângulos que não crescem para nenhum lado)
        static (int area, int i, int j, int w, int d) MaiorRetangulo(int[,] classe, int k, int menor, int maior)
        {
            int nx = classe.GetLength(0), nz = classe.GetLength(1);
            var alto = new int[nx + 1];
            var pilha = new Stack<int>();
            (int area, int i, int j, int w, int d) melhor = (0, 0, 0, 0, 0);
            for (int j = 0; j < nz; j++)
            {
                for (int i = 0; i < nx; i++) alto[i] = classe[i, j] == k ? alto[i] + 1 : 0;
                alto[nx] = 0;
                pilha.Clear();
                for (int i = 0; i <= nx; i++)
                {
                    while (pilha.Count > 0 && alto[pilha.Peek()] >= alto[i])
                    {
                        int topo = pilha.Pop();
                        int d = alto[topo];
                        int esquerda = pilha.Count > 0 ? pilha.Peek() + 1 : 0;
                        int w = i - esquerda;
                        if (d > 0 && Mathf.Min(w, d) >= menor && Mathf.Max(w, d) >= maior && w * d > melhor.area)
                            melhor = (w * d, esquerda, j - d + 1, w, d);
                    }
                    pilha.Push(i);
                }
            }
            return melhor;
        }
    }
}
