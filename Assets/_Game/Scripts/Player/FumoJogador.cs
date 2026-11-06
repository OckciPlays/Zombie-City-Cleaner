using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering;
using ZombieCityCleaner.Combat;
using ZombieCityCleaner.Loot;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// O fumo (03/11/2026, a parte 4 do Plano_Cigarro.md da pasta de arte, a pedido: "pegar um cigarro do maço ao usar o
    /// item quando estiver na mão e fumar com animação, mostrando o cigarro na mão e saindo fumaça do cigarro"). Com o maço
    /// na mão, o clique acende um cigarro (o Acender, 3 s: o maço sobe, sacode, a boca pega o cigarro, o isqueiro acende);
    /// daí o cigarro fica aceso na mão, e cada clique é uma tragada (a baforada sai logo depois). Ele dura 6 tragadas ou 2
    /// minutos (o papel encolhe aos poucos) e, no fim (ou no R, ou trocando de item), o peteleco: a bituca cai no chão e
    /// fica soltando fumaça uns 20 s. O cigarro sai do maço (a pilha) no peteleco: com o último, o maço some.
    /// As animações e as peças são do bloco_cigarro.py: os ossos do fumo, com as malhas nos vazios, e a escala de cada osso
    /// dizendo se a peça aparece. Aqui ficam o estado (pela rede: o anfitrião e os outros sabem quem está fumando, e quanto
    /// do papel sobra), o papel encolhendo, a fumaça (em bolinhas facetadas, sem textura), a luz da brasa, a bituca e os
    /// sons. De noite, a brasa faz o jogador ser visto de um pouco mais longe (o ZombieManager).
    /// </summary>
    [DefaultExecutionOrder(160)]   // depois da animação: o papel e a brasa vão no lugar depois que os ossos mexeram
    public class FumoJogador : NetworkBehaviour
    {
        // os tempos dos clipes (o bloco_cigarro.py, a 24 quadros por segundo)
        const float Fps = 24f;
        public const float TempoDeAcender = 72f / Fps, BrasaAcende = 46f / Fps, ChamaAcende = 42f / Fps;
        public const float TempoDeTragar = 36f / Fps, PuxaDe = 9f / Fps, PuxaAte = 20f / Fps, Baforada = 24f / Fps;
        public const float TempoDoPeteleco = 16f / Fps, Solta = 6f / Fps;
        /// <summary>O cigarro dura isso ou estas tragadas, o que vier primeiro (a decisão 1).</summary>
        public const int TragadasPorCigarro = 6;
        public const float DuracaoDoCigarro = 120f;
        /// <summary>O que sobra do papel no fim (a bituca).</summary>
        const float PapelNoFim = 0.15f;
        /// <summary>A bituca no chão: o tempo da fumaça e o de sumir.</summary>
        const float FumacaDaBituca = 18f, BitucaSome = 22f;

        [Header("A montagem põe (MontagemDoFumo)")]
        [Tooltip("As bolinhas da fumaça (partículas, transparente, com a luz).")]
        public Material materialDaFumaca;
        [Tooltip("A bituca no chão (do Loot.fbx) e o material dela (a paleta).")]
        public Mesh malhaDaBituca;
        public Material materialDaBituca;

        public enum Estado : byte { Nada, Acendendo, Aceso, Jogando }

        /// <summary>O estado do fumo (escrito pelo dono): o que o corpo dele mostra nas outras máquinas.</summary>
        public readonly NetworkVariable<byte> estado = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        /// <summary>Quanto do papel sobra (255: inteiro), escrito pelo dono.</summary>
        public readonly NetworkVariable<byte> sobra = new(255, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        public Estado Agora => (Estado)estado.Value;
        /// <summary>O cigarro aceso está na mão (fumando ou no peteleco): os braços e o corpo do cigarro.</summary>
        public bool ComOCigarroNaMao => Agora == Estado.Aceso || Agora == Estado.Jogando;
        /// <summary>Fumando (a brasa acesa): de noite, os zumbis veem de um pouco mais longe.</summary>
        public bool Aceso => Agora == Estado.Aceso;

        JogadorRede _rede;
        ArmaJogador _arma;
        InventarioJogador _inventario;
        CharacterController _corpoFisico;
        Coroutine _rotina;
        ushort _idDoMaco;
        float _acesoDesde, _tragadaDesde = -99f;
        int _tragadas;
        bool _tragando;

        /// <summary>As peças de um conjunto (o corpo, os braços do maço e os do cigarro): os vazios e o que mexe neles.</summary>
        sealed class Pecas
        {
            public Transform cigarro, brasa, chama, papel, ponta;
            /// <summary>Os seis vazios (o maço, a tampa, o cigarro, a brasa, o isqueiro e a chama), cada um na cabeça do osso dele.</summary>
            public Transform[] vazios;
            public Vector3 eixo, ponta0;
            public ParticleSystem fio;
            public Light luz;
            public bool fp;
        }
        Pecas _corpo, _fpMaco, _fpCigarro;
        ParticleSystem _baforadaCorpo, _baforadaFP;

        void Awake()
        {
            _rede = GetComponent<JogadorRede>();
            _arma = GetComponent<ArmaJogador>();
            _inventario = GetComponent<InventarioJogador>();
            _corpoFisico = GetComponent<CharacterController>();
        }

        public override void OnNetworkSpawn()
        {
            estado.OnValueChanged += AoMudarOEstado;
            if (_rede != null) _rede.CorpoMudou += AoMudarOCorpo;
        }

        public override void OnNetworkDespawn()
        {
            estado.OnValueChanged -= AoMudarOEstado;
            if (_rede != null) _rede.CorpoMudou -= AoMudarOCorpo;
        }

        void AoMudarOCorpo() => _corpo = null;   // o personagem mudou: as peças do corpo são outras

        void AoMudarOEstado(byte antes, byte agora)
        {
            if (_rede != null) _rede.AtualizarControladorDoCorpo();
        }

        // ------------------------------------------------------------------ o dono: acender, tragar e jogar fora

        /// <summary>O clique com o maço na mão (o ArmaJogador): acende um cigarro, ou, aceso, traga.</summary>
        public void Clicar()
        {
            if (!IsOwner || _rotina != null) return;
            if (Agora == Estado.Nada) _rotina = StartCoroutine(Acender());
            else if (Agora == Estado.Aceso && !_tragando) _rotina = StartCoroutine(Tragar());
        }

        /// <summary>O R com o cigarro aceso: o peteleco.</summary>
        public void JogarFora()
        {
            if (!IsOwner || _rotina != null || Agora != Estado.Aceso) return;
            _rotina = StartCoroutine(Peteleco());
        }

        /// <summary>
        /// O que está na mão vai mudar (o ArmaJogador.Trocar, antes de mudar): o cigarro aceso cai no chão (sem o
        /// peteleco: a mão já saiu) e o acender pela metade para (o cigarro volta para o maço).
        /// </summary>
        public void AoTrocar(Item novo, ushort id)
        {
            if (!IsOwner || Agora == Estado.Nada) return;
            if (Itens.EhFumo(novo) && id == _idDoMaco) return;   // o mesmo maço (o corpo aplicado de novo)
            if (_rotina != null) StopCoroutine(_rotina);
            _rotina = null;
            _tragando = false;
            if (Agora == Estado.Aceso) SoltarBituca(false);
            estado.Value = (byte)Estado.Nada;
            sobra.Value = 255;
            if (_arma != null) _arma.Ocupar(false);
        }

        IEnumerator Acender()
        {
            _idDoMaco = _arma.IdNaMao;
            sobra.Value = 255;
            estado.Value = (byte)Estado.Acendendo;
            _arma.Ocupar(true);
            _arma.TocarAcao(0);
            AcaoNoCorpo(0);
            AcaoRpc(0);
            StartCoroutine(Sons(0, false));
            yield return new WaitForSeconds(TempoDeAcender);
            _arma.Ocupar(false);
            _acesoDesde = Time.time;
            _tragadas = 0;
            estado.Value = (byte)Estado.Aceso;
            MudouNaMao(false);
            _rotina = null;
        }

        IEnumerator Tragar()
        {
            _tragando = true;
            _tragadaDesde = Time.time;
            _arma.TocarAcao(0);
            AcaoNoCorpo(0);
            AcaoRpc(1);
            StartCoroutine(Sons(1, false));
            yield return new WaitForSeconds(Baforada);
            Baforar();
            yield return new WaitForSeconds(TempoDeTragar - Baforada);
            _tragadas++;
            _tragando = false;
            _rotina = null;
        }

        IEnumerator Peteleco()
        {
            estado.Value = (byte)Estado.Jogando;
            _arma.Ocupar(true);
            _arma.TocarAcao(1);
            AcaoNoCorpo(1);
            AcaoRpc(2);
            yield return new WaitForSeconds(Solta);
            Efeitos.Som(SomDoFumo.Peteleco, transform.position, 0.6f, false);
            SoltarBituca(true);
            yield return new WaitForSeconds(TempoDoPeteleco - Solta);
            _arma.Ocupar(false);
            estado.Value = (byte)Estado.Nada;
            sobra.Value = 255;
            _rotina = null;
            MudouNaMao(true);
        }

        /// <summary>O maço virou cigarro (ou o contrário): o modelo de primeira pessoa e o controlador do corpo.</summary>
        void MudouNaMao(bool sacar)
        {
            _arma.MostrarModeloDaMao(sacar);
            _rede.AtualizarControladorDoCorpo();
        }

        void Update()
        {
            if (!IsSpawned || !IsOwner || Agora != Estado.Aceso) return;
            // o cigarro queima: o tempo ou as tragadas, o que vier primeiro; acabou, o peteleco
            float usado = Mathf.Max((Time.time - _acesoDesde) / DuracaoDoCigarro, _tragadas / (float)TragadasPorCigarro);
            byte s = (byte)Mathf.RoundToInt(Mathf.Clamp01(1f - usado) * 255f);
            if (s != sobra.Value) sobra.Value = s;
            if (usado >= 1f && !_tragando && _rotina == null) _rotina = StartCoroutine(Peteleco());
        }

        /// <summary>
        /// A bituca sai da mão (o peteleco, com a velocidade dele; ou caída, trocando de item): no lugar do cigarro do corpo,
        /// nesta máquina e nas outras; e o cigarro sai do maço (no anfitrião).
        /// </summary>
        void SoltarBituca(bool peteleco)
        {
            var c = PecasDoCorpo();
            Vector3 ponto = c != null && c.cigarro != null ? c.cigarro.position : transform.position + transform.forward * 0.3f + Vector3.up * 1.1f;
            Vector3 vel = _corpoFisico != null ? _corpoFisico.velocity : Vector3.zero;
            if (peteleco) vel += transform.forward * 2.4f + Vector3.up * 1.6f + transform.right * Random.Range(-0.4f, 0.4f);
            else vel += Vector3.up * 0.3f;
            var giro = Random.rotation;
            Bituca(ponto, giro, vel);
            BitucaRpc(ponto, giro, vel);
            if (_inventario != null) _inventario.FumarUm(_idDoMaco);
        }

        // ------------------------------------------------------------------ o corpo e as outras máquinas

        static readonly int FireId = Animator.StringToHash("Fire");
        static readonly int ReloadId = Animator.StringToHash("Reload");

        /// <summary>O corpo (o que os outros veem; no dono, a sombra e a terceira pessoa): 0 o tiro (acender ou tragar), 1 o peteleco.</summary>
        void AcaoNoCorpo(int qual)
        {
            if (_rede == null || _rede.animadorCorpo == null) return;
            _rede.animadorCorpo.SetTrigger(qual == 1 ? ReloadId : FireId);
        }

        /// <summary>Nas outras máquinas: 0 acender, 1 tragar, 2 o peteleco (o corpo e os sons; a baforada sai no tempo dela).</summary>
        [Rpc(SendTo.NotMe)]
        void AcaoRpc(byte qual)
        {
            AcaoNoCorpo(qual == 2 ? 1 : 0);
            if (qual == 1) _tragadaDesde = Time.time;
            StartCoroutine(Sons(qual, true));
        }

        [Rpc(SendTo.NotMe)]
        void BitucaRpc(Vector3 ponto, Quaternion giro, Vector3 velocidade)
        {
            Efeitos.Som(SomDoFumo.Peteleco, ponto, 0.5f, true, 8f);
            Bituca(ponto, giro, velocidade);
        }

        /// <summary>Os sons de cada ação, nos tempos dos clipes (no dono, dentro da cabeça; nos outros, no corpo dele).</summary>
        IEnumerator Sons(byte qual, bool espacial)
        {
            float t0 = Time.time;
            Vector3 Onde() => _rede != null && _rede.ossoCabeca != null ? _rede.ossoCabeca.position : transform.position + Vector3.up * 1.6f;
            void Tocar(AudioClip c, float volume) => Efeitos.Som(c, Onde(), volume, espacial, 10f);
            IEnumerator Ate(float t) { while (Time.time - t0 < t) yield return null; }
            if (qual == 0)
            {
                yield return Ate(11f / Fps); Tocar(SomDoFumo.Sacudir, 0.5f);
                yield return Ate(15f / Fps); Tocar(SomDoFumo.Sacudir, 0.4f);
                yield return Ate(ChamaAcende - 0.08f); Tocar(SomDoFumo.Pedra, 0.6f);
                yield return Ate(ChamaAcende + 0.25f); Tocar(SomDoFumo.Chama, 0.4f);
                yield return Ate(BrasaAcende); Tocar(SomDoFumo.Tragada, 0.45f);
            }
            else if (qual == 1)
            {
                yield return Ate(PuxaDe); Tocar(SomDoFumo.Tragada, 0.55f);
                yield return Ate(Baforada); Tocar(SomDoFumo.Sopro, 0.5f);
                if (espacial) Baforar();
            }
        }

        // ------------------------------------------------------------------ as peças: o papel queimando, a fumaça e a luz

        Pecas PecasDoCorpo()
        {
            if (_corpo == null && _rede != null && _rede.fumoCorpo != null && _rede.fumoCorpo.Length >= 6)
                _corpo = Montar(_rede.fumoCorpo[2], _rede.fumoCorpo[3], _rede.fumoCorpo[5], _rede.fumoCorpo, false);
            return _corpo;
        }

        Pecas PecasDosBracos(int modelo, ref Pecas cache)
        {
            if (cache != null) return cache;
            if (_arma == null || _arma.modelos == null || modelo >= _arma.modelos.Length || _arma.modelos[modelo] == null) return null;
            var raiz = _arma.modelos[modelo].transform;
            Transform Achar(string n)
            {
                foreach (var t in raiz.GetComponentsInChildren<Transform>(true)) if (t.name == n) return t;
                return null;
            }
            var vazios = new Transform[NomesDosVazios.Length];
            for (int k = 0; k < vazios.Length; k++) vazios[k] = Achar("Fumo_FP_" + NomesDosVazios[k]);
            cache = Montar(vazios[2], vazios[3], vazios[5], vazios, true);
            return cache;
        }

        /// <summary>Os vazios das peças, na ordem do <see cref="JogadorRede.fumoCorpo"/> (a MontagemDoFumo).</summary>
        static readonly string[] NomesDosVazios = { "Maco", "Tampa", "Cigarro", "Brasa", "Isqueiro", "Chama" };

        /// <summary>As peças de um conjunto: o cigarro (o papel e a ponta dentro dele), a brasa e a chama, com a fumaça da ponta e a luz.</summary>
        Pecas Montar(Transform cigarro, Transform brasa, Transform chama, Transform[] vazios, bool fp)
        {
            if (cigarro == null || brasa == null) return null;
            var p = new Pecas { cigarro = cigarro, brasa = brasa, chama = chama, vazios = vazios, fp = fp };
            foreach (Transform t in cigarro)
            {
                if (t.name.EndsWith("_Papel")) p.papel = t;
                else if (t.name.EndsWith("_Ponta")) p.ponta = t;
            }
            if (p.papel != null)
            {
                // o comprido do papel (o eixo do cigarro, no espaço dele) e o fim dele (onde fica a ponta: o papel vai de 0
                // ao fim, a partir da emenda com o filtro), pela malha: as transformações do FBX não servem (veja Assentar)
                var mf = p.papel.GetComponent<MeshFilter>();
                var limites = mf != null && mf.sharedMesh != null ? mf.sharedMesh.bounds : new Bounds(Vector3.forward * 0.04f, Vector3.forward * 0.08f);
                var b = limites.size;
                p.eixo = b.x >= b.y && b.x >= b.z ? Vector3.right : b.y >= b.z ? Vector3.up : Vector3.forward;
                p.ponta0 = p.eixo * Vector3.Dot(limites.max, p.eixo);
            }
            int camada = fp ? JogadorRede.CamadaBracos : gameObject.layer;
            p.fio = NovaFumaca("Fumaça do cigarro", camada, false, fp);
            if (!fp)
            {
                // a luz da brasa (no corpo: no dono, os braços de primeira pessoa recebem a cópia dela, o BracosNaOrigem)
                var go = new GameObject("Luz da brasa");
                go.transform.SetParent(transform, false);
                p.luz = go.AddComponent<Light>();
                p.luz.type = LightType.Point;
                p.luz.color = new Color(1f, 0.45f, 0.15f);
                p.luz.range = 1.6f;
                p.luz.intensity = 0f;
                p.luz.shadows = LightShadows.None;
                p.luz.enabled = false;
            }
            return p;
        }

        void LateUpdate()
        {
            if (!IsSpawned) return;
            float s = Mathf.Lerp(PapelNoFim, 1f, sobra.Value / 255f);
            bool naMao = Itens.EhFumo(_rede != null ? _rede.NaMao : Item.Nada);
            bool dono = IsOwner;
            // o corpo: nos outros, sempre; no dono, só com o corpo à vista (a terceira pessoa). As peças só aparecem com o
            // maço na mão: os estados do corpo não escrevem o padrão, e os ossos do fumo ficavam onde a última ação deixou
            bool corpo = naMao && (!dono || _rede.CorpoVisivelNoDono);
            if (_rede.renderersDoFumo != null)
                foreach (var r in _rede.renderersDoFumo)
                    if (r != null && r.enabled != corpo) r.enabled = corpo;
            var c = PecasDoCorpo();
            if (c != null) Atualizar(c, s, corpo);
            if (dono)
            {
                bool fp = naMao && !_rede.EmTerceiraPessoa && _rede.cameraBracos != null && _rede.cameraBracos.enabled;
                var m = PecasDosBracos(Itens.MacoFP, ref _fpMaco);
                var k = PecasDosBracos(Itens.CigarroFP, ref _fpCigarro);
                if (m != null) Atualizar(m, 1f, fp && _arma.modelos[Itens.MacoFP].activeInHierarchy);
                if (k != null) Atualizar(k, s, fp && _arma.modelos[Itens.CigarroFP].activeInHierarchy);
            }
        }

        /// <summary>
        /// Cada vazio na cabeça do osso dele, e cada malha no lugar dela dentro do vazio (todas na origem dele; a ponta, o
        /// papel e a brasa vão depois). O export do Blender grava a posição dos vazios e das malhas a cada quadro, e nos
        /// ossos que encolhem para sumir (0,001: a brasa e a chama) a conta dele estraga em centímetros (a brasa ficava do
        /// lado do cigarro); nos braços, até a posição fixa no FBX vem assim. Aqui, depois da animação, tudo volta.
        /// </summary>
        static void Assentar(Pecas p)
        {
            if (p.vazios == null) return;
            foreach (var v in p.vazios)
            {
                if (v == null) continue;
                v.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                v.localScale = Vector3.one;
                foreach (Transform m in v)
                {
                    m.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                    m.localScale = Vector3.one;
                }
            }
        }

        /// <summary>O papel encolhido até s (em volta da emenda), a ponta e a brasa junto, a fumaça da ponta e a luz.</summary>
        void Atualizar(Pecas p, float s, bool aMostra)
        {
            Assentar(p);
            if (p.papel != null) p.papel.localScale = Vector3.one + p.eixo * (s - 1f);
            if (p.ponta != null) p.ponta.localPosition = p.ponta0 * s;
            // a brasa (no osso dela, na ponta do papel inteiro) desce com a ponta
            if (p.brasa != null && s < 0.999f) p.brasa.position = Vector3.LerpUnclamped(p.cigarro.position, p.brasa.position, s);
            // acesa: a escala do osso da brasa (as animações)
            bool acesa = aMostra && p.brasa != null && p.brasa.lossyScale.x > 0.5f * Mathf.Abs(transform.lossyScale.x);
            if (p.fio != null)
            {
                var em = p.fio.emission;
                em.enabled = acesa;
                if (acesa) p.fio.transform.position = p.brasa.position;
            }
            if (p.luz != null)
            {
                float puxa = Time.time - _tragadaDesde;
                float forte = puxa > PuxaDe && puxa < PuxaAte + 0.2f ? 1f : 0f;
                p.luz.enabled = acesa || (p.chama != null && aMostra && p.chama.lossyScale.x > 0.5f);
                if (p.luz.enabled)
                {
                    bool chama = p.chama != null && p.chama.lossyScale.x > 0.5f;
                    p.luz.transform.position = chama ? p.chama.position : p.brasa.position;
                    p.luz.color = chama ? new Color(1f, 0.75f, 0.35f) : new Color(1f, 0.45f, 0.15f);
                    // fracas: a luz cai com o quadrado da distância, e o rosto fica a uns 10 cm da brasa (com 0,45, o
                    // corpo inteiro ficava laranja); de noite, é o brilho no rosto e na mão, e a brasa (sem a luz) se vê de longe
                    p.luz.range = chama ? 2.0f : 1.5f;
                    p.luz.intensity = chama ? 0.012f : Mathf.Lerp(p.luz.intensity, 0.003f + forte * 0.005f + 0.0005f * Mathf.Sin(Time.time * 9f), 1f - Mathf.Exp(-10f * Time.deltaTime));
                }
            }
        }

        /// <summary>A baforada: a fumaça saindo da boca, para a frente (no dono, na primeira pessoa, embaixo da tela).</summary>
        void Baforar()
        {
            bool dono = IsOwner;
            bool fp = dono && !_rede.EmTerceiraPessoa && _rede.cameraBracos != null && _rede.cameraBracos.enabled;
            if (fp)
            {
                _baforadaFP ??= NovaFumaca("Baforada (primeira pessoa)", JogadorRede.CamadaBracos, true, true);
                var cam = _rede.cameraBracos.transform;
                _baforadaFP.transform.SetPositionAndRotation(cam.TransformPoint(new Vector3(0f, -0.12f, 0.18f)),
                    Quaternion.LookRotation(cam.forward + cam.up * 0.15f, cam.up));
                _baforadaFP.Emit(16);
                return;
            }
            if (dono && !_rede.CorpoVisivelNoDono) return;
            _baforadaCorpo ??= NovaFumaca("Baforada", gameObject.layer, true, false);
            var cabeca = _rede.ossoCabeca;
            Vector3 frente = Quaternion.AngleAxis(_rede.Pitch.Value, transform.right) * transform.forward;
            Vector3 boca = (cabeca != null ? cabeca.position : transform.position + Vector3.up * 1.55f) + Vector3.up * 0.04f + frente * 0.13f;
            _baforadaCorpo.transform.SetPositionAndRotation(boca, Quaternion.LookRotation(frente + Vector3.up * 0.2f));
            _baforadaCorpo.Emit(14);
        }

        // ------------------------------------------------------------------ a fumaça e a bituca

        static Mesh _icosfera;

        /// <summary>A bolinha da fumaça: um icosaedro de faces planas (20 faces, cada uma com os vértices dela).</summary>
        static Mesh Icosfera()
        {
            if (_icosfera != null) return _icosfera;
            float t = (1f + Mathf.Sqrt(5f)) / 2f;
            var v = new[]
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1),
            };
            int[] f =
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
            };
            var vs = new Vector3[f.Length];
            var tri = new int[f.Length];
            for (int i = 0; i < f.Length; i++)
            {
                vs[i] = v[f[i]].normalized * 0.5f;
                tri[i] = i;
            }
            _icosfera = new Mesh { name = "Bolinha da fumaça", vertices = vs, triangles = tri };
            _icosfera.RecalculateNormals();
            _icosfera.RecalculateBounds();
            return _icosfera;
        }

        /// <summary>
        /// Uma fumaça (as bolinhas facetadas cinza-claro, que crescem, sobem, giram e somem): o fio da ponta (contínuo,
        /// fino) ou a baforada (umas 14 maiores de uma vez, para a frente, abrindo). Na primeira pessoa (fp), menores e mais
        /// transparentes: a câmera fica a uns 30 cm do cigarro, e as do corpo pareciam pedras na frente da tela.
        /// </summary>
        ParticleSystem NovaFumaca(string nome, int camada, bool baforada, bool fp)
        {
            var go = new GameObject(nome) { layer = camada };
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startRotation3D = true;
            main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.85f, 0.85f, 0.87f, 1f), new Color(0.95f, 0.95f, 0.96f, 1f));
            main.maxParticles = baforada ? 80 : 70;
            var em = ps.emission;
            var forma = ps.shape;
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            var ruido = ps.noise;
            ruido.enabled = true;
            ruido.frequency = 1.4f;
            ruido.scrollSpeed = 0.6f;
            var tam = ps.sizeOverLifetime;
            tam.enabled = true;
            var cor = ps.colorOverLifetime;
            cor.enabled = true;
            var g = new Gradient();
            float pico = baforada ? (fp ? 0.2f : 0.4f) : (fp ? 0.3f : 0.4f);
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(pico, 0.1f), new GradientAlphaKey(pico * 0.45f, 0.6f), new GradientAlphaKey(0f, 1f) });
            cor.color = g;
            var giro = ps.rotationOverLifetime;
            giro.enabled = true;
            giro.separateAxes = true;
            giro.x = giro.y = giro.z = new ParticleSystem.MinMaxCurve(-1.5f, 1.5f);
            if (baforada)
            {
                main.loop = false;
                main.startLifetime = new ParticleSystem.MinMaxCurve(1.4f, 2.2f);
                main.startSpeed = fp ? new ParticleSystem.MinMaxCurve(0.25f, 0.5f) : new ParticleSystem.MinMaxCurve(0.35f, 0.75f);
                main.startSize = fp ? new ParticleSystem.MinMaxCurve(0.008f, 0.016f) : new ParticleSystem.MinMaxCurve(0.02f, 0.04f);
                em.enabled = false;
                forma.shapeType = ParticleSystemShapeType.Cone;
                forma.angle = 18f;
                forma.radius = 0.01f;
                vel.x = vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);
                vel.y = new ParticleSystem.MinMaxCurve(0.04f, 0.09f);
                var freio = ps.limitVelocityOverLifetime;
                freio.enabled = true;
                freio.limit = 0.05f;
                freio.dampen = 0.08f;
                ruido.strength = 0.08f;
                tam.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, fp ? 2.6f : 4f));
            }
            else
            {
                main.startLifetime = fp ? new ParticleSystem.MinMaxCurve(1.6f, 2.2f) : new ParticleSystem.MinMaxCurve(2.2f, 3.0f);
                main.startSpeed = 0f;
                main.startSize = fp ? new ParticleSystem.MinMaxCurve(0.003f, 0.006f) : new ParticleSystem.MinMaxCurve(0.006f, 0.012f);
                em.rateOverTime = fp ? 24f : 14f;
                em.enabled = false;
                forma.shapeType = ParticleSystemShapeType.Sphere;
                forma.radius = 0.003f;
                vel.x = vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);
                vel.y = fp ? new ParticleSystem.MinMaxCurve(0.07f, 0.10f) : new ParticleSystem.MinMaxCurve(0.10f, 0.16f);
                ruido.strength = 0.05f;
                tam.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.6f, 1f, fp ? 2.2f : 3f));
            }
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Mesh;
            r.mesh = Icosfera();
            r.sharedMaterial = materialDaFumaca;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.alignment = ParticleSystemRenderSpace.World;
            ps.Play();
            return ps;
        }

        /// <summary>A bituca: cai com física, solta um fio de fumaça da ponta queimada por uns 18 s e some (só um efeito).</summary>
        void Bituca(Vector3 ponto, Quaternion giro, Vector3 velocidade)
        {
            if (malhaDaBituca == null) return;
            var go = new GameObject("Bituca");
            go.transform.SetPositionAndRotation(ponto, giro);
            go.AddComponent<MeshFilter>().sharedMesh = malhaDaBituca;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = materialDaBituca;
            var b = malhaDaBituca.bounds;
            var col = go.AddComponent<BoxCollider>();
            col.center = b.center;
            col.size = Vector3.Max(b.size, Vector3.one * 0.008f);
            if (_corpoFisico != null) Physics.IgnoreCollision(col, _corpoFisico);
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 0.01f;
            rb.linearDamping = 0.2f;
            rb.angularDamping = 0.8f;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            rb.linearVelocity = velocidade;
            rb.angularVelocity = Random.insideUnitSphere * 14f;
            // a fumaça da ponta queimada (a ponta do comprido da malha, do lado do papel)
            var fio = NovaFumaca("Fumaça da bituca", go.layer, false, false);
            fio.transform.SetParent(go.transform, false);
            Vector3 eixo = b.size.x >= b.size.y && b.size.x >= b.size.z ? Vector3.right : b.size.y >= b.size.z ? Vector3.up : Vector3.forward;
            fio.transform.localPosition = b.center + Vector3.Scale(b.extents, eixo);
            var em = fio.emission;
            em.enabled = true;
            em.rateOverTime = 6f;
            Destroy(go, BitucaSome + 1f);   // se o jogador sair antes (a rotina para com ele)
            StartCoroutine(ApagarBituca(go, fio));
        }

        static IEnumerator ApagarBituca(GameObject go, ParticleSystem fio)
        {
            float t0 = Time.time;
            while (go != null && Time.time - t0 < FumacaDaBituca)
            {
                var em = fio.emission;
                em.rateOverTime = Mathf.Lerp(6f, 1.5f, (Time.time - t0) / FumacaDaBituca);
                yield return null;
            }
            if (fio != null)
            {
                var em = fio.emission;
                em.enabled = false;
            }
            while (go != null && Time.time - t0 < BitucaSome) yield return null;
            if (go != null) Destroy(go);
        }
    }
}
