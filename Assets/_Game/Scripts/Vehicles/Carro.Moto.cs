using UnityEngine;

namespace ZombieCityCleaner.Vehicles
{
    /// <summary>
    /// A moto (06/11/2026, a pedido: a Fat Boy e a cross no jogo). É o mesmo <see cref="Carro"/> (a rede, os lugares, a
    /// chave e a ligação direta, o farol, a buzina, o som, a gasolina, a vela e o atropelamento), com duas rodas:
    /// <list type="bullet">
    /// <item>rodas[0] é a da frente (vira) e rodas[1] a de trás (puxa e tem o freio de trás no Espaço);</item>
    /// <item>o corpo da física fica sempre em pé (<see cref="EmPe"/>: um giro que desfaz o tombo para o lado, e as forças das
    /// rodas aplicadas na altura do centro de massa), e o <see cref="modelo"/> inclina nas curvas, pela conta da curva
    /// (a velocidade e o giro do guidão), em todas as máquinas;</item>
    /// <item>o <see cref="guidao"/> (o garfo, o guidão e a roda da frente) gira em volta do eixo da direção, inclinado;</item>
    /// <item>o <see cref="descanso"/> baixa com a moto parada e vazia e recolhe com alguém nela;</item>
    /// <item>sem pneus, bateria e radiador (a pedido: "na moto, apenas a vela estará comprometida"): a bateria e o radiador
    /// ficam em 100% e não gastam, os pneus não gastam nem furam, e as batidas não estragam nada. A vela se troca agachado
    /// perto do motor (<see cref="MotorDaMoto"/>, que abre a <see cref="Player.TelaDoCapo"/> no <see cref="cofre"/> da moto).</item>
    /// </list>
    /// </summary>
    public partial class Carro
    {
        [Header("Moto (duas rodas; vazio nos carros)")]
        [Tooltip("Uma moto: duas rodas (rodas[0] a da frente, que vira; rodas[1] a de trás, que puxa), o corpo da física " +
                 "sempre em pé e o modelo que inclina nas curvas. Sem pneus, bateria e radiador: só a vela e a gasolina.")]
        public bool moto;
        [Tooltip("O que inclina nas curvas (o modelo, os lugares, os punhos e as pedaleiras): a origem no chão, no meio entre as rodas.")]
        public Transform modelo;
        [Tooltip("A direção (o garfo, o guidão e a roda da frente): gira em volta do Y dela (o eixo da direção, inclinado).")]
        public Transform guidao;
        [Tooltip("O descanso lateral (a origem na dobradiça): baixado com a moto parada e vazia, recolhido com alguém nela.")]
        public Transform descanso;
        [Tooltip("O pé do descanso baixado, no espaço do pai dele (para a conta do giro que recolhe).")]
        public Vector3 peDoDescanso = new Vector3(-0.3f, 0.01f, 0f);
        [Tooltip("Para onde o descanso recolhido aponta, da dobradiça (no espaço do pai dele): para trás, quase deitado.")]
        public Vector3 descansoRecolhido = new Vector3(-0.12f, 0.1f, -1f);
        [Tooltip("Os punhos (0 o direito, 1 o esquerdo): onde o piloto põe as mãos. Filhos da direção.")]
        public Transform[] punhos;
        [Tooltip("Onde ficam os pés de cada lugar (o piloto: 0 o direito, 1 o esquerdo; o garupa: 2 e 3).")]
        public Transform[] pedaleiras;
        [Tooltip("O quanto o piloto deita o tronco para a frente (graus): a cross mais, a Fat Boy quase nada.")]
        public float troncoDoPiloto = 6f;
        [Tooltip("A lanterna de trás, quando ela fica numa malha que não é a lataria (na moto, o farol fica na direção e a lanterna no corpo).")]
        public MeshRenderer lanterna2;
        public int submeshLanterna2 = -1;
        public Material lanterna2Acesa;
        [Tooltip("A inclinação máxima nas curvas (graus; só o modelo).")]
        public float inclinacaoMaxima = 38f;
        [Tooltip("O quanto o corpo da física volta a ficar em pé (rad/s² por radiano de tombo) e o amortecimento.")]
        public float ficarEmPe = 500f, amortecerEmPe = 45f;
        [Tooltip("O centro de massa da moto (no espaço dela).")]
        public Vector3 centroDaMoto = new Vector3(0f, 0.48f, -0.05f);
        [Tooltip("Onde fica o motor, no espaço do veículo (a tela do motor fecha longe dele). Nos carros, o meio do capô.")]
        public Vector3 pontoDoMotor = new Vector3(0f, 0.9f, 1.6f);
        [Tooltip("O nome do veículo, no alto da tela do capô (ou do motor).")]
        public string nomeDoVeiculo = "Perua da família";
        [Tooltip("Onde fica a ignição, para a dica de quem senta no lugar do motorista.")]
        public string ondeFicaAIgnicao = "à direita do volante";

