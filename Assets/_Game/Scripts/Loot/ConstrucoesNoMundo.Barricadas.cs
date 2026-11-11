using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using ZombieCityCleaner.Combat;
using ZombieCityCleaner.Player;
using ZombieCityCleaner.Zombies;
using A = ZombieCityCleaner.Player.ArmadilhasDoJogador;

namespace ZombieCityCleaner.Loot
{
    /// <summary>
    /// As barricadas (11/11/2026, a etapa 5b da árvore nova: a Barricada de tábuas, t2, e a Fortaleza, t7; os números no
    /// Player/ArmadilhasDoJogador.cs). A de tábuas é uma construção como as outras (o tipo <see cref="Item.TabuasBarricada"/>,
    /// na mesma lista da rede), pregada numa porta ou numa janela das casas: a <see cref="Construcao.vao"/> guarda a chave
    /// dela (a que o <c>World.PortasDoMundo</c> usa), e a pose fica na face da parede do lado de quem pregou (o +Z da
    /// construção aponta para ele). O anfitrião decide tudo (pregar, a vida, quebrar, desmontar, consertar); cada cópia vê:
    ///  - pregar (o <c>PortasDoMundo.PregarRpc</c>, que acha o vão): gasta as tábuas, fecha a porta ou a janela e faz o
    ///    barulho do martelo (os zumbis até 15 m vêm ver). Enquanto ela existe, a porta ou a janela não abre, dos dois lados;
    ///  - a vida: a porta 150, a janela 100 (o nível 2 da Barricada de tábuas +50%, o Armadilheiro +50% por nível e a
    ///    Fortaleza ×2, guardados na construção); os zumbis engajados encostados tiram como na barricada solta, o Brutamonte
    ///    a derruba, e o golpe de arma branca de um jogador (dos dois lados) tira metade do dano da arma. Quebrou: some,
    ///    sobram 2 madeiras, e a porta volta a funcionar (fechada). Abaixo de 50%, a malha danificada;
    ///  - do lado dela, segurar o E com a ferramenta desmonta (volta o item com 60% da vida ou mais; senão, 2 madeiras), e
    ///    no nível 2, segurar o clique com 1 madeira, 2 pregos e a ferramenta conserta 35% da máxima;
    ///  - a Fortaleza: as defesas do dono (as de tábuas, a solta e as estacas) se consertam sozinhas, 5% da máxima por hora
    ///    do jogo, com o dono a até 30 m e nenhum zumbi batendo nelas há 5 s.
    /// </summary>
    public partial class ConstrucoesNoMundo
    {
        /// <summary>Os bits do <see cref="Construcao.extra"/> (a etapa 5b).</summary>
        public const byte BitJanela = 1, BitReforcada = 2, BitFortaleza = 4;

        // as portas e as janelas barricadas (a chave do vão → a construção), em cada cópia (refeito no Atualizar)
        readonly Dictionary<ulong, ushort> _barricadasDosVaos = new Dictionary<ulong, ushort>();
        // a última vez que um zumbi bateu (o sossego antes do conserto da Fortaleza), no anfitrião
        readonly Dictionary<ushort, float> _batidaEm = new Dictionary<ushort, float>();
        float _proximoConserto, _ultimoConserto = -1f;

        public static bool Janela(Construcao c) => (c.extra & BitJanela) != 0;
        public static bool Fortaleza(Construcao c) => (c.extra & BitFortaleza) != 0;
        public static bool Reforcada(Construcao c) => (c.extra & BitReforcada) != 0;

        /// <summary>A barricada de tábuas pregada na porta ou na janela da chave (null: nenhuma).</summary>
        public Construcao? BarricadaDoVao(ulong chave)
        {
            if (chave == 0 || !_barricadasDosVaos.TryGetValue(chave, out var id)) return null;
            return Dados(id);
        }

        /// <summary>Quantas barricadas de tábuas há no mundo (o F1 e o teste).</summary>
        public int BarricadasDeTabuas => _barricadasDosVaos.Count;

        /// <summary>O ponto está do lado em que a barricada foi pregada (o +Z dela).</summary>
        public static bool DoLadoDela(Construcao c, Vector3 ponto) =>
            (Quaternion.Inverse(Quaternion.Euler(0f, c.giro, 0f)) * (ponto - c.pos)).z > 0f;

