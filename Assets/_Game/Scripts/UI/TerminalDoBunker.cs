using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using ZombieCityCleaner.Loot;
using ZombieCityCleaner.Moradores;
using ZombieCityCleaner.Mundo;
using ZombieCityCleaner.Player;
using ZombieCityCleaner.Video;
using ZombieCityCleaner.World;
using static ZombieCityCleaner.Mundo.PlantaDeBunker;

namespace ZombieCityCleaner.UI
{
    /// <summary>
    /// O terminal do bunker (a parte 7 do Plano_NPCs_Bunker.md da pasta de arte, a decisão 20): nos dois computadores da sala
    /// de operações (B0), o E abre a tela, verde no preto, com letra de máquina, como os computadores dos anos 90. As abas:
    ///  - <b>MEMBROS:</b> cada morador (o que está fazendo e onde, as barras, a saúde, o que carrega e a cama, que se muda
    ///    ali) e a ficha inteira do escolhido; e os jogadores (a vida, a fome, a sede e onde estão);
    ///  - <b>ESTOQUE:</b> a comida e a bebida em dias (só os moradores, e com os jogadores comendo daqui), os remédios, o
    ///    fumo, a sinuca e a lista de tudo por categoria, com a estante e o andar;
    ///  - <b>LUGARES:</b> os lugares do estoque com a meta (+ e −), as estantes e as camas;
    ///  - <b>REGISTRO:</b> o diário do bunker (os 100 últimos);
    ///  - <b>MAPA:</b> o mapa do M, com o bunker e os jogadores;
    ///  - <b>RÁDIO:</b> os pedidos de abrigo e de socorro (vazio por enquanto);
    ///  - <b>CARTAS:</b> os 52 NPCs do mundo, os conhecidos com a ficha e os outros com "?".
    /// O que é dos moradores vem do anfitrião, a cada segundo, da aba aberta (<c>JogadorRede.Terminal</c>); o estoque, as
    /// estantes, o mapa e os jogadores cada máquina já tem.
    /// </summary>
    public class TerminalDoBunker : MonoBehaviour
    {
        public const byte Dados = 1, Ficha = 2;
        const string Titulo = "BLACKWATER SHELTER · TERMINAL";
        static readonly string[] NomesDasAbas = { "MEMBROS", "ESTOQUE", "LUGARES", "REGISTRO", "MAPA", "RÁDIO", "CARTAS" };

        static readonly Color Verde = new Color(0.42f, 1f, 0.52f), Claro = new Color(0.85f, 1f, 0.86f), Escuro = new Color(0.25f, 0.6f, 0.32f),
                              Ambar = new Color(1f, 0.78f, 0.3f), Vermelho = new Color(1f, 0.45f, 0.35f);

        public static TerminalDoBunker Instancia { get; private set; }
        public bool Aberto { get; private set; }

        /// <summary>Abre o terminal (o E no computador).</summary>
        public static void Abrir()
        {
            if (Instancia == null) new GameObject("Terminal do bunker").AddComponent<TerminalDoBunker>();
            Instancia.Mostrar(true);
        }

        AbaDoTerminal _aba;
        DadosDoTerminal _dados;
        readonly List<string> _registro = new List<string>();
        readonly List<CartaNoTerminal> _cartas = new List<CartaNoTerminal>();
        readonly Dictionary<int, string> _fichas = new Dictionary<int, string>();
        readonly Dictionary<int, float> _fichaPedidaEm = new Dictionary<int, float>();
        int _membro = -1, _carta = -1, _abertoNoQuadro, _montadoNoQuadro = -1;
        bool _mudandoACama, _comCartas;
        float _proximoPedido, _rolagemA, _rolagemB;

        // ------------------------------------------------------------------ o começo e o fim

        void Awake() => Instancia = this;

        void OnEnable() => MenuOnline.FecharTelas += Fechar;

        void OnDisable()
        {
            MenuOnline.FecharTelas -= Fechar;
            if (Aberto) Mostrar(false);
        }

        void OnDestroy()
        {
            if (_riscas != null) Destroy(_riscas);
            if (Instancia == this) Instancia = null;
        }

        void Fechar() => Mostrar(false);

        void Mostrar(bool abrir)
        {
            if (abrir == Aberto) return;
            Aberto = abrir;
            MenuOnline.Tela(this, abrir);
            if (!abrir) return;
            _abertoNoQuadro = Time.frameCount;
            _proximoPedido = 0f;
            _fichas.Clear();
            _fichaPedidaEm.Clear();
            _mudandoACama = false;
            _comCartas = false;
        }

        void Trocar(AbaDoTerminal aba)
        {
            if (aba == _aba) return;
            _aba = aba;
            _rolagemA = _rolagemB = 0f;
            _mudandoACama = false;
            _proximoPedido = 0f;
        }

        void Update()
        {
            if (!Aberto) return;
            var local = JogadorRede.Local;
            if (local == null || !BunkerDoMundo.Dentro(local.transform.position))
            {
                Mostrar(false);
                return;
            }
            var tec = Keyboard.current;
            if (tec != null && Time.frameCount > _abertoNoQuadro + 1 && !MenuOnline.Pausado)
            {
                if (tec.eKey.wasPressedThisFrame)
                {
                    Mostrar(false);
                    return;
                }
                var numeros = new[] { tec.digit1Key, tec.digit2Key, tec.digit3Key, tec.digit4Key, tec.digit5Key, tec.digit6Key, tec.digit7Key };
                for (int i = 0; i < numeros.Length; i++) if (numeros[i].wasPressedThisFrame) Trocar((AbaDoTerminal)i);
                if (tec.tabKey.wasPressedThisFrame)
                    Trocar((AbaDoTerminal)(((int)_aba + (tec.shiftKey.isPressed ? NomesDasAbas.Length - 1 : 1)) % NomesDasAbas.Length));
            }
            if (Time.unscaledTime >= _proximoPedido)
            {
                _proximoPedido = Time.unscaledTime + 1f;
                local.PedirTerminal(_aba);
            }
            // a ficha do escolhido (uma vez por abertura; de novo se não chegou em 3 s)
            int id = _aba == AbaDoTerminal.Cartas ? _carta : _aba == AbaDoTerminal.Membros ? _membro : -1;
            if (id >= 0 && !_fichas.ContainsKey(id) && (!_fichaPedidaEm.TryGetValue(id, out float em) || Time.unscaledTime > em + 3f))
            {
                _fichaPedidaEm[id] = Time.unscaledTime;
                local.PedirFicha(id);
            }
        }

