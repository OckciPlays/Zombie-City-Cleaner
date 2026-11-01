using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using ZombieCityCleaner.Loot;
using ZombieCityCleaner.Mundo;
using ZombieCityCleaner.Player;

namespace ZombieCityCleaner.DiaZero
{
    /// <summary>
    /// Uma loja da rua do Dia 0 (01/11/2026, a pedido: "pegar no mercado, pôr no carrinho e passar no caixa"), no objeto
    /// dela (montada pela <see cref="RuaDoDiaZero"/>):
    ///  - os produtos nos móveis do salão (as prateleiras, as gôndolas, os expositores e as vitrines: os pontos dos móveis
    ///    da obra), cada fileira um <see cref="ProdutoNaPrateleira"/> (E põe um no carrinho). As armas, nos expositores
    ///    de armas; o resto, por todos os outros, repetindo para as prateleiras ficarem cheias;
    ///  - na loja de roupas, as roupas como itens (<see cref="RoupaNaLoja"/>: E prova) e as mochilas;
    ///  - os caixas (os balcões da planta, <see cref="CaixaDaLoja"/>: E abre a tela do caixa) e onde ficam os atendentes;
    ///  - no mercado, o corredor em volta das gôndolas (o faxineiro e quem compra andam por ele).
    /// O carrinho é de quem joga (<see cref="Carrinho"/>), e é desta loja: sair sem pagar devolve tudo.
    /// </summary>
    public class LojaDoDiaZero : MonoBehaviour
    {
        public LojaDoDia Tipo { get; private set; }
        public string Nome => PrecosDoDiaZero.Nome(Tipo);
        /// <summary>A caixa da loja (o salão e os fundos), nas coordenadas deste objeto.</summary>
        public Bounds Area { get; private set; }
        /// <summary>Onde os atendentes ficam (no mundo) e para onde olham.</summary>
        public readonly List<(Vector3 ponto, float giro)> Atendentes = new List<(Vector3, float)>();
        /// <summary>O corredor do salão (no mundo), em laço: o faxineiro e quem compra (só no mercado).</summary>
        public Vector3[] Corredor { get; private set; } = new Vector3[0];
        /// <summary>Quantos produtos (e roupas) ficaram à mostra, e quantos da tabela não acharam lugar.</summary>
        public int Mostrados { get; private set; }
        public int SemLugar { get; private set; }

        /// <summary>O ponto (mundo) está na loja, com a folga.</summary>
        public bool Dentro(Vector3 mundo, float folga = 0f)
        {
            var b = Area;
            b.Expand(folga * 2f);
            return b.Contains(transform.InverseTransformPoint(mundo));
        }

        // uma fileira de um móvel: o meio (nas coordenadas do móvel), a largura, para onde ela olha (no móvel), se é de arma
        struct Fileira
        {
            public PontoDeMovel movel;
            public Vector3 meio;
            public float largura;
            public Vector3 frente;
            public bool deArma, pequena, fria;
        }

        RuaDoDiaZero _rua;

        // a malha do produto (sem modelo, uma caixa de papelão no lugar: o produto continua à venda) e o material
        Mesh MalhaDe(Item item) => ItensNoMundo.Instancia?.MalhaDe(item) ?? Caixinha();
        Material MaterialDe(Item item) => ItensNoMundo.Instancia != null && ItensNoMundo.Instancia.MalhaDe(item) != null
            ? ItensNoMundo.Instancia.MaterialDe(item) : _rua.material;

        static Mesh _caixinha;
        static Mesh Caixinha()
        {
            if (_caixinha != null) return _caixinha;
            var o = new Obra(3, Medidas.DoJogo);
            o.Caixa(new Vector3(0f, 0.06f, 0f), new Vector3(0.22f, 0.12f, 0.16f), "MadeiraVelha");
            o.Caixa(new Vector3(0f, 0.121f, 0f), new Vector3(0.12f, 0.004f, 0.08f), "PlacaBranca");
            _caixinha = o.vis.ParaMesh("Caixa do produto");
            MalhaDeObra.Devolver(o.vis);
            MalhaDeObra.Devolver(o.col);
            return _caixinha;
        }