        [Header("O som próprio do motor (as motos; vazio, o laço do carro)")]
        [Tooltip("Os laços do motor, do giro lento ao rápido (Ferramentas/motos.py). Tocam os dois mais perto do giro de agora, " +
                 "misturados, cada um com o pitch perto de 1: o tom do escape não estica como no laço único do carro.")]
        public AudioClip[] lacosDoMotor;
        [Tooltip("O giro de cada laço (vezes a marcha lenta), na mesma ordem.")]
        public float[] giroDosLacos;
        [Tooltip("O giro (vezes a marcha lenta) com a rotação no máximo.")]
        public float giroNoMaximo = 3.6f;
        [Tooltip("O motor pegando, no lugar do do carro.")]
        public AudioClip pegarProprio;

        float _inclinacaoVista, _giroDaFrente, _giroDeTras, _descansoVisto = 1f;
        Quaternion _guidaoRepouso = Quaternion.identity, _modeloRepouso = Quaternion.identity, _descansoRepouso = Quaternion.identity, _descansoGuardado;
        Quaternion _rodaFrenteRepouso = Quaternion.identity, _rodaTrasRepouso = Quaternion.identity;
        Material[] _mats2Apagados, _mats2Acesos;

        /// <summary>Na moto, "a moto"; no carro, "o carro" (para os avisos).</summary>
        public string OVeiculo => moto ? "a moto" : "o carro";

        // no Awake (depois do do carro): o centro de massa, os repousos das peças que mexem e a lanterna de trás
        void PrepararMoto()
        {
            if (!moto) return;
            _corpo.centerOfMass = centroDaMoto;
            _corpo.maxAngularVelocity = 12f;
            if (modelo != null) _modeloRepouso = modelo.localRotation;
            if (guidao != null) _guidaoRepouso = guidao.localRotation;
            if (descanso != null)
            {
                _descansoRepouso = descanso.localRotation;
                // o giro que leva o descanso baixado (da dobradiça até o pé) para o recolhido
                Vector3 baixado = peDoDescanso - descanso.localPosition;
                _descansoGuardado = Quaternion.FromToRotation(baixado, descansoRecolhido) * _descansoRepouso;
            }
            if (rodasVisuais != null && rodasVisuais.Length >= 2)
            {
                if (rodasVisuais[0] != null) _rodaFrenteRepouso = rodasVisuais[0].localRotation;
                if (rodasVisuais[1] != null) _rodaTrasRepouso = rodasVisuais[1].localRotation;
            }
            if (lanterna2 != null && submeshLanterna2 >= 0 && lanterna2Acesa != null)
            {
                _mats2Apagados = lanterna2.sharedMaterials;
                _mats2Acesos = (Material[])_mats2Apagados.Clone();
                if (submeshLanterna2 < _mats2Acesos.Length) _mats2Acesos[submeshLanterna2] = lanterna2Acesa;
            }
        }

        /// <summary>Este veículo tem os laços próprios do motor (as motos).</summary>
        bool MotorEmCamadas => lacosDoMotor != null && giroDosLacos != null && lacosDoMotor.Length >= 2 && giroDosLacos.Length == lacosDoMotor.Length;

        AudioSource[] _camadas;
        float _giroOuvido = 1f;

