using UnityEngine;
using ZombieCityCleaner.Mundo;
using ZombieCityCleaner.Player;

namespace ZombieCityCleaner.World
{
    /// <summary>
    /// Uma janela que abre de uma casa do mundo (o Plano_Portas.md da pasta de arte, a parte 5): as duas folhas de vidro sujo
    /// presas nos batentes, abrindo para fora como portas. O objeto fica no meio do peitoril, com o colisor que fecha o vão
    /// (na camada das portas: o jogador não pula, o tiro para; desligado com ela aberta) e o gatilho da mira. O E pede ao
    /// anfitrião (<see cref="PortasDoMundo.PedirJanela"/>): abrir, fechar, destrancar por dentro (o trinco) ou, trancada,
    /// tremer; trancada e sabida, segurar o E arromba, como as portas.
    /// </summary>
    public class JanelaDaCasa : Interagivel
    {
        public PortasDaConstrucao construcao;
        public int indice;
        public ulong chave;
        public JanelaDaObra dados;

        Transform _esquerda, _direita;
        Quaternion _baseE, _baseD;
        float _sinalE, _sinalD, _giro, _alvo, _tremer;
        BoxCollider _fecha;

        public const float Abertura = 100f;
        const float Velocidade = 300f;

        public bool Aberta => _alvo > 1f;
        /// <summary>O meio do vão, no mundo (o som e a distância).</summary>
        public Vector3 Meio => transform.position + Vector3.up * (dados.altura / 2);

        /// <summary>As folhas (as malhas, a esquerda presa no começo do vão e a direita no fim) e os colisores.</summary>
        public void Preparar(Mesh folha, Material material, bool aberta)
        {
            var u = new Vector3(dados.u.x, 0f, dados.u.y);
            var fora = new Vector3(dados.fora.x, 0f, dados.fora.y);
            // o objeto: no meio do peitoril, o +Z para fora
            transform.localRotation = Quaternion.LookRotation(fora);
            float meia = dados.largura / 2 - 0.01f, recuo = dados.parede / 2 - 0.03f;
            _esquerda = Folha("Folha esquerda", -u * meia + fora * recuo, u, fora, folha, material, out _baseE, out _sinalE);
            _direita = Folha("Folha direita", u * meia + fora * recuo, -u, fora, folha, material, out _baseD, out _sinalD);
            // o vão fechado (o jogador, o pulo e o tiro) e a mira (sempre)
            var tam = new Vector3(dados.largura, dados.altura, Mathf.Max(0.08f, dados.parede * 0.5f));
            _fecha = gameObject.AddComponent<BoxCollider>();
            _fecha.center = new Vector3(0f, dados.altura / 2, 0f);
            _fecha.size = tam;
            var mira = gameObject.AddComponent<BoxCollider>();
            mira.isTrigger = true;
            mira.center = _fecha.center;
            mira.size = tam + new Vector3(0f, 0f, 0.1f);
            _giro = _alvo = aberta ? Abertura : 0f;
            _fecha.enabled = !aberta;
            Pousar();
            enabled = false;
        }

        // uma folha: presa em 'dobradica' (no espaço da casa, a partir do peitoril), indo para 'fechada', abrindo para 'abre'
        Transform Folha(string nome, Vector3 dobradica, Vector3 fechada, Vector3 abre, Mesh malha, Material material, out Quaternion base_, out float sinal)
        {
            var go = new GameObject(nome) { layer = PortasDoMundo.Camada };
            go.transform.SetParent(transform.parent, false);
            go.transform.localPosition = transform.localPosition + dobradica + Vector3.up * 0.01f;
            go.AddComponent<MeshFilter>().sharedMesh = malha;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            if (Vector3.Dot(Vector3.Cross(Vector3.up, abre), fechada) > 0f) { base_ = Quaternion.LookRotation(abre); sinal = -1f; }
            else { base_ = Quaternion.LookRotation(-abre); sinal = 1f; }
            return go.transform;
        }

        /// <summary>Aberta ou fechada (o estado da rede); 'ja': sem a animação.</summary>
        public void Definir(bool aberta, bool ja = false)
        {
            float alvo = aberta ? Abertura : 0f;
            if (Mathf.Approximately(alvo, _alvo) && !ja) return;
            _alvo = alvo;
            if (ja) _giro = alvo;
            if (aberta && _fecha != null) _fecha.enabled = false;
            enabled = true;
            if (ja) Update();
        }

        public void Tremer()
        {
            _tremer = PortaDaCasa.DuracaoDaTremida;
            enabled = true;
        }

        void Update()
        {
            _giro = Mathf.MoveTowards(_giro, _alvo, Velocidade * Time.deltaTime);
            if (_tremer > 0f) _tremer = Mathf.Max(0f, _tremer - Time.deltaTime);
            Pousar();
            if (Mathf.Approximately(_giro, _alvo) && _tremer <= 0f)
            {
                if (_fecha != null) _fecha.enabled = _alvo < 1f;
                enabled = false;
            }
        }

        void Pousar()
        {
            float g = _giro;
            if (_tremer > 0f)
            {
                float t = 1f - _tremer / PortaDaCasa.DuracaoDaTremida;
                g += Mathf.Abs(Mathf.Sin(t * Mathf.PI * 6f)) * 1.4f * (1f - t);
            }
            if (_esquerda != null) _esquerda.localRotation = _baseE * Quaternion.Euler(0f, _sinalE * g, 0f);
            if (_direita != null) _direita.localRotation = _baseD * Quaternion.Euler(0f, _sinalD * g, 0f);
        }

        /// <summary>O ponto está do lado de dentro da casa (as folhas abrem para fora).</summary>
        public bool DoLadoDeDentro(Vector3 ponto)
        {
            var q = transform.parent.InverseTransformPoint(ponto);
            return (q.x - dados.peitoril.x) * dados.fora.x + (q.z - dados.peitoril.z) * dados.fora.y < 0f;
        }

        void OnDestroy()
        {
            if (_esquerda != null) Destroy(_esquerda.gameObject);
            if (_direita != null) Destroy(_direita.gameObject);
            PortasDoMundo.Instancia?.Esquecer(this);
        }

        // ------------------------------------------------------------ a mira

        public override string Dica(ArmaJogador quem)
        {
            var mundo = PortasDoMundo.Instancia;
            if (mundo == null) return null;
            if (Aberta) return "[E] Fechar a janela";
            if (mundo.Trancada(this))
            {
                if (DoLadoDeDentro(quem.transform.position)) return "[E] Destrancar e abrir a janela";
                if (mundo.SabidaTrancada(chave)) return mundo.DicaDaTrancada(this, quem);
            }
            return "[E] Abrir a janela";
        }

        public override void Usar(ArmaJogador quem) => PortasDoMundo.Instancia?.PedirJanela(this);

        public override float Segurar(ArmaJogador quem) => PortasDoMundo.Instancia != null ? PortasDoMundo.Instancia.TempoDeArrombar(this, quem) : 0f;

        public override void Segurado(ArmaJogador quem) => PortasDoMundo.Instancia?.PedirArrombar(this, quem);

        public override string Segurando(ArmaJogador quem) => PortasDoMundo.Instancia?.TextoDoArrombar(this, quem);
    }
}
