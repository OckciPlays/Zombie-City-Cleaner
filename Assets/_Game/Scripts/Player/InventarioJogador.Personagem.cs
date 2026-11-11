using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using ZombieCityCleaner.Loot;
using ZombieCityCleaner.Salvar;
using ZombieCityCleaner.Video;

namespace ZombieCityCleaner.Player
{
    /// <summary>O personagem salvo pela rede (o dono manda ao anfitrião quando entra num mundo).</summary>
    public struct PacoteDoPersonagem : INetworkSerializable
    {
        /// <summary>Os itens da mochila (o Id de cada um é dado de novo pelo anfitrião).</summary>
        public ItemNaMochila[] itens;
        public Item mochila;
        public byte pneu;
        public int dinheiro;
        public float vida, fome, sede;
        /// <summary>Os 3 atalhos: o índice do item em <see cref="itens"/> (-1, vazio).</summary>
        public sbyte a0, a1, a2;
        public byte escolhido;
        /// <summary>As roupas vestidas (só com <see cref="comRoupas"/>: um save de antes delas entra com as do começo).</summary>
        public RoupasVestidas roupas;
        public bool comRoupas;
        /// <summary>O corpo (05/11/2026; vazio = sadio, como num save de antes dele).</summary>
        public EstadoDoCorpo corpo;
        /// <summary>O abalo da mente (06/11/2026; 0 = calma, como num save de antes dela).</summary>
        public float abaloDaMente;
        /// <summary>O cansaço e a nicotina (06/11/2026, a etapa 3 da mente).</summary>
        public float cansaco, nicotina;
        public bool viciado;
        /// <summary>O XP e as habilidades (06/11/2026, a árvore de habilidades).</summary>
        public ProgressoDoPersonagem progresso;
        /// <summary>A proteção de sucata vestida (11/11/2026; vazia num save de antes dela).</summary>
        public ProtecaoVestida protecao;

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            int n = itens?.Length ?? 0;
            s.SerializeValue(ref n);
            if (s.IsReader) itens = new ItemNaMochila[n];
            for (int i = 0; i < n; i++) s.SerializeValue(ref itens[i]);
            s.SerializeValue(ref mochila);
            s.SerializeValue(ref pneu);
            s.SerializeValue(ref dinheiro);
            s.SerializeValue(ref vida);
            s.SerializeValue(ref fome);
            s.SerializeValue(ref sede);
            s.SerializeValue(ref a0);
            s.SerializeValue(ref a1);
            s.SerializeValue(ref a2);
            s.SerializeValue(ref escolhido);
            s.SerializeValue(ref roupas);
            s.SerializeValue(ref comRoupas);
            s.SerializeValue(ref corpo);
            s.SerializeValue(ref abaloDaMente);
            s.SerializeValue(ref cansaco);
            s.SerializeValue(ref nicotina);
            s.SerializeValue(ref viciado);
            s.SerializeValue(ref progresso);
            s.SerializeValue(ref protecao);
        }

