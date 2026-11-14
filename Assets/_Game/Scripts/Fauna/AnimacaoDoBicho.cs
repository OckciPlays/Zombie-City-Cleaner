using System.Collections.Generic;
using UnityEngine;

namespace ZombieCityCleaner.Fauna
{
    /// <summary>
    /// A animação dos bichos, feita na hora (sem clipes): as passadas pelo andar, o trote e o galope (a fase de cada perna),
    /// a cabeça baixa comendo, o alerta (a cabeça e as orelhas para cima, a cauda levantada), a morte (deitado de lado),
    /// as asas, a cobra e o peixe. Cada junta gira em volta dos eixos do próprio bicho (o de lado para levantar e baixar, o
    /// de cima para virar), levados para o espaço do pai dela: serve para as caixas e para os ossos do cervo (o Blender),
    /// sem saber o giro de cada osso. Quando os modelos de verdade chegarem com clipes, um Animator pode tomar o lugar.
    /// </summary>
    public sealed class AnimacaoDoBicho
    {
        public enum Pose : byte { Parado, Comendo, Alerta, Andando, Correndo, Atacando, Morto, Deitado, Nadando, Voando, Sentado }

        struct Junta
        {
            public Transform t;
            public Quaternion repouso;
            public Vector3 posicao;
            public bool Valida => t != null;
        }

        readonly Transform _bicho;
        readonly Corpo _tipo;
        readonly float _altura, _largura;
        readonly bool _caudaDeBandeira, _urso, _predador, _orelhasParaTras, _trepador, _bateAsPatas;
        readonly float _sorte, _comprimento;
        Junta _raiz, _corpo, _pescoco, _cabeca;
        readonly List<(Junta cima, Junta baixo, bool tras)> _pernas = new List<(Junta, Junta, bool)>();
        readonly List<Junta> _cauda = new List<Junta>(), _orelhas = new List<Junta>(), _asas = new List<Junta>(), _gomos = new List<Junta>();
        float _fase, _tempo, _comer, _alerta, _morto, _deitado, _voar, _ataque, _andar, _correr, _orelhaAte, _orelhaGiro, _empe, _nadar, _sentado;

        public AnimacaoDoBicho(Transform bicho, ModeloDoBicho m, Especie e)
        {
            _bicho = bicho;
            var d = Especies.De(e);
            _tipo = d.corpo;
            _altura = Mathf.Max(0.05f, m.altura);
            _largura = Mathf.Max(0.04f, d.altura * 0.35f * (m.altura / Mathf.Max(0.01f, d.altura)));
            _caudaDeBandeira = e == Especie.Veado || e == Especie.Gamba;   // o cauda-branca levanta a cauda ao fugir (a "bandeira"); o gambá, no aviso
            _trepador = e == Especie.Esquilo || e == Especie.Guaxinim;   // de pé no tronco (a etapa 4)
            _bateAsPatas = e == Especie.Gamba;
            // (a etapa 2: o urso fica de pé no alerta; a matilha espreita de cabeça baixa)
            _urso = d.papel == Papel.Urso;
            _predador = d.papel == Papel.Matilha;
            _orelhasParaTras = e == Especie.Alce;   // o alce bravo deita as orelhas para trás
            _comprimento = m.comprimento > 0.01f ? m.comprimento : d.comprimento * (m.altura / Mathf.Max(0.01f, d.altura));
            _sorte = Random.value * 10f;
            _raiz = J(m.raiz);
            _corpo = J(m.corpo);
            _pescoco = J(m.pescoco);
            _cabeca = J(m.cabeca);
            foreach (var p in m.pernas) _pernas.Add((J(p.cima), J(p.baixo), p.traseira));
            foreach (var t in m.cauda) _cauda.Add(J(t));
            foreach (var t in m.orelhas) _orelhas.Add(J(t));
            foreach (var t in m.asas) _asas.Add(J(t));
            foreach (var t in m.gomos) _gomos.Add(J(t));
        }

        static Junta J(Transform t) => t == null ? default : new Junta { t = t, repouso = t.localRotation, posicao = t.localPosition };

