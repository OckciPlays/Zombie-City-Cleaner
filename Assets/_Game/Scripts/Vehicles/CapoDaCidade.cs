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
        [Tooltip("Quem guarda as peças: o PecasDosCarros (Demo_Cidade) ou o PecasDosCarrosDoMundo (o mundo gerado).")]
        public FonteDasPecas pecas;
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
        [Tooltip("Quanto o canto do macaco sobe (m). O carro gira em volta do canto do outro lado: os dois cantos vizinhos sobem a metade, e as rodas deles continuam no chão (a suspensão estica).")]
        public float alturaDoMacaco = 0.2f;
        [Tooltip("A colisão do carro (um filho, no mundo gerado): inclina junto com ele. Na Demo_Cidade, vazio.")]
        public Transform colisao;

        float _visto, _ergueu, _erguidoMudouEm = -99f, _alturaDoMacacoFechado = 0.118f;
        bool _erguidoVisto, _macacoFeito;
        BoxCollider _miraDoMacaco;
        // sem a roda, o canto do carro desce até a lataria quase encostar no chão (o modelo não tem o cubo nem o disco)
        const float RaioDoCubo = 0.06f;
        // o quanto a suspensão estica (a roda desce) e encolhe (a roda sobe) para a roda continuar no chão; a roda do macaco
        // estica pouco: ela sai do chão
        const float Estica = 0.2f, Encolhe = 0.12f, EsticaNoMacaco = 0.05f;

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
        /// <summary>A roda é a do macaco (só ela sai do chão e pode ser trocada).</summary>
        public bool NaRodaDoMacaco(int roda) { int m = MacacoNaRoda; return m >= 0 && roda == m; }
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
            MostrarMacaco(m);
            if (!abrir && _visto == 0f && !mexido)
            {
                Mostrar(false);
                if (colisao != null && colisao.localRotation != Quaternion.identity) colisao.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                return;
            }
            Mostrar(true);
            CofreDoMotor.AnimarCapo(tampa, Quaternion.identity, abertura, ref _visto, abrir, Time.deltaTime);
            // com o capô fechado (a cópia só pela roda ou pelo macaco), o cofre não aparece: são ~5 peças a menos por carro
            MostrarCofre(abrir || _visto > 0f);
            Pose(pneus, m);
            if (_visto == 0f && !mexido) Mostrar(false);
        }

        // a cópia inclina pelo plano que passa mais perto da altura de cada canto:
        //  - sem a roda, o canto desce até a lataria quase encostar no chão;
        //  - no macaco, o carro gira em volta do canto do outro lado (na diagonal): o canto do macaco sobe alturaDoMacaco e os
        //    dois vizinhos a metade (os quatro num plano só).
        // Depois, cada roda que ficou acompanha o chão (a suspensão estica ou encolhe); a do macaco quase não estica e sai do chão.
        void Pose(PneusDoCarro p, int m)
        {
            if (aberto == null || centrosDasRodas == null || centrosDasRodas.Length < 4) return;
            for (int i = 0; i < 4 && rodas != null && i < rodas.Length; i++)
                if (rodas[i] != null && rodas[i].gameObject.activeSelf != p.Tem(i)) rodas[i].gameObject.SetActive(p.Tem(i));
            float queda = Mathf.Max(0f, raioDaRoda - RaioDoCubo);
            for (int i = 0; i < 4; i++) _alturas[i] = (p.Tem(i) ? 0f : -queda) + Macaco(m, i);
            float bitola = Mathf.Max(0.5f, Mathf.Abs(centrosDasRodas[1].x - centrosDasRodas[0].x));
            float entre = Mathf.Max(0.5f, Mathf.Abs(centrosDasRodas[0].z - centrosDasRodas[2].z));
            float h0 = _alturas[0], h1 = _alturas[1], h2 = _alturas[2], h3 = _alturas[3];
            float meio = (h0 + h1 + h2 + h3) / 4f;
            float lado = ((h1 + h3) - (h0 + h2)) / 2f / bitola;    // a direita mais alta
            float frente = ((h0 + h1) - (h2 + h3)) / 2f / entre;   // a frente mais alta
            var pos = new Vector3(0f, meio, 0f);
            var giro = Quaternion.Euler(-Mathf.Atan(frente) * Mathf.Rad2Deg, 0f, Mathf.Atan(lado) * Mathf.Rad2Deg);
            aberto.transform.SetLocalPositionAndRotation(pos, giro);
            if (colisao != null) colisao.SetLocalPositionAndRotation(pos, giro);
            // as rodas: a altura do canto (no plano) vira o quanto a roda desce para continuar no chão
            for (int i = 0; i < 4 && rodas != null && i < rodas.Length; i++)
            {
                if (rodas[i] == null || !p.Tem(i)) continue;
                float sx = Mathf.Sign(centrosDasRodas[i].x), sz = Mathf.Sign(centrosDasRodas[i].z);
                float canto = meio + lado * sx * bitola / 2f + frente * sz * entre / 2f;
                float desce = Mathf.Clamp(canto, -Encolhe, i == m && Erguido ? EsticaNoMacaco : Estica);
                rodas[i].localPosition = centrosDasRodas[i] + Vector3.down * desce;
            }
        }

        readonly float[] _alturas = new float[4];

        // o quanto o macaco levanta este canto: o dele inteiro, os vizinhos a metade, o da diagonal nada
        float Macaco(int m, int i)
        {
            if (m < 0 || _ergueu <= 0f) return 0f;
            float h = alturaDoMacaco * _ergueu;
            if (i == m) return h;
            return i == (m ^ 3) ? 0f : h * 0.5f;
        }

        // o macaco apoiado: do chão até onde ele encosta na lataria (que sobe com a cópia), com o gatilho dele
        void MostrarMacaco(int m)
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

        GameObject _paredesDoCofre;
        bool _procurouParedes;

        void MostrarCofre(bool ver)
        {
            if (!_procurouParedes)
            {
                _procurouParedes = true;
                var p = aberto != null ? aberto.transform.Find("Paredes do cofre") : null;
                _paredesDoCofre = p != null ? p.gameObject : null;
            }
            if (cofre != null && cofre.gameObject.activeSelf != ver) cofre.gameObject.SetActive(ver);
            if (_paredesDoCofre != null && _paredesDoCofre.activeSelf != ver) _paredesDoCofre.SetActive(ver);
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
