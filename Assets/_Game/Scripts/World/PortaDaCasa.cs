using UnityEngine;
using UnityEngine.AI;
using ZombieCityCleaner.Mundo;
using ZombieCityCleaner.Player;

namespace ZombieCityCleaner.World
{
    /// <summary>
    /// Uma porta que abre de uma casa do mundo (o Plano_Portas.md da pasta de arte): a folha (a origem na dobradiça, a folha
    /// ao longo do +X), com a colisão na camada das portas (<see cref="PortasDoMundo.Camada"/>: para o jogador e o tiro, fora
    /// do NavMesh) e, no anfitrião, o obstáculo do NavMesh quando fechada (o zumbi não passa). O E pede ao anfitrião
    /// (<see cref="PortasDoMundo.PedirPorta"/>): abrir, fechar, destrancar por dentro ou, trancada, tremer.
    /// </summary>
    public class PortaDaCasa : Interagivel
    {
        public PortasDaConstrucao construcao;
        public int indice;
        public ulong chave;
        public PortaDaObra dados;

        Quaternion _base;
        float _sinal, _giro, _alvo, _tremer;
        NavMeshObstacle _obstaculo;

        /// <summary>Fechada ou abrindo para fechar (o alvo da animação).</summary>
        public bool Aberta => _alvo > 1f;
        /// <summary>O meio da folha fechada (onde o som sai e de onde o anfitrião mede a distância), no mundo.</summary>
        public Vector3 Meio => transform.parent.TransformPoint(dados.dobradica + new Vector3(dados.fechada.x, 0f, dados.fechada.y) * (dados.largura / 2) + Vector3.up * 1f);

        /// <summary>Monta a folha no lugar ('giro' do começo, sem animação).</summary>
        public void Preparar(float giro, bool comObstaculo)
        {
            var f = new Vector3(dados.fechada.x, 0f, dados.fechada.y);
            var a = new Vector3(dados.abre.x, 0f, dados.abre.y);
            // o +X local é a folha fechada; o giro leva a ponta para o lado de 'abre' (na Unity, o giro em y positivo leva o
            // +X para o -Z)
            if (Vector3.Dot(Vector3.Cross(Vector3.up, a), f) > 0f) { _base = Quaternion.LookRotation(a); _sinal = -1f; }
            else { _base = Quaternion.LookRotation(-a); _sinal = 1f; }
            _giro = _alvo = giro;
            if (comObstaculo)
            {
                _obstaculo = gameObject.AddComponent<NavMeshObstacle>();
                _obstaculo.shape = NavMeshObstacleShape.Box;
                _obstaculo.center = new Vector3(dados.largura / 2, dados.altura / 2, 0f);
                _obstaculo.size = new Vector3(dados.largura + 0.1f, dados.altura, 0.5f);
                _obstaculo.carving = true;
                _obstaculo.carveOnlyStationary = true;
                _obstaculo.enabled = giro < 1f;
            }
            Pousar();
            enabled = false;
        }

        /// <summary>
        /// Aberta ou fechada (o estado da rede): aberta, no giro do começo enquanto ninguém mexeu nela ('doComeco': as de
        /// dentro bem abertas, as de fora entreabertas) e depois a 90°; 'ja': sem a animação (a casa acabou de aparecer).
        /// </summary>
        public void Definir(bool aberta, bool doComeco, bool ja = false)
        {
            float alvo = !aberta ? 0f : doComeco && dados.giro > 1f ? dados.giro : Mathf.Min(dados.maximo, 90f);
            if (Mathf.Approximately(alvo, _alvo) && !ja) return;
            _alvo = alvo;
            if (ja) _giro = alvo;
            // o obstáculo some na hora de abrir e volta quando ela termina de fechar
            if (_obstaculo != null && aberta) _obstaculo.enabled = false;
            enabled = true;
            if (ja) Update();
        }

        /// <summary>A tremida da trancada: a folha bate um pouco na tranca, para os dois lados.</summary>
        public void Tremer()
        {
            _tremer = DuracaoDaTremida;
            enabled = true;
        }

        public const float DuracaoDaTremida = 0.45f;
        const float Velocidade = 260f;   // graus por segundo

        void Update()
        {
            _giro = Mathf.MoveTowards(_giro, _alvo, Velocidade * Time.deltaTime);
            if (_tremer > 0f) _tremer = Mathf.Max(0f, _tremer - Time.deltaTime);
            Pousar();
            if (Mathf.Approximately(_giro, _alvo) && _tremer <= 0f)
            {
                if (_obstaculo != null) _obstaculo.enabled = _alvo < 1f;
                enabled = false;
            }
        }

        void Pousar()
        {
            float g = _giro;
            if (_tremer > 0f)
            {
                // uns 1,5° indo e voltando, 3 vezes, diminuindo (para o lado de abrir: a tranca segura)
                float t = 1f - _tremer / DuracaoDaTremida;
                g += Mathf.Abs(Mathf.Sin(t * Mathf.PI * 6f)) * 1.6f * (1f - t);
            }
            transform.localRotation = _base * Quaternion.Euler(0f, _sinal * g, 0f);
        }

        // ------------------------------------------------------------ a mira

        /// <summary>O jogador está do lado para onde a folha abre (o de dentro da casa, nas de fora).</summary>
        public bool DoLadoQueAbre(Vector3 ponto)
        {
            var q = transform.parent.InverseTransformPoint(ponto);
            return (q.x - dados.dobradica.x) * dados.abre.x + (q.z - dados.dobradica.z) * dados.abre.y > 0f;
        }

        public override string Dica(ArmaJogador quem)
        {
            var mundo = PortasDoMundo.Instancia;
            if (mundo == null) return null;
            // a barricada de tábuas (11/11/2026, a etapa 5b): não abre, dos dois lados (do lado dela, a mira acha as tábuas)
            string barricada = PortasDoMundo.DicaDaBarricada(chave, quem, false);
            if (barricada != null) return barricada;
            string pregar = PortasDoMundo.DicaDePregar(chave, quem);
            if (Aberta) return "[E] Fechar a porta" + pregar;
            if (mundo.Trancada(this))
            {
                if (dados.DeFora && DoLadoQueAbre(quem.transform.position)) return "[E] Destrancar e abrir a porta" + pregar;
                if (mundo.SabidaTrancada(chave)) return mundo.DicaDaTrancada(this, quem) + (mundo.TempoDeArrombar(this, quem) > 0f ? "" : pregar);
            }
            return "[E] Abrir a porta" + pregar;
        }

        public override void Usar(ArmaJogador quem) => PortasDoMundo.Instancia?.PedirPorta(this);

        // segurar o E: arrombar (a trancada sabida, com a ferramenta) ou pregar a barricada de tábuas (11/11/2026, a etapa 5b;
        // aí o toque no E continua abrindo e fechando)
        public override float Segurar(ArmaJogador quem) => PortasDoMundo.Instancia != null ? PortasDoMundo.Instancia.TempoDoSegurar(this, quem) : 0f;

        public override void Segurado(ArmaJogador quem) => PortasDoMundo.Instancia?.Segurado(this, quem);

        public override string Segurando(ArmaJogador quem) => PortasDoMundo.Instancia?.TextoDoSegurar(this, quem);

        public override bool ToqueUsa(ArmaJogador quem) => PortasDoMundo.Instancia != null && PortasDoMundo.Instancia.ToqueUsa(this, quem);

        void OnDestroy() => PortasDoMundo.Instancia?.Esquecer(this);
    }
}