        // ------------------------------------------------------------------ a rede

        static readonly Dictionary<(byte, int), string[]> _chegando = new Dictionary<(byte, int), string[]>();

        /// <summary>Um pedaço da resposta do anfitrião (o <c>JogadorRede.Terminal</c>); a resposta inteira entra na tela.</summary>
        public static void Receber(byte tipo, int serie, int i, int n, string pedaco)
        {
            if (Instancia == null || n <= 0 || i >= n) return;
            var chave = (tipo, serie);
            if (!_chegando.TryGetValue(chave, out var partes))
            {
                if (_chegando.Count > 32) _chegando.Clear();
                _chegando[chave] = partes = new string[n];
            }
            if (partes.Length != n) return;
            partes[i] = pedaco ?? "";
            foreach (var p in partes) if (p == null) return;
            _chegando.Remove(chave);
            Instancia.Chegou(tipo, string.Concat(partes));
        }

        void Chegou(byte tipo, string texto)
        {
            if (tipo == Dados)
            {
                DadosDoTerminal d;
                try { d = JsonUtility.FromJson<DadosDoTerminal>(texto); }
                catch (Exception e)
                {
                    Debug.LogWarning($"[Terminal] Os dados não chegaram inteiros: {e.Message}");
                    return;
                }
                _dados = d;
                if (d.aba == (int)AbaDoTerminal.Registro)
                {
                    _registro.Clear();
                    _registro.AddRange(d.registro);
                }
                if (d.aba == (int)AbaDoTerminal.Cartas)
                {
                    _cartas.Clear();
                    _cartas.AddRange(d.cartas);
                    _comCartas = true;
                    // a primeira conhecida já aberta (a ficha não fica vazia)
                    if (!_cartas.Exists(c => c.id == _carta && c.conhecido))
                    {
                        var primeira = _cartas.Find(c => c.conhecido);
                        _carta = primeira != null ? primeira.id : -1;
                    }
                }
                if (d.membros.Count > 0 && !d.membros.Exists(m => m.id == _membro)) _membro = d.membros[0].id;
            }
            else if (tipo == Ficha)
            {
                int quebra = texto.IndexOf('\n');
                if (quebra < 0 || !int.TryParse(texto.Substring(0, quebra), out int id)) return;
                string ficha = texto.Substring(quebra + 1);
                _fichas[id] = ficha.Length == 0 ? "?" : ficha;
            }
        }

        // ------------------------------------------------------------------ o texto em linhas

        sealed class Linha
        {
            public readonly List<Trecho> trechos = new List<Trecho>(4);
        }

        struct Trecho
        {
            public string texto;
            public Color cor;
            public Action clique;
        }

        readonly List<Linha> _a = new List<Linha>(), _b = new List<Linha>();
        List<Linha> _alvo;
        Linha _linha;
        readonly List<Action> _cliques = new List<Action>();
        int _colunasA, _colunasB;

        void Em(List<Linha> alvo)
        {
            _alvo = alvo;
            alvo.Clear();
            _linha = null;
        }

        void L()
        {
            _linha = new Linha();
            _alvo.Add(_linha);
        }

        void T(string texto, Color cor, Action clique = null)
        {
            if (_linha == null) L();
            _linha.trechos.Add(new Trecho { texto = texto, cor = cor, clique = clique });
        }

        void LT(string texto, Color cor, Action clique = null)
        {
            L();
            T(texto, cor, clique);
        }

        void Vazia() => L();

        // um texto quebrado na largura (as linhas de baixo com o recuo); o rótulo vai na frente da primeira, em claro
        void Paragrafo(string texto, Color cor, int colunas, int recuo = 2, string rotulo = null)
        {
            int antes = rotulo?.Length ?? 0;
            var linhas = Quebrar(texto, colunas - antes, colunas, recuo);
            for (int i = 0; i < linhas.Count; i++)
            {
                L();
                if (i == 0 && rotulo != null) T(rotulo, Claro);
                T(linhas[i], cor);
            }
        }

        static List<string> Quebrar(string s, int colunas, int recuo) => Quebrar(s, colunas, colunas, recuo);

        // a primeira linha com a largura dela (depois do rótulo), as outras com o recuo; os espaços do começo ficam
        static List<string> Quebrar(string s, int primeira, int colunas, int recuo)
        {
            var r = new List<string>();
            primeira = Mathf.Max(primeira, 8);
            colunas = Mathf.Max(colunas, 12);
            int lead = 0;
            while (lead < s.Length && s[lead] == ' ') lead++;
            string pad = new string(' ', recuo);
            var sb = new StringBuilder(s.Substring(0, lead));
            bool vazia = true;
            foreach (var p in s.Substring(lead).Split(' '))
            {
                int limite = r.Count == 0 ? primeira : colunas - recuo;
                if (!vazia && sb.Length + 1 + p.Length > limite)
                {
                    r.Add((r.Count == 0 ? "" : pad) + sb);
                    sb.Clear();
                    vazia = true;
                }
                if (!vazia) sb.Append(' ');
                sb.Append(p);
                vazia = false;
            }
            if (sb.Length > 0 || r.Count == 0) r.Add((r.Count == 0 ? "" : pad) + sb);
            return r;
        }

        static string Pontos(string a, int largura) => a.Length >= largura - 2 ? a + " " : a + " " + new string('.', largura - a.Length - 2) + " ";

        static string Barra(float v)
        {
            int n = Mathf.Clamp(Mathf.RoundToInt(v / 10f), 0, 10);
            return new string('█', n) + new string('░', 10 - n) + $" {v,3:0}";
        }

        static Color CorDaBarra(float v) => v < 25f ? Vermelho : v < 50f ? Ambar : Verde;

        static Color CorDoHumor(int h) => h < HumorDoMorador.CriseLeve ? Vermelho : h < HumorDoMorador.Triste ? Ambar : h > HumorDoMorador.Feliz ? Claro : Verde;

        // ------------------------------------------------------------------ as abas

