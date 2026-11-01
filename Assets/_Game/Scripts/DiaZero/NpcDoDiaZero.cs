using UnityEngine;
using ZombieCityCleaner.Mundo;
using ZombieCityCleaner.Player;
using ZombieCityCleaner.Roupas;

namespace ZombieCityCleaner.DiaZero
{
    /// <summary>
    /// Um NPC do Dia 0 (o NPC das roupas do Gustavo, vestido pelo sorteio do tipo dele). A rotina vem do lugar
    /// (<see cref="LugarDeNpc"/>):
    ///  - quem anda (o passeio na calçada, a corrida, o cortador de grama, o faxineiro e quem compra no mercado) está onde o
    ///    relógio da partida diz, no caminho dele (igual em todas as máquinas), com o "Andar" ou o "Correr";
    ///  - os gestos vão por cima da animação, em cada osso (<see cref="LateUpdate"/>): os braços apontados para onde a pose
    ///    pede (a mangueira, o cabo do cortador, o esfregão, o aceno, a conversa), sem depender do eixo dos ossos;
    ///  - quem está parado olha para quem chega perto e acena (o atendente, os vizinhos);
    ///  - os doentes (5) têm os olhos vermelhos, tossem (com a mão na boca) e coçam o braço de vez em quando, como num
    ///    resfriado. Os gestos e as tosses são de cada máquina (não precisam bater).
    /// </summary>
    public class NpcDoDiaZero : MonoBehaviour
    {
        /// <summary>Com quem conversa (o outro NPC).</summary>
        public Transform Par { get; set; }
        public LugarDeNpc Lugar { get; private set; }
        public bool Doente => Lugar != null && Lugar.doente;

        Animator _an;
        Transform _peito, _pescoco, _cabeca, _ombroD, _bracoD, _anteD, _maoD, _ombroE, _bracoE, _anteE, _maoE;
        Vector3[] _caminho;
        float[] _acum;
        float _total, _vel, _desvio, _passo, _parado;
        string _animacao;
        AudioSource _voz, _maquina;
        bool _mulher;
        float _proximaTosse, _tosseDesde = -10f, _proximaCoceira, _coceiraDesde = -10f, _acenoDesde = -10f, _proximoAceno;
        Transform _cortador, _esfregao, _bico;
        ParticleSystem _agua;
        float _fase;
        bool _andando;

        const int SemJogador = ~((1 << 2) | (1 << 6) | (1 << 7) | (1 << 8) | (1 << 9) | (1 << 10) | (1 << 11));

        public void Iniciar(RuaDoDiaZero rua, LugarDeNpc l, int indice, Material particulas)
        {
            Lugar = l;
            _an = GetComponentInChildren<Animator>();
            var v = GetComponent<Vestimenta>();
            if (v != null)
            {
                v.npc = "";
                v.VestirSorteado(l.tipo, l.semente);
                _mulher = v.corpo != CatalogoDasRoupas.Homem;
            }
            _peito = Osso("chest"); _pescoco = Osso("neck"); _cabeca = Osso("head");
            _ombroD = Osso("shoulder.R"); _bracoD = Osso("upper_arm.R"); _anteD = Osso("forearm.R"); _maoD = Osso("hand.R");
            _ombroE = Osso("shoulder.L"); _bracoE = Osso("upper_arm.L"); _anteE = Osso("forearm.L"); _maoE = Osso("hand.L");
            var r = new System.Random(l.semente);
            _fase = (float)r.NextDouble() * 10f;
            _caminho = l.caminho;
            if (_caminho != null && _caminho.Length > 1)
            {
                _acum = new float[_caminho.Length + 1];
                for (int i = 0; i < _caminho.Length; i++)
                    _acum[i + 1] = _acum[i] + Vector3.Distance(_caminho[i], _caminho[(i + 1) % _caminho.Length]);
                _total = _acum[_caminho.Length];
            }
            switch (l.rotina)
            {
                case Rotina.Passear: _vel = 1.15f + (float)r.NextDouble() * 0.3f; break;
                case Rotina.Correr: _vel = 3.0f; break;
                case Rotina.CortarGrama: _vel = 0.65f; break;
                case Rotina.Faxineiro: _vel = 0.45f; _passo = 1.6f; _parado = 5f; break;
                case Rotina.Comprar: _vel = 0.9f; _passo = 3.5f; _parado = 3.5f; break;
            }
            _desvio = (float)r.NextDouble() * Mathf.Max(1f, _total);
            _voz = gameObject.AddComponent<AudioSource>();
            _voz.spatialBlend = 1f;
            _voz.rolloffMode = AudioRolloffMode.Linear;
            _voz.minDistance = 1.5f;
            _voz.maxDistance = 18f;
            _voz.playOnAwake = false;
            _proximaTosse = Time.time + 2f + (float)r.NextDouble() * 8f;
            _proximaCoceira = Time.time + 6f + (float)r.NextDouble() * 10f;

            // os objetos da rotina
            var material = rua.material;
            switch (l.rotina)
            {
                case Rotina.Regar: Mangueira(material, particulas, (float)r.NextDouble()); break;
                case Rotina.CortarGrama: Cortador(material); break;
                case Rotina.Faxineiro: Esfregao(material); break;
            }
            _particulas = particulas;
            // quem fica parado tem o corpo (o jogador esbarra); quem anda passa
            if (_caminho == null)
            {
                var cap = gameObject.AddComponent<CapsuleCollider>();
                cap.center = new Vector3(0f, 0.9f, 0f);
                cap.height = 1.8f;
                cap.radius = 0.28f;
            }
            Tocar("Parado");
            AoChao(transform.position);
        }

