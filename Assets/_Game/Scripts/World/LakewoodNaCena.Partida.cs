using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using ZombieCityCleaner.Combat;
using ZombieCityCleaner.Loot;
using ZombieCityCleaner.Mundo;
using ZombieCityCleaner.Vehicles;

namespace ZombieCityCleaner.World
{
    /// <summary>
    /// A casa da partida na cena (10/11/2026, a pedido: "coloque para o jogador nascer perto de alguma estrada... ele já
    /// deve nascer com os carros e as motos ao lado todos arrumados... uma casa perto da estrada... sem mobília, só com
    /// mesas e armas"): a térrea do kit no terreiro (<see cref="LugarDaPartida"/>), sem móveis e com as portas como vãos
    /// abertos (as folhas que abririam saem da malha), e as mesas da mobília nova nos cômodos da frente e nos quartos. O
    /// anfitrião põe os quatro veículos arrumados nas vagas do terreiro (de frente para a estrada) e, nas mesas, as armas de
    /// fogo, as brancas, a munição (caixas cheias) e as chaves dos veículos, como itens do chão (<see cref="ItensNoMundo"/>).
    /// </summary>
    public partial class LakewoodNaCena
    {
        [Header("A casa da partida (10/11/2026)")]
        [Tooltip("Os veículos arrumados no terreiro, na ordem das vagas: a perua, o cupê, a moto cross e a Fat Boy (os prefabs da lista da rede).")]
        public GameObject[] veiculos;
        [Tooltip("A mesa das armas (a de 6 lugares da mobília nova; no jogo, ×1,30).")]
        public GameObject mesaDaPartida;
        [Tooltip("Quantas caixas cheias de cada munição vão nas mesas.")]
        public int caixasPorMunicao = 4;

        /// <summary>A escala dos móveis da mobília nova no jogo.</summary>
        public const float EscalaDasMesas = 1.3f;

        /// <summary>A casa da partida (null sem ela).</summary>
        public Transform CasaDaPartida { get; private set; }

        // as mesas postas: o meio do tampo no mundo (em cima dele), o giro no mundo (o comprido da mesa no x dele) e o
        // tamanho do tampo (o comprido e o fundo)
        readonly List<(Vector3 tampo, float giro, Vector2 tamanho)> _mesas = new List<(Vector3, float, Vector2)>();
        bool _veiculosPostos, _armasPostas;
        readonly List<Item> _chaves = new List<Item>();

        /// <summary>Os cômodos que levam mesa, na ordem em que as mesas se enchem (a sala e a cozinha primeiro).</summary>
        static readonly string[] ComodosComMesas = { "sala", "cozinha", "quarto" };

        // ------------------------------------------------------------------ a casa e as mesas (em todas as cópias)