        public void Montar(RuaDoDiaZero rua, LojaDoDia tipo, PlantaDeLoja p, List<PontoDeMovel> pontos, Matrix4x4 plantaParaLocal, bool espelhar, Bounds area)
        {
            _rua = rua;
            Tipo = tipo;
            Area = area;
            Vector3 L(Vector3 q) => plantaParaLocal.MultiplyPoint3x4(espelhar ? new Vector3(-q.x, q.y, q.z) : q);
            Quaternion G(float giro) => plantaParaLocal.rotation * Quaternion.Euler(0f, espelhar ? -giro : giro, 0f);
            Vector3 W(Vector3 local) => transform.TransformPoint(local);
            float Yaw(Quaternion local) => (transform.rotation * local).eulerAngles.y;
            float piso = PlantaDeLoja.Piso;

            // os caixas: os balcões da planta (com a registradora em cima) e os atendentes atrás
            var detalhes = new Obra(p.semente + 5, Medidas.DoJogo);
            var porta = p.ligacoes.Find(l => l.b == null && l.lado == 0);
            float xp = porta != null ? porta.pos : 0f;
            for (int i = 0; i < p.balcoes.Count; i++)
            {
                var r = p.balcoes[i];
                var caixa = Filho($"Caixa {i + 1}");
                caixa.localPosition = L(new Vector3(r.center.x, piso + 0.95f, r.center.y));
                caixa.localRotation = G(0f);
                var col = caixa.gameObject.AddComponent<BoxCollider>();
                col.isTrigger = true;
                col.size = new Vector3(r.width + 0.1f, 0.5f, r.height + 0.1f);
                caixa.gameObject.AddComponent<CaixaDaLoja>().loja = this;
                // a registradora: no meio do balcão (no mercado, na ponta de dentro)
                var reg = new Vector3(r.center.x, piso + 1.0f, tipo == LojaDoDia.Mercado ? r.yMax - 0.4f : r.center.y);
                detalhes.Caixa(reg + Vector3.up * 0.08f, new Vector3(0.42f, 0.16f, 0.36f), "Metal");
                detalhes.Caixa(reg + new Vector3(0f, 0.24f, 0.05f), new Vector3(0.32f, 0.2f, 0.04f), "Farol");

                // o atendente
                Vector3 ponto;
                float giro;
                if (tipo == LojaDoDia.Mercado)
                {
                    if (i >= 2) continue;
                    ponto = new Vector3(r.xMin - 0.55f, piso, r.center.y);
                    giro = 90f;
                }
                else if (r.width > r.height)
                {
                    // o balcão atravessado (a farmácia, a loja de armas): atrás dele, no meio
                    ponto = new Vector3(r.center.x, piso, r.yMax + 0.6f);
                    giro = 180f;
                }
                else
                {
                    // o caixa ao lado da porta: do lado de longe da porta
                    bool direita = r.center.x > xp;
                    ponto = new Vector3(direita ? r.xMax + 0.5f : r.xMin - 0.5f, piso, r.center.y);
                    giro = direita ? 270f : 90f;
                }
                Atendentes.Add((W(L(ponto)), Yaw(G(giro))));
            }
            if (detalhes.vis.Triangulos > 0)
            {
                var go = new GameObject("Registradoras") { hideFlags = HideFlags.DontSave };
                go.transform.SetParent(transform, false);
                go.transform.localPosition = plantaParaLocal.MultiplyPoint3x4(Vector3.zero);
                go.transform.localRotation = plantaParaLocal.rotation;
                if (espelhar) go.transform.localScale = new Vector3(-1f, 1f, 1f);
                go.AddComponent<MeshFilter>().sharedMesh = detalhes.vis.ParaMesh("Registradoras");
                go.GetComponent<MeshFilter>().sharedMesh.hideFlags = HideFlags.DontSave;
                go.AddComponent<MeshRenderer>().sharedMaterial = rua.material;
            }
            MalhaDeObra.Devolver(detalhes.vis);
            MalhaDeObra.Devolver(detalhes.col);

            // as fileiras dos móveis do salão
            var fileiras = new List<Fileira>();
            float gx0 = float.MaxValue, gx1 = float.MinValue, gz0 = float.MaxValue, gz1 = float.MinValue;
            foreach (var pm in pontos)
            {
                if (pm.comodo != "salao" || pm.andar != 0) continue;
                float sy = pm.tamanho.y, sz = pm.tamanho.z, sx = pm.tamanho.x;
                void Nivel(float y, float z, Vector3 frente, float largura, bool deArma = false, bool pequena = false, bool fria = false) =>
                    fileiras.Add(new Fileira { movel = pm, meio = new Vector3(0f, y, z), largura = largura, frente = frente, deArma = deArma, pequena = pequena, fria = fria });
                switch (pm.tipo)
                {
                    case "prateleira":
                        foreach (float y in new[] { 0.12f, 0.56f, 0.98f, 1.40f }) Nivel(y * sy / 1.9f, sz * 0.18f, Vector3.forward, sx - 0.12f);
                        break;
                    case "gondola":
                        // a gôndola: as prateleiras nos dois lados compridos (o x dela)
                        foreach (float y in new[] { 0.12f, 0.61f, 1.06f })
                        {
                            fileiras.Add(new Fileira { movel = pm, meio = new Vector3(sx * 0.3f, y * sy / 1.6f, 0f), largura = sz - 0.15f, frente = Vector3.right });
                            fileiras.Add(new Fileira { movel = pm, meio = new Vector3(-sx * 0.3f, y * sy / 1.6f, 0f), largura = sz - 0.15f, frente = Vector3.left });
                        }
                        var c = pm.centro;
                        float meiaX = Mathf.Abs(Mathf.Sin(pm.giro * Mathf.Deg2Rad)) > 0.7f ? sz / 2 : sx / 2;
                        float meiaZ = Mathf.Abs(Mathf.Sin(pm.giro * Mathf.Deg2Rad)) > 0.7f ? sx / 2 : sz / 2;
                        gx0 = Mathf.Min(gx0, c.x - meiaX); gx1 = Mathf.Max(gx1, c.x + meiaX);
                        gz0 = Mathf.Min(gz0, c.z - meiaZ); gz1 = Mathf.Max(gz1, c.z + meiaZ);
                        break;
                    case "expositor refrigerado":
                        foreach (float y in new[] { 0.16f, 0.56f, 0.96f, 1.36f }) Nivel(y * sy / 2f, sz * 0.05f, Vector3.forward, sx - 0.2f, fria: true);
                        break;
                    case "expositor de armas":
                        Nivel(1.12f * sy / 2f, -sz * 0.15f, Vector3.forward, sx - 0.2f, deArma: true);
                        Nivel(1.58f * sy / 2f, -sz * 0.15f, Vector3.forward, sx - 0.2f, deArma: true);
                        Nivel(0.90f * sy / 2f, sz * 0.05f, Vector3.forward, sx - 0.2f, pequena: true);
                        break;
                    case "vitrine":
                        Nivel(0.62f * sy, 0f, Vector3.forward, sx - 0.2f, pequena: true);
                        break;
                }
            }

            // o corredor do mercado: em volta do bloco das gôndolas
            if (tipo == LojaDoDia.Mercado)
            {
                float a = gx0 < gx1 ? gx0 - 0.8f : p.X0 + 1.6f, b = gx0 < gx1 ? gx1 + 0.8f : p.X1 - 1.6f;
                float frente = gz0 < gz1 ? gz0 - 0.8f : p.Z0 + 5f, fundo = gz0 < gz1 ? Mathf.Min(gz1 + 0.8f, p.zs - 0.9f) : p.zs - 1.5f;
                a = Mathf.Max(a, p.X0 + 1.3f);
                b = Mathf.Min(b, p.X1 - 1.3f);
                Corredor = new[]
                {
                    W(L(new Vector3(a, piso, frente))), W(L(new Vector3(b, piso, frente))),
                    W(L(new Vector3(b, piso, fundo))), W(L(new Vector3((a + b) / 2, piso, fundo))),
                    W(L(new Vector3((a + b) / 2, piso, frente))), W(L(new Vector3(a, piso, frente))),
                    W(L(new Vector3(a, piso, fundo))), W(L(new Vector3(b, piso, fundo))), W(L(new Vector3(b, piso, frente))),
                };
            }

            if (tipo == LojaDoDia.Roupas)
            {
                Roupas(rua, fileiras, L, G);
                Mochilas(rua, p, pontos, L, G);
            }
            else Produtos(fileiras, L, G);
            Luzes(p, L);
        }

