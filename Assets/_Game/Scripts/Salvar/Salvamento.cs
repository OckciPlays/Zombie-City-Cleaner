using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using ZombieCityCleaner.Combat;
using ZombieCityCleaner.Loot;
using ZombieCityCleaner.Mundo;
using ZombieCityCleaner.Player;
using ZombieCityCleaner.Vehicles;
using ZombieCityCleaner.World;
using ZombieCityCleaner.Zombies;

namespace ZombieCityCleaner.Salvar
{
    /// <summary>
    /// O save (02/11/2026, a pedido: "e já vamos fazer o sistema de save"), no objeto "Mundo" (com o
    /// <see cref="MundoDaPartida"/>):
    ///  - o mundo fica com quem hospeda (<see cref="MundoSalvo"/>, posto pelo menu em <see cref="Atual"/> antes de
    ///    hospedar: um novo, ou o do Continuar); cada jogador guarda o próprio personagem (<see cref="PersonagemSalvo"/>);
    ///  - os modos (<see cref="ModoDeSalvar"/>, escolhido ao criar o mundo): a cada 5 minutos e ao sair; só ao sair; só
    ///    dormindo (E na própria cama, ou numa cama da casa da família). Dormir salva em qualquer modo;
    ///  - quando o anfitrião salva, ele grava o mundo e pede a todos que gravem o próprio personagem
    ///    (<see cref="SalvarPersonagensRpc"/>);
    ///  - carregando um mundo salvo, quando ele fica pronto: a hora, o dia, o clima, o chão, as construções, os carros do
    ///    mundo mexidos, a mobília aberta, o saque já pego, as portas e as janelas das casas, as estantes do bunker, os
    ///    sustos, o painel de armas e os dois carros (no lugar dos do começo, que o <see cref="MundoDaPartida"/> não cria);
    ///    cada personagem que entra vai para onde estava (<see cref="AoCarregarPersonagem"/>);
    ///  - no Dia 0, o personagem salvo de quem entra espera a porta do abrigo (<see cref="GuardarParaDepois"/>).
    /// Sem <see cref="Atual"/> (os testes, a linha de comando, o robô), a partida não salva.
    /// </summary>
    public class Salvamento : NetworkBehaviour
    {
        public const float MinutosEntreSalvamentos = 5f;

        public static Salvamento Instancia { get; private set; }

        /// <summary>O mundo salvo desta partida (no anfitrião; posto pelo menu antes de hospedar). Null: não salva.</summary>
        public static MundoSalvo Atual { get; set; }
        /// <summary>A partida veio do save (o mundo já tem estado): os carros e a chave do começo não são criados.</summary>
        public static bool Carregando => Atual != null && Atual.temEstado;

        // sem o Domain Reload, o mundo de um Play ficava para o próximo
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Zerar() => Atual = null;

        // o modo, para todos (255: esta partida não salva)
        readonly NetworkVariable<byte> _modo = new(255);

        /// <summary>Esta partida salva (o anfitrião tem um mundo salvo).</summary>
        public bool Salva => IsSpawned && _modo.Value != 255;
        public ModoDeSalvar Modo => (ModoDeSalvar)Mathf.Min(_modo.Value, (byte)2);
        /// <summary>Quando salvou por último (o relógio desta máquina; -1, ainda não).</summary>
        public float SalvouEm { get; private set; } = -1f;

        MundoDaPartida _mundo;
        float _proximo;
        bool _restaurado, _saindo;
        readonly Dictionary<ulong, PacoteDoPersonagem> _depois = new Dictionary<ulong, PacoteDoPersonagem>();

        void Awake()
        {
            Instancia = this;
            _mundo = GetComponent<MundoDaPartida>();
        }

        public override void OnDestroy()
        {
            if (Instancia == this) Instancia = null;
            base.OnDestroy();
        }

        public override void OnNetworkSpawn()
        {
            Instancia = this;
            if (_mundo == null) _mundo = MundoDaPartida.Instancia;
            _restaurado = false;
            _saindo = false;
            _depois.Clear();
            SalvouEm = -1f;
            if (IsServer) _modo.Value = Atual != null ? (byte)Atual.Modo : (byte)255;
            if (_mundo != null) _mundo.FicouPronto += AoFicarPronto;
        }

        public override void OnNetworkDespawn()
        {
            if (_mundo != null) _mundo.FicouPronto -= AoFicarPronto;
        }

