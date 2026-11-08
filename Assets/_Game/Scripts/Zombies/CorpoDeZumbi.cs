using UnityEngine;
using ZombieCityCleaner.Combat;

namespace ZombieCityCleaner.Zombies
{
    /// <summary>
    /// O corpo de um zumbi morto: uma cópia do modelo com física (ragdoll), sem animação. Quando o zumbi morre, ele some
    /// e o corpo do pool dele toma o lugar, na mesma pose e com a mesma velocidade, e cai com um empurrão do tiro.
    /// Cada máquina tem os seus corpos (são só visuais); quem decide a morte é o anfitrião.
    /// As juntas guardam o repouso de quando o corpo foi criado (na pose de repouso, braços abertos): por isso o corpo
    /// nunca é desligado no pool, só fica cinemático, sem colisão e invisível.
    /// O componente só fica ligado nos primeiros passos de física depois da queda (veja FixedUpdate): a interpolação e os
    /// músculos que vão apagando (CorpoDeZumbi.Musculos.cs, ~3,5 s).
    /// </summary>
    public partial class CorpoDeZumbi : MonoBehaviour
    {
        /// <summary>Os corpos só batem no cenário e uns nos outros: o tiro passa, e jogadores e zumbis atravessam.</summary>
        public const int Camada = 8;
        /// <summary>As partes com física, na ordem de <see cref="Zumbi.partes"/>.</summary>
        public static readonly string[] Partes =
            { "hips", "chest", "head", "upper_arm.L", "forearm.L", "upper_arm.R", "forearm.R", "thigh.L", "shin.L", "thigh.R", "shin.R" };
        public const int ParteCabeca = 2;

        [Header("Peças (a montagem preenche)")]
        public SkinnedMeshRenderer pele;
        public Mesh malhaInteira, malhaSemCabeca;
        public Material[] materiaisInteira, materiaisSemCabeca;
        [Tooltip("Todos os ossos, na mesma ordem do esqueleto do zumbi (a pose é copiada osso a osso).")]
        public Transform[] esqueleto;
        public Rigidbody[] partes;
        public Transform pescoco;

        internal float caiuEm;
        internal bool emUso;
        /// <summary>O boneco de um zumbi vivo caindo sem uma perna (Zumbi.Equilibrio.cs): volta ao pool quando ele levanta.</summary>
        internal bool vivo;
        bool _parado;
        int _passosSemInterpolar;
        // os zumbis leves: a cópia do material, com a aparência do zumbi que caiu e o rosto de morto
        Material _material;
        Material[] _inteira, _semCabeca;

        /// <summary>Recém-criado ou de volta ao pool: cinemático, sem colisão, sem a interpolação e invisível.</summary>
        internal void Guardar()
        {
            emUso = false;
            vivo = false;
            _vigiando = false;
            _comMusculos = false;
            foreach (var p in partes)
            {
                p.isKinematic = true;
                p.detectCollisions = false;
                // parado, a interpolação não serve para nada, e a física interpolava os ~1.000 corpos guardados a cada
                // quadro (~0,5 ms, 31/10/2026); ela volta um passo depois de cair (FixedUpdate)
                p.interpolation = RigidbodyInterpolation.None;
                var c = p.GetComponent<Collider>();
                if (c != null) c.enabled = true;
            }
            pele.enabled = false;
            enabled = false;
            Estatua(false);
        }

        // ------------------------------------------------------------------ a estátua (o Oco que virou pedra no sol)

        static readonly int PetrificacaoId = Shader.PropertyToID("_Petrificacao");
        CharacterJoint[] _juntas;
        (SoftJointLimit baixo, SoftJointLimit alto, SoftJointLimit balanco1, SoftJointLimit balanco2)[] _limites;
        MaterialPropertyBlock _blocoDaPedra;
        bool _estatua;

        // a pose de repouso (a da montagem, em que as juntas foram feitas), para devolver as juntas a ela depois da estátua
        Quaternion[] _repouso;
        Vector3[] _repousoPos;

        void ConhecerAsJuntas()
        {
            if (_juntas != null) return;
            _juntas = GetComponentsInChildren<CharacterJoint>(true);
            _limites = new (SoftJointLimit, SoftJointLimit, SoftJointLimit, SoftJointLimit)[_juntas.Length];
            for (int i = 0; i < _juntas.Length; i++)
                _limites[i] = (_juntas[i].lowTwistLimit, _juntas[i].highTwistLimit, _juntas[i].swing1Limit, _juntas[i].swing2Limit);
            // (chamado antes da primeira queda: os ossos ainda estão no repouso do prefab)
            _repouso = new Quaternion[esqueleto.Length];
            _repousoPos = new Vector3[esqueleto.Length];
            for (int i = 0; i < esqueleto.Length; i++) esqueleto[i].GetLocalPositionAndRotation(out _repousoPos[i], out _repouso[i]);
            ConhecerOsMusculos();
        }