        public static PacoteDoPersonagem De(PersonagemSalvo p)
        {
            var itens = new ItemNaMochila[p.itens.Count];
            var aMais = new System.Collections.Generic.List<ItemNaMochila>();
            for (int i = 0; i < itens.Length; i++)
            {
                var it = p.itens[i];
                itens[i] = new ItemNaMochila
                {
                    item = (Item)it.item, x = (byte)Mathf.Clamp(it.x, 0, 255), y = (byte)Mathf.Clamp(it.y, 0, 255), girado = it.girado,
                    qtd = (ushort)Mathf.Clamp(it.qtd, 0, ushort.MaxValue), desgaste = (byte)Mathf.Clamp(it.desgaste, 0, 100),
                    cores = unchecked((ulong)it.cores),
                };
                // (05/11/2026) o spray empilhava até 3 e agora é um por célula, com os usos: a pilha antiga vira sprays
                // separados (o Aplicar acha lugar para eles, ou larga no chão)
                if (Itens.ComUsos(itens[i].item) && itens[i].qtd > 1)
                {
                    for (int k = 1; k < itens[i].qtd && k < 10; k++)
                    {
                        var outro = itens[i];
                        outro.qtd = 1;
                        aMais.Add(outro);
                    }
                    itens[i].qtd = 1;
                }
            }
            if (aMais.Count > 0)
            {
                // os novos vão no fim (os atalhos apontam pelo índice: não mudam)
                var todos = new ItemNaMochila[itens.Length + aMais.Count];
                itens.CopyTo(todos, 0);
                aMais.CopyTo(todos, itens.Length);
                itens = todos;
            }
            var roupas = new RoupasVestidas();
            bool comRoupas = p.roupas != null && p.roupas.Length == Itens.QuantosLugaresDeRoupa;
            if (comRoupas)
                for (int k = 0; k < Itens.QuantosLugaresDeRoupa; k++)
                {
                    long cores = p.coresDasRoupas != null && k < p.coresDasRoupas.Length ? p.coresDasRoupas[k] : 0;
                    roupas.Por((LugarDaRoupa)k, (Item)p.roupas[k], unchecked((ulong)cores));
                }
            var protecao = new ProtecaoVestida();
            if (p.protecao != null)
                for (int k = 0; k < Protecao.Quantos && k < p.protecao.Length; k++)
                {
                    int gasto = p.desgasteDaProtecao != null && k < p.desgasteDaProtecao.Length ? p.desgasteDaProtecao[k] : 0;
                    protecao.Por((LugarDaProtecao)k, (Item)Mathf.Clamp(p.protecao[k], 0, 255), gasto);
                }
            sbyte A(int k) => (sbyte)(p.atalhos != null && k < p.atalhos.Length ? Mathf.Clamp(p.atalhos[k], -1, 127) : -1);
            return new PacoteDoPersonagem
            {
                itens = itens, mochila = (Item)p.mochila, pneu = (byte)Mathf.Clamp(p.pneu, 0, 255), dinheiro = p.dinheiro,
                vida = p.vida, fome = p.fome, sede = p.sede, a0 = A(0), a1 = A(1), a2 = A(2), escolhido = (byte)Mathf.Clamp(p.escolhido, 0, 2),
                roupas = roupas, comRoupas = comRoupas, corpo = p.corpo.Conferido(),
                abaloDaMente = Mathf.Clamp(p.abaloDaMente, 0f, VidaJogador.MenteMaxima),
                cansaco = Mathf.Clamp(p.cansaco, 0f, 100f), nicotina = Mathf.Clamp(p.nicotina, 0f, 100f), viciado = p.viciado,
                progresso = p.progresso, protecao = protecao,
            };
        }
    }

    /// <summary>
    /// O personagem (02/11/2026, a pedido: o save; "cada um leva o seu personagem"): o dinheiro dele e o que ele leva quando
    /// entra num mundo. O dono, ao criar o personagem (<see cref="JogadorRede.Criar"/>), manda o personagem salvo ao
    /// anfitrião (<see cref="CarregarRpc"/>), ou pede o começo de um novo (<see cref="NovoRpc"/>: a pistola e a lanterna no
    /// chão, menos no Dia 0 e no vídeo). No Dia 0, o personagem salvo espera a porta do abrigo (<see cref="Salvamento"/>).
    /// O dono também enche o arquivo dele com o que está na rede (<see cref="Encher"/>), quando o mundo salva.
    /// </summary>
    public partial class InventarioJogador
    {
        /// <summary>Os dólares do personagem (o que sobrou do Dia 0, mais o que ganhar). Escrito pelo anfitrião.</summary>
        public readonly NetworkVariable<int> Dinheiro = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        bool _comecou, _pedirComeco;
        PersonagemSalvo _paraCarregar;

        /// <summary>
        /// No dono, ao criar o personagem: no próximo quadro (depois de o inventário nascer: no OnNetworkSpawn ele ainda
        /// limpa a mochila), manda o salvo (ou pede o começo de um novo) ao anfitrião, uma vez. Se é salvo ou novo, decide
        /// agora (a tela de criar marca o novo como jogado logo depois).
        /// </summary>
        public void ComecarPersonagem()
        {
            if (!IsOwner || _comecou) return;
            var p = EscolhaDoPersonagem.Atual;
            _paraCarregar = p != null && p.jogou && !MenuOnlineBot ? p : null;
            _pedirComeco = true;
        }

        static bool MenuOnlineBot => UI.MenuOnline.Bot;

        void EnviarPersonagem()
        {
            _pedirComeco = false;
            if (_comecou) return;
            _comecou = true;
            if (_paraCarregar != null) CarregarRpc(PacoteDoPersonagem.De(_paraCarregar));
            else NovoRpc();
        }

        /// <summary>No anfitrião: soma (ou tira) dinheiro do personagem, nunca abaixo de 0.</summary>
        public void Pagar(int dolares)
        {
            if (IsServer) Dinheiro.Value = Mathf.Max(0, Dinheiro.Value + dolares);
        }

        // ------------------------------------------------------------------ a mochila no chão (Loot/MochilasNoChao)

        /// <summary>O primeiro lugar em que o item cabe numa grade qualquer (a de uma mochila no chão).</summary>
        public static bool EspacoNaGrade(List<ItemNaMochila> postos, Vector2Int espaco, Item item, out int x, out int y, out bool girado) =>
            EspacoEm(postos, espaco, item, out x, out y, out girado);

        /// <summary>No anfitrião: tira tudo (a mochila vestida, os itens e o pneu das mãos) e devolve os itens.</summary>
        public List<ItemNaMochila> TirarTudo(out Item mochila, out int pneu)
        {
            var itens = new List<ItemNaMochila>();
            mochila = MochilaVestida.Value;
            pneu = PneuNasMaos.Value;
            if (!IsServer) return itens;
            foreach (var it in Mochila) itens.Add(it);
            Mochila.Clear();
            MochilaVestida.Value = Item.Nada;
            PneuNasMaos.Value = SemPneu;
            return itens;
        }

        /// <summary>No anfitrião: veste a mochila e põe os itens (no lugar deles, se couber; senão, onde der). Devolve o que não coube.</summary>
        public List<ItemNaMochila> VestirComItens(Item mochila, List<ItemNaMochila> itens)
        {
            var sobra = new List<ItemNaMochila>();
            if (!IsServer) return sobra;
            MochilaVestida.Value = Itens.EhMochila(mochila) ? mochila : Item.Nada;
            var espaco = TamanhoDaGrade;
            var postos = new List<ItemNaMochila>();
            foreach (var it in Mochila) postos.Add(it);
            foreach (var it in itens)
            {
                var novo = it;
                if (!CabeEm(postos, espaco, novo.item, novo.girado, novo.x, novo.y, 0))
                {
                    if (!EspacoEm(postos, espaco, novo.item, out int x, out int y, out bool girado))
                    {
                        sobra.Add(it);
                        continue;
                    }
                    novo.x = (byte)x;
                    novo.y = (byte)y;
                    novo.girado = girado;
                }
                novo.id = NovoId();
                postos.Add(novo);
                Mochila.Add(novo);
            }
            return sobra;
        }

        /// <summary>No anfitrião: o item (com as balas e o desgaste dele) entra; devolve quantos sobraram (0: entrou tudo).</summary>
        public int ReceberIgual(ItemNaMochila it)
        {
            if (!IsServer) return Itens.Quantos(it.item, it.qtd);
            if (Itens.EmPilha(it.item)) return ReceberEmPilha(it.item, Itens.Quantos(it.item, it.qtd));
            return Receber(it.item, it.qtd, it.desgaste, it.cores) ? 0 : 1;   // (as cores da roupa e as melhorias da arma de sucata, 10/11/2026)
        }

        /// <summary>No anfitrião: as balas no pente da arma (a cópia do anfitrião).</summary>
        public int PenteDe(ushort id, int qtd) => qtd;

        /// <summary>No anfitrião: um aviso na tela do dono.</summary>
        public void AvisarDono(string texto)
        {
            if (IsServer) AvisoRpc(texto);
        }

        /// <summary>O F1: mais 1.000 dólares (para testar).</summary>
        public void PedirMaisDinheiro()
        {
            if (IsSpawned) MaisDinheiroRpc();
        }

        [Rpc(SendTo.Server)]
        void MaisDinheiroRpc(RpcParams parametros = default)
        {
            if (parametros.Receive.SenderClientId == OwnerClientId) Pagar(1000);
        }

        [Rpc(SendTo.Server)]
        void NovoRpc(RpcParams parametros = default)
        {
            if (parametros.Receive.SenderClientId != OwnerClientId) return;
            // na cena de vídeo, de mãos vazias; no Dia 0 também (a vida normal: o que tiver, foi comprado)
            var mundo = World.MundoDaPartida.Instancia;
            if (ModoVideo.SemArma || (mundo != null && mundo.NoDiaZero)) return;
            Receber(Item.Pistola);
            if (!_lanternaDoComeco)
            {
                _lanternaDoComeco = true;
                StartCoroutine(LanternaDoComeco());
            }
            ushort pistola = Primeiro(Item.Pistola);
            CarregadoRpc(pistola, 0, 0, 0);
        }

        [Rpc(SendTo.Server)]
        void CarregarRpc(PacoteDoPersonagem p, RpcParams parametros = default)
        {
            if (parametros.Receive.SenderClientId != OwnerClientId) return;
            // no Dia 0, o personagem salvo espera a porta do abrigo (depois dela, chega no mundo com as coisas dele)
            var mundo = World.MundoDaPartida.Instancia;
            if (mundo != null && mundo.NoDiaZero && Salvamento.Instancia != null)
            {
                Salvamento.Instancia.GuardarParaDepois(this, p);
                return;
            }
            Aplicar(p);
        }

        /// <summary>
        /// No anfitrião: o personagem salvo entra. A mochila vestida, os itens (cada um no lugar dele; o que não couber vai
        /// para outro lugar ou para o chão), o pneu nas mãos, o dinheiro (somado: o do Dia 0 já pode estar lá), a vida, a
        /// fome e a sede; depois, os atalhos (no dono) e a posição dele neste mundo (<see cref="Salvamento"/>).
        /// </summary>
        public void Aplicar(PacoteDoPersonagem p)
        {
            if (!IsServer) return;
            Mochila.Clear();
            _proximoId = 1;
            PneuNasMaos.Value = SemPneu;
            MochilaVestida.Value = Itens.EhMochila(p.mochila) ? p.mochila : Item.Nada;
            var espaco = TamanhoDaGrade;
            var postos = new List<ItemNaMochila>();
            int n = p.itens?.Length ?? 0, caidos = 0;
            var ids = new ushort[n];
            for (int i = 0; i < n; i++)
            {
                var it = p.itens[i];
                if (!Itens.NoJogo(it.item)) continue;
                if (!CabeEm(postos, espaco, it.item, it.girado, it.x, it.y, 0))
                {
                    if (!EspacoEm(postos, espaco, it.item, out int x, out int y, out bool girado))
                    {
                        ItensNoMundo.Instancia?.Criar(it.item, PontoAosPes(++caidos), transform.eulerAngles.y + caidos * 70f, it.qtd, it.desgaste);
                        continue;
                    }
                    it.x = (byte)x;
                    it.y = (byte)y;
                    it.girado = girado;
                }
                it.id = NovoId();
                ids[i] = it.id;
                postos.Add(it);
                Mochila.Add(it);
            }
            if (p.pneu != SemPneu) PneuNasMaos.Value = (byte)Mathf.Clamp(p.pneu, 0, 100);
            Dinheiro.Value = Mathf.Max(0, Dinheiro.Value + p.dinheiro);
            var vida = GetComponent<VidaJogador>();
            if (vida != null)
            {
                vida.Vida.Value = Mathf.Clamp(p.vida, 10f, vida.vidaMaxima);
                vida.DefinirCorpo(p.corpo);   // as feridas, as lesões e as talas de quando ele saiu (05/11/2026)
                vida.DefinirAbalo(p.abaloDaMente);   // e a mente (06/11/2026)
                vida.DefinirSonoEVicio(p.cansaco, p.nicotina, p.viciado);
            }
            GetComponent<FomeESede>()?.Definir(p.fome, p.sede);
            DefinirProgresso(p.progresso);   // o XP e as habilidades (06/11/2026)
            if (p.comRoupas) VestirAsSalvas(p.roupas);
            VestirAProtecaoSalva(p.protecao);   // (a proteção de sucata, 11/11/2026)
            // (um save antigo pode ter no atalho um item que não vai mais na mão: a gaze e o desinfetante, 05/11/2026)
            ushort A(int k) => k >= 0 && k < n && Itens.VaiNaMao(p.itens[k].item) ? ids[k] : (ushort)0;
            CarregadoRpc(A(p.a0), A(p.a1), A(p.a2), p.escolhido);
            Debug.Log($"[Save] O personagem de {_rede?.Aparencia.Value.nome} entrou: {Mochila.Count} itens, {Itens.Nome(MochilaVestida.Value)}, " +
                      $"{DiaZero.PrecosDoDiaZero.Dinheiro(Dinheiro.Value)}.");
            Salvamento.Instancia?.AoCarregarPersonagem(_rede);
        }

        /// <summary>Anfitrião → dono: os atalhos do personagem (os Ids novos) e o da mão.</summary>
        [Rpc(SendTo.Owner)]
        void CarregadoRpc(ushort a0, ushort a1, ushort a2, byte escolhido)
        {
            long a = 0;
            a = Gravar(a, 0, a0);
            a = Gravar(a, 1, a1);
            a = Gravar(a, 2, a2);
            _atalhos.Value = a;
            Escolhido.Value = (byte)Mathf.Clamp(escolhido, 0, Atalhos - 1);
            Mudou?.Invoke();
        }

        /// <summary>
        /// No dono: o arquivo do personagem com o que está na rede agora (a mochila, o dinheiro, a vida, a fome, a sede e a
        /// aparência), os atalhos e o pente de cada arma (o que só o dono sabe).
        /// </summary>
        public void Encher(PersonagemSalvo p)
        {
            if (p == null) return;
            p.itens.Clear();
            var indice = new Dictionary<ushort, int>();
            foreach (var it in Mochila)
            {
                int qtd = it.qtd;
                if (Itens.EhArma(it.item) && _arma != null) qtd = _arma.MunicaoDe(it.id, it.item);
                indice[it.id] = p.itens.Count;
                p.itens.Add(new ItemSalvo { item = (int)it.item, x = it.x, y = it.y, girado = it.girado, qtd = qtd, desgaste = it.desgaste, cores = unchecked((long)it.cores) });
            }
            p.mochila = (int)MochilaVestida.Value;
            p.pneu = PneuNasMaos.Value;
            p.dinheiro = Dinheiro.Value;
            if (p.atalhos == null || p.atalhos.Length != Atalhos) p.atalhos = new int[Atalhos];
            for (int i = 0; i < Atalhos; i++)
            {
                ushort id = Atalho(i);
                p.atalhos[i] = id != 0 && indice.TryGetValue(id, out int k) ? k : -1;
            }
            p.escolhido = Escolhido.Value;
            var vestidas = Roupas.Value;
            p.roupas = new int[Itens.QuantosLugaresDeRoupa];
            p.coresDasRoupas = new long[Itens.QuantosLugaresDeRoupa];
            for (int k = 0; k < Itens.QuantosLugaresDeRoupa; k++)
            {
                p.roupas[k] = (int)vestidas.Peca((LugarDaRoupa)k);
                p.coresDasRoupas[k] = unchecked((long)vestidas.Cores((LugarDaRoupa)k));
            }
            var protecao = Protecao.Value;   // (a proteção de sucata, 11/11/2026)
            p.protecao = new int[Player.Protecao.Quantos];
            p.desgasteDaProtecao = new int[Player.Protecao.Quantos];
            for (int k = 0; k < Player.Protecao.Quantos; k++)
            {
                p.protecao[k] = (int)protecao.Peca((LugarDaProtecao)k);
                p.desgasteDaProtecao[k] = protecao.Desgaste((LugarDaProtecao)k);
            }
            if (_rede != null)
            {
                if (_rede.Vida != null)
                {
                    p.vida = _rede.Vida.Caido.Value ? _rede.Vida.vidaMaxima : _rede.Vida.Vida.Value;   // (caído: renasce inteiro)
                    p.corpo = _rede.Vida.Caido.Value ? default : _rede.Vida.Corpo.Value;
                    p.abaloDaMente = _rede.Vida.AbaloParaOSave;
                    p.cansaco = _rede.Vida.Cansaco.Value;
                    p.nicotina = _rede.Vida.Nicotina.Value;
                    p.viciado = _rede.Vida.TemEfeito(VidaJogador.EfeitoViciado);
                }
                p.progresso = Progresso.Value;   // o XP e as habilidades (06/11/2026)
                if (_rede.Criado) p.Vestir(_rede.Aparencia.Value);
            }
            var fs = GetComponent<FomeESede>();
            if (fs != null)
            {
                p.fome = fs.Fome.Value;
                p.sede = fs.Sede.Value;
            }
        }
    }
}
