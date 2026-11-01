using Unity.Netcode;
using UnityEngine;
using ZombieCityCleaner.Combat;
using ZombieCityCleaner.Loot;
using ZombieCityCleaner.Player;
using ZombieCityCleaner.Zombies;

namespace ZombieCityCleaner.Vehicles
{
    /// <summary>
    /// Os números do desgaste do carro e da gasolina (01/11/2026, a pedido). Ficam no prefab de cada carro
    /// (<see cref="Carro.desgaste"/>).
    /// </summary>
    [System.Serializable]
    public class AjustesDoDesgaste
    {
        [Header("Pneus: o desgaste")]
        [Tooltip("Quantos km um pneu de resistência 1 roda até ficar careca, rodando normal (sem derrapar, no asfalto).")]
        public float kmAteCareca = 60f;
        [Tooltip("O desgaste sozinho não passa disto (%): careca, o pneu não fura por gasto; quem fura são os furos.")]
        public int pisoDoDesgaste = 12;
        [Tooltip("A resistência sorteada de cada pneu posto no carro (o desgaste é dividido por ela).")]
        public Vector2 resistenciaDoPneu = new Vector2(0.7f, 1.3f);
        [Tooltip("Os da frente (a direção e a tração) gastam mais; os de trás, menos.")]
        public float naFrente = 1.2f, atras = 0.85f;
        [Tooltip("Quanto a derrapagem (a roda escorregando de lado ou patinando) multiplica o desgaste.")]
        public float derrapar = 5f;
        [Tooltip("Fora do asfalto (longe das estradas e fora das cidades), o desgaste multiplica por isto.")]
        public float foraDoAsfalto = 1.6f;
        [Tooltip("Rodar com o pneu furado acaba com ele: cada 100 m tira isto (%), até rasgar (0%: sem remendo).")]
        public float furadoPor100m = 3f;

        [Header("Pneus: os furos (sorteados, cada pneu por si)")]
        [Tooltip("Um pneu bom (100%) fura, em média, uma vez a cada tantos km.")]
        public float kmPorFuroNovo = 90f;
        [Tooltip("Um pneu gasto (30% ou menos) fura, em média, uma vez a cada tantos km.")]
        public float kmPorFuroGasto = 7f;
        [Tooltip("Depois de uma falha (um furo, o radiador vazando), quantos segundos dirigindo sem outra sorteada. Os tiros, a gosma e as batidas não esperam.")]
        public float carencia = 180f;
        [Tooltip("A chance de o furo ser um estouro (na hora) num pneu bom e devagar; o pneu gasto e a velocidade aumentam. O resto é o furo lento.")]
        public float chanceDeEstouro = 0.15f;
        [Tooltip("O furo lento: em quantos segundos o pneu murcha.")]
        public Vector2 tempoDoFuroLento = new Vector2(40f, 60f);
        [Tooltip("Atropelar um zumbi: a chance de furar o pneu da frente daquele lado (os ossos).")]
        public float furoNoAtropelo = 0.04f;

        [Header("Motor")]
        [Tooltip("Quantos minutos de motor ligado a vela de resistência 1 dura (de 100 a 0%).")]
        public float minutosDaVela = 150f;
        [Tooltip("Quantos minutos de motor ligado o radiador de resistência 1 dura (de 100 a 0%).")]
        public float minutosDoRadiador = 300f;
        [Tooltip("A resistência sorteada da vela e do radiador.")]
        public Vector2 resistenciaDaPeca = new Vector2(0.7f, 1.3f);
        [Tooltip("Abaixo disto (%), a vela gasta faz o motor engasgar de vez em quando (mais, quanto pior).")]
        public int velaQueEngasga = 35;
        [Tooltip("O radiador vazando perde isto (% por minuto de motor ligado).")]
        public float vazamentoPorMinuto = 10f;
        [Tooltip("Um radiador bom começa a vazar sozinho, em média, uma vez a cada tantos minutos de motor ligado (o gasto, até 6 vezes mais).")]
        public float minutosPorVazamento = 360f;

        [Header("Batidas")]
        [Tooltip("A partir desta mudança de velocidade numa batida (m/s), ela estraga alguma coisa: de frente, o radiador; do lado de uma roda, o pneu.")]
        public float batidaQueEstraga = 5f;

        [Header("Gasolina")]
        [Tooltip("O tanque (litros).")]
        public float tanque = 45f;
        [Tooltip("O gasto em marcha lenta e no giro máximo (litros por minuto de motor ligado).")]
        public float gastoParado = 0.12f, gastoNoMaximo = 2.4f;
        [Tooltip("Com menos que isto (litros), a gasolina está na reserva: o motor engasga de vez em quando.")]
        public float reserva = 3f;
    }

