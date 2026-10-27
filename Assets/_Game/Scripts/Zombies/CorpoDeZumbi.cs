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
    /// O componente só fica ligado nos primeiros passos de física depois da queda (veja FixedUpdate).
    /// </summary>
    public class CorpoDeZumbi : MonoBehaviour
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
        bool _parado;
        int _passosSemInterpolar;
        // os zumbis leves: a cópia do material, com a aparência do zumbi que caiu e o rosto de morto
        Material _material;
        Material[] _inteira, _semCabeca;

        /// <summary>Recém-criado ou de volta ao pool: cinemático, sem colisão e invisível.</summary>
        internal void Guardar()
        {
            emUso = false;
            foreach (var p in partes)
            {
                p.isKinematic = true;
                p.detectCollisions = false;
                var c = p.GetComponent<Collider>();
                if (c != null) c.enabled = true;
            }
            pele.enabled = false;
            enabled = false;
        }

        /// <summary>
        /// Toma o lugar do zumbi: a mesma pose (osso a osso), a velocidade dele em todas as partes e o empurrão do tiro
        /// na parte atingida. Sem cabeça, troca a malha (a tampa de carne no pescoço) e o pescoço jorra. Com membros
        /// perdidos, a malha é a cortada e as partes que caíram não colidem (seguem invisíveis, presas às juntas).
        /// Nos zumbis leves, o corpo copia a aparência do zumbi (o material dele) e fica com o rosto de morto.
        /// </summary>
        internal void Cair(Transform modelo, Transform[] pose, Vector3 velocidade, bool semCabeca, int parte, Vector3 empurrao, byte cortes,
                           Material aparencia = null)
        {
            emUso = true;
            _parado = false;
            caiuEm = Time.time;
            transform.SetPositionAndRotation(modelo.position, modelo.rotation);
            for (int i = 0; i < esqueleto.Length && i < pose.Length; i++)
                esqueleto[i].SetLocalPositionAndRotation(pose[i].localPosition, pose[i].localRotation);
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
                _material.SetVector(Aparencia.QuadrosId, Aparencia.Morto(aparencia.GetVector(Aparencia.QuadrosId)));
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
            if (semCabeca) Sangue.Jorro(pescoco, pescoco.up, 4f);
            _passosSemInterpolar = 2;
            enabled = true;
        }

        void Colidir(int parte, bool sim)
        {
            if (parte < partes.Length && partes[parte].TryGetComponent<Collider>(out var c)) c.enabled = sim;
        }

        // um passo de física já rodou na pose nova: a interpolação volta (o corpo cai liso, mesmo com mais quadros que
        // passos de física)
        void FixedUpdate()
        {
            if (--_passosSemInterpolar > 0) return;
            foreach (var p in partes) p.interpolation = RigidbodyInterpolation.Interpolate;
            enabled = false;
        }

        /// <summary>Depois de assentar, a física para (o corpo vira cinemático, parado no lugar).</summary>
        internal void Assentar()
        {
            if (_parado) return;
            _parado = true;
            foreach (var p in partes)
            {
                p.isKinematic = true;
                p.detectCollisions = false;
            }
        }

        internal void Afundar(float metros) => transform.position += Vector3.down * metros;
    }
}
