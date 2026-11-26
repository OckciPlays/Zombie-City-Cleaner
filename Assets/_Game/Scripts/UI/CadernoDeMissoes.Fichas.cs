using System.Collections.Generic;
using UnityEngine;
using ZombieCityCleaner.Fauna;
using ZombieCityCleaner.Loot;
using ZombieCityCleaner.Player;
using ZombieCityCleaner.Zombies;

namespace ZombieCityCleaner.UI
{
    /// <summary>
    /// O caderno do P (25/11/2026, a etapa 9 do plano da v17, a pedido: "O P abre um caderno de duas páginas, com a letra de
    /// mão e as abas de papel"): as abas de papel em cima do caderno da aba ANOTAÇÕES.
    /// <list type="bullet">
    /// <item>Missões: as fases, com as submissões riscadas (CadernoDeMissoes.Anotacoes.cs);</item>
    /// <item>Zumbis: cada tipo que ele já viu (InventarioJogador.Caderno.cs), com o desenho a lápis e a anotação à mão;</item>
    /// <item>Animais: cada espécie que ele caçou, com o desenho, quantos e o que a carcaça dá;</item>
    /// <item>Construções: os kits de montar das receitas que ele sabe, com o que gastam e onde se fazem;</item>
    /// <item>Receitas: as outras receitas que ele sabe, do mesmo jeito.</item>
    /// </list>
    /// Os desenhos são fotos de lado dos modelos do jogo passadas num filtro de lápis (Trabalho-Claude\caderno_desenhos\lapis.py),
    /// em Resources/Caderno/Desenhos. Duas fichas por página; as setas embaixo (ou ← e →, ou a roda do mouse) viram a folha,
    /// com o som do papel.
    /// </summary>
    public partial class CadernoDeMissoes
    {
        enum AbaDoCaderno { Missoes, Zumbis, Animais, Construcoes, Receitas }

        static AbaDoCaderno _abaDoCaderno;
        static readonly int[] _folha = new int[5];   // a folha aberta (o par de páginas) de cada aba
        static int _folhasDaAba = 1;
        static readonly string[] NomesDasAbas = { "Missões", "Zumbis", "Animais", "Construções", "Receitas" };
        // as abas de trás, cada uma de uma cor de papel (as divisórias de um caderno de verdade)
        static readonly Color[] CoresDasAbas =
        {
            new Color(0.93f, 0.86f, 0.58f), new Color(0.88f, 0.66f, 0.64f), new Color(0.66f, 0.8f, 0.62f),
            new Color(0.62f, 0.74f, 0.88f), new Color(0.93f, 0.72f, 0.5f),
        };

        /// <summary>A aba do caderno aberta (0 Missões, 1 Zumbis, 2 Animais, 3 Construções, 4 Receitas).</summary>
        public static int AbaAberta => (int)_abaDoCaderno;

        /// <summary>Abre o caderno nessa aba (o teste e quem quiser ir direto).</summary>
        public static void IrParaAAba(int aba)
        {
            if (aba < 0 || aba >= NomesDasAbas.Length || aba == (int)_abaDoCaderno) return;
            _abaDoCaderno = (AbaDoCaderno)aba;
            TocarAqui(SomDaPagina, 0.45f);
        }

        /// <summary>Vira a folha da aba aberta (+1 a próxima, -1 a de antes), com o som do papel.</summary>
        public static void Virar(int lado)
        {
            int a = (int)_abaDoCaderno;
            int nova = Mathf.Clamp(_folha[a] + lado, 0, Mathf.Max(0, _folhasDaAba - 1));
            if (nova == _folha[a]) return;
            _folha[a] = nova;
            TocarAqui(SomDaPagina, 0.45f);
        }

        // ------------------------------------------------------------------ a geometria (o desenho e o clique usam a mesma)