    /// <summary>
    /// O desgaste do carro e a gasolina (01/11/2026, a pedido: "precisamos colocar um sistema onde isso poderia ocorrer [...]
    /// porém precisamos pensar em um sistema que não ocorra tudo de uma única vez"). A falha não vem de um limite de km (os 4
    /// pneus chegariam juntos): o desgaste é lento e diferente em cada peça, e a falha é um sorteio cuja chance cresce com o
    /// desgaste.
    /// <para>
    /// - <b>Os pneus gastam</b> rodando (no dono, que simula: cada roda pela distância, a derrapagem, o eixo e o chão; manda
    ///   ao anfitrião a cada segundo, <see cref="RodouRpc"/>), divididos pela resistência sorteada de cada pneu posto. O
    ///   desgaste para em <see cref="AjustesDoDesgaste.pisoDoDesgaste"/>: careca não fura sozinho.
    /// - <b>Os furos são sorteados</b> por km, cada pneu por si; a chance cresce do pneu bom ao gasto. Depois de uma falha, a
    ///   <see cref="AjustesDoDesgaste.carencia"/> (segundos dirigindo) segura a próxima: um problema por vez. Atropelar um
    ///   zumbi também sorteia um furo no pneu da frente daquele lado (com a carência). O tiro (5%: tem remendo), a batida e
    ///   a gosma não esperam.
    /// - <b>O furo lento</b> (o mais comum) murcha o pneu em 40 a 60 s, com o chiado do ar (todos ouvem): dá tempo de
    ///   escolher onde parar. <b>O estouro</b> (mais provável no pneu gasto e rápido) é na hora, rasga o pneu (0%: sem
    ///   remendo) e chama os zumbis em volta, como um tiro.
    /// - <b>Rodar furado</b> acaba com o pneu (até 0%). O pneu furado (de 1 a 9%) tem remendo: tirado com o macaco, na
    ///   mochila, o kit de reparo o deixa com 40% (<see cref="InventarioJogador.Remendar"/>).
    /// - <b>A vela e o radiador gastam</b> com o motor ligado (cada um com a sua resistência sorteada). A vela gasta faz o
    ///   motor engasgar (perde a força por um instante; a vela abaixo de 10% não pega mais). O radiador pode começar a vazar
    ///   (sorteado, ou numa batida de frente): vazando, ele perde 10% por minuto e o motor esquenta até ferver.
    /// - <b>As batidas</b> (no dono, <see cref="OnCollisionEnter"/>): de frente, estragam o radiador e podem furá-lo; do lado
    ///   de uma roda, podem estourar o pneu.
    /// - <b>A gasolina</b> (<see cref="Gasolina"/>, em litros, do anfitrião) gasta com o motor ligado, pelo giro. Na reserva,
    ///   o motor engasga; sem gasolina, apaga e não pega. Abastece com o galão, na tampa do tanque
    ///   (<see cref="TanqueDoCarro"/>, <see cref="PedirAbastecer"/>).
    /// </para>
    /// </summary>
    public partial class Carro
    {
        [Header("O desgaste e a gasolina (01/11/2026)")]
        public AjustesDoDesgaste desgaste = new AjustesDoDesgaste();
        [Tooltip("A tampa do tanque, no espaço do carro: onde a mira abastece com o galão.")]
        public Vector3 tampaDoTanque = new Vector3(-0.862f, 0.88f, -1.62f);
        [Tooltip("Desenha a tampa (um disco escuro): para o carro que não a tem no modelo, como a perua.")]
        public bool desenharTampa = true;

        /// <summary>Os nomes das rodas, na ordem dos WheelColliders.</summary>
        public static readonly string[] NomesDasRodas = { "dianteiro esquerdo", "dianteiro direito", "traseiro esquerdo", "traseiro direito" };

