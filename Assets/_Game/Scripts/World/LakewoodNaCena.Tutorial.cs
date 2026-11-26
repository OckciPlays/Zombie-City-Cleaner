using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using ZombieCityCleaner.Combat;
using ZombieCityCleaner.DiaZero;
using ZombieCityCleaner.Loot;
using ZombieCityCleaner.Mundo;
using ZombieCityCleaner.Player;
using ZombieCityCleaner.Sobreviventes;
using static ZombieCityCleaner.World.CampanhaDeLakewood;

namespace ZombieCityCleaner.World
{
    /// <summary>
    /// A fase 1 depois do Dia 0, o tutorial do Gus (25/11/2026, a etapa 6, a pedido do Pedro: "a primeira missão após o D0
    /// deveria ser com o Gus e não com o Capitão... ele poderia nos dar um machado para a gente ir catar madeira, lá a gente
    /// iria craftar uma mesa da oficina e começaríamos a fazer alguns craft básicos que ele nos ensinaria (as lanças, pegar
    /// algumas sucatas no chão...)"). No anfitrião:
    ///  - <b>o Gus</b> espera no pátio do bunker, do outro lado do portão (um Sobrevivente aliado e imortal, como o Capitão;
    ///    o morador Gus some lá de dentro enquanto isso: MoradoresDoBunker). Depois da fase, ele volta para dentro (o do pátio
    ///    some quando ninguém está perto);
    ///  - <b>as submissões</b> (<see cref="CampanhaDeLakewood"/>): falar com ele; pegar o machado (e a mochila pequena, a quem
    ///    não tem: o kit da mesa não cabe nos bolsos); juntar 2 toras; ter 4 madeiras; montar a mesa de oficina; juntar o
    ///    metal e a fita do pátio (uma peça e uma fita por jogador, espalhadas quando a submissão começa); uma lança para cada
    ///    um; uma fogueira acesa; uma tocha; uma habilidade; e falar com ele de novo. Os números vão para o caderno;
    ///  - <b>o limite</b> (em cada máquina, o jogador dela): até a fase acabar, quem passa de 300 m do bunker volta para a
    ///    beira, como na quarentena do Dia 0, com o aviso "O Gus ainda não terminou de explicar".
    /// </summary>
    public partial class LakewoodNaCena
    {
        /// <summary>O grupo do Gus na identidade (as outras máquinas acham ele por isto, para a conversa).</summary>
        public const ushort GrupoDoGus = 64998;

        /// <summary>O raio do limite do tutorial, em volta do bunker (m).</summary>
        public const float LimiteDoTutorial = 300f;

        const int TorasDoTutorial = 2, MadeirasDoTutorial = 4;

        Sobrevivente _gus;
        float _proximaContaDoTutorial, _avisoDoLimiteAte;
        bool _metalNoPatio;
        // quem já pegou o machado com o Gus (a conexão; quem volta para a partida pode pedir de novo)
        readonly HashSet<ulong> _machadosDados = new HashSet<ulong>();
        static Sobrevivente _gusVisto;

        /// <summary>Onde está o Gus do pátio (em qualquer máquina), ou null.</summary>
        public static Vector3? PosicaoDoGus() => _gusVisto != null && _gusVisto.IsSpawned ? _gusVisto.transform.position : (Vector3?)null;

        // ------------------------------------------------------------------ no anfitrião

