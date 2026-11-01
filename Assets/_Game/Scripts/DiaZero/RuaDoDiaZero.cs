using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using ZombieCityCleaner.Combat;
using ZombieCityCleaner.Mundo;
using ZombieCityCleaner.World;

namespace ZombieCityCleaner.DiaZero
{
    /// <summary>O que um NPC do Dia 0 faz (a pedido: "regando planta, cortando grama, acenando, conversando").</summary>
    public enum Rotina : byte { Regar, CortarGrama, Conversar, Passear, Correr, Atendente, Faxineiro, Comprar, Sentado }

    /// <summary>Onde um NPC do Dia 0 fica e o que ele faz (montado pela rua; os NPCs leem).</summary>
    public sealed class LugarDeNpc
    {
        public Rotina rotina;
        public Vector3 ponto;
        public float giro;
        /// <summary>O caminho (no mundo), para quem anda: em laço.</summary>
        public Vector3[] caminho;
        /// <summary>O tipo do sorteio das roupas (o "Civil", o "Trabalhador"...).</summary>
        public string tipo = "Civil";
        public int semente;
        public bool doente;
        /// <summary>Com quem ele conversa (o índice na lista; -1: ninguém).</summary>
        public int par = -1;
        /// <summary>A loja dele (os atendentes, o faxineiro e quem compra).</summary>
        public LojaDoDiaZero loja;
    }

    /// <summary>
    /// A rua do Dia 0 (01/11/2026, a pedido: "uma rua longa", o mundo normal antes do apocalipse), montada pelo jogo a cada
    /// vez que a cena abre (como as cenas de exemplo: nada é salvo na cena). Na cena <c>Scenes/DiaZero.unity</c>, carregada
    /// por cima da cena do mundo quando um jogo novo começa (<see cref="DiaZeroDaPartida"/>):
    ///  - do lado norte, as casas dos vizinhos e a nossa casa (a casa da família, a mesma do mundo de 5 anos depois:
    ///    <see cref="PlantasDeCasa.DaFamilia(Medidas)"/>), e a porta do abrigo dentro dela (no fim do corredor);
    ///  - do lado sul, mais casas; depois, o comércio: o mercado e a farmácia (norte), a loja de armas, a de ferragens e a de
    ///    roupas (sul) e o posto no fim;
    ///  - tudo novo: sem o abandono (o material da cidade), sem as tábuas nas janelas e sem as manchas (<see cref="Obra.nova"/>);
    ///  - as pontas fechadas (a obra na rua, as cercas atrás dos quintais e as paredes invisíveis).
    /// Os produtos, os caixas e os atendentes de cada loja ficam na <see cref="LojaDoDiaZero"/>; os NPCs, no
    /// <see cref="NpcsDoDiaZero"/>, pelos <see cref="Npcs"/>.
    /// </summary>
    [ExecuteAlways]
    public class RuaDoDiaZero : CenaDeExemplo
    {
        [Header("Dia 0")]
        [Tooltip("Os modelos do Cidade_Mobilia.fbx (fora do jogo; no jogo, a MobiliaDoMundo da cena do mundo já registrou).")]
        public Mesh[] mobilia;
        [Tooltip("O material das letras das placas (shader ZombieCityCleaner/Letreiro).")]
        public Material materialDasLetras;
        [Tooltip("As roupas como itens (Prefabs/Roupas/Itens): o que a loja de roupas vende.")]
        public GameObject[] roupas;
        [Tooltip("Do Cidade_Rua.fbx: a barreira, o cavalete, o cone, o hidrante e a lixeira.")]
        public Mesh barreira, cavalete, cone, hidrante, lixeira;

        public static RuaDoDiaZero Instancia { get; private set; }
        /// <summary>A rua está montada (as lojas, a casa e os pontos).</summary>
        public bool Pronta { get; private set; }

        /// <summary>A nossa casa (o objeto dela: as coordenadas da planta, já espelhadas).</summary>
        public Transform NossaCasa { get; private set; }
        public Planta PlantaDaCasa { get; private set; }
        /// <summary>Onde os 4 nascem (na sala, de frente para a TV) e para onde olham.</summary>
        public readonly Vector3[] Nascimentos = new Vector3[4];
        public readonly float[] GirosDosNascimentos = new float[4];
        /// <summary>O olho de quem está no sofá e a TV (para a introdução), no mundo.</summary>
        public Vector3 OlhoNoSofa { get; private set; }
        public Vector3 Tv { get; private set; }
        /// <summary>Onde as compras aparecem dentro da casa (nas coordenadas da casa: o chão da sala e da cozinha, fora dos móveis).</summary>
        public readonly List<Vector3> LugaresDasCompras = new List<Vector3>();
        public readonly List<LojaDoDiaZero> Lojas = new List<LojaDoDiaZero>();
        public readonly List<LugarDeNpc> Npcs = new List<LugarDeNpc>();
        /// <summary>A rua vai de x = Inicio a x = Fim (as pontas fechadas).</summary>
        public float Inicio { get; private set; }
        public float Fim { get; private set; }

        protected override string Chave => $"dia-zero|{semente}|{mobilia?.Length ?? 0}|{roupas?.Length ?? 0}";

        float _norte, _sul;
        readonly List<(Transform t, List<ModelosDaEstrada.Texto> textos, int cor)> _letras = new List<(Transform, List<ModelosDaEstrada.Texto>, int)>();
        readonly List<MeshFilter> _filtrosDasLetras = new List<MeshFilter>();
        int _versaoDasLetras;
        Material[] _materiaisDasLetras;
        readonly List<Rect> _quintaisNorte = new List<Rect>(), _quintaisSul = new List<Rect>();

