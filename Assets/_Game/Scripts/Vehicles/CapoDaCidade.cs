using UnityEngine;
using ZombieCityCleaner.Player;

namespace ZombieCityCleaner.Vehicles
{
    /// <summary>
    /// O capô de um carro da cidade (um carro parado, abandonado): um gatilho em volta da frente, que a mira procura. E
    /// leva a cabeça para dentro do capô (<see cref="TelaDoCapo"/>), com as peças que sobraram nele (guardadas no
    /// <see cref="PecasDosCarros"/>, pelo <see cref="indice"/>): dá para tirar as que servem e levar.
    /// O modelo do carro (do Cidade_Rua.fbx) não abre: a montagem dos veículos recorta o capô da malha e guarda, no
    /// "Capô aberto", a carroceria sem o capô, o capô à parte (<see cref="tampa"/>, a origem na dobradiça) e o cofre do
    /// motor (<see cref="cofre"/>). Enquanto alguém olha o motor, o carro da cidade some e o "Capô aberto" aparece no
    /// lugar dele, com o capô subindo.
    /// </summary>
    public class CapoDaCidade : Interagivel, ICapo
    {
        public PecasDosCarros pecas;
        public int indice;
        [Tooltip("O nome do carro na tela (\"Sedã cinza\").")]
        public string nome = "Carro";

        [Header("O capô aberto")]
        [Tooltip("O carro da cidade (a malha inteira), que some com o capô aberto.")]
        public MeshRenderer carroFechado;
        [Tooltip("A carroceria sem o capô, o capô e o cofre: aparece no lugar do carro com o capô aberto.")]
        public GameObject aberto;
        [Tooltip("O capô recortado da malha (a origem na dobradiça, atrás): abre girando em X.")]
        public Transform tampa;
        public CofreDoMotor cofre;
        public float abertura = 62f;

        [Header("As rodas e o macaco")]
        [Tooltip("As 4 rodas recortadas da malha (DE, DD, TE, TD), no \"Capô aberto\": somem quando são tiradas.")]
        public Transform[] rodas;
        [Tooltip("O meio de cada roda e onde o macaco encosta ao lado dela (no espaço do carro).")]
        public Vector3[] centrosDasRodas, pontosDoMacaco;
        public float raioDaRoda = 0.3f;
        [Tooltip("O macaco apoiado (filho do carro, fora do \"Capô aberto\", que inclina): esticado do chão até a lataria.")]
        public MeshRenderer macaco;
        [Tooltip("Quanto o lado do macaco sobe (m).")]
        public float alturaDoMacaco = 0.22f;

        float _visto, _ergueu, _erguidoMudouEm = -99f, _alturaDoMacacoFechado = 0.118f;
        bool _erguidoVisto, _macacoFeito;
        BoxCollider _miraDoMacaco;
        // o cubo da roda (sem ela, o canto do carro desce até ele)
        const float RaioDoCubo = 0.13f;

        public string NomeDoCarro => nome;
        public CofreDoMotor Cofre => cofre;
        public PecasDoMotor Pecas => pecas != null ? pecas.Do(indice) : default;
        public Vector3 Ponto => transform.position;
        public bool Aberto => pecas != null && pecas.IsSpawned;
        public string Impedimento => null;
        public string Situacao => "Abandonado. O que sobrou no motor:";

        // as rodas e o macaco, da lista do PecasDosCarros
        public PneusDoCarro Pneus => pecas != null ? pecas.PneusDo(indice) : PneusDoCarro.Com(null);
        public int MacacoNaRoda => pecas != null ? pecas.MacacoDo(indice) : -1;
        public bool Erguido => pecas != null && pecas.ErguidoDo(indice);
        /// <summary>O carro ainda está subindo ou descendo no macaco.</summary>
        public bool MacacoMexendo => MacacoNaRoda >= 0 && Time.time - _erguidoMudouEm < PecasDosCarros.TempoDoMacaco;
        public bool NoLadoErguido(int roda) { int m = MacacoNaRoda; return m >= 0 && roda >= 0 && roda < 4 && roda % 2 == m % 2; }
        public Transform Carro => carroFechado != null ? carroFechado.transform : transform;
        /// <summary>O meio da roda no mundo (a distância dos pedidos conta daqui).</summary>
        public Vector3 PosicaoDaRoda(int roda) =>
            centrosDasRodas != null && roda >= 0 && roda < centrosDasRodas.Length ? Carro.TransformPoint(centrosDasRodas[roda]) : Carro.position;

        public void PedirTirar(int peca) => pecas.PedirTirar(indice, peca);
        public void PedirPor(int peca, ushort idNaMochila) => pecas.PedirPor(indice, peca, idNaMochila);
        public void Olhando(bool olhando) => pecas?.Olhar(indice, olhando);

        void OnDisable() => Mostrar(false);

