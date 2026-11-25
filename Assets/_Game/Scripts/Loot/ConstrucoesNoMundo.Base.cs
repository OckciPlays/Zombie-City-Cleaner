using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using ZombieCityCleaner.Mundo;
using ZombieCityCleaner.Player;

namespace ZombieCityCleaner.Loot
{
    /// <summary>
    /// A base (24/11/2026, a pedido: "começar a realizar o modo construção do jogo (criar muros e tudo — a partir disso acho
    /// que precisamos colocar uma espécie de núcleo da base, somente com isso conseguiríamos construir ao redor, como se fosse
    /// uma chama grande ou um braseiro grande e ele teria uma área que conseguimos criar dentro dela). Depois daria para upar
    /// ela com alguns itens específicos (tipo a cabeça dos bosses, ou o olho do brutamonte, garganta do long e cabeça do Oco)"):
    /// <list type="bullet">
    /// <item>o núcleo da base: um braseiro grande de sucata, sempre aceso, com a roda de pedras. A área dele (um raio) é onde
    /// se montam o muro, o portão e a barricada (<see cref="PodeMontarAqui"/>, no cliente e no anfitrião). Um núcleo não
    /// fica dentro da área de outro;</item>
    /// <item>o nível (no <c>valor</c>): 1, a área de 12 m; o E com os troféus dos zumbis especiais sobe o nível: 3 cabeças de
    /// Oco (2, 18 m), 2 gargantas do Long (3, 26 m), o olho do Brutamonte (4, 35 m), a cabeça de um chefe (5, 45 m). O fogo
    /// cresce com o nível;</item>
    /// <item>o muro de toras (3 × 2,2 m) e o portão de toras (que abre e fecha no E): sólidos, cortam o NavMesh, e os zumbis
    /// encostados batem neles até quebrar (como na barricada); o Brutamonte derruba de uma vez;</item>
    /// <item>segurar o E desmonta o núcleo, o muro e o portão (o kit volta; os troféus gastos, não);</item>
    /// <item>montando uma peça da base, a área de cada núcleo aparece no chão (uma roda de luz).</item>
    /// </list>
    /// As malhas são feitas aqui (MalhaDeObra, as cores da paleta), até o Pedro trocar os modelos.
    /// </summary>
    public partial class ConstrucoesNoMundo
    {
        public const int NivelMaximoDoNucleo = 5;
        static readonly float[] RaiosDoNucleo = { 0f, 12f, 18f, 26f, 35f, 45f };
        // (com 2,5 de dano por zumbi encostado por segundo: 3 zumbis levam uns 65 s para derrubar o muro, uma horda de 10, uns 20 s)
        public const float VidaDoMuro = 500f, VidaDoPortao = 380f;
        /// <summary>O bit do <c>extra</c> do portão aberto.</summary>
        public const byte BitAberto = 1;

        public static int NivelDoNucleo(Construcao c) => Mathf.Clamp(Mathf.RoundToInt(c.valor), 1, NivelMaximoDoNucleo);
        public static float RaioDoNivel(int nivel) => RaiosDoNucleo[Mathf.Clamp(nivel, 1, NivelMaximoDoNucleo)];
        public static float RaioDoNucleo(Construcao c) => RaioDoNivel(NivelDoNucleo(c));

        /// <summary>O que sobe o núcleo do nível 'atual' para o seguinte (Nada no máximo).</summary>
        public static (Item item, int qtd) ParaSubir(int atual) => atual switch
        {
            1 => (Item.CabecaDoOco, 3),
            2 => (Item.GargantaDoLong, 2),
            3 => (Item.OlhoDoBrutamonte, 1),
            4 => (Item.CabecaDeChefe, 1),
            _ => (Item.Nada, 0),
        };

        /// <summary>O que só se monta dentro da área de um núcleo da base (o muro, o portão e a barricada).</summary>
        public static bool PrecisaDoNucleo(Item tipo) => tipo == Item.Barricada || tipo == Item.MuroDeToras || tipo == Item.PortaoDeToras;