        static readonly Color[] CoresDasLetras = { new Color(0.95f, 0.94f, 0.9f), new Color(0.08f, 0.08f, 0.09f), new Color(0.75f, 0.12f, 0.1f) };

        void OnDestroy()
        {
            if (Instancia == this) Instancia = null;
        }

        void Update()
        {
            // o atlas das letras foi refeito: as malhas das placas também
            if (_letras.Count > 0 && _versaoDasLetras != Letreiro.Versao) RefazerAsLetras();
        }

        // ------------------------------------------------------------------ a montagem

        protected override void Montar()
        {
            Instancia = this;
            Pronta = false;
            Lojas.Clear();
            Npcs.Clear();
            LugaresDasCompras.Clear();
            _letras.Clear();
            _filtrosDasLetras.Clear();
            _quintaisNorte.Clear();
            _quintaisSul.Clear();
            NossaCasa = null;
            var M = Medidas.DoJogo;
            if (!Mobilia.Pronta && mobilia != null && mobilia.Length > 0) Mobilia.Registrar(mobilia);

            // as casas: o norte (os vizinhos e a nossa, a segunda) e o sul
            float x = 0f;
            x = PorCasaNova(Vizinha(101, M), "Casa do vizinho (norte 1)", x, 1, false);
            x = PorCasaNova(PlantasDeCasa.DaFamilia(M), "Nossa casa", x, 1, true);
            for (int i = 0; i < 3; i++) x = PorCasaNova(Vizinha(102 + i, M), $"Casa do vizinho (norte {i + 2})", x, 1, false);
            float xs = -6f;
            for (int i = 0; i < 5; i++) xs = PorCasaNova(Vizinha(201 + i, M), $"Casa do vizinho (sul {i + 1})", xs, -1, false);

            // o comércio, depois das casas
            float comeco = Mathf.Max(x, xs) + 4f;
            _norte = _sul = comeco;
            PorLojaDoDia(LojaDoDia.Mercado, 26f, 22f, 1, 301, "ParedeBege");
            PorLojaDoDia(LojaDoDia.Farmacia, 13f, 16f, 1, 302, "ConcretoClaro");
            PorLojaDoDia(LojaDoDia.Armas, 13f, 16f, -1, 303, "ConcretoClaro");
            PorLojaDoDia(LojaDoDia.Ferragens, 15f, 17f, -1, 304, "ParedeAzul");
            PorLojaDoDia(LojaDoDia.Roupas, 14f, 16f, -1, 305, "ParedeVerde");
            PorPosto(_norte + 2f);

            Inicio = -32f;
            Fim = Mathf.Max(_norte, _sul) + 26f;
            Rua(Inicio, Fim, 6);
            Fechar(Inicio, Fim);
            PorNpcs();
            Pronta = true;
            resumo.Insert(0, $"Rua do Dia 0: {Construcoes.Count} construções, {Lojas.Count} lojas, {Npcs.Count} lugares de NPC, " +
                             $"{LugaresDasCompras.Count} lugares para as compras, {triangulos:N0} triângulos.\n");
        }

        static Planta Vizinha(int semente, Medidas M)
        {
            var r = new System.Random(semente);
            var p = r.NextDouble() < 0.55 ? PlantasDeCasa.Terrea(r, M, false) : PlantasDeCasa.Sobrado(r, M, false);
            PlantasDeCasa.Estilo(r, p);
            if (p.garagem != null) p.garagem.fechada = r.NextDouble() < 0.5;
            return p;
        }

        // ------------------------------------------------------------------ as casas

        /// <summary>
        /// Uma casa nova de frente para a rua (como o PorCasa da base, com a mobília nova quando ela está registrada: a do
        /// jogo, com as portas e as gavetas que abrem). Devolve onde o lote termina.
        /// </summary>
        float PorCasaNova(Planta p, string nome, float x, int lado, bool familia)
        {
            var M = Medidas.DoJogo;
            var casa = new Casa(p, M) { nova = true };
            casa.Montar();
            Mobilia.Por(casa.moveis, casa.vis, casa.col, p.semente);   // os antigos que ainda existem (sem eles, nada)
            if (p.espelhar) casa.col.Espelhar();
            var pontos = new List<PontoDeMovel>();
            foreach (var pm in casa.moveis) pontos.Add(p.espelhar ? pm.Espelhado() : pm);
            MalhaDaMobilia nova = null;
            if (MobiliaNova.Pronta)
            {
                MobiliaNova.Caixas(pontos, casa.col);
                nova = MobiliaNova.Desenho(pontos, p.semente);
            }

            Vector2 lim = casa.LimitesEmX;
            float sx = lado > 0 ? 1f : -1f;
            float cx = lado > 0 ? x - lim.x + 1.5f : x + lim.y + 1.5f;
            float cz = lado * (Frente + p.D / 2);
            Vector2 Mundo(Vector2 q) => new Vector2(cx + sx * q.x, cz + sx * q.y);
            var go = Objeto(nome, raiz, Malha(casa.vis, nome, p.espelhar), Malha(casa.col, nome + " (colisão)"), new Vector3(cx, 0f, cz), lado > 0 ? 0f : 180f);
            Construcoes.Add(go);
            triangulos += casa.vis.Triangulos;
            foreach (var b in casa.BuracosNoChao)
            {
                Vector2 a = Mundo(b.min), c = Mundo(b.max);
                buracos.Add(Rect.MinMaxRect(Mathf.Min(a.x, c.x), Mathf.Min(a.y, c.y), Mathf.Max(a.x, c.x), Mathf.Max(a.y, c.y)));
            }
            Caminho(Mundo(casa.Entrada), lado * Calcada);
            if (casa.EntradaDoCarro.HasValue) Caminho(Mundo(casa.EntradaDoCarro.Value), lado * Calcada, 3.0f, -0.135f);
            var rnd = new System.Random(Misturar(semente, Mathf.RoundToInt(cx * 10f), lado));
            if (carros != null && carros.Length > 0)
                foreach (var (pos, yaw) in casa.Carros)
                {
                    if (!familia && rnd.NextDouble() > 0.75) continue;
                    Vector2 c = Mundo(pos);
                    Objeto("Carro da casa", raiz, carros[rnd.Next(carros.Length)], null, new Vector3(c.x, 0.05f, c.y), (lado > 0 ? 0f : 180f) + yaw);
                }
            if (nova != null) MobiliaDaConstrucao.Por(go, pontos, nova, p.semente, -100);
            PisosEDentro(go, p, casa, M);
            MalhaDeObra.Devolver(casa.vis);
            MalhaDeObra.Devolver(casa.col);

            // o quintal da frente (entre a calçada e a casa), para os NPCs
            float xa = lado > 0 ? x + 0.5f : x + 0.5f, xb = x + (lim.y - lim.x) + 2.5f;
            var quintal = Rect.MinMaxRect(xa, Calcada + 0.8f, xb, Frente - 0.8f);
            (lado > 0 ? _quintaisNorte : _quintaisSul).Add(quintal);
            if (familia) ACasaDaFamilia(go, p, pontos, M);
            return x + (lim.y - lim.x) + 3f;
        }