        /// <summary>
        /// A junta guarda a pose em que foi ligada: soltar e ligar de novo o corpo do pai faz a pose de agora ser a dela (o
        /// zero dos limites). Na estátua, a pose da estátua; de volta, a de repouso.
        /// </summary>
        void Reancorar()
        {
            foreach (var j in _juntas)
            {
                var pai = j.connectedBody;
                j.connectedBody = null;
                j.connectedBody = pai;
            }
        }

        /// <summary>
        /// O corpo de pedra: todo de pedra no shader do Oco (_Petrificacao 2) e as juntas travadas na pose da estátua (cai
        /// duro, como uma estátua tombando, na postura em que estava: 07/11/2026, antes as juntas travavam no zero delas, a
        /// pose de repouso de braços abertos, e a estátua mudava de postura ao cair). Chamado com a pose já copiada. De
        /// volta, as juntas voltam ao repouso e aos limites da montagem.
        /// </summary>
        void Estatua(bool sim)
        {
            if (sim == _estatua) return;
            _estatua = sim;
            ConhecerAsJuntas();
            if (sim) Reancorar();
            else
            {
                for (int i = 0; i < esqueleto.Length; i++) esqueleto[i].SetLocalPositionAndRotation(_repousoPos[i], _repouso[i]);
                Reancorar();
            }
            for (int i = 0; i < _juntas.Length; i++)
            {
                var j = _juntas[i];
                var travada = new SoftJointLimit { limit = 0f };
                j.lowTwistLimit = sim ? travada : _limites[i].baixo;
                j.highTwistLimit = sim ? travada : _limites[i].alto;
                j.swing1Limit = sim ? travada : _limites[i].balanco1;
                j.swing2Limit = sim ? travada : _limites[i].balanco2;
            }
            if (sim)
            {
                _blocoDaPedra ??= new MaterialPropertyBlock();
                _blocoDaPedra.SetFloat(PetrificacaoId, 2f);
                pele.SetPropertyBlock(_blocoDaPedra);
            }
            else pele.SetPropertyBlock(null);
        }

        /// <summary>
        /// Toma o lugar do zumbi: a mesma pose (osso a osso), a velocidade dele em todas as partes e o empurrão do tiro
        /// na parte atingida. Sem cabeça, troca a malha (a tampa de carne no pescoço) e o pescoço jorra. Com membros
        /// perdidos, a malha é a cortada e as partes que caíram não colidem (seguem invisíveis, presas às juntas).
        /// Nos zumbis leves, o corpo copia a aparência do zumbi (o material dele) e fica com o rosto de morto (vivo: o boneco
        /// de um zumbi caindo sem uma perna, com o rosto que ele tinha).
        /// </summary>
        internal void Cair(Transform modelo, Transform[] pose, Vector3 velocidade, bool semCabeca, int parte, Vector3 empurrao, byte cortes,
                           Material aparencia = null, float pedra = 0f, bool vivo = false, Morte jeito = Morte.Comum)
        {
            ConhecerAsJuntas();
            if (pedra < 0.5f) Estatua(false);   // (a estátua trava as juntas depois, na pose copiada)
            emUso = true;
            this.vivo = vivo;
            _cortes = cortes;
            _cabecaFora = semCabeca;
            _quebrou = false;
            _vigiando = false;
            _parado = false;
            caiuEm = Time.time;
            transform.SetPositionAndRotation(modelo.position, modelo.rotation);
            for (int i = 0; i < esqueleto.Length && i < pose.Length; i++)
                esqueleto[i].SetLocalPositionAndRotation(pose[i].localPosition, pose[i].localRotation);
            if (pedra >= 0.5f)
            {
                _estatua = false;   // (de novo, se já era: as juntas pegam a pose desta estátua)
                Estatua(true);
                _comMusculos = false;
            }
            else
            {
                // as juntas na pose da morte (sem o tranco) e os músculos que vão apagando; o boneco vivo (caindo sem uma
                // perna) só tem o atrito
                LimitesNaPose();
                Musculos(vivo ? Morte.Desligou : jeito);
            }
            pele.sharedMesh = Membros.Variante(semCabeca ? malhaSemCabeca : malhaInteira, esqueleto, cortes);
            var inteira = materiaisInteira;
            var cortada = materiaisSemCabeca;
            if (Aparencia.Leve(aparencia))
            {
                if (_material == null)
                {
                    _material = new Material(aparencia) { name = aparencia.name + " (corpo)" };
                    _inteira = new[] { _material };
                    _semCabeca = new[] { _material, materiaisSemCabeca[1] };
                }
                _material.CopyPropertiesFromMaterial(aparencia);
                if (!vivo) _material.SetVector(Aparencia.QuadrosId, Aparencia.Morto(aparencia.GetVector(Aparencia.QuadrosId)));
                inteira = _inteira;
                cortada = _semCabeca;
            }
            pele.sharedMaterials = semCabeca || cortes != 0 ? cortada : inteira;   // a carne é o 2º material
            for (int l = 0; l < Membros.Quantos; l++)
            {
                int e = Membros.Estado(cortes, l);
                // as partes de cada membro: braço e antebraço (3/4 e 5/6), coxa e canela (7/8 e 9/10)
                int cima = 3 + l * 2, baixo = cima + 1;
                Colidir(baixo, e < 1);
                Colidir(cima, e < 2);
            }
            Colidir(ParteCabeca, true);   // (a estátua que perdeu a cabeça a desligou: o corpo pode voltar sem passar pelo pool)
            pele.enabled = true;
            foreach (var p in partes)
            {
                // a interpolação desenharia a parte entre a pose antiga (onde o corpo estava, às vezes do outro lado da
                // cidade) e a nova, por alguns quadros: um clarão do corpo sendo puxado. Ela volta depois de um passo.
                p.interpolation = RigidbodyInterpolation.None;
                // a física não lê a pose nova dos ossos sozinha: sem isto, as partes caíam de onde o corpo estava
                // guardado no pool (o meio do mapa)
                p.position = p.transform.position;
                p.rotation = p.transform.rotation;
                p.isKinematic = false;
                p.detectCollisions = true;
                p.linearVelocity = velocidade;
                p.angularVelocity = Vector3.zero;
            }
            if (parte >= 0 && parte < partes.Length) partes[parte].AddForce(empurrao, ForceMode.Impulse);
            if (semCabeca && pedra < 0.5f) Sangue.Jorro(pescoco, pescoco.up, 4f);
            // a estátua quebra ao bater no chão; a espingarda de perto e o carro a despedaçam já (CorpoDeZumbi.Pedra.cs)
            if (_estatua) VigiarAEstatua(jeito == Morte.Arremesso, empurrao.sqrMagnitude > 0.01f ? empurrao : velocidade);
            _passosSemInterpolar = 2;
            enabled = true;
        }