        Transform Osso(string nome)
        {
            foreach (var t in GetComponentsInChildren<Transform>(true)) if (t.name == nome) return t;
            return null;
        }

        void Tocar(string estado, float velocidade = 1f)
        {
            if (_an == null) return;
            _an.speed = velocidade;
            if (_animacao == estado) return;
            _animacao = estado;
            _an.CrossFadeInFixedTime(estado, 0.25f);
        }

        void AoChao(Vector3 p)
        {
            if (Physics.Raycast(p + Vector3.up * 1.2f, Vector3.down, out var h, 3f, SemJogador, QueryTriggerInteraction.Ignore)) p.y = h.point.y;
            transform.position = p;
        }

        // ------------------------------------------------------------------ o movimento

        void Update()
        {
            float t = Time.time;
            if (_caminho != null && _total > 0.1f)
            {
                double agora = NpcsDoDiaZero.Agora;
                // com as paradas (o faxineiro esfrega, quem compra olha a prateleira): anda 'passo' metros e para 'parado' s
                float s;
                if (_passo > 0f)
                {
                    float ciclo = _passo / _vel + _parado;
                    double c = agora / ciclo;
                    float resto = (float)((c - System.Math.Floor(c)) * ciclo);
                    s = (float)(System.Math.Floor(c) * _passo) + Mathf.Min(resto, _passo / _vel) * _vel;
                    _andando = resto < _passo / _vel;
                }
                else
                {
                    s = (float)(agora * _vel);
                    _andando = true;
                }
                s = Mathf.Repeat(s + _desvio, _total);
                int i = 0;
                while (i < _caminho.Length - 1 && _acum[i + 1] < s) i++;
                var a = _caminho[i];
                var b = _caminho[(i + 1) % _caminho.Length];
                float seg = Mathf.Max(0.001f, _acum[i + 1] - _acum[i]);
                var p = Vector3.Lerp(a, b, (s - _acum[i]) / seg);
                var dir = b - a;
                dir.y = 0f;
                AoChao(new Vector3(p.x, p.y, p.z));
                if (dir.sqrMagnitude > 0.001f && _andando)
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * 6f);
                if (!_andando && Lugar.rotina == Rotina.Comprar)
                {
                    // olhando a prateleira (de lado)
                    var lado = Quaternion.LookRotation(Quaternion.Euler(0f, 90f, 0f) * dir.normalized);
                    transform.rotation = Quaternion.Slerp(transform.rotation, lado, Time.deltaTime * 3f);
                }
                if (_andando)
                {
                    bool correndo = Lugar.rotina == Rotina.Correr;
                    Tocar(correndo ? "Correr" : "Andar", correndo ? _vel / 3.6f : Mathf.Clamp(_vel / 1.35f, 0.4f, 1.3f));
                }
                else Tocar("Parado");
            }
            else
            {
                Tocar("Parado");
                // quem conversa olha para o outro
                if (Par != null)
                {
                    var d = Par.position - transform.position;
                    d.y = 0f;
                    if (d.sqrMagnitude > 0.01f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(d), Time.deltaTime * 3f);
                }
            }
            if (_maquina != null) _maquina.pitch = _andando ? 1f : 0.85f;