        // o eixo do bicho (no espaço dele) levado para o espaço do pai da junta
        Vector3 Eixo(Junta j, Vector3 noBicho)
        {
            var pai = j.t.parent;
            Vector3 mundo = _bicho.TransformDirection(noBicho);
            return pai != null ? pai.InverseTransformDirection(mundo) : mundo;
        }

        // levantar (+: a ponta para baixo e para trás), virar (+: para a direita) e rolar, a partir do repouso
        void Por(Junta j, float levantar, float virar = 0f, float rolar = 0f)
        {
            if (!j.Valida) return;
            var q = j.repouso;
            if (levantar != 0f) q = Quaternion.AngleAxis(levantar, Eixo(j, Vector3.right)) * q;
            if (virar != 0f) q = Quaternion.AngleAxis(virar, Eixo(j, Vector3.up)) * q;
            if (rolar != 0f) q = Quaternion.AngleAxis(rolar, Eixo(j, Vector3.forward)) * q;
            j.t.localRotation = q;
        }

        static float Chegar(float atual, float alvo, float rapidez, float dt) => Mathf.MoveTowards(atual, alvo, rapidez * dt);

        /// <summary>
        /// Um quadro da animação. 'velocidade' (m/s): o passo sai dela; 'correr': a velocidade de corrida da espécie (para
        /// escolher o andar, o trote e o galope); 'olhar': para onde a cabeça vira (graus, + à direita).
        /// </summary>
        public void Atualizar(Pose pose, float velocidade, float correr, float olhar, float dt)
        {
            _tempo += dt;
            _comer = Chegar(_comer, pose == Pose.Comendo ? 1f : 0f, 1.6f, dt);
            _alerta = Chegar(_alerta, pose == Pose.Alerta ? 1f : 0f, 3f, dt);
            _morto = Chegar(_morto, pose == Pose.Morto ? 1f : 0f, pose == Pose.Morto ? 1.8f : 4f, dt);
            _deitado = Chegar(_deitado, pose == Pose.Deitado ? 1f : 0f, 0.8f, dt);
            _voar = Chegar(_voar, pose == Pose.Voando ? 1f : 0f, 2f, dt);
            _empe = Chegar(_empe, (_urso || _trepador) && pose == Pose.Alerta && velocidade < 0.5f ? 1f : 0f, 2.2f, dt);
            _nadar = Chegar(_nadar, pose == Pose.Nadando ? 1f : 0f, 3f, dt);
            _sentado = Chegar(_sentado, pose == Pose.Sentado ? 1f : 0f, 1.5f, dt);
            if (pose == Pose.Sentado) _comer = Chegar(_comer, 0.6f, 1.6f, dt);   // (sentado, comendo o que tem nas patas)
            _ataque = pose == Pose.Atacando ? Mathf.Repeat(_ataque + dt * 1.6f, 1f) : Chegar(_ataque, 0f, 3f, dt);
            if (pose == Pose.Morto) velocidade = 0f;

            switch (_tipo)
            {
                case Corpo.Cobra: Cobra(velocidade, olhar, dt); break;
                case Corpo.Peixe: Peixe(velocidade, dt); break;
                case Corpo.Ave: Ave(velocidade, olhar, dt); break;
                case Corpo.Sapo: Sapo(velocidade, dt); break;
                default: Quadrupede(pose, velocidade, correr, olhar, dt); break;
            }
            Morte();
        }

        // ------------------------------------------------------------------ os quadrúpedes

        // a fase de cada perna (a dianteira esquerda, a direita, a traseira esquerda, a direita) no andar, no trote e no galope
        static readonly float[] FaseDoAndar = { 0.25f, 0.75f, 0f, 0.5f };
        static readonly float[] FaseDoTrote = { 0f, 0.5f, 0.5f, 0f };
        static readonly float[] FaseDoGalope = { 0f, 0.08f, 0.55f, 0.62f };