        void Membros()
        {
            Em(_a);
            var d = _dados;
            if (d == null || !d.comMoradores) LT("NENHUM MORADOR NO BUNKER.", Ambar);
            else
            {
                LT($"MORADORES ({d.membros.Count})", Claro);
                Vazia();
                foreach (var m in d.membros)
                {
                    int id = m.id;
                    bool escolhido = id == _membro;
                    LT(escolhido ? "▸ " : "  ", Claro);
                    T(m.completo.ToUpperInvariant(), escolhido ? Claro : Verde, () =>
                    {
                        _membro = id;
                        _mudandoACama = false;
                        _rolagemB = 0f;
                    });
                    LT("    " + m.idade, Escuro);
                    Paragrafo(m.agora, Verde, _colunasA, 10, "    agora: ");
                    L();
                    T("    fome ", Escuro); T(Barra(m.fome), CorDaBarra(m.fome));
                    T("   sede ", Escuro); T(Barra(m.sede), CorDaBarra(m.sede));
                    T("   sono ", Escuro); T(Barra(m.sono), CorDaBarra(m.sono));
                    if (m.fumo >= 0f)
                    {
                        L();
                        T("    vontade de fumar ", Escuro);
                        T(Barra(m.fumo), m.fumo > 75f ? Ambar : Verde);
                    }
                    L();
                    T("    saúde: ", Escuro); T(m.saude, m.saude.StartsWith("bem") ? Verde : Ambar);
                    T("   humor: ", Escuro); T($"{m.humor} {m.faixa}", CorDoHumor(m.humor));
                    T("   função: ", Escuro); T("—", Verde);
                    if (!string.IsNullOrEmpty(m.crise))
                    {
                        L();
                        T("    EM CRISE: ", Vermelho); T(m.crise, Vermelho);
                    }
                    Paragrafo(m.carrega, Verde, _colunasA, 13, "    carrega: ");
                    L();
                    T("    cama: ", Escuro); T(CamasDoBunker.Nome(m.cama), Verde);
                    Vazia();
                }
            }
            Jogadores();

            // a ficha do escolhido, com a cama que se muda
            Em(_b);
            var membro = d?.membros.Find(m => m.id == _membro);
            if (membro == null)
            {
                LT("FICHA", Claro);
                return;
            }
            L();
            T("CAMA: ", Claro);
            T(CamasDoBunker.Nome(membro.cama), Verde);
            T("  ", Verde);
            T(_mudandoACama ? "[fechar]" : "[mudar]", Claro, () => _mudandoACama = !_mudandoACama);
            if (_mudandoACama) CamasLivres(membro);
            Vazia();
            // o humor e os motivos (os que pesam mais primeiro)
            L();
            T("HUMOR: ", Claro);
            T($"{membro.humor} · {membro.faixa.ToUpperInvariant()}", CorDoHumor(membro.humor));
            if (!string.IsNullOrEmpty(membro.crise)) LT("  EM CRISE: " + membro.crise, Vermelho);
            if (membro.motivos == null || membro.motivos.Count == 0) LT("  nada pesando, nem para cima nem para baixo.", Escuro);
            else foreach (var t in membro.motivos) LT("  " + t, t.StartsWith("+") ? Verde : Ambar);
            Vazia();
            EscreverAFicha(membro.id, _colunasB);
        }

        // as camas com lugar, para o morador escolhido
        void CamasLivres(MembroNoTerminal membro)
        {
            int id = membro.id;
            int atual = CamasDoBunker.Indice(membro.cama);
            var donos = Donos();
            int n = 0;
            var camas = CamasDoBunker.Todas;
            for (int i = 0; i < camas.Count; i++)
            {
                if (i == atual) continue;
                donos.TryGetValue(i, out var deles);
                int ocupadas = deles?.Count ?? 0;
                if (ocupadas >= camas[i].capacidade) continue;
                int movel = camas[i].movel;
                string com = ocupadas > 0 ? $" (com {string.Join(", ", deles)})" : " (livre)";
                LT("  → ", Escuro);
                T(camas[i].nome + com, Verde, () =>
                {
                    JogadorRede.Local?.PedirCama(id, movel);
                    _mudandoACama = false;
                    _proximoPedido = Time.unscaledTime + 0.3f;
                });
                n++;
            }
            if (n == 0) LT("  nenhuma cama livre.", Ambar);
        }

        // os donos de cada cama (o índice em CamasDoBunker.Todas → os nomes)
        Dictionary<int, List<string>> Donos()
        {
            var r = new Dictionary<int, List<string>>();
            if (_dados == null) return r;
            foreach (var m in _dados.membros)
            {
                int c = CamasDoBunker.Indice(m.cama);
                if (c < 0) continue;
                if (!r.TryGetValue(c, out var l)) r[c] = l = new List<string>();
                l.Add(m.nome);
            }
            return r;
        }

        void EscreverAFicha(int id, int colunas)
        {
            if (!_fichas.TryGetValue(id, out var ficha))
            {
                LT("CARREGANDO A FICHA" + (Mathf.Repeat(Time.unscaledTime, 1f) < 0.5f ? "_" : " "), Escuro);
                return;
            }
            if (ficha == "?")
            {
                LT("?", Claro);
                LT("Ninguém aqui conhece essa pessoa.", Escuro);
                return;
            }
            bool primeira = true;
            foreach (var linha in ficha.Split('\n'))
            {
                if (linha.Length == 0)
                {
                    if (!primeira) Vazia();
                    continue;
                }
                int dois = linha.IndexOf(": ", StringComparison.Ordinal);
                string antes = dois > 0 ? linha.Substring(0, dois) : "";
                if (primeira) Paragrafo(linha, Claro, colunas);
                else if (dois > 0 && antes == antes.ToUpperInvariant() && !linha.StartsWith(" ")) Paragrafo(linha.Substring(dois + 2), Verde, colunas, 2, antes + ": ");
                else if (linha == linha.ToUpperInvariant() && !linha.StartsWith(" ")) LT(linha, Claro);
                else Paragrafo(linha, linha.StartsWith("  ·") ? Escuro : Verde, colunas, 4);
                primeira = false;
            }
        }

        void Jogadores()
        {
            LT($"JOGADORES ({JogadorRede.Todos.Count})", Claro);
            foreach (var j in JogadorRede.Todos)
            {
                if (j == null) continue;
                var vida = j.GetComponent<VidaJogador>();
                var fs = j.GetComponent<FomeESede>();
                L();
                T("  " + j.NomeNoMapa.ToUpperInvariant(), j == JogadorRede.Local ? Claro : Verde);
                T(" · " + OndeEsta(j.transform.position), Escuro);
                L();
                if (vida != null) { T("    vida ", Escuro); T(Barra(vida.Vida.Value), CorDaBarra(vida.Vida.Value)); }
                if (fs != null)
                {
                    T("   fome ", Escuro); T(Barra(fs.Fome.Value), CorDaBarra(fs.Fome.Value));
                    T("   sede ", Escuro); T(Barra(fs.Sede.Value), CorDaBarra(fs.Sede.Value));
                }
            }
        }