        // o mundo ficou pronto nesta máquina: no anfitrião, o que veio do save, e os personagens que esperavam o Dia 0
        void AoFicarPronto(DadosDoMundo dados)
        {
            if (!IsServer) return;
            _proximo = Time.unscaledTime + MinutosEntreSalvamentos * 60f;
            if (Carregando && !_restaurado) Restaurar();
            foreach (var (cliente, p) in _depois) InventarioDe(cliente)?.Aplicar(p);
            _depois.Clear();
        }

        void Update()
        {
            if (!IsServer || !Salva || _mundo == null || !_mundo.Pronto || _mundo.NoDiaZero) return;
            if (Modo == ModoDeSalvar.Automatico && Time.unscaledTime >= _proximo) Salvar("automático");
        }

        InventarioJogador InventarioDe(ulong cliente) =>
            NetworkManager.ConnectedClients.TryGetValue(cliente, out var c) && c.PlayerObject != null ? c.PlayerObject.GetComponent<InventarioJogador>() : null;

        // ------------------------------------------------------------------ salvar

        /// <summary>
        /// No anfitrião: grava o mundo e pede a cada jogador que grave o próprio personagem. Não salva sem um mundo salvo,
        /// antes de o mundo ficar pronto, nem no Dia 0.
        /// </summary>
        public bool Salvar(string motivo)
        {
            if (!IsServer || Atual == null || _mundo == null || !_mundo.Pronto || _mundo.NoDiaZero) return false;
            _proximo = Time.unscaledTime + MinutosEntreSalvamentos * 60f;
            var m = Atual;
            m.versaoDoGerador = GeradorDoMundo.Versao;
            m.semente = _mundo.Semente;
            m.tamanho = (int)_mundo.Tamanho;
            m.nome = _mundo.Nome;
            m.hora = CicloDoDia.Hora;
            m.dia = CicloDoDia.Dia;
            if (CicloDoDia.Instancia != null) m.tempo = CicloDoDia.Instancia.CopiaDoTempo();

            m.itens.Clear();
            if (ItensNoMundo.Instancia != null)
                foreach (var it in ItensNoMundo.Instancia.Copia())
                    m.itens.Add(new ItemNoChaoSalvo { item = (int)it.item, apoio = it.apoio, giro = it.giro, qtd = it.qtd, desgaste = it.desgaste, cores = unchecked((long)it.cores) });

            m.construcoes.Clear();
            if (ConstrucoesNoMundo.Instancia != null)
                foreach (var c in ConstrucoesNoMundo.Instancia.Copia())
                    m.construcoes.Add(new ConstrucaoSalva { tipo = (int)c.tipo, pos = c.pos, giro = c.giro, valor = c.valor, dono = unchecked((long)c.dono) });

            m.carros.Clear();
            foreach (var carro in Carro.Todos)
            {
                if (carro == null || !carro.IsSpawned) continue;
                var s = carro.ParaSalvar();
                s.qual = _mundo.segundoCarro != null && carro.name.StartsWith(_mundo.segundoCarro.name) ? 1 : 0;
                m.carros.Add(s);
            }

            m.carrosDoMundo.Clear();
            if (PecasDosCarrosDoMundo.Instancia != null)
                foreach (var e in PecasDosCarrosDoMundo.Instancia.Copia())
                {
                    var s = new CarroDoMundoSalvo { id = e.id, vela = e.pecas.vela, bateria = e.pecas.bateria, radiador = e.pecas.radiador, macaco = e.macaco };
                    for (int i = 0; i < PneusDoCarro.Quantas; i++) s.pneus[i] = e.pneus[i];
                    m.carrosDoMundo.Add(s);
                }

            m.mobiliaAberta.Clear();
            if (MobiliaNovaDoMundo.Instancia != null)
                foreach (var k in MobiliaNovaDoMundo.Instancia.Copia()) m.mobiliaAberta.Add(unchecked((long)k));

            m.saquePego.Clear();
            if (MobiliaNovaDoMundo.Instancia != null)
                foreach (var k in MobiliaNovaDoMundo.Instancia.CopiaDoSaque()) m.saquePego.Add(unchecked((long)k));

            m.portas.Clear();
            if (PortasDoMundo.Instancia != null)
                foreach (var p in PortasDoMundo.Instancia.Copia()) m.portas.Add(new PortaSalva { chave = unchecked((long)p.chave), bits = p.bits });

            m.estantes.Clear();
            if (EstantesDoBunker.Instancia != null)
                foreach (var it in EstantesDoBunker.Instancia.Copia())
                    m.estantes.Add(new ItemNaEstanteSalvo
                    {
                        estante = it.estante, item = (int)it.item, x = it.x, y = it.y, girado = it.girado, qtd = it.qtd, desgaste = it.desgaste,
                        cores = unchecked((long)it.cores),
                    });

            m.sustos = ZombieManager.Instancia != null ? ZombieManager.Instancia.Sustos() : new List<int>();
            if (ParedeDeArmas.Instancia != null) ParedeDeArmas.Instancia.Copia(out m.armasNaParede, out m.pentesNaParede);

            m.sobreviventes.Clear();
            if (Sobreviventes.SobreviventesDoMundo.Instancia != null)
                foreach (var (cidade, parte, geracao, vivos, quantos, diaDaMorte) in Sobreviventes.SobreviventesDoMundo.Instancia.Copia())
                    m.sobreviventes.Add(new GrupoSalvo { cidade = cidade, parte = parte, geracao = geracao, vivos = vivos, quantos = quantos, diaDaMorte = diaDaMorte });

            m.acampamentos.Clear();
            if (Sobreviventes.AcampamentosDoMundo.Instancia != null)
                foreach (var (acampamento, hostil, mortos, dia, estoque) in Sobreviventes.AcampamentosDoMundo.Instancia.Copia())
                    m.acampamentos.Add(new AcampamentoSalvo { indice = acampamento, hostil = hostil, mortos = mortos, diaDaReposicao = dia, estoque = estoque });

            m.carrosQuebrados.Clear();
            if (Sobreviventes.CarrosQuebradosDoMundo.Instancia != null)
                foreach (var c in Sobreviventes.CarrosQuebradosDoMundo.Instancia.Copia())
                    m.carrosQuebrados.Add(new CarroQuebradoSalvo { estado = (int)c.estado, mortos = c.mortos, pneu = c.pneu, falta = c.falta });

            m.mochilas.Clear();
            if (MochilasNoChao.Instancia != null)
                foreach (var (mc, itensDela) in MochilasNoChao.Instancia.Copia())
                {
                    var s = new MochilaSalva
                    {
                        nome = mc.nome.ToString(), pos = mc.pos, giro = mc.giro, mochila = (int)mc.mochila, dinheiro = mc.dinheiro,
                        colunas = mc.colunas, linhas = mc.linhas,
                    };
                    foreach (var it in itensDela)
                        s.itens.Add(new ItemSalvo { item = (int)it.item, x = it.x, y = it.y, girado = it.girado, qtd = it.qtd, desgaste = it.desgaste, cores = unchecked((long)it.cores) });
                    m.mochilas.Add(s);
                }

            foreach (var j in JogadorRede.Todos)
                if (j != null && j.Criado) m.LembrarPosicao(j.Chave.Value, j.transform.position, j.transform.eulerAngles.y);

            m.temEstado = true;
            try
            {
                m.Gravar();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[Save] Não deu para gravar o mundo: {e.Message}");
                return false;
            }
            SalvarPersonagensRpc(motivo == "dormindo");
            Debug.Log($"[Save] O mundo \"{m.nome}\" foi salvo ({motivo}): dia {m.dia}, {CicloDoDia.Relogio()}, {m.itens.Count} itens no chão, " +
                      $"{m.construcoes.Count} construções, {m.carros.Count} carros, {m.carrosDoMundo.Count} carros do mundo mexidos, {m.saquePego.Count} itens do saque pegos, " +
                      $"{m.portas.Count} portas e janelas mexidas, {m.estantes.Count} itens nas estantes, {m.posicoes.Count} posições.");
            return true;
        }

