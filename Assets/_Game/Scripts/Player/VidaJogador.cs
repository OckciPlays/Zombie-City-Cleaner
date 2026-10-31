using Unity.Netcode;
using UnityEngine;
using ZombieCityCleaner.Combat;
using ZombieCityCleaner.Network;
using ZombieCityCleaner.UI;
using ZombieCityCleaner.Video;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// A vida do jogador. Só o anfitrião muda (os golpes dos zumbis, e a fome e a sede quando acabam). O dono vê a barra,
    /// o clarão vermelho e o tranco de cada golpe; se a vida acaba, ele cai e volta na frente da casa da família com a
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

        public readonly NetworkVariable<float> Vida = new(100f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        /// <summary>No dono, quando ele cai (a vida acabou): a trilha toca a morte (Music/MaestroDaTrilha).</summary>
        public static event System.Action Caiu;

        static AudioClip _somDoGolpe;
        float _ultimoGolpe, _proximaRecuperacao, _clarao, _caiuAte;
        FomeESede _fomeESede;

        public override void OnNetworkSpawn()
        {
            _fomeESede = GetComponent<FomeESede>();
            if (IsServer) Vida.Value = vidaMaxima;
            if (_somDoGolpe == null) _somDoGolpe = GerarSom();
        }

        /// <summary>Só no anfitrião: um golpe tira vida. Se acabar, o jogador cai e volta para casa.</summary>
        public void Dano(float valor, Vector3 de)
        {
            if (!IsServer || !IsSpawned) return;
            _ultimoGolpe = Time.time;
            Perder(valor);
            GolpeRpc(de);
        }

        /// <summary>Só no anfitrião: um remédio (ou a comida que cura) devolve vida, até a máxima.</summary>
        public void Curar(float valor)
        {
            if (!IsServer || !IsSpawned || valor <= 0f) return;
            Vida.Value = Mathf.Min(vidaMaxima, Vida.Value + valor);
        }

        /// <summary>Só no anfitrião: tira vida sem golpe (a fome e a sede). Se acabar, o jogador cai e volta para casa.</summary>
        public void Perder(float valor)
        {
            if (!IsServer || !IsSpawned) return;
            float nova = Vida.Value - valor;
            if (nova <= 0f)
            {
                Vida.Value = vidaMaxima;
                if (_fomeESede != null) _fomeESede.AoCair();
                Vehicles.Carro.TirarDeTodos(OwnerClientId);   // caiu dentro de um carro: o lugar fica vazio
                CaiuRpc();
            }
            else Vida.Value = nova;
        }

        [Rpc(SendTo.Owner)]
        void GolpeRpc(Vector3 de)
        {
            _clarao = 1f;
            Efeitos.Som(_somDoGolpe, transform.position + Vector3.up * 1.5f, 0.9f, false);
            GetComponent<MovimentoPrimeiraPessoa>().Coice(-2.5f);   // o golpe sacode a cabeça para baixo
        }

        [Rpc(SendTo.Owner)]
        void CaiuRpc()
        {
            // a cama dele (a última que montou), ou o ponto de nascimento
            var ponto = Loot.ConstrucoesNoMundo.CamaDe(OwnerClientId, out var cama) ? cama : PontosDeNascimento.Ponto(OwnerClientId);
            GetComponent<PassageiroDoCarro>().Soltar();
            GetComponent<MovimentoPrimeiraPessoa>().Teleportar(ponto.position, ponto.rotation.eulerAngles.y);
            _caiuAte = Time.time + 3f;
            Caiu?.Invoke();
        }

        void Update()
        {
            if (!IsSpawned) return;
            _clarao = Mathf.Max(0f, _clarao - Time.deltaTime * 2.5f);
            if (IsServer && Vida.Value < vidaMaxima && Time.time - _ultimoGolpe > esperaParaRecuperar && Time.time >= _proximaRecuperacao
                && (_fomeESede == null || _fomeESede.Bem))
            {
                _proximaRecuperacao = Time.time + 1f;
                Vida.Value = Mathf.Min(vidaMaxima, Vida.Value + recuperacao);
            }
        }

        void OnGUI()
        {
            if (!IsSpawned || !IsOwner || MenuOnline.Aberto || ModoVideo.Ligado) return;
            float u = Screen.height / 720f;
            float fracao = Mathf.Clamp01(Vida.Value / vidaMaxima);
            // golpe: a tela pisca vermelho; com pouca vida, fica um vermelho fraco
            float vermelho = Mathf.Max(_clarao * 0.35f, (1f - fracao) * 0.18f);
            if (vermelho > 0.01f) Retangulo(0, 0, Screen.width, Screen.height, new Color(0.55f, 0f, 0f, vermelho));
            float x = 20 * u, y = Screen.height - 44 * u, w = 200 * u, h = 10 * u;
            var estilo = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(14 * u), fontStyle = FontStyle.Bold };
            estilo.normal.textColor = new Color(0.9f, 0.88f, 0.8f);
            GUI.Label(new Rect(x, y - 22 * u, w, 20 * u), "VIDA", estilo);
            Retangulo(x, y, w, h, new Color(0f, 0f, 0f, 0.5f));
            Retangulo(x, y, w * fracao, h, fracao > 0.35f ? new Color(0.78f, 0.74f, 0.62f, 0.9f) : new Color(0.85f, 0.25f, 0.2f, 0.95f));
            if (Time.time < _caiuAte)
            {
                var aviso = new GUIStyle(estilo) { fontSize = Mathf.RoundToInt(30 * u), alignment = TextAnchor.MiddleCenter };
                aviso.normal.textColor = new Color(0.85f, 0.3f, 0.25f);
                GUI.Label(new Rect(0, Screen.height * 0.4f, Screen.width, 50 * u), "VOCÊ CAIU", aviso);
            }
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