        static string OndeEsta(Vector3 p)
        {
            var b = BunkerDoMundo.Instancia;
            if (b == null || b.Raiz == null) return "?";
            if (BunkerDoMundo.Dentro(p))
            {
                var q = b.Raiz.InverseTransformPoint(p);
                return q.y > 1.5f ? "no morro do bunker" : $"no {Codigos[AndarEm(q.y + 0.05f)]}";
            }
            if (BunkerDoMundo.NaArea(p)) return "no pátio do bunker";
            float d = Vector2.Distance(new Vector2(p.x, p.z), new Vector2(b.Raiz.position.x, b.Raiz.position.z));
            return d < 1000f ? $"fora, a {d:0} m" : $"fora, a {d / 1000f:0.0} km";
        }

        // ------------------------------------------------------------------ o estoque

        static readonly string[] Categorias = { "COMIDA", "BEBIDA", "REMÉDIOS", "FUMO", "SINUCA", "ARMAS E MUNIÇÃO", "MATERIAIS E PEÇAS", "OUTROS" };

        static int Categoria(Item i) =>
            EstoqueDoBunker.EhComida(i) ? 0 : EstoqueDoBunker.EhBebida(i) ? 1 : Itens.EhRemedio(i) ? 2 : Itens.EhFumo(i) ? 3 : Itens.EhSinuca(i) ? 4
            : Itens.EhArma(i) || Itens.EhMunicao(i) || Itens.EhBranca(i) ? 5 : Itens.Loot(i) != null || Itens.EhPeca(i) ? 6 : 7;

        void Estoque()
        {
            Em(_a);
            var e = EstantesDoBunker.Instancia;
            if (e == null || !e.IsSpawned)
            {
                LT("O ESTOQUE NÃO RESPONDE.", Ambar);
                return;
            }
            int moradores = _dados != null ? _dados.membros.Count : 0, jogadores = JogadorRede.Todos.Count;
            int comidas = 0, bebidas = 0, remedios = 0, macos = 0, cigarros = 0, tacos = 0, caixas = 0, altos = 0;
            float fome = 0f, sede = 0f;
            var grupos = new Dictionary<(int cat, Item item, int estante), (int n, bool alto)>();
            foreach (var it in e.Guardados)
            {
                int n = Itens.Quantos(it.item, it.qtd);
                bool alto = !EstoqueDoBunker.Alcanca(it);
                var l = Itens.Loot(it.item);
                if (!alto && l != null)
                {
                    fome += l.fome * n;
                    sede += l.sede * n;
                }
                if (alto) altos += n;
                if (EstoqueDoBunker.EhComida(it.item)) comidas += n;
                else if (EstoqueDoBunker.EhBebida(it.item)) bebidas += n;
                else if (Itens.EhRemedio(it.item)) remedios += n;
                if (Itens.EhFumo(it.item))
                {
                    macos++;
                    cigarros += Mathf.Max(0, it.qtd);
                    n = 1;
                }
                if (Itens.EhTaco(it.item)) tacos++;
                if (it.item == Item.CaixaDeBolas) caixas++;
                var chave = (Categoria(it.item), it.item, (int)it.estante);
                grupos.TryGetValue(chave, out var antes);
                grupos[chave] = (antes.n + n, antes.alto || alto);
            }
            fome = Mathf.Max(0f, fome);
            sede = Mathf.Max(0f, sede);
            // os dias: os moradores comem na metade do ritmo dos jogadores
            string Dias(float total, float porHora)
            {
                if (moradores <= 0) return "sem moradores";
                float so = total / (porHora * 24f * moradores);
                float com = total / (porHora * 24f * (moradores + 2f * jogadores));
                return $"dura {so:0.0} dias ({moradores} moradores) · {com:0.0} com {(jogadores == 1 ? "o jogador" : $"os {jogadores} jogadores")} comendo daqui";
            }
            LT("O ESTOQUE · o que está nas estantes e nas prateleiras (o chão não conta)", Claro);
            Vazia();
            L(); T("COMIDA     ", Claro); T($"{comidas} itens · +{fome:0} de fome · ", Verde);
            T(Dias(fome, AgendaDoMorador.FomePorHora), moradores > 0 && fome / (AgendaDoMorador.FomePorHora * 24f * moradores) < 2f ? Ambar : Verde);
            L(); T("BEBIDA     ", Claro); T($"{bebidas} itens · +{sede:0} de sede · ", Verde);
            T(Dias(sede, AgendaDoMorador.SedePorHora), moradores > 0 && sede / (AgendaDoMorador.SedePorHora * 24f * moradores) < 2f ? Ambar : Verde);
            L(); T("REMÉDIOS   ", Claro); T($"{remedios}", Verde);
            T("     FUMO ", Claro); T($"{macos} {(macos == 1 ? "maço" : "maços")} ({cigarros} cigarros)", Verde);
            T("     SINUCA ", Claro); T($"{tacos} {(tacos == 1 ? "taco" : "tacos")} · {caixas} {(caixas == 1 ? "caixa" : "caixas")} de bolas", Verde);
            if (altos > 0) LT($"({altos} na tábua de cima das estantes de aço: os moradores não alcançam)", Escuro);

            var lista = new List<KeyValuePair<(int cat, Item item, int estante), (int n, bool alto)>>(grupos);
            lista.Sort((x, y) =>
            {
                int c = x.Key.cat.CompareTo(y.Key.cat);
                if (c != 0) return c;
                c = string.Compare(Itens.Nome(x.Key.item), Itens.Nome(y.Key.item), StringComparison.CurrentCulture);
                return c != 0 ? c : x.Key.estante.CompareTo(y.Key.estante);
            });
            int cat = -1;
            int largura = Mathf.Clamp(_colunasA - 44, 24, 46);
            foreach (var kv in lista)
            {
                if (kv.Key.cat != cat)
                {
                    cat = kv.Key.cat;
                    Vazia();
                    LT(Categorias[cat] + " " + new string('─', Mathf.Max(2, 30 - Categorias[cat].Length)), Claro);
                }
                L();
                string nome = Itens.Nome(kv.Key.item);
                T("  " + Pontos(nome, largura), Verde);
                T($"{kv.Value.n,4}   ", Claro);
                T(NomesNoTerminal.Estante(kv.Key.estante) + (kv.Value.alto ? " (alto)" : ""), Escuro);
            }
            if (lista.Count == 0)
            {
                Vazia();
                LT("NADA GUARDADO.", Ambar);
            }
        }