        void AcompanharOTutorial(MundoDeLakewood m)
        {
            bool naFase = m.Fase == FaseDaCampanha.OBunker;
            PorOGus(naFase);
            if (!naFase || Time.time < _proximaContaDoTutorial) return;
            _proximaContaDoTutorial = Time.time + 0.5f;
            int jogadores = 0, toras = 0, madeiras = 0, metal = 0, fita = 0, lancas = 0, tochas = 0;
            bool habilidade = false;
            foreach (var j in JogadorRede.Todos)
            {
                if (j == null || !j.Criado || j.Inventario == null) continue;
                var inv = j.Inventario;
                jogadores++;
                toras += inv.Quantos(Item.Toras);
                madeiras += inv.Quantos(Item.Madeira);
                metal += inv.Quantos(Item.PecasDeMetal);
                fita += inv.Quantos(Item.FitaAdesiva);
                lancas += inv.Quantos(Item.Lanca);
                tochas += inv.Quantos(Item.Tocha);
                if (inv.Progresso.Value.Gastos > 0) habilidade = true;
            }
            jogadores = Mathf.Max(1, jogadores);
            const FaseDaCampanha F = FaseDaCampanha.OBunker;
            switch (m.Passo)
            {
                case CorteUmaArvore:
                    m.Numeros(Mathf.Min(toras, TorasDoTutorial), TorasDoTutorial, 0);
                    if (toras >= TorasDoTutorial) m.Concluir(F, CorteUmaArvore);
                    break;
                case RacheAsToras:
                    m.Numeros(Mathf.Min(madeiras, MadeirasDoTutorial), MadeirasDoTutorial, 0);
                    if (madeiras >= MadeirasDoTutorial) m.Concluir(F, RacheAsToras);
                    break;
                case MonteAMesa:
                    if (ConstrucoesNoMundo.Instancia != null && ConstrucoesNoMundo.Instancia.MesasMontadas() > 0) m.Concluir(F, MonteAMesa);
                    break;
                case JunteOMetal:
                {
                    // (25/11/2026, a revisão, junto do bug 97) a lança já feita conta como o par juntado: quem fazia a lança
                    // antes da hora gastava o par, e o grupo ficava sem metal no pátio para fechar a submissão
                    int pares = Mathf.Min(metal, fita) + lancas;
                    PorOMetalNoPatio(jogadores, pares);
                    m.Numeros(Mathf.Min(pares, jogadores), jogadores, 0);
                    if (pares >= jogadores) m.Concluir(F, JunteOMetal);
                    break;
                }
                case FacaALanca:
                    if (lancas < jogadores) PorOMetalNoPatio(jogadores, Mathf.Min(metal, fita) + lancas);
                    m.Numeros(Mathf.Min(lancas, jogadores), jogadores, 0);
                    if (lancas >= jogadores) m.Concluir(F, FacaALanca);
                    break;
                case FacaAFogueira:
                    _fogueirasAcesas.Clear();
                    ConstrucoesNoMundo.Instancia?.FogueirasAcesas(_fogueirasAcesas);
                    if (_fogueirasAcesas.Count > 0) m.Concluir(F, FacaAFogueira);
                    break;
                case FacaATocha:
                    if (tochas > 0) m.Concluir(F, FacaATocha);
                    break;
                case AprendaUmaHabilidade:
                    if (habilidade) m.Concluir(F, AprendaUmaHabilidade);
                    break;
            }
        }

        readonly List<(ushort, Vector3)> _fogueirasAcesas = new List<(ushort, Vector3)>();

