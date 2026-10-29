using System.Collections.Generic;
using UnityEngine;
using ZombieCityCleaner.Combat;
using ZombieCityCleaner.Mundo;
using ZombieCityCleaner.Player;
using ZombieCityCleaner.Vehicles;

namespace ZombieCityCleaner.World
{
    /// <summary>
    /// A beira das estradas na cena (Plano_Mapa, Parte M4): cria os objetos de <see cref="ObjetosDaEstrada"/> dos blocos até
    /// <see cref="raio"/> m do jogador local (os mais perto primeiro, com um limite de tempo por quadro) e apaga os que
    /// ficaram longe. Nada vai pela rede:
    /// cada máquina cria os seus, iguais em todas.
    ///  - os carros abandonados, as barreiras de concreto, o cavalete e o cone: os modelos do Blender (Cidade_Rua.fbx), com
    ///    uma caixa de colisão do tamanho deles. Na pista, o carro acompanha a subida; fora dela, deita no barranco;
    ///  - o posto, o caminhão do exército, o blindado, o contêiner, os sacos de areia e o alambrado: os modelos de
    ///    <see cref="ModelosDaEstrada"/>, feitos uma vez e reaproveitados, com as caixas de colisão deles;
    ///  - as placas: a chapa e os postes (uma malha por placa) e as letras (<see cref="Letreiro"/>), refeitas se o atlas da
    ///    fonte mudar.
    /// Fica no objeto "Mundo" da cena Mundo; os modelos e os materiais vêm da montagem da cena.
    /// </summary>
    public class ObjetosDoMundo : MonoBehaviour
    {
        public Material materialDaCidade;
        [Tooltip("O material das letras das placas (shader ZombieCityCleaner/Letreiro); a cena faz uma cópia clara e uma escura.")]
        public Material materialDasLetras;
        [Tooltip("Os carros, na ordem de ModelosDaEstrada.NomesDosCarros.")]
        public Mesh[] carros;
        [Tooltip("O kit de cada carro (Prefabs/Veiculos/Mundo), na ordem de ModelosDaEstrada.NomesDosCarros: o carro com o capô que abre, as rodas e o macaco (o PecasDosCarrosDoMundo guarda o estado). Sem kit, o carro é só a malha.")]
        public GameObject[] kitsDosCarros;
        public Mesh barreira, cavalete, cone;
        [Tooltip("As ruas das cidades (Parte M5): o poste, a lente dele, as árvores (a verde e a seca), o hidrante e a lixeira.")]
        public Mesh poste, posteLente, arvore, arvoreSeca, hidrante, lixeira;
        [Tooltip("Até onde os objetos aparecem em volta do jogador local (m).")]
        public float raio = 170f;
        [Tooltip("Tempo máximo por quadro (ms) para criar objetos; o resto fica para os próximos.")]
        [Min(0.2f)] public float orcamentoMs = 1.5f;
        public float intervaloDeRevisao = 0.25f;

        public static ObjetosDoMundo Instancia { get; private set; }
        /// <summary>O maior tempo de um quadro criando objetos desde a última leitura (ms; quem mede zera).</summary>
        public static float MaiorQuadroMs;
        public int Blocos => _blocos.Count;

        sealed class Bloco
        {
            public Vector2Int Chave;
            public GameObject Objeto;
            public int Proximo;
            public bool Feito;
            public float QueridoEm, Distancia;
            public readonly List<Mesh> Malhas = new List<Mesh>();
        }

        sealed class ModeloPronto
        {
            public Mesh Malha;
            public List<ModelosDaEstrada.Caixa> Colisao;
            /// <summary>A colisão em malha (a loja de conveniência do posto, já cozida) e o interior dela; null nos outros.</summary>
            public Mesh MalhaDeColisao;
            public ModelosDaEstrada.Modelo Modelo;
        }

        sealed class Letras
        {
            public MeshFilter Filtro;
            public List<ModelosDaEstrada.Texto> Textos;
            public int Cor;
        }

