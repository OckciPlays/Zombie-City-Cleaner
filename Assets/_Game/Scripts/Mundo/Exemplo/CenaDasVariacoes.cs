using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ZombieCityCleaner.Mundo
{
    /// <summary>
    /// A cena das variações das construções (a pedido em 29/10/2026, para ver uma a uma e pensar no layout e na mobília):
    /// do mapa gerado com a semente e o tamanho dados, uma construção de cada variação, com os móveis, numa rua só:
    ///  - as casas (do lado norte): a térrea e o sobrado, sem e com porão, com a vaga, a garagem ou a garagem com a porta
    ///    para dentro (as 12 da conferência das casas, sem o espelho), e a casa da família;
    ///  - as lojas (do lado sul; quando ele fica bem mais comprido, do norte): cada tipo de um andar e com o apartamento
    ///    em cima (o mercado só tem um andar), e a loja de conveniência do posto;
    ///  - os prédios (do lado mais curto): o térreo com apartamentos ou com lojas, o núcleo no meio ou encostado num lado
    ///    (os estreitos), e o hospital; o mais baixo de cada, com todos os andares por dentro.
    /// Nenhuma fica espelhada (o espelho não muda a planta), e o material é o da cidade sem o abandono, para ver as cores
    /// dos cômodos e dos móveis. No Play, o AndarNoExemplo pula de uma para a outra (as setas), mostra a planta de cada
    /// andar (P) e a ficha da construção mais perto: os cômodos, o tamanho e os móveis de cada um. O R gera com outra
    /// semente (outro mapa: outra construção de cada variação).
    /// No começo da rua, antes das casas, o mostruário da mobília nova (o Plano_Mobilia.md, 29/10/2026): os prefabs de
    /// <see cref="mobiliaNova"/> postos pelo <see cref="CatalogoDaMobilia"/>, um cômodo aberto na frente para cada grupo
    /// (a sala, o quarto...), com o piso, as paredes e a porta do tamanho das construções (×1,15). As portas e as gavetas
    /// abrem com o E (o ParteQueAbre), o C troca a cor do móvel na mira (o MovelDoMostruario) e o K troca a escala dos
    /// móveis (1,00, 1,15 e 1,30; começa em 1,30, a escolhida para o jogo).
    /// Montada por Editor/MontagemDoExemploDasCasas (menu Zombie City Cleaner > Exemplos > Variações das construções).
    /// </summary>
    [ExecuteAlways]
    public class CenaDasVariacoes : CenaDeExemplo
    {
        public TamanhoDoMundo tamanho = TamanhoDoMundo.Medio;
        [Tooltip("Os modelos do Cidade_Mobilia.fbx (os nomes de Mobilia.Feitos); sem eles, as construções ficam sem os móveis.")]
        public Mesh[] mobilia;
        [Tooltip("Os prefabs da mobília nova (Prefabs/MobiliaNova, de Editor/MontagemDaMobiliaNova): o mostruário no começo da rua.")]
        public GameObject[] mobiliaNova;

        /// <summary>O espaço entre um lote e o outro.</summary>
        const float Entre = 4f;

        protected override string Chave => $"{semente}|{tamanho}|variações|{mobilia?.Length ?? 0}|{mobiliaNova?.Length ?? 0}";

        float _norte, _sul;

        public override string Texto(Vector3 posicao)
        {
            int i = MaisPerto(posicao);
            return i < 0 ? Resumo : Paradas[i].Ficha;
        }

        // ------------------------------------------------------------------ as variações

        static string VariacaoDaCasa(Planta p)
        {
            var g = p.garagem;
            return (p.molde == "Terrea" ? "térrea" : "sobrado") + (p.porao ? " com porão" : "") +
                   (g == null ? ", sem o lugar do carro" : !g.coberta ? ", vaga" : g.portaParaDentro ? ", garagem com porta para dentro" : ", garagem");
        }

        static IEnumerable<string> OrdemDasCasas()
        {
            foreach (var molde in new[] { "térrea", "sobrado" })
                foreach (var porao in new[] { "", " com porão" })
                    foreach (var carro in new[] { ", vaga", ", garagem", ", garagem com porta para dentro" })
                        yield return molde + porao + carro;
            yield return "a casa da família";
        }

        static string VariacaoDaLoja(PlantaDeLoja p) => PlantaDeLoja.Nome(p.tipo) + (p.andares > 1 ? ", com apartamento em cima" : ", de um andar");

        static IEnumerable<string> OrdemDasLojas()
        {
            foreach (TipoDeLoja t in System.Enum.GetValues(typeof(TipoDeLoja)))
            {
                if (t == TipoDeLoja.Conveniencia) continue;
                yield return PlantaDeLoja.Nome(t) + ", de um andar";
                // o mercado é sempre de um andar (alto)
                if (t != TipoDeLoja.Mercado) yield return PlantaDeLoja.Nome(t) + ", com apartamento em cima";
            }
        }

        static bool NucleoNumLado(PlantaDePredio p) => p.NucleoX0 <= p.X0 + 0.05f || p.NucleoX1 >= p.X1 - 0.05f;

        static string VariacaoDoPredio(PlantaDePredio p) =>
            p.hospital ? "hospital" :
            (p.lojasNoTerreo ? "lojas no térreo" : "apartamentos no térreo") + (NucleoNumLado(p) ? ", estreito (o núcleo num lado)" : ", o núcleo no meio");

        static readonly string[] OrdemDosPredios =
        {
            "apartamentos no térreo, o núcleo no meio", "apartamentos no térreo, estreito (o núcleo num lado)",
            "lojas no térreo, o núcleo no meio", "lojas no térreo, estreito (o núcleo num lado)", "hospital",
        };

        // as chaves na ordem pedida, e depois as que aparecerem fora dela; as que faltam no mapa vão para 'faltam'
        static List<string> NaOrdem<T>(IEnumerable<string> ordem, Dictionary<string, T> achados, List<string> faltam)
        {
            var r = new List<string>();
            foreach (var k in ordem)
                if (achados.ContainsKey(k)) r.Add(k);
                else faltam.Add(k);
            r.AddRange(achados.Keys.Where(k => !r.Contains(k)).OrderBy(k => k));
            return r;
        }

        // ------------------------------------------------------------------ a montagem

        protected override void Montar()
        {
            var M = Medidas.DoJogo;
            if (mobilia != null && mobilia.Length > 0) Mobilia.Registrar(mobilia);
            var d = GeradorDoMundo.Gerar(semente, tamanho);

            // a primeira de cada variação (a casa da família à parte); das lojas e dos prédios, a de menos andares
            var casas = new Dictionary<string, (TracadoDaCidade t, Lote l)>();
            var lojas = new Dictionary<string, (TracadoDaCidade t, Lote l)>();
            var predios = new Dictionary<string, (TracadoDaCidade t, Lote l)>();
            foreach (var t in d.Plano.Tracados)
                foreach (var l in t.Lotes)
                {
                    if (l.Casa != null)
                    {
                        string k = l.Tipo == TipoDeLote.Familia ? "a casa da família" : VariacaoDaCasa(l.Casa);
                        if (!casas.ContainsKey(k)) casas[k] = (t, l);
                    }
                    else if (l.Loja != null)
                    {
                        string k = VariacaoDaLoja(l.Loja);
                        if (!lojas.TryGetValue(k, out var a) || l.Loja.andares < a.l.Loja.andares) lojas[k] = (t, l);
                    }
                    else if (l.Predio != null)
                    {
                        string k = VariacaoDoPredio(l.Predio);
                        if (!predios.TryGetValue(k, out var a) || l.Predio.andares < a.l.Predio.andares) predios[k] = (t, l);
                    }
                }
            var faltam = new List<string>();
            var ordemDasCasas = NaOrdem(OrdemDasCasas(), casas, faltam);
            var ordemDasLojas = NaOrdem(OrdemDasLojas(), lojas, faltam);
            var ordemDosPredios = NaOrdem(OrdemDosPredios, predios, faltam);

            float inicio = Mostruario(M);
            _norte = _sul = 0f;
            for (int i = 0; i < ordemDasCasas.Count; i++)
            {
                var (t, l) = casas[ordemDasCasas[i]];
                PorCasaDaVariacao(l.Casa, $"Casas ({i + 1} de {ordemDasCasas.Count}): {ordemDasCasas[i]}", t.Cidade.Nome, M);
            }
            int quantasLojas = ordemDasLojas.Count + 1;
            for (int i = 0; i < ordemDasLojas.Count; i++)
            {
                var (t, l) = lojas[ordemDasLojas[i]];
                PorLoja(l.Loja, $"Lojas ({i + 1} de {quantasLojas}): {ordemDasLojas[i]}", $"lote de {t.Cidade.Nome}", M);
            }
            PorLoja(PlantasDeLoja.DoPosto(new System.Random(semente + 11), M), $"Lojas ({quantasLojas} de {quantasLojas}): a loja de conveniência do posto",
                    "o posto da beira da estrada", M);
            for (int i = 0; i < ordemDosPredios.Count; i++)
            {
                var (t, l) = predios[ordemDosPredios[i]];
                PorPredio(l.Predio, $"Prédios ({i + 1} de {ordemDosPredios.Count}): {ordemDosPredios[i]}", t.Cidade.Nome, M);
            }
            Rua(Mathf.Min(-12f, inicio - 4f), Mathf.Max(_norte, _sul) + 8f, 0);

            resumo.Insert(0, $"{tamanho} {semente}: {ordemDasCasas.Count} casas, {quantasLojas} lojas e {ordemDosPredios.Count} prédios, " +
                             $"{triangulos:N0} triângulos com os móveis{(Mobilia.Pronta ? "" : " (sem os móveis: os modelos não estão na cena)")}.\n" +
                             (faltam.Count > 0 ? $"Não há no mapa: {string.Join("; ", faltam)}.\n" : "") + "\n");
        }

        void PorCasaDaVariacao(Planta p, string titulo, string cidade, Medidas M)
        {
            p.espelhar = false;
            var casa = new Casa(p, M);
            casa.Montar();
            Mobilia.Por(casa.moveis, casa.vis, casa.col, p.semente);
            int tri = casa.vis.Triangulos;
            float x = _norte;
            _norte = PorCasa(casa, titulo, x, 1, out var go) + Entre - 3f;
            MalhaDeObra.Devolver(casa.vis);
            MalhaDeObra.Devolver(casa.col);

            var parada = new Parada { Giro = 0f, Limites = Limites(go) };
            var porta = NaCasa(go, p, casa.Entrada.x, 0f, casa.Entrada.y);
            parada.Chegada = new Vector3(porta.x, 0.2f, Calcada - 1.2f);
            // os pisos: o porão, o térreo e o de cima
            var pisos = new List<int>();
            if (p.porao) pisos.Add(-1);
            for (int a = 0; a < p.andares; a++) pisos.Add(a);
            float PisoDoAndar(int a) => a >= 0 ? Casa.PisoDe(p) + a * (M.altura + M.laje) : Casa.PisoDe(p) - M.laje - M.alturaPorao;
            var ficha = new StringBuilder();
            ficha.AppendLine(titulo);
            ficha.AppendLine($"{p.W:0.0} × {p.D:0.0} m, {(p.andares > 1 ? $"{p.andares} andares" : "1 andar")}{(p.porao ? " e o porão" : "")}; " +
                             $"de um lote de {cidade}; {tri:N0} triângulos com os móveis");
            foreach (int a in pisos)
            {
                float y = PisoDoAndar(a);
                parada.Pisos.Add(go.transform.position.y + y);
                parada.NomesDosPisos.Add(NomeDoAndar(a));
                var doAndar = p.comodos.Where(k => k.andar == a).ToList();
                Andar(ficha, NomeDoAndar(a), doAndar, casa.moveis, y, false);
                foreach (var k in doAndar) parada.Comodos.Add((NaCasa(go, p, k.Cx, y + 0.1f, k.Cz), parada.Pisos.Count - 1, Rotulo(k)));
            }
            parada.Ficha = ficha.ToString();
            Paradas.Add(parada);
        }

        void PorLoja(PlantaDeLoja p, string titulo, string deOnde, Medidas M)
        {
            p.espelhar = false;
            var loja = new Loja(p, M);
            loja.Montar((x, z) => 0f, 0f);
            Mobilia.Por(loja.moveis, loja.vis, loja.col, p.semente);
            int lado = _sul <= _norte + 40f ? -1 : 1;
            var go = PorNoLote(titulo, loja.vis, loja.col, p.W, p.D, lado);
            var parada = NovaParada(go, lado, p.W);
            var ficha = new StringBuilder();
            ficha.AppendLine(titulo);
            ficha.AppendLine($"{p.W:0.0} × {p.D:0.0} m, {(p.andares > 1 ? $"{p.andares} andares (o apartamento em cada andar de cima)" : "1 andar")}; " +
                             $"{deOnde}; {loja.vis.Triangulos:N0} triângulos com os móveis");
            for (int f = 0; f < p.andares; f++)
            {
                float y = p.Base(f);
                parada.Pisos.Add(y);
                parada.NomesDosPisos.Add(NomeDoAndar(f));
                var doAndar = p.comodos.Where(k => k.andar == f && k.tipo != "escada").ToList();
                Andar(ficha, NomeDoAndar(f), doAndar, loja.moveis, y, false);
                foreach (var k in doAndar) parada.Comodos.Add((go.transform.TransformPoint(new Vector3(k.Cx, y + 0.1f, k.Cz)), f, Rotulo(k)));
            }
            parada.Pisos.Add(p.Cobertura);
            parada.NomesDosPisos.Add("Cobertura");
            parada.Ficha = ficha.ToString();
            Paradas.Add(parada);
            MalhaDeObra.Devolver(loja.vis);
            MalhaDeObra.Devolver(loja.col);
        }

        void PorPredio(PlantaDePredio p, string titulo, string cidade, Medidas M)
        {
            p.espelhar = false;
            var casca = new Predio(p, M, -1);
            casca.MontarCasca((x, z) => 0f, 0f);
            foreach (var e in casca.Escuros) MalhaDeObra.Devolver(e);
            int lado = _sul <= _norte ? -1 : 1;
            int daCasca = casca.vis.Triangulos, tri = daCasca;
            var go = PorNoLote(titulo, casca.vis, casca.col, p.W, p.D, lado);
            MalhaDeObra.Devolver(casca.vis);
            MalhaDeObra.Devolver(casca.col);

            var parada = NovaParada(go, lado, p.W, p.xPorta);
            var andares = new StringBuilder();
            for (int f = 0; f < p.andares; f++)
            {
                var andar = new Predio(p, M, f);
                andar.MontarAndar();
                foreach (var e in andar.Escuros) MalhaDeObra.Devolver(e);
                Mobilia.Por(andar.Moveis, andar.vis, andar.col, p.semente * 31 + f);
                tri += andar.vis.Triangulos;
                var g = Novo($"Andar {f}", go.transform);
                g.AddComponent<MeshFilter>().sharedMesh = Malha(andar.vis, $"{titulo}, andar {f}");
                g.AddComponent<MeshRenderer>().sharedMaterial = material;
                g.AddComponent<MeshCollider>().sharedMesh = Malha(andar.col, $"{titulo}, andar {f} (colisão)");
                float y = p.Base(f);
                parada.Pisos.Add(y);
                parada.NomesDosPisos.Add(NomeDoAndar(f));
                foreach (var k in andar.Comodos) parada.Comodos.Add((go.transform.TransformPoint(new Vector3(k.Cx, y + 0.1f, k.Cz)), f, Rotulo(k)));
                // na ficha, o térreo e o primeiro andar (os de cima são iguais a ele, com os móveis sorteados de novo)
                if (f <= 1) Andar(andares, NomeDoAndar(f), andar.Comodos, andar.Moveis, y, andar.Comodos.Count > 8);
                MalhaDeObra.Devolver(andar.vis);
                MalhaDeObra.Devolver(andar.col);
            }
            parada.Pisos.Add(p.Cobertura);
            parada.NomesDosPisos.Add("Cobertura");
            parada.Limites = Limites(go);
            triangulos += tri - daCasca;
            if (p.andares > 2) andares.AppendLine($"Do 2º ao {p.andares - 1}º andar: iguais ao 1º, com os móveis sorteados de novo.");
            parada.Ficha = $"{titulo}\n{p.W:0.0} × {p.D:0.0} m, {p.andares} andares; {(p.hospital ? "o hospital" : "o prédio")} de um lote de {cidade}; " +
                           $"{tri:N0} triângulos com os móveis\n{andares}";
            Paradas.Add(parada);
        }

        // a loja ou o prédio no lote de frente para a rua, com a frente perto da calçada (lado 1: o norte; -1: o sul, girado)
        GameObject PorNoLote(string nome, MalhaDeObra vis, MalhaDeObra col, float W, float D, int lado)
        {
            float x = lado > 0 ? _norte : _sul;
            float cx = x + W / 2f, cz = lado * (Calcada + 0.4f + D / 2f);
            var go = Objeto(nome, raiz, Malha(vis, nome), Malha(col, nome + " (colisão)"), new Vector3(cx, 0f, cz), lado > 0 ? 0f : 180f);
            Construcoes.Add(go);
            triangulos += vis.Triangulos;
            if (lado > 0) _norte = x + W + Entre; else _sul = x + W + Entre;
            return go;
        }

        // na calçada, de frente para a porta (ou o meio da frente)
        Parada NovaParada(GameObject go, int lado, float W, float xDaPorta = 0f)
        {
            var porta = go.transform.TransformPoint(new Vector3(xDaPorta, 0f, 0f));
            return new Parada
            {
                Giro = lado > 0 ? 0f : 180f, Limites = Limites(go),
                Chegada = new Vector3(porta.x, 0.2f, lado * (Calcada - 1.2f)),
            };
        }

        static Bounds Limites(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        // ------------------------------------------------------------------ o mostruário da mobília nova

        /// <summary>As escalas que o K troca nos móveis do mostruário: a escolhida para o jogo é 1,30 (29/10/2026; as
        /// construções estão em 1,15).</summary>
        static readonly float[] Escalas = { 1f, 1.15f, 1.3f };
        int _escala = 2;
        /// <summary>O piso do mostruário e onde ele começa (a frente, em z).</summary>
        const float PisoDoMostruario = 0.15f, FrenteDoMostruario = 7.5f;
        /// <summary>O espaço entre os móveis, entre a fileira da parede e a do meio e entre os cômodos (com os móveis na
        /// maior escala).</summary>
        const float EntreMoveis = 0.7f, EntreFileiras = 1.6f, EntreComodos = 2f;

        sealed class Exposto
        {
            public CatalogoDaMobilia.Tipo tipo;
            public GameObject prefab;
            public Bounds tamanho;
            public int perto, longe;
            // o que abre ("porta", "gaveta", "tampa", "forno"), uma vez por parte
            public readonly List<string> partes = new List<string>();
            // o plano: o meio em x (a partir da esquerda do cômodo)
            public float x;
        }

        sealed class Sala
        {
            public CatalogoDaMobilia.Grupo grupo;
            public readonly List<Exposto> expostos = new List<Exposto>();
            public float largura, fundoDaParede, fundoDoMeio;
            public Parada parada;
        }

        readonly List<Sala> _comodos = new List<Sala>();
        readonly List<Transform> _suportes = new List<Transform>();

        public override void Teclas(Keyboard k)
        {
            if (k.kKey.wasPressedThisFrame && _suportes.Count > 0) TrocarEscala();
        }

        public override string AjudaDasTeclas => _suportes.Count > 0
            ? $"K a escala dos móveis do mostruário (agora ×{Escalas[_escala]:0.00}) · C troca a cor do móvel na mira" : null;

        /// <summary>A próxima escala dos móveis do mostruário (1,00, 1,15 e 1,30).</summary>
        public void TrocarEscala()
        {
            _escala = (_escala + 1) % Escalas.Length;
            Escalar();
        }

        void Escalar()
        {
            foreach (var s in _suportes) s.localScale = Vector3.one * Escalas[_escala];
            foreach (var c in _comodos) c.parada.Ficha = FichaDoComodo(c);
        }

        // o tamanho do móvel (o corpo e as partes que abrem, fechadas, sem o de longe) e os triângulos, do prefab
        static Exposto Medir(CatalogoDaMobilia.Tipo tipo, GameObject prefab)
        {
            var e = new Exposto { tipo = tipo, prefab = prefab };
            bool primeiro = true;
            var paraARaiz = prefab.transform.worldToLocalMatrix;
            foreach (var f in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                if (f.sharedMesh == null) continue;
                int tri = (int)(f.sharedMesh.GetIndexCount(0) / 3);
                if (f.name.EndsWith("_Longe")) { e.longe += tri; continue; }
                e.perto += tri;
                var M = paraARaiz * f.transform.localToWorldMatrix;
                var b = f.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var canto = new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1);
                    var c = M.MultiplyPoint3x4(b.center + Vector3.Scale(b.extents, canto));
                    if (primeiro) { e.tamanho = new Bounds(c, Vector3.zero); primeiro = false; }
                    else e.tamanho.Encapsulate(c);
                }
            }
            // o nome sem o artigo ("a porta": "porta")
            foreach (var p in prefab.GetComponentsInChildren<ParteQueAbre>(true)) e.partes.Add(p.Nome.Substring(2));
            return e;
        }

        // um suporte, que escala com o K, com os móveis dentro
        Transform Suporte(string nome, Vector3 lugar, float giro)
        {
            var s = Novo(nome, raiz).transform;
            s.SetLocalPositionAndRotation(lugar, Quaternion.Euler(0f, giro, 0f));
            _suportes.Add(s);
            return s;
        }

        // o prefab no suporte, com o MovelDoMostruario (o C troca a cor)
        GameObject Instanciar(GameObject prefab, Transform pai, Vector3 local, float giro)
        {
            var go = Instantiate(prefab, pai);
            go.name = prefab.name;
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.hideFlags = HideFlags.DontSave;
            go.transform.SetLocalPositionAndRotation(local, Quaternion.Euler(0f, giro, 0f));
            go.AddComponent<MovelDoMostruario>().Guardar(prefab.name);
            return go;
        }

        /// <summary>
        /// O mostruário: um cômodo aberto para a rua no lado norte para cada grupo do catálogo (a sala, o quarto...), lado a
        /// lado, o último terminando em x = -14 (antes das casas), cada um uma parada. Em cada um: o piso de taco, a parede
        /// do fundo com a porta das construções (à esquerda), as paredes dos lados e os móveis: os da parede encostados no
        /// fundo, os pendurados (os quadros) na parede, em cima dos baixos, os soltos numa fileira no meio (a mesa com as
        /// cadeiras em volta) e os que vão em cima de outro (a TV no rack, o abajur na mesa de canto). Devolve onde o
        /// primeiro começa (em x), ou 0 sem a mobília nova.
        /// </summary>
        float Mostruario(Medidas M)
        {
            _comodos.Clear();
            _suportes.Clear();
            var prefabs = (mobiliaNova ?? new GameObject[0]).Where(p => p != null).GroupBy(p => p.name).ToDictionary(g => g.Key, g => g.First());
            if (prefabs.Count == 0) return 0f;
            foreach (CatalogoDaMobilia.Grupo g in System.Enum.GetValues(typeof(CatalogoDaMobilia.Grupo)))
            {
                var c = new Sala { grupo = g };
                foreach (var t in CatalogoDaMobilia.Tipos)
                    if (t.grupo == g && prefabs.TryGetValue(t.modelo, out var pf)) c.expostos.Add(Medir(t, pf));
                if (c.expostos.Count == 0) continue;
                Planejar(c, M);
                _comodos.Add(c);
            }
            float x = -14f - _comodos.Sum(c => c.largura) - EntreComodos * (_comodos.Count - 1), inicio = x;
            foreach (var c in _comodos)
            {
                MontarComodo(c, x, M);
                x += c.largura + EntreComodos;
            }
            Escalar();
            resumo.AppendLine($"O mostruário da mobília nova: {_comodos.Count} cômodos, {_comodos.Sum(c => c.expostos.Count)} móveis, " +
                              $"de x = {inicio:0} a -14.");
            return inicio;
        }

        static float Topo(Exposto e) => e.tamanho.max.y;

        // onde cada móvel vai em x (a partir da esquerda do cômodo), a largura e os fundos das duas fileiras
        void Planejar(Sala c, Medidas M)
        {
            float maior = Escalas[Escalas.Length - 1];
            float porta = 1f + M.porta + 1f;
            float parede = porta, meio = 1f;
            var altos = new List<(float x0, float x1, float topo)>();
            c.fundoDaParede = 0.5f;
            c.fundoDoMeio = 0.5f;
            // o espaço depois de um móvel: nenhum entre dois "juntos" (os módulos da cozinha, que formam a bancada)
            float Depois(List<Exposto> lista, int i, float vao) =>
                lista[i].tipo.junto && i + 1 < lista.Count && lista[i + 1].tipo.junto ? 0f : vao;
            var naParede = c.expostos.Where(o => o.tipo.lugar == CatalogoDaMobilia.Lugar.Parede).ToList();
            for (int i = 0; i < naParede.Count; i++)
            {
                var e = naParede[i];
                float w = e.tamanho.size.x * maior;
                e.x = parede + w / 2f;
                parede += w + Depois(naParede, i, EntreMoveis);
                // a mesa encostada na parede com a cadeira na frente (a de escritório): o fundo dela e o da cadeira
                var cad = e.tipo.cadeira != null ? c.expostos.FirstOrDefault(o => o.tipo.modelo == e.tipo.cadeira) : null;
                float fundo = e.tamanho.size.z + (cad != null ? cad.tamanho.size.z * 0.8f : 0f);
                c.fundoDaParede = Mathf.Max(c.fundoDaParede, fundo * maior);
                altos.Add((e.x - w / 2f - 0.15f, e.x + w / 2f + 0.15f, Topo(e)));
            }
            foreach (var e in c.expostos.Where(o => o.tipo.lugar == CatalogoDaMobilia.Lugar.Meio))
            {
                float w = e.tamanho.size.x * maior;
                float d = e.tamanho.size.z;
                var cad = e.tipo.cadeira != null ? c.expostos.FirstOrDefault(o => o.tipo.modelo == e.tipo.cadeira) : null;
                if (cad != null) { d += 2f * cad.tamanho.size.z; if (e.tipo.redonda) w += 2f * cad.tamanho.size.z * maior; }
                e.x = meio + w / 2f;
                meio += w + EntreMoveis;
                c.fundoDoMeio = Mathf.Max(c.fundoDoMeio, d * maior);
            }
            // os pendurados: da esquerda para a direita, só em cima dos móveis mais baixos que eles (os juntos, colados)
            float alto = porta;
            var penduras = c.expostos.Where(o => o.tipo.lugar == CatalogoDaMobilia.Lugar.Alto).ToList();
            for (int i = 0; i < penduras.Count; i++)
            {
                var e = penduras[i];
                float w = e.tamanho.size.x * maior;
                bool livre;
                do
                {
                    livre = true;
                    foreach (var (x0, x1, topo) in altos)
                        if (topo > e.tipo.altura - 0.05f && alto < x1 && alto + w > x0) { alto = x1 + 0.05f; livre = false; }
                } while (!livre);
                e.x = alto + w / 2f;
                alto += w + Depois(penduras, i, 0.6f);
            }
            c.largura = Mathf.Max(Mathf.Max(parede, meio), alto) + 0.6f;
        }

        void MontarComodo(Sala c, float xl, Medidas M)
        {
            var parada = new Parada { Giro = 0f };
            parada.Pisos.Add(PisoDoMostruario);
            parada.NomesDosPisos.Add("O mostruário: " + CatalogoDaMobilia.NomeDoGrupo(c.grupo));
            c.parada = parada;
            float fundo = 0.4f + c.fundoDaParede + EntreFileiras + c.fundoDoMeio + 1.4f;
            float z0 = FrenteDoMostruario, zp = z0 + fundo;
            float zMeio = zp - 0.2f - c.fundoDaParede - EntreFileiras - c.fundoDoMeio / 2f;
            // os móveis postos: o suporte de cada modelo e o meio do pé dele no suporte (para os que vão em cima)
            var postos = new Dictionary<string, (Transform suporte, Vector3 meio)>();
            string Rotulo(Exposto e) => e.tipo.nome;
            foreach (var e in c.expostos)
            {
                var b = e.tamanho;
                switch (e.tipo.lugar)
                {
                    case CatalogoDaMobilia.Lugar.Parede:
                    {
                        // o suporte na parede, girado: as costas do móvel nele e a frente para a rua (-z)
                        var s = Suporte(e.tipo.modelo, new Vector3(xl + e.x, PisoDoMostruario, zp - 0.02f), 180f);
                        Instanciar(e.prefab, s, new Vector3(-b.center.x, 0f, -b.min.z), 0f);
                        // na mesa com o vão da cadeira, o que vai em cima e a cadeira ficam na frente do vão
                        float vx = e.tipo.vao != 0f ? e.tipo.vao - b.center.x : 0f;
                        postos[e.tipo.modelo] = (s, new Vector3(vx, 0f, b.size.z / 2f));
                        parada.Comodos.Add((new Vector3(xl + e.x, PisoDoMostruario + 0.1f, zp - 1.2f), 0, Rotulo(e)));
                        var cad = e.tipo.cadeira != null ? c.expostos.FirstOrDefault(o => o.tipo.modelo == e.tipo.cadeira) : null;
                        if (cad != null) CadeiraNaFrente(s, b, cad, vx);
                        break;
                    }
                    case CatalogoDaMobilia.Lugar.Alto:
                    {
                        var s = Suporte(e.tipo.modelo, new Vector3(xl + e.x, PisoDoMostruario, zp - 0.02f), 180f);
                        Instanciar(e.prefab, s, new Vector3(-b.center.x, e.tipo.altura, -b.min.z), 0f);
                        break;
                    }
                    case CatalogoDaMobilia.Lugar.Meio:
                    {
                        var s = Suporte(e.tipo.modelo, new Vector3(xl + e.x, PisoDoMostruario, zMeio), 180f);
                        Instanciar(e.prefab, s, new Vector3(-b.center.x, 0f, -b.center.z), 0f);
                        postos[e.tipo.modelo] = (s, Vector3.zero);
                        parada.Comodos.Add((new Vector3(xl + e.x, PisoDoMostruario + 0.1f, zMeio), 0, Rotulo(e)));
                        var cad = e.tipo.cadeira != null ? c.expostos.FirstOrDefault(o => o.tipo.modelo == e.tipo.cadeira) : null;
                        if (cad != null) Cadeiras(s, b, cad, e.tipo.cadeiras, e.tipo.redonda);
                        break;
                    }
                }
            }
            foreach (var e in c.expostos.Where(o => o.tipo.lugar == CatalogoDaMobilia.Lugar.Sobre))
                foreach (var (modelo, altura) in e.tipo.sobre)
                    if (postos.TryGetValue(modelo, out var posto))
                        Instanciar(e.prefab, posto.suporte, new Vector3(posto.meio.x - e.tamanho.center.x, altura, posto.meio.z - e.tamanho.center.z), 0f);

            // o piso, as paredes (a do fundo com a porta das casas, à esquerda), o batente e o rodapé
            float x0 = xl, x1 = xl + c.largura, cxm = (x0 + x1) / 2f;
            float topo = PisoDoMostruario + M.altura, esp = 0.2f, meioY = (PisoDoMostruario + topo) / 2f;
            float portaX = x0 + 1f + M.porta / 2f;
            chao.Caixa(new Vector3(cxm, PisoDoMostruario / 2f, (z0 + zp) / 2f), new Vector3(x1 - x0, PisoDoMostruario, zp - z0), "Taco", colide: true);
            float p0 = portaX - M.porta / 2f, p1 = portaX + M.porta / 2f, verga = PisoDoMostruario + M.alturaPorta;
            chao.Caixa(new Vector3((x0 + p0) / 2f, meioY, zp + esp / 2f), new Vector3(p0 - x0, M.altura, esp), "ParedeInterna", colide: true);
            chao.Caixa(new Vector3((p1 + x1) / 2f, meioY, zp + esp / 2f), new Vector3(x1 - p1, M.altura, esp), "ParedeInterna", colide: true);
            chao.Caixa(new Vector3(portaX, (verga + topo) / 2f, zp + esp / 2f), new Vector3(M.porta, topo - verga, esp), "ParedeInterna", colide: true);
            foreach (float bx in new[] { p0 + 0.04f, p1 - 0.04f })
                chao.Caixa(new Vector3(bx, (PisoDoMostruario + verga) / 2f, zp + esp / 2f), new Vector3(0.08f, M.alturaPorta, esp + 0.04f), "Batente", colide: true);
            chao.Caixa(new Vector3(portaX, verga - 0.04f, zp + esp / 2f), new Vector3(M.porta, 0.08f, esp + 0.04f), "Batente");
            foreach (float lx in new[] { x0 - esp / 2f, x1 + esp / 2f })
                chao.Caixa(new Vector3(lx, meioY, (z0 + zp + esp) / 2f), new Vector3(esp, M.altura, zp + esp - z0), "ParedeInterna", colide: true);
            chao.Caixa(new Vector3((x0 + p0) / 2f, PisoDoMostruario + 0.05f, zp - 0.01f), new Vector3(p0 - x0, 0.1f, 0.02f), "Batente");
            chao.Caixa(new Vector3((p1 + x1) / 2f, PisoDoMostruario + 0.05f, zp - 0.01f), new Vector3(x1 - p1, 0.1f, 0.02f), "Batente");
            parada.Comodos.Add((new Vector3(portaX, PisoDoMostruario + 0.1f, zp - 1.2f), 0, $"a porta das casas\n{M.porta:0.00} × {M.alturaPorta:0.00}"));

            parada.Limites = new Bounds(new Vector3(cxm, topo / 2f, (z0 + zp) / 2f), new Vector3(x1 - x0 + 2f * esp, topo, zp - z0 + esp));
            parada.Chegada = new Vector3(cxm, 0.2f, Calcada - 1.2f);
            Paradas.Add(parada);
        }

        // as cadeiras em volta da mesa (no suporte dela, com a mesa no meio): duas em cada lado comprido (seis: três), um
        // pouco embaixo dela, a frente da cadeira para a mesa; uma puxada e torta. Na redonda, em volta, por igual.
        void Cadeiras(Transform suporte, Bounds mesa, Exposto cadeira, int n, bool redonda)
        {
            float W = mesa.size.x, D = mesa.size.z, cd = cadeira.tamanho.size.z;
            float z = D / 2f + cd / 2f - 0.14f;
            if (redonda)
            {
                for (int k = 0; k < n; k++)
                {
                    bool puxada = k == 0;
                    float a = 360f * k / n, r = z + (puxada ? 0.3f : 0f);
                    // a cadeira olha para +z (a frente dela): girada a + 180, ela fica no ângulo a olhando para o meio
                    var giro = Quaternion.Euler(0f, a, 0f);
                    var pos = giro * new Vector3(0f, 0f, -r) - giro * cadeira.tamanho.center;
                    Instanciar(cadeira.prefab, suporte, new Vector3(pos.x, 0f, pos.z), a + (puxada ? 12f : 0f));
                }
                return;
            }
            int porLado = Mathf.Max(1, n / 2);
            for (int lado = 0; lado < 2; lado++)
                for (int k = 0; k < porLado; k++)
                {
                    float x = (k + 0.5f) / porLado * W - W / 2f;
                    bool puxada = lado == 0 && k == porLado - 1;
                    var pos = new Vector3(x - cadeira.tamanho.center.x, 0f, (lado == 0 ? -1f : 1f) * (z + (puxada ? 0.32f : 0f)));
                    Instanciar(cadeira.prefab, suporte, pos, (lado == 0 ? 0f : 180f) + (puxada ? -14f : 0f));
                }
        }

        // a cadeira na frente da mesa encostada na parede (no suporte dela, as costas da mesa em z = 0), no vão (x), um
        // pouco embaixo do tampo e virada de leve: girada 180°, a frente dela (+z) olha para a mesa
        void CadeiraNaFrente(Transform suporte, Bounds mesa, Exposto cadeira, float x)
        {
            var c = cadeira.tamanho.center;
            float z = mesa.size.z + cadeira.tamanho.size.z / 2f - 0.15f;
            Instanciar(cadeira.prefab, suporte, new Vector3(x + c.x, 0f, z + c.z), 172f);
        }

        string FichaDoComodo(Sala c)
        {
            float k = Escalas[_escala];
            var M = Medidas.DoJogo;
            var sb = new StringBuilder();
            sb.AppendLine($"A mobília nova: {CatalogoDaMobilia.NomeDoGrupo(c.grupo)} ({c.expostos.Count} modelos), os móveis em ×{k:0.00} " +
                          "(o K troca: 1,00, 1,15 e 1,30; no jogo, 1,30). Mire num móvel: o C troca a cor, o E abre as portas e as gavetas.");
            sb.AppendLine($"A porta do fundo é a das casas ({M.porta:0.00} × {M.alturaPorta:0.00} m), e as paredes têm o pé-direito delas ({M.altura:0.00} m). " +
                          "Medidas no tamanho real (largura × fundo × altura); triângulos de perto / de longe:");
            foreach (var e in c.expostos)
            {
                var t = e.tamanho.size;
                var partes = e.partes.GroupBy(p => p).Select(g => $"{g.Count()} {g.Key}{(g.Count() > 1 ? "s" : "")}").ToList();
                int n = e.tipo.variacoes?.Length ?? 1;
                sb.AppendLine($"    {e.tipo.nome}: {t.x:0.00} × {t.z:0.00} × {t.y:0.00} m; {e.perto:N0} / {e.longe:N0}" +
                              (partes.Count > 0 ? "; abre: " + string.Join(" e ", partes) : "") + (n > 1 ? $"; {n} cores" : ""));
            }
            return sb.ToString();
        }

        // ------------------------------------------------------------------ a ficha

        static string NomeDoAndar(int a) => a < 0 ? "Porão" : a == 0 ? "Térreo" : $"{a}º andar";

        static readonly Dictionary<string, string> NomesDosComodos = new Dictionary<string, string>
        {
            { "porao", "porão" }, { "deposito", "depósito" }, { "salao", "salão" }, { "acougue", "açougue" },
            { "escritorio", "escritório" }, { "recepcao", "recepção" }, { "emergencia", "emergência" },
            { "consultorio", "consultório" }, { "raiox", "raio X" }, { "posto", "posto de enfermagem" },
        };

        static string NomeDoComodo(string tipo) => NomesDosComodos.TryGetValue(tipo, out var n) ? n : tipo;

        static readonly Dictionary<string, string> NomesDosMoveis = new Dictionary<string, string>
        {
            { "sofa", "sofá" }, { "fogao", "fogão" }, { "armario", "armário" }, { "comoda", "cômoda" },
            { "maquina de lavar", "máquina de lavar" }, { "balcao", "balcão" }, { "gondola", "gôndola" },
            { "estante de aco", "estante de aço" }, { "armario de armas", "armário de armas" },
            { "caixas de municao", "caixas de munição" }, { "mesa de escritorio", "mesa de escritório" },
            { "armario de remedios", "armário de remédios" },
        };

        static string NomeDoMovel(string tipo) => NomesDosMoveis.TryGetValue(tipo, out var n) ? n : tipo;

        static string Tamanho(Comodo k) => $"{Mathf.Abs(k.x1 - k.x0):0.0} × {Mathf.Abs(k.z1 - k.z0):0.0} m";

        static string Rotulo(Comodo k) => $"{NomeDoComodo(k.tipo)}\n{Mathf.Abs(k.x1 - k.x0):0.0} × {Mathf.Abs(k.z1 - k.z0):0.0}";

        // os móveis com o pé dentro do cômodo, no piso dele
        static IEnumerable<PontoDeMovel> MoveisDo(Comodo k, List<PontoDeMovel> moveis, float piso)
        {
            float x0 = Mathf.Min(k.x0, k.x1), x1 = Mathf.Max(k.x0, k.x1), z0 = Mathf.Min(k.z0, k.z1), z1 = Mathf.Max(k.z0, k.z1);
            return moveis.Where(m => Mathf.Abs(m.centro.y - piso) < 0.4f && m.centro.x >= x0 && m.centro.x <= x1 && m.centro.z >= z0 && m.centro.z <= z1);
        }

        // "cama, guarda-roupa, criado-mudo ×2", na ordem em que aparecem
        static string Contar(IEnumerable<PontoDeMovel> moveis)
        {
            var contagem = new List<(string nome, int n)>();
            foreach (var m in moveis)
            {
                string nome = NomeDoMovel(m.tipo);
                int i = contagem.FindIndex(c => c.nome == nome);
                if (i < 0) contagem.Add((nome, 1)); else contagem[i] = (nome, contagem[i].n + 1);
            }
            return string.Join(", ", contagem.Select(c => c.n > 1 ? $"{c.nome} ×{c.n}" : c.nome));
        }

        // os cômodos de um andar e os móveis de cada um: um por linha, ou juntos por tipo (os andares dos prédios, com muitos
        // apartamentos iguais)
        static void Andar(StringBuilder sb, string titulo, List<Comodo> comodos, List<PontoDeMovel> moveis, float piso, bool juntar)
        {
            if (comodos.Count == 0) return;
            sb.AppendLine(titulo + ":");
            if (!juntar)
            {
                foreach (var k in comodos)
                {
                    string nomes = Contar(MoveisDo(k, moveis, piso));
                    sb.AppendLine($"    {NomeDoComodo(k.tipo)} {Tamanho(k)}{(nomes.Length > 0 ? ": " + nomes : "")}");
                }
                return;
            }
            foreach (var g in comodos.GroupBy(k => k.tipo))
            {
                var areas = g.Select(k => Mathf.Abs((k.x1 - k.x0) * (k.z1 - k.z0))).ToList();
                string nomes = Contar(g.SelectMany(k => MoveisDo(k, moveis, piso)));
                string area = Mathf.Round(areas.Min()) < Mathf.Round(areas.Max()) ? $"{areas.Min():0} a {areas.Max():0} m²" : $"{areas[0]:0} m²";
                sb.AppendLine($"    {NomeDoComodo(g.Key)} ×{g.Count()} ({area}){(nomes.Length > 0 ? ": " + nomes : "")}");
            }
        }
    }
}
