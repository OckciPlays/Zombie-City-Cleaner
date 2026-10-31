using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using ZombieCityCleaner.Loot;
using ZombieCityCleaner.Player;
using ZombieCityCleaner.Video;
using ZombieCityCleaner.World;
using ZombieCityCleaner.Zombies;

namespace ZombieCityCleaner.UI
{
    /// <summary>
    /// O menu do desenvolvedor (F1; no Editor, nas builds de desenvolvimento e no jogo compilado com a opção ligada,
    /// <see cref="Opcoes.LiberarF1"/>): sempre de dia ou sempre de noite (o meio-dia ou a meia-noite com o relógio parado,
    /// para todos), criar itens (as armas, a lata, a
    /// garrafa, as mochilas, as caixas de munição cheias e as peças do motor a 100%) na mochila ou no chão à
    /// frente, ver quantos zumbis há, matar todos e criar mais em volta do jogador. Fica
    /// no jogador (só o dono abre); os pedidos vão para o anfitrião, que faz. Na cena Mundo, também o painel do mundo
    /// (MenuDoDesenvolvedor.Mundo.cs: a semente, voar com F2 e ir para um ponto).
    /// </summary>
    public partial class MenuDoDesenvolvedor : NetworkBehaviour
    {
        /// <summary>
        /// No Editor, nas builds de desenvolvimento e no jogo compilado com a opção ligada (ESC > Configurações). Os pedidos
        /// valem pela opção do anfitrião: com ela desligada lá, ele ignora os do F1 dos outros.
        /// </summary>
        public static bool Disponivel => Application.isEditor || Debug.isDebugBuild || Opcoes.LiberarF1;

        /// <summary>
        /// Se o F1 está liberado no anfitrião (escrito por ele, em todos os jogadores): quem entra numa partida com o F1
        /// desligado lá vê o aviso no painel (28/10/2026: o painel abria, e os pedidos sumiam sem explicação até o anfitrião
        /// ligar o dele).
        /// </summary>
        public readonly NetworkVariable<bool> LiberadoNoAnfitriao = new(false);
        /// <summary>A janela na tela (vazia, fechada): a tela do inventário não pega cliques nela.</summary>
        public static Rect Area { get; private set; }

        // os itens que o menu cria, por espécie (31/10/2026: com o saque, eram itens demais numa lista só)
        static readonly (string nome, Item[] itens)[] ItensDoMenu =
        {
            ("Armas", new[]
            {
                Item.Pistola, Item.Rifle, Item.Submetralhadora, Item.Espingarda, Item.Sniper, Item.Faca, Item.Machado,
                Item.Bastao, Item.Lanca, Item.Tocha, Item.Municao9mm, Item.Municao556, Item.Municao12, Item.Municao762,
            }),
            ("Comida", new[]
            {
                Item.Comida, Item.Feijao, Item.Carne, Item.Milho, Item.Sopa, Item.Salgadinho, Item.Chocolate, Item.BarraDeEnergia,
                Item.Agua, Item.CopoDeAgua, Item.RefrigeranteGarrafa, Item.RefrigeranteLata, Item.Energetico, Item.Leite, Item.Suco, Item.GarrafaTermica,
            }),
            ("Remédios", new[]
            {
                Item.Remedio, Item.Comprimidos, Item.KitMedico, Item.Atadura, Item.Gaze, Item.Desinfetante, Item.SprayAntisseptico, Item.Antibiotico, Item.Bandagem,
            }),
            ("Materiais", new[]
            {
                Item.Madeira, Item.Toras, Item.Pano, Item.Corda, Item.FitaAdesiva, Item.Ferramenta, Item.PecasDeMetal, Item.Fios, Item.Pregos,
                Item.Isca, Item.VaraDePesca, Item.KitDeReparo, Item.Roupa, Item.Fogueira, Item.Cama, Item.Barricada,
            }),
            ("Outros", new[]
            {
                Item.MochilaPequena, Item.MochilaMedia, Item.MochilaGrande,
                Item.Vela, Item.Bateria, Item.Radiador, Item.Pneu, Item.Macaco, Item.ChaveDaPerua,
                Item.CaixaDeBolas, Item.TacoMadeira, Item.TacoEscuro, Item.TacoPreto, Item.TacoVermelho,
            }),
        };
        static readonly string[] NomesDasEspecies = System.Array.ConvertAll(ItensDoMenu, e => e.nome);
        int _especie;
        static readonly string[] Destinos = { "Na mochila", "No chão" };