        /// <summary>Anfitrião → todos: o mundo salvou; cada um grava o próprio personagem.</summary>
        [Rpc(SendTo.Everyone)]
        void SalvarPersonagensRpc(bool dormindo)
        {
            SalvouEm = Time.unscaledTime;
            SalvarOPersonagemDaqui();
            InventarioJogador.AvisoLocal(dormindo ? "Você descansou. Jogo salvo." : "Jogo salvo.");
        }

        /// <summary>Nesta máquina: grava o personagem do jogador local com o que está na rede agora.</summary>
        public static void SalvarOPersonagemDaqui()
        {
            var p = EscolhaDoPersonagem.Atual;
            var j = JogadorRede.Local;
            if (p == null || j == null || !j.Criado || j.Inventario == null || UI.MenuOnline.Bot) return;
            j.Inventario.Encher(p);
            p.jogou = true;
            if (MundoDaPartida.Instancia != null) p.ultimoMundo = MundoDaPartida.Instancia.Nome;
            p.Gravar();
        }

        /// <summary>
        /// "Sair da partida" (o menu): nos modos automático e ao sair, salva antes. O anfitrião salva o mundo e todos, e só
        /// sai depois de um instante (para o pedido chegar aos outros); quem entrou num mundo grava o próprio personagem.
        /// Devolve se vai sair depois (o anfitrião), e então chama <paramref name="sair"/> sozinho.
        /// </summary>
        public static bool AoSairDaPartida(System.Action sair)
        {
            var s = Instancia;
            if (s == null || !s.IsSpawned || !s.Salva || s.Modo == ModoDeSalvar.Dormindo || s._saindo) return false;
            if (!s.IsServer)
            {
                SalvarOPersonagemDaqui();
                return false;
            }
            if (!s.Salvar("saindo")) return false;
            s._saindo = true;
            s.StartCoroutine(s.SairDepois(sair));
            return true;
        }

