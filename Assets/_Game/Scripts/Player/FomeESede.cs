using Unity.Netcode;
using UnityEngine;
using ZombieCityCleaner.Loot;
using ZombieCityCleaner.UI;
using ZombieCityCleaner.Video;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// A fome e a sede do jogador: duas barras de 0 a 100 (cheia = satisfeito) que esvaziam com o tempo, mais rápido
    /// correndo. A sede acaba antes: cheia até vazia em 25 minutos (pouco mais de um dia do jogo); a fome, em 40. Com uma
    /// delas abaixo de 25 a vida para de voltar sozinha; vazia, ela tira vida aos poucos, e o jogador pode cair (volta
    /// para casa com as duas pelo menos na metade). A lata de comida e a garrafa de água enchem as barras
    /// (<see cref="InventarioJogador.Consumir"/>).
    /// Quem conta é o anfitrião (a rede recebe o valor uma vez por segundo); o dono vê as barras em cima da vida.
    /// </summary>
    public class FomeESede : NetworkBehaviour
    {
        [Tooltip("Minutos de cheia até vazia (parado ou andando).")]
        public float minutosDaFome = 40f;
        public float minutosDaSede = 25f;
        [Tooltip("Correndo, gasta esta vezes mais.")]
        public float correndo = 1.6f;
        [Tooltip("Como as barras começam a partida (de 0 a 100): a família saiu do bunker com pouca coisa.")]
        public float fomeInicial = 80f;
        public float sedeInicial = 70f;
        [Tooltip("Abaixo disso a vida não volta sozinha, e a barra fica vermelha.")]
        public float pouca = 25f;
        [Tooltip("Vida perdida por segundo com a barra vazia.")]
        public float danoDaFome = 0.25f;
        public float danoDaSede = 0.4f;
        [Tooltip("Depois de cair, as barras voltam pelo menos a isto.")]
        public float depoisDeCair = 50f;

        public readonly NetworkVariable<float> Fome = new(100f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public readonly NetworkVariable<float> Sede = new(100f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        /// <summary>Nem com fome nem com sede: a vida pode voltar sozinha.</summary>
        public bool Bem => Fome.Value >= pouca && Sede.Value >= pouca;

        JogadorRede _rede;
        VidaJogador _vida;
        float _fome, _sede, _dano, _proximaGravacao;

        public override void OnNetworkSpawn()
        {
            _rede = GetComponent<JogadorRede>();
            _vida = GetComponent<VidaJogador>();
            if (!IsServer) return;
            _fome = fomeInicial;
            _sede = sedeInicial;
            Gravar();
        }

        void Update()
        {
            if (!IsSpawned || !IsServer) return;
            // no Dia 0 e nos 5 anos depois (o mundo ainda não está pronto), a fome e a sede não descem: a vida é normal
            var mundo = World.MundoDaPartida.Instancia;
            if (mundo != null && !mundo.Pronto) return;
            float dt = Time.deltaTime * (_rede.Velocidade > 3f ? correndo : 1f);
            _fome = Mathf.Max(0f, _fome - 100f / (minutosDaFome * 60f) * dt);
            _sede = Mathf.Max(0f, _sede - 100f / (minutosDaSede * 60f) * dt);
            if (_fome <= 0f) _dano += danoDaFome * Time.deltaTime;
            if (_sede <= 0f) _dano += danoDaSede * Time.deltaTime;
            if (_dano >= 1f)
            {
                float d = Mathf.Floor(_dano);
                _dano -= d;
                _vida.Perder(d);
            }
            if (Time.time >= _proximaGravacao) Gravar();
        }

        void Gravar()
        {
            _proximaGravacao = Time.time + 1f;
            Fome.Value = _fome;
            Sede.Value = _sede;
        }

        /// <summary>Só no anfitrião: o jogador comeu ou bebeu.</summary>
        public void Consumir(DadosDoLoot d)
        {
            if (!IsServer) return;
            _fome = Mathf.Clamp(_fome + d.fome, 0f, 100f);
            _sede = Mathf.Clamp(_sede + d.sede, 0f, 100f);
            Gravar();
        }

        /// <summary>Só no anfitrião: o jogador caiu (a vida acabou) e volta para casa.</summary>
        public void AoCair()
        {
            _fome = Mathf.Max(_fome, depoisDeCair);
            _sede = Mathf.Max(_sede, depoisDeCair);
            _dano = 0f;
            Gravar();
        }

        void OnGUI()
        {
            if (!IsSpawned || !IsOwner || MenuOnline.Aberto || MenuOnline.Cinema || ModoVideo.Ligado) return;
            float u = Screen.height / 720f;
            // em cima da barra da vida (VidaJogador): a sede logo acima, a fome acima dela
            float yVida = Screen.height - 44 * u;
            Barra(u, yVida - 72 * u, "FOME", Fome.Value, new Color(0.78f, 0.6f, 0.3f, 0.9f), "com fome");
            Barra(u, yVida - 38 * u, "SEDE", Sede.Value, new Color(0.38f, 0.58f, 0.74f, 0.9f), "com sede");
        }

        void Barra(float u, float y, string nome, float valor, Color cor, string aviso)
        {
            float x = 20 * u, w = 150 * u, h = 7 * u;
            float fracao = Mathf.Clamp01(valor / 100f);
            bool baixa = valor < pouca;
            var estilo = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(13 * u), fontStyle = FontStyle.Bold };
            estilo.normal.textColor = baixa ? new Color(0.95f, 0.42f, 0.36f) : new Color(0.86f, 0.84f, 0.76f);
            GUI.Label(new Rect(x, y - 19 * u, 260 * u, 18 * u), baixa ? $"{nome}  ·  {aviso}" : nome, estilo);
            // com a barra vazia, o fundo pisca vermelho (está tirando vida)
            float pisca = valor <= 0f ? 0.25f + 0.25f * Mathf.Sin(Time.time * 6f) : 0f;
            Retangulo(x, y, w, h, Color.Lerp(new Color(0f, 0f, 0f, 0.5f), new Color(0.8f, 0.15f, 0.1f, 0.8f), pisca));
            Retangulo(x, y, w * fracao, h, baixa ? new Color(0.85f, 0.28f, 0.22f, 0.95f) : cor);
        }

        static void Retangulo(float x, float y, float w, float h, Color c)
        {
            var antes = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);
            GUI.color = antes;
        }
    }
}