        // o boneco de teste: uma cópia do corpo com a mochila e as armas dos 3 atalhos penduradas (só visual, só aqui)
        static readonly (string nome, Item mochila, Item[] armas)[] Bonecos =
        {
            ("sem mochila: pistola no cós", Item.Nada, new[] { Item.Pistola, Item.Nada, Item.Nada }),
            ("sem mochila: espingarda, rifle e faca", Item.Nada, new[] { Item.Espingarda, Item.Rifle, Item.Faca }),
            ("sem mochila: sniper, rifle e machado", Item.Nada, new[] { Item.Sniper, Item.Rifle, Item.Machado }),
            ("mochila pequena: pistola, submetralhadora", Item.MochilaPequena, new[] { Item.Pistola, Item.Submetralhadora, Item.Nada }),
            ("mochila média: rifle, espingarda e pistola", Item.MochilaMedia, new[] { Item.Rifle, Item.Espingarda, Item.Pistola }),
            ("mochila grande: sniper, rifle e machado", Item.MochilaGrande, new[] { Item.Sniper, Item.Rifle, Item.Machado }),
        };
        GameObject _boneco;
        int _qualBoneco = -1;

        bool _aberto;
        int _destino;
        string _quantos = "10", _aviso;
        float _avisoAte;
        GUIStyle _titulo, _texto, _botao, _campo;
        float _uDosEstilos;

        public override void OnNetworkSpawn()
        {
            if (IsOwner) MenuOnline.FecharTelas += Fechar;
        }

        public override void OnNetworkDespawn()
        {
            MenuOnline.FecharTelas -= Fechar;
            if (_boneco != null) Destroy(_boneco);
            if (_aberto) Abrir(false);
        }

        void Fechar() => Abrir(false);

        void Abrir(bool abrir)
        {
            _aberto = abrir;
            MenuOnline.Tela(this, abrir);
            if (!abrir) Area = default;
        }

        void Update()
        {
            if (IsSpawned && IsServer && LiberadoNoAnfitriao.Value != Disponivel) LiberadoNoAnfitriao.Value = Disponivel;
            if (!IsSpawned || !IsOwner || !Disponivel || MenuOnline.Bot) return;
            var teclado = Keyboard.current;
            if (teclado != null && teclado.f1Key.wasPressedThisFrame && !MenuOnline.Aberto) Abrir(!_aberto);
            AtalhosDoMundo(teclado);
            AtalhosDoMapaFixo(teclado);
        }

        void Avisar(string texto)
        {
            _aviso = texto;
            _avisoAte = Time.time + 3f;
        }

