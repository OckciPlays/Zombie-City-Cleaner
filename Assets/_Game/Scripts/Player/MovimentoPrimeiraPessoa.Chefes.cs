using UnityEngine;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// Na mão de um chefe (15/11/2026: o agarrão do Blindado, a mão de carne dos Agregados, o braço comprido do Alongado;
    /// Zumbi.Chefes.cs). O anfitrião decide quem é agarrado e quando solta (JogadorRede.Chefes.cs); aqui, na máquina do
    /// dono, o jogador vai com a mão da cópia do chefe (a mesma que ele vê), sem a cápsula (não fica preso no corpo do
    /// chefe nem no chão), e continua olhando e atirando (o E não usa nada: nem a porta do carro, nem o caiaque). Solto, sai
    /// com a velocidade do arremesso (ou só cai). Sem notícias do anfitrião, o tempo máximo solta sozinho.
    /// </summary>
    public partial class MovimentoPrimeiraPessoa
    {
        Transform _maoDoChefe;
        Zombies.Zumbi _chefeQueSegura;
        float _seguradoAte;
        // desligado (o desmaio, o pânico) na mão do chefe: a cápsula volta quando o movimento voltar (OnEnable)
        bool _capsulaDoChefe;

        /// <summary>Na mão de um chefe agora (nesta máquina).</summary>
        public bool NaMaoDoChefe => _maoDoChefe != null;

        /// <summary>O chefe que segura o jogador agora (nesta máquina), ou null.</summary>
        public Zombies.Zumbi ChefeQueSegura => _maoDoChefe != null ? _chefeQueSegura : null;

        /// <summary>
        /// O chefe agarrou o jogador (o anfitrião mandou): vai com a mão dele até ser solto. False: não deu (o movimento está
        /// desligado: o pânico, o pulo da janela, o carro), e o anfitrião é avisado.
        /// </summary>
        public bool AgarrarPeloChefe(Zombies.Zumbi chefe, Transform mao, float tempoMaximo)
        {
            if (_voando || mao == null || !enabled || !isActiveAndEnabled) return false;
            _chefeQueSegura = chefe;
            _maoDoChefe = mao;
            _seguradoAte = Time.time + Mathf.Clamp(tempoMaximo + 0.6f, 0.6f, 6f);
            PresoNaGosma = false;
            Nadando = NadandoRapido = false;   // (agarrado nadando: a mão sai da água, e a arma volta)
            if (Agachado) Levantar(true);
            _controle.enabled = false;
            _velocidade = Vector3.zero;
            Tremer(0.6f);
            return true;
        }

        /// <summary>O chefe soltou (o anfitrião mandou): com esta velocidade (o arremesso), ou só cai.</summary>
        public void SoltarDoChefe(Vector3 velocidade)
        {
            if (_maoDoChefe == null) return;
            LargarDoChefe(true);
            _velocidade = velocidade;
            if (velocidade.sqrMagnitude > 4f) Tremer(0.7f);
        }

        /// <summary>
        /// Sai da mão. Com <paramref name="ligarCapsula"/>, a cápsula volta já (fora das hitboxes); sem (o movimento sendo
        /// desligado: quem desligou cuida dela), ela volta só quando o movimento voltar, se ninguém a tiver ligado.
        /// </summary>
        void LargarDoChefe(bool ligarCapsula)
        {
            if (_maoDoChefe == null) return;
            var chefe = _chefeQueSegura;
            _maoDoChefe = null;
            _chefeQueSegura = null;
            if (_controle == null) return;
            if (!ligarCapsula)
            {
                _capsulaDoChefe = true;
                return;
            }
            if (!_voando && chefe != null) ForaDasHitboxes(chefe.transform.position);
            _controle.enabled = !_voando;
        }

        void OnEnable()
        {
            // (o movimento voltou depois de desligado na mão do chefe: o desmaio, o pânico; o carro e o caiaque ligam a cápsula
            // ao sair, pelo Teleportar)
            if (!_capsulaDoChefe) return;
            _capsulaDoChefe = false;
            if (_controle != null && !_voando && !_controle.enabled) _controle.enabled = true;
        }

        /// <summary>
        /// Na mão, o jogador fica dentro das hitboxes da mão (a camada Zumbi bate no Jogador): a cápsula ligada ali o
        /// empurrava ~1,7 m para cima. Antes de ligar, ele sai para o lado de fora do chefe até ficar livre das hitboxes e do
        /// cenário (até 4 m); sem lugar livre, fica onde está.
        /// </summary>
        static int _comQuemBate = -1, _camadaDoComQuemBate = -1;

        /// <summary>As camadas que batem nesta (a matriz de colisões da física), calculada uma vez.</summary>
        static int ComQuemBate(int camada)
        {
            if (_camadaDoComQuemBate == camada) return _comQuemBate;
            int m = 0;
            for (int c = 0; c < 32; c++)
                if (!Physics.GetIgnoreLayerCollision(camada, c)) m |= 1 << c;
            _comQuemBate = m;
            _camadaDoComQuemBate = camada;
            return m;
        }

        void ForaDasHitboxes(Vector3 chefe)
        {
            // o que bate na cápsula do jogador (as hitboxes, os corpos dos zumbis, o cenário, os carros, as portas), menos ele
            // mesmo (os braços e o retrato); e o mundo, que o caminho até o lugar novo não atravessa (uma parede, uma porta)
            int comQuemBate = ComQuemBate(gameObject.layer) & ~((1 << gameObject.layer) | (1 << 6) | (1 << 8) | (1 << 10));
            int mundo = comQuemBate & ~((1 << Zombies.Zumbi.CamadaZumbi) | (1 << 2));
            Vector3 fora = transform.position - chefe;
            fora.y = 0f;
            fora = fora.sqrMagnitude > 0.01f ? fora.normalized : -transform.forward;
            Vector3 lado = Vector3.Cross(Vector3.up, fora);
            float r = _controle.radius;
            // (um degrau acima: perto do chão, uma guia ou um declive barrava todos os lugares; a gravidade o traz de volta)
            Vector3 acima = Vector3.up * (_controle.stepOffset + 0.05f);
            Vector3 origem = transform.position + acima + Vector3.up * _controle.center.y;
            Vector3[] rumos = { fora, (fora + lado).normalized, (fora - lado).normalized, lado, -lado };
            foreach (var rumo in rumos)
                for (float d = 0f; d <= 4f; d += 0.25f)
                {
                    Vector3 p = transform.position + rumo * d + acima;
                    if (d > 0f && Physics.Linecast(origem, p + Vector3.up * _controle.center.y, mundo, QueryTriggerInteraction.Ignore)) break;   // (parede no caminho)
                    Vector3 baixo = p + _controle.center - Vector3.up * (_controle.height * 0.5f - r);
                    Vector3 alto = p + _controle.center + Vector3.up * (_controle.height * 0.5f - r);
                    if (Physics.CheckCapsule(baixo, alto, r, comQuemBate, QueryTriggerInteraction.Ignore)) continue;
                    transform.position = p;
                    return;
                }
        }

        /// <summary>A cada quadro, no dono: na mão, vai com ela (true: o resto do movimento fica parado).</summary>
        bool SeguradoPeloChefe(float dt)
        {
            if (_maoDoChefe == null) return false;
            var z = _chefeQueSegura;
            if (Time.time > _seguradoAte || z == null || !z.isActiveAndEnabled || z.Morto || !_maoDoChefe.gameObject.activeInHierarchy)
            {
                SoltarDoChefe(Vector3.zero);
                return false;
            }
            // a mão na altura do peito; nunca abaixo do chão embaixo dela (o Blindado pega do chão)
            Vector3 alvo = _maoDoChefe.position - Vector3.up * 1f;
            // (o lado da laje é o de onde o jogador está: o raio para o chão sai da altura dele, no lugar da mão, sobe até 1,5 m
            // acima da mão, mas para embaixo do que houver em cima (uma marquise, o teto), e desce dali. Assim quem está embaixo
            // de uma marquise fica embaixo, e quem está em cima de uma sacada fica em cima, até a mão sair da beirada)
            Vector3 mao = _maoDoChefe.position;
            var deOnde = new Vector3(mao.x, transform.position.y + 0.3f, mao.z);
            float topoY = Mathf.Max(mao.y + 1.5f, deOnde.y);
            if (Physics.Raycast(deOnde, Vector3.up, out var teto, topoY - deOnde.y + 0.01f, 1, QueryTriggerInteraction.Ignore)) topoY = teto.point.y - 0.05f;
            if (Physics.Raycast(new Vector3(mao.x, topoY, mao.z), Vector3.down, out var chao, 30f, 1, QueryTriggerInteraction.Ignore))
                alvo.y = Mathf.Max(alvo.y, chao.point.y + 0.05f);
            if (_controle.enabled) _controle.enabled = false;
            transform.position = Vector3.Lerp(transform.position, alvo, 1f - Mathf.Exp(-12f * dt));
            _velocidade = Vector3.zero;
            Correndo = false;
            CameraDoAbaixar(dt);
            CameraNaAgua(dt, Vector2.zero);   // (agarrado nadando: a câmera sai da água)
            return true;
        }
    }
}