        // ------------------------------------------------------------------ a malha e a caixa

        // o vão das malhas (o Armadilhas_medidas.json: as casas x 1,15)
        const float LarguraDaPorta = 1.265f, AlturaDaPorta = 2.415f, LarguraDaJanela = 1.38f, AlturaDaJanela = 1.515f;

        Mesh MalhaDaBarricada(Construcao c) => ItensDasArmadilhas.Barricada(Janela(c), c.valor < MaximoDe(c) * A.Danificada);

        /// <summary>A escala da malha da barricada de tábuas para caber no vão (a largura e a altura; a grossura fica).</summary>
        public static Vector3 EscalaDoVao(Construcao c)
        {
            if (c.largura <= 0f || c.altura <= 0f) return Vector3.one;
            bool janela = Janela(c);
            float x = c.largura / (janela ? LarguraDaJanela : LarguraDaPorta), y = c.altura / (janela ? AlturaDaJanela : AlturaDaPorta);
            return new Vector3(Mathf.Clamp(x, 0.4f, 3f), Mathf.Clamp(y, 0.4f, 3f), 1f);
        }

        // a construção tem uma caixa (a malha montada)
        bool TemCaixa(Construcao c) => c.tipo == Item.TabuasBarricada ? ItensDasArmadilhas.Barricada(Janela(c), false) != null : MalhaDe(c.tipo) != null;

        /// <summary>A caixa da construção no espaço dela (sem o giro; a de tábuas, a inteira na escala do vão).</summary>
        Bounds CaixaDe(Construcao c)
        {
            if (c.tipo != Item.TabuasBarricada) return MalhaDe(c.tipo).bounds;
            var b = ItensDasArmadilhas.Barricada(Janela(c), false).bounds;
            var e = EscalaDoVao(c);
            return new Bounds(Vector3.Scale(b.center, e), Vector3.Scale(b.size, e));
        }

        // ------------------------------------------------------------------ pregar (no anfitrião)

        /// <summary>
        /// Só no anfitrião (o <c>PortasDoMundo.PregarRpc</c> já achou o vão e conferiu a distância): prega a barricada de tábuas
        /// de quem pediu (o nó, as tábuas da mochila) na pose do vão. <paramref name="deGraca"/>: o teste (sem as tábuas nem o
        /// nó). Devolve se pregou (a porta ou a janela fecha: quem chamou escreve).
        /// </summary>
        public bool PregarNoVao(InventarioJogador inventario, ulong cliente, ulong chave, bool janela, Vector3 pos, float giro, float largura, float altura, bool deGraca = false)
        {
            if (!IsServer || !IsSpawned || inventario == null || chave == 0 || BarricadaDoVao(chave).HasValue) return false;
            var j = inventario.GetComponent<JogadorRede>();
            int t2 = A.BarricadaDe(j);
            if (!deGraca && (t2 <= 0 || !inventario.GastarItem(Item.TabuasBarricada, 1))) return false;
            int nivel = A.NivelDe(j);
            bool fortaleza = A.FortalezaDe(j), reforcada = t2 >= 2;
            float vida = A.VidaDaBarricadaDeTabuas(janela, reforcada, nivel, fortaleza);
            byte extra = (byte)((janela ? BitJanela : 0) | (reforcada ? BitReforcada : 0) | (fortaleza ? BitFortaleza : 0));
            ushort id = _proximoId++;
            _lista.Add(new Construcao
            {
                id = id, tipo = Item.TabuasBarricada, pos = pos, giro = giro, valor = vida, dono = ChaveDe(inventario, cliente),
                nivel = (byte)nivel, vao = chave, largura = largura, altura = altura, extra = extra,
            });
            _barricadasDosVaos[chave] = id;
            Vector3 meio = MeioDaBarricada(pos, janela, altura);
            ZombieManager.Barulho(meio, j, A.BarulhoDoMartelo);
            BarricadaRpc(EfeitoPregou, meio);
            Pregadas++;
            return true;
        }

        static Vector3 MeioDaBarricada(Vector3 pos, bool janela, float altura) => pos + Vector3.up * Mathf.Max(0.5f, (janela ? altura : Mathf.Min(altura, 2.2f)) * 0.5f);