        // os pisos dos cômodos (os passos), a caixa de dentro (a neblina de dentro) e o eco, como no mundo
        static void PisosEDentro(GameObject go, Planta p, Casa casa, Medidas M)
        {
            var zonas = new List<Bounds>();
            var pisos = new List<Superficie>();
            float Sx(float v) => p.espelhar ? -v : v;
            void Zona(float xa, float xb, float za, float zb, float y, string piso)
            {
                float a = Sx(xa), b = Sx(xb);
                zonas.Add(new Bounds(new Vector3((a + b) / 2, y, (za + zb) / 2), new Vector3(Mathf.Abs(b - a), 1f, zb - za)));
                pisos.Add(TerrenoDoMundo.SomDoPiso(piso));
            }
            foreach (var k in p.comodos) Zona(k.x0, k.x1, k.z0, k.z1, casa.Base(k.andar), k.piso ?? "Taco");
            var pz = go.AddComponent<PisoPorZona>();
            pz.zonas = zonas.ToArray();
            pz.superficies = pisos.ToArray();
            pz.resto = Superficie.Concreto;
            float baixo = casa.Base(p.porao ? -1 : 0), alto = casa.Base(p.andares - 1) + M.altura + M.forro;
            var centro = new Vector3(0f, (baixo + alto) / 2, 0f);
            var tamanho = new Vector3(p.W - 0.2f, alto - baixo, p.D - 0.2f);
            var dentro = new GameObject("Dentro") { hideFlags = HideFlags.DontSave };
            dentro.transform.SetParent(go.transform, false);
            dentro.transform.localPosition = centro;
            dentro.AddComponent<AreaInterna>().tamanho = tamanho;
            if (Application.isPlaying) EcoSoDentro.Criar(go.transform, new Bounds(centro, tamanho));
        }