        IEnumerator SairDepois(System.Action sair)
        {
            yield return new WaitForSecondsRealtime(0.6f);
            sair?.Invoke();
        }

        void OnApplicationQuit()
        {
            // o jogo fechou no meio da partida (nos modos automático e ao sair): o que der para salvar agora
            if (!IsSpawned || !Salva || Modo == ModoDeSalvar.Dormindo) return;
            if (IsServer) Salvar("o jogo fechou");
            else SalvarOPersonagemDaqui();
        }

        // ------------------------------------------------------------------ dormir

        /// <summary>O jogador local dorme (na própria cama, ou numa cama da casa da família): o anfitrião salva.</summary>
        public static void PedirDormir()
        {
            if (Instancia != null && Instancia.IsSpawned) Instancia.DormirRpc();
        }

        [Rpc(SendTo.Server)]
        void DormirRpc(RpcParams parametros = default)
        {
            ulong quem = parametros.Receive.SenderClientId;
            if (Atual == null) AvisoRpc("Esta partida não salva (ela não veio do menu).", RpcTarget.Single(quem, RpcTargetUse.Temp));
            else if (!Salvar("dormindo")) AvisoRpc("Agora não dá para dormir.", RpcTarget.Single(quem, RpcTargetUse.Temp));
        }

        [Rpc(SendTo.SpecifiedInParams)]
        void AvisoRpc(string texto, RpcParams parametros = default) => InventarioJogador.AvisoLocal(texto);

        // ------------------------------------------------------------------ carregar

        /// <summary>No anfitrião: quem saiu (ou o anfitrião, ao fechar) fica lembrado onde estava, para o próximo save.</summary>
        public static void LembrarPosicao(JogadorRede j)
        {
            if (Atual == null || j == null || !j.Criado || j.Chave.Value == 0) return;
            Atual.LembrarPosicao(j.Chave.Value, j.transform.position, j.transform.eulerAngles.y);
        }

        /// <summary>No anfitrião: o personagem salvo de quem entra no Dia 0 espera a porta do abrigo.</summary>
        public void GuardarParaDepois(InventarioJogador inventario, PacoteDoPersonagem p)
        {
            if (IsServer && inventario != null) _depois[inventario.OwnerClientId] = p;
        }

        /// <summary>No anfitrião: o personagem salvo entrou; se ele já esteve neste mundo, vai para onde estava.</summary>
        public void AoCarregarPersonagem(JogadorRede j)
        {
            if (!IsServer || j == null || Atual == null) return;
            var pos = Atual.PosicaoDe(j.Chave.Value);
            if (pos == null) return;
            IrParaRpc(pos.pos, pos.giro, RpcTarget.Single(j.OwnerClientId, RpcTargetUse.Temp));
        }

        [Rpc(SendTo.SpecifiedInParams)]
        void IrParaRpc(Vector3 pos, float giro, RpcParams parametros = default) => StartCoroutine(IrPara(pos, giro));

