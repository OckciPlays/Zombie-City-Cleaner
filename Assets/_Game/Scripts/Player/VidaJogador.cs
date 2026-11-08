using Unity.Netcode;
using UnityEngine;
using ZombieCityCleaner.Combat;
using ZombieCityCleaner.Network;
using ZombieCityCleaner.UI;
using ZombieCityCleaner.Video;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// A vida do jogador. Só o anfitrião muda (os golpes dos zumbis, os tiros, e a fome e a sede quando acabam). O dono vê
    /// a barra, o clarão vermelho e o tranco de cada golpe.
    /// Se a vida acaba (03/11/2026, a pedido: "uma tela de morte, com o botão de renascer depois de uns 5 segundos"), ele
    /// fica <see cref="Caido"/>: o corpo deitado para os outros, os zumbis e os sobreviventes largam dele, e o dono vê a
    /// <see cref="TelaDeMorte"/> (do que morreu e a contagem). A mochila e as roupas vestidas ficam no chão onde ele caiu
    /// (no Dia 0, nada se perde). O botão pede ao anfitrião, que leva o jogador à cama dele (ou ao ponto de nascimento) com a
    /// vida cheia.
    /// Desde 05/11/2026 (a pedido: "a vida não se regenera mais sozinha"), só o tratamento cura: a bandagem, o kit médico
    /// e a tala tratam o corpo por partes (<see cref="Corpo"/>, no VidaJogador.Corpo.cs: as feridas que sangram, as
    /// fraturas, a queda). A fogueira e as comidas não dão mais vida.
    /// A vida geral sai das partes (06/11/2026, a pedido: "se todos os membros estiverem 100% a minha vida geral também
    /// estará 100%"): é a máxima menos tudo o que as partes perderam. O golpe tira da parte que pegou; o sangramento, da
    /// parte que sangra; a fome, a sede, a infecção e o raio, do tronco. O que a lesão tira a mais do membro fica só nele.
    /// </summary>
    public partial class VidaJogador : NetworkBehaviour
    {
        public float vidaMaxima = 100f;
        [Tooltip("Caído, quanto tempo até o botão de renascer acender (s).")]
        public float esperaParaRenascer = 5f;

        public readonly NetworkVariable<float> Vida = new(100f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        /// <summary>A vida acabou: ele está caído, esperando renascer (do anfitrião).</summary>
        public readonly NetworkVariable<bool> Caido = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        /// <summary>No dono, quando ele cai (a vida acabou): a trilha toca a morte (Music/MaestroDaTrilha).</summary>
        public static event System.Action Caiu;

        /// <summary>No dono: do que ele morreu (para a tela de morte).</summary>
        public string Causa { get; private set; } = "";
        /// <summary>No dono: quando o botão de renascer acende (Time.time).</summary>
        public float PodeRenascerEm { get; private set; }

        static AudioClip _somDoGolpe;
        float _ultimoGolpe, _clarao, _caiuNoServidor;
        string _ultimaCausa;
        FomeESede _fomeESede;
        JogadorRede _rede;
        // o corpo deitado (em todas as máquinas)
        bool _deitado;
        Quaternion _giroDoCorpo;

        public override void OnNetworkSpawn()
        {
            _fomeESede = GetComponent<FomeESede>();
            _rede = GetComponent<JogadorRede>();
            if (IsServer)
            {
                Vida.Value = vidaMaxima;
                Caido.Value = false;
            }
            NascerCorpo();
            if (_somDoGolpe == null) _somDoGolpe = GerarSom();
            if (IsOwner && GetComponent<TelaDeMorte>() == null) gameObject.AddComponent<TelaDeMorte>();
            // o corpo (05/11/2026): no dono, a queda, os efeitos das lesões, o curativo com a barra e a silhueta no HUD; em
            // todas as máquinas, a faixa e a tala no corpo
            if (IsOwner)
            {
                if (GetComponent<QuedaDoJogador>() == null) gameObject.AddComponent<QuedaDoJogador>();
                if (GetComponent<EfeitosDoCorpo>() == null) gameObject.AddComponent<EfeitosDoCorpo>();
                if (GetComponent<Tratamento>() == null) gameObject.AddComponent<Tratamento>();
                if (GetComponent<CorpoNoHud>() == null) gameObject.AddComponent<CorpoNoHud>();
                if (GetComponent<TratarOAmigo>() == null) gameObject.AddComponent<TratarOAmigo>();   // a fase 3: mirar no amigo e segurar E
                // a mente (06/11/2026): o monitor cardíaco no lugar da barra da vida e o cérebro da sanidade
                if (GetComponent<MonitorCardiaco>() == null) gameObject.AddComponent<MonitorCardiaco>();
                if (GetComponent<MenteNoHud>() == null) gameObject.AddComponent<MenteNoHud>();
                // a etapa 2 da mente: a crise de pânico, as alucinações e o colapso (a fuga e o desmaio)
                if (GetComponent<CriseDePanico>() == null) gameObject.AddComponent<CriseDePanico>();
                if (GetComponent<Alucinacoes>() == null) gameObject.AddComponent<Alucinacoes>();
                if (GetComponent<ColapsoDoJogador>() == null) gameObject.AddComponent<ColapsoDoJogador>();
                // a árvore de habilidades (06/11/2026): o "+3 XP" e o nível novo embaixo da mira
                if (GetComponent<XpNaTela>() == null) gameObject.AddComponent<XpNaTela>();
                if (GetComponent<FaroNaTela>() == null) gameObject.AddComponent<FaroNaTela>();   // (o Faro, a ponta do Sortudo)
                if (GetComponent<AguaNaTela>() == null) gameObject.AddComponent<AguaNaTela>();   // (a água: embaixo dela e o fôlego)
                if (GetComponent<PescaJogador>() == null) gameObject.AddComponent<PescaJogador>();   // (a pesca)
            }
            if (GetComponent<CurativosNoCorpo>() == null) gameObject.AddComponent<CurativosNoCorpo>();
            if (GetComponent<PassageiroDoCaiaque>() == null) gameObject.AddComponent<PassageiroDoCaiaque>();   // (o caiaque, 06/11/2026)
            if (GetComponent<NadoNoCorpo>() == null) gameObject.AddComponent<NadoNoCorpo>();   // (o nado visto e ouvido, 07/11/2026)
            if (GetComponent<PescaVista>() == null) gameObject.AddComponent<PescaVista>();   // (a boia e a linha dos outros, 07/11/2026)
            if (GetComponent<PoseDoCurativo>() == null) gameObject.AddComponent<PoseDoCurativo>();   // a fase 3: o corpo enfaixando a parte
        }

        public override void OnNetworkDespawn()
        {
            SumirCorpo();
            PararDeOuvirOsRaios();
        }

        /// <summary>
        /// Só no anfitrião: um golpe (ou um tiro) tira vida. Se acabar, o jogador cai. A causa vai para a tela de morte
        /// ("Mordido pelos zumbis", "Morto por Fulano"...). Com o jeito do golpe (05/11/2026), ele também pega numa parte do
        /// corpo (a <paramref name="parte"/>, ou sorteada pelo jeito e pelo lado de quem bate), que perde vida, e pode abrir
        /// uma ferida que sangra e quebrar ou torcer o membro (<paramref name="lesao"/>: a queda e o Brutamonte). Sem o
        /// jeito (Nenhum), e no Dia 0, o golpe vai para o tronco (o raio, o F1).
        /// </summary>
        public void Dano(float valor, Vector3 de, string causa = null, TipoDeGolpe tipo = TipoDeGolpe.Nenhum,
                         ParteDoCorpo parte = PartesDoCorpo.Sortear, Lesao lesao = Lesao.Nenhuma)
        {
            if (!IsServer || !IsSpawned || Caido.Value) return;
            _ultimoGolpe = Time.time;
            _ultimaCausa = causa;
            bool abriu = false;
            var nova = Lesao.Nenhuma;
            // no Dia 0 (e nos 5 anos depois), o corpo não guarda nada: só a vida geral
            if (tipo != TipoDeGolpe.Nenhum && CorpoAnda) Ferir(valor, de, tipo, ref parte, lesao, out abriu, out nova);
            else parte = PartesDoCorpo.Sortear;
            MenteNoGolpe(nova);   // a mente (06/11/2026): o golpe abala um pouco, a fratura mais
            AcordarDoSono(true);   // (e acorda quem dormia)
            // a vida geral sai das partes (06/11/2026): o Ferir já pôs o golpe na parte; sem ela, ele vai para o tronco
            if (PartesDoCorpo.Valida(parte)) ConferirAVida(causa);
            else Perder(valor, causa);
            GolpeRpc(de, (byte)parte, (byte)tipo, (byte)nova, abriu);
        }

        /// <summary>
        /// Só no anfitrião: devolve vida geral, até a máxima. Como ela sai das partes (06/11/2026), o machucado delas diminui
        /// (a mais machucada primeiro).
        /// </summary>
        public void Curar(float valor)
        {
            if (!IsServer || !IsSpawned || valor <= 0f || Caido.Value) return;
            CurarAsPartes(EmPorcento(valor));
            ConferirAVida(null);
        }

        /// <summary>
        /// Só no anfitrião: tira vida sem golpe (a fome, a sede, a infecção, o raio). Ela sai do tronco (06/11/2026: a vida
        /// geral é o que as partes perderam). Se acabar, o jogador cai.
        /// </summary>
        public void Perder(float valor, string causa = null)
        {
            if (!IsServer || !IsSpawned || Caido.Value || valor <= 0f) return;
            PorNaParte(ParteDoCorpo.Tronco, EmPorcento(valor));
            ConferirAVida(causa);
        }

        // no anfitrião: a vida geral é a que as partes deixam; se elas perderam tudo, ele cai
        void ConferirAVida(string causa)
        {
            if (Caido.Value) return;
            float nova = VidaDasPartes();
            if (nova > 0.001f)
            {
                if (Vida.Value != nova) Vida.Value = nova;
                return;
            }
            Vida.Value = 0f;
            Caido.Value = true;
            _caiuNoServidor = Time.time;
            MenteDeQuemViuCair();   // a mente (06/11/2026): quem está perto vê o amigo cair
            if (_fomeESede != null) _fomeESede.AoCair();
            Vehicles.Carro.TirarDeTodos(OwnerClientId);   // caiu dentro de um carro: o lugar fica vazio
            Vehicles.Caiaque.TirarDeTodos(OwnerClientId);   // (e no caiaque)
            // a mochila, com tudo dentro, e as roupas vestidas ficam onde ele caiu (02/11 e 03/11/2026, a pedido); no Dia 0
            // não se perde nada
            var mundo = World.MundoDaPartida.Instancia;
            bool diaZero = mundo != null && mundo.NoDiaZero;
            if (!diaZero) Loot.MochilasNoChao.Instancia?.DaMorte(GetComponent<InventarioJogador>());
            MorreuRpc(causa ?? _ultimaCausa ?? "Mordido pelos zumbis.", diaZero);
        }

        [Rpc(SendTo.Owner)]
        void GolpeRpc(Vector3 de, byte parte, byte tipo, byte lesao, bool abriu)
        {
            _clarao = 1f;
            UltimoGolpeNoDono = Time.time;
            // a queda já teve o tranco e o baque no dono (QuedaDoJogador): aqui, só o osso
            if ((TipoDeGolpe)tipo == TipoDeGolpe.Queda)
            {
                if (lesao >= (byte)Lesao.Fratura) GetComponent<MovimentoPrimeiraPessoa>().Coice(-3f);
            }
            else
            {
                Efeitos.Som(_somDoGolpe, transform.position + Vector3.up * 1.5f, 0.9f, false);
                GetComponent<MovimentoPrimeiraPessoa>().Coice(lesao >= (byte)Lesao.Fratura ? -6f : -2.5f);   // o golpe sacode a cabeça para baixo
            }
            AvisarDoGolpe((ParteDoCorpo)parte, (TipoDeGolpe)tipo, (Lesao)lesao, abriu);
        }

        // anfitrião → dono: a tela de morte (a causa, e se perdeu as coisas)
        [Rpc(SendTo.Owner)]
        void MorreuRpc(string causa, bool diaZero)
        {
            Causa = causa;
            PerdeuAsCoisas = !diaZero;
            PodeRenascerEm = Time.time + esperaParaRenascer;
            GetComponent<PassageiroDoCarro>().Soltar();
            Caiu?.Invoke();
        }

        /// <summary>No dono: a mochila e as roupas ficaram no chão (fora do Dia 0).</summary>
        public bool PerdeuAsCoisas { get; private set; }

        /// <summary>No dono, o botão da tela de morte: pede ao anfitrião para renascer.</summary>
        public void PedirRenascer()
        {
            if (!IsOwner || !Caido.Value || Time.time < PodeRenascerEm) return;
            RenascerRpc();
        }

        [Rpc(SendTo.Server)]
        void RenascerRpc(RpcParams parametros = default)
        {
            if (parametros.Receive.SenderClientId != OwnerClientId || !Caido.Value) return;
            if (Time.time - _caiuNoServidor < esperaParaRenascer - 0.5f) return;
            Vida.Value = vidaMaxima;
            SararTudo();   // a morte não muda (05/11/2026): renasce com tudo inteiro
            MenteAoVoltar();   // (a mente fica como estava, mas não volta em colapso)
            Caido.Value = false;
            _ultimaCausa = null;
            VoltarRpc();
        }

        /// <summary>No anfitrião (os testes): renasce na hora, sem esperar o botão.</summary>
        public void RenascerAgora()
        {
            if (!IsServer || !Caido.Value) return;
            Vida.Value = vidaMaxima;
            SararTudo();
            MenteAoVoltar();
            Caido.Value = false;
            _ultimaCausa = null;
            VoltarRpc();
        }

        // anfitrião → dono: renasce na cama dele (a última que montou), ou no ponto de nascimento
        [Rpc(SendTo.Owner)]
        void VoltarRpc()
        {
            var rede = GetComponent<JogadorRede>();
            ulong dono = rede != null && rede.Chave.Value != 0 ? rede.Chave.Value : OwnerClientId;
            var ponto = Loot.ConstrucoesNoMundo.CamaDe(dono, out var cama) ? cama : PontosDeNascimento.Ponto(OwnerClientId);
            GetComponent<PassageiroDoCarro>().Soltar();
            GetComponent<MovimentoPrimeiraPessoa>().Teleportar(ponto.position, ponto.rotation.eulerAngles.y);
        }

        void Update()
        {
            if (!IsSpawned) return;
            _clarao = Mathf.Max(0f, _clarao - Time.deltaTime * 2.5f);
            // (06/11/2026: o desmaio também deita o corpo)
            bool deitado = Caido.Value || Desmaiado.Value || Dormindo.Value;
            Deitar(deitado);
            // (a luneta, saindo dela, liga os braços de novo: caído, continuam apagados)
            if (deitado && IsOwner && _rede != null && _rede.cameraBracos != null && _rede.cameraBracos.enabled) _rede.cameraBracos.enabled = false;
            // a vida não volta mais sozinha (05/11/2026): o corpo anda (o sangramento, os curativos, as lesões sarando)
            if (IsServer)
            {
                AtualizarCorpo(Time.deltaTime);
                AtualizarMente(Time.deltaTime);   // a mente (06/11/2026): o medo e o alívio de cada momento
                AtualizarDesmaio();
                AtualizarSono();   // a etapa 3 da mente: dormir, acordar e a noite passando
                AtualizarFolego(Time.deltaTime);   // a água (06/11/2026): o fôlego e o afogamento
                AtualizarEnjoo(Time.deltaTime);   // o peixe cru que caiu mal (07/11/2026)
            }
        }

        /// <summary>No dono: quando foi o último golpe (o coração dispara no monitor).</summary>
        public float UltimoGolpeNoDono { get; private set; } = -99f;

        // o corpo (o que os outros veem, e a sombra do dono) deita de costas no chão enquanto ele está caído
        void Deitar(bool caido)
        {
            if (caido == _deitado || _rede == null || _rede.animadorCorpo == null) return;
            _deitado = caido;
            var corpo = _rede.animadorCorpo.transform;
            if (caido)
            {
                _giroDoCorpo = corpo.localRotation;
                corpo.localRotation = Quaternion.Euler(-90f, 0f, 0f) * _giroDoCorpo;
            }
            else corpo.localRotation = _giroDoCorpo;
            _rede.animadorCorpo.speed = caido ? 0f : 1f;
            // no dono, os braços da primeira pessoa somem atrás da tela de morte
            if (IsOwner && _rede.cameraBracos != null) _rede.cameraBracos.enabled = !caido;
        }

        void OnGUI()
        {
            if (!IsSpawned || !IsOwner || MenuOnline.Aberto || MenuOnline.Cinema || ModoVideo.Ligado || Caido.Value) return;
            float u = Screen.height / 720f;
            float fracao = Mathf.Clamp01(Vida.Value / vidaMaxima);
            // golpe: a tela pisca vermelho; com pouca vida, fica um vermelho fraco
            float vermelho = Mathf.Max(_clarao * 0.35f, (1f - fracao) * 0.18f);
            if (vermelho > 0.01f) Retangulo(0, 0, Screen.width, Screen.height, new Color(0.55f, 0f, 0f, vermelho));
            // (a barra da vida virou o monitor cardíaco, 05/11/2026: o MonitorCardiaco, com a cor pela vida)
        }

        static void Retangulo(float x, float y, float w, float h, Color c)
        {
            var antes = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);
            GUI.color = antes;
        }

        // um baque surdo, gerado por código como os outros sons
        static AudioClip GerarSom()
        {
            const int taxa = 22050;
            int n = taxa / 3;
            var dados = new float[n];
            var r = new System.Random(7);
            float filtro = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)taxa;
                filtro = Mathf.Lerp(filtro, (float)(r.NextDouble() * 2 - 1), 0.12f);
                dados[i] = (filtro * 1.6f + Mathf.Sin(t * 2f * Mathf.PI * 85f) * 0.5f) * Mathf.Exp(-t * 14f);
            }
            return SomGerado.Clip("Golpe", dados, taxa);
        }
    }
}