        readonly NetworkVariable<float> _gasolina = new(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        // o que vaza agora (o chiado, em todas as máquinas): os bits 0 a 3, os pneus murchando; o 4, o radiador
        readonly NetworkVariable<byte> _vazando = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        const int BitDoRadiador = 1 << 4;

        // no anfitrião
        readonly float[] _resistenciaDoPneu = { 1f, 1f, 1f, 1f }, _gastoDoPneu = new float[4], _murcha = new float[4], _restoDaMurcha = new float[4];
        float _resistenciaDaVela = 1f, _resistenciaDoRadiador = 1f, _gastoDaVela, _gastoDoRadiador, _litros, _carencia;
        // no dono (quem simula)
        readonly float[] _rodou = new float[4];
        float _metros, _enviouRodou, _engasgoAte, _proximoEngasgo = -1f;
        // em todas as máquinas
        AudioSource _somDoAr, _somDoVapor;

        /// <summary>A gasolina no tanque (litros).</summary>
        public float Gasolina => IsSpawned ? _gasolina.Value : combustivel * desgaste.tanque;
        public float Tanque => desgaste.tanque;
        public bool SemGasolina => IsSpawned && _gasolina.Value <= 0.01f;
        public bool NaReserva => IsSpawned && _gasolina.Value < desgaste.reserva;
        /// <summary>Os pneus murchando agora (um bit por roda).</summary>
        public int PneusMurchando => IsSpawned ? _vazando.Value & 15 : 0;
        public bool RadiadorVazando => IsSpawned && (_vazando.Value & BitDoRadiador) != 0;
        /// <summary>Quando o motor engasgou por último (em todas as máquinas; o aviso do motorista).</summary>
        public float UltimoEngasgo { get; private set; } = -99f;
        /// <summary>Segundos dirigindo até a próxima falha sorteada poder acontecer (no anfitrião; o F1 mostra).</summary>
        public float Carencia => _carencia;
        /// <summary>O ponto da tampa do tanque, no mundo.</summary>
        public Vector3 PontoDoTanque => transform.TransformPoint(tampaDoTanque);
        bool Engasgando => Time.time < _engasgoAte;

        // ------------------------------------------------------------------------------------------ o começo

        // no Awake: a mira da tampa do tanque (um gatilho com o TanqueDoCarro), a tampa desenhada e os sons do ar e do vapor
        void PrepararDesgaste()
        {
            var tampa = new GameObject("Tanque (mira)") { layer = CamadaVeiculo };
            tampa.transform.SetParent(transform, false);
            tampa.transform.localPosition = tampaDoTanque + new Vector3(Mathf.Sign(tampaDoTanque.x) * 0.04f, 0f, 0f);
            var caixa = tampa.AddComponent<BoxCollider>();
            caixa.isTrigger = true;
            caixa.size = new Vector3(0.14f, 0.3f, 0.34f);
            tampa.AddComponent<TanqueDoCarro>().carro = this;
            if (desenharTampa)
            {
                var disco = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                disco.name = "Tampa do tanque";
                Object.Destroy(disco.GetComponent<Collider>());
                disco.transform.SetParent(transform, false);
                disco.transform.localPosition = tampaDoTanque;
                disco.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                disco.transform.localScale = new Vector3(0.11f, 0.008f, 0.11f);
                var r = disco.GetComponent<MeshRenderer>();
                if (carroceria != null && carroceria.sharedMaterials.Length > 0) r.sharedMaterial = carroceria.sharedMaterials[0];
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var cor = new MaterialPropertyBlock();
                cor.SetColor("_BaseColor", new Color(0.12f, 0.12f, 0.12f));
                r.SetPropertyBlock(cor);
            }
            _somDoAr = NovoSom("Ar do pneu", SomDoCarro.ArVazando);
            _somDoVapor = NovoSom("Vapor do radiador", SomDoCarro.Vapor);
        }

        AudioSource NovoSom(string nome, AudioClip clip)
        {
            var go = new GameObject(nome);
            go.transform.SetParent(transform, false);
            var s = go.AddComponent<AudioSource>();
            s.clip = clip;
            s.loop = true;
            s.playOnAwake = false;
            s.spatialBlend = 1f;
            s.rolloffMode = AudioRolloffMode.Linear;
            s.minDistance = 1.5f;
            s.maxDistance = 28f;
            s.volume = 0f;
            return s;
        }

        // no anfitrião, ao nascer: a gasolina e as resistências sorteadas
        void DesgasteAoNascer()
        {
            _litros = Mathf.Clamp01(combustivel) * desgaste.tanque;
            _gasolina.Value = _litros;
            _vazando.Value = 0;
            for (int i = 0; i < PneusDoCarro.Quantas; i++) NovoPneu(i);
            NovaPeca(PecasDoMotor.Vela);
            NovaPeca(PecasDoMotor.Radiador);
            _carencia = 0f;
        }

        /// <summary>No anfitrião: um pneu novo nesta roda (posto, ou o do começo): a resistência dele, sem furo nem murcha.</summary>
        void NovoPneu(int roda)
        {
            _resistenciaDoPneu[roda] = Random.Range(desgaste.resistenciaDoPneu.x, desgaste.resistenciaDoPneu.y);
            _gastoDoPneu[roda] = 0f;
            PararDeMurchar(roda);
        }

        /// <summary>No anfitrião: uma peça nova no motor (posta, ou a do começo): a resistência dela; o radiador novo não vaza.</summary>
        void NovaPeca(int peca)
        {
            float r = Random.Range(desgaste.resistenciaDaPeca.x, desgaste.resistenciaDaPeca.y);
            if (peca == PecasDoMotor.Vela)
            {
                _resistenciaDaVela = r;
                _gastoDaVela = 0f;
            }
            else if (peca == PecasDoMotor.Radiador)
            {
                _resistenciaDoRadiador = r;
                _gastoDoRadiador = 0f;
                if ((_vazando.Value & BitDoRadiador) != 0) _vazando.Value = (byte)(_vazando.Value & ~BitDoRadiador);
            }
        }

        void PararDeMurchar(int roda)
        {
            _murcha[roda] = 0f;
            _restoDaMurcha[roda] = 0f;
            int bit = 1 << roda;
            if ((_vazando.Value & bit) != 0) _vazando.Value = (byte)(_vazando.Value & ~bit);
        }

        // ------------------------------------------------------------------------------------------ no dono (quem simula)

        /// <summary>
        /// O engasgo (no dono, a cada passo da física): a vela gasta ou a gasolina na reserva cortam a força por um instante,
        /// de tempos em tempos (mais seguido quanto pior). Devolve o quanto da força sobra (1, normal).
        /// </summary>
        float FatorDoEngasgo()
        {
            if (!IsSpawned || !MotorLigado)
            {
                _proximoEngasgo = -1f;
                return 1f;
            }
            float agora = Time.time;
            if (agora < _engasgoAte) return 0.12f;
            var a = desgaste;
            float gravidade = 0f;
            int vela = Pecas.Estado(PecasDoMotor.Vela);
            if (vela < a.velaQueEngasga) gravidade = Mathf.InverseLerp(a.velaQueEngasga, PecasDoMotor.VelaMinima, vela);
            if (Gasolina < a.reserva) gravidade = Mathf.Max(gravidade, 0.25f + 0.75f * Mathf.InverseLerp(a.reserva, 0f, Gasolina));
            if (gravidade <= 0f)
            {
                _proximoEngasgo = -1f;
                return 1f;
            }
            if (_proximoEngasgo < 0f) _proximoEngasgo = agora + Mathf.Lerp(Random.Range(8f, 20f), Random.Range(2f, 5f), gravidade);
            if (agora < _proximoEngasgo) return 1f;
            _engasgoAte = agora + Random.Range(0.35f, 0.8f);
            _proximoEngasgo = _engasgoAte + Mathf.Lerp(Random.Range(18f, 35f), Random.Range(3f, 7f), gravidade);
            EngasgouRpc();
            return 0.12f;
        }

        [Rpc(SendTo.Everyone)]
        void EngasgouRpc()
        {
            UltimoEngasgo = Time.time;
            Efeitos.Som(SomDoCarro.Tossida, motor != null ? motor.transform.position : transform.position, 0.75f, true, 35f);
        }

        /// <summary>
        /// No dono, a cada passo da física: quanto cada roda rodou, pesado pela derrapagem e pelo eixo (os metros "de
        /// desgaste"), e quanto o carro andou. A cada segundo, manda ao anfitrião (o chão de agora pesa o segundo inteiro).
        /// </summary>
        void RodarNoDono(float dt)
        {
            if (!IsSpawned || rodas == null || rodas.Length < 4) return;
            var a = desgaste;
            for (int i = 0; i < 4; i++)
            {
                if (!rodas[i].GetGroundHit(out var chao)) continue;
                float d = _corpo.GetPointVelocity(chao.point).magnitude * dt;
                // patinando parado (o pneu girando no lugar) também gasta
                float giro = Mathf.Abs(rodas[i].rpm) * 2f * Mathf.PI * rodas[i].radius / 60f * dt;
                float escorrega = Mathf.Clamp01(Mathf.Abs(chao.sidewaysSlip) * 1.2f + Mathf.Abs(chao.forwardSlip) * 0.6f);
                _rodou[i] += Mathf.Max(d, giro * 0.5f) * (1f + a.derrapar * escorrega) * (i < 2 ? a.naFrente : a.atras);
            }
            _metros += _corpo.linearVelocity.magnitude * dt;
            if (Time.time - _enviouRodou < 1f) return;
            _enviouRodou = Time.time;
            if (_metros < 0.3f && _rodou[0] + _rodou[1] + _rodou[2] + _rodou[3] < 0.3f) return;
            float chao2 = ForaDoAsfalto(transform.position) ? a.foraDoAsfalto : 1f;
            RodouRpc(Dm(_rodou[0] * chao2), Dm(_rodou[1] * chao2), Dm(_rodou[2] * chao2), Dm(_rodou[3] * chao2), Dm(_metros));
            for (int i = 0; i < 4; i++) _rodou[i] = 0f;
            _metros = 0f;
        }

        static ushort Dm(float metros) => (ushort)Mathf.Clamp(Mathf.RoundToInt(metros * 10f), 0, 65535);

        /// <summary>Longe das estradas e fora das cidades (a terra e a grama do mundo gerado).</summary>
        static bool ForaDoAsfalto(Vector3 p)
        {
            var d = World.MundoDaPartida.Instancia != null ? World.MundoDaPartida.Instancia.Dados : null;
            if (d == null || d.Rede == null) return false;
            if (d.CidadeEm(p.x, p.z) != null) return false;
            return d.Rede.MaisPerto(p.x, p.z, out float distancia, out _) < 0 || distancia > 5.5f;
        }

        /// <summary>Um zumbi atropelado (no dono): os ossos podem furar o pneu da frente daquele lado.</summary>
        void Atropelou(Vector3 ondeEstava)
        {
            bool direita = transform.InverseTransformPoint(ondeEstava).x > 0f;
            AtropelouRpc((byte)(direita ? 1 : 0));
        }

        // as batidas (no dono, que simula): onde bateu e quanto a velocidade do carro mudou nos 0,15 s seguintes (o
        // impulso do primeiro contato é só uma parte: a física espalha a freada da batida por alguns passos)
        Vector3 _velDoPasso, _velDaBatida, _pontoDaBatida;
        float _batidaAte = -1f, _maiorMudanca;

        void OnCollisionEnter(Collision c)
        {
            if (!IsSpawned || !Simula || c.contactCount == 0 || _batidaAte > 0f) return;
            int camada = c.collider.gameObject.layer;
            if (camada == Zumbi.CamadaZumbi || camada == JogadorRede.CamadaJogador) return;
            var contato = c.GetContact(0);
            if (Mathf.Abs(contato.normal.y) > 0.75f) return;   // caindo de pé no chão (um pulo): não estraga
            _velDaBatida = _velDoPasso;
            _pontoDaBatida = transform.InverseTransformPoint(contato.point);
            _maiorMudanca = 0f;
            _batidaAte = Time.time + 0.15f;
        }

        /// <summary>No dono, no começo de cada passo da física: mede a batida em curso e guarda a velocidade do passo.</summary>
        void MedirBatida()
        {
            if (_batidaAte > 0f)
            {
                _maiorMudanca = Mathf.Max(_maiorMudanca, (_velDaBatida - _corpo.linearVelocity).magnitude);
                if (Time.time >= _batidaAte)
                {
                    _batidaAte = -1f;
                    if (_maiorMudanca >= desgaste.batidaQueEstraga) BateuRpc(_pontoDaBatida, _maiorMudanca);
                }
            }
            _velDoPasso = _corpo.linearVelocity;
        }

        // ------------------------------------------------------------------------------------------ no anfitrião

        [Rpc(SendTo.Server)]
        void RodouRpc(ushort r0, ushort r1, ushort r2, ushort r3, ushort andou, RpcParams parametros = default)
        {
            if (parametros.Receive.SenderClientId != OwnerClientId) return;
            var a = desgaste;
            var p = _pneus.Value;
            bool mudou = false;
            float metros = andou / 10f, km = metros / 1000f;
            int furar = -1;
            for (int i = 0; i < PneusDoCarro.Quantas; i++)
            {
                if (!p.Tem(i)) continue;
                int e = p[i];
                float efetivos = (i == 0 ? r0 : i == 1 ? r1 : i == 2 ? r2 : r3) / 10f;
                if (e < PneusDoCarro.Minimo)
                {
                    // furado: rodar acaba com ele (até rasgar)
                    if (e <= 0 || _murcha[i] > 0f) continue;
                    _gastoDoPneu[i] += metros / 100f * a.furadoPor100m;
                }
                else if (_murcha[i] <= 0f)
                {
                    _gastoDoPneu[i] += efetivos / 1000f * (100f / Mathf.Max(1f, a.kmAteCareca)) / Mathf.Max(0.1f, _resistenciaDoPneu[i]);
                    // o furo sorteado: a chance por km cresce do pneu bom ao gasto (só um por vez: a carência)
                    if (furar < 0 && _carencia <= 0f && km > 0f)
                    {
                        float gasto = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1f, 0.3f, e / 100f));
                        float porKm = Mathf.Lerp(1f / Mathf.Max(0.1f, a.kmPorFuroNovo), 1f / Mathf.Max(0.1f, a.kmPorFuroGasto), gasto);
                        if (Random.value < 1f - Mathf.Exp(-porKm * km)) furar = i;
                    }
                }
                int inteiro = Mathf.FloorToInt(_gastoDoPneu[i]);
                if (inteiro <= 0) continue;
                _gastoDoPneu[i] -= inteiro;
                int novo = e < PneusDoCarro.Minimo ? Mathf.Max(0, e - inteiro) : Mathf.Max(e - inteiro, Mathf.Min(e, a.pisoDoDesgaste));
                if (novo == e) continue;
                p[i] = (byte)novo;
                mudou = true;
            }
            if (mudou) _pneus.Value = p;
            if (furar >= 0) Furo(furar, true, false);
        }

