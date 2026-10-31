using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using ZombieCityCleaner.Loot;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// O retrato do jogador na tela do inventário: uma cópia do corpo, longe de tudo (500 m embaixo da cidade), numa
    /// camada só dela (<see cref="JogadorRede.CamadaRetrato"/>), com o material do retrato (a cor da paleta e uma luz
    /// fixa: não muda com a hora, a neblina nem o bunker) e uma câmera própria que desenha numa RenderTexture de fundo
    /// transparente. Ela gira (<see cref="Giro"/>).
    /// Com a pistola, a comida, a água ou as mãos vazias, a pose é feita aqui, sem o Animator (a pedido: a pose de parado
    /// da pistola, com as duas mãos na frente do peito, não ficava boa): o corpo em pé, respirando devagar, o braço
    /// esquerdo solto ao lado do corpo e o direito baixo, segurando o que está na mão (a pistola com o cano para baixo e
    /// para a frente). As armas de coronha (o rifle, a submetralhadora, a espingarda e a sniper) usam o parado delas, com
    /// as duas mãos e a arma baixa na frente do corpo: numa mão só, a coronha entrava no corpo.
    /// O resto do que ele leva fica no corpo (<see cref="EquipamentoNoCorpo"/>): a mochila, o coldre e as outras armas,
    /// refeitos quando o inventário muda.
    /// A cópia é feita a cada vez que a tela abre (pega a cor naquela hora) e some quando ela fecha.
    /// </summary>
    public class RetratoDoJogador
    {
        static readonly Vector3 Lugar = new Vector3(0f, -500f, 0f);

        // a pose, no espaço do corpo (olhando para +Z; +X é a direita dele): para onde cada osso do braço aponta
        // (não são constantes para poder ajustar com o jogo rodando)
        static Vector3 BracoDireito = new Vector3(0.14f, -0.97f, 0.2f);
        static Vector3 AntebracoDireitoSolto = new Vector3(0.06f, -0.95f, 0.3f);
        static Vector3 BracoEsquerdo = new Vector3(-0.14f, -0.97f, 0.12f);
        static Vector3 AntebracoEsquerdo = new Vector3(-0.07f, -0.95f, 0.3f);
        // para onde aponta o que está na mão direita: o cano da pistola 39° para baixo e um pouco para fora; a lata e a
        // garrafa em pé, com a frente para a frente
        static Vector3 CanoDaPistola = new Vector3(0.12f, -0.63f, 0.78f);
        static Vector3 FrenteDoItem = new Vector3(0.1f, -0.1f, 1f);
        const float Respiracao = 4f;         // segundos por respiração

        readonly GameObject _raiz;
        readonly Transform _modelo;
        readonly Camera _camera;
        readonly GameObject[] _naMao, _naMaoOriginal;
        readonly List<Material> _materiais = new List<Material>();
        // as cópias do material do retrato com a textura de um material próprio (a pistola nova), uma por material
        readonly Dictionary<Material, Material> _copiasDosProprios = new Dictionary<Material, Material>();
        readonly InventarioJogador _inventario;
        readonly EquipamentoNoCorpo _equipamento;
        readonly Transform _referencia;
        readonly Animator _animador, _original;
        static readonly int SpeedId = Animator.StringToHash("Speed");
        long _vestido = -1;
        RenderTexture _imagem;

        // os ossos da pose e a rotação de repouso deles
        readonly Transform _peito, _bracoD, _antebracoD, _maoD, _bracoE, _antebracoE, _maoE;
        readonly Quaternion _peitoRepouso, _bracoDRepouso, _antebracoDRepouso, _maoDRepouso, _bracoERepouso, _antebracoERepouso, _maoERepouso;
        readonly Quaternion _pulsoReto;    // a mão direita no antebraço, com o pulso reto (o repouso do esqueleto)

        /// <summary>O giro do modelo, em graus (0 = de frente). Começa um pouco de lado, mostrando a arma da mão.</summary>
        public float Giro = 20f;
        public RenderTexture Imagem => _imagem;

        public RetratoDoJogador(JogadorRede rede, Material material, int largura, int altura)
        {
            var original = _original = rede.animadorCorpo;
            _inventario = rede.GetComponent<InventarioJogador>();
            _raiz = new GameObject("Retrato do jogador");
            _raiz.transform.position = Lugar;
            var copia = Object.Instantiate(original.gameObject, _raiz.transform, false);
            copia.name = "Corpo";
            // o equipamento do corpo do jogador (o EquipamentoDoJogador) vem junto na cópia: sai, e o retrato põe o dele
            EquipamentoNoCorpo.TirarCopiadas(copia.transform);
            foreach (var e in copia.GetComponents<EquipamentoNoCorpo>()) Object.Destroy(e);
            copia.transform.localPosition = Vector3.zero;
            copia.transform.localRotation = Quaternion.identity;
            // as roupas: as mesmas peças e cores do jogador (a cópia não leva as cores, que ficam fora do material)
            var vestimenta = copia.GetComponent<ZombieCityCleaner.Roupas.Vestimenta>();
            if (vestimenta != null && rede.Vestimenta != null) vestimenta.VestirComo(rede.Vestimenta);
            _modelo = copia.transform;
            // sem animação: a pose é feita à mão (abaixo), a partir da de repouso do prefab
            _animador = copia.GetComponent<Animator>();
            if (_animador != null)
            {
                _animador.enabled = false;
                _animador.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }
            var prefab = NetworkManager.Singleton != null ? NetworkManager.Singleton.NetworkConfig.PlayerPrefab : null;
            _referencia = prefab != null ? prefab.transform.Find(original.name) : null;   // o corpo do mesmo personagem
            if (_referencia != null) Repouso(_referencia, copia.transform);

            foreach (var t in copia.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = JogadorRede.CamadaRetrato;
            // o material do retrato com a textura (e o deslocamento da variação da cor) de cada material do corpo
            foreach (var r in copia.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
                r.enabled = true;   // no dono, o que está na mão do corpo fica desligado
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = Copia(material, mats[i]);
                r.sharedMaterials = mats;
                if (r is SkinnedMeshRenderer pele) pele.updateWhenOffscreen = true;
            }
            // o que está na mão: as mesmas peças na cópia, pelo nome
            _naMaoOriginal = rede.naMaoCorpo;
            _naMao = new GameObject[_naMaoOriginal.Length];
            for (int i = 0; i < _naMao.Length; i++) _naMao[i] = Buscar(copia.transform, _naMaoOriginal[i].name)?.gameObject;

            // os ossos da pose
            _peito = Buscar(_modelo, "chest");
            _bracoD = Buscar(_modelo, "upper_arm.R");
            _antebracoD = Buscar(_modelo, "forearm.R");
            _maoD = Buscar(_modelo, "hand.R");
            _bracoE = Buscar(_modelo, "upper_arm.L");
            _antebracoE = Buscar(_modelo, "forearm.L");
            _maoE = Buscar(_modelo, "hand.L");
            if (_peito != null) _peitoRepouso = _peito.localRotation;
            if (_bracoD != null) _bracoDRepouso = _bracoD.localRotation;
            if (_antebracoD != null) _antebracoDRepouso = _antebracoD.localRotation;
            if (_maoD != null)
            {
                _maoDRepouso = _maoD.localRotation;
                // o pulso reto: a mão no antebraço na pose de ligação da malha (a pose T); sem ela, a do prefab
                _pulsoReto = _antebracoD != null ? Quaternion.Inverse(_antebracoD.rotation) * _maoD.rotation : Quaternion.identity;
                foreach (var pele in copia.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    int f = System.Array.IndexOf(pele.bones, _antebracoD), h = System.Array.IndexOf(pele.bones, _maoD);
                    if (f < 0 || h < 0 || pele.sharedMesh == null) continue;
                    var ligacao = pele.sharedMesh.bindposes;
                    _pulsoReto = (ligacao[f] * ligacao[h].inverse).rotation;
                    break;
                }
            }
            if (_bracoE != null) _bracoERepouso = _bracoE.localRotation;
            if (_antebracoE != null) _antebracoERepouso = _antebracoE.localRotation;
            if (_maoE != null) _maoERepouso = _maoE.localRotation;

            // o equipamento no corpo, com o material do retrato (a paleta das armas)
            _equipamento = copia.AddComponent<EquipamentoNoCorpo>();
            _equipamento.semSombra = true;
            var mundo = ItensNoMundo.Instancia;
            _equipamento.material = Copia(material, mundo != null ? mundo.material : null);
            _equipamento.copiaDoProprio = proprio =>
            {
                if (!_copiasDosProprios.TryGetValue(proprio, out var c)) _copiasDosProprios[proprio] = c = Copia(material, proprio);
                return c;
            };

            var camGo = new GameObject("Câmera do retrato");
            camGo.transform.SetParent(_raiz.transform, false);
            camGo.transform.localPosition = new Vector3(0f, 1.02f, 4.7f);
            camGo.transform.LookAt(Lugar + new Vector3(0f, 0.9f, 0f));
            _camera = camGo.AddComponent<Camera>();
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            _camera.cullingMask = 1 << JogadorRede.CamadaRetrato;
            _camera.fieldOfView = 25f;
            _camera.nearClipPlane = 1f;
            _camera.farClipPlane = 10f;
            var dados = camGo.AddComponent<UniversalAdditionalCameraData>();
            dados.renderPostProcessing = false;
            dados.renderShadows = false;
            dados.antialiasing = AntialiasingMode.None;
            // o renderizador 1 (o dos braços) não tem a neblina de altura
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp && urp.rendererDataList.Length > 1) dados.SetRenderer(1);
            // o dobro da resolução da tela (desenhado menor, fica sem serrilhado)
            _imagem = new RenderTexture(Mathf.Max(16, largura * 2), Mathf.Max(16, altura * 2), 24, RenderTextureFormat.ARGB32) { name = "Retrato do jogador" };
            _imagem.Create();
            _camera.targetTexture = _imagem;
            Atualizar();
        }

        // uma cópia do material do retrato com a textura (e o deslocamento da variação da cor) de outro material
        Material Copia(Material retrato, Material de)
        {
            // as roupas (o shader delas, com as cores por peça): a cópia do material delas na luz fixa do retrato
            if (de != null && de.shader != null && de.shader.name == "ZombieCityCleaner/Roupas")
            {
                var roupa = new Material(de);
                roupa.SetFloat("_Retrato", 1f);
                foreach (var p in new[] { "_Ambiente", "_Luz" })
                    if (retrato.HasProperty(p)) roupa.SetFloat(p, retrato.GetFloat(p));
                if (retrato.HasProperty("_DirecaoDaLuz")) roupa.SetVector("_DirecaoDaLuz", retrato.GetVector("_DirecaoDaLuz"));
                _materiais.Add(roupa);
                return roupa;
            }
            var m = new Material(retrato);
            if (de != null && de.mainTexture != null)
            {
                m.mainTexture = de.mainTexture;
                m.mainTextureOffset = de.mainTextureOffset;
                m.mainTextureScale = de.mainTextureScale;
            }
            _materiais.Add(m);
            return m;
        }

        // a cópia foi feita do corpo animado: volta cada osso para a pose de repouso do prefab
        static void Repouso(Transform referencia, Transform copia)
        {
            var ossos = new Dictionary<string, Transform>();
            foreach (var t in copia.GetComponentsInChildren<Transform>(true)) ossos[t.name] = t;
            foreach (var r in referencia.GetComponentsInChildren<Transform>(true))
            {
                if (r == referencia || !ossos.TryGetValue(r.name, out var t)) continue;
                t.localPosition = r.localPosition;
                t.localRotation = r.localRotation;
                t.localScale = r.localScale;
            }
        }

        static Transform Buscar(Transform raiz, string nome)
        {
            foreach (var t in raiz.GetComponentsInChildren<Transform>(true))
                if (t.name == nome) return t;
            return null;
        }

        /// <summary>A cada quadro, com a tela aberta: o giro, o que está na mão, o equipamento e a pose.</summary>
        public void Atualizar()
        {
            _modelo.localRotation = Quaternion.Euler(0f, Giro, 0f);   // o corpo olha para +Z, para a câmera
            GameObject naMao = null;
            for (int i = 0; i < _naMao.Length; i++)
            {
                if (_naMao[i] == null) continue;
                bool ligado = _naMaoOriginal[i].activeSelf;
                if (_naMao[i].activeSelf != ligado) _naMao[i].SetActive(ligado);
                if (ligado) naMao = _naMao[i];
            }
            Vestir();
            Posar(naMao);
        }

        // o que ele leva, menos o que está na mão: a mochila e as armas dos outros atalhos (como no corpo dele)
        readonly Item[] _armas = new Item[InventarioJogador.Atalhos];

        void Vestir()
        {
            if (_inventario == null || _referencia == null || !_inventario.IsSpawned) return;
            long chave = EquipamentoNoCorpo.OQueVeste(_inventario, out var mochila, _armas);
            if (chave == _vestido) return;
            _vestido = chave;
            _equipamento.Vestir(_referencia, mochila, _armas);
        }

        // a pose: as armas de coronha, o parado delas; o resto, o peito respirando, o braço esquerdo solto e o direito
        // baixo, segurando o que está na mão
        void Posar(GameObject naMao)
        {
            string nome = naMao != null ? naMao.name : null;
            bool comCoronha = nome != null && nome.StartsWith("Arma_") && nome != "Arma_Pistola";
            if (_animador != null)
            {
                if (comCoronha)
                {
                    if (_animador.runtimeAnimatorController != _original.runtimeAnimatorController)
                        _animador.runtimeAnimatorController = _original.runtimeAnimatorController;
                    if (!_animador.enabled) _animador.enabled = true;
                    _animador.SetFloat(SpeedId, 0f);
                    return;
                }
                if (_animador.enabled)
                {
                    // saindo da animação: os ossos voltam ao repouso antes da pose
                    _animador.enabled = false;
                    if (_referencia != null) Repouso(_referencia, _modelo);
                }
            }
            if (_peito != null)
            {
                float fase = Mathf.Sin(Time.realtimeSinceStartup * 2f * Mathf.PI / Respiracao);
                _peito.localRotation = _peitoRepouso * Quaternion.Euler(-0.8f * fase, 0f, 0f);
            }
            if (_bracoD == null || _antebracoD == null || _maoD == null || _bracoE == null || _antebracoE == null || _maoE == null) return;
            _bracoD.localRotation = _bracoDRepouso;
            _antebracoD.localRotation = _antebracoDRepouso;
            _maoD.localRotation = _maoDRepouso;
            _bracoE.localRotation = _bracoERepouso;
            _antebracoE.localRotation = _antebracoERepouso;
            _maoE.localRotation = _maoERepouso;

            Apontar(_bracoE, BracoEsquerdo);
            Apontar(_antebracoE, AntebracoEsquerdo);
            Apontar(_maoE, AntebracoEsquerdo);

            Apontar(_bracoD, BracoDireito);
            if (naMao == null)
            {
                Apontar(_antebracoD, AntebracoDireitoSolto);
                Apontar(_maoD, AntebracoDireitoSolto);
                return;
            }
            // a mão sai do que ela segura (no espaço da arma, o cano em -Y e o topo em +Z): o cano para a frente e para
            // baixo, o topo para cima; o antebraço vai junto, com o pulso reto (o do repouso do esqueleto). Girando a mão
            // a partir da pose do prefab, o pulso do principal em blocos dobrava para trás (a pedido, 28/10/2026)
            Vector3 cano = (nome.StartsWith("Item_") ? FrenteDoItem : CanoDaPistola).normalized;
            Vector3 topo = Vector3.ProjectOnPlane(Vector3.up, cano).normalized;
            var naMaoNaMao = Quaternion.Inverse(_maoD.rotation) * naMao.transform.rotation;
            var mao = _modelo.rotation * Quaternion.LookRotation(topo, -cano) * Quaternion.Inverse(naMaoNaMao);
            _antebracoD.rotation = mao * Quaternion.Inverse(_pulsoReto);
            _maoD.rotation = mao;
        }

        // gira o osso (o eixo Y dele vai ao longo do osso) para apontar na direção dada, no espaço do corpo
        void Apontar(Transform osso, Vector3 direcao)
        {
            var alvo = _modelo.TransformDirection(direcao.normalized);
            osso.rotation = Quaternion.FromToRotation(osso.up, alvo) * osso.rotation;
        }

        /// <summary>Onde os pés ficam na imagem (de 0 a 1, com o y de cima para baixo), para a sombra no chão.</summary>
        public Vector2 Pes
        {
            get
            {
                var v = _camera.WorldToViewportPoint(Lugar);
                return new Vector2(v.x, 1f - v.y);
            }
        }

        public void Destruir()
        {
            if (_camera != null) _camera.targetTexture = null;
            if (_imagem != null)
            {
                _imagem.Release();
                Object.Destroy(_imagem);
            }
            foreach (var m in _materiais) Object.Destroy(m);
            Object.Destroy(_raiz);
        }
    }
}
