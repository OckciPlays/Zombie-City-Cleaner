using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ZombieCityCleaner.Zombies
{
    /// <summary>
    /// Os membros que caem (desmembramento), em todas as máquinas: um pool de pedaços com física, na camada dos corpos
    /// (só batem no cenário e uns nos outros). O pedaço nasce onde o osso do corte está, com a malha do que caiu (feita
    /// na hora pelo <see cref="Membros"/>), sai voando na direção do tiro e, como os corpos, para, afunda e volta ao pool.
    /// </summary>
    public partial class ZombieManager
    {
        [Header("Pedaços (desmembramento)")]
        public int pedacosNoPool = 24;
        [Tooltip("A velocidade do pedaço que cai: na direção do tiro e para cima (m/s).")]
        public Vector2 empurraoDoPedaco = new Vector2(2.2f, 1.6f);

        class Pedaco
        {
            public GameObject go;
            public MeshFilter filtro;
            public MeshRenderer pele;
            public Rigidbody corpo;
            public BoxCollider caixa;
            public float caiuEm;
            public bool emUso, parado;
            // os zumbis leves: a cópia do material do zumbi (o pedaço não muda se o zumbi voltar do pool com outra cara)
            public Material material;
            public Material[] materiais;
        }

        readonly List<Pedaco> _pedacos = new List<Pedaco>();
        Transform _pastaDosPedacos;

        Pedaco PegarPedaco()
        {
            Pedaco livre = null;
            foreach (var p in _pedacos)
                if (!p.emUso) { livre = p; break; }
            if (livre == null && _pedacos.Count < pedacosNoPool)
            {
                if (_pastaDosPedacos == null) _pastaDosPedacos = new GameObject("Pedaços").transform;
                var go = new GameObject("Pedaço") { layer = CorpoDeZumbi.Camada };
                go.transform.SetParent(_pastaDosPedacos, false);
                livre = new Pedaco
                {
                    go = go,
                    filtro = go.AddComponent<MeshFilter>(),
                    pele = go.AddComponent<MeshRenderer>(),
                    corpo = go.AddComponent<Rigidbody>(),
                    caixa = go.AddComponent<BoxCollider>(),
                };
                livre.corpo.mass = 3f;
                livre.corpo.linearDamping = 0.1f;
                livre.corpo.angularDamping = 0.4f;
                livre.corpo.interpolation = RigidbodyInterpolation.Interpolate;
                livre.pele.shadowCastingMode = ShadowCastingMode.On;
                go.SetActive(false);
                _pedacos.Add(livre);
            }
            if (livre == null)
            {
                // todos em uso: o mais antigo
                livre = _pedacos[0];
                foreach (var p in _pedacos)
                    if (p.caiuEm < livre.caiuEm) livre = p;
            }
            return livre;
        }

        /// <summary>Um membro cai: o pedaço nasce no osso do corte, com a malha dele, e voa na direção do tiro.</summary>
        internal void SoltarPedaco(Mesh malha, Material[] materiais, Transform osso, Vector3 direcao, Vector3 velocidade)
        {
            if (malha == null || osso == null) return;
            var p = PegarPedaco();
            p.go.SetActive(false);
            p.filtro.sharedMesh = malha;
            if (materiais.Length > 1 && Aparencia.Leve(materiais[0]))
            {
                if (p.material == null)
                {
                    p.material = new Material(materiais[0]) { name = materiais[0].name + " (pedaço)" };
                    p.materiais = new Material[2];
                }
                p.material.CopyPropertiesFromMaterial(materiais[0]);
                p.materiais[0] = p.material;
                p.materiais[1] = materiais[1];
                materiais = p.materiais;
            }
            p.pele.sharedMaterials = materiais;
            var b = malha.bounds;
            p.caixa.center = b.center;
            p.caixa.size = Vector3.Max(b.size, Vector3.one * 0.05f);
            p.go.transform.SetPositionAndRotation(osso.position, osso.rotation);
            p.go.SetActive(true);
            p.corpo.isKinematic = false;
            p.corpo.detectCollisions = true;
            p.corpo.position = osso.position;
            p.corpo.rotation = osso.rotation;
            direcao.y = 0f;
            p.corpo.linearVelocity = velocidade + direcao.normalized * empurraoDoPedaco.x + Vector3.up * empurraoDoPedaco.y;
            p.corpo.angularVelocity = Random.onUnitSphere * 6f;
            p.caiuEm = Time.time;
            p.emUso = true;
            p.parado = false;
        }

        void AtualizarPedacos(float agora, float dt)
        {
            foreach (var p in _pedacos)
            {
                if (!p.emUso) continue;
                float t = agora - p.caiuEm;
                if (t > tempoDoCorpo + 2.5f)
                {
                    p.emUso = false;
                    p.go.SetActive(false);
                }
                else if (t > tempoDoCorpo) p.go.transform.position += Vector3.down * (0.12f * dt);
                else if (t > tempoAteAssentar && !p.parado)
                {
                    p.parado = true;
                    p.corpo.isKinematic = true;
                    p.corpo.detectCollisions = false;
                }
            }
        }

        // ------------------------------------------------------------------ o aquecimento do começo (03/11/2026)
        //
        // O Profiler mostrou o começo no bunker com engasgos de 25 a 600 ms (um corte de um especial, de 24 mil vértices,
        // levou 342 ms mais 245 de coleta do lixo) por 1 a 3 s: os cortes dos 21 modelos (504 passos, ~10 s de trabalho) não
        // acabavam no carregamento e caíam no jogo. Agora:
        //  - os cortes só andam com a tela coberta: o carregamento do mundo (vários passos por quadro, até ~120 ms), uma tela
        //    cheia do menu ou o cinema (até ~25 ms, para a tela responder). No Dia 0, na rua, eles esperam;
        //  - o MundoDaPartida segura a tela de carregamento até eles ficarem prontos (CortesProntos);
        //  - se o mundo ficar pronto antes (um cliente, um tempo esgotado), um passo por quadro, como era.

        const float OrcamentoNoCarregamentoMs = 120f, OrcamentoNasTelasMs = 25f;
        readonly System.Diagnostics.Stopwatch _relogioDoAquecimento = new System.Diagnostics.Stopwatch();
        int _quadroDoAquecimento = -1, _passosDosCortes, _passosFeitos;

        /// <summary>Os cortes do começo estão prontos (o MundoDaPartida espera por eles para soltar a tela de carregamento).</summary>
        public bool CortesProntos { get; private set; }

        /// <summary>Quanto dos cortes do começo já foi feito (0 a 1, para a tela de carregamento).</summary>
        public float ProgressoDosCortes => _passosDosCortes > 0 ? Mathf.Clamp01(_passosFeitos / (float)_passosDosCortes) : 1f;

        /// <summary>O mundo está sendo carregado agora (fora do Dia 0): a tela de carregamento está na frente.</summary>
        static bool CarregandoOMundo
        {
            get
            {
                var m = World.MundoDaPartida.Instancia;
                return m != null && m.IsSpawned && !m.Pronto && !m.NoDiaZero;
            }
        }

        static bool NumaTelaCheia => UI.MenuOnline.Aberto || UI.MenuOnline.Cinema;

        /// <summary>
        /// O aquecimento pode trabalhar agora: com a tela coberta, ou com o mundo já pronto (o resto, um passo por quadro).
        /// Jogando no Dia 0 (a rua, a casa), não: espera a tela de carregamento do mundo.
        /// </summary>
        static bool PodeAquecer
        {
            get
            {
                if (CarregandoOMundo || NumaTelaCheia) return true;
                var m = World.MundoDaPartida.Instancia;
                return m == null || !m.IsSpawned || m.Pronto;
            }
        }

        /// <summary>Depois de cada passo do aquecimento: espera o próximo quadro (jogando, sempre; coberta, só depois do orçamento).</summary>
        bool EsperarOProximoQuadro()
        {
            if (Time.frameCount != _quadroDoAquecimento)
            {
                _quadroDoAquecimento = Time.frameCount;
                _relogioDoAquecimento.Restart();
            }
            float orcamento = CarregandoOMundo ? OrcamentoNoCarregamentoMs : NumaTelaCheia ? OrcamentoNasTelasMs : 0f;
            return _relogioDoAquecimento.Elapsed.TotalMilliseconds >= orcamento;
        }

        /// <summary>
        /// As malhas cortadas mais comuns e os pedaços delas, no começo da partida: cortar uma malha leva de 3 a 15 ms (nos
        /// especiais, até ~340) e o pedaço de 6 a 20 ms, e na hora do tiro isso virava um tranco. Ficam prontos os cortes de
        /// um membro só (nos dois pontos), as duas pernas juntas e os pedaços de cada corte. As malhas ficam guardadas (são
        /// estáticas). Só com a tela coberta (veja acima).
        /// </summary>
        System.Collections.IEnumerator PreaquecerCortes()
        {
            CortesProntos = false;
            _passosFeitos = 0;
            _passosDosCortes = 0;
            foreach (var modelo in modelos)
                if (modelo != null && modelo.malhaInteira != null) _passosDosCortes += Membros.Quantos * 5 + 4;
            float noRelogio = Time.realtimeSinceStartup;
            foreach (var modelo in modelos)
            {
                if (modelo == null || modelo.malhaInteira == null) continue;
                var ossos = modelo.esqueleto;
                for (int l = 0; l < Membros.Quantos; l++)
                    for (int e = 1; e <= 2; e++)
                    {
                        byte cortes = Membros.ComEstado(0, l, e);
                        int osso = Membros.IndiceDoOsso(ossos, Membros.OssoDoCorte(l, e));
                        while (!PodeAquecer) yield return null;
                        Membros.Variante(modelo.malhaInteira, ossos, cortes);
                        _passosFeitos++;
                        if (EsperarOProximoQuadro()) yield return null;
                        while (!PodeAquecer) yield return null;
                        Membros.Pedaco(modelo.malhaInteira, ossos, 0, cortes, osso);
                        _passosFeitos++;
                        if (EsperarOProximoQuadro()) yield return null;
                        if (e == 2)
                        {
                            while (!PodeAquecer) yield return null;
                            Membros.Pedaco(modelo.malhaInteira, ossos, Membros.ComEstado(0, l, 1), cortes, osso);
                            _passosFeitos++;
                            if (EsperarOProximoQuadro()) yield return null;
                        }
                    }
                for (int a = 1; a <= 2; a++)
                    for (int b = 1; b <= 2; b++)
                    {
                        while (!PodeAquecer) yield return null;
                        Membros.Variante(modelo.malhaInteira, ossos, Membros.ComEstado(Membros.ComEstado(0, Membros.PernaE, a), Membros.PernaD, b));
                        _passosFeitos++;
                        if (EsperarOProximoQuadro()) yield return null;
                    }
            }
            CortesProntos = true;
            Debug.Log($"[Zumbis] Os cortes do começo ficaram prontos: {_passosFeitos} passos em {(Time.realtimeSinceStartup - noRelogio):0.0} s.");
        }

        void GuardarPedacos()
        {
            foreach (var p in _pedacos)
            {
                p.emUso = false;
                if (p.go != null) p.go.SetActive(false);
            }
        }
    }
}