        // os AudioSources dos laços: o "Motor" do prefab é o primeiro, e os outros são cópias dele (no mesmo objeto)
        void CriarCamadas()
        {
            _camadas = new AudioSource[lacosDoMotor.Length];
            for (int i = 0; i < _camadas.Length; i++)
            {
                var s = i == 0 ? motor : motor.gameObject.AddComponent<AudioSource>();
                if (i > 0)
                {
                    s.playOnAwake = false;
                    s.spatialBlend = motor.spatialBlend;
                    s.rolloffMode = motor.rolloffMode;
                    s.minDistance = motor.minDistance;
                    s.maxDistance = motor.maxDistance;
                    s.dopplerLevel = motor.dopplerLevel;
                    s.outputAudioMixerGroup = motor.outputAudioMixerGroup;
                    s.priority = motor.priority;
                }
                s.loop = true;
                s.clip = lacosDoMotor[i];
                s.volume = 0f;
                _camadas[i] = s;
            }
        }

        /// <summary>
        /// O motor da moto, em camadas (em cada máquina): o giro (vezes a marcha lenta) vem da rotação (0,12 parada, sem
        /// acelerar, até 0,98 no máximo), sobe rápido e cai devagar (as aceleradas da cross); os dois laços em volta dele
        /// tocam juntos, misturados com a mesma potência na escala do log do giro, cada um com o pitch giro / o giro dele.
        /// Desligado, o giro cai e o volume some, como no carro.
        /// </summary>
        void SomEmCamadas(float dt)
        {
            if (_camadas == null) CriarCamadas();
            bool ligado = MotorLigado;
            float rot = IsOwner ? _rotacaoLocal : _rotacao.Value / 255f;
            float alvoGiro = ligado ? 1f + Mathf.Max(0f, rot - 0.12f) / 0.86f * (giroNoMaximo - 1f) : 0.55f;
            float rapidez = alvoGiro > _giroOuvido ? 7f : ligado ? 2.6f : 1.5f;
            _giroOuvido = Mathf.Lerp(_giroOuvido, alvoGiro, 1f - Mathf.Exp(-rapidez * dt));
            float alvo = ligado && Time.time > _pegouEm + SomDoCarro.LacoEntra ? 0.45f + rot * 0.45f : 0f;
            _volumeDoMotor = Mathf.MoveTowards(_volumeDoMotor, alvo, dt * (alvo > 0f ? 1.5f : 1.1f));
            float g = Mathf.Max(_giroOuvido, giroDosLacos[0] * 0.4f);
            int i = 0;
            while (i < giroDosLacos.Length - 2 && g > giroDosLacos[i + 1]) i++;
            float x = Mathf.Clamp01(Mathf.InverseLerp(Mathf.Log(giroDosLacos[i]), Mathf.Log(giroDosLacos[i + 1]), Mathf.Log(g)));
            for (int j = 0; j < _camadas.Length; j++)
            {
                var s = _camadas[j];
                if (s == null) continue;
                float peso = j == i ? Mathf.Cos(x * Mathf.PI * 0.5f) : j == i + 1 ? Mathf.Sin(x * Mathf.PI * 0.5f) : 0f;
                s.volume = _volumeDoMotor * peso;
                s.pitch = Mathf.Clamp(g / Mathf.Max(0.01f, giroDosLacos[j]), 0.3f, 3f);
                if (s.volume > 0.001f && !s.isPlaying) s.Play();
                else if (s.volume <= 0.001f && s.isPlaying) s.Stop();
            }
        }

        // a lanterna de trás da moto acende e apaga com o farol (Carro.AplicarFarois)
        void AplicarLanterna2(bool ligados)
        {
            if (lanterna2 != null && _mats2Apagados != null) lanterna2.sharedMaterials = ligados ? _mats2Acesos : _mats2Apagados;
        }