        void MontarACasaDaPartida()
        {
            var lugar = Chao.Partida;
            if (lugar == null) return;
            var M = Medidas.DoJogo;
            var p = lugar.Planta;
            // as portas "vivas" saem da malha e da colisão (a casa fica com os vãos abertos: aqui não há o sistema das portas)
            var casa = new Casa(p, M) { portasVivas = true };
            casa.Montar();
            AbandonoDaObra.Janelas(casa, AbandonoDaObra.NoMundo, p.semente);
            var desenho = new MalhaDeObra();
            desenho.Juntar(casa.vis, Matrix4x4.identity, false, altura: true);
            var go = Objeto("Casa da partida", desenho.ParaMesh("Casa da partida"), materialDaCidade, _pai);
            go.isStatic = false;
            go.transform.SetPositionAndRotation(lugar.PosicaoDaCasa, lugar.RotacaoDaCasa);
            go.AddComponent<MeshCollider>().sharedMesh = casa.col.ParaMesh("Casa da partida (colisão)");
            CasaDaPartida = go.transform;

            // os pisos dos cômodos (os passos), a caixa de dentro (a neblina de dentro) e o eco, como nas casas do mundo
            var zonas = new List<Bounds>();
            var pisos = new List<Superficie>();
            foreach (var k in p.comodos)
            {
                zonas.Add(new Bounds(new Vector3(k.Cx, casa.Base(k.andar), k.Cz), new Vector3(k.x1 - k.x0, 1f, k.z1 - k.z0)));
                pisos.Add(TerrenoDoMundo.SomDoPiso(k.piso ?? "Taco"));
            }
            var pz = go.AddComponent<PisoPorZona>();
            pz.zonas = zonas.ToArray();
            pz.superficies = pisos.ToArray();
            pz.resto = Superficie.Concreto;
            float baixo = casa.Base(0), alto = casa.Base(p.andares - 1) + M.altura + M.forro;
            var centro = new Vector3(0f, (baixo + alto) / 2f, 0f);
            var tamanho = new Vector3(p.W - 0.2f, alto - baixo, p.D - 0.2f);
            var dentro = new GameObject("Dentro");
            dentro.transform.SetParent(go.transform, false);
            dentro.transform.localPosition = centro;
            dentro.AddComponent<AreaInterna>().tamanho = tamanho;
            EcoSoDentro.Criar(go.transform, new Bounds(centro, tamanho));

            PorAsMesas(go.transform, p, casa.piso);
            Debug.Log($"[Lakewood] A casa da partida: {p.molde} de {p.W:F1} × {p.D:F1} m, {_mesas.Count} mesas, em ({lugar.PosicaoDaCasa.x:F0}; {lugar.PosicaoDaCasa.z:F0}), setor {SetorDe(lugar.PosicaoDaCasa)}.");
        }

        // as mesas nos cômodos do térreo: o comprido delas ao longo do lado maior do cômodo, a 1 m das paredes (as portas e
        // as passagens ficam livres), até duas por cômodo
        void PorAsMesas(Transform casa, Planta p, float piso)
        {
            if (mesaDaPartida == null) return;
            var mf = mesaDaPartida.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) return;
            var b = mf.sharedMesh.bounds;
            float W = b.size.x * EscalaDasMesas, D = b.size.z * EscalaDasMesas, H = b.max.y * EscalaDasMesas;
            const float Parede = 1.0f, Entre = 1.0f;
            foreach (var tipo in ComodosComMesas)
                foreach (var k in p.comodos)
                {
                    if (k.andar != 0 || k.tipo != tipo) continue;
                    float lx = k.x1 - k.x0 - 2f * Parede, lz = k.z1 - k.z0 - 2f * Parede;
                    bool aoLongoDeX = lx >= lz;
                    float comprido = aoLongoDeX ? lx : lz, curto = aoLongoDeX ? lz : lx;
                    int n1 = Mathf.Min(2, Mathf.FloorToInt((comprido + Entre) / (W + Entre)));
                    int n2 = Mathf.Min(2, Mathf.FloorToInt((curto + Entre) / (D + Entre)));
                    if (n1 < 1 || n2 < 1) continue;
                    // duas no máximo: lado a lado no comprido, ou uma atrás da outra
                    if (n1 >= 2) n2 = 1;
                    for (int i = 0; i < n1; i++)
                    for (int j = 0; j < n2; j++)
                    {
                        float u = (i - (n1 - 1) * 0.5f) * (W + Entre), v = (j - (n2 - 1) * 0.5f) * (D + Entre);
                        var meio = aoLongoDeX ? new Vector3(k.Cx + u, piso, k.Cz + v) : new Vector3(k.Cx + v, piso, k.Cz + u);
                        float giro = aoLongoDeX ? 0f : 90f;
                        var rot = Quaternion.Euler(0f, giro, 0f);
                        var mesa = Instantiate(mesaDaPartida, casa);
                        mesa.name = $"Mesa das armas {_mesas.Count + 1} ({k.tipo})";
                        mesa.transform.localScale = Vector3.one * EscalaDasMesas;
                        mesa.transform.SetLocalPositionAndRotation(meio - rot * new Vector3(b.center.x, 0f, b.center.z) * EscalaDasMesas, rot);
                        _mesas.Add((casa.TransformPoint(meio + Vector3.up * H), casa.eulerAngles.y + giro, new Vector2(W, D)));
                    }
                }
        }