        // as lâmpadas acesas do salão (a loja aberta, de dia): uma a cada ~4,5 m, sem sombra
        void Luzes(PlantaDeLoja p, System.Func<Vector3, Vector3> L)
        {
            var s = p.Salao;
            float alto = PlantaDeLoja.Piso + p.PeDireitoDe(0) - 0.35f;
            int nx = Mathf.Max(1, Mathf.RoundToInt((s.x1 - s.x0) / 4.5f)), nz = Mathf.Max(1, Mathf.RoundToInt((s.z1 - s.z0) / 4.5f));
            for (int i = 0; i < nx; i++)
                for (int k = 0; k < nz; k++)
                {
                    var t = Filho("Lâmpada");
                    t.localPosition = L(new Vector3(Mathf.Lerp(s.x0, s.x1, (i + 0.5f) / nx), alto, Mathf.Lerp(s.z0, s.z1, (k + 0.5f) / nz)));
                    var luz = t.gameObject.AddComponent<Light>();
                    luz.type = LightType.Point;
                    luz.range = 7.5f;
                    luz.intensity = 1.6f;
                    luz.color = new Color(1f, 0.97f, 0.9f);
                    luz.shadows = LightShadows.None;
                }
        }

        Transform Filho(string nome)
        {
            var go = new GameObject(nome) { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(transform, false);
            return go.transform;
        }

        // a fileira no lugar dela: o objeto da fileira (o meio dela, virado como o móvel)
        Transform Lugar(Fileira f, System.Func<Vector3, Vector3> L, System.Func<float, Quaternion> G, string nome)
        {
            var t = Filho(nome);
            var giro = G(f.movel.giro);
            t.localPosition = L(f.movel.centro) + giro * f.meio;
            t.localRotation = giro * Quaternion.LookRotation(f.frente, Vector3.up);
            return t;
        }

        // ------------------------------------------------------------------ os produtos

        void Produtos(List<Fileira> fileiras, System.Func<Vector3, Vector3> L, System.Func<float, Quaternion> G)
        {
            var mundo = ItensNoMundo.Instancia;
            var produtos = PrecosDoDiaZero.Produtos(Tipo);
            if (mundo == null || produtos.Length == 0) return;   // fora do jogo (a cena aberta sozinha), sem os modelos
            bool Bebida(Item i) => Itens.Loot(i)?.tipo == TipoDeLoot.Bebida;
            bool Arma(Item i) => Itens.EhArma(i) || Itens.EhBranca(i);
            // o que cabe em cada fileira: no expositor de armas, as armas; na geladeira, as bebidas; na vitrine e na
            // prateleirinha do expositor, o que é pequeno (não as armas longas); nas outras, o resto (as armas só sem expositor)
            bool temExpositor = fileiras.Exists(f => f.deArma);
            bool Serve(Fileira f, Item i)
            {
                if (f.deArma) return Arma(i);
                if (f.fria) return Bebida(i);
                if (f.pequena) return !Itens.EhArma(i) || i == Item.Pistola;
                return !Arma(i) || !temExpositor;
            }
            var usadas = new bool[fileiras.Count];
            var postos = new HashSet<int>();
            var comModelo = new List<int>();
            for (int i = 0; i < produtos.Length; i++) comModelo.Add(i);
            // 1) cada produto num lugar (as armas primeiro: os expositores são poucos)
            comModelo.Sort((a, b) => (Arma(produtos[b].item) ? 1 : 0).CompareTo(Arma(produtos[a].item) ? 1 : 0));
            foreach (int i in comModelo)
                for (int k = 0; k < fileiras.Count; k++)
                {
                    if (usadas[k] || !Serve(fileiras[k], produtos[i].item)) continue;
                    if (!Mostrar(fileiras[k], i, produtos[i], L, G)) continue;
                    usadas[k] = true;
                    postos.Add(i);
                    break;
                }
            // 2) as fileiras que sobraram, repetindo os produtos que servem nelas (as prateleiras cheias)
            int volta = 0;
            for (int k = 0; k < fileiras.Count; k++)
            {
                if (usadas[k]) continue;
                for (int n = 0; n < comModelo.Count; n++)
                {
                    int i = comModelo[(volta + n) % comModelo.Count];
                    if (!Serve(fileiras[k], produtos[i].item) || !Mostrar(fileiras[k], i, produtos[i], L, G)) continue;
                    usadas[k] = true;
                    volta = (volta + n + 1) % comModelo.Count;
                    break;
                }
            }
            Mostrados = postos.Count;
            SemLugar = produtos.Length - postos.Count;
            if (SemLugar > 0)
            {
                var faltam = new List<string>();
                for (int i = 0; i < produtos.Length; i++) if (!postos.Contains(i)) faltam.Add(produtos[i].Nome);
                Debug.LogWarning($"[Dia 0] {Nome}: sem lugar nas prateleiras ({fileiras.Count} fileiras): {string.Join(", ", faltam)}.");
            }
        }

        // os produtos de uma fileira: as cópias lado a lado (as armas, uma ou duas) e o colisor da mira
        bool Mostrar(Fileira f, int indice, Produto produto, System.Func<Vector3, Vector3> L, System.Func<float, Quaternion> G)
        {
            var malha = MalhaDe(produto.item);
            if (malha == null) return false;
            bool arma = Itens.EhArma(produto.item) || Itens.EhBranca(produto.item);
            // o giro de cada cópia na fileira (o z da fileira olha para quem compra): as armas deitadas de lado, no
            // expositor de armas penduradas com o cano ao longo da fileira
            Quaternion giro = f.deArma ? Quaternion.LookRotation(Vector3.right, Vector3.up)
                            : arma ? Quaternion.Euler(0f, 90f, 90f) : Quaternion.Euler(0f, 180f, 0f);
            var b = malha.bounds;
            Vector3 min = Vector3.positiveInfinity, max = Vector3.negativeInfinity;
            for (int c = 0; c < 8; c++)
            {
                var q = giro * new Vector3((c & 1) == 0 ? b.min.x : b.max.x, (c & 2) == 0 ? b.min.y : b.max.y, (c & 4) == 0 ? b.min.z : b.max.z);
                min = Vector3.Min(min, q);
                max = Vector3.Max(max, q);
            }
            var tam = max - min;
            float largura = Mathf.Max(0.05f, tam.x);
            int quantas = arma ? (f.deArma || largura > f.largura * 0.5f ? 1 : 2) : Mathf.Clamp(Mathf.FloorToInt(f.largura / (largura + 0.03f)), 1, 7);
            if (largura > f.largura + 0.15f && !f.deArma) return false;
            var t = Lugar(f, L, G, $"Produto: {produto.Nome}");
            float passo = largura + 0.03f, ini = -(quantas - 1) * passo / 2;
            var material = MaterialDe(produto.item);
            for (int k = 0; k < quantas; k++)
            {
                var copia = new GameObject("Cópia") { hideFlags = HideFlags.DontSave };
                copia.transform.SetParent(t, false);
                // apoiado na fileira (no expositor de armas, pendurado: o meio na altura da fileira)
                float y = f.deArma ? -(min.y + max.y) / 2 : -min.y + 0.005f;
                var centro = new Vector3(-(min.x + max.x) / 2 + ini + k * passo, y, -(min.z + max.z) / 2);
                copia.transform.localPosition = centro;
                copia.transform.localRotation = giro;
                copia.AddComponent<MeshFilter>().sharedMesh = malha;
                var r = copia.AddComponent<MeshRenderer>();
                r.sharedMaterial = material;
                r.shadowCastingMode = ShadowCastingMode.Off;
            }
            var col = t.gameObject.AddComponent<BoxCollider>();
            col.isTrigger = true;
            float alto = Mathf.Max(0.18f, tam.y + 0.04f);
            col.center = new Vector3(0f, f.deArma ? 0f : alto / 2, 0.02f);
            col.size = new Vector3(Mathf.Min(f.largura, quantas * passo + 0.05f), alto, Mathf.Max(0.3f, tam.z + 0.1f));
            var pp = t.gameObject.AddComponent<ProdutoNaPrateleira>();
            pp.loja = this;
            pp.indice = indice;
            return true;
        }

        // ------------------------------------------------------------------ as roupas

        // a loja de roupas: as roupas à venda nas prateleiras, várias por fileira (as mochilas ficam na mesa delas, Mochilas)
        void Roupas(RuaDoDiaZero rua, List<Fileira> fileiras, System.Func<Vector3, Vector3> L, System.Func<float, Quaternion> G)
        {
            var aVenda = new List<(GameObject prefab, int lugar, byte valor)>();
            if (rua.roupas != null)
                foreach (var prefab in rua.roupas)
                {
                    if (prefab == null) continue;
                    string peca = prefab.name.StartsWith("Item_") ? prefab.name.Substring(5) : prefab.name;
                    int lugar = AparenciaDoJogador.LugarDaPeca(peca, out byte valor);
                    if (lugar >= 0 && PrecosDoDiaZero.PrecoDaRoupa(peca) > 0) aVenda.Add((prefab, lugar, valor));
                }
            aVenda.Sort((a, b) => a.lugar != b.lugar ? a.lugar.CompareTo(b.lugar) : a.valor.CompareTo(b.valor));
            int k = 0, mostradas = 0;
            foreach (var f in fileiras)
            {
                if (k >= aVenda.Count) break;
                var t = Lugar(f, L, G, "Roupas");
                // até 3 por fileira, cada uma com o seu colisor (vindo do prefab)
                int n = Mathf.Clamp(Mathf.FloorToInt(f.largura / 0.42f), 1, 4);
                for (int i = 0; i < n && k < aVenda.Count; i++, k++)
                {
                    var (prefab, lugar, valor) = aVenda[k];
                    var go = Instantiate(prefab, t, false);
                    go.hideFlags = HideFlags.DontSave;
                    go.name = prefab.name;
                    go.transform.localPosition = new Vector3(-f.largura / 2 + f.largura * (i + 0.5f) / n, 0.01f, 0f);
                    go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                    foreach (var c in go.GetComponentsInChildren<Collider>()) c.isTrigger = true;
                    var col = go.GetComponentInChildren<Collider>();
                    var alvo = col != null ? col.gameObject : go;
                    if (col == null) alvo.AddComponent<BoxCollider>().isTrigger = true;
                    var r = alvo.AddComponent<RoupaNaLoja>();
                    r.loja = this;
                    r.lugar = lugar;
                    r.valor = valor;
                    mostradas++;
                }
            }
            Mostrados = mostradas;
            SemLugar = Mathf.Max(0, aVenda.Count - k);
            if (SemLugar > 0) Debug.LogWarning($"[Dia 0] {Nome}: {SemLugar} roupa(s) sem lugar ({fileiras.Count} fileiras).");
        }

        // ------------------------------------------------------------------ a seção das mochilas

        /// <summary>A mesa das mochilas (null: sem lugar no salão).</summary>
        public Transform SecaoDasMochilas { get; private set; }

        // a seção das mochilas da loja de roupas (01/11/2026, a pedido: "uma parte que vende mochilas"): a mesa de exposição
        // no meio do salão, com o degrau atrás, as três mochilas em dois andares (na frente, baixo; no degrau, alto) e a
        // placa "MOCHILAS" no poste. A frente da mesa olha para a porta. Ela vai para o primeiro lugar livre do meio do
        // salão (longe dos móveis e do caixa, com 1 m de passagem em volta)
        void Mochilas(RuaDoDiaZero rua, PlantaDeLoja p, List<PontoDeMovel> pontos, System.Func<Vector3, Vector3> L, System.Func<float, Quaternion> G)
        {
            var s = p.Salao;
            float piso = PlantaDeLoja.Piso;
            const float largura = 2.6f, fundo = 1.1f, passagem = 1.0f;
            var ocupado = new List<Rect>();
            foreach (var pm in pontos)
            {
                if (pm.comodo != "salao" || pm.andar != 0 || !pm.OcupaOChao) continue;
                bool deitado = Mathf.Abs(Mathf.Sin(pm.giro * Mathf.Deg2Rad)) > 0.7f;
                float mx = (deitado ? pm.tamanho.z : pm.tamanho.x) / 2, mz = (deitado ? pm.tamanho.x : pm.tamanho.z) / 2;
                ocupado.Add(Rect.MinMaxRect(pm.centro.x - mx, pm.centro.z - mz, pm.centro.x + mx, pm.centro.z + mz));
            }
            ocupado.AddRange(p.balcoes);
            Vector2? lugar = null;
            float cx = (s.x0 + s.x1) / 2, cz = (s.z0 + s.z1) / 2;
            foreach (float dz in new[] { 0f, -1.2f, 1.2f, -2.4f, 2.4f })
            {
                foreach (float dx in new[] { 0f, -1.5f, 1.5f, -3f, 3f })
                {
                    var c = new Vector2(cx + dx, cz + dz);
                    var r = Rect.MinMaxRect(c.x - largura / 2 - passagem, c.y - fundo / 2 - passagem, c.x + largura / 2 + passagem, c.y + fundo / 2 + passagem);
                    if (r.xMin < s.x0 || r.xMax > s.x1 || r.yMin < s.z0 || r.yMax > s.z1) continue;
                    bool livre = true;
                    foreach (var o in ocupado)
                        if (o.Overlaps(r)) { livre = false; break; }
                    if (!livre) continue;
                    lugar = c;
                    break;
                }
                if (lugar.HasValue) break;
            }
            if (!lugar.HasValue)
            {
                Debug.LogWarning($"[Dia 0] {Nome}: sem lugar no salão para a seção das mochilas.");
                return;
            }
            // a mesa: a frente dela (o +z) para a porta (o -z da planta)
            var mesa = Filho("Seção de mochilas");
            mesa.localPosition = L(new Vector3(lugar.Value.x, piso, lugar.Value.y));
            mesa.localRotation = G(180f);
            SecaoDasMochilas = mesa;
            var o2 = new Obra(p.semente + 9, Medidas.DoJogo);
            o2.Caixa(new Vector3(0f, 0.36f, 0.1f), new Vector3(largura - 0.1f, 0.72f, fundo - 0.4f), "Madeira");
            o2.Caixa(new Vector3(0f, 0.74f, 0.1f), new Vector3(largura, 0.04f, fundo - 0.3f), "Batente");
            o2.Caixa(new Vector3(0f, 0.53f, -0.33f), new Vector3(largura - 0.1f, 1.06f, 0.42f), "Madeira");
            o2.Caixa(new Vector3(0f, 1.08f, -0.33f), new Vector3(largura, 0.04f, 0.5f), "Batente");
            // a placa no poste, num lado, atrás do degrau
            o2.Caixa(new Vector3(largura / 2 - 0.15f, 1.05f, -0.5f), new Vector3(0.06f, 2.1f, 0.06f), "Metal");
            o2.Caixa(new Vector3(largura / 2 - 0.15f, 2.15f, -0.5f), new Vector3(1.2f, 0.36f, 0.04f), "PlacaVermelha");
            var desenho = new GameObject("Mesa") { hideFlags = HideFlags.DontSave };
            desenho.transform.SetParent(mesa, false);
            var malha = o2.vis.ParaMesh("Seção de mochilas");
            malha.hideFlags = HideFlags.DontSave;
            desenho.AddComponent<MeshFilter>().sharedMesh = malha;
            desenho.AddComponent<MeshRenderer>().sharedMaterial = rua.material;
            MalhaDeObra.Devolver(o2.vis);
            MalhaDeObra.Devolver(o2.col);
            var corpo = mesa.gameObject.AddComponent<BoxCollider>();
            corpo.center = new Vector3(0f, 0.53f, 0f);
            corpo.size = new Vector3(largura, 1.06f, fundo);
            // as letras na frente da placa (o texto olha para o +z da mesa: para quem chega pela porta)
            rua.Escrever(mesa, new Vector3(largura / 2 - 0.15f, 2.15f, -0.475f), "MOCHILAS", 0.16f, ModelosDaEstrada.LetraClara);

            // as mochilas: a pequena, a média e a grande, cada uma no seu terço, na frente (baixo) e no degrau (alto)
            if (ItensNoMundo.Instancia == null) return;
            var produtos = PrecosDoDiaZero.Produtos(Tipo);
            float terco = (largura - 0.2f) / produtos.Length;
            var movel = new PontoDeMovel { tipo = "mesa de mochilas", centro = new Vector3(lugar.Value.x, piso, lugar.Value.y), giro = 180f, comodo = "salao" };
            int postas = 0;
            for (int i = 0; i < produtos.Length; i++)
            {
                float x = -(largura - 0.2f) / 2 + terco * (i + 0.5f);
                foreach (var (y, z) in new[] { (0.765f, 0.22f), (1.105f, -0.33f) })
                {
                    var f = new Fileira { movel = movel, meio = new Vector3(x, y, z), largura = terco - 0.05f, frente = Vector3.forward };
                    if (Mostrar(f, i, produtos[i], L, G)) postas++;
                }
            }
            if (postas < produtos.Length) Debug.LogWarning($"[Dia 0] {Nome}: só {postas} fileiras de mochilas couberam na mesa.");
        }
    }
}