        // ------------------------------------------------------------------ os lugares

        static readonly TipoDoEstoque[] TiposDosLugares = { TipoDoEstoque.Despensa, TipoDoEstoque.Agua, TipoDoEstoque.Deposito };

        void Lugares()
        {
            Em(_a);
            var e = EstantesDoBunker.Instancia;
            LT("OS LUGARES DO ESTOQUE", Claro);
            Vazia();
            foreach (var t in TiposDosLugares)
            {
                var estantes = EstoqueDoBunker.Estantes(t);
                var comodos = new List<string>();
                foreach (int s in estantes)
                {
                    var m = MoveisDoBunker.Todos[s];
                    string c = $"{Codigos[m.andar]} {NomesNoTerminal.Comodo(m.andar, m.lugar.x, m.lugar.z)}";
                    if (!comodos.Contains(c)) comodos.Add(c);
                }
                string onde = comodos.Count == 0 ? "—" : string.Join(", ", comodos);
                int largOnde = Mathf.Clamp(_colunasA - 62, 18, 34);
                if (onde.Length > largOnde - 1)
                {
                    var andares = new List<string>();
                    foreach (var c in comodos) if (!andares.Contains(c.Substring(0, 2))) andares.Add(c.Substring(0, 2));
                    onde = string.Join(", ", andares);
                }
                int tem = 0;
                if (e != null && e.IsSpawned)
                    foreach (var it in e.Guardados)
                        if (EstoqueDoBunker.TipoDe(it.estante) == t && EstoqueDoBunker.DoTipo(t, it.item)) tem += Itens.Quantos(it.item, it.qtd);
                int meta = _dados != null && (int)t < _dados.metas.Count ? _dados.metas[(int)t] : EstoqueDoBunker.Meta(t);
                L();
                T(EstoqueDoBunker.Nome(t).ToUpperInvariant().PadRight(18), Claro);
                T(onde.PadRight(largOnde), Escuro);
                string oQue = t == TipoDoEstoque.Despensa ? "de comida" : t == TipoDoEstoque.Agua ? "de bebida" : "itens";
                T($"tem {tem,3} {oQue}".PadRight(22), meta >= 0 && tem < meta ? Ambar : Verde);
                if (meta < 0)
                {
                    T("sem meta", Escuro);
                    continue;
                }
                var tipo = t;
                T("meta ", Escuro);
                T("[−]", Claro, () => MudarMeta(tipo, meta - 1));
                T($" {meta,2} ", Claro);
                T("[+]", Claro, () => MudarMeta(tipo, meta + 1));
            }
            LT("A meta é quanto o lugar quer ter: o organizador mantém, e o morador pega no lugar mais perto que tenha.", Escuro);

            Vazia();
            LT("AS ESTANTES", Claro);
            var todos = MoveisDoBunker.Todos;
            int largura = Mathf.Clamp(_colunasA - 30, 30, 60);
            // quantos itens em cada estante (as pilhas contam cada um, como no estoque)
            var porEstante = new Dictionary<int, int>();
            if (e != null && e.IsSpawned)
                foreach (var it in e.Guardados)
                {
                    porEstante.TryGetValue(it.estante, out int antes);
                    porEstante[it.estante] = antes + (Itens.EhFumo(it.item) ? 1 : Itens.Quantos(it.item, it.qtd));
                }
            for (int s = 0; s < todos.Length; s++)
            {
                if (todos[s].andar >= Andares - 1 || !EstantesDoBunker.EhEstante(todos[s].modelo)) continue;
                porEstante.TryGetValue(s, out int n);
                L();
                T("  " + Pontos(NomesNoTerminal.Estante(s), largura), Verde);
                T(EstoqueDoBunker.Nome(EstoqueDoBunker.TipoDe(s)).ToLowerInvariant().PadRight(17), Escuro);
                T(n == 0 ? "vazia" : n == 1 ? "1 item" : $"{n} itens", n == 0 ? Escuro : Claro);
            }

            Vazia();
            var camas = CamasDoBunker.Todas;
            var donos = Donos();
            int vagas = 0, ocupadas = 0;
            foreach (var c in camas) vagas += c.capacidade;
            foreach (var l in donos.Values) ocupadas += l.Count;
            LT($"AS CAMAS · {Mathf.Max(0, vagas - ocupadas)} livres de {vagas} (a cama de cada um se muda em MEMBROS)", Claro);
            for (int i = 0; i < camas.Count; i++)
            {
                donos.TryGetValue(i, out var deles);
                L();
                T("  " + Pontos(camas[i].nome, largura), Verde);
                T(deles == null ? "livre" : string.Join(", ", deles) + (deles.Count < camas[i].capacidade ? " (e um lugar)" : ""), deles == null ? Escuro : Claro);
            }
        }

        void MudarMeta(TipoDoEstoque t, int meta)
        {
            meta = Mathf.Clamp(meta, 0, EstoqueDoBunker.MetaMaxima);
            JogadorRede.Local?.PedirMeta(t, meta);
            if (_dados != null && (int)t < _dados.metas.Count) _dados.metas[(int)t] = meta;
            _proximoPedido = Time.unscaledTime + 0.4f;
        }

        // ------------------------------------------------------------------ o registro, o rádio

        void Registro()
        {
            Em(_a);
            LT($"O REGISTRO DO BUNKER · os {MoradoresDoBunker.MaxNoRegistro} últimos, do mais novo ao mais velho", Claro);
            Vazia();
            if (_registro.Count == 0) LT(_dados == null ? "LENDO..." : "NADA NO REGISTRO.", Escuro);
            foreach (var r in _registro)
            {
                int dois = r.IndexOf(": ", StringComparison.Ordinal);
                if (dois > 0) Paragrafo(r.Substring(dois + 2), Verde, _colunasA, 16, (r.Substring(0, dois) + ":").PadRight(15) + " ");
                else Paragrafo(r, Verde, _colunasA);
            }
        }

        void Radio()
        {
            Em(_a);
            LT("O RÁDIO · CANAL 9", Claro);
            Vazia();
            // o chiado
            var sb = new StringBuilder("  ");
            int semente = Mathf.FloorToInt(Time.unscaledTime * 7f);
            var r = new System.Random(semente);
            const string Chiado = " ·.:˙,'`";
            for (int i = 0; i < Mathf.Min(60, _colunasA - 4); i++) sb.Append(Chiado[r.Next(Chiado.Length)]);
            LT(sb.ToString(), Escuro);
            Vazia();
            LT("Ninguém chamando.", Verde);
            LT("Os pedidos de abrigo e de socorro aparecem aqui, com o lugar no mapa.", Escuro);
        }