        /// <summary>Quantas barricadas de tábuas foram pregadas nesta partida (o teste).</summary>
        public int Pregadas { get; private set; }

        // ------------------------------------------------------------------ desmontar e consertar

        /// <summary>O jogador local pode desmontar a barricada de tábuas (do lado dela, com a ferramenta).</summary>
        public bool PodeDesmontar(Construcao c, ArmaJogador quem) =>
            c.tipo == Item.TabuasBarricada && quem != null && quem.Inventario != null && DoLadoDela(c, quem.transform.position)
            && quem.Inventario.Quantos(Item.Ferramenta) > 0;

        /// <summary>O jogador local pode consertar a barricada de tábuas (o nível 2, do lado dela, gasta, com os materiais e a ferramenta).</summary>
        public bool PodeConsertar(Construcao c, ArmaJogador quem)
        {
            if (c.tipo != Item.TabuasBarricada || quem == null || quem.Inventario == null || !DoLadoDela(c, quem.transform.position)) return false;
            var inv = quem.Inventario;
            return A.BarricadaDe(quem.GetComponent<JogadorRede>()) >= 2 && c.valor < MaximoDe(c) - 0.5f && inv.Quantos(Item.Ferramenta) > 0
                   && inv.Quantos(Item.Madeira) >= A.MadeiraDoConserto && inv.Quantos(Item.Pregos) >= A.PregosDoConserto;
        }

        public void PedirDesmontar(ushort id) => DesmontarRpc(id);
        public void PedirConsertar(ushort id) => ConsertarRpc(id);

        // o anfitrião confere quem pede: perto (4 m), do lado dela, com a ferramenta
        bool PodeMexer(ulong cliente, ushort id, out int k, out Construcao c, out InventarioJogador inventario)
        {
            inventario = InventarioDe(NetworkManager, cliente);
            k = Indice(id);
            c = default;
            if (inventario == null || k < 0) return false;
            c = _lista[k];
            if (c.tipo != Item.TabuasBarricada) return false;
            var pes = inventario.transform.position;
            if ((MeioDaBarricada(c.pos, Janela(c), c.altura) - (pes + Vector3.up)).sqrMagnitude > 4f * 4f) return false;
            return DoLadoDela(c, pes) && inventario.Quantos(Item.Ferramenta) > 0;
        }

        [Rpc(SendTo.Server)]
        void DesmontarRpc(ushort id, RpcParams parametros = default)
        {
            if (!PodeMexer(parametros.Receive.SenderClientId, id, out int k, out var c, out var inventario)) return;
            _lista.RemoveAt(k);
            float pct = c.valor / MaximoDe(c) * 100f;
            if (pct >= A.TabuasVoltamCom) inventario.EntregarItem(Item.TabuasBarricada, 1);
            else
            {
                inventario.EntregarItem(Item.Madeira, 2);
                inventario.AvisarDono("As tábuas estavam gastas: sobraram 2 madeiras");
            }
            BarricadaRpc(EfeitoArrancou, MeioDaBarricada(c.pos, Janela(c), c.altura));
        }

        [Rpc(SendTo.Server)]
        void ConsertarRpc(ushort id, RpcParams parametros = default)
        {
            if (!PodeMexer(parametros.Receive.SenderClientId, id, out int k, out var c, out var inventario)) return;
            var j = inventario.GetComponent<JogadorRede>();
            float max = MaximoDe(c);
            if (A.BarricadaDe(j) < 2 || c.valor >= max - 0.5f) return;
            if (inventario.Quantos(Item.Madeira) < A.MadeiraDoConserto || inventario.Quantos(Item.Pregos) < A.PregosDoConserto) return;
            inventario.GastarItem(Item.Madeira, A.MadeiraDoConserto);
            inventario.GastarItem(Item.Pregos, A.PregosDoConserto);
            c.valor = Mathf.Min(max, c.valor + max * A.Conserto);
            _lista[k] = c;
            Vector3 meio = MeioDaBarricada(c.pos, Janela(c), c.altura);
            ZombieManager.Barulho(meio, j, A.BarulhoDoMartelo);
            BarricadaRpc(EfeitoPregou, meio);
            Consertos++;
        }