        void Quadrupede(Pose pose, float v, float correr, float olhar, float dt)
        {
            // o jeito de andar pela velocidade: o andar, o trote (~30% da corrida) e o galope; as passadas misturadas
            float trote = Mathf.InverseLerp(correr * 0.18f, correr * 0.32f, v);
            float galope = Mathf.InverseLerp(correr * 0.45f, correr * 0.62f, v);
            _andar = Chegar(_andar, Mathf.Clamp01(v / Mathf.Max(0.2f, correr * 0.12f)), 3f, dt);
            float passada = _altura * Mathf.Lerp(Mathf.Lerp(1.3f, 1.9f, trote), 3.2f, galope);
            _fase = Mathf.Repeat(_fase + v / Mathf.Max(0.05f, passada) * dt, 1f);
            float amp = Mathf.Lerp(Mathf.Lerp(20f, 28f, trote), 42f, galope) * _andar;
            float joelho = Mathf.Lerp(Mathf.Lerp(28f, 40f, trote), 62f, galope) * _andar;

            // o corpo: o sobe e desce e, no galope, o balanço de frente para trás
            if (_corpo.Valida)
            {
                float bob = Mathf.Lerp(0.012f, Mathf.Lerp(0.03f, 0.07f, galope), trote) * _altura * _andar;
                float y = Mathf.Abs(Mathf.Sin(_fase * Mathf.PI * 2f * (galope > 0.5f ? 1f : 2f))) * bob;
                float respirar = Mathf.Sin(_tempo * 1.6f + _sorte) * 0.004f * _altura * (1f - _andar);
                _corpo.t.localPosition = _corpo.posicao + _corpo.t.parent.InverseTransformVector(_bicho.up * (y + respirar));
                Por(_corpo, Mathf.Sin(_fase * Mathf.PI * 2f) * 6f * galope - 4f * _ataque * Mathf.Sin(_ataque * Mathf.PI));
            }

            // as pernas
            for (int i = 0; i < _pernas.Count; i++)
            {
                var (cima, baixo, tras) = _pernas[i];
                int k = Mathf.Min(i, 3);
                float off = Mathf.Lerp(Mathf.Lerp(FaseDoAndar[k], FaseDoTrote[k], trote), FaseDoGalope[k], galope);
                float a = (_fase + off) * Mathf.PI * 2f;
                float balanco = Mathf.Sin(a) * amp;
                // a dobra no joelho só enquanto a pata está no ar (a fase em que a perna vem para a frente)
                float dobra = Mathf.Max(0f, Mathf.Cos(a)) * joelho;
                // de pé e parado, as pernas descansam; morto, ficam meio dobradas
                // nadando: as patas remam devagar, sempre (a velocidade não conta)
                if (_nadar > 0.001f)
                {
                    float rema = (_tempo * 7f + off * Mathf.PI * 2f);
                    balanco = Mathf.Lerp(balanco, Mathf.Sin(rema) * 30f, _nadar);
                    dobra = Mathf.Lerp(dobra, Mathf.Max(0f, Mathf.Cos(rema)) * 40f, _nadar);
                }
                // o gambá no aviso bate as patas da frente no chão
                if (_bateAsPatas && !tras) balanco += Mathf.Max(0f, Mathf.Sin(_tempo * 14f + i * Mathf.PI)) * 22f * _alerta;
                // de pé (o urso), as de trás ficam a prumo e as da frente penduradas
                // sentado (a etapa 5): as de trás dobradas para a frente, no chão; as da frente a prumo, segurando
                Por(cima.t != null ? cima : default, balanco + (tras ? -6f : 4f) * _morto + (tras ? 70f : 25f) * _empe + (tras ? 95f : 34f) * _sentado);
                if (baixo.Valida) Por(baixo, (tras ? -dobra : dobra) + (tras ? -14f : 18f) * _morto + 10f * _deitado + (tras ? -115f : 12f) * _sentado);
            }

            // o pescoço e a cabeça: comendo (para baixo), alerta (para cima), correndo (esticado), o ataque (a cabeçada)
            float mastigar = Mathf.Sin(_tempo * 11f) * 2.5f * _comer;
            float aceno = Mathf.Sin(_fase * Mathf.PI * 4f) * 3f * _andar * (1f - galope);
            float alertaPescoco = _predador ? 16f : -14f, alertaCabeca = _predador ? 4f : -6f;
            // (nadando, a cabeça vai para cima, fora da água: abaixo)
            float pescoco = 55f * _comer + alertaPescoco * _alerta + 14f * galope + 30f * Mathf.Sin(_ataque * Mathf.PI) + 20f * _morto + 25f * _deitado
                            + 40f * _empe - 10f * _sentado;
            float cabeca = 22f * _comer + alertaCabeca * _alerta + mastigar + aceno + 20f * Mathf.Sin(_ataque * Mathf.PI) + 15f * _morto + 20f * _empe - 14f * _nadar;
            pescoco -= 22f * _nadar;
            float virar = olhar * (1f - _comer) * (1f - _morto);
            Por(_pescoco, pescoco, virar * 0.45f);
            Por(_cabeca, cabeca, virar * 0.55f);

            // a cauda: abana devagar; levantada no alerta e na fuga (a bandeira do veado)
            float bandeira = _caudaDeBandeira ? Mathf.Max(_alerta, galope) : 0f;
            for (int i = 0; i < _cauda.Count; i++)
                Por(_cauda[i], (i == 0 ? 80f : 25f) * bandeira * (1f - _morto), Mathf.Sin(_tempo * 2.3f + _sorte + i) * 8f * (1f - _morto));

            // as orelhas: um tremor de vez em quando; no alerta, para a frente
            if (_tempo > _orelhaAte)
            {
                _orelhaAte = _tempo + Random.Range(1.5f, 5f);
                _orelhaGiro = Random.Range(-20f, 20f);
            }
            float tremor = Mathf.Lerp(_orelhaGiro, 0f, Mathf.Clamp01((_orelhaAte - _tempo) * 2f - 4f));
            for (int i = 0; i < _orelhas.Count; i++)
                Por(_orelhas[i], (_orelhasParaTras ? 35f : -12f) * _alerta + 25f * _morto, (i == 0 ? 1f : -1f) * (tremor * (1f - _alerta) - 12f * _alerta));
        }