        // as cores das letras, na ordem de ModelosDaEstrada.LetraClara, LetraEscura e LetraVermelha
        static readonly Color[] CoresDasLetras = { new Color(0.78f, 0.77f, 0.71f), new Color(0.12f, 0.2f, 0.15f), new Color(0.48f, 0.1f, 0.09f) };

        readonly Dictionary<Vector2Int, Bloco> _blocos = new Dictionary<Vector2Int, Bloco>();
        readonly Dictionary<int, ModeloPronto> _modelos = new Dictionary<int, ModeloPronto>();
        readonly List<Letras> _letras = new List<Letras>();
        readonly List<Bloco> _fila = new List<Bloco>();
        readonly List<Bloco> _sobra = new List<Bloco>();
        DadosDoMundo _dados;
        Transform _raiz;
        // (sem guardar: na recarga do Play, a Unity devolveria o vetor nulo como vazio)
        [System.NonSerialized] Material[] _letrasPorCor;
        int _versaoDasLetras = -1;
        float _proximaRevisao;

        void Awake() => Instancia = this;

        void OnDestroy()
        {
            Limpar();
            foreach (var m in _modelos.Values)
            {
                Destroy(m.Malha);
                if (m.MalhaDeColisao != null) Destroy(m.MalhaDeColisao);
            }
            _modelos.Clear();
            if (_letrasPorCor != null) foreach (var m in _letrasPorCor) Destroy(m);
            if (Instancia == this) Instancia = null;
        }

        void Limpar()
        {
            foreach (var b in _blocos.Values) Apagar(b);
            _blocos.Clear();
            _letras.Clear();
            if (_raiz != null) Destroy(_raiz.gameObject);
            _raiz = null;
            _dados = null;
        }

        void Update()
        {
            var mundo = MundoDaPartida.Instancia;
            var dados = mundo != null ? mundo.Dados : null;
            if (dados != _dados)
            {
                Limpar();
                _dados = dados;
                if (dados != null) _raiz = new GameObject("Beira das estradas").transform;
            }
            if (_dados?.Objetos == null) return;
            if (_versaoDasLetras != Letreiro.Versao) RefazerAsLetras();
            if (Time.unscaledTime >= _proximaRevisao)
            {
                _proximaRevisao = Time.unscaledTime + intervaloDeRevisao;
                Revisar();
            }
            // os objetos um a um, dos blocos mais perto, até o tempo do quadro acabar
            _relogio.Restart();
            foreach (var b in _fila)
            {
                if (b.Feito) continue;
                Montar(b);
                if (_relogio.Elapsed.TotalMilliseconds > orcamentoMs) break;
            }
            MaiorQuadroMs = Mathf.Max(MaiorQuadroMs, (float)_relogio.Elapsed.TotalMilliseconds);
        }

        readonly System.Diagnostics.Stopwatch _relogio = new System.Diagnostics.Stopwatch();

        void Revisar()
        {
            Vector3 centro;
            var local = JogadorRede.Local;
            if (local != null) centro = local.transform.position;
            else if (Camera.main != null) centro = Camera.main.transform.position;
            else return;
            float agora = Time.unscaledTime;
            var min = _dados.BlocoDe(centro.x - raio, centro.z - raio);
            var max = _dados.BlocoDe(centro.x + raio, centro.z + raio);
            for (int bz = min.y; bz <= max.y; bz++)
            for (int bx = min.x; bx <= max.x; bx++)
            {
                if (_dados.Objetos.DoBloco(bx, bz) == null) continue;
                var c = _dados.CentroDoBloco(bx, bz);
                float d = Vector2.Distance(c, new Vector2(centro.x, centro.z)) - DadosDoMundo.TamanhoDoBloco * 0.7f;
                if (d > raio) continue;
                var chave = new Vector2Int(bx, bz);
                if (!_blocos.TryGetValue(chave, out var b))
                {
                    b = new Bloco { Chave = chave };
                    _blocos.Add(chave, b);
                }
                b.QueridoEm = agora;
                b.Distancia = d;
            }
            _sobra.Clear();
            foreach (var b in _blocos.Values)
                if (agora - b.QueridoEm > 3f) _sobra.Add(b);
            foreach (var b in _sobra)
            {
                Apagar(b);
                _blocos.Remove(b.Chave);
            }
            // os mais perto primeiro
            _fila.Clear();
            foreach (var b in _blocos.Values) if (!b.Feito) _fila.Add(b);
            _fila.Sort((a, b) => a.Distancia.CompareTo(b.Distancia));
        }