        /// <summary>Quantos consertos com o clique nesta partida (o teste).</summary>
        public int Consertos { get; private set; }

        // ------------------------------------------------------------------ o golpe de arma branca

        /// <summary>
        /// O golpe de arma branca do jogador local acertou o colisor: se ele é uma barricada de tábuas (as tábuas, ou a porta
        /// ou a janela barricada, do outro lado), o anfitrião tira metade do dano da arma da vida dela.
        /// </summary>
        public void GolpeNaBarricada(Collider colisor, Vector3 ponto, float dano)
        {
            if (colisor == null || !IsSpawned) return;
            ushort id = 0;
            if (colisor.TryGetComponent<ConstrucaoNaMira>(out var mira) && mira.mundo == this) id = mira.id;
            else if (colisor.TryGetComponent<World.PortaDaCasa>(out var porta) && _barricadasDosVaos.TryGetValue(porta.chave, out var p)) id = p;
            else if (colisor.TryGetComponent<World.JanelaDaCasa>(out var janela) && _barricadasDosVaos.TryGetValue(janela.chave, out var q)) id = q;
            var d = id != 0 ? Dados(id) : null;
            if (d == null || d.Value.tipo != Item.TabuasBarricada) return;
            Efeitos.Som(SomDasArmadilhas.Madeira, ponto, 0.45f, true, 20f);
            GolpeNaBarricadaRpc(id, dano);
        }

        [Rpc(SendTo.Server)]
        void GolpeNaBarricadaRpc(ushort id, float dano, RpcParams parametros = default)
        {
            var inventario = InventarioDe(NetworkManager, parametros.Receive.SenderClientId);
            int k = Indice(id);
            if (inventario == null || k < 0) return;
            var c = _lista[k];
            if (c.tipo != Item.TabuasBarricada) return;
            // (a parede entre os dois: do outro lado, o jogador fica a uns 30 cm a mais)
            if ((MeioDaBarricada(c.pos, Janela(c), c.altura) - (inventario.transform.position + Vector3.up)).sqrMagnitude > 4.5f * 4.5f) return;
            c.valor -= Mathf.Clamp(dano, 0f, 300f) * A.GolpeNaBarricada;
            _batidaEm[c.id] = Time.time;
            GolpesNaBarricada++;
            if (c.valor <= 0f) BarricadaDeTabuasQuebrou(k, c);
            else _lista[k] = c;
        }

        /// <summary>Quantos golpes de arma branca pegaram numa barricada de tábuas (o teste).</summary>
        public int GolpesNaBarricada { get; private set; }

        // ------------------------------------------------------------------ o anfitrião: os zumbis, quebrar e a Fortaleza

        // a cada meio segundo (o tique das construções): os zumbis encostados gastam a solta e as de tábuas; a cada 2 s, o
        // conserto da Fortaleza
        void BarricadasNoServidor(float passo)
        {
            for (int k = _lista.Count - 1; k >= 0; k--)
            {
                var c = _lista[k];
                if (c.tipo != Item.Barricada && c.tipo != Item.TabuasBarricada) continue;
                int zumbis = ZumbisNa(c);
                if (zumbis == 0) continue;
                _batidaEm[c.id] = Time.time;
                c.valor -= DanoPorZumbi * zumbis * passo;
                if (c.valor > 0f)
                {
                    _lista[k] = c;
                    continue;
                }
                if (c.tipo == Item.TabuasBarricada)
                {
                    BarricadaDeTabuasQuebrou(k, c);
                    continue;
                }
                _lista.RemoveAt(k);
                // sobram umas tábuas no chão
                ItensNoMundo.Instancia?.Criar(Item.Madeira, c.pos, c.giro, 2);
            }
            if (Time.time >= _proximoConserto)
            {
                // (a cada 2 s, com o tempo que passou de verdade: o tique é de meio segundo, e os 2 s viravam até 2,5)
                float passou = _ultimoConserto < 0f ? 0f : Mathf.Min(Time.time - _ultimoConserto, 5f);
                _ultimoConserto = Time.time;
                _proximoConserto = Time.time + 2f;
                if (passou > 0f) ConsertoDaFortaleza(passou);
            }
        }