        /// <summary>
        /// O zumbi morreu enquanto o boneco dele caía (sem uma perna): o boneco passa a ser o corpo (conta o tempo do corpo
        /// a partir de agora), com o rosto de morto, a cabeça trocada se explodiu e o empurrão do tiro.
        /// </summary>
        internal void VirarMorto(bool semCabeca, int parte, Vector3 empurrao, byte cortes, Material aparencia)
        {
            vivo = false;
            caiuEm = Time.time;
            if (_material != null && Aparencia.Leve(aparencia))
                _material.SetVector(Aparencia.QuadrosId, Aparencia.Morto(aparencia.GetVector(Aparencia.QuadrosId)));
            if (semCabeca)
            {
                pele.sharedMesh = Membros.Variante(malhaSemCabeca, esqueleto, cortes);
                pele.sharedMaterials = _semCabeca ?? materiaisSemCabeca;
                Sangue.Jorro(pescoco, pescoco.up, 4f);
            }
            if (parte >= 0 && parte < partes.Length && !partes[parte].isKinematic) partes[parte].AddForce(empurrao, ForceMode.Impulse);
        }

        void Colidir(int parte, bool sim)
        {
            if (parte < partes.Length && partes[parte].TryGetComponent<Collider>(out var c)) c.enabled = sim;
        }

        // um passo de física já rodou na pose nova: a interpolação volta (o corpo cai liso, mesmo com mais quadros que
        // passos de física); e os músculos, enquanto durarem
        void FixedUpdate()
        {
            if (_passosSemInterpolar > 0 && --_passosSemInterpolar == 0)
                foreach (var p in partes) p.interpolation = RigidbodyInterpolation.Interpolate;
            PassoDosMusculos();
            PassoDaEstatua();
            if (_passosSemInterpolar <= 0 && !_comMusculos && !_vigiando) enabled = false;
        }

        /// <summary>Depois de assentar, a física para (o corpo vira cinemático, parado no lugar).</summary>
        internal void Assentar()
        {
            if (_parado) return;
            _parado = true;
            _comMusculos = false;
            foreach (var p in partes)
            {
                p.isKinematic = true;
                p.detectCollisions = false;
                p.interpolation = RigidbodyInterpolation.None;   // parado no chão (ele afunda pelo transform)
            }
        }

        internal void Afundar(float metros) => transform.position += Vector3.down * metros;
    }
}