        // ------------------------------------------------------------------ os veículos e as armas (no anfitrião)

        // os veículos arrumados nas vagas do terreiro (uma vez) e, com os itens do chão prontos, as armas nas mesas
        bool PorOsDaPartida()
        {
            var lugar = Chao.Partida;
            if (lugar == null || veiculos == null || veiculos.Length == 0) return false;
            var rede = NetworkManager.Singleton;
            if (rede == null || !rede.IsListening) return true;
            if (!rede.IsServer) return true;
            // a partida que veio do save (12/11/2026, a parte 8 do "O jogo do mundo sorteado no Lakewood"): os veículos e o
            // que estava nas mesas voltam do save (o Salvamento), e nada do começo é posto de novo
            if (Salvar.Salvamento.Carregando)
            {
                _veiculosPostos = _armasPostas = true;
                return true;
            }
            if (!_veiculosPostos)
            {
                _veiculosPostos = true;
                for (int i = 0; i < veiculos.Length && i < LugarDaPartida.VagasEmA.Length; i++)
                {
                    if (veiculos[i] == null) continue;
                    var pos = lugar.NoMundo(LugarDaPartida.VagasEmA[i], LugarDaPartida.LinhaDosVeiculos, 0.6f);
                    var go = Instantiate(veiculos[i], pos, Quaternion.Euler(0f, lugar.GiroDosVeiculos, 0f));
                    go.name = veiculos[i].name;
                    var carro = go.GetComponent<Carro>();
                    if (carro != null)
                    {
                        carro.ArrumadoDeFabrica();
                        if (carro.chaveDoVeiculo != Item.Nada) _chaves.Add(carro.chaveDoVeiculo);
                    }
                    go.GetComponent<NetworkObject>().Spawn(true);
                }
            }
            var mundo = ItensNoMundo.Instancia;
            if (!_armasPostas && mundo != null && mundo.IsSpawned)
            {
                _armasPostas = true;
                PorAsArmas(mundo);
            }
            return true;
        }

        /// <summary>O número do veículo para o save: a vaga dele no terreiro (a perua 0, o cupê 1, as motos 2 e 3); -1 de outro.</summary>
        public int QualDoCarro(Carro c)
        {
            if (c == null || veiculos == null) return -1;
            for (int i = 0; i < veiculos.Length; i++)
                if (veiculos[i] != null && c.name.StartsWith(veiculos[i].name)) return i;
            return -1;
        }

        /// <summary>O prefab do veículo pelo número do save, ou null.</summary>
        public GameObject PrefabDoCarro(int qual) => veiculos != null && qual >= 0 && qual < veiculos.Length ? veiculos[qual] : null;

        // os grupos das mesas (cada um começa numa mesa): as chaves e as armas de fogo, a munição e as armas brancas
        List<List<(Item item, int qtd)>> GruposDasMesas()
        {
            var fogo = new List<(Item, int)>();
            foreach (var c in _chaves) fogo.Add((c, -1));
            foreach (var a in new[] { Item.Pistola, Item.PistolaSucata, Item.Sniper, Item.Rifle, Item.Espingarda, Item.RifleSucata, Item.Submetralhadora, Item.SubmetralhadoraSucata })
                fogo.Add((a, -1));
            var municao = new List<(Item, int)>();
            for (var m = Item.Municao9mm; m <= Item.MunicaoParafuso; m++)
                if (Itens.EhMunicao(m))
                    for (int k = 0; k < Mathf.Max(1, caixasPorMunicao); k++) municao.Add((m, -1));
            var brancas = new List<(Item, int)>();
            for (var m = Item.Faca; m <= Item.Tocha; m++)
                if (Itens.EhBranca(m)) brancas.Add((m, -1));
            return new List<List<(Item, int)>> { fogo, municao, brancas };
        }