        void Apagar(Bloco b)
        {
            if (b.Objeto != null)
            {
                foreach (var f in b.Objeto.GetComponentsInChildren<MeshFilter>(true))
                    _letras.RemoveAll(l => l.Filtro == f);
                Destroy(b.Objeto);
            }
            foreach (var m in b.Malhas) Destroy(m);
            b.Malhas.Clear();
        }

        // continua o bloco de onde parou, até acabar o tempo do quadro
        void Montar(Bloco b)
        {
            var lista = _dados.Objetos.DoBloco(b.Chave.x, b.Chave.y);
            if (lista == null)
            {
                b.Feito = true;
                return;
            }
            if (b.Objeto == null)
            {
                b.Objeto = new GameObject($"Beira {b.Chave.x}, {b.Chave.y}");
                b.Objeto.transform.SetParent(_raiz, false);
            }
            while (b.Proximo < lista.Length)
            {
                int indice = lista[b.Proximo++];
                Criar(_dados.Objetos.Todos[indice], indice, b);
                if (_relogio.Elapsed.TotalMilliseconds > orcamentoMs) return;
            }
            b.Feito = true;
        }

        // ------------------------------------------------------------------ cada objeto

        void Criar(ObjetoDaEstrada o, int indice, Bloco b)
        {
            if (o.Tipo == TipoDeObjeto.Carro && CriarComKit(o, indice, b)) return;
            Mesh malha;
            List<ModelosDaEstrada.Caixa> caixas = null;
            List<ModelosDaEstrada.Texto> textos = null;
            ModeloPronto comInterior = null;
            switch (o.Tipo)
            {
                case TipoDeObjeto.Carro: malha = carros != null && carros.Length > 0 ? carros[o.Variante % carros.Length] : null; break;
                case TipoDeObjeto.Barreira: malha = barreira; break;
                case TipoDeObjeto.Cavalete: malha = cavalete; break;
                case TipoDeObjeto.Cone: malha = cone; break;
                // as ruas das cidades: as colisões só no tronco (o braço do poste e a copa ficam por cima)
                case TipoDeObjeto.Poste:
                    malha = poste;
                    caixas = new List<ModelosDaEstrada.Caixa> { new ModelosDaEstrada.Caixa { Centro = new Vector3(0f, 3.05f, 0f), Tamanho = new Vector3(0.24f, 6.1f, 0.24f) } };
                    break;
                case TipoDeObjeto.Arvore:
                    malha = o.Variante == 1 ? arvoreSeca : arvore;
                    caixas = new List<ModelosDaEstrada.Caixa> { new ModelosDaEstrada.Caixa { Centro = new Vector3(0f, 1.5f, 0f), Tamanho = new Vector3(0.36f, 3f, 0.36f) } };
                    break;
                case TipoDeObjeto.Hidrante: malha = hidrante; break;
                case TipoDeObjeto.Lixeira: malha = lixeira; break;
                case TipoDeObjeto.PlacaDeDirecao:
                case TipoDeObjeto.PlacaDeEntrada:
                case TipoDeObjeto.PlacaDeAviso:
                case TipoDeObjeto.PlacaDeRua:
                {
                    var m = o.Tipo == TipoDeObjeto.PlacaDeDirecao ? ModelosDaEstrada.PlacaDeDirecao(o.Linhas)
                          : o.Tipo == TipoDeObjeto.PlacaDeEntrada ? ModelosDaEstrada.PlacaDeEntrada(o.Linhas, o.Variante)
                          : o.Tipo == TipoDeObjeto.PlacaDeRua ? ModelosDaEstrada.PlacaDeRua(o.Linhas)
                          : ModelosDaEstrada.PlacaDeAviso(o.Linhas);
                    malha = m.Malha.ParaMesh("Placa");
                    b.Malhas.Add(malha);
                    caixas = m.Colisao;
                    textos = m.Textos;
                    break;
                }
                default:
                {
                    var pronto = Modelo(o.Tipo, o.Variante);
                    malha = pronto.Malha;
                    caixas = pronto.Colisao;
                    if (pronto.MalhaDeColisao != null) comInterior = pronto;
                    break;
                }
            }
            if (malha == null) return;

            var go = new GameObject(o.Tipo.ToString());
            go.transform.SetParent(b.Objeto.transform, false);
            go.transform.SetPositionAndRotation(Posicao(o, malha, out var giro), giro);
            go.AddComponent<MeshFilter>().sharedMesh = malha;
            go.AddComponent<MeshRenderer>().sharedMaterial = materialDaCidade;
            if (caixas != null)
                foreach (var c in caixas)
                {
                    var bc = go.AddComponent<BoxCollider>();
                    bc.center = c.Centro;
                    bc.size = c.Tamanho;
                }
            else
            {
                var bc = go.AddComponent<BoxCollider>();
                bc.center = malha.bounds.center;
                bc.size = malha.bounds.size;
            }
            if (textos != null)
                for (int cor = 0; cor < CoresDasLetras.Length; cor++) PorLetras(go.transform, textos, cor, b);
            if (o.Tipo == TipoDeObjeto.Poste) PorLenteDoPoste(go);
            if (comInterior != null) PorInterior(go, comInterior);
        }