        /// <summary>
        /// No dono, a cada passo da física: pilotar a moto. O guidão vira menos quanto mais rápido; o acelerador puxa a roda
        /// de trás (só com o motor ligado); o freio (S) segura as duas; a ré é devagar, como empurrar a moto; o Espaço trava
        /// a roda de trás (ela escorrega de lado, rápida). O corpo fica em pé (<see cref="EmPe"/>).
        /// </summary>
        void DirigirMoto(float dt)
        {
            if (rodas == null || rodas.Length < 2 || rodas[0] == null || rodas[1] == null) return;
            MedirBatida();
            bool comMotorista = IsSpawned && _ocupantes.Value[0] != 0;
            float acelerar = comMotorista ? _acelerar : 0f, virar = comMotorista ? _virar : 0f, mao = comMotorista ? _maoPuxada : 0f;
            float v = Vector3.Dot(_corpo.linearVelocity, transform.forward);
            var frente = rodas[0];
            var tras = rodas[1];

            float max = Mathf.Lerp(giroMaximo, giroNaVelocidadeMaxima, Mathf.InverseLerp(0f, velocidadeMaxima * 0.75f, Mathf.Abs(v)));
            float rapidez = Mathf.Abs(virar) < 0.05f ? velocidadeDoVolante * 1.6f : velocidadeDoVolante;
            _giro = Mathf.MoveTowards(_giro, virar * max, rapidez * dt);
            frente.steerAngle = _giro;
            tras.steerAngle = 0f;

            bool ligado = MotorLigado;
            float forca = 0f, frearF = 0f, frearT = 0f;
            float torque = this.torque * Pecas.FatorDeForca * FatorDoEngasgo();
            if (acelerar > 0.05f)
            {
                if (v < -0.5f) frearF = frearT = freio * acelerar;
                else if (ligado) forca = torque * acelerar * Mathf.Clamp01(1f - v / velocidadeMaxima);
                else frearT = Mathf.Abs(v) < 0.4f ? 200f : 30f;
            }
            else if (acelerar < -0.05f)
            {
                if (v > 0.5f || !ligado)
                {
                    frearF = freio * 0.65f * -acelerar;
                    frearT = freio * 0.45f * -acelerar;
                }
                else forca = -torque * 0.3f * -acelerar * Mathf.Clamp01(1f + v / velocidadeDeRe);
            }
            else if (ligado) frearT = Mathf.Abs(v) < 0.4f ? 200f : 35f;   // o freio-motor; quase parada, segura
            else frearT = Mathf.Abs(v) < 0.4f ? 300f : 40f;
            // sem ninguém, freada (como o carro sem motorista)
            if (!comMotorista)
            {
                frearF = Mathf.Max(frearF, freio);
                frearT = Mathf.Max(frearT, freio);
            }
            frearT += mao * freioDeMao;
            frente.motorTorque = 0f;
            tras.motorTorque = forca;
            frente.brakeTorque = frearF;
            tras.brakeTorque = frearT;
            // o Espaço, rápido: a roda de trás travada solta de lado
            var lado = tras.sidewaysFriction;
            float rigidez = mao > 0f && Mathf.Abs(v) > 3f ? 0.9f : 2f;
            if (!Mathf.Approximately(lado.stiffness, rigidez))
            {
                lado.stiffness = rigidez;
                tras.sidewaysFriction = lado;
            }

            EmPe();
            _corpo.AddForce(-transform.up * Mathf.Abs(v) * 6f);

            if (IsSpawned)
            {
                var direcao = (sbyte)Mathf.RoundToInt(Mathf.Clamp(_giro / giroMaximo, -1f, 1f) * 127f);
                if (direcao != _direcao.Value) _direcao.Value = direcao;
                _rotacaoLocal = ligado ? Rotacao(Mathf.Abs(v), Mathf.Abs(acelerar)) * (Engasgando ? 0.55f : 1f) : 0f;
                byte rot = (byte)Mathf.RoundToInt(_rotacaoLocal * 255f);
                if (Mathf.Abs(rot - _rotacao.Value) > 4 && Time.time - _enviouRotacao > 0.1f)
                {
                    _rotacao.Value = rot;
                    _enviouRotacao = Time.time;
                }
            }
            if (comMotorista) Atropelar(v);
        }