        // a nossa casa: os nascimentos (na sala, de frente para a TV), o olho do sofá, os lugares das compras e a porta do abrigo
        void ACasaDaFamilia(GameObject go, Planta p, List<PontoDeMovel> pontos, Medidas M)
        {
            NossaCasa = go.transform;
            PlantaDaCasa = p;
            float sx = p.espelhar ? -1f : 1f, piso = global::ZombieCityCleaner.Mundo.Casa.PisoDe(p);
            var sala = p.comodos.Find(k => k.tipo == "sala" && k.andar == 0) ?? p.comodos[0];
            Vector3 NaCasa(float lx, float ly, float lz) => go.transform.TransformPoint(new Vector3(lx, ly, lz));

            // o sofá e a TV (o rack) da sala: os pontos já espelhados
            PontoDeMovel? sofa = null, rack = null;
            foreach (var pm in pontos)
            {
                if (pm.comodo != "sala" || pm.andar != 0) continue;
                if (pm.tipo != null && pm.tipo.StartsWith("Sofa") && sofa == null) sofa = pm;
                if (pm.tipo != null && (pm.tipo.StartsWith("Rack") || pm.tipo.StartsWith("TV")) && rack == null) rack = pm;
            }
            Vector3 meioDaSala = new Vector3(sx * sala.Cx, piso, sala.Cz);
            Vector3 tvLocal = rack.HasValue ? rack.Value.centro + Vector3.up * 0.95f : meioDaSala + new Vector3(0f, 1f, sala.z1 - sala.Cz - 0.4f);
            Vector3 sofaLocal = sofa.HasValue ? sofa.Value.centro : meioDaSala - (tvLocal - meioDaSala).normalized * 1.5f;
            if (sofa.HasValue)
            {
                var frente = Quaternion.Euler(0f, sofa.Value.giro, 0f) * Vector3.forward;
                sofaLocal += frente * (sofa.Value.tamanho.z * 0.5f + 0.55f);
            }
            sofaLocal.y = piso;
            Tv = NaCasa(tvLocal.x, tvLocal.y, tvLocal.z);
            OlhoNoSofa = NaCasa(sofaLocal.x, piso + 1.1f, sofaLocal.z);
            // os 4 em fila na frente do sofá, de frente para a TV
            var paraTv = tvLocal - sofaLocal;
            paraTv.y = 0f;
            if (paraTv.sqrMagnitude < 0.01f) paraTv = Vector3.forward;
            paraTv.Normalize();
            var lado = Vector3.Cross(Vector3.up, paraTv);
            float[] desvios = { -0.55f, 0.55f, -1.5f, 1.5f };
            for (int i = 0; i < 4; i++)
            {
                var l = sofaLocal + lado * desvios[i] + paraTv * (i >= 2 ? -0.1f : 0.15f);
                l.x = Mathf.Clamp(l.x, Mathf.Min(sx * sala.x0, sx * sala.x1) + 0.5f, Mathf.Max(sx * sala.x0, sx * sala.x1) - 0.5f);
                l.z = Mathf.Clamp(l.z, sala.z0 + 0.5f, sala.z1 - 0.5f);
                Nascimentos[i] = NaCasa(l.x, piso + 0.05f, l.z);
                var olhar = go.transform.TransformDirection(tvLocal - l);
                GirosDosNascimentos[i] = Mathf.Atan2(olhar.x, olhar.z) * Mathf.Rad2Deg;
            }

            // os lugares das compras: o chão da sala, depois o da cozinha e o do corredor, de 0,55 em 0,55 m, fora dos
            // móveis (com 0,2 m de folga), da frente do sofá (os nascimentos) e longe das paredes
            var ocupados = new List<Rect>();
            foreach (var pm in pontos)
            {
                if (!pm.OcupaOChao || pm.andar != 0) continue;
                var giro = Quaternion.Euler(0f, pm.giro, 0f);
                var ext = new Vector2(Mathf.Abs((giro * new Vector3(pm.tamanho.x, 0, pm.tamanho.z)).x), Mathf.Abs((giro * new Vector3(pm.tamanho.x, 0, pm.tamanho.z)).z));
                var e2 = new Vector2(Mathf.Abs((giro * new Vector3(pm.tamanho.x, 0, -pm.tamanho.z)).x), Mathf.Abs((giro * new Vector3(pm.tamanho.x, 0, -pm.tamanho.z)).z));
                ext = Vector2.Max(ext, e2) * 0.5f + Vector2.one * 0.2f;
                ocupados.Add(Rect.MinMaxRect(pm.centro.x - ext.x, pm.centro.z - ext.y, pm.centro.x + ext.x, pm.centro.z + ext.y));
            }
            foreach (var tipo in new[] { "sala", "cozinha", "corredor" })
                foreach (var k in p.comodos)
                {
                    if (k.tipo != tipo || k.andar != 0) continue;
                    float a = Mathf.Min(sx * k.x0, sx * k.x1) + 0.5f, b = Mathf.Max(sx * k.x0, sx * k.x1) - 0.5f;
                    var celulas = new List<Vector3>();
                    for (float cz = k.z0 + 0.5f; cz <= k.z1 - 0.5f; cz += 0.55f)
                        for (float cxx = a; cxx <= b; cxx += 0.55f)
                        {
                            var c = new Vector2(cxx, cz);
                            bool livre = true;
                            foreach (var o in ocupados) if (o.Contains(c)) { livre = false; break; }
                            if (!livre) continue;
                            if (tipo == "sala" && Vector2.Distance(c, new Vector2(sofaLocal.x, sofaLocal.z)) < 2.0f) continue;
                            celulas.Add(new Vector3(c.x, piso, c.y));
                        }
                    // das paredes para o meio (as compras encostadas, como quem guardou)
                    var meio = new Vector3(sx * k.Cx, piso, k.Cz);
                    celulas.Sort((u, v) => (v - meio).sqrMagnitude.CompareTo((u - meio).sqrMagnitude));
                    LugaresDasCompras.AddRange(celulas);
                }

            // a porta do abrigo: no fim do corredor, do lado sem a porta de fora (a parede de x0 da planta)
            var corredor = p.comodos.Find(k => k.tipo == "corredor" && k.andar == 0);
            if (corredor != null)
            {
                float lx = sx * (corredor.x0 + M.esp / 2 + 0.02f);
                var porta = new GameObject("Porta do abrigo") { hideFlags = HideFlags.DontSave };
                porta.transform.SetParent(go.transform, false);
                porta.transform.localPosition = new Vector3(lx, piso, corredor.Cz);
                porta.transform.localRotation = Quaternion.Euler(0f, p.espelhar ? -90f : 90f, 0f);
                PortaDoAbrigo.Montar(porta, material);
            }
        }

        /// <summary>O ponto (mundo) está dentro da nossa casa (a caixa da planta, do chão ao telhado).</summary>
        public bool DentroDaCasa(Vector3 mundo)
        {
            if (NossaCasa == null || PlantaDaCasa == null) return false;
            var l = NossaCasa.InverseTransformPoint(mundo);
            var p = PlantaDaCasa;
            // a garagem coberta conta como dentro (do lado dela; a planta é simétrica em x, o espelho só troca o lado)
            float x0 = p.X0, x1 = p.X1;
            if (p.garagem != null && p.garagem.coberta)
            {
                int g = p.espelhar ? -p.garagem.lado : p.garagem.lado;
                if (g > 0) x1 += p.garagem.largura; else x0 -= p.garagem.largura;
            }
            return l.x >= x0 - 0.1f && l.x <= x1 + 0.1f && l.z >= p.Z0 - 0.1f && l.z <= p.Z1 + 0.1f && l.y > -1f && l.y < 6f;
        }

        // ------------------------------------------------------------------ as lojas