        // ------------------------------------------------------------------ o desenho

        Font _fonte;
        GUIStyle _texto;
        float _u = -1f, _cw, _lh;
        Texture2D _riscas;

        void Estilos(float u)
        {
            if (_texto != null && Mathf.Approximately(u, _u)) return;
            _u = u;
            if (_fonte == null) _fonte = Font.CreateDynamicFontFromOSFont(new[] { "Consolas", "Lucida Console", "Courier New" }, 16);
            _texto = new GUIStyle(GUI.skin.label)
            {
                font = _fonte, fontSize = Mathf.Max(10, Mathf.RoundToInt(15f * u)), alignment = TextAnchor.UpperLeft,
                wordWrap = false, clipping = TextClipping.Overflow, padding = new RectOffset(0, 0, 0, 0), margin = new RectOffset(0, 0, 0, 0),
                richText = false,
            };
            _cw = _texto.CalcSize(new GUIContent("MMMMMMMMMMMMMMMMMMMM")).x / 20f;
            _lh = Mathf.Round(_texto.fontSize * 1.3f);
            if (_riscas == null)
            {
                _riscas = new Texture2D(1, 3, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat, name = "Riscas do terminal" };
                _riscas.SetPixels(new[] { new Color(0f, 0f, 0f, 0.28f), new Color(0f, 0f, 0f, 0f), new Color(0f, 0f, 0f, 0f) });
                _riscas.Apply();
            }
        }

        static void Cor(Rect r, Color c)
        {
            var antes = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = antes;
        }

        static void Contorno(Rect r, float g, Color c)
        {
            Cor(new Rect(r.x, r.y, r.width, g), c);
            Cor(new Rect(r.x, r.yMax - g, r.width, g), c);
            Cor(new Rect(r.x, r.y, g, r.height), c);
            Cor(new Rect(r.xMax - g, r.y, g, r.height), c);
        }

        void Escrever(Rect r, string texto, Color cor)
        {
            if (Event.current.type != EventType.Repaint) return;
            _texto.normal.textColor = cor;
            GUI.Label(r, texto, _texto);
        }

        // as linhas num painel com a rolagem (a roda do mouse) e os trechos que se clicam
        void Painel(Rect area, List<Linha> linhas, ref float rolagem)
        {
            var e = Event.current;
            float total = linhas.Count * _lh;
            float max = Mathf.Max(0f, total - area.height);
            if (e.type == EventType.ScrollWheel && area.Contains(e.mousePosition))
            {
                rolagem += e.delta.y * _lh * 1.5f;
                e.Use();
            }
            rolagem = Mathf.Clamp(rolagem, 0f, max);
            GUI.BeginGroup(area);
            int primeira = Mathf.Max(0, Mathf.FloorToInt(rolagem / _lh));
            for (int i = primeira; i < linhas.Count; i++)
            {
                float y = i * _lh - rolagem;
                if (y > area.height) break;
                float x = 0f;
                foreach (var t in linhas[i].trechos)
                {
                    var r = new Rect(x, y, t.texto.Length * _cw, _lh);
                    if (t.clique != null)
                    {
                        bool sobre = r.Contains(Event.current.mousePosition);
                        if (sobre && e.type == EventType.Repaint) Cor(new Rect(r.x - 2f, r.y, r.width + 4f, r.height), new Color(0.3f, 1f, 0.4f, 0.16f));
                        if (sobre && e.type == EventType.MouseDown && e.button == 0)
                        {
                            _cliques.Add(t.clique);
                            e.Use();
                        }
                    }
                    Escrever(r, t.texto, t.cor);
                    x += r.width;
                }
            }
            GUI.EndGroup();
            if (max > 0f && e.type == EventType.Repaint)
            {
                var trilho = new Rect(area.xMax + 3f * _u, area.y, Mathf.Max(2f, 3f * _u), area.height);
                Cor(trilho, new Color(0.25f, 0.6f, 0.32f, 0.25f));
                float alto = Mathf.Max(20f * _u, area.height * area.height / total);
                Cor(new Rect(trilho.x, area.y + (area.height - alto) * rolagem / max, trilho.width, alto), new Color(0.42f, 1f, 0.52f, 0.7f));
            }
        }

        void OnGUI()
        {
            if (!Aberto || ModoVideo.Ligado) return;
            GUI.depth = -9;
            var e = Event.current;
            float u = Mathf.Min(Screen.height / 720f, Screen.width / 1280f);
            Estilos(u);
            Cor(new Rect(0, 0, Screen.width, Screen.height), new Color(0f, 0f, 0f, 0.72f));
            // (a linha de cima do HUD, a da partida, fica livre)
            float w = Mathf.Min(1220f * u, Screen.width - 24f), h = Mathf.Min(660f * u, Screen.height - 96f * u);
            var tela = new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f + 18f * u, w, h);
            float g = Mathf.Max(1f, Mathf.Round(u));
            Cor(new Rect(tela.x - 12f * u, tela.y - 12f * u, tela.width + 24f * u, tela.height + 24f * u), new Color(0.12f, 0.12f, 0.11f));
            Cor(tela, new Color(0.012f, 0.035f, 0.018f));
            float pad = 16f * u;
            var dentro = new Rect(tela.x + pad, tela.y + pad * 0.8f, tela.width - 2f * pad, tela.height - 1.6f * pad);

