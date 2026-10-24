using UnityEngine;
using ZombieCityCleaner.Player;

namespace ZombieCityCleaner.World
{
    /// <summary>
    /// A folha da porta blindada do bunker: gira em volta das dobradiças (a origem do objeto) até <see cref="abertura"/>
    /// graus, para dentro do salão. É pesada: para abrir, o volante gira e as trancas soltam antes de ela andar, e ela
    /// abre devagar no começo e no fim; para fechar, ela ganha velocidade até o batente. O estado vem do
    /// <see cref="BunkerRede"/>; E na porta pede para abrir ou fechar. Ela não faz som (os de abrir e de fechar saíram a
    /// pedido).
    /// </summary>
    public class PortaDoBunker : Interagivel
    {
        /// <summary>O volante girando e as trancas soltando, antes de a folha andar (s).</summary>
        public const float Destrancar = 0.75f;
        public const float TempoAbrir = 1.8f, TempoFechar = 1.5f;

        public BunkerRede bunker;
        [Tooltip("Quanto ela abre (graus, em Y).")]
        public float abertura = 100f;
        [Tooltip("A largura da folha (m): ninguém pode estar nesse raio, do lado do salão, para ela fechar.")]
        public float largura = 1.3f;

        // quanto ela está aberta (0 a 1); o movimento em curso vai de _de a _alvo, e _progresso vai de 0 a 1
        float _aberta, _de, _alvo, _progresso = 1f, _espera;

        /// <summary>A folha encostada no batente (parada, ou com o volante ainda girando para abrir): o bunker está isolado.</summary>
        public bool Fechada => _aberta <= 0f && (_progresso >= 1f || _espera > 0f);

        /// <summary>Vai para aberta ou fechada; agora = sem animação (no começo da partida).</summary>
        public void Ir(bool aberta, bool agora)
        {
            float alvo = aberta ? 1f : 0f;
            if (agora)
            {
                _aberta = _de = _alvo = alvo;
                _progresso = 1f;
                _espera = 0f;
                Pousar();
                return;
            }
            if (alvo == _alvo) return;
            // ela pode mudar de ideia no meio: o movimento novo parte de onde ela está
            _de = _aberta;
            _alvo = alvo;
            _progresso = 0f;
            _espera = 0f;
            // abrindo: o volante gira e as trancas soltam antes de ela andar
            if (aberta && _aberta <= 0f) _espera = Destrancar;
        }

        void Update()
        {
            if (_progresso >= 1f) return;
            if (_espera > 0f)
            {
                _espera -= Time.deltaTime;
                return;
            }
            bool abrindo = _alvo > _de;
            // um movimento parcial (ela mudou de ideia no meio) leva a parte proporcional do tempo
            float tempo = (abrindo ? TempoAbrir : TempoFechar) * Mathf.Max(0.3f, Mathf.Abs(_alvo - _de));
            _progresso = Mathf.Min(1f, _progresso + Time.deltaTime / tempo);
            float p = abrindo ? Mathf.SmoothStep(0f, 1f, _progresso) : _progresso * _progresso;
            _aberta = Mathf.Lerp(_de, _alvo, p);
            Pousar();
        }

        void Pousar() => transform.localRotation = Quaternion.Euler(0f, abertura * _aberta, 0f);

        /// <summary>No anfitrião: ninguém no caminho da folha, que fecha de dentro do salão até o batente.</summary>
        public bool CaminhoLivre()
        {
            var pai = transform.parent;
            foreach (var j in JogadorRede.Todos)
            {
                if (j == null) continue;
                // nas coordenadas da folha fechada: ela vai para -x, e o salão fica em +z
                Vector3 p = pai.InverseTransformPoint(j.transform.position) - transform.localPosition;
                if (p.z > -0.6f && p.x < 0.5f && new Vector2(p.x, p.z).magnitude < largura + 0.4f) return false;
            }
            return true;
        }

        public override string Dica(ArmaJogador quem) =>
            bunker == null ? null : bunker.Aberta ? "[E] Fechar a porta" : "[E] Abrir a porta";

        public override void Usar(ArmaJogador quem)
        {
            if (bunker != null) bunker.PedirPorta();
        }
    }
}