        void PorLojaDoDia(LojaDoDia tipo, float W, float D, int lado, int semente, string cor)
        {
            var M = Medidas.DoJogo;
            var r = new System.Random(semente);
            // a de ferragens tem os móveis da 24 horas (as prateleiras, as gôndolas e a geladeira das bebidas, perto do caixa)
            var t = tipo switch
            {
                LojaDoDia.Mercado => TipoDeLoja.Mercado,
                LojaDoDia.Farmacia => TipoDeLoja.Farmacia,
                LojaDoDia.Armas => TipoDeLoja.Armas,
                LojaDoDia.Ferragens => TipoDeLoja.VinteQuatroHoras,
                _ => TipoDeLoja.Comum,
            };
            var p = PlantasDeLoja.ParaOLote(r, M, t, W, D, 1, cor);
            p.espelhar = false;
            p.nome = PrecosDoDiaZero.Placa(tipo);
            if (tipo == LojaDoDia.Roupas) { p.placa = "PlacaVermelha"; p.toldo = "PlacaVermelha"; }
            if (tipo == LojaDoDia.Ferragens) { p.placa = "PlacaAmarela"; p.toldo = null; }
            // as lojas comuns ganham o caixa ao lado da porta (como a 24 horas)
            if (t == TipoDeLoja.Comum && p.balcoes.Count == 0) BalcaoPertoDaPorta(p);
            var loja = new Loja(p, M) { nova = true };
            loja.Montar((a, b) => 0f, 0f);
            Mobilia.Por(loja.moveis, loja.vis, loja.col, p.semente);

            float x = lado > 0 ? _norte : _sul;
            float cx = x + p.W / 2f, cz = lado * (Calcada + 0.4f + p.D / 2f);
            var go = Objeto(PrecosDoDiaZero.Nome(tipo), raiz, Malha(loja.vis, p.nome), Malha(loja.col, p.nome + " (colisão)"), new Vector3(cx, 0f, cz), lado > 0 ? 0f : 180f);
            Construcoes.Add(go);
            triangulos += loja.vis.Triangulos;
            if (lado > 0) _norte = x + p.W + 3f; else _sul = x + p.W + 3f;
            PisosDaLoja(go, p, M);
            Placa(go.transform, p);
            var l = go.AddComponent<LojaDoDiaZero>();
            l.Montar(this, tipo, p, loja.moveis, Matrix4x4.identity, false,
                     new Bounds(new Vector3(0f, 2f, 0f), new Vector3(p.W, 4f, p.D)));
            Lojas.Add(l);
            MalhaDeObra.Devolver(loja.vis);
            MalhaDeObra.Devolver(loja.col);
        }

        // o caixa da loja comum: como o da 24 horas, ao lado da porta da frente, para dentro
        static void BalcaoPertoDaPorta(PlantaDeLoja p)
        {
            var porta = p.ligacoes.Find(l => l.b == null && l.lado == 0);
            float xp = porta != null ? porta.pos : 0f;
            float lado = xp + 2.2f + 0.7f < p.X1 - 0.6f ? 1f : -1f;
            float a = xp + lado * 1.9f, b = a + lado * 0.7f;
            float z0 = p.Z0 + 1.0f, z1 = Mathf.Min(p.Z0 + 3.4f, p.zs - 1.6f);
            if (z1 - z0 > 1f) p.balcoes.Add(Rect.MinMaxRect(Mathf.Min(a, b), z0, Mathf.Max(a, b), z1));
        }

        static void PisosDaLoja(GameObject go, PlantaDeLoja p, Medidas M)
        {
            var zonas = new List<Bounds>();
            var pisos = new List<Superficie>();
            foreach (var k in p.comodos)
            {
                zonas.Add(new Bounds(new Vector3(k.Cx, p.Base(k.andar), k.Cz), new Vector3(k.x1 - k.x0, 1f, k.z1 - k.z0)));
                pisos.Add(TerrenoDoMundo.SomDoPiso(k.piso ?? "Piso"));
            }
            var pz = go.AddComponent<PisoPorZona>();
            pz.zonas = zonas.ToArray();
            pz.superficies = pisos.ToArray();
            pz.resto = Superficie.Concreto;
            float baixo = PlantaDeLoja.Piso, alto = p.Cobertura - M.laje;
            var centro = new Vector3(0f, (baixo + alto) / 2, 0f);
            var tamanho = new Vector3(p.W - 0.2f, alto - baixo, p.D - 0.2f);
            var dentro = new GameObject("Dentro") { hideFlags = HideFlags.DontSave };
            dentro.transform.SetParent(go.transform, false);
            dentro.transform.localPosition = centro;
            dentro.AddComponent<AreaInterna>().tamanho = tamanho;
            if (Application.isPlaying) EcoSoDentro.Criar(go.transform, new Bounds(centro, tamanho));
        }