        // a loja de conveniência do posto (Parte M6, terceira fatia): a colisão em malha (cozida uma vez, no modelo), os
        // pisos para os passos, a caixa de dentro (a neblina de dentro) e o eco
        static void PorInterior(GameObject go, ModeloPronto pronto)
        {
            go.AddComponent<MeshCollider>().sharedMesh = pronto.MalhaDeColisao;
            var m = pronto.Modelo;
            MoveisDaConstrucao.Por(go, m.Moveis);
            var pisos = go.AddComponent<PisoPorZona>();
            pisos.zonas = new Bounds[m.Zonas.Count];
            pisos.superficies = new Superficie[m.Zonas.Count];
            for (int i = 0; i < m.Zonas.Count; i++)
            {
                pisos.zonas[i] = new Bounds(m.Zonas[i].Centro, m.Zonas[i].Tamanho);
                pisos.superficies[i] = TerrenoDoMundo.SomDoPiso(m.Zonas[i].Piso);
            }
            pisos.resto = Superficie.Concreto;
            var dentro = new GameObject("Dentro");
            dentro.transform.SetParent(go.transform, false);
            dentro.transform.localPosition = m.CentroDeDentro;
            dentro.AddComponent<AreaInterna>().tamanho = m.TamanhoDeDentro;
            var eco = new GameObject("Eco").AddComponent<AudioReverbZone>();
            eco.transform.SetParent(go.transform, false);
            eco.transform.localPosition = m.CentroDeDentro;
            eco.reverbPreset = AudioReverbPreset.Room;
            eco.minDistance = Mathf.Min(m.TamanhoDeDentro.x, m.TamanhoDeDentro.z) * 0.4f;
            eco.maxDistance = Mathf.Max(m.TamanhoDeDentro.x, m.TamanhoDeDentro.z) * 0.6f;
        }

