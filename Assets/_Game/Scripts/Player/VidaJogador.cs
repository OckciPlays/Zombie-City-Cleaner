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
    /// Depois de um tempo sem apanhar a vida volta devagar, se o jogador não estiver com fome nem com sede
    /// (<see cref="FomeESede"/>).
    /// </summary>
    public class VidaJogador : NetworkBehaviour
    {
        public float vidaMaxima = 100f;
        [Tooltip("Vida recuperada por segundo, depois de um tempo sem apanhar.")]
        public float recuperacao = 2f;
        public float esperaParaRecuperar = 8f;
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
        float _ultimoGolpe, _proximaRecuperacao, _clarao, _caiuNoServidor;
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
            if (_somDoGolpe == null) _somDoGolpe = GerarSom();
            if (IsOwner && GetComponent<TelaDeMorte>() == null) gameObject.AddComponent<TelaDeMorte>();
        }

        /// <summary>
        /// Só no anfitrião: um golpe (ou um tiro) tira vida. Se acabar, o jogador cai. A causa vai para a tela de morte
        /// ("Mordido pelos zumbis", "Morto por Fulano"...).
        /// </summary>
        public void Dano(float valor, Vector3 de, string causa = null)
        {
            if (!IsServer || !IsSpawned || Caido.Value) return;
            _ultimoGolpe = Time.time;
            _ultimaCausa = causa;
            Perder(valor, causa);
            GolpeRpc(de);
        }

        /// <summary>Só no anfitrião: um remédio (ou a comida que cura) devolve vida, até a máxima.</summary>
        public void Curar(float valor)
        {
            if (!IsServer || !IsSpawned || valor <= 0f || Caido.Value) return;
            Vida.Value = Mathf.Min(vidaMaxima, Vida.Value + valor);
        }

        /// <summary>Só no anfitrião: tira vida sem golpe (a fome e a sede). Se acabar, o jogador cai.</summary>
        public void Perder(float valor, string causa = null)
        {
            if (!IsServer || !IsSpawned || Caido.Value) return;
            float nova = Vida.Value - valor;
            if (nova > 0f)
            {
                Vida.Value = nova;
                return;
            }
            Vida.Value = 0f;
            Caido.Value = true;
            _caiuNoServidor = Time.time;
            if (_fomeESede != null) _fomeESede.AoCair();
            Vehicles.Carro.TirarDeTodos(OwnerClientId);   // caiu dentro de um carro: o lugar fica vazio
            // a mochila, com tudo dentro, e as roupas vestidas ficam onde ele caiu (02/11 e 03/11/2026, a pedido); no Dia 0
            // não se perde nada
            var mundo = World.MundoDaPartida.Instancia;
            bool diaZero = mundo != null && mundo.NoDiaZero;
            if (!diaZero) Loot.MochilasNoChao.Instancia?.DaMorte(GetComponent<InventarioJogador>());
            MorreuRpc(causa ?? _ultimaCausa ?? "Mordido pelos zumbis.", diaZero);
        }

        [Rpc(SendTo.Owner)]
        void GolpeRpc(Vector3 de)
        {
            _clarao = 1f;
            Efeitos.Som(_somDoGolpe, transform.position + Vector3.up * 1.5f, 0.9f, false);
            GetComponent<MovimentoPrimeiraPessoa>().Coice(-2.5f);   // o golpe sacode a cabeça para baixo
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
            Caido.Value = false;
            _ultimaCausa = null;
            VoltarRpc();
        }

        /// <summary>No anfitrião (os testes): renasce na hora, sem esperar o botão.</summary>
        public void RenascerAgora()
        {
            if (!IsServer || !Caido.Value) return;
            Vida.Value = vidaMaxima;
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
            Deitar(Caido.Value);
            // (a luneta, saindo dela, liga os braços de novo: caído, continuam apagados)
            if (Caido.Value && IsOwner && _rede != null && _rede.cameraBracos != null && _rede.cameraBracos.enabled) _rede.cameraBracos.enabled = false;
            if (IsServer && !Caido.Value && Vida.Value < vidaMaxima && Time.time - _ultimoGolpe > esperaParaRecuperar && Time.time >= _proximaRecuperacao
                && (_fomeESede == null || _fomeESede.Bem))
            {
                _proximaRecuperacao = Time.time + 1f;
                Vida.Value = Mathf.Min(vidaMaxima, Vida.Value + recuperacao);
            }
        }

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
            float x = 20 * u, y = Screen.height - 44 * u, w = 200 * u, h = 10 * u;
            var estilo = EstilosDoHud.Rotulo(Mathf.RoundToInt(14 * u), FontStyle.Bold);
            estilo.normal.textColor = new Color(0.9f, 0.88f, 0.8f);
            GUI.Label(new Rect(x, y - 22 * u, w, 20 * u), "VIDA", estilo);
            Retangulo(x, y, w, h, new Color(0f, 0f, 0f, 0.5f));
            Retangulo(x, y, w * fracao, h, fracao > 0.35f ? new Color(0.78f, 0.74f, 0.62f, 0.9f) : new Color(0.85f, 0.25f, 0.2f, 0.95f));
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