        void Update()
        {
            // alguém olha o motor (pela rede), ou o jogador desta máquina (sem esperar a rede)
            bool abrir = (pecas != null && pecas.EstaAberto(indice)) || (cofre != null && cofre.Inspecionando);
            // sem uma roda ou com o macaco, o carro também é a cópia (o modelo da cidade tem as 4 rodas grudadas)
            var pneus = Pneus;
            int m = MacacoNaRoda;
            bool erguido = Erguido;
            if (erguido != _erguidoVisto)
            {
                _erguidoVisto = erguido;
                _erguidoMudouEm = Time.time;
            }
            _ergueu = Mathf.MoveTowards(_ergueu, m >= 0 && erguido ? 1f : 0f, Time.deltaTime / PecasDosCarros.TempoDoMacaco);
            bool mexido = m >= 0 || !pneus.Tem(0) || !pneus.Tem(1) || !pneus.Tem(2) || !pneus.Tem(3);
            Macaco(m);
            if (!abrir && _visto == 0f && !mexido)
            {
                Mostrar(false);
                return;
            }
            Mostrar(true);
            CofreDoMotor.AnimarCapo(tampa, Quaternion.identity, abertura, ref _visto, abrir, Time.deltaTime);
            Pose(pneus, m);
            if (_visto == 0f && !mexido) Mostrar(false);
        }

        // a cópia inclina: o canto sem a roda desce até o cubo, e o lado do macaco sobe (o plano que passa mais perto da
        // altura de cada canto)
        void Pose(PneusDoCarro p, int m)
        {
            if (aberto == null || centrosDasRodas == null || centrosDasRodas.Length < 4) return;
            for (int i = 0; i < 4 && rodas != null && i < rodas.Length; i++)
                if (rodas[i] != null && rodas[i].gameObject.activeSelf != p.Tem(i)) rodas[i].gameObject.SetActive(p.Tem(i));
            float queda = Mathf.Max(0f, raioDaRoda - RaioDoCubo);
            float h0 = Altura(p, m, 0, queda), h1 = Altura(p, m, 1, queda), h2 = Altura(p, m, 2, queda), h3 = Altura(p, m, 3, queda);
            float bitola = Mathf.Max(0.5f, Mathf.Abs(centrosDasRodas[1].x - centrosDasRodas[0].x));
            float entre = Mathf.Max(0.5f, Mathf.Abs(centrosDasRodas[0].z - centrosDasRodas[2].z));
            float lado = ((h1 + h3) - (h0 + h2)) / 2f / bitola;    // a direita mais alta
            float frente = ((h0 + h1) - (h2 + h3)) / 2f / entre;   // a frente mais alta
            aberto.transform.SetLocalPositionAndRotation(new Vector3(0f, (h0 + h1 + h2 + h3) / 4f, 0f),
                Quaternion.Euler(-Mathf.Atan(frente) * Mathf.Rad2Deg, 0f, Mathf.Atan(lado) * Mathf.Rad2Deg));
        }

        float Altura(PneusDoCarro p, int m, int i, float queda)
        {
            float h = p.Tem(i) ? 0f : -queda;
            return m >= 0 && i % 2 == m % 2 ? Mathf.Lerp(h, alturaDoMacaco, _ergueu) : h;
        }

        // o macaco apoiado: do chão até onde ele encosta na lataria (que sobe com a cópia), com o gatilho dele
        void Macaco(int m)
        {
            if (macaco == null) return;
            if (!_macacoFeito)
            {
                _macacoFeito = true;
                _miraDoMacaco = macaco.GetComponent<BoxCollider>();
                var mf = macaco.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null) _alturaDoMacacoFechado = Mathf.Max(0.02f, mf.sharedMesh.bounds.size.y);
            }
            bool ver = m >= 0 && pontosDoMacaco != null && m < pontosDoMacaco.Length && aberto != null;
            if (macaco.enabled != ver) macaco.enabled = ver;
            if (_miraDoMacaco != null && _miraDoMacaco.enabled != ver) _miraDoMacaco.enabled = ver;
            if (!ver) return;
            var carro = Carro;
            Vector3 topo = aberto.transform.TransformPoint(pontosDoMacaco[m]);
            float chao = carro.position.y;
            float altura = Mathf.Max(0.04f, topo.y - chao);
            var frente = Vector3.ProjectOnPlane(carro.forward, Vector3.up);
            if (frente.sqrMagnitude < 1e-4f) frente = Vector3.forward;
            var t = macaco.transform;
            t.SetPositionAndRotation(new Vector3(topo.x, chao, topo.z), Quaternion.LookRotation(frente.normalized) * Quaternion.Euler(0f, -90f, 0f));
            t.localScale = new Vector3(1f, altura / _alturaDoMacacoFechado, 1f);
            if (_miraDoMacaco != null)
            {
                float s = Mathf.Sign(centrosDasRodas[m].x);
                _miraDoMacaco.center = new Vector3(0f, _alturaDoMacacoFechado * 0.5f, -s * 0.09f);
                _miraDoMacaco.size = new Vector3(0.44f, _alturaDoMacacoFechado * 1.1f, 0.34f);
            }
        }

        // o carro da cidade ou o do capô aberto (o mesmo carro, com o capô à parte)
        void Mostrar(bool comCapoAberto)
        {
            if (aberto == null || carroFechado == null) return;
            if (aberto.activeSelf != comCapoAberto) aberto.SetActive(comCapoAberto);
            if (carroFechado.enabled == comCapoAberto) carroFechado.enabled = !comCapoAberto;
        }

        public override string Dica(ArmaJogador quem)
        {
            if (!Aberto) return null;
            var p = Pecas;
            bool vazio = true;
            for (int k = 0; k < PecasDoMotor.Quantas; k++) vazio &= !p.Tem(k);
            return vazio ? "[E] Abrir o capô (já vasculhado)" : "[E] Abrir o capô";
        }

        public override void Usar(ArmaJogador quem)
        {
            var tela = quem.GetComponent<TelaDoCapo>();
            if (tela != null) tela.Abrir(this);
        }
    }
}