            // o cabeçalho e as abas
            Escrever(new Rect(dentro.x, dentro.y, dentro.width, _lh), Titulo, Claro);
            float hora = CicloDoDia.Hora;
            int hh = Mathf.FloorToInt(hora), mm = Mathf.Clamp(Mathf.FloorToInt((hora - hh) * 60f), 0, 59);
            string relogio = $"DIA {CicloDoDia.Dia} · {hh:00}:{mm:00}";
            Escrever(new Rect(dentro.xMax - relogio.Length * _cw, dentro.y, relogio.Length * _cw, _lh), relogio, Claro);
            float x = dentro.x, y = dentro.y + _lh * 1.4f;
            for (int i = 0; i < NomesDasAbas.Length; i++)
            {
                bool esta = i == (int)_aba;
                string rot = esta ? $"[{NomesDasAbas[i]}]" : $" {NomesDasAbas[i]} ";
                var r = new Rect(x, y, rot.Length * _cw, _lh);
                bool sobre = r.Contains(e.mousePosition);
                if (sobre && !esta && e.type == EventType.Repaint) Cor(r, new Color(0.3f, 1f, 0.4f, 0.14f));
                if (sobre && e.type == EventType.MouseDown && e.button == 0)
                {
                    int aba = i;
                    _cliques.Add(() => Trocar((AbaDoTerminal)aba));
                    e.Use();
                }
                Escrever(r, rot, esta ? Claro : Escuro);
                x += r.width + _cw * 2f;
            }
            y += _lh * 1.3f;
            Cor(new Rect(dentro.x, y, dentro.width, g), Escuro);
            float rodape = dentro.yMax - _lh;
            Cor(new Rect(dentro.x, rodape - _lh * 0.4f, dentro.width, g), Escuro);
            var conteudo = new Rect(dentro.x, y + _lh * 0.6f, dentro.width - 10f * u, rodape - _lh * 0.6f - (y + _lh * 0.6f));
            string ajuda = "[1-7] ABAS   [TAB] PRÓXIMA   [RODA] ROLAR   [E] [ESC] SAIR";
            Escrever(new Rect(dentro.x, rodape, dentro.width, _lh), ajuda + (Mathf.Repeat(Time.unscaledTime, 1f) < 0.5f ? " █" : ""), Escuro);

            if (_dados == null)
                Escrever(new Rect(conteudo.x, conteudo.y, conteudo.width, _lh), "LIGANDO" + new string('.', 1 + (int)Mathf.Repeat(Time.unscaledTime * 3f, 3f)), Verde);
            else
            {
                // as duas colunas (MEMBROS e CARTAS) ou uma
                bool duas = _aba == AbaDoTerminal.Membros || _aba == AbaDoTerminal.Cartas;
                float meio = _aba == AbaDoTerminal.Cartas ? 0.6f : 0.57f;
                var a = duas ? new Rect(conteudo.x, conteudo.y, conteudo.width * meio - 12f * u, conteudo.height) : conteudo;
                var b = new Rect(conteudo.x + conteudo.width * meio + 12f * u, conteudo.y, conteudo.width * (1f - meio) - 12f * u, conteudo.height);
                _colunasA = Mathf.FloorToInt(a.width / _cw);
                _colunasB = Mathf.FloorToInt(b.width / _cw);
                if (duas) Cor(new Rect(conteudo.x + conteudo.width * meio, conteudo.y, g, conteudo.height), new Color(0.25f, 0.6f, 0.32f, 0.5f));
                // as linhas uma vez por quadro (o OnGUI roda várias vezes: o Layout antes de cada evento)
                if (_montadoNoQuadro != Time.frameCount)
                {
                    _montadoNoQuadro = Time.frameCount;
                    Montar();
                }
                switch (_aba)
                {
                    case AbaDoTerminal.Mapa:
                        Mapa(conteudo);
                        break;
                    case AbaDoTerminal.Cartas:
                        Cartas(a);
                        Painel(b, _b, ref _rolagemB);
                        break;
                    default:
                        Painel(a, _a, ref _rolagemA);
                        if (duas) Painel(b, _b, ref _rolagemB);
                        break;
                }
            }

            // as riscas da tela e a moldura
            if (e.type == EventType.Repaint)
            {
                GUI.DrawTextureWithTexCoords(tela, _riscas, new Rect(0f, 0f, 1f, tela.height / Mathf.Max(2f, 3f * u)));
                Contorno(tela, g, new Color(0.25f, 0.6f, 0.32f, 0.6f));
            }

            if (_cliques.Count > 0)
            {
                var copia = _cliques.ToArray();
                _cliques.Clear();
                foreach (var c in copia) c();
            }
        }

        void Montar()
        {
            switch (_aba)
            {
                case AbaDoTerminal.Membros: Membros(); break;
                case AbaDoTerminal.Estoque: Estoque(); break;
                case AbaDoTerminal.Lugares: Lugares(); break;
                case AbaDoTerminal.Registro: Registro(); break;
                case AbaDoTerminal.Radio: Radio(); break;
                case AbaDoTerminal.Cartas:
                    Em(_a);
                    Em(_b);
                    var carta = _cartas.Find(c => c.id == _carta);
                    if (carta == null || !carta.conhecido) LT("Escolha uma carta conhecida para ver a ficha.", Escuro);
                    else EscreverAFicha(carta.id, _colunasB);
                    break;
                default:
                    Em(_a);
                    Em(_b);
                    break;
            }
        }

        // ------------------------------------------------------------------ o mapa

        void Mapa(Rect area)
        {
            var mapa = MapaDoMundo.Instancia;
            var tex = mapa != null ? mapa.Textura : null;
            var dados = mapa != null ? mapa.Dados : null;
            float lado = Mathf.Min(area.height, area.width * 0.62f);
            var quadro = new Rect(area.x, area.y, lado, lado);
            var info = new Rect(quadro.xMax + 24f * _u, area.y, area.xMax - quadro.xMax - 24f * _u, area.height);
            if (tex == null || dados == null)
            {
                Escrever(new Rect(area.x, area.y, area.width, _lh), "O MAPA AINDA ESTÁ SENDO DESENHADO" + new string('.', 1 + (int)Mathf.Repeat(Time.unscaledTime * 3f, 3f)), Escuro);
                return;
            }
            if (Event.current.type == EventType.Repaint)
            {
                var antes = GUI.color;
                GUI.color = new Color(0.22f, 0.62f, 0.3f, 1f);
                GUI.DrawTexture(quadro, tex, ScaleMode.StretchToFill);
                GUI.color = antes;
                Contorno(quadro, Mathf.Max(1f, Mathf.Round(_u)), Escuro);
            }
            Vector2 NoQuadro(Vector3 p) => new Vector2(quadro.x + (p.x + dados.Metade) / (2f * dados.Metade) * quadro.width,
                                                       quadro.yMax - (p.z + dados.Metade) / (2f * dados.Metade) * quadro.height);
            var b = BunkerDoMundo.Instancia;
            var linhas = new List<(string, Color)> { ("O MAPA", Claro), ("", Verde) };
            if (b != null && b.Raiz != null)
            {
                var p = NoQuadro(b.Raiz.position);
                float s = 9f * _u;
                Cor(new Rect(p.x - s / 2f, p.y - s / 2f, s, s), Claro);
                Contorno(new Rect(p.x - s, p.y - s, 2f * s, 2f * s), Mathf.Max(1f, Mathf.Round(_u)), Claro);
                Escrever(new Rect(p.x - 3f * _cw, p.y + s * 1.2f, 6f * _cw, _lh), "BUNKER", Claro);
                linhas.Add(("■ O BUNKER", Claro));
            }
            var seta = MapaDoMundo.SetaDosJogadores;
            foreach (var j in JogadorRede.Todos)
            {
                if (j == null) continue;
                var p = NoQuadro(j.transform.position);
                var frente = j.transform.forward;
                float giro = Mathf.Atan2(frente.x, frente.z) * Mathf.Rad2Deg;
                float s = 16f * _u;
                bool voce = j == JogadorRede.Local;
                if (Event.current.type == EventType.Repaint)
                {
                    var matriz = GUI.matrix;
                    var antes = GUI.color;
                    GUIUtility.RotateAroundPivot(giro, p);
                    GUI.color = voce ? Claro : Ambar;
                    GUI.DrawTexture(new Rect(p.x - s / 2f, p.y - s / 2f, s, s), seta);
                    GUI.color = antes;
                    GUI.matrix = matriz;
                }
                string nome = j.NomeNoMapa.ToUpperInvariant();
                Escrever(new Rect(p.x + s * 0.7f, p.y - _lh / 2f, nome.Length * _cw, _lh), nome, voce ? Claro : Ambar);
                linhas.Add(($"▲ {nome} · {OndeEsta(j.transform.position)}", voce ? Claro : Ambar));
            }
            linhas.Add(("", Verde));
            linhas.Add(("PEDIDOS DE SOCORRO", Claro));
            linhas.Add(("  nenhum.", Escuro));
            float y = info.y;
            foreach (var (t, c) in linhas)
            {
                foreach (var parte in Quebrar(t, Mathf.Max(12, Mathf.FloorToInt(info.width / _cw)), 2))
                {
                    Escrever(new Rect(info.x, y, info.width, _lh), parte, c);
                    y += _lh;
                }
            }
        }