        // a lente do poste (uma peça à parte no Cidade_Rua.fbx), apagada: anos depois do apocalipse, não há energia
        void PorLenteDoPoste(GameObject go)
        {
            if (posteLente == null) return;
            var lente = new GameObject("Lente");
            lente.transform.SetParent(go.transform, false);
            lente.AddComponent<MeshFilter>().sharedMesh = posteLente;
            var rl = lente.AddComponent<MeshRenderer>();
            rl.sharedMaterial = materialDaCidade;
            rl.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // onde e como o objeto fica: no chão (ou no asfalto), girado, seguindo a subida da pista ou deitado no barranco, e
        // tombado; a parte mais baixa dele encosta no chão
        Vector3 Posicao(ObjetoDaEstrada o, Mesh malha, out Quaternion giro)
        {
            float x = o.Posicao.x, z = o.Posicao.y;
            giro = Quaternion.Euler(0f, o.Giro, 0f);
            float y = _dados.Altura(x, z);
            if (o.NaEstrada) y += RedeDeEstradas.AcimaDoChao;
            if (o.NaCalcada) y += TracadoDaCidade.AlturaDaCalcada;
            if (o.NaGaragem) y += 0.05f;   // o chão de concreto da garagem (ou da vaga), no patamar da casa
            var frente = giro * Vector3.forward;
            if (o.Tipo == TipoDeObjeto.Carro && o.NaEstrada)
            {
                float hf = _dados.Altura(x + frente.x * 2f, z + frente.z * 2f), ht = _dados.Altura(x - frente.x * 2f, z - frente.z * 2f);
                giro *= Quaternion.Euler(-Mathf.Atan2(hf - ht, 4f) * Mathf.Rad2Deg, 0f, 0f);
            }
            if (o.NoTalude)
            {
                const float r = 1.5f;
                float hx1 = _dados.Altura(x + r, z), hx0 = _dados.Altura(x - r, z), hz1 = _dados.Altura(x, z + r), hz0 = _dados.Altura(x, z - r);
                var normal = new Vector3(hx0 - hx1, 2f * r, hz0 - hz1).normalized;
                giro = Quaternion.FromToRotation(Vector3.up, normal) * giro;
            }
            if (Mathf.Abs(o.Tombo) > 0.01f) giro *= Quaternion.Euler(0f, 0f, o.Tombo);
            // a parte mais baixa da malha (girada) encosta no chão
            var bb = malha.bounds;
            float menor = float.MaxValue;
            for (int k = 0; k < 8; k++)
            {
                var canto = new Vector3((k & 1) == 0 ? bb.min.x : bb.max.x, (k & 2) == 0 ? bb.min.y : bb.max.y, (k & 4) == 0 ? bb.min.z : bb.max.z);
                menor = Mathf.Min(menor, (giro * canto).y);
            }
            // (o alambrado e o aviso inclinam com a subida da serra: o meio fica no chão)
            bool acompanha = o.Tipo == TipoDeObjeto.Alambrado || o.Tipo == TipoDeObjeto.PlacaDeAviso;
            if ((Mathf.Abs(o.Tombo) > 0.01f && !acompanha) || o.NoTalude) y -= menor;
            if (o.EmCima) y += 2.59f;
            return new Vector3(x, y, z);
        }

        // ------------------------------------------------------------------ os carros com o kit (o capô, as rodas e o macaco)

        // o kit de cada modelo (o CapoDaCidade do prefab, com os pontos das rodas e do macaco) e o material dele
        CapoDaCidade[] _capoDoKit;
        Material[] _materialDoKit;

        CapoDaCidade KitDe(int variante, out GameObject prefab)
        {
            prefab = null;
            if (kitsDosCarros == null || kitsDosCarros.Length == 0) return null;
            int i = variante % kitsDosCarros.Length;
            prefab = kitsDosCarros[i];
            if (prefab == null) return null;
            if (_capoDoKit == null || _capoDoKit.Length != kitsDosCarros.Length)
            {
                _capoDoKit = new CapoDaCidade[kitsDosCarros.Length];
                _materialDoKit = new Material[kitsDosCarros.Length];
            }
            if (_capoDoKit[i] == null)
            {
                _capoDoKit[i] = prefab.GetComponentInChildren<CapoDaCidade>(true);
                var mr = prefab.GetComponent<MeshRenderer>();
                _materialDoKit[i] = mr != null ? mr.sharedMaterial : null;
            }
            return _capoDoKit[i];
        }

        /// <summary>O carro abre o capô e troca as rodas (de pé: o capotado e o de lado, não).</summary>
        static bool ComKit(ObjetoDaEstrada o) => o.Tipo == TipoDeObjeto.Carro && Mathf.Abs(o.Tombo) < 20f;

        bool CriarComKit(ObjetoDaEstrada o, int indice, Bloco b)
        {
            if (!ComKit(o) || carros == null || carros.Length == 0) return false;
            var kit = KitDe(o.Variante, out var prefab);
            var malha = carros[o.Variante % carros.Length];
            if (kit == null || malha == null) return false;
            var go = Instantiate(prefab, b.Objeto.transform, false);
            go.name = "Carro";
            go.transform.SetPositionAndRotation(Posicao(o, malha, out var giro), giro);
            // o material do mundo (com o abandono) no lugar do da montagem
            var doKit = _materialDoKit[o.Variante % kitsDosCarros.Length];
            if (materialDaCidade != null && doKit != null)
                foreach (var mr in go.GetComponentsInChildren<MeshRenderer>(true))
                    if (mr.sharedMaterial == doKit) mr.sharedMaterial = materialDaCidade;
            var capo = go.GetComponentInChildren<CapoDaCidade>(true);
            capo.pecas = PecasDosCarrosDoMundo.Instancia;
            capo.indice = indice;
            // a colisão num filho, que inclina com o carro (sem roda ou no macaco)
            var colisao = new GameObject("Colisão").transform;
            colisao.SetParent(go.transform, false);
            var bc = colisao.gameObject.AddComponent<BoxCollider>();
            bc.center = malha.bounds.center;
            bc.size = malha.bounds.size;
            capo.colisao = colisao;
            return true;
        }

        /// <summary>Onde está o carro do mundo (pelos dados, sem precisar do objeto: o anfitrião confere pedidos de longe).</summary>
        public bool PoseDoCarro(int indice, out Vector3 posicao, out Quaternion giro, out CapoDaCidade kit)
        {
            posicao = default;
            giro = Quaternion.identity;
            kit = null;
            if (_dados?.Objetos == null || indice < 0 || indice >= _dados.Objetos.Todos.Count) return false;
            var o = _dados.Objetos.Todos[indice];
            if (!ComKit(o) || carros == null || carros.Length == 0) return false;
            kit = KitDe(o.Variante, out _);
            var malha = carros[o.Variante % carros.Length];
            if (kit == null || malha == null) return false;
            posicao = Posicao(o, malha, out giro);
            return true;
        }

        /// <summary>
        /// Onde está o carro do mundo e a caixa dele (a da colisão, no espaço do carro), pelos dados: o alarme confere se o
        /// tiro pegou nele e põe o pisca-alerta nos cantos (Vehicles/PecasDosCarrosDoMundo.Alarme.cs).
        /// </summary>
        public bool CaixaDoCarro(int indice, out Vector3 posicao, out Quaternion giro, out Bounds caixa)
        {
            caixa = default;
            if (!PoseDoCarro(indice, out posicao, out giro, out _)) return false;
            caixa = carros[_dados.Objetos.Todos[indice].Variante % carros.Length].bounds;
            return true;
        }

        /// <summary>O meio do capô do carro do mundo (onde o pedido no motor conta a distância).</summary>
        public bool PontoDoCapo(int indice, out Vector3 ponto)
        {
            ponto = default;
            if (!PoseDoCarro(indice, out var pos, out var giro, out var kit)) return false;
            ponto = pos + giro * kit.transform.localPosition;
            return true;
        }

        /// <summary>O meio da roda do carro do mundo e o lado de fora dela (para onde cai o que não coube na mochila).</summary>
        public bool PontoDaRoda(int indice, int roda, out Vector3 ponto, out Vector3 fora)
        {
            ponto = fora = default;
            if (!PoseDoCarro(indice, out var pos, out var giro, out var kit)) return false;
            if (kit.centrosDasRodas == null || roda < 0 || roda >= kit.centrosDasRodas.Length) return false;
            var c = kit.centrosDasRodas[roda];
            ponto = pos + giro * c;
            fora = giro * new Vector3(Mathf.Sign(c.x), 0f, 0f);
            return true;
        }

        ModeloPronto Modelo(TipoDeObjeto tipo, int variante)
        {
            int chave = (int)tipo * 64 + variante;
            if (_modelos.TryGetValue(chave, out var pronto)) return pronto;
            ModelosDaEstrada.Modelo m = tipo switch
            {
                TipoDeObjeto.Posto => ModelosDaEstrada.Posto(variante),
                TipoDeObjeto.CaminhaoMilitar => ModelosDaEstrada.CaminhaoMilitar(),
                TipoDeObjeto.Blindado => ModelosDaEstrada.Blindado(),
                TipoDeObjeto.Conteiner => ModelosDaEstrada.Conteiner(variante),
                TipoDeObjeto.Sacos => ModelosDaEstrada.Sacos(),
                _ => ModelosDaEstrada.Alambrado(variante),
            };
            pronto = new ModeloPronto { Malha = m.Malha.ParaMesh($"{tipo} {variante}"), Colisao = m.Colisao, Modelo = m };
            if (m.MalhaDeColisao != null)
            {
                // cozida aqui, uma vez: os colisores de cada posto usam a mesma
                pronto.MalhaDeColisao = m.MalhaDeColisao.ParaMesh($"{tipo} {variante} (colisão)");
                Physics.BakeMesh(pronto.MalhaDeColisao.GetEntityId(), false);
            }
            _modelos[chave] = pronto;
            return pronto;
        }

        // ------------------------------------------------------------------ as letras

        Material MaterialDasLetras(int cor)
        {
            if (_letrasPorCor == null || _letrasPorCor.Length != CoresDasLetras.Length)
            {
                _letrasPorCor = new Material[CoresDasLetras.Length];
                for (int i = 0; i < _letrasPorCor.Length; i++)
                {
                    _letrasPorCor[i] = new Material(materialDasLetras) { name = $"Letras {i}" };
                    _letrasPorCor[i].SetColor("_Color", CoresDasLetras[i]);
                }
            }
            var atlas = Letreiro.Atlas;
            if (_letrasPorCor[0].mainTexture != atlas)
                foreach (var m in _letrasPorCor) m.SetTexture("_MainTex", atlas);
            return _letrasPorCor[cor];
        }

        void PorLetras(Transform placa, List<ModelosDaEstrada.Texto> textos, int cor, Bloco b)
        {
            if (materialDasLetras == null) return;
            var malha = Letreiro.Malha(textos, cor);
            if (malha == null) return;
            b.Malhas.Add(malha);
            var go = new GameObject("Letras");
            go.transform.SetParent(placa, false);
            var filtro = go.AddComponent<MeshFilter>();
            filtro.sharedMesh = malha;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = MaterialDasLetras(cor);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _letras.Add(new Letras { Filtro = filtro, Textos = textos, Cor = cor });
        }

        void RefazerAsLetras()
        {
            _versaoDasLetras = Letreiro.Versao;
            _letras.RemoveAll(l => l.Filtro == null);
            foreach (var l in _letras) Letreiro.Malha(l.Textos, l.Cor, l.Filtro.sharedMesh);
            if (_letrasPorCor != null) MaterialDasLetras(0);
            // pedir as letras pode refazer o atlas de novo: a versão nova fica para o próximo quadro
        }
    }
}