        // a pegada do item no tampo (o comprido ao longo da mesa): o giro em volta do y (0 ou 90, a partir do da mesa) e o
        // tamanho, como o ItensNoMundo o deita (as armas e as brancas de lado)
        static (float giro, Vector2 tamanho) Pegada(Mesh malha, Item item)
        {
            var deitado = Itens.EhArma(item) || Itens.EhBranca(item) ? Quaternion.Euler(0f, 0f, 90f) : Quaternion.identity;
            var b = malha.bounds;
            Vector2 Tamanho(float giro)
            {
                var q = Quaternion.Euler(0f, giro, 0f) * deitado;
                float x0 = float.MaxValue, x1 = float.MinValue, z0 = float.MaxValue, z1 = float.MinValue;
                for (int c = 0; c < 8; c++)
                {
                    var p = q * new Vector3((c & 1) == 0 ? b.min.x : b.max.x, (c & 2) == 0 ? b.min.y : b.max.y, (c & 4) == 0 ? b.min.z : b.max.z);
                    x0 = Mathf.Min(x0, p.x); x1 = Mathf.Max(x1, p.x); z0 = Mathf.Min(z0, p.z); z1 = Mathf.Max(z1, p.z);
                }
                return new Vector2(x1 - x0, z1 - z0);
            }
            var t0 = Tamanho(0f);
            if (t0.x >= t0.y) return (0f, t0);
            return (90f, Tamanho(90f));
        }

        // as armas nas mesas: cada grupo a partir de uma mesa nova, em fileiras ao longo do comprido dela, no meio do tampo
        void PorAsArmas(ItensNoMundo mundo)
        {
            const float Borda = 0.08f, EntreItens = 0.07f;
            int mesa = 0, postos = 0, sobraram = 0;
            foreach (var grupo in GruposDasMesas())
            {
                if (grupo.Count == 0) continue;
                // o plano de cada mesa: (o item, a quantidade, o giro, o meio no tampo a partir do canto)
                var plano = new List<(Item item, int qtd, float giro, Vector2 meio, Vector2 tam)>();
                float x = 0f, z = 0f, fundo = 0f;
                void Fechar()
                {
                    if (plano.Count == 0) return;
                    if (mesa >= _mesas.Count) { sobraram += plano.Count; plano.Clear(); return; }
                    var (tampo, giroDaMesa, tam) = _mesas[mesa++];
                    float usadoX = 0f, usadoZ = 0f;
                    foreach (var it in plano) { usadoX = Mathf.Max(usadoX, it.meio.x + it.tam.x / 2f); usadoZ = Mathf.Max(usadoZ, it.meio.y + it.tam.y / 2f); }
                    var rot = Quaternion.Euler(0f, giroDaMesa, 0f);
                    foreach (var it in plano)
                    {
                        var local = new Vector3(it.meio.x - usadoX / 2f, 0f, it.meio.y - usadoZ / 2f);
                        mundo.Criar(it.item, tampo + rot * local, giroDaMesa + it.giro, it.qtd);
                        postos++;
                    }
                    plano.Clear();
                }
                var mesaAgora = mesa < _mesas.Count ? _mesas[mesa].tamanho : new Vector2(2.34f, 1.17f);
                float uW = mesaAgora.x - 2f * Borda, uD = mesaAgora.y - 2f * Borda;
                foreach (var (item, qtd) in grupo)
                {
                    var malha = mundo.MalhaDe(item);
                    if (malha == null) continue;
                    var (giro, t) = Pegada(malha, item);
                    if (x > 0f && x + t.x > uW) { x = 0f; z += fundo + EntreItens; fundo = 0f; }
                    if (z > 0f && z + t.y > uD)
                    {
                        Fechar();
                        x = z = fundo = 0f;
                    }
                    plano.Add((item, qtd, giro, new Vector2(x + t.x / 2f, z + t.y / 2f), t));
                    x += t.x + EntreItens;
                    fundo = Mathf.Max(fundo, t.y);
                }
                Fechar();
            }
            Debug.Log($"[Lakewood] A casa da partida: {postos} itens em {mesa} mesas (das {_mesas.Count})" + (sobraram > 0 ? $"; {sobraram} sem mesa." : "."));
        }
    }
}