        // o Gus no pátio do bunker, do outro lado do portão (o Capitão fica do lado direito), olhando para fora; depois da fase,
        // ele some do pátio quando ninguém está perto (o morador Gus volta lá dentro)
        void PorOGus(bool naFase)
        {
            if (!naFase)
            {
                if (_gus != null && _gus.IsSpawned && !AlguemPerto(_gus.transform.position, 30f))
                {
                    _gus.NetworkObject.Despawn(true);
                    _gus = null;
                    Debug.Log("[Tutorial] O Gus voltou para dentro do bunker.");
                }
                return;
            }
            if (_gus != null && _gus.IsSpawned) return;
            var sdm = SobreviventesDoMundo.Instancia;
            var b = Chao != null ? Chao.Bunker : null;
            if (sdm == null || sdm.prefab == null || b == null) return;
            var noMapa = b.NoMapa(PlantaDeBunker.PortaoX0 + PlantaDeBunker.PortaoLargura * 0.5f - 2.5f, PlantaDeBunker.Z0 - 4f);
            var ponto = OrigemDeLakewood.NoMundo(new Vector3(noMapa.x, b.Altura + 0.05f, noMapa.y));
            if (NavMesh.SamplePosition(ponto, out var hit, 4f, NavMesh.AllAreas)) ponto = hit.position;
            var longe = b.NoMapa(PlantaDeBunker.PortaoX0, PlantaDeBunker.Z0 - 20f);
            var fora = OrigemDeLakewood.NoMundo(new Vector3(longe.x, b.Altura, longe.y)) - ponto;
            float giro = Mathf.Atan2(fora.x, fora.z) * Mathf.Rad2Deg;
            var go = Instantiate(sdm.prefab, ponto, Quaternion.Euler(0f, giro, 0f));
            var sob = go.GetComponent<Sobrevivente>();
            sob.Identidade.Value = new IdentidadeDoSobrevivente
            {
                // (a roupa civil: a "Sobrevivente", a 0, sorteava a máscara de gás, que não combina com o dono do bunker)
                semente = 4242, arma = TipoDeArma.Pistola, roupa = 3, grupo = GrupoDoGus, papel = PapelDoSobrevivente.Lider, indice = 0,
            };
            var g = new GrupoDeSobreviventes { cidade = -1, aliado = true, quantos = 1 };
            sob.grupo = g;
            sob.nome = "Gus";
            sob.posto = ponto;
            sob.giroDoPosto = giro;
            SobreviventesDoMundo.Encher(sob, TipoDeArma.Pistola);
            go.GetComponent<NetworkObject>().Spawn(true);
            g.membros.Add(sob);
            _gus = sob;
            Debug.Log($"[Tutorial] O Gus espera no pátio do bunker ({SetorDe(ponto)}).");
        }

        /// <summary>No anfitrião (a conversa, <see cref="MundoDeLakewood.PedirOMachadoRpc"/>): o machado e a mochila pequena, uma vez por jogador.</summary>
        public void DarOMachado(JogadorRede quem)
        {
            var m = MundoDeLakewood.Instancia;
            if (quem == null || quem.Inventario == null || m == null || m.Fase != FaseDaCampanha.OBunker) return;
            var inv = quem.Inventario;
            if (!_machadosDados.Add(quem.OwnerClientId))
            {
                inv.AvisarODono("O Gus já te deu um machado.");
                return;
            }
            // a mochila primeiro (o machado cabe nela); quem já tem uma fica com a dele
            if (inv.MochilaVestida.Value == Item.Nada) inv.Receber(Item.MochilaPequena);
            inv.EntregarItem(Item.Machado, 1);
            inv.AvisarODono(inv.MochilaVestida.Value == Item.MochilaPequena ? "O Gus te deu um machado e uma mochila pequena." : "O Gus te deu um machado.");
            Debug.Log($"[Tutorial] O Gus deu o machado a {quem.Aparencia.Value.nome}.");
            m.Concluir(FaseDaCampanha.OBunker, PegueOMachado);
        }

        /// <summary>
        /// No anfitrião: quem abriu a conversa com o Gus o segura parado, olhando para ele, até fechar (no máximo 2 min).
        /// (25/11/2026, a revisão: o bug 78) Cada um que conversa conta: ele só volta ao posto quando a última tela fecha.
        /// </summary>
        public void ConversaComOGus(JogadorRede quem, bool comecou)
        {
            if (_gus == null || quem == null) return;
            if (comecou) _gus.ComecarAConversa(quem);
            else _gus.AcabarAConversa(quem);
        }