        void Estilos(float u)
        {
            if (_titulo != null && Mathf.Approximately(_uDosEstilos, u)) return;
            _uDosEstilos = u;
            _titulo = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(15 * u), fontStyle = FontStyle.Bold };
            _titulo.normal.textColor = new Color(0.98f, 0.82f, 0.4f);
            _texto = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(13 * u), wordWrap = true };
            _texto.normal.textColor = new Color(0.88f, 0.86f, 0.8f);
            _botao = new GUIStyle(GUI.skin.button) { fontSize = Mathf.RoundToInt(13 * u) };
            _botao.onNormal.textColor = _botao.onHover.textColor = _botao.onActive.textColor = new Color(0.98f, 0.85f, 0.45f);
            _botao.onNormal.background = _botao.active.background;
            _campo = new GUIStyle(GUI.skin.textField) { fontSize = Mathf.RoundToInt(14 * u), alignment = TextAnchor.MiddleCenter };
        }

        void OnGUI()
        {
            if (!_aberto || !IsSpawned || !IsOwner || MenuOnline.Aberto || ModoVideo.Ligado) return;
            GUI.depth = -10;   // por cima da tela do inventário
            float u = Mathf.Min(Screen.height / 720f, Screen.width / 1280f);
            Estilos(u);
            bool semAnfitriao = !IsServer && !LiberadoNoAnfitriao.Value;
            var area = new Rect(12 * u, 40 * u, 300 * u, (semAnfitriao ? 712 : 658) * u);
            Area = area;
            var antes = GUI.color;
            GUI.color = new Color(0.08f, 0.08f, 0.075f, 0.92f);
            GUI.DrawTexture(area, Texture2D.whiteTexture);
            GUI.color = new Color(0.72f, 0.58f, 0.2f, 0.9f);
            GUI.DrawTexture(new Rect(area.x, area.y, area.width, 2 * u), Texture2D.whiteTexture);
            GUI.color = antes;

            float x = area.x + 12 * u, w = area.width - 24 * u, y = area.y + 8 * u;
            GUI.Label(new Rect(x, y, w, 22 * u), "DESENVOLVEDOR  (F1)", _titulo);
            y += 30 * u;
            if (semAnfitriao)
            {
                // os pedidos (itens, relógio, zumbis) valem pela opção do anfitrião
                var cor = _texto.normal.textColor;
                _texto.normal.textColor = new Color(1f, 0.45f, 0.35f);
                GUI.Label(new Rect(x, y, w, 50 * u), "Desligado no anfitrião: os itens, o relógio e os zumbis daqui não valem. Lá: ESC > Configurações > Menu do desenvolvedor (F1).", _texto);
                _texto.normal.textColor = cor;
                y += 54 * u;
            }

            // sempre de dia ou sempre de noite: meio-dia ou meia-noite com o relógio parado (o anfitrião manda, para todos)
            bool deDia = CicloDoDia.Parado && CicloDoDia.Noite < 0.5f, deNoite = CicloDoDia.Parado && !deDia;
            float meio = (w - 8 * u) / 2f;
            // (ligado, o botão fica apertado e com o texto amarelo: o estilo "on" do _botao)
            if (GUI.Toggle(new Rect(x, y, meio, 28 * u), deDia, "Sempre de dia", _botao) != deDia)
            {
                PararRelogioRpc(!deDia, false);
                Avisar(deDia ? "O relógio voltou a andar." : "Meio-dia, com o relógio parado.");
            }
            if (GUI.Toggle(new Rect(x + meio + 8 * u, y, meio, 28 * u), deNoite, "Sempre de noite", _botao) != deNoite)
            {
                PararRelogioRpc(!deNoite, true);
                Avisar(deNoite ? "O relógio voltou a andar." : "Meia-noite, com o relógio parado.");
            }
            y += 34 * u;

            // os zumbis
            var zm = ZombieManager.Instancia;
            string contagem = "Zumbis: —";
            if (zm != null && zm.IsSpawned)
            {
                zm.ContarNiveis(out int perto, out int medio, out int longe);
                contagem = zm.IsServer
                    ? $"Zumbis vivos: {zm.Vivos}   (mortos {zm.Mortos})\nperto {perto} · médio {medio} · longe {longe}"
                    : $"Zumbis vivos perto de você: {zm.Vivos}\nperto {perto} · médio {medio}";
            }
            GUI.Label(new Rect(x, y, w, 36 * u), contagem, _texto);
            y += 42 * u;
            if (GUI.Button(new Rect(x, y, w, 28 * u), "Matar todos os zumbis", _botao))
            {
                MatarZumbisRpc();
                Avisar("Todos os zumbis morreram.");
            }
            y += 36 * u;
            GUI.Label(new Rect(x, y + 4 * u, 46 * u, 22 * u), "Criar", _texto);
            _quantos = GUI.TextField(new Rect(x + 44 * u, y, 60 * u, 28 * u), _quantos, 4, _campo);
            if (GUI.Button(new Rect(x + 112 * u, y, w - 112 * u, 28 * u), "zumbis perto de mim", _botao))
            {
                if (int.TryParse(_quantos, out int n) && n > 0)
                {
                    n = Mathf.Min(n, 300);
                    CriarZumbisRpc((ushort)n);
                    Avisar($"Criando {n} zumbis de 6 a 14 m daqui.");
                }
                else Avisar("Escreva um número.");
            }
            y += 44 * u;

            // os itens
            GUI.Label(new Rect(x, y, w, 22 * u), "Itens", _titulo);
            y += 26 * u;
            _destino = GUI.SelectionGrid(new Rect(x, y, w, 26 * u), _destino, Destinos, 2, _botao);
            y += 34 * u;
            _especie = GUI.SelectionGrid(new Rect(x, y, w, 26 * u), _especie, NomesDasEspecies, NomesDasEspecies.Length, _botao);
            y += 34 * u;
            float bw = (w - 8 * u) / 2f;
            var inventario = GetComponent<InventarioJogador>();
            var lista = ItensDoMenu[_especie].itens;
            for (int i = 0; i < lista.Length; i++)
            {
                var item = lista[i];
                var r = new Rect(x + (i % 2) * (bw + 8 * u), y + (i / 2) * 32 * u, bw, 26 * u);
                string nome = Itens.Nome(item);
                if (!GUI.Button(r, char.ToUpper(nome[0]) + nome.Substring(1), _botao)) continue;
                bool noChao = _destino == 1;
                if (!noChao && inventario != null && !inventario.PodeReceber(item, out var motivo))
                {
                    noChao = true;
                    Avisar($"{motivo}: {Itens.NomeComArtigo(item)} foi para o chão.");
                }
                CriarItemRpc(item, noChao);
            }
            // a altura da maior espécie (a janela não pula ao trocar)
            int maior = 0;
            foreach (var e in ItensDoMenu) maior = Mathf.Max(maior, e.itens.Length);
            y += (maior + 1) / 2 * 32 * u + 4 * u;

            // o boneco de teste
            GUI.Label(new Rect(x, y, w, 22 * u), "Boneco equipado (teste)", _titulo);
            y += 26 * u;
            if (GUI.Button(new Rect(x, y, bw, 26 * u), _boneco == null ? "Pôr o boneco" : "Próximo", _botao)) TrocarBoneco();
            if (GUI.Button(new Rect(x + bw + 8 * u, y, bw, 26 * u), "Tirar", _botao) && _boneco != null)
            {
                Destroy(_boneco);
                _boneco = null;
                _qualBoneco = -1;
            }
            y += 32 * u;
            if (_boneco != null) GUI.Label(new Rect(x, y, w, 22 * u), Bonecos[_qualBoneco].nome, _texto);
            y += 26 * u;
            if (_aviso != null && Time.time < _avisoAte) GUI.Label(new Rect(x, y, w, 36 * u), _aviso, _texto);

            // na cena Mundo, o painel do mundo ao lado (a tela do inventário não pega cliques nos dois)
            var mundo = PainelDoMundo(u, area);
            // ou, na cena do mapa fixo, o painel dele
            if (mundo.width <= 0f) mundo = PainelDoMapaFixo(u, area);
            if (mundo.width > 0f) Area = Rect.MinMaxRect(area.xMin, area.yMin, mundo.xMax, Mathf.Max(area.yMax, mundo.yMax));
            // o Oco de teste, ao lado do último painel
            var oco = PainelDoOco(u, mundo.width > 0f ? mundo : area);
            Area = Rect.MinMaxRect(Area.xMin, Area.yMin, oco.xMax, Mathf.Max(Area.yMax, oco.yMax));
            // os zumbis (teste: criar um na frente, a pedra do sol), ao lado
            var zumbis = PainelDosZumbis(u, oco);
            Area = Rect.MinMaxRect(Area.xMin, Area.yMin, zumbis.xMax, Mathf.Max(Area.yMax, zumbis.yMax));
            // a trilha sonora (teste), embaixo dos zumbis
            var trilha = PainelDaTrilha(u, zumbis);
            Area = Rect.MinMaxRect(Area.xMin, Area.yMin, Mathf.Max(Area.xMax, trilha.xMax), Mathf.Max(Area.yMax, trilha.yMax));
        }

        /// <summary>
        /// Põe (ou troca) o boneco 2 m à frente, de costas para o jogador, com a próxima combinação de equipamento.
        /// É uma cópia do "Corpo" do prefab do jogador (em repouso), com a animação de parado dos punhos.
        /// </summary>
        void TrocarBoneco()
        {
            var rede = GetComponent<JogadorRede>();
            var prefab = NetworkManager.Singleton != null ? NetworkManager.Singleton.NetworkConfig.PlayerPrefab : null;
            var referencia = prefab != null && rede != null ? prefab.transform.Find(rede.animadorCorpo.name) : null;   // o corpo do personagem dele
            if (rede == null || referencia == null) return;
            _qualBoneco = (_qualBoneco + 1) % Bonecos.Length;
            if (_boneco == null)
            {
                Vector3 frente = transform.forward;
                frente.y = 0f;
                frente.Normalize();
                Vector3 pe = transform.position + frente * 2f;
                if (Physics.Raycast(pe + Vector3.up * 1.5f, Vector3.down, out var chao, 4f, ~((1 << 2) | (1 << JogadorRede.CamadaJogador)), QueryTriggerInteraction.Ignore))
                    pe = chao.point;
                _boneco = new GameObject("Boneco equipado (teste)");
                _boneco.transform.SetPositionAndRotation(pe, Quaternion.LookRotation(frente));
                var corpo = Instantiate(referencia.gameObject, _boneco.transform, false);
                corpo.name = "Corpo";
                // vestido como o jogador (as mesmas peças e cores)
                var vestimenta = corpo.GetComponent<ZombieCityCleaner.Roupas.Vestimenta>();
                if (vestimenta != null && rede.Vestimenta != null) vestimenta.VestirComo(rede.Vestimenta);
                corpo.transform.localPosition = Vector3.zero;
                corpo.transform.localRotation = Quaternion.identity;
                foreach (var t in corpo.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 0;
                foreach (var a in rede.naMaoCorpo)
                {
                    var igual = corpo.transform.GetComponentsInChildren<Transform>(true);
                    foreach (var t in igual) if (t.name == a.name) t.gameObject.SetActive(false);
                }
                foreach (var r in corpo.GetComponentsInChildren<Renderer>(true))
                {
                    r.enabled = true;
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                    if (r is SkinnedMeshRenderer pele) pele.updateWhenOffscreen = true;
                }
                var animador = corpo.GetComponent<Animator>();
                if (animador != null)
                {
                    if (rede.controladorDasMaos != null) animador.runtimeAnimatorController = rede.controladorDasMaos;
                    animador.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                }
                corpo.AddComponent<EquipamentoNoCorpo>();
            }
            var b = Bonecos[_qualBoneco];
            _boneco.GetComponentInChildren<EquipamentoNoCorpo>().Vestir(referencia, b.mochila, b.armas);
        }

        // ------------------------------------------------------------------ no anfitrião

        bool Aceita(RpcParams parametros) => Disponivel && parametros.Receive.SenderClientId == OwnerClientId;

        [Rpc(SendTo.Server)]
        void CriarItemRpc(Item item, bool noChao, RpcParams parametros = default)
        {
            if (!Aceita(parametros) || item == Item.Nada) return;
            var inventario = GetComponent<InventarioJogador>();
            // o saque vem numa pilha cheia (os materiais da fabricação, de uma vez)
            int qtd = Itens.EmPilha(item) && !Itens.EhMunicao(item) ? Itens.Pilha(item) : -1;
            if (!noChao && inventario != null && inventario.Receber(item, qtd)) return;
            if (ItensNoMundo.Instancia == null) return;
            // no chão, 1,2 m à frente (onde um raio de cima bater)
            Vector3 frente = transform.position + transform.forward * 1.2f;
            var ponto = Physics.Raycast(frente + Vector3.up * 1.5f, Vector3.down, out var chao, 4f, ~((1 << 2) | (1 << JogadorRede.CamadaJogador)), QueryTriggerInteraction.Ignore)
                ? chao.point : transform.position;
            ItensNoMundo.Instancia.Criar(item, ponto, transform.eulerAngles.y + 90f, qtd);
        }

        [Rpc(SendTo.Server)]
        void PararRelogioRpc(bool ligado, bool deNoite, RpcParams parametros = default)
        {
            if (!Aceita(parametros) || CicloDoDia.Instancia == null) return;
            if (deNoite) CicloDoDia.Instancia.SempreDeNoite(ligado);
            else CicloDoDia.Instancia.SempreDeDia(ligado);
        }

        [Rpc(SendTo.Server)]
        void MatarZumbisRpc(RpcParams parametros = default)
        {
            if (Aceita(parametros)) ZombieManager.Instancia?.MatarTodos();
        }

        [Rpc(SendTo.Server)]
        void CriarZumbisRpc(ushort quantos, RpcParams parametros = default)
        {
            if (Aceita(parametros)) ZombieManager.Instancia?.CriarPerto(transform.position, Mathf.Min((int)quantos, 300));
        }
    }
}