        // o nome da loja escrito na placa da fachada (a mesma conta do Loja.Fachada)
        void Placa(Transform loja, PlantaDeLoja p)
        {
            var M = Medidas.DoJogo;
            float e = M.esp / 2, h = p.PeDireitoDe(0);
            float y0 = PlantaDeLoja.Piso + Mathf.Min(h - 1.25f, 3.45f), y1 = y0 + 0.9f;
            float a = p.X0 + 0.4f, b = p.X1 - 0.4f;
            if (p.tipo == TipoDeLoja.Farmacia) a += 1.2f;
            int cor = p.placa == "PlacaBranca" || p.placa == "PlacaAmarela" ? ModelosDaEstrada.LetraEscura : ModelosDaEstrada.LetraClara;
            float altura = Mathf.Min(0.55f, (b - a) / Mathf.Max(4, p.nome.Length) * 1.25f);
            var textos = new List<ModelosDaEstrada.Texto>
            {
                new ModelosDaEstrada.Texto { Conteudo = p.nome, U = 0f, V = 0f, Z = 0f, Altura = altura, Alinhamento = 0, Cor = cor },
            };
            var go = new GameObject("Letreiro") { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(loja, false);
            // o texto olha para +z; a frente da loja é o -z dela
            go.transform.localPosition = new Vector3((a + b) / 2, (y0 + y1) / 2, p.Z0 - e - 0.135f);
            go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            PorLetras(go.transform, textos, cor);
        }

        /// <summary>
        /// Um texto numa placa (as letras do <see cref="Letreiro"/>, refeitas se o atlas mudar): no objeto 'pai', na posição
        /// local, olhando para o +z do pai.
        /// </summary>
        public void Escrever(Transform pai, Vector3 local, string texto, float altura, int cor)
        {
            var go = new GameObject("Letreiro") { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(pai, false);
            go.transform.localPosition = local;
            PorLetras(go.transform, new List<ModelosDaEstrada.Texto>
            {
                new ModelosDaEstrada.Texto { Conteudo = texto, U = 0f, V = 0f, Z = 0f, Altura = altura, Alinhamento = 0, Cor = cor },
            }, cor);
        }

        void PorLetras(Transform pai, List<ModelosDaEstrada.Texto> textos, int cor)
        {
            if (materialDasLetras == null) return;
            var malha = Letreiro.Malha(textos, cor);
            if (malha == null) return;
            malha.hideFlags = HideFlags.DontSave;
            var go = new GameObject("Letras") { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(pai, false);
            var f = go.AddComponent<MeshFilter>();
            f.sharedMesh = malha;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = MaterialDasLetras(cor);
            r.shadowCastingMode = ShadowCastingMode.Off;
            _letras.Add((go.transform, textos, cor));
            _filtrosDasLetras.Add(f);
            _versaoDasLetras = Letreiro.Versao;
        }

        Material MaterialDasLetras(int cor)
        {
            if (_materiaisDasLetras == null || _materiaisDasLetras.Length != CoresDasLetras.Length || _materiaisDasLetras[0] == null)
            {
                _materiaisDasLetras = new Material[CoresDasLetras.Length];
                for (int i = 0; i < _materiaisDasLetras.Length; i++)
                {
                    _materiaisDasLetras[i] = new Material(materialDasLetras) { name = $"Letras {i}", hideFlags = HideFlags.DontSave };
                    _materiaisDasLetras[i].SetColor("_Color", CoresDasLetras[i]);
                }
            }
            var atlas = Letreiro.Atlas;
            foreach (var m in _materiaisDasLetras) if (m.mainTexture != atlas) m.SetTexture("_MainTex", atlas);
            return _materiaisDasLetras[Mathf.Clamp(cor, 0, _materiaisDasLetras.Length - 1)];
        }

        void RefazerAsLetras()
        {
            _versaoDasLetras = Letreiro.Versao;
            for (int i = 0; i < _letras.Count; i++)
                if (_filtrosDasLetras[i] != null) Letreiro.Malha(_letras[i].textos, _letras[i].cor, _filtrosDasLetras[i].sharedMesh);
            MaterialDasLetras(0);
        }

        // ------------------------------------------------------------------ o posto

        void PorPosto(float x)
        {
            const int variante = 2;
            var m = ModelosDaEstrada.Posto(variante);
            // o posto do lado norte: a frente (+z do modelo, as bombas) para a rua (o -z do mundo)
            float cx = x + 10f, cz = Calcada + 10f;
            var go = Objeto("Posto", raiz, Malha(m.Malha, "Posto"), m.MalhaDeColisao != null ? Malha(m.MalhaDeColisao, "Posto (colisão)") : null,
                            new Vector3(cx, 0f, cz), 180f);
            Construcoes.Add(go);
            triangulos += m.Malha.Triangulos;
            foreach (var c in m.Colisao)
            {
                var col = new GameObject("Colisão") { hideFlags = HideFlags.DontSave };
                col.transform.SetParent(go.transform, false);
                col.transform.localPosition = c.Centro;
                col.transform.localRotation = Quaternion.Euler(0f, c.GiroY, 0f);
                col.AddComponent<BoxCollider>().size = c.Tamanho;
            }
            for (int cor = 0; cor < 3; cor++) PorLetras(go.transform, m.Textos, cor);
            if (m.Zonas.Count > 0)
            {
                var pz = go.AddComponent<PisoPorZona>();
                pz.zonas = new Bounds[m.Zonas.Count];
                pz.superficies = new Superficie[m.Zonas.Count];
                for (int i = 0; i < m.Zonas.Count; i++)
                {
                    pz.zonas[i] = new Bounds(m.Zonas[i].Centro, m.Zonas[i].Tamanho);
                    pz.superficies[i] = TerrenoDoMundo.SomDoPiso(m.Zonas[i].Piso);
                }
                pz.resto = Superficie.Concreto;
            }
            var dentro = new GameObject("Dentro") { hideFlags = HideFlags.DontSave };
            dentro.transform.SetParent(go.transform, false);
            dentro.transform.localPosition = m.CentroDeDentro;
            dentro.AddComponent<AreaInterna>().tamanho = m.TamanhoDeDentro;

            // a loja do posto: a mesma planta que o ModelosDaEstrada.Posto montou (a semente da variante), na mesma matriz
            var M = Medidas.DoJogo;
            var planta = PlantasDeLoja.DoPosto(new System.Random(variante + 11), M);
            var paraOPosto = Matrix4x4.TRS(new Vector3(0f, 0f, -10f), Quaternion.Euler(0f, 180f, 0f), Vector3.one);
            var pontos = new List<PontoDeMovel>();
            foreach (var pm in m.Moveis)
            {
                // os do modelo já estão no posto: volta para a planta (a LojaDoDiaZero leva tudo pela matriz)
                var q = pm;
                q.centro = paraOPosto.inverse.MultiplyPoint3x4(q.centro);
                q.giro -= 180f;
                if (planta.espelhar) q = q.Espelhado();
                pontos.Add(q);
            }
            var l = go.AddComponent<LojaDoDiaZero>();
            var area = new Bounds(paraOPosto.MultiplyPoint3x4(new Vector3(0f, 2f, 0f)), new Vector3(planta.W, 4f, planta.D));
            l.Montar(this, LojaDoDia.Posto, planta, pontos, paraOPosto, planta.espelhar, area);
            Lojas.Add(l);
            _norte = x + 22f;
        }

        // ------------------------------------------------------------------ as pontas

        /// <summary>
        /// As pontas da rua fechadas: a obra (as barreiras, os cavaletes e os cones atravessados), as cercas de madeira atrás
        /// dos quintais e as paredes invisíveis (o jogador não sai da rua; os carros dos NPCs passam por elas).
        /// </summary>
        void Fechar(float xa, float xb)
        {
            var M = Medidas.DoJogo;
            var obra = new Obra(semente + 77, M);
            // as cercas atrás dos lotes (de madeira, 1,8 m), dos dois lados, e nas pontas
            foreach (int lado in new[] { 1, -1 })
            {
                float z = lado * 44f;
                for (float x = xa - 4f; x < xb + 4f; x += 2.4f)
                {
                    obra.Caixa(new Vector3(x + 1.2f, 0.9f, z), new Vector3(2.38f, 1.8f, 0.06f), "MadeiraVelha", colide: true);
                    obra.Caixa(new Vector3(x, 0.95f, z - lado * 0.05f), new Vector3(0.1f, 1.9f, 0.1f), "Madeira");
                }
            }
            Objeto("Cercas", raiz, Malha(obra.vis, "Cercas"), Malha(obra.col, "Cercas (colisão)"), Vector3.zero, 0f);
            MalhaDeObra.Devolver(obra.vis);
            MalhaDeObra.Devolver(obra.col);

            // a obra nas duas pontas: as barreiras na largura da rua e da calçada, os cavaletes e os cones na frente
            foreach (float x in new[] { xa + 6f, xb - 6f })
            {
                float sentido = x < 0f ? 1f : -1f;
                if (barreira != null)
                    for (float z = -6f; z <= 6.01f; z += 2f)
                        Objeto("Barreira da obra", raiz, barreira, null, new Vector3(x, 0.02f, z), 90f);
                if (cavalete != null)
                    for (int i = 0; i < 3; i++)
                        Objeto("Cavalete", raiz, cavalete, null, new Vector3(x + sentido * 2.2f, 0.02f, -3f + i * 3f), 90f + (i - 1) * 6f);
                if (cone != null)
                    for (int i = 0; i < 5; i++)
                        Objeto("Cone", raiz, cone, null, new Vector3(x + sentido * (3.6f + (i % 2) * 0.6f), 0.02f, -3.6f + i * 1.8f), i * 37f);
                // a parede invisível (da cerca de um lado à do outro)
                var parede = Novo("Parede invisível", raiz);
                parede.transform.localPosition = new Vector3(x - sentido * 0.6f, 3f, 0f);
                parede.AddComponent<BoxCollider>().size = new Vector3(1f, 8f, 90f);
            }
            // e atrás das cercas
            foreach (int lado in new[] { 1, -1 })
            {
                var parede = Novo("Parede invisível (atrás)", raiz);
                parede.transform.localPosition = new Vector3((xa + xb) / 2, 3f, lado * 45f);
                parede.AddComponent<BoxCollider>().size = new Vector3(xb - xa + 20f, 8f, 1f);
            }
            // os hidrantes e as lixeiras na calçada
            var rnd = new System.Random(semente + 5);
            for (float x = xa + 14f; x < xb - 10f; x += 31f)
            {
                int lado = rnd.Next(2) == 0 ? 1 : -1;
                if (hidrante != null) Objeto("Hidrante", raiz, hidrante, null, new Vector3(x, 0.06f, lado * 4.6f), rnd.Next(4) * 90f);
                if (lixeira != null) Objeto("Lixeira", raiz, lixeira, null, new Vector3(x + 9f, 0.06f, -lado * 4.7f), lado > 0 ? 180f : 0f);
            }
        }

        // ------------------------------------------------------------------ os NPCs

        // os lugares dos NPCs: os quintais (regando, cortando a grama, sentados, conversando na cerca), as calçadas
        // (passeando e correndo), as lojas (os atendentes, o faxineiro e quem compra). Cinco ficam doentes (a pedido)
        void PorNpcs()
        {
            var rnd = new System.Random(semente + 909);
            int n = 0;
            LugarDeNpc Lugar(Rotina r, Vector3 ponto, float giro, string tipo)
            {
                var l = new LugarDeNpc { rotina = r, ponto = ponto, giro = giro, tipo = tipo, semente = 1000 + n * 37 };
                n++;
                Npcs.Add(l);
                return l;
            }
            // os quintais: um sim, um não, alguém fazendo alguma coisa (a nossa casa e a do lado ficam livres)
            void Quintais(List<Rect> quintais, int lado)
            {
                for (int i = 0; i < quintais.Count; i++)
                {
                    var q = quintais[i];
                    if (lado > 0 && i <= 1) continue;
                    float zf = lado * Mathf.Lerp(q.yMin, q.yMax, 0.55f);
                    int caso = (i + (lado > 0 ? 0 : 1)) % 3;
                    if (caso == 0)
                    {
                        var p = new Vector3(Mathf.Lerp(q.xMin, q.xMax, 0.3f), 0f, zf);
                        Lugar(Rotina.Regar, p, lado > 0 ? 200f : 20f, rnd.NextDouble() < 0.5 ? "Idoso" : "Civil");
                    }
                    else if (caso == 1)
                    {
                        // a grama cortada em faixas, de um lado ao outro do quintal
                        var caminho = new List<Vector3>();
                        float za = lado * (q.yMin + 0.3f), zb = lado * (q.yMax - 0.3f);
                        int faixas = 4;
                        for (int f = 0; f < faixas; f++)
                        {
                            float z = Mathf.Lerp(za, zb, f / (float)(faixas - 1));
                            float xa = f % 2 == 0 ? q.xMin + 0.8f : q.xMax - 0.8f, xb = f % 2 == 0 ? q.xMax - 0.8f : q.xMin + 0.8f;
                            caminho.Add(new Vector3(xa, 0f, z));
                            caminho.Add(new Vector3(xb, 0f, z));
                        }
                        // e de volta pela outra ordem (o laço)
                        for (int f = faixas - 1; f >= 0; f--) caminho.Add(caminho[f * 2 + 1]);
                        var l = Lugar(Rotina.CortarGrama, caminho[0], 90f, rnd.NextDouble() < 0.5 ? "Trabalhador" : "Civil");
                        l.caminho = caminho.ToArray();
                    }
                    else
                    {
                        // dois vizinhos conversando perto da calçada
                        var p = new Vector3(Mathf.Lerp(q.xMin, q.xMax, 0.6f), 0f, lado * (q.yMin + 0.4f));
                        var a = Lugar(Rotina.Conversar, p + Vector3.right * 0.55f, 270f, "Civil");
                        var b = Lugar(Rotina.Conversar, p - Vector3.right * 0.55f, 90f, rnd.NextDouble() < 0.5 ? "Idoso" : "Estudante");
                        a.par = Npcs.IndexOf(b);
                        b.par = Npcs.IndexOf(a);
                    }
                }
            }
            Quintais(_quintaisNorte, 1);
            Quintais(_quintaisSul, -1);

            // as calçadas: quem passeia (ida e volta, de uma ponta à outra) e quem corre
            float xa0 = Inicio + 10f, xb0 = Fim - 10f;
            for (int i = 0; i < 7; i++)
            {
                int lado = i % 2 == 0 ? 1 : -1;
                float z = lado * (5.0f + (i % 3) * 0.35f);
                var caminho = new[] { new Vector3(xa0, 0.06f, z), new Vector3(xb0, 0.06f, z), new Vector3(xb0, 0.06f, z + lado * 0.4f), new Vector3(xa0, 0.06f, z + lado * 0.4f) };
                string tipo = new[] { "Civil", "Estudante", "Idoso", "Civil", "Trabalhador", "Civil", "Estudante" }[i];
                var l = Lugar(i == 5 ? Rotina.Correr : Rotina.Passear, caminho[0], 90f, tipo);
                l.caminho = caminho;
            }

            // as lojas: os atendentes; no mercado, o faxineiro e quem compra
            foreach (var loja in Lojas)
            {
                for (int i = 0; i < loja.Atendentes.Count; i++)
                {
                    var (p, g) = loja.Atendentes[i];
                    var l = Lugar(Rotina.Atendente, p, g, loja.Tipo == LojaDoDia.Farmacia ? "Médico" : loja.Tipo == LojaDoDia.Armas ? "Policial" : "Civil");
                    l.loja = loja;
                }
                if (loja.Tipo == LojaDoDia.Mercado && loja.Corredor.Length > 1)
                {
                    var f = Lugar(Rotina.Faxineiro, loja.Corredor[0], 90f, "Trabalhador");
                    f.loja = loja;
                    f.caminho = loja.Corredor;
                    for (int k = 0; k < 2; k++)
                    {
                        var c = Lugar(Rotina.Comprar, loja.Corredor[0], 90f, k == 0 ? "Idoso" : "Civil");
                        c.loja = loja;
                        var invertido = (Vector3[])loja.Corredor.Clone();
                        if (k == 0) System.Array.Reverse(invertido);
                        c.caminho = invertido;
                    }
                }
            }

            // os doentes (5): o faxineiro do mercado, o primeiro atendente do mercado, o do posto, alguém na calçada e quem rega
            var doentes = new List<LugarDeNpc>();
            void Doente(System.Predicate<LugarDeNpc> qual)
            {
                var l = Npcs.Find(x => !x.doente && qual(x));
                if (l != null) { l.doente = true; doentes.Add(l); }
            }
            Doente(l => l.rotina == Rotina.Faxineiro);
            Doente(l => l.rotina == Rotina.Atendente && l.loja != null && l.loja.Tipo == LojaDoDia.Mercado);
            Doente(l => l.rotina == Rotina.Atendente && l.loja != null && l.loja.Tipo == LojaDoDia.Posto);
            Doente(l => l.rotina == Rotina.Passear);
            Doente(l => l.rotina == Rotina.Regar);
            while (doentes.Count < 5)
            {
                int antes = doentes.Count;
                Doente(l => l.rotina == Rotina.Passear || l.rotina == Rotina.Conversar || l.rotina == Rotina.Atendente);
                if (doentes.Count == antes) break;
            }
        }
    }
}

namespace ZombieCityCleaner.DiaZero
{
    /// <summary>A luz da cena do Dia 0 aberta sozinha no Editor: no Play (por cima da cena do mundo), a luz é a do clima.</summary>
    public class LuzDaPrevia : MonoBehaviour
    {
        void Awake()
        {
            if (Application.isPlaying) gameObject.SetActive(false);
        }
    }
}