        [Rpc(SendTo.Server)]
        void AtropelouRpc(byte direita, RpcParams parametros = default)
        {
            if (parametros.Receive.SenderClientId != OwnerClientId || _carencia > 0f) return;
            if (Random.value < desgaste.furoNoAtropelo) Furo(direita != 0 ? 1 : 0, true, false);
        }

        [Rpc(SendTo.Server)]
        void BateuRpc(Vector3 local, float dv, RpcParams parametros = default)
        {
            if (parametros.Receive.SenderClientId != OwnerClientId) return;
            var a = desgaste;
            float forca = dv - a.batidaQueEstraga;
            // de frente: o radiador amassa e pode furar
            if (local.z > 1.25f)
            {
                var p = _pecas.Value;
                if (p.Tem(PecasDoMotor.Radiador))
                {
                    p.radiador = (byte)Mathf.Max(0, p.radiador - Mathf.RoundToInt(forca * 4f));
                    _pecas.Value = p;
                    if (!RadiadorVazando && Random.value < Mathf.Clamp01(forca / 8f)) ComecarAVazar();
                }
                return;
            }
            // do lado de uma roda: o pneu pode estourar
            if (rodas == null) return;
            for (int i = 0; i < PneusDoCarro.Quantas && i < rodas.Length; i++)
            {
                var r = transform.InverseTransformPoint(rodas[i].transform.position);
                if (Mathf.Abs(local.z - r.z) > 0.75f || Mathf.Sign(local.x) != Mathf.Sign(r.x) || Mathf.Abs(local.x) < 0.45f) continue;
                if (Random.value < Mathf.Clamp01(forca / 10f)) Furo(i, true, true);
                break;
            }
        }