        // ------------------------------------------------------------------ as aves, a cobra, o peixe e o sapo

        void Ave(float v, float olhar, float dt)
        {
            float bater = _voar;
            _fase = Mathf.Repeat(_fase + (bater > 0.1f ? 3.2f : v / Mathf.Max(0.05f, _altura * 0.9f)) * dt, 1f);
            for (int i = 0; i < _asas.Count; i++)
            {
                float lado = i == 0 ? 1f : -1f;
                float asa = (Mathf.Sin(_fase * Mathf.PI * 2f) * 45f + 10f) * bater;
                Por(_asas[i], 0f, 0f, -lado * asa);
            }
            for (int i = 0; i < _pernas.Count; i++)
            {
                float a = (_fase + i * 0.5f) * Mathf.PI * 2f;
                Por(_pernas[i].cima, Mathf.Sin(a) * 28f * (1f - bater) * Mathf.Clamp01(v * 2f) - 40f * bater);
            }
            if (_corpo.Valida) Por(_corpo, 0f, 0f, Mathf.Sin(_fase * Mathf.PI * 2f) * 6f * Mathf.Clamp01(v * 2f) * (1f - bater));
            float bica = Mathf.Max(0f, Mathf.Sin(_tempo * 1.3f + _sorte)) * 40f * _comer;
            Por(_pescoco, bica + 30f * _morto, olhar * 0.4f);
            Por(_cabeca, Mathf.Sin(_tempo * 7f) * 4f * _comer, olhar * 0.5f);
            foreach (var c in _cauda) Por(c, Mathf.Sin(_tempo * 3f + _sorte) * 5f);
        }

        void Cobra(float v, float olhar, float dt)
        {
            _fase += (0.35f + v * 2.2f) * dt;
            for (int i = 0; i < _gomos.Count; i++)
            {
                float onda = Mathf.Sin(_fase * Mathf.PI * 2f - i * 0.75f) * Mathf.Lerp(6f, 22f, Mathf.Clamp01(v * 3f));
                Por(_gomos[i], 0f, (i == 0 ? olhar * 0.3f : 0f) + onda * (1f - _morto) + (i % 2 == 0 ? 20f : -20f) * _morto);
            }
        }