        // ------------------------------------------------------------------ as cartas

        void Cartas(Rect area)
        {
            var e = Event.current;
            if (!_comCartas)
            {
                Escrever(new Rect(area.x, area.y, area.width, _lh), "LENDO AS CARTAS" + new string('.', 1 + (int)Mathf.Repeat(Time.unscaledTime * 3f, 3f)), Escuro);
                return;
            }
            int noBunker = 0, conhecidos = 0;
            foreach (var c in _cartas)
            {
                if (c.noBunker) noBunker++;
                if (c.conhecido) conhecidos++;
            }
            Escrever(new Rect(area.x, area.y, area.width, _lh), $"{_cartas.Count} PESSOAS NO MUNDO · {noBunker} NO BUNKER · {conhecidos} CONHECIDAS", Claro);
            var grade = new Rect(area.x, area.y + _lh * 1.5f, area.width, area.height - _lh * 1.5f);
            float cw = 18f * _cw, ch = 3.7f * _lh, folga = 8f * _u;
            int colunas = Mathf.Max(1, Mathf.FloorToInt((grade.width + folga) / (cw + folga)));
            cw = (grade.width - folga * (colunas - 1)) / colunas;
            int linhas = (_cartas.Count + colunas - 1) / colunas;
            float total = linhas * (ch + folga);
            float max = Mathf.Max(0f, total - grade.height);
            if (e.type == EventType.ScrollWheel && grade.Contains(e.mousePosition))
            {
                _rolagemA += e.delta.y * _lh * 1.5f;
                e.Use();
            }
            _rolagemA = Mathf.Clamp(_rolagemA, 0f, max);
            float g = Mathf.Max(1f, Mathf.Round(_u));
            GUI.BeginGroup(grade);
            for (int i = 0; i < _cartas.Count; i++)
            {
                var c = _cartas[i];
                var r = new Rect((i % colunas) * (cw + folga), (i / colunas) * (ch + folga) - _rolagemA, cw, ch);
                if (r.yMax < 0f || r.y > grade.height) continue;
                bool escolhida = c.id == _carta;
                bool sobre = r.Contains(e.mousePosition);
                if (e.type == EventType.Repaint)
                {
                    if (escolhida) Cor(r, new Color(0.3f, 1f, 0.4f, 0.14f));
                    else if (sobre && c.conhecido) Cor(r, new Color(0.3f, 1f, 0.4f, 0.07f));
                    Contorno(r, g, escolhida ? Claro : c.conhecido ? Escuro : new Color(0.2f, 0.4f, 0.24f));
                }
                if (sobre && c.conhecido && e.type == EventType.MouseDown && e.button == 0)
                {
                    int id = c.id;
                    _cliques.Add(() =>
                    {
                        _carta = id;
                        _rolagemB = 0f;
                    });
                    e.Use();
                }
                float tx = r.x + _cw * 0.8f, ty = r.y + _lh * 0.25f;
                int cabe = Mathf.Max(4, Mathf.FloorToInt((cw - _cw * 1.6f) / _cw));
                Escrever(new Rect(tx, ty, cw, _lh), $"#{c.id:00}", Escuro);
                if (c.conhecido)
                {
                    if (c.noBunker) Escrever(new Rect(r.xMax - _cw * 10f, ty, _cw * 9.5f, _lh), "NO BUNKER", Claro);
                    string nome = c.nome.Length > cabe ? c.nome.Substring(0, cabe - 1) + "…" : c.nome;
                    Escrever(new Rect(tx, ty + _lh, cw, _lh), nome, escolhida ? Claro : Verde);
                    Escrever(new Rect(tx, ty + _lh * 2f, cw, _lh), c.idade + (c.noBunker ? "" : " · lá fora"), Escuro);
                }
                else Escrever(new Rect(r.x + cw / 2f - _cw / 2f, r.y + ch / 2f - _lh / 2f, _cw * 2f, _lh), "?", new Color(0.3f, 0.6f, 0.36f));
            }
            GUI.EndGroup();
            if (max > 0f && e.type == EventType.Repaint)
            {
                var trilho = new Rect(grade.xMax + 3f * _u, grade.y, Mathf.Max(2f, 3f * _u), grade.height);
                Cor(trilho, new Color(0.25f, 0.6f, 0.32f, 0.25f));
                float alto = Mathf.Max(20f * _u, grade.height * grade.height / total);
                Cor(new Rect(trilho.x, grade.y + (grade.height - alto) * _rolagemA / max, trilho.width, alto), new Color(0.42f, 1f, 0.52f, 0.7f));
            }
        }
    }
}