            // os doentes: a tosse e a coceira, de vez em quando
            if (Doente)
            {
                if (t >= _proximaTosse)
                {
                    _tosseDesde = t;
                    _proximaTosse = t + Random.Range(7f, 16f);
                    _voz.pitch = Random.Range(0.92f, 1.08f);
                    _voz.PlayOneShot(SonsDoDiaZero.Tosse(_mulher, Random.Range(0, 3)), 0.9f);
                }
                if (t >= _proximaCoceira && t - _tosseDesde > 2f)
                {
                    _coceiraDesde = t;
                    _proximaCoceira = t + Random.Range(9f, 20f);
                }
            }
            // acenar para quem chega perto (os parados; quem passeia, às vezes)
            var j = JogadorRede.Local;
            if (j != null && t >= _proximoAceno && Lugar.rotina != Rotina.Correr && Lugar.rotina != Rotina.CortarGrama && Lugar.rotina != Rotina.Faxineiro)
            {
                var d = j.transform.position - transform.position;
                float dist = d.magnitude;
                if (dist < (Lugar.rotina == Rotina.Atendente ? 3.5f : 5f) && dist > 1.0f && Vector3.Dot(transform.forward, d / dist) > 0.2f)
                {
                    _acenoDesde = t;
                    _proximoAceno = t + Random.Range(25f, 45f);
                }
            }
        }

        // ------------------------------------------------------------------ os gestos (por cima da animação)

        Material _particulas;
        bool _comOlhos;