        /// <summary>Quanto a Fortaleza já consertou nesta partida (vida somada, o teste).</summary>
        public float ConsertadoPelaFortaleza { get; private set; }

        // a Fortaleza: as defesas do dono (as barricadas e as estacas) se consertam sozinhas, com ele perto e em sossego
        void ConsertoDaFortaleza(float passo)
        {
            float horas = World.CicloDoDia.HorasPorSegundo * passo;
            for (int k = _lista.Count - 1; k >= 0; k--)
            {
                var c = _lista[k];
                if (!Fortaleza(c) || (c.tipo != Item.Barricada && c.tipo != Item.TabuasBarricada && c.tipo != Item.Estacas)) continue;
                float max = MaximoDe(c);
                if (c.valor >= max) continue;
                if (_batidaEm.TryGetValue(c.id, out float quando) && Time.time - quando < A.SossegoDoConserto) continue;
                var dono = DonoNaPartida(c.dono);
                if (dono == null || (dono.transform.position - c.pos).sqrMagnitude > A.DonoPerto * A.DonoPerto) continue;
                float antes = c.valor;
                c.valor = Mathf.Min(max, c.valor + max * A.ConsertoPorHora * horas);
                ConsertadoPelaFortaleza += c.valor - antes;
                _lista[k] = c;
            }
        }

        /// <summary>Um zumbi bateu nela agora (as estacas também contam para o sossego da Fortaleza).</summary>
        void BateuNa(ushort id) => _batidaEm[id] = Time.time;

        // a barricada de tábuas caiu (os zumbis, o golpe, o Brutamonte): some, 2 madeiras no chão em frente, e a porta volta
        // a funcionar (fechada: ninguém escreve nela)
        void BarricadaDeTabuasQuebrou(int k, Construcao c)
        {
            _lista.RemoveAt(k);
            _barricadasDosVaos.Remove(c.vao);
            Vector3 frente = Quaternion.Euler(0f, c.giro, 0f) * Vector3.forward;
            Vector3 onde = c.pos + frente * 0.45f;
            // (a da janela: as tábuas caem no chão, embaixo do peitoril)
            if (Physics.Raycast(onde + Vector3.up * 0.3f, Vector3.down, out var chao, 3.5f, MascaraDoChaoDasTabuas, QueryTriggerInteraction.Ignore)) onde = chao.point;
            ItensNoMundo.Instancia?.Criar(Item.Madeira, onde, c.giro, 2);
            BarricadaRpc(EfeitoArrebentouAsTabuas, MeioDaBarricada(c.pos, Janela(c), c.altura));
            Quebradas++;
        }

        static readonly int MascaraDoChaoDasTabuas = ~((1 << 2) | (1 << JogadorRede.CamadaBracos) | (1 << 7) | (1 << 8) | (1 << JogadorRede.CamadaJogador));

        /// <summary>Quantas barricadas de tábuas caíram nesta partida (o teste).</summary>
        public int Quebradas { get; private set; }

        void EsquecerABarricada(ushort id) => _batidaEm.Remove(id);

        // ------------------------------------------------------------------ o que cada cópia vê

        const byte EfeitoPregou = 1, EfeitoArrancou = 2, EfeitoArrebentouAsTabuas = 3;

        [Rpc(SendTo.ClientsAndHost)]
        void BarricadaRpc(byte efeito, Vector3 onde)
        {
            switch (efeito)
            {
                case EfeitoPregou:
                    Efeitos.Som(SomDasArmadilhas.Martelo, onde, 1f, true, 30f);
                    break;
                case EfeitoArrancou:
                    Efeitos.Som(SomDasArmadilhas.Arrancar, onde, 0.9f, true, 20f);
                    break;
                case EfeitoArrebentouAsTabuas:
                    Efeitos.Som(SomDasArmadilhas.Madeira, onde, 1f, true, 35f);
                    Efeitos.Som(SomDasArmadilhas.Arrancar, onde, 0.7f, true, 25f);
                    break;
            }
        }

        // cada cópia: a malha danificada abaixo de 50%; e o jogador local segurando o E (desmontar) ou o clique (consertar)
        // numa barricada de tábuas: o som repete nela
        ConstrucaoNaMira _mexendo;
        float _proximoSomDaBarricada;