        // o metal e a fita largados pelo pátio, na frente do portão: uma peça e uma fita por jogador, e uma de sobra, mais
        // um pouco de sucata (a pedido: "pegar algumas sucatas no chão"). A primeira vez, no começo da submissão.
        // (25/11/2026, a revisão: o bug 97) Antes, uma vez só, pelo número de jogadores daquela hora: se 2 amigos entravam
        // depois, ou alguém saía da partida levando as peças, o pátio não tinha mais o que juntar e "Junte o metal" (ou "Faça
        // uma lança") travava. Agora, de 5 em 5 s, se o que o grupo tem (<paramref name="comEles"/>: os pares e as lanças) não
        // dá um por jogador, e com o que está no chão do pátio não dá um por jogador e um de sobra, os que faltam são largados
        // de novo (até 12 pares por partida, para o que some de outro jeito, numa estante ou num carro, não encher o pátio)
        void PorOMetalNoPatio(int jogadores, int comEles)
        {
            if (_metalNoPatio && (comEles >= jogadores || Time.time < _proximaReposicaoDoMetal)) return;
            var itens = ItensNoMundo.Instancia;
            var b = Chao != null ? Chao.Bunker : null;
            if (itens == null || !itens.IsSpawned || b == null) return;
            _proximaReposicaoDoMetal = Time.time + 5f;
            // (um save do meio da submissão já traz os do chão: eles contam, e não são largados de novo; a sucata também,
            // senão cada carregamento nessas duas submissões largava mais 2 montes)
            int noChao = ParesNoPatio(itens, b, out int sucatasNoChao);
            int falta = jogadores + 1 - comEles - noChao;
            if (!_metalNoPatio)
            {
                _metalNoPatio = true;
                int montes = Mathf.Max(0, falta), sucatas = Mathf.Max(0, SucatasDoPatio - sucatasNoChao);
                LargarNoPatio(itens, b, montes, sucatas, 5150);
                Debug.Log($"[Tutorial] O metal no pátio: {montes} peças de metal e {montes} fitas, e {sucatas} montes de sucata ({noChao} pares e {sucatasNoChao} montes de sucata já estavam no chão).");
                return;
            }
            falta = Mathf.Min(falta, MaximoDeParesNoPatio - _paresNoPatio);
            if (falta <= 0) return;
            LargarNoPatio(itens, b, falta, 0, 5150 + 7919 * _paresNoPatio);
            Debug.Log($"[Tutorial] Faltava metal para todos ({jogadores} jogadores, {comEles} pares com eles, {noChao} no chão): mais {falta} peças de metal e {falta} fitas no pátio.");
        }

        // (25/11/2026, a revisão: o bug 97) quantos pares foram largados nesta partida, e quando conferir de novo
        int _paresNoPatio;
        float _proximaReposicaoDoMetal;
        const int MaximoDeParesNoPatio = 12;
        /// <summary>Os montes de sucata largados com o metal (2 sucatas cada).</summary>
        const int SucatasDoPatio = 2;
        /// <summary>Até onde (m) do meio do pátio, na frente do portão, o metal e a fita no chão contam (eles caem de 6 a 16 m do portão).</summary>
        const float RaioDoPatio = 30f;

        static float MeioDoPortaoX => PlantaDeBunker.PortaoX0 + PlantaDeBunker.PortaoLargura * 0.5f;

        // os pares de metal e fita no chão do pátio (o que der menos dos dois), e os montes de sucata
        int ParesNoPatio(ItensNoMundo itens, LugarDoBunker b, out int sucatas)
        {
            var meioNoMapa = b.NoMapa(MeioDoPortaoX, PlantaDeBunker.Z0 - 10f);
            var meio = OrigemDeLakewood.NoMundo(new Vector3(meioNoMapa.x, b.Altura, meioNoMapa.y));
            int metal = 0, fita = 0;
            sucatas = 0;
            foreach (var it in itens.Copia())
            {
                if (it.item != Item.PecasDeMetal && it.item != Item.FitaAdesiva && it.item != Item.Sucata) continue;
                if (Horizontal(it.apoio - meio) > RaioDoPatio) continue;
                if (it.item == Item.Sucata)
                {
                    sucatas++;
                    continue;
                }
                int n = Itens.Quantos(it.item, it.qtd);
                if (it.item == Item.PecasDeMetal) metal += n;
                else fita += n;
            }
            return Mathf.Min(metal, fita);
        }