        void LateUpdate()
        {
            if (_bracoD == null || _anteD == null || _maoD == null) return;
            // os olhos vermelhos dos doentes: postos no primeiro quadro já animado (a cabeça na pose de verdade)
            if (Doente && !_comOlhos)
            {
                _comOlhos = true;
                OlhosVermelhos(_particulas);
            }
            float t = Time.time + _fase;
            var f = transform.forward;
            var d = transform.right;
            var c = Vector3.up;
            switch (Lugar.rotina)
            {
                case Rotina.Regar:
                {
                    // o braço direito para a frente e para baixo, com a mangueira, balançando devagar
                    float bal = Mathf.Sin(t * 0.7f) * 0.25f;
                    Apontar(_bracoD, _anteD, f * 0.75f - c * 0.55f + d * (0.15f + bal * 0.3f), 1f);
                    Apontar(_anteD, _maoD, f * 0.95f - c * 0.05f + d * bal, 1f);
                    if (_agua != null && _bico != null)
                    {
                        _bico.SetPositionAndRotation(_maoD.position + f * 0.06f, Quaternion.LookRotation(f * 0.9f - c * 0.25f + d * bal));
                    }
                    break;
                }
                case Rotina.CortarGrama:
                    // as duas mãos no cabo do cortador
                    Apontar(_bracoD, _anteD, f * 0.55f - c * 0.8f + d * 0.05f, 1f);
                    Apontar(_anteD, _maoD, f * 0.9f - c * 0.35f, 1f);
                    Apontar(_bracoE, _anteE, f * 0.55f - c * 0.8f - d * 0.05f, 1f);
                    Apontar(_anteE, _maoE, f * 0.9f - c * 0.35f, 1f);
                    if (_cortador != null)
                        _cortador.SetPositionAndRotation(transform.position + f * 0.95f, transform.rotation);
                    break;
                case Rotina.Faxineiro:
                {
                    // o esfregão: as mãos juntas no cabo, indo e vindo quando parado
                    float vai = _andando ? 0f : Mathf.Sin(t * 3.2f) * 0.35f;
                    Apontar(_bracoD, _anteD, f * 0.45f - c * 0.85f + d * (0.1f + vai * 0.4f), 1f);
                    Apontar(_anteD, _maoD, f * 0.8f - c * 0.2f + d * vai * 0.5f, 1f);
                    Apontar(_bracoE, _anteE, f * 0.5f - c * 0.6f + d * (0.2f + vai * 0.4f), 1f);
                    Apontar(_anteE, _maoE, f * 0.6f + c * 0.2f + d * (0.4f + vai * 0.5f), 1f);
                    if (_esfregao != null)
                    {
                        var base_ = transform.position + f * 0.75f + d * vai;
                        var topo = (_maoD.position + _maoE.position) * 0.5f;
                        _esfregao.SetPositionAndRotation(base_, Quaternion.LookRotation(f, topo - base_));
                    }
                    break;
                }
                case Rotina.Conversar:
                {
                    // as mãos falando: um braço de cada vez, de vez em quando
                    float g = Mathf.Sin(t * 0.9f);
                    if (g > 0.3f)
                    {
                        float w = Mathf.InverseLerp(0.3f, 0.6f, g);
                        Apontar(_bracoD, _anteD, f * 0.25f - c * 0.95f + d * 0.15f, w);
                        Apontar(_anteD, _maoD, f * 0.9f + c * (0.1f + Mathf.Sin(t * 5f) * 0.15f) + d * 0.2f, w);
                    }
                    else if (g < -0.5f)
                    {
                        float w = Mathf.InverseLerp(-0.5f, -0.8f, g);
                        Apontar(_bracoE, _anteE, f * 0.25f - c * 0.95f - d * 0.15f, w);
                        Apontar(_anteE, _maoE, f * 0.9f + c * (0.1f + Mathf.Sin(t * 4f) * 0.15f) - d * 0.2f, w);
                    }
                    if (_cabeca != null) _cabeca.rotation = Quaternion.AngleAxis(Mathf.Sin(t * 2.3f) * 4f, d) * _cabeca.rotation;
                    break;
                }
                case Rotina.Atendente:
                    // as mãos no balcão
                    Apontar(_bracoD, _anteD, f * 0.3f - c * 0.95f + d * 0.1f, 0.8f);
                    Apontar(_anteD, _maoD, f * 0.95f - c * 0.2f, 0.8f);
                    Apontar(_bracoE, _anteE, f * 0.3f - c * 0.95f - d * 0.1f, 0.8f);
                    Apontar(_anteE, _maoE, f * 0.95f - c * 0.2f, 0.8f);
                    break;
            }
            OlharPraQuemChega();
            Acenar(t, f, d, c);
            Coceira(f, d, c);
            Tosse(f, d, c);
        }

        // o osso aponta para a direção (no mundo), pela ponta dele (o filho): sem depender do eixo do osso
        internal static void Apontar(Transform osso, Transform filho, Vector3 dir, float peso)
        {
            if (osso == null || filho == null || peso <= 0f) return;
            var atual = filho.position - osso.position;
            if (atual.sqrMagnitude < 1e-6f || dir.sqrMagnitude < 1e-6f) return;
            var alvo = Vector3.Slerp(atual.normalized, dir.normalized, Mathf.Clamp01(peso));
            osso.rotation = Quaternion.FromToRotation(atual.normalized, alvo) * osso.rotation;
        }

        // a cabeça vira (até 55°) para o jogador local que está perto e na frente
        void OlharPraQuemChega()
        {
            var j = JogadorRede.Local;
            if (j == null || _cabeca == null) return;
            var olho = j.transform.position + Vector3.up * 1.6f;
            var d = olho - _cabeca.position;
            float dist = d.magnitude;
            if (dist > 6f || dist < 0.3f) return;
            var plano = new Vector3(d.x, 0f, d.z);
            if (Vector3.Angle(transform.forward, plano) > 100f) return;
            var alvo = Vector3.RotateTowards(transform.forward, d.normalized, 55f * Mathf.Deg2Rad, 0f);
            float w = Mathf.InverseLerp(6f, 3f, dist) * 0.8f;
            var giro = Quaternion.FromToRotation(transform.forward, alvo);
            _cabeca.rotation = Quaternion.Slerp(Quaternion.identity, giro, w) * _cabeca.rotation;
        }