        void BarricadasAgora()
        {
            foreach (var c in _lista)
            {
                if (c.tipo != Item.TabuasBarricada || !_objetos.TryGetValue(c.id, out var o) || o == null) continue;
                var filtro = o.GetComponent<MeshFilter>();
                var malha = MalhaDaBarricada(c);
                if (filtro != null && malha != null && filtro.sharedMesh != malha) filtro.sharedMesh = malha;
            }
            var arma = JogadorRede.Local != null ? JogadorRede.Local.GetComponent<ArmaJogador>() : null;
            var alvo = arma != null ? arma.SegurandoAgora as ConstrucaoNaMira : null;
            var d = alvo != null && alvo.mundo == this ? Dados(alvo.id) : null;
            if (d == null || d.Value.tipo != Item.TabuasBarricada)
            {
                _mexendo = null;
                return;
            }
            if (alvo != _mexendo)
            {
                _mexendo = alvo;
                _proximoSomDaBarricada = 0f;
            }
            if (Time.time < _proximoSomDaBarricada) return;
            Vector3 onde = MeioDaBarricada(d.Value.pos, Janela(d.Value), d.Value.altura);
            bool conserto = arma.SegurandoPeloClique;
            Efeitos.Som(conserto ? SomDasArmadilhas.Martelo : SomDasArmadilhas.Arrancar, onde, 0.75f, true, 20f);
            _proximoSomDaBarricada = Time.time + (conserto ? 1.05f : 0.8f);
        }

        // ------------------------------------------------------------------ a dica e o teste

        /// <summary>A dica da barricada de tábuas na mira (a vida, desmontar e consertar).</summary>
        public string DicaDaBarricadaDeTabuas(Construcao c, ArmaJogador quem)
        {
            float max = MaximoDe(c), pct = c.valor / max * 100f;
            string s = $"Barricada de tábuas ({Mathf.CeilToInt(pct)}%)";
            if (!DoLadoDela(c, quem.transform.position)) return s + "   ·   pregada do outro lado";
            // (curta, numa linha só: a barra do segurar fica logo embaixo; 11/11/2026, a foto do conserto mostrou a dica em duas)
            if (PodeConsertar(c, quem)) return s + $"   ·   [segure o clique] Consertar +{A.Conserto * 100f:0}%   ·   [segure E] Desmontar";
            s += quem.Inventario.Quantos(Item.Ferramenta) > 0
                ? "   ·   [segure E] Desmontar" + (pct >= A.TabuasVoltamCom ? "" : " (sobram 2 madeiras)")
                : "   ·   a ferramenta desmonta";
            if (c.valor < max - 0.5f && A.BarricadaDe(quem.GetComponent<JogadorRede>()) >= 2)
                s += $"   ·   conserto: {A.MadeiraDoConserto} madeira, {A.PregosDoConserto} pregos";
            return s;
        }

        /// <summary>Só no anfitrião (o teste): as barricadas (a solta e as de tábuas: a vida, a máxima, o vão e o lado).</summary>
        public string ResumoDasBarricadas()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var c in _lista)
                if (c.tipo == Item.Barricada || c.tipo == Item.TabuasBarricada)
                    sb.Append(System.FormattableString.Invariant(
                        $"[{c.id} {(c.tipo == Item.Barricada ? "solta" : Janela(c) ? "janela" : "porta")}: {c.valor:F1} de {MaximoDe(c):F0}, nível {c.nivel}, extra {c.extra}, vão {c.vao:X}, {c.largura:F3} × {c.altura:F3}, ({c.pos.x:F2}, {c.pos.y:F2}, {c.pos.z:F2}), giro {c.giro:F0}] "));
            return sb.ToString();
        }

        /// <summary>Só no anfitrião (o teste): põe a vida da construção (a barricada gasta, para o conserto).</summary>
        public void PorAVida(ushort id, float valor)
        {
            int k = Indice(id);
            if (!IsServer || k < 0) return;
            var c = _lista[k];
            c.valor = Mathf.Clamp(valor, 0.1f, MaximoDe(c));
            _lista[k] = c;
        }
    }
}