        static void Geometria(Rect area, float u, out Rect faixa, out Rect caderno, out Rect esq, out Rect dir)
        {
            float alturaDasAbas = 30f * u;
            faixa = new Rect(area.x, area.y, area.width, alturaDasAbas);
            caderno = new Rect(area.x, area.y + alturaDasAbas, area.width, area.height - alturaDasAbas);
            float vao = 16f * u;
            float larg = (caderno.width - vao) * 0.5f;
            esq = new Rect(caderno.x, caderno.y, larg, caderno.height);
            dir = new Rect(caderno.x + larg + vao, caderno.y, larg, caderno.height);
        }

        static Rect RetanguloDaAba(int i, Rect faixa, float u)
        {
            float w = 128f * u, passo = 134f * u;
            return new Rect(faixa.x + 24f * u + i * passo, faixa.y + 2f * u, w, faixa.height + 2f * u);
        }

        static Rect SetaAnterior(Rect esq, float u) => new Rect(esq.x + 6f * u, esq.yMax - 32f * u, 30f * u, 28f * u);
        static Rect SetaProxima(Rect dir, float u) => new Rect(dir.xMax - 36f * u, dir.yMax - 32f * u, 30f * u, 28f * u);

        /// <summary>O clique na aba ANOTAÇÕES (a TelaDoInventario.Processar): uma aba de papel ou uma seta. Devolve se pegou.</summary>
        public static bool CliqueNoCaderno(Vector2 m, Rect area, float u)
        {
            Geometria(area, u, out var faixa, out _, out var esq, out var dir);
            for (int i = 0; i < NomesDasAbas.Length; i++)
                if (RetanguloDaAba(i, faixa, u).Contains(m))
                {
                    IrParaAAba(i);
                    return true;
                }
            int a = (int)_abaDoCaderno;
            if (_folha[a] > 0 && SetaAnterior(esq, u).Contains(m)) { Virar(-1); return true; }
            if (_folha[a] < _folhasDaAba - 1 && SetaProxima(dir, u).Contains(m)) { Virar(1); return true; }
            return false;
        }

        // ------------------------------------------------------------------ as abas e os números das páginas

        // as abas que não estão abertas: atrás da capa (só a ponta de cima aparece), cada uma na cor dela
        static void AbasDeTras(Rect faixa, float u)
        {
            for (int i = 0; i < NomesDasAbas.Length; i++)
            {
                if (i == (int)_abaDoCaderno) continue;
                var r = RetanguloDaAba(i, faixa, u);
                r.y += 4f * u;
                GUI.color = CoresDasAbas[i] * new Color(0.92f, 0.92f, 0.92f, 1f);
                GUI.DrawTexture(r, Papel);
                GUI.color = new Color(0f, 0f, 0f, 0.18f);
                GUI.DrawTexture(new Rect(r.x, r.y, r.width, Mathf.Max(1f, 1f * u)), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(r.xMax - Mathf.Max(1f, 1f * u), r.y, Mathf.Max(1f, 1f * u), r.height), Texture2D.whiteTexture);
                // (o nome no pedaço que aparece acima da capa: mais baixo, a capa cortava as letras)
                GUI.color = ComAlfa(Tinta, 0.72f);
                GUI.Label(new Rect(r.x, r.y - 2f * u, r.width, faixa.height - 12f * u), NomesDasAbas[i], _nomeDeTras);
            }
        }