        // larga os pares (uma peça de metal e uma fita lado a lado) e as sucatas em leque na frente do portão, de 6 a 16 m dele
        void LargarNoPatio(ItensNoMundo itens, LugarDoBunker b, int pares, int sucatas, int semente)
        {
            var rnd = new System.Random(semente);
            float meioX = MeioDoPortaoX;
            int lugares = pares + sucatas;
            for (int i = 0; i < lugares; i++)
            {
                float ang = Mathf.Lerp(-70f, 70f, (i + 0.5f) / lugares) * Mathf.Deg2Rad, dist = 6f + (float)rnd.NextDouble() * 10f;
                var noMapa = b.NoMapa(meioX + Mathf.Sin(ang) * dist, PlantaDeBunker.Z0 - Mathf.Cos(ang) * dist);
                var p = OrigemDeLakewood.NoMundo(new Vector3(noMapa.x, b.Altura, noMapa.y));
                if (Physics.Raycast(p + Vector3.up * 4f, Vector3.down, out var chao, 10f, 1, QueryTriggerInteraction.Ignore)) p = chao.point;
                float giro = (float)rnd.NextDouble() * 360f;
                var lado = Quaternion.Euler(0f, giro, 0f) * Vector3.right * 0.35f;
                if (i < pares)
                {
                    itens.Criar(Item.PecasDeMetal, p, giro, 1);
                    itens.Criar(Item.FitaAdesiva, p + lado, giro + 40f, 1);
                }
                else itens.Criar(Item.Sucata, p, giro, 2);
            }
            _paresNoPatio += pares;
        }

        // ------------------------------------------------------------------ em cada máquina

        // a conversa no Gus (ele é achado pela identidade)
        void PorAConversaNoGus()
        {
            if (Time.frameCount % 30 != 15) return;
            if (_gusVisto != null && !_gusVisto.IsSpawned) _gusVisto = null;
            foreach (var s in FindObjectsByType<Sobrevivente>(FindObjectsSortMode.None))
            {
                if (s == null || !s.IsSpawned || s.Identidade.Value.grupo != GrupoDoGus) continue;
                _gusVisto = s;
                if (s.GetComponent<ConversaComNpc>() != null) continue;
                var conversa = s.gameObject.AddComponent<ConversaDoGus>();
                conversa.gus = s;
                ConversaComNpc.Por(s.gameObject, conversa);
            }
        }

        // o limite do tutorial: o jogador desta máquina que passa de 300 m do bunker volta para a beira (a pé, ou o carro,
        // se ele dirige), virado para o bunker
        void SegurarNoTutorial(MundoDeLakewood m)
        {
            if (m == null || m.Fase != FaseDaCampanha.OBunker || _antes || _palco != null) return;
            var b = Chao != null ? Chao.Bunker : null;
            var j = JogadorRede.Local;
            if (b == null || j == null || !j.Criado) return;
            var mov = j.GetComponent<MovimentoPrimeiraPessoa>();
            if (mov == null || mov.Voando) return;
            var noMapa = OrigemDeLakewood.NoMapa(j.transform.position);
            var meio = b.MeioDaPegada;
            var fora = new Vector2(noMapa.x, noMapa.z) - meio;
            if (fora.magnitude <= LimiteDoTutorial) return;
            var dentro = -fora.normalized;
            var beira = meio - dentro * (LimiteDoTutorial - 2f);
            var mundo = OrigemDeLakewood.NoMundo(new Vector3(beira.x, Chao.Altura(beira.x, beira.y), beira.y));
            float giro = Mathf.Atan2(dentro.x, dentro.y) * Mathf.Rad2Deg;
            var pass = j.Passageiro;
            if (pass != null && pass.NoCarro && pass.Carro != null)
            {
                var carro = pass.Carro;
                if (pass.Lugar == 0 && carro.IsOwner)
                {
                    var corpo = carro.GetComponent<Rigidbody>();
                    var pos = mundo + Vector3.up * 0.6f;
                    var rot = Quaternion.Euler(0f, giro, 0f);
                    if (corpo != null)
                    {
                        corpo.linearVelocity = Vector3.zero;
                        corpo.angularVelocity = Vector3.zero;
                        corpo.position = pos;
                        corpo.rotation = rot;
                    }
                    carro.transform.SetPositionAndRotation(pos, rot);
                }
            }
            else mov.Teleportar(mundo + Vector3.up * 0.3f, giro);
            if (Time.time > _avisoDoLimiteAte)
            {
                _avisoDoLimiteAte = Time.time + 4f;
                InventarioJogador.AvisoLocal("O Gus ainda não terminou de explicar: fiquem perto do bunker.");
            }
        }
    }
}