        /// <summary>
        /// No anfitrião: o pneu fura. O estouro (forçado, ou sorteado: mais provável no pneu gasto e rápido) rasga o pneu na
        /// hora, com o barulho que chama os zumbis; o furo lento murcha em 40 a 60 s, com o chiado. Começa a carência.
        /// </summary>
        void Furo(int roda, bool podeEstourar, bool estourar)
        {
            var a = desgaste;
            var p = _pneus.Value;
            if (roda < 0 || roda >= PneusDoCarro.Quantas || !p.Tem(roda) || p.Furado(roda) || _murcha[roda] > 0f) return;
            float c = p[roda] / 100f;
            float chance = a.chanceDeEstouro + 0.35f * (1f - c) + (Velocidade > 18f ? 0.2f : 0f);
            _carencia = a.carencia;
            if (estourar || (podeEstourar && Random.value < chance))
            {
                p[roda] = 0;
                _pneus.Value = p;
                FurouRpc((byte)roda);
                ZombieManager.Barulho(rodas != null && roda < rodas.Length ? rodas[roda].transform.position : transform.position, Ocupante(0));
                return;
            }
            float tempo = Random.Range(a.tempoDoFuroLento.x, a.tempoDoFuroLento.y);
            _murcha[roda] = Mathf.Max(1f, p[roda] - 5f) / Mathf.Max(1f, tempo);
            _restoDaMurcha[roda] = 0f;
            _vazando.Value = (byte)(_vazando.Value | (1 << roda));
        }