        void Acenar(float t, Vector3 f, Vector3 d, Vector3 c)
        {
            float a = Time.time - _acenoDesde;
            if (a > 2.2f) return;
            float w = Mathf.Clamp01(a / 0.3f) * Mathf.Clamp01((2.2f - a) / 0.35f);
            float vai = Mathf.Sin(a * 11f) * 0.35f;
            Apontar(_bracoD, _anteD, c * 0.75f + d * 0.6f + f * 0.15f, w);
            Apontar(_anteD, _maoD, c * 0.95f + d * vai + f * 0.1f, w);
        }

        // a coceira: a mão esquerda no braço direito, esfregando
        void Coceira(Vector3 f, Vector3 d, Vector3 c)
        {
            float a = Time.time - _coceiraDesde;
            if (a > 2.4f || _bracoE == null) return;
            float w = Mathf.Clamp01(a / 0.3f) * Mathf.Clamp01((2.4f - a) / 0.3f);
            float vai = Mathf.Sin(a * 14f) * 0.12f;
            Apontar(_bracoD, _anteD, -c * 0.9f + f * 0.2f + d * 0.15f, w * 0.6f);
            Apontar(_bracoE, _anteE, f * 0.35f - c * 0.85f + d * 0.25f, w);
            Apontar(_anteE, _maoE, d * 0.85f + f * 0.4f + c * (0.1f + vai), w);
        }

        // a tosse: o peito dobra em trancos e a mão direita vai para a boca
        void Tosse(Vector3 f, Vector3 d, Vector3 c)
        {
            float a = Time.time - _tosseDesde;
            if (a > 1.5f) return;
            float w = Mathf.Clamp01(a / 0.15f) * Mathf.Clamp01((1.5f - a) / 0.4f);
            if (_peito != null)
            {
                float tranco = Mathf.Max(0f, Mathf.Sin(a * 17f)) * 9f + 8f;
                _peito.rotation = Quaternion.AngleAxis(tranco * w, d) * _peito.rotation;
            }
            Apontar(_bracoD, _anteD, f * 0.55f - c * 0.6f - d * 0.15f, w);
            Apontar(_anteD, _maoD, c * 0.85f - d * 0.45f + f * 0.25f, w);
        }

        // ------------------------------------------------------------------ os objetos