        // a aba aberta: do papel da folha, na frente da capa, colada nela
        static void AbaDaFrente(Rect faixa, Rect caderno, float u)
        {
            int i = (int)_abaDoCaderno;
            var r = RetanguloDaAba(i, faixa, u);
            r.yMax = caderno.y + 2f * u;
            GUI.color = Color.Lerp(Color.white, CoresDasAbas[i], 0.35f);
            GUI.DrawTexture(r, Papel);
            GUI.color = new Color(0f, 0f, 0f, 0.14f);
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, Mathf.Max(1f, 1f * u)), Texture2D.whiteTexture);
            GUI.color = Tinta;
            GUI.Label(new Rect(r.x, r.y + 1f * u, r.width, faixa.height - 4f * u), NomesDasAbas[i], _nomeDaAba);
        }

        // os números embaixo, nos cantos de fora, e as setas de virar a folha
        static void NumerosDasPaginas(Rect esq, Rect dir, float u)
        {
            int a = (int)_abaDoCaderno;
            _folha[a] = Mathf.Clamp(_folha[a], 0, Mathf.Max(0, _folhasDaAba - 1));
            int f = _folha[a];
            GUI.color = ComAlfa(Tinta, 0.6f);
            GUI.Label(new Rect(esq.x + 38f * u, esq.yMax - 30f * u, 40f * u, 24f * u), (2 * f + 1).ToString(), _numeroDaPagina);
            GUI.Label(new Rect(dir.xMax - 78f * u, dir.yMax - 30f * u, 40f * u, 24f * u), (2 * f + 2).ToString(), _numeroDaPagina);
            GUI.color = Tinta;
            if (f > 0) GUI.Label(SetaAnterior(esq, u), "«", _faseDaPagina);
            if (f < _folhasDaAba - 1) GUI.Label(SetaProxima(dir, u), "»", _faseDaPagina);
        }

        // ------------------------------------------------------------------ os zumbis

        /// <summary>Os bits do caderno de todos os zumbis (os 4 tipos e os 6 chefes), na ordem das páginas.</summary>
        public static readonly int[] BitsDosZumbis = { 0, 1, 2, 3, 8, 9, 10, 11, 12, 13 };

        /// <summary>O bit do caderno de um zumbi: o tipo (0 a 3) ou, num chefe, 8 + qual.</summary>
        public static int BitDoZumbi(TipoDeZumbi tipo, QualChefe chefe) => tipo == TipoDeZumbi.Chefe ? 8 + (int)chefe : (int)tipo;

        /// <summary>O nome do zumbi no caderno, com o artigo ("o Corredor").</summary>
        public static string NomeDoZumbi(int bit) => bit switch
        {
            0 => "o Oco", 1 => "o Corredor", 2 => "o Long", 3 => "o Brutamonte",
            8 => "o Blindado", 9 => "o Agregado A", 10 => "o Agregado B", 11 => "o Alongado", 12 => "o Longo", 13 => "o Condutor",
            _ => "um zumbi",
        };

        static string DesenhoDoZumbi(int bit) => bit switch
        {
            0 => "zumbi_oco", 1 => "zumbi_corredor", 2 => "zumbi_long", 3 => "zumbi_brutamonte",
            8 => "chefe_blindado", 9 => "chefe_agregadoa", 10 => "chefe_agregadob", 11 => "chefe_alongado", 12 => "chefe_longo", 13 => "chefe_condutor",
            _ => null,
        };

        // a anotação à mão de cada um (o que dá para ver e o que aprendemos apanhando)
        static string NotaDoZumbi(int bit) => bit switch
        {
            0 => "O mais comum. Lento, anda em bando atrás do barulho. Mire na cabeça. No sol vira pedra: aí só o machado ou o bastão quebram.",
            1 => "Um Oco mais antigo. Frágil, mas rápido demais: dá o bote nos últimos metros. Aparece mais à noite, com as hordas.",
            2 => "O cuspidor. Fica longe, entre 14 e 30 m, e cospe uma gosma que prende. Odeia a luz. O papo é o ponto fraco.",
            3 => "Enorme, só um por mundo. O ponto fraco é o olho nas costas. Ferido, some na terra e volta depois.",
            8 => "Vários Ocos num casco de calos, uns 8 m. Agarra, aperta e arremessa. Agachado, só a cabeça sente o tiro. Morto, vira dois.",
            9 => "Metade do Blindado, uns 3,5 m. A mão de carne estica, agarra de longe e puxa a gente até ele.",
            10 => "A outra metade do Blindado. De longe, a mão que estica e puxa; de perto, o golpe do outro braço.",
            11 => "Uns 9 m. O braço comprido alcança longe, agarra e arremessa por cima da cabeça. De perto, varre o chão.",
            12 => "O que sobra do Alongado. Agachado e rápido: cospe ácido e dá o bote. O papo é o ponto fraco.",
            13 => "Uns 8 m. O grito chama a horda e deixa os de perto mais rápidos. A clava de corpos varre. O ponto fraco é o rosto no peito.",
            _ => "",
        };

        static string TipoDoZumbi(int bit) => bit >= 8 ? "chefe" : bit == 0 ? "o comum" : "especial";

        static void OsZumbis(Rect esq, Rect dir, float topo, float margem, float altura, InventarioJogador inv, float u)
        {
            var fichas = new List<Ficha>();
            if (inv != null && inv.IsSpawned)
            {
                var p = inv.Progresso.Value;
                foreach (int bit in BitsDosZumbis)
                    if (p.ViuOZumbi(bit))
                        fichas.Add(new Ficha { titulo = Maiuscula(NomeDoZumbi(bit)), nota = NotaDoZumbi(bit), detalhe = TipoDoZumbi(bit), desenho = DesenhoDoZumbi(bit) });
            }
            Fichas(esq, dir, topo, margem, altura, "Zumbis", fichas, "Nenhum zumbi anotado ainda. Quando você vir um de perto, ele entra aqui, desenhado.", u);
        }

        // ------------------------------------------------------------------ os animais

        static string NotaDoBicho(Especie e) => e switch
        {
            Especie.Veado => "Anda em grupos de 4 a 9 na beira da mata. Foge de tudo; o macho grande protege o grupo.",
            Especie.Alce => "Raro e enorme. O macho ferido investe contra a gente. Vive perto da água e nada bem.",
            Especie.Lobo => "Anda em matilha. Mexer com um chama a alcateia toda. De noite caçam quem está sozinho ou ferido.",
            Especie.Coiote => "Um lobo pequeno, em grupos de 3 a 5. Mais fraco que o lobo, mas também caça em bando.",
            Especie.UrsoNegro => "Anda sozinho ou com os filhotes. Avisa antes; às vezes só finge o ataque. Nunca chegue perto do filhote.",
            Especie.UrsoPardo => "O maior bicho daqui. Avisa, finge ou ataca de verdade. A patada derruba e quebra osso.",
            Especie.Coelho => "Pequeno e rápido. Só foge. Pouca carne, mas fácil de achar no campo.",
            Especie.Castor => "Vive no lago em grupos pequenos. Assustado, mergulha. Dá carne, couro e gordura.",
            Especie.Pato => "Em bandos no lago. Levanta voo no primeiro barulho.",
            Especie.Ganso => "Em bandos grandes na beira do lago. Bravo: bica quem chega perto.",
            Especie.Aguia => "Sempre voando alto, sozinha, sobre o lago e a serra.",
            Especie.Urubu => "Em bando, rodando em cima do que morreu. Onde eles estão, tem carcaça.",
            Especie.Sapo => "Na beira do lago, de noite. Canta antes da chuva.",
            Especie.Cascavel => "Sozinha no mato seco. O chocalho avisa: olhe onde pisa.",
            Especie.Guaxinim => "Bicho da noite, mas na cidade anda de dia também. Revira o lixo e as caçambas.",
            Especie.Gamba => "Sai de noite pelos quintais e terrenos baldios. Finge de morto.",
            Especie.Esquilo => "Nas árvores da mata e dos quintais. Rápido demais para valer a bala.",
            Especie.Peru => "Em bandos de 4 a 10 no campo. Corre mais do que parece.",
            Especie.Salmao => "Em cardumes no rio. O urso pesca na correnteza; a lança também pega.",
            _ => "",
        };

        static string DoQueDa(DadosDaEspecie d)
        {
            var partes = new List<string>();
            if (d.carne > 0) partes.Add(d.carne == 1 ? "1 carne" : $"{d.carne} carnes");
            if (d.couro > 0) partes.Add(d.couro == 1 ? "1 couro" : $"{d.couro} couros");
            if (d.gordura > 0) partes.Add(d.gordura == 1 ? "1 gordura" : $"{d.gordura} gorduras");
            return partes.Count == 0 ? "" : "dá " + Juntar(partes);
        }

        static void OsAnimais(Rect esq, Rect dir, float topo, float margem, float altura, InventarioJogador inv, float u)
        {
            var fichas = new List<Ficha>();
            if (inv != null && inv.IsSpawned)
            {
                var p = inv.Progresso.Value;
                for (int e = 0; e < Especies.Quantas; e++)
                {
                    int n = p.Cacados(e);
                    if (n <= 0) continue;
                    var d = Especies.De((Especie)e);
                    string quantos = n >= 255 ? "caçados: 255 ou mais" : n == 1 ? "1 caçado" : $"{n} caçados";
                    string da = DoQueDa(d);
                    fichas.Add(new Ficha
                    {
                        titulo = Maiuscula(d.nome), nota = NotaDoBicho((Especie)e), detalhe = da.Length > 0 ? $"{quantos} · {da}" : quantos,
                        desenho = "bicho_" + ((Especie)e).ToString().ToLowerInvariant(),
                    });
                }
            }
            Fichas(esq, dir, topo, margem, altura, "Animais", fichas, "Nenhum bicho caçado ainda. Cada espécie que você caçar ganha o desenho dela aqui.", u);
        }

        // ------------------------------------------------------------------ as fichas (duas por página)

        struct Ficha
        {
            public string titulo, nota, detalhe, desenho;
        }

        static readonly Dictionary<string, Texture2D> _desenhos = new Dictionary<string, Texture2D>();

        static Texture2D Desenho(string chave)
        {
            if (string.IsNullOrEmpty(chave)) return null;
            if (!_desenhos.TryGetValue(chave, out var t))
            {
                t = Resources.Load<Texture2D>("Caderno/Desenhos/" + chave);
                _desenhos[chave] = t;
            }
            return t;
        }

        const int FichasPorPagina = 2;

        static void Fichas(Rect esq, Rect dir, float topo, float margem, float altura, string titulo, List<Ficha> fichas, string vazio, float u)
        {
            int porFolha = FichasPorPagina * 2;
            _folhasDaAba = Mathf.Max(1, (fichas.Count + porFolha - 1) / porFolha);
            int a = (int)_abaDoCaderno;
            _folha[a] = Mathf.Clamp(_folha[a], 0, _folhasDaAba - 1);
            Cabecalho(esq, margem, titulo, u);
            if (fichas.Count == 0)
            {
                float y = esq.y + topo;
                foreach (var l in Quebrar(vazio, esq.width - margem - 20f * u, _linhaDaPagina))
                {
                    GUI.color = ComAlfa(Tinta, 0.8f);
                    GUI.Label(new Rect(esq.x + margem, y, esq.width - margem, altura), l, _linhaDaPagina);
                    y += altura;
                }
                return;
            }
            float alto = (esq.height - topo - 34f * u) / FichasPorPagina;
            for (int k = 0; k < porFolha; k++)
            {
                int i = _folha[a] * porFolha + k;
                if (i >= fichas.Count) break;
                var pagina = k < FichasPorPagina ? esq : dir;
                int lugar = k % FichasPorPagina;
                var caixa = new Rect(pagina.x + margem, pagina.y + topo + lugar * alto, pagina.width - margem - 14f * u, alto - 6f * u);
                DesenharFicha(caixa, fichas[i], i % 2 == 1, altura, u);
            }
        }

        // uma ficha: o desenho de um lado e, do outro, o nome sublinhado, a anotação e o detalhe
        static void DesenharFicha(Rect caixa, Ficha f, bool desenhoADireita, float altura, float u)
        {
            float lado = Mathf.Min(caixa.height, caixa.width * 0.46f);
            var quadro = new Rect(desenhoADireita ? caixa.xMax - lado : caixa.x - 6f * u, caixa.y + (caixa.height - lado) * 0.5f, lado, lado);
            var tex = Desenho(f.desenho);
            if (tex != null)
            {
                GUI.color = new Color(1f, 1f, 1f, 0.95f);
                GUI.DrawTexture(quadro, tex, ScaleMode.ScaleToFit, true);
            }
            else
            {
                // (os bichos de modelo provisório, ainda de caixas: sem o desenho, só o lembrete)
                GUI.color = ComAlfa(Tinta, 0.45f);
                GUI.Label(new Rect(quadro.x, quadro.center.y - altura * 0.5f, quadro.width, altura), "(desenho depois)", _numeroDaPagina);
            }
            float x = desenhoADireita ? caixa.x : quadro.xMax + 10f * u;
            float largura = caixa.width - lado - 10f * u;
            float y = caixa.y + 4f * u;
            GUI.color = Tinta;
            GUI.Label(new Rect(x, y, largura, altura * 1.2f), f.titulo, _faseDaPagina);
            float l = Mathf.Min(largura, _faseDaPagina.CalcSize(new GUIContent(f.titulo)).x);
            GUI.color = ComAlfa(Tinta, 0.75f);
            GUI.DrawTexture(new Rect(x, y + altura * 1.2f - 4f * u, l, 3f * u), Risco);
            y += altura * 1.25f;
            float fim = caixa.yMax - altura;
            foreach (var linha in Quebrar(f.nota, largura, _linhaDaPagina))
            {
                if (y > fim - altura * 0.5f) break;
                GUI.color = Tinta;
                GUI.Label(new Rect(x, y, largura, altura), linha, _linhaDaPagina);
                y += altura;
            }
            if (!string.IsNullOrEmpty(f.detalhe))
            {
                y = Mathf.Min(y + 2f * u, fim);
                foreach (var linha in Quebrar(f.detalhe, largura, _detalheDaPagina))
                {
                    GUI.color = ComAlfa(Tinta, 0.7f);
                    // (26/11/2026, a pedido do Pedro: os textos cortados) a caixa com a folga da inclinação do itálico
                    GUI.Label(new Rect(x, y, largura + FolgaDoItalico * u, altura), linha, _detalheDaPagina);
                    y += altura * 0.9f;
                }
            }
        }

        // ------------------------------------------------------------------ as construções e as receitas (as listas)

        struct LinhaDaReceita
        {
            public string nome, onde, gasta;
            public Item item;
        }

        static List<LinhaDaReceita> LinhasDasReceitas(InventarioJogador inv, bool construcoes)
        {
            var saida = new List<LinhaDaReceita>();
            if (inv == null || !inv.IsSpawned) return saida;
            for (int k = 0; k < Receitas.Todas.Length; k++)
            {
                if (!inv.ConheceAReceita(k)) continue;
                var r = Receitas.Todas[k];
                bool construcao = Itens.EhConstrucao(r.resultado) || r.resultado == Item.TabuasBarricada;
                if (construcao != construcoes) continue;
                var gasta = new List<string>();
                // (25/11/2026, a revisão) o gasto de verdade, o mesmo da aba Criar e do craft: com o Mestre armeiro, as do Armeiro
                // de sucata gastam menos sucata (antes, aqui saía a quantidade cheia)
                var custo = r.gasta != null ? inv.GastaDe(r) : null;
                if (custo != null) foreach (var g in custo) gasta.Add($"{g.qtd}× {Itens.Nome(g.item)}");
                string onde = r.bancada ? "na mesa de oficina" : "à mão";
                // (com o artigo, como o resto do jogo: "Precisa ter o machado na mochila"; 25/11/2026, a revisão: um item sem o
                // nome com o artigo, que o NomeComArtigo devolve vazio, sai pelo nome, para a linha não acabar em "com ")
                if (r.ferramenta != Item.Nada)
                {
                    string ferramenta = Itens.NomeComArtigo(r.ferramenta);
                    onde += $", com {(string.IsNullOrEmpty(ferramenta) ? Itens.Nome(r.ferramenta) : ferramenta)}";
                }
                if (r.habilidade >= 0) onde += $" ({Habilidades.Nome((Habilidade)r.habilidade)})";
                saida.Add(new LinhaDaReceita
                {
                    nome = Maiuscula(Itens.Nome(r.resultado)) + (r.qtd > 1 ? $" ({r.qtd})" : ""),
                    onde = onde, gasta = string.Join(", ", gasta), item = r.resultado,
                });
            }
            return saida;
        }

        // (26/11/2026, a pedido do Pedro: os textos cortados) o onde ia sempre ao lado do nome e, nas receitas das habilidades
        // ("na mesa de oficina (Rifle de precisão de sucata)") e nas tábuas ("à mão, com o machado (Barricada de tábuas)"),
        // passava da folha e saía cortado no meio de uma letra: quando não cabe ao lado do nome, ele desce para uma linha só
        // dele, e o que gasta vai na terceira. As páginas saem da altura de cada receita (2 ou 3 linhas), guardadas até a
        // lista mudar (medir as ~50 receitas a cada evento do OnGUI pesava)
        static readonly List<int> _paginasDasReceitas = new List<int>();
        static readonly List<bool> _ondeEmbaixo = new List<bool>();
        static int _assinaturaDasReceitas;

        static void PaginarAsReceitas(List<LinhaDaReceita> linhas, float altura, float largura, float cabe, float u)
        {
            int assinatura = 17;
            unchecked
            {
                assinatura = assinatura * 31 + linhas.Count;
                assinatura = assinatura * 31 + Mathf.RoundToInt(altura * 100f);
                assinatura = assinatura * 31 + Mathf.RoundToInt(largura * 100f);
                assinatura = assinatura * 31 + Mathf.RoundToInt(cabe * 100f);
                foreach (var l in linhas) assinatura = assinatura * 31 + (l.nome.GetHashCode() ^ (l.onde.GetHashCode() * 7));
            }
            if (assinatura == _assinaturaDasReceitas && _paginasDasReceitas.Count > 0 && _ondeEmbaixo.Count == linhas.Count) return;
            _assinaturaDasReceitas = assinatura;
            _paginasDasReceitas.Clear();
            _ondeEmbaixo.Clear();
            _paginasDasReceitas.Add(0);
            float ocupado = 0f;
            for (int i = 0; i < linhas.Count; i++)
            {
                bool embaixo = Largura(_linhaDaPagina, linhas[i].nome) + 8f * u + Largura(_detalheDaPagina, linhas[i].onde) > largura;
                _ondeEmbaixo.Add(embaixo);
                float h = (embaixo ? 3f : 2f) * altura + 2f * u;
                if (ocupado > 0f && ocupado + h > cabe)
                {
                    _paginasDasReceitas.Add(i);
                    ocupado = 0f;
                }
                ocupado += h;
            }
        }

        // (26/11/2026, a pedido do Pedro: os textos cortados) o texto numa linha da largura; quando não cabe, a primeira
        // linha com as reticências, quebrada já com o lugar delas (antes, o " …" vinha depois da quebra na largura toda e
        // podia passar da caixa)
        static string NumaLinha(string texto, float largura, GUIStyle estilo)
        {
            var partes = Quebrar(texto, largura, estilo);
            if (partes.Count <= 1) return partes.Count == 1 ? partes[0] : "";
            partes = Quebrar(texto, largura - Largura(estilo, " …"), estilo);
            return partes[0] + " …";
        }

        static void AsReceitas(Rect esq, Rect dir, float topo, float margem, float altura, InventarioJogador inv, bool construcoes, float u)
        {
            var linhas = LinhasDasReceitas(inv, construcoes);
            float largura = esq.width - margem - 18f * u;   // (as duas folhas têm a mesma largura)
            PaginarAsReceitas(linhas, altura, largura, esq.height - topo - 34f * u, u);
            _folhasDaAba = Mathf.Max(1, (_paginasDasReceitas.Count + 1) / 2);
            int a = (int)_abaDoCaderno;
            _folha[a] = Mathf.Clamp(_folha[a], 0, _folhasDaAba - 1);
            Cabecalho(esq, margem, construcoes ? "Construções" : "Receitas", u);
            if (linhas.Count == 0)
            {
                string vazio = construcoes ? "Nenhuma construção aprendida ainda. Pegue madeira e toras para aprender as primeiras."
                                           : "Nenhuma receita aprendida ainda. Elas aparecem quando você pega os materiais.";
                float yv = esq.y + topo;
                foreach (var l in Quebrar(vazio, esq.width - margem - 20f * u, _linhaDaPagina))
                {
                    GUI.color = ComAlfa(Tinta, 0.8f);
                    GUI.Label(new Rect(esq.x + margem, yv, esq.width - margem, altura), l, _linhaDaPagina);
                    yv += altura;
                }
                return;
            }
            float folga = FolgaDoItalico * u;
            for (int lado = 0; lado < 2; lado++)
            {
                int p = _folha[a] * 2 + lado;
                if (p >= _paginasDasReceitas.Count) break;
                var pagina = lado == 0 ? esq : dir;
                int ate = p + 1 < _paginasDasReceitas.Count ? _paginasDasReceitas[p + 1] : linhas.Count;
                float y = pagina.y + topo;
                float x = pagina.x + margem + 4f * u;
                for (int i = _paginasDasReceitas[p]; i < ate && i < linhas.Count; i++)
                {
                    var lr = linhas[i];
                    bool embaixo = i < _ondeEmbaixo.Count && _ondeEmbaixo[i];
                    // o ícone do que sai, a lápis (só o contorno escuro), na margem
                    var icone = inv.IconeDe(lr.item);
                    if (icone != null)
                    {
                        GUI.color = ComAlfa(Tinta, 0.8f);
                        GUI.DrawTexture(new Rect(pagina.x + 4f * u, y + 2f * u, margem - 12f * u, margem - 12f * u), icone, ScaleMode.ScaleToFit, true);
                    }
                    GUI.color = Tinta;
                    GUI.Label(new Rect(x, y, largura, altura), lr.nome, _linhaDaPagina);
                    // o onde ao lado do nome ou, quando não cabe, na linha de baixo (com a folga da inclinação do itálico)
                    GUI.color = ComAlfa(Tinta, 0.62f);
                    if (embaixo)
                        GUI.Label(new Rect(x + 10f * u, y + altura, largura - 10f * u + folga, altura), NumaLinha(lr.onde, largura - 10f * u, _detalheDaPagina), _detalheDaPagina);
                    else
                    {
                        float ln = Largura(_linhaDaPagina, lr.nome);
                        GUI.Label(new Rect(x + ln + 8f * u, y + 2f * u, Mathf.Max(0f, largura - ln - 8f * u) + folga, altura), lr.onde, _detalheDaPagina);
                    }
                    GUI.color = ComAlfa(Tinta, 0.8f);
                    float yDoGasto = y + altura * (embaixo ? 2f : 1f);
                    GUI.Label(new Rect(x + 10f * u, yDoGasto, largura - 10f * u + folga, altura), NumaLinha(lr.gasta, largura - 10f * u, _detalheDaPagina), _detalheDaPagina);
                    y += (embaixo ? 3f : 2f) * altura + 2f * u;
                }
            }
        }

        // ------------------------------------------------------------------ pequenos

        static string Maiuscula(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0]) + s.Substring(1);

        static string Juntar(List<string> partes)
        {
            if (partes.Count <= 1) return partes.Count == 1 ? partes[0] : "";
            return string.Join(", ", partes.GetRange(0, partes.Count - 1)) + " e " + partes[partes.Count - 1];
        }
    }
}