        void ComecarAVazar()
        {
            _carencia = desgaste.carencia;
            _vazando.Value = (byte)(_vazando.Value | BitDoRadiador);
        }

        /// <summary>
        /// No anfitrião, a cada quadro (depois da bateria e da temperatura): os pneus murchando, a carência, a gasolina, a
        /// vela e o radiador gastando com o motor ligado, e o vazamento sorteado do radiador.
        /// </summary>
        void DesgasteNoServidor(float dt)
        {
            var a = desgaste;
            if (Velocidade > 2f && _carencia > 0f) _carencia -= dt;

            // os pneus murchando (o furo lento): até furar de vez (5%, com remendo)
            var p = _pneus.Value;
            bool mudou = false;
            for (int i = 0; i < PneusDoCarro.Quantas; i++)
            {
                if (_murcha[i] <= 0f) continue;
                if (!p.Tem(i) || p.Furado(i))
                {
                    PararDeMurchar(i);
                    continue;
                }
                _restoDaMurcha[i] += _murcha[i] * dt;
                int n = Mathf.FloorToInt(_restoDaMurcha[i]);
                if (n <= 0) continue;
                _restoDaMurcha[i] -= n;
                int novo = p[i] - n;
                if (novo < PneusDoCarro.Minimo)
                {
                    novo = 5;
                    PararDeMurchar(i);
                }
                p[i] = (byte)novo;
                mudou = true;
            }
            if (mudou) _pneus.Value = p;

            bool ligado = Estado == EstadoDoMotor.Ligado;
            // a gasolina: pelo giro do motor (o do dono, que vai pela rede)
            if (ligado && _litros > 0f)
            {
                float giro = _rotacao.Value / 255f;
                _litros = Mathf.Max(0f, _litros - Mathf.Lerp(a.gastoParado, a.gastoNoMaximo, Mathf.Pow(giro, 1.5f)) * dt / 60f);
            }
            if (Mathf.Abs(_litros - _gasolina.Value) >= 0.02f || (_litros <= 0f && _gasolina.Value > 0f)) _gasolina.Value = _litros;
            if (!ligado) return;

            // a vela e o radiador gastam com o motor ligado
            var pc = _pecas.Value;
            bool mudouPeca = false;
            if (pc.Tem(PecasDoMotor.Vela) && pc.vela > 0)
            {
                _gastoDaVela += dt / 60f * 100f / Mathf.Max(1f, a.minutosDaVela) / _resistenciaDaVela;
                int n = Mathf.FloorToInt(_gastoDaVela);
                if (n > 0)
                {
                    _gastoDaVela -= n;
                    pc.vela = (byte)Mathf.Max(0, pc.vela - n);
                    mudouPeca = true;
                }
            }
            if (pc.Tem(PecasDoMotor.Radiador))
            {
                bool vazando = RadiadorVazando;
                _gastoDoRadiador += dt / 60f * (100f / Mathf.Max(1f, a.minutosDoRadiador) / _resistenciaDoRadiador + (vazando ? a.vazamentoPorMinuto : 0f));
                int n = Mathf.FloorToInt(_gastoDoRadiador);
                if (n > 0)
                {
                    _gastoDoRadiador -= n;
                    pc.radiador = (byte)Mathf.Max(0, pc.radiador - n);
                    mudouPeca = true;
                }
                // o vazamento sorteado: mais provável no radiador gasto (só um problema por vez: a carência)
                if (!vazando && _carencia <= 0f && pc.radiador > 0)
                {
                    float gasto = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1f, 0.3f, pc.radiador / 100f));
                    float porMinuto = (1f + 5f * gasto) / Mathf.Max(1f, a.minutosPorVazamento);
                    if (Random.value < 1f - Mathf.Exp(-porMinuto * dt / 60f)) ComecarAVazar();
                }
            }
            if (mudouPeca) _pecas.Value = pc;
        }

        // ------------------------------------------------------------------------------------------ a gasolina

        /// <summary>Abastece com o galão (o Id dele na mochila de quem pede): passa o que couber no tanque.</summary>
        public void PedirAbastecer(ushort galao) => AbastecerRpc(galao);

        [Rpc(SendTo.Server)]
        void AbastecerRpc(ushort galao, RpcParams parametros = default)
        {
            var j = Jogador(parametros.Receive.SenderClientId);
            var inventario = j != null ? j.GetComponent<InventarioJogador>() : null;
            if (inventario == null || (j.transform.position - PontoDoTanque).sqrMagnitude > 3.2f * 3.2f) return;
            float noGalao = inventario.LitrosNoGalao(galao);
            float cabe = desgaste.tanque - _litros;
            float passa = Mathf.Min(noGalao, cabe);
            if (passa < 0.05f) return;
            inventario.TirarDoGalao(galao, passa);
            _litros = Mathf.Min(desgaste.tanque, _litros + passa);
            _gasolina.Value = _litros;
            AbasteceuRpc();
        }

        [Rpc(SendTo.Everyone)]
        void AbasteceuRpc() => Efeitos.Som(SomDoCarro.Abastecer, PontoDoTanque, 0.9f, true, 25f);

        // ------------------------------------------------------------------------------------------ em todas as máquinas

        // o chiado do ar no pneu que murcha e o vapor do radiador vazando (com o motor ligado)
        void SonsDoDesgaste(float dt)
        {
            int murchando = PneusMurchando;
            if (_somDoAr != null)
            {
                int roda = -1;
                for (int i = 0; i < PneusDoCarro.Quantas; i++) if ((murchando & (1 << i)) != 0) { roda = i; break; }
                if (roda >= 0 && rodasVisuais != null && roda < rodasVisuais.Length && rodasVisuais[roda] != null)
                    _somDoAr.transform.position = rodasVisuais[roda].position;
                Tocar(_somDoAr, roda >= 0 ? 0.55f : 0f, dt);
            }
            if (_somDoVapor != null)
            {
                _somDoVapor.transform.localPosition = new Vector3(0f, 0.9f, 1.75f);
                Tocar(_somDoVapor, RadiadorVazando && (MotorLigado || Temperatura > 0.4f) ? 0.4f : 0f, dt);
            }
        }

        static void Tocar(AudioSource s, float alvo, float dt)
        {
            s.volume = Mathf.MoveTowards(s.volume, alvo, dt * 1.5f);
            if (s.volume > 0.001f && !s.isPlaying) s.Play();
            else if (s.volume <= 0.001f && s.isPlaying) s.Stop();
        }

        // ------------------------------------------------------------------------------------------ o F1

        /// <summary>No anfitrião, pelo menu do desenvolvedor: 0 um furo lento, 1 um estouro, 2 os pneus a 30%, 3 o radiador
        /// vazando, 4 a gasolina quase no fim (1 L), 5 o tanque cheio, 6 a vela a 25%, 7 sem carência, 8 o motor novo (a vela,
        /// a bateria e o radiador a 100%, sem vazamento), 9 os pneus novos (os 4 a 100%, sem murchar).</summary>
        public string Teste(int acao)
        {
            if (!IsServer) return null;
            var p = _pneus.Value;
            switch (acao)
            {
                case 0:
                case 1:
                {
                    var boas = new System.Collections.Generic.List<int>();
                    for (int i = 0; i < PneusDoCarro.Quantas; i++) if (p.Tem(i) && !p.Furado(i) && _murcha[i] <= 0f) boas.Add(i);
                    if (boas.Count == 0) return "Nenhum pneu bom para furar";
                    int r = boas[Random.Range(0, boas.Count)];
                    Furo(r, acao == 1, acao == 1);
                    return (acao == 1 ? "Estourou o pneu " : "Furo lento no pneu ") + NomesDasRodas[r];
                }
                case 2:
                    for (int i = 0; i < PneusDoCarro.Quantas; i++) if (p.Tem(i) && !p.Furado(i)) p[i] = 30;
                    _pneus.Value = p;
                    return "Os pneus bons ficaram com 30%";
                case 3:
                    if (!_pecas.Value.Tem(PecasDoMotor.Radiador)) return "O carro está sem radiador";
                    ComecarAVazar();
                    return "O radiador começou a vazar";
                case 4:
                    _litros = 1f;
                    _gasolina.Value = _litros;
                    return "Gasolina: 1 L (a reserva, quase no fim)";
                case 5:
                    _litros = desgaste.tanque;
                    _gasolina.Value = _litros;
                    return $"Tanque cheio: {desgaste.tanque:0} L";
                case 6:
                {
                    var pc = _pecas.Value;
                    if (!pc.Tem(PecasDoMotor.Vela)) return "O carro está sem vela";
                    pc.vela = 25;
                    _pecas.Value = pc;
                    return "A vela ficou com 25%";
                }
                case 7:
                    _carencia = 0f;
                    return "Sem carência: a próxima falha pode vir já";
                case 8:
                {
                    _pecas.Value = PecasDoMotor.Com(100, 100, 100);
                    _carga = 100f;
                    NovaPeca(PecasDoMotor.Vela);
                    NovaPeca(PecasDoMotor.Radiador);
                    return "Motor novo: a vela, a bateria e o radiador a 100%";
                }
                case 9:
                    _pneus.Value = PneusDoCarro.Com(new[] { 100, 100, 100, 100 });
                    for (int i = 0; i < PneusDoCarro.Quantas; i++) NovoPneu(i);
                    return "Pneus novos: os 4 a 100%";
            }
            return null;
        }
    }
}