        static float NoPlano(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;

        /// <summary>
        /// Por que não dá para montar o tipo aqui (null: dá): a peça da base fora da área de um núcleo, o núcleo dentro da área
        /// de outro. O mesmo no cliente (a sombra vermelha) e no anfitrião.
        /// </summary>
        public string PodeMontarAqui(Item tipo, Vector3 ponto)
        {
            if (tipo == Item.NucleoDaBase)
            {
                foreach (var c in _lista)
                    if (c.tipo == Item.NucleoDaBase && NoPlano(c.pos, ponto) < RaioDoNucleo(c) + RaioDoNivel(1))
                        return "Muito perto de outro núcleo da base";
                return null;
            }
            if (!PrecisaDoNucleo(tipo)) return null;
            foreach (var c in _lista)
                if (c.tipo == Item.NucleoDaBase && Mathf.Abs(c.pos.y - ponto.y) < 15f && NoPlano(c.pos, ponto) <= RaioDoNucleo(c)) return null;
            return "Fora da área de um núcleo da base (monte o núcleo primeiro)";
        }

        // ------------------------------------------------------------------ as malhas

        static Mesh _malhaDoNucleo, _malhaDoMuro, _malhaDoPortao, _batentesDoPortao, _folhaDoPortao;
        /// <summary>Onde fica a dobradiça da folha do portão (no espaço dele).</summary>
        static readonly Vector3 Dobradica = new Vector3(-1.42f, 0f, 0f);

        static Mesh MalhaDaBase(Item tipo) => tipo switch
        {
            Item.NucleoDaBase => MalhaDoNucleo(),
            Item.MuroDeToras => MalhaDoMuro(),
            Item.PortaoDeToras => MalhaDoPortao(),
            _ => null,
        };

        static Matrix4x4 Em(float x, float y, float z, float giro = 0f) => Matrix4x4.TRS(new Vector3(x, y, z), Quaternion.Euler(0f, giro, 0f), Vector3.one);

        // o braseiro: a roda de pedras, as três pernas, a bacia de sucata com a borda, a lenha e as brasas
        static Mesh MalhaDoNucleo()
        {
            if (_malhaDoNucleo != null) return _malhaDoNucleo;
            var o = new MalhaDeObra(4096);
            for (int i = 0; i < 12; i++)
            {
                float a = i * 30f;
                var p = Quaternion.Euler(0f, a, 0f) * Vector3.forward * 1.15f;
                o.Caixa(Matrix4x4.TRS(new Vector3(p.x, 0.12f, p.z), Quaternion.Euler(0f, a + 10f * (i % 3), 0f), Vector3.one), new Vector3(0.42f, 0.24f + 0.05f * (i % 2), 0.3f), i % 3 == 0 ? "PedraEscura" : "Pedra");
            }
            for (int i = 0; i < 3; i++)
            {
                var embaixo = Quaternion.Euler(0f, i * 120f, 0f) * Vector3.forward * 0.55f;
                var emCima = Quaternion.Euler(0f, i * 120f, 0f) * Vector3.forward * 0.4f;
                o.Tubo(embaixo, emCima + Vector3.up * 0.7f, 0.05f, 6, "FerrugemEscura");
            }
            o.Cilindro(Em(0f, 0.62f, 0f), 0.42f, 0.78f, 0.38f, 14, "Ferrugem");
            o.Cilindro(Em(0f, 1f, 0f), 0.8f, 0.8f, 0.07f, 14, "FerrugemEscura", true);
            o.Tubo(new Vector3(-0.55f, 1.05f, -0.1f), new Vector3(0.55f, 1.1f, 0.15f), 0.09f, 6, "TroncoEscuro");
            o.Tubo(new Vector3(-0.2f, 1.06f, 0.55f), new Vector3(0.15f, 1.12f, -0.55f), 0.09f, 6, "Tronco");
            o.Tubo(new Vector3(0.45f, 1.05f, 0.35f), new Vector3(-0.4f, 1.1f, -0.35f), 0.08f, 6, "TroncoEscuro");
            o.Caixa(Em(0f, 1.04f, 0f), new Vector3(0.9f, 0.05f, 0.9f), "Laranja");
            _malhaDoNucleo = o.ParaMesh("Núcleo da base");
            return _malhaDoNucleo;
        }

        // o muro: as duas estacas, seis toras deitadas, uma sim e uma não mais clara, e a escora atrás
        static Mesh MalhaDoMuro()
        {
            if (_malhaDoMuro != null) return _malhaDoMuro;
            var o = new MalhaDeObra(4096);
            // (as estacas entram meio metro no chão: na ladeira, a ponta de baixo não fica no ar)
            o.Tubo(new Vector3(-1.45f, -0.5f, 0f), new Vector3(-1.45f, 2.4f, 0f), 0.15f, 8, "TroncoEscuro");
            o.Tubo(new Vector3(1.45f, -0.5f, 0f), new Vector3(1.45f, 2.4f, 0f), 0.15f, 8, "TroncoEscuro");
            for (int i = 0; i < 6; i++)
            {
                float y = 0.18f + i * 0.35f, z = (i % 2 == 0 ? 0.015f : -0.015f);
                o.Tubo(new Vector3(-1.55f, y, z), new Vector3(1.55f, y + (i % 2 == 0 ? 0.02f : -0.02f), z), 0.18f, 8, i % 2 == 0 ? "Tronco" : "TroncoClaro");
            }
            o.Tubo(new Vector3(-1.25f, 0.3f, 0.24f), new Vector3(1.25f, 1.9f, 0.24f), 0.07f, 6, "TroncoEscuro");
            _malhaDoMuro = o.ParaMesh("Muro de toras");
            return _malhaDoMuro;
        }

        // o portão: os batentes (as duas estacas e a viga de cima) e a folha de toras em pé, presa na dobradiça da esquerda
        static Mesh BatentesDoPortao()
        {
            if (_batentesDoPortao != null) return _batentesDoPortao;
            var o = new MalhaDeObra(2048);
            Batentes(o);
            _batentesDoPortao = o.ParaMesh("Portão de toras: os batentes");
            return _batentesDoPortao;
        }

        static void Batentes(MalhaDeObra o)
        {
            o.Tubo(new Vector3(-1.58f, -0.5f, 0f), new Vector3(-1.58f, 2.55f, 0f), 0.16f, 8, "TroncoEscuro");
            o.Tubo(new Vector3(1.58f, -0.5f, 0f), new Vector3(1.58f, 2.55f, 0f), 0.16f, 8, "TroncoEscuro");
            o.Tubo(new Vector3(-1.75f, 2.45f, 0f), new Vector3(1.75f, 2.45f, 0f), 0.12f, 8, "Tronco");
        }

        static Mesh FolhaDoPortao()
        {
            if (_folhaDoPortao != null) return _folhaDoPortao;
            var o = new MalhaDeObra(4096);
            Folha(o, Vector3.zero);
            _folhaDoPortao = o.ParaMesh("Portão de toras: a folha");
            return _folhaDoPortao;
        }

        // a folha (a origem na dobradiça; ela vai para +x)
        static void Folha(MalhaDeObra o, Vector3 em)
        {
            for (int i = 0; i < 9; i++)
            {
                float x = 0.16f + i * 0.32f;
                o.Tubo(em + new Vector3(x, 0.06f, 0f), em + new Vector3(x, 2.15f + (i % 2) * 0.05f, 0f), 0.15f, 8, i % 2 == 0 ? "Tronco" : "TroncoClaro");
            }
            o.Tubo(em + new Vector3(0.05f, 0.5f, 0.17f), em + new Vector3(2.85f, 0.5f, 0.17f), 0.07f, 6, "TroncoEscuro");
            o.Tubo(em + new Vector3(0.05f, 1.75f, 0.17f), em + new Vector3(2.85f, 1.75f, 0.17f), 0.07f, 6, "TroncoEscuro");
            o.Tubo(em + new Vector3(0.15f, 0.55f, 0.2f), em + new Vector3(2.75f, 1.7f, 0.2f), 0.06f, 6, "TroncoEscuro");
            o.Caixa(Matrix4x4.Translate(em + new Vector3(0.1f, 0.5f, 0f)), new Vector3(0.28f, 0.08f, 0.34f), "Metal");
            o.Caixa(Matrix4x4.Translate(em + new Vector3(0.1f, 1.75f, 0f)), new Vector3(0.28f, 0.08f, 0.34f), "Metal");
        }

        // o portão inteiro, fechado (a sombra de quem monta e a caixa dele)
        static Mesh MalhaDoPortao()
        {
            if (_malhaDoPortao != null) return _malhaDoPortao;
            var o = new MalhaDeObra(4096);
            Batentes(o);
            Folha(o, Dobradica);
            _malhaDoPortao = o.ParaMesh("Portão de toras");
            return _malhaDoPortao;
        }

        // ------------------------------------------------------------------ o objeto de cada uma (em cada máquina)

        void MontarDaBase(GameObject go, Construcao c)
        {
            var col = go.GetComponent<BoxCollider>();
            switch (c.tipo)
            {
                case Item.NucleoDaBase:
                {
                    // o fogo grande do braseiro (o tamanho sobe com o nível: BaseAgora)
                    Fogo(go.transform, new Vector3(0f, 1.08f, 0f), materialDoFogo, 1.8f, 18f, true, go.layer);
                    var obstaculo = go.AddComponent<NavMeshObstacle>();
                    obstaculo.shape = NavMeshObstacleShape.Capsule;
                    obstaculo.center = new Vector3(0f, 0.6f, 0f);
                    obstaculo.radius = 0.9f;
                    obstaculo.height = 1.2f;
                    obstaculo.carving = true;
                    break;
                }
                case Item.MuroDeToras:
                {
                    var obstaculo = go.AddComponent<NavMeshObstacle>();
                    obstaculo.shape = NavMeshObstacleShape.Box;
                    obstaculo.center = col.center;
                    obstaculo.size = col.size + new Vector3(0.1f, 0f, 0.2f);
                    obstaculo.carving = true;
                    break;
                }
                case Item.PortaoDeToras:
                {
                    // a raiz fica só com os batentes; a caixa dela vira o gatilho da mira (o E abre e fecha); a folha é sólida e
                    // gira na dobradiça, com o pedaço do NavMesh junto
                    go.GetComponent<MeshFilter>().sharedMesh = BatentesDoPortao();
                    col.isTrigger = true;
                    foreach (float x in new[] { -1.58f, 1.58f })
                    {
                        var estaca = go.AddComponent<BoxCollider>();
                        estaca.center = new Vector3(x, 1.02f, 0f);
                        estaca.size = new Vector3(0.34f, 3.05f, 0.34f);
                    }
                    var folha = new GameObject("Folha");
                    folha.layer = go.layer;
                    folha.transform.SetParent(go.transform, false);
                    folha.transform.localPosition = Dobradica;
                    folha.transform.localRotation = Quaternion.Euler(0f, AberturaDoPortao(c), 0f);
                    var malha = FolhaDoPortao();
                    folha.AddComponent<MeshFilter>().sharedMesh = malha;
                    var r = folha.AddComponent<MeshRenderer>();
                    r.sharedMaterial = material;
                    var caixa = folha.AddComponent<BoxCollider>();
                    caixa.center = malha.bounds.center;
                    caixa.size = malha.bounds.size;
                    // (a folha aberta também tem a mira: o E fecha)
                    var mira = folha.AddComponent<ConstrucaoNaMira>();
                    mira.mundo = this;
                    mira.id = c.id;
                    var obstaculo = folha.AddComponent<NavMeshObstacle>();
                    obstaculo.shape = NavMeshObstacleShape.Box;
                    obstaculo.center = malha.bounds.center;
                    obstaculo.size = malha.bounds.size + new Vector3(0f, 0f, 0.2f);
                    obstaculo.carving = true;
                    obstaculo.carveOnlyStationary = false;
                    break;
                }
            }
        }

        static float AberturaDoPortao(Construcao c) => (c.extra & BitAberto) != 0 ? -100f : 0f;

        // a cada quadro, em cada máquina: o fogo do núcleo pelo nível, a folha do portão girando e a área dos núcleos no chão
        void BaseAgora()
        {
            bool mostrarArea = MontadorDeConstrucao.MontandoABase;
            foreach (var c in _lista)
            {
                if (!Itens.EhDaBase(c.tipo) || !_objetos.TryGetValue(c.id, out var o) || o == null) continue;
                if (c.tipo == Item.NucleoDaBase)
                {
                    var fogo = o.transform.Find("Fogo");
                    if (fogo != null)
                    {
                        int nivel = NivelDoNucleo(c);
                        fogo.localScale = Vector3.one * (1f + 0.18f * (nivel - 1));
                        if (fogo.TryGetComponent<Light>(out var luz))
                        {
                            luz.range = 18f + 3f * (nivel - 1);
                            luz.intensity = 3.8f + 1f * Mathf.PerlinNoise(Time.time * 2.7f, c.id) + 0.4f * Mathf.Sin(Time.time * 15f + c.id);
                        }
                    }
                    MostrarArea(o, c, mostrarArea);
                }
                else if (c.tipo == Item.PortaoDeToras)
                {
                    var folha = o.transform.Find("Folha");
                    if (folha == null) continue;
                    var alvo = Quaternion.Euler(0f, AberturaDoPortao(c), 0f);
                    if (Quaternion.Angle(folha.localRotation, alvo) > 0.1f)
                        folha.localRotation = Quaternion.RotateTowards(folha.localRotation, alvo, 140f * Time.deltaTime);
                }
            }
        }

        static Material _materialDaArea;

        // a roda de luz no chão, no raio da área do núcleo (só montando uma peça da base); refeita quando o nível muda
        void MostrarArea(GameObject o, Construcao c, bool mostrar)
        {
            var area = o.transform.Find("Área");
            if (!mostrar)
            {
                if (area != null && area.gameObject.activeSelf) area.gameObject.SetActive(false);
                return;
            }
            float raio = RaioDoNucleo(c);
            LineRenderer linha;
            if (area == null)
            {
                var go = new GameObject("Área");
                go.transform.SetParent(o.transform, false);
                linha = go.AddComponent<LineRenderer>();
                if (_materialDaArea == null) _materialDaArea = new Material(Shader.Find("Sprites/Default")) { name = "A área do núcleo" };
                linha.sharedMaterial = _materialDaArea;
                linha.useWorldSpace = true;
                linha.loop = true;
                linha.widthMultiplier = 0.12f;
                linha.startColor = linha.endColor = new Color(1f, 0.62f, 0.2f, 0.75f);
                linha.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                linha.receiveShadows = false;
                area = go.transform;
            }
            else linha = area.GetComponent<LineRenderer>();
            if (!area.gameObject.activeSelf) area.gameObject.SetActive(true);
            if (linha.positionCount == 72 && Mathf.Abs(area.localScale.x - raio) < 0.01f) return;
            // (o raio guardado na escala, que não mexe nos pontos do mundo: assim só refaz quando o nível muda)
            area.localScale = Vector3.one * raio;
            linha.positionCount = 72;
            for (int i = 0; i < 72; i++)
            {
                var p = c.pos + Quaternion.Euler(0f, i * 5f, 0f) * Vector3.forward * raio;
                // no chão de cada ponto (o terreno sobe e desce)
                // (de 4 m acima do núcleo: as copas e os telhados mais altos não contam)
                p.y = Physics.Raycast(p + Vector3.up * 4f, Vector3.down, out var chao, 12f, MascaraDeMontar, QueryTriggerInteraction.Ignore) ? chao.point.y + 0.12f : c.pos.y + 0.12f;
                linha.SetPosition(i, p);
            }
        }

        // ------------------------------------------------------------------ no anfitrião

        /// <summary>O E no núcleo (o anfitrião): os troféus do nível seguinte sobem o nível dele.</summary>
        void SubirONucleo(int k, Construcao c, InventarioJogador inventario)
        {
            int nivel = NivelDoNucleo(c);
            if (nivel >= NivelMaximoDoNucleo)
            {
                inventario.AvisarDono("O núcleo da base já está no nível máximo");
                return;
            }
            var (item, qtd) = ParaSubir(nivel);
            if (inventario.Quantos(item) < qtd)
            {
                inventario.AvisarDono($"Para o nível {nivel + 1}: {Itens.ComNumero(item, qtd)} (você tem {inventario.Quantos(item)})");
                return;
            }
            if (!inventario.GastarItem(item, qtd)) return;
            c.valor = nivel + 1;
            _lista[k] = c;
            inventario.AvisarDono($"O núcleo da base subiu para o nível {nivel + 1}: a área agora tem {RaioDoNivel(nivel + 1):0} m");
        }

        /// <summary>O jogador local segurou o E no núcleo, no muro ou no portão: desmonta (o kit volta).</summary>
        public void PedirDesmontarDaBase(ushort id) => DesmontarDaBaseRpc(id);

        [Rpc(SendTo.Server)]
        void DesmontarDaBaseRpc(ushort id, RpcParams parametros = default)
        {
            var inventario = InventarioDe(NetworkManager, parametros.Receive.SenderClientId);
            int k = Indice(id);
            if (inventario == null || k < 0) return;
            var c = _lista[k];
            if (!Itens.EhDaBase(c.tipo) || (inventario.transform.position - c.pos).sqrMagnitude > 4.5f * 4.5f) return;
            _lista.RemoveAt(k);
            // muito quebrado (abaixo da metade), o muro e o portão viram uma tora
            bool inteiro = c.tipo == Item.NucleoDaBase || c.valor >= MaximoDe(c) * 0.5f;
            if (inteiro) inventario.EntregarItem(c.tipo, 1);
            else inventario.EntregarItem(Item.Toras, 1);
            if (c.tipo == Item.NucleoDaBase && NivelDoNucleo(c) > 1) inventario.AvisarDono("O núcleo voltou para a mochila (o nível e os troféus gastos ficaram)");
        }

        /// <summary>O muro e o portão fechado: os zumbis encostados batem neles (como na barricada).</summary>
        static bool ZumbisBatem(Construcao c) => c.tipo == Item.MuroDeToras || (c.tipo == Item.PortaoDeToras && (c.extra & BitAberto) == 0);

        // ------------------------------------------------------------------ a mira (ConstrucaoNaMira)

        public const float SegundosDeDesmontarABase = 2.5f;

        static int Porcento(Construcao c) => Mathf.CeilToInt(c.valor / MaximoDe(c) * 100f);

        /// <summary>A dica do núcleo, do muro e do portão para o jogador local.</summary>
        public static string DicaDaBase(Construcao c, ArmaJogador quem)
        {
            switch (c.tipo)
            {
                case Item.NucleoDaBase:
                {
                    int nivel = NivelDoNucleo(c);
                    string s = $"Núcleo da base, nível {nivel} (área de {RaioDoNivel(nivel):0} m)";
                    if (nivel >= NivelMaximoDoNucleo) s += "   ·   nível máximo";
                    else
                    {
                        var (item, qtd) = ParaSubir(nivel);
                        int tem = quem != null && quem.Inventario != null ? quem.Inventario.Quantos(item) : 0;
                        s += tem >= qtd ? $"   ·   [E] Subir para o nível {nivel + 1} ({Itens.ComNumero(item, qtd)})"
                                        : $"   ·   nível {nivel + 1}: {Itens.ComNumero(item, qtd)} (você tem {tem})";
                    }
                    return s + "   ·   [segure E] Desmontar";
                }
                case Item.MuroDeToras:
                    return $"Muro de toras ({Porcento(c)}%)   ·   [segure E] Desmontar";
                case Item.PortaoDeToras:
                    return $"Portão de toras ({Porcento(c)}%)   ·   [E] {((c.extra & BitAberto) != 0 ? "Fechar" : "Abrir")}   ·   [segure E] Desmontar";
            }
            return null;
        }

        public static string DesmontandoDaBase(Construcao? c) => c == null ? "Desmontando"
            : c.Value.tipo == Item.NucleoDaBase ? "Apagando o núcleo da base" : c.Value.tipo == Item.PortaoDeToras ? "Desmontando o portão" : "Desmontando o muro";

        // ------------------------------------------------------------------ encaixar (quem monta)

        /// <summary>
        /// O muro e o portão encaixam na ponta de outro muro ou portão perto da mira (até 1,4 m do encaixe): em linha ou
        /// virado 90° (a quina). Muda o ponto (só no plano: quem chama acha o chão de novo) e o giro; devolve se encaixou.
        /// </summary>
        public bool Encaixar(Item tipo, ref Vector3 ponto, ref float giro)
        {
            if (tipo != Item.MuroDeToras && tipo != Item.PortaoDeToras) return false;
            float meu = MeiaLargura(tipo);
            float melhor = 1.4f * 1.4f;
            bool achou = false;
            Vector3 mira = ponto, alvo = ponto;
            float giroDoAlvo = giro;
            foreach (var c in _lista)
            {
                if (c.tipo != Item.MuroDeToras && c.tipo != Item.PortaoDeToras) continue;
                if (Mathf.Abs(c.pos.y - mira.y) > 2f || NoPlano(c.pos, mira) > 8f) continue;
                var rot = Quaternion.Euler(0f, c.giro, 0f);
                Vector3 direita = rot * Vector3.right, frente = rot * Vector3.forward;
                float dele = MeiaLargura(c.tipo);
                for (int lado = -1; lado <= 1; lado += 2)
                {
                    // em linha: encostado na ponta
                    Tentar(c.pos + direita * lado * (dele + meu), c.giro);
                    // a quina: virado 90°, encostado na ponta deste (a grossura dele cobre a quina), para a frente e para trás
                    Vector3 ponta = c.pos + direita * lado * (dele + Grossura);
                    Tentar(ponta + frente * (meu - Grossura), c.giro + 90f);
                    Tentar(ponta - frente * (meu - Grossura), c.giro + 90f);
                }
            }
            if (!achou) return false;
            ponto = new Vector3(alvo.x, ponto.y, alvo.z);
            // (o giro mais perto do que o jogador escolheu, de 180 em 180: o portão abre para o lado que ele quer)
            float delta = Mathf.DeltaAngle(giroDoAlvo, giro);
            giro = giroDoAlvo + (Mathf.Abs(delta) > 90f ? 180f : 0f);
            return true;

            void Tentar(Vector3 onde, float g)
            {
                float d = new Vector2(onde.x - mira.x, onde.z - mira.z).sqrMagnitude;
                if (d >= melhor) return;
                melhor = d;
                alvo = onde;
                giroDoAlvo = g;
                achou = true;
            }
        }

        // a metade da grossura do muro e do portão (com a escora de trás), para a quina
        const float Grossura = 0.33f;

        // a metade da largura da peça (a caixa dela, no comprido)
        static float MeiaLargura(Item tipo)
        {
            var m = MalhaDaBase(tipo);
            return m != null ? m.bounds.extents.x : 1.5f;
        }
    }
}