        /// <summary>
        /// O corpo da física em pé: um giro em volta da frente da moto desfaz o tombo para o lado (o "para cima" do mundo,
        /// sem a parte da frente: a moto ainda sobe e desce ladeiras). Quem inclina nas curvas é só o modelo.
        /// </summary>
        void EmPe()
        {
            Vector3 f = transform.forward;
            Vector3 alvo = Vector3.ProjectOnPlane(Vector3.up, f);
            if (alvo.sqrMagnitude < 0.01f) return;
            alvo.Normalize();
            float tombo = Vector3.SignedAngle(transform.up, alvo, f) * Mathf.Deg2Rad;
            float giro = Vector3.Dot(_corpo.angularVelocity, f);
            _corpo.AddTorque(f * (tombo * ficarEmPe - giro * amortecerEmPe), ForceMode.Acceleration);
        }

        /// <summary>
        /// Em todas as máquinas, a cada quadro: a moto vista. O modelo inclina na curva (a conta da curva: tan = v² tan(giro)
        /// / (g × entre-eixos), quase nada parada), o guidão gira em volta do eixo da direção, as rodas giram pela
        /// velocidade e o descanso baixa (parada e vazia) ou recolhe.
        /// </summary>
        void VisualDaMoto(float dt)
        {
            float giro = IsOwner ? _giro : _direcao.Value / 127f * giroMaximo;
            float v = VelocidadeParaFrente;
            float entre = 1.5f;
            if (rodas != null && rodas.Length >= 2 && rodas[0] != null && rodas[1] != null)
                entre = Mathf.Max(0.8f, Mathf.Abs(rodas[0].transform.localPosition.z - rodas[1].transform.localPosition.z));
            float alvo = -Mathf.Atan(v * v * Mathf.Tan(giro * Mathf.Deg2Rad) / (9.81f * entre)) * Mathf.Rad2Deg;
            alvo *= Mathf.Clamp01(Mathf.Abs(v) / 2f);
            alvo = Mathf.Clamp(alvo, -inclinacaoMaxima, inclinacaoMaxima);
            _inclinacaoVista = Mathf.Lerp(_inclinacaoVista, alvo, 1f - Mathf.Exp(-5f * dt));
            if (modelo != null) modelo.localRotation = _modeloRepouso * Quaternion.Euler(0f, 0f, _inclinacaoVista);
            if (guidao != null) guidao.localRotation = _guidaoRepouso * Quaternion.Euler(0f, giro, 0f);

            // as rodas giram em volta do X do pai (o eixo delas)
            if (rodasVisuais != null && rodasVisuais.Length >= 2)
            {
                float rf = rodas != null && rodas.Length > 0 && rodas[0] != null ? rodas[0].radius : 0.33f;
                float rt = rodas != null && rodas.Length > 1 && rodas[1] != null ? rodas[1].radius : 0.33f;
                _giroDaFrente = Mathf.Repeat(_giroDaFrente + v * dt / rf * Mathf.Rad2Deg, 360f);
                _giroDeTras = Mathf.Repeat(_giroDeTras + v * dt / rt * Mathf.Rad2Deg, 360f);
                if (rodasVisuais[0] != null) rodasVisuais[0].localRotation = Quaternion.AngleAxis(_giroDaFrente, Vector3.right) * _rodaFrenteRepouso;
                if (rodasVisuais[1] != null) rodasVisuais[1].localRotation = Quaternion.AngleAxis(_giroDeTras, Vector3.right) * _rodaTrasRepouso;
            }

            // o descanso: baixa com a moto quase parada e vazia (meio segundo), recolhe com alguém nela
            if (descanso != null)
            {
                bool baixar = Vazio && Mathf.Abs(v) < 0.6f;
                _descansoVisto = Mathf.MoveTowards(_descansoVisto, baixar ? 1f : 0f, dt / 0.45f);
                float k = Mathf.SmoothStep(0f, 1f, _descansoVisto);
                descanso.localRotation = Quaternion.Slerp(_descansoGuardado, _descansoRepouso, k);
            }
        }
    }
}