        void Peixe(float v, float dt)
        {
            _fase += (1.2f + v * 2.5f) * dt;
            foreach (var c in _cauda) Por(c, 0f, Mathf.Sin(_fase * Mathf.PI * 2f) * 28f * (1f - _morto));
            if (_corpo.Valida) Por(_corpo, 0f, -Mathf.Sin(_fase * Mathf.PI * 2f) * 6f * (1f - _morto));
        }

        void Sapo(float v, float dt)
        {
            // pulando: o corpo sobe num arco e as pernas de trás esticam
            _fase = v > 0.05f ? Mathf.Repeat(_fase + dt * 2.2f, 1f) : Chegar(_fase, 0f, 2f, dt);
            float pulo = v > 0.05f ? Mathf.Sin(_fase * Mathf.PI) : 0f;
            if (_corpo.Valida) _corpo.t.localPosition = _corpo.posicao + Vector3.up * pulo * 0.06f;
            for (int i = 0; i < _pernas.Count; i++)
                if (_pernas[i].tras) Por(_pernas[i].cima, -50f * pulo);
            float papo = Mathf.Max(0f, Mathf.Sin(_tempo * 3f + _sorte));
            Por(_cabeca, -4f * papo);
        }

        // ------------------------------------------------------------------ a morte (deitado de lado)

        void Morte()
        {
            if (!_raiz.Valida) return;
            float t = _morto;
            if (_sentado > 0.001f && _empe <= 0.001f && t <= 0f && _tipo == Corpo.Quadrupede)
            {
                // sentado (a etapa 5): o corpo inclinado para cima em volta do quadril, a anca baixa (no chão)
                float s = Mathf.SmoothStep(0f, 1f, _sentado);
                var gira = Quaternion.AngleAxis(-36f * s, Vector3.right);
                Vector3 pivo = new Vector3(0f, _altura * 0.25f, -_comprimento * 0.42f);
                _raiz.t.localRotation = gira * _raiz.repouso;
                _raiz.t.localPosition = _raiz.posicao + (pivo - gira * pivo) - Vector3.up * _altura * 0.22f * s;
                return;
            }
            if (_empe > 0.001f && t <= 0f && _tipo == Corpo.Quadrupede)
            {
                // de pé nas patas de trás: gira o corpo para cima em volta do quadril (o nariz para o alto)
                var gira = Quaternion.AngleAxis(-68f * Mathf.SmoothStep(0f, 1f, _empe), Vector3.right);
                Vector3 pivo = new Vector3(0f, _altura * 0.25f, -_comprimento * 0.42f);
                _raiz.t.localRotation = gira * _raiz.repouso;
                _raiz.t.localPosition = _raiz.posicao + (pivo - gira * pivo);
                return;
            }
            if (t <= 0f && _deitado <= 0f)
            {
                _raiz.t.localRotation = _raiz.repouso;
                _raiz.t.localPosition = _raiz.posicao;
                return;
            }
            if (_tipo == Corpo.Quadrupede)
            {
                // gira em volta do meio do corpo, até a lateral encostar no chão (o meio fica a meia largura do chão)
                float h = _altura * 0.8f;
                float ang = 90f * Mathf.SmoothStep(0f, 1f, t);
                var giro = Quaternion.AngleAxis(ang, Vector3.forward);
                Vector3 meio = new Vector3(0f, h, 0f);
                Vector3 pos = meio - giro * meio + new Vector3(0f, (_largura * 0.55f - h) * Mathf.SmoothStep(0f, 1f, t), 0f);
                // deitado (descansando, sem morrer): só baixa o corpo
                pos += Vector3.down * (_altura * 0.42f * _deitado);
                _raiz.t.localRotation = giro * _raiz.repouso;
                _raiz.t.localPosition = _raiz.posicao + pos;
            }
            else
            {
                _raiz.t.localRotation = Quaternion.AngleAxis(90f * t, Vector3.forward) * _raiz.repouso;
                _raiz.t.localPosition = _raiz.posicao + Vector3.up * (_altura * 0.15f * t);
            }
        }
    }
}