        // no dono: espera o chão de lá ter colisão (o jogador fica na casa enquanto isso) e vai
        IEnumerator IrPara(Vector3 pos, float giro)
        {
            var terreno = _mundo != null ? _mundo.terreno : null;
            float ate = Time.unscaledTime + 20f;
            if (terreno != null)
            {
                terreno.Pedir(this, pos, 40f, true);
                while (!terreno.Pronto(pos, 12f) && Time.unscaledTime < ate) yield return null;
            }
            var j = JogadorRede.Local;
            if (j != null) j.GetComponent<MovimentoPrimeiraPessoa>().Teleportar(pos + Vector3.up * 0.1f, giro);
            yield return new WaitForSeconds(2f);
            terreno?.Esquecer(this);
        }

        // o mundo salvo no lugar do novo (no anfitrião, quando o mundo fica pronto)
        void Restaurar()
        {
            _restaurado = true;
            var m = Atual;
            var ciclo = CicloDoDia.Instancia;
            if (ciclo != null)
            {
                ciclo.Definir(m.hora, m.dia);
                ciclo.RestaurarTempo(m.tempo);
            }

            var itens = new List<ItemNoChao>(m.itens.Count);
            foreach (var it in m.itens)
                itens.Add(new ItemNoChao
                {
                    item = (Item)it.item, apoio = it.apoio, giro = it.giro, qtd = (ushort)Mathf.Clamp(it.qtd, 0, ushort.MaxValue), desgaste = (byte)Mathf.Clamp(it.desgaste, 0, 100),
                    cores = unchecked((ulong)it.cores),
                });
            ItensNoMundo.Instancia?.Restaurar(itens);

            var construcoes = new List<Construcao>(m.construcoes.Count);
            foreach (var c in m.construcoes)
                construcoes.Add(new Construcao { tipo = (Item)c.tipo, pos = c.pos, giro = c.giro, valor = c.valor, dono = unchecked((ulong)c.dono) });
            ConstrucoesNoMundo.Instancia?.Restaurar(construcoes);

            var mexidos = new List<CarroMexido>(m.carrosDoMundo.Count);
            foreach (var s in m.carrosDoMundo)
            {
                var e = new CarroMexido { id = s.id, macaco = (byte)Mathf.Clamp(s.macaco, 0, 255) };
                e.pecas = new PecasDoMotor { vela = (byte)s.vela, bateria = (byte)s.bateria, radiador = (byte)s.radiador };
                for (int i = 0; i < PneusDoCarro.Quantas && i < s.pneus.Length; i++) e.pneus[i] = (byte)Mathf.Clamp(s.pneus[i], 0, 255);
                mexidos.Add(e);
            }
            PecasDosCarrosDoMundo.Instancia?.Restaurar(mexidos);

            var abertas = new List<ulong>(m.mobiliaAberta.Count);
            foreach (long k in m.mobiliaAberta) abertas.Add(unchecked((ulong)k));
            MobiliaNovaDoMundo.Instancia?.Restaurar(abertas);

            var pegos = new List<ulong>(m.saquePego.Count);
            foreach (long k in m.saquePego) pegos.Add(unchecked((ulong)k));
            MobiliaNovaDoMundo.Instancia?.RestaurarOSaque(pegos);

            var portas = new List<PortasDoMundo.Mudanca>(m.portas.Count);
            foreach (var p in m.portas) portas.Add(new PortasDoMundo.Mudanca { chave = unchecked((ulong)p.chave), bits = (byte)(p.bits & 3) });
            PortasDoMundo.Instancia?.Restaurar(portas);

            var estantes = new List<ItemNaEstante>(m.estantes.Count);
            foreach (var it in m.estantes)
                estantes.Add(new ItemNaEstante
                {
                    estante = (ushort)Mathf.Clamp(it.estante, 0, ushort.MaxValue), item = (Item)it.item, x = (byte)Mathf.Clamp(it.x, 0, 255),
                    y = (byte)Mathf.Clamp(it.y, 0, 255), girado = it.girado, qtd = (ushort)Mathf.Clamp(it.qtd, 0, ushort.MaxValue),
                    desgaste = (byte)Mathf.Clamp(it.desgaste, 0, 100), cores = unchecked((ulong)it.cores),
                });
            EstantesDoBunker.Instancia?.Restaurar(estantes);

            ZombieManager.Instancia?.RestaurarSustos(m.sustos);
            if (m.armasNaParede != 0 || m.pentesNaParede != 0) ParedeDeArmas.Instancia?.Restaurar(m.armasNaParede, m.pentesNaParede);

            var grupos = new List<(int, int, int, int, int, int)>(m.sobreviventes.Count);
            foreach (var g in m.sobreviventes) grupos.Add((g.cidade, g.parte, g.geracao, g.vivos, g.quantos, g.diaDaMorte));
            Sobreviventes.SobreviventesDoMundo.Instancia?.Restaurar(grupos);

            var acampamentos = new List<(int, bool, int, int, List<int>)>(m.acampamentos.Count);
            foreach (var a in m.acampamentos) acampamentos.Add((a.indice, a.hostil, a.mortos, a.diaDaReposicao, a.estoque));
            Sobreviventes.AcampamentosDoMundo.Instancia?.Restaurar(acampamentos);

            if (m.carrosQuebrados != null && m.carrosQuebrados.Count > 0)
            {
                var quebrados = new List<Sobreviventes.CenaDoCarroQuebrado>(m.carrosQuebrados.Count);
                foreach (var c in m.carrosQuebrados)
                    quebrados.Add(new Sobreviventes.CenaDoCarroQuebrado
                    {
                        estado = (Sobreviventes.EstadoDoCarroQuebrado)Mathf.Clamp(c.estado, 0, 4), mortos = (byte)(c.mortos & 3),
                        pneu = (byte)Mathf.Clamp(c.pneu, 0, 100), falta = (byte)Mathf.Clamp(c.falta, 0, 250),
                    });
                Sobreviventes.CarrosQuebradosDoMundo.Instancia?.Restaurar(quebrados);
            }

            var mochilas = new List<(MochilaNoChao, List<ItemNaMochila>)>(m.mochilas.Count);
            foreach (var s in m.mochilas)
            {
                var mc = new MochilaNoChao
                {
                    pos = s.pos, giro = s.giro, mochila = (Item)s.mochila, dinheiro = s.dinheiro,
                    colunas = (byte)Mathf.Clamp(s.colunas, 1, 255), linhas = (byte)Mathf.Clamp(s.linhas, 1, 255),
                    nome = new Unity.Collections.FixedString32Bytes(MochilasNoChao.Curto(s.nome)),
                };
                var itensDela = new List<ItemNaMochila>(s.itens.Count);
                foreach (var it in s.itens)
                    itensDela.Add(new ItemNaMochila
                    {
                        item = (Item)it.item, x = (byte)Mathf.Clamp(it.x, 0, 255), y = (byte)Mathf.Clamp(it.y, 0, 255), girado = it.girado,
                        qtd = (ushort)Mathf.Clamp(it.qtd, 0, ushort.MaxValue), desgaste = (byte)Mathf.Clamp(it.desgaste, 0, 100),
                        cores = unchecked((ulong)it.cores),
                    });
                mochilas.Add((mc, itensDela));
            }
            MochilasNoChao.Instancia?.Restaurar(mochilas);

            foreach (var c in m.carros) StartCoroutine(CriarCarro(c));
            Debug.Log($"[Save] O mundo \"{m.nome}\" voltou: dia {m.dia}, {CicloDoDia.Relogio()}, {itens.Count} itens no chão, {construcoes.Count} construções, " +
                      $"{m.carros.Count} carros, {mexidos.Count} carros do mundo mexidos, {pegos.Count} itens do saque pegos, {portas.Count} portas e janelas mexidas, " +
                      $"{estantes.Count} itens nas estantes.");
        }

        // um carro do save: nasce parado (sem a física) até o chão de lá ter colisão, para não cair por ele
        IEnumerator CriarCarro(CarroSalvo c)
        {
            var prefab = c.qual == 1 ? _mundo.segundoCarro : _mundo.carro;
            if (prefab == null) yield break;
            var novo = Instantiate(prefab, c.pos + Vector3.up * 0.15f, c.giro);
            novo.name = c.qual == 1 ? prefab.name : "Carro";
            var corpo = novo.GetComponent<Rigidbody>();
            if (corpo != null) corpo.isKinematic = true;
            novo.GetComponent<NetworkObject>().Spawn(true);
            novo.GetComponent<Carro>()?.Restaurar(c);
            var terreno = _mundo.terreno;
            float ate = Time.unscaledTime + 30f;
            if (terreno != null)
            {
                terreno.Pedir(novo, c.pos, 20f, true);
                while (!terreno.Pronto(c.pos, 6f) && Time.unscaledTime < ate) yield return null;
                terreno.Esquecer(novo);
            }
            if (corpo != null && novo != null)
            {
                corpo.isKinematic = false;
                corpo.WakeUp();
            }
        }
    }
}