        // a mangueira: o bico na mão (com a água) e a mangueira no chão até a torneira (na parede da casa, atrás)
        void Mangueira(Material material, Material particulas, float sorte)
        {
            var obra = new Obra(Lugar.semente, Medidas.DoJogo);
            obra.Caixa(new Vector3(0f, 0f, 0.07f), new Vector3(0.035f, 0.035f, 0.14f), "PlacaVerde");
            obra.Caixa(new Vector3(0f, -0.04f, 0.0f), new Vector3(0.03f, 0.08f, 0.03f), "Metal");
            _bico = Peca("Bico da mangueira", obra, material, null).transform;
            if (particulas != null)
            {
                var go = new GameObject("Água");
                go.transform.SetParent(_bico, false);
                go.transform.localPosition = new Vector3(0f, 0f, 0.15f);
                _agua = go.AddComponent<ParticleSystem>();
                var main = _agua.main;
                main.startSpeed = new ParticleSystem.MinMaxCurve(4.2f, 5.2f);
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.7f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.07f);
                main.startColor = new Color(0.75f, 0.88f, 1f, 0.55f);
                main.gravityModifier = 1.4f;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.maxParticles = 300;
                var em = _agua.emission;
                em.rateOverTime = 140f;
                var forma = _agua.shape;
                forma.shapeType = ParticleSystemShapeType.Cone;
                forma.angle = 4f;
                forma.radius = 0.01f;
                go.GetComponent<ParticleSystemRenderer>().sharedMaterial = particulas;
                var som = go.AddComponent<AudioSource>();
                som.clip = SonsDoDiaZero.Agua;
                som.loop = true;
                som.spatialBlend = 1f;
                som.rolloffMode = AudioRolloffMode.Linear;
                som.minDistance = 1f;
                som.maxDistance = 12f;
                som.volume = 0.35f;
                som.time = sorte * Mathf.Max(0.1f, som.clip.length);
                som.Play();
            }
            // o canteiro na frente (onde a água cai) e a mangueira enrolada no chão, atrás
            var canteiro = new Obra(Lugar.semente + 1, Medidas.DoJogo);
            canteiro.Caixa(new Vector3(0f, 0.08f, 0f), new Vector3(2.2f, 0.16f, 0.9f), "MadeiraVelha");
            canteiro.Caixa(new Vector3(0f, 0.15f, 0f), new Vector3(2.05f, 0.05f, 0.75f), "Grama");
            var r = new System.Random(Lugar.semente);
            string[] flores = { "PlacaVermelha", "PlacaAmarela", "PlacaBranca", "Laranja" };
            for (int i = 0; i < 18; i++)
            {
                var p = new Vector3(-0.95f + (float)r.NextDouble() * 1.9f, 0.3f, -0.3f + (float)r.NextDouble() * 0.6f);
                canteiro.Caixa(p - Vector3.up * 0.08f, new Vector3(0.02f, 0.16f, 0.02f), "PlacaVerde");
                canteiro.Caixa(p, new Vector3(0.08f, 0.06f, 0.08f), flores[r.Next(flores.Length)]);
            }
            var c = Peca("Canteiro", canteiro, material, null);
            c.transform.SetPositionAndRotation(transform.position + transform.forward * 2.3f, transform.rotation);
            var rolo = new Obra(Lugar.semente + 2, Medidas.DoJogo);
            for (int i = 0; i < 6; i++)
            {
                float ang = i * 60f * Mathf.Deg2Rad;
                rolo.Caixa(new Vector3(Mathf.Cos(ang) * 0.22f, 0.02f, Mathf.Sin(ang) * 0.22f), new Vector3(0.26f, 0.03f, 0.03f), "PlacaVerde", i * -60f + 90f);
            }
            var m = Peca("Mangueira enrolada", rolo, material, null);
            m.transform.position = transform.position - transform.forward * 0.6f + transform.right * 0.5f;
        }

        // o cortador de grama: o corpo vermelho, as rodas, o cabo; o som do motor (laço)
        void Cortador(Material material)
        {
            var obra = new Obra(Lugar.semente, Medidas.DoJogo);
            obra.Caixa(new Vector3(0f, 0.17f, 0f), new Vector3(0.52f, 0.12f, 0.58f), "PlacaVermelha");
            obra.Caixa(new Vector3(0f, 0.28f, -0.05f), new Vector3(0.26f, 0.12f, 0.26f), "Metal");
            foreach (var x in new[] { -0.28f, 0.28f })
                foreach (var z in new[] { -0.22f, 0.22f })
                    obra.Caixa(new Vector3(x, 0.09f, z), new Vector3(0.05f, 0.18f, 0.18f), "Asfalto");
            // o cabo: duas barras do fundo até a altura das mãos (na frente do corpo de quem empurra) e a travessa
            foreach (var x in new[] { -0.2f, 0.2f })
                obra.CaixaEntre(new Vector3(x, 0.25f, -0.28f), new Vector3(x, 0.92f, -0.72f), 0.025f, 0.025f, "Grade");
            obra.Caixa(new Vector3(0f, 0.92f, -0.72f), new Vector3(0.44f, 0.03f, 0.03f), "Grade");
            _cortador = Peca("Cortador de grama", obra, material, null).transform;
            _maquina = _cortador.gameObject.AddComponent<AudioSource>();
            _maquina.clip = SonsDoDiaZero.Cortador;
            _maquina.loop = true;
            _maquina.spatialBlend = 1f;
            _maquina.rolloffMode = AudioRolloffMode.Linear;
            _maquina.minDistance = 2f;
            _maquina.maxDistance = 35f;
            _maquina.volume = 0.5f;
            _maquina.Play();
        }

        // o esfregão (o cabo e a cabeça) e o balde amarelo no chão
        void Esfregao(Material material)
        {
            var obra = new Obra(Lugar.semente, Medidas.DoJogo);
            obra.Caixa(new Vector3(0f, 0.65f, 0f), new Vector3(0.03f, 1.3f, 0.03f), "Madeira");
            obra.Caixa(new Vector3(0f, 0.04f, 0f), new Vector3(0.32f, 0.08f, 0.12f), "PlacaBranca");
            _esfregao = Peca("Esfregão", obra, material, null).transform;
            var balde = new Obra(Lugar.semente + 1, Medidas.DoJogo);
            balde.Caixa(new Vector3(0f, 0.17f, 0f), new Vector3(0.34f, 0.34f, 0.34f), "PlacaAmarela");
            balde.Caixa(new Vector3(0f, 0.32f, 0f), new Vector3(0.28f, 0.03f, 0.28f), "CarroAzul");
            var b = Peca("Balde", balde, material, null);
            if (_caminho != null && _caminho.Length > 0) b.transform.position = _caminho[0] + new Vector3(0.6f, 0f, 0.6f);
        }

        GameObject Peca(string nome, Obra obra, Material material, Transform pai)
        {
            var go = new GameObject(nome);
            go.transform.SetParent(pai != null ? pai : transform.parent, false);
            var malha = obra.vis.ParaMesh(nome);
            go.AddComponent<MeshFilter>().sharedMesh = malha;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            MalhaDeObra.Devolver(obra.vis);
            MalhaDeObra.Devolver(obra.col);
            return go;
        }

        // ------------------------------------------------------------------ os olhos vermelhos

        static Texture2D _manchaVermelha;
        static Material _materialDosOlhos;
        /// <summary>Onde ficam os olhos na cabeça (no osso da cabeça: para a frente, para cima e para o lado, em metros).</summary>
        public static Vector3 OlhoNaCabeca = new Vector3(0.048f, 0.125f, 0.142f);

        // em volta de cada olho, uma mancha vermelha transparente encostada no rosto (a febre, os olhos irritados)
        void OlhosVermelhos(Material particulas)
        {
            if (_cabeca == null || particulas == null) return;
            if (_manchaVermelha == null)
            {
                const int n = 32;
                _manchaVermelha = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Olheira" };
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float dx = (x + 0.5f) / n * 2f - 1f, dy = ((y + 0.5f) / n * 2f - 1f) * 1.5f;
                        float r = Mathf.Sqrt(dx * dx + dy * dy);
                        float a = Mathf.Clamp01(1f - r) * Mathf.Clamp01((r - 0.28f) * 4f);   // o miolo (o olho) fica livre
                        _manchaVermelha.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
                    }
                _manchaVermelha.Apply();
                _materialDosOlhos = new Material(particulas) { name = "Olhos vermelhos" };
                _materialDosOlhos.SetTexture("_BaseMap", _manchaVermelha);
                _materialDosOlhos.SetColor("_BaseColor", new Color(0.62f, 0.16f, 0.14f, 0.42f));
            }
            var quad = CriarQuad();
            foreach (float lado in new[] { -1f, 1f })
            {
                var go = new GameObject(lado < 0 ? "Olho vermelho (esquerdo)" : "Olho vermelho (direito)");
                go.transform.SetParent(_cabeca, false);
                // no espaço da cabeça do corpo: o "para a frente" do corpo é o do NPC
                var frente = _cabeca.InverseTransformDirection(transform.forward);
                var cima = _cabeca.InverseTransformDirection(Vector3.up);
                var lad = _cabeca.InverseTransformDirection(transform.right);
                go.transform.localPosition = cima * OlhoNaCabeca.y + frente * OlhoNaCabeca.z + lad * (OlhoNaCabeca.x * lado);
                go.transform.localRotation = Quaternion.LookRotation(-frente, cima);
                go.AddComponent<MeshFilter>().sharedMesh = quad;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = _materialDosOlhos;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        static Mesh _quad;
        static Mesh CriarQuad()
        {
            if (_quad != null) return _quad;
            const float w = 0.088f, h = 0.06f;
            _quad = new Mesh { name = "Olheira" };
            _quad.vertices = new[] { new Vector3(-w / 2, -h / 2, 0), new Vector3(w / 2, -h / 2, 0), new Vector3(w / 2, h / 2, 0), new Vector3(-w / 2, h / 2, 0) };
            _quad.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            // dos dois lados (a mancha aparece de qualquer jeito que o osso girar)
            _quad.triangles = new[] { 0, 2, 1, 0, 3, 2, 0, 1, 2, 0, 2, 3 };
            _quad.RecalculateNormals();
            _quad.RecalculateBounds();
            return _quad;
        }
    }
}
