using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using ZombieCityCleaner.Mundo;

namespace ZombieCityCleaner.World
{
    /// <summary>
    /// Os móveis novos de uma construção na cena (a etapa 4 do Plano_Mobilia.md da pasta de arte): de uma casa, dos
    /// apartamentos de uma loja ou de um andar de prédio, nas coordenadas do objeto dela (<see cref="MobiliaNova"/>). A
    /// <see cref="MobiliaNovaDoMundo"/> revisa todas a cada quadro (<see cref="Revisar"/>):
    ///  - os de longe (a malha montada em outra thread, <see cref="MalhasDasCasas"/>) viram Mesh perto do jogador local e
    ///    aparecem até o raio de longe;
    ///  - com ele na construção (e no andar dela), os de perto entram no lugar, poucos por quadro: os prefabs da mobília
    ///    nova, com as portas e as gavetas que abrem (<see cref="ParteDoMovel"/>, a chave de cada uma é a da obra, do ponto,
    ///    da peça e da parte: a mesma em todas as máquinas); com todos postos, os de longe somem. Quando ele sai, os de
    ///    perto saem e os de longe voltam;
    ///  - o saque das moradias dela (<see cref="Saque"/>, pela semente) entra com os de perto, nos móveis
    ///    (<see cref="MobiliaNovaDoMundo.PorOSaque"/>).
    /// A colisão é a das caixas na colisão da obra (<see cref="MobiliaNova.Caixas"/>), em todas as máquinas.
    /// </summary>
    public class MobiliaDaConstrucao : MonoBehaviour
    {
        static readonly List<MobiliaDaConstrucao> Todas = new List<MobiliaDaConstrucao>();
        static readonly List<MobiliaNova.Peca> _pecas = new List<MobiliaNova.Peca>();
        static readonly List<ParteDoMovel> _partesDoMovel = new List<ParteDoMovel>();

        /// <summary>Os móveis de perto em todas as construções.</summary>
        public static int QuantosDePerto { get; private set; }

        PontoDeMovel[] _pontos;
        MalhaDaMobilia _dados;
        int _semente, _andar;
        ulong _obra;
        bool _comChave, _comSaque;
        Bounds _caixa;                 // a dos pontos, nas coordenadas da obra
        Mesh _malha;
        MeshRenderer _longe;
        GameObject _perto;
        int _proximo;                  // o próximo ponto a pôr de perto
        bool _querPerto;
        int _dePerto;
        readonly List<ParteDoMovel> _partes = new List<ParteDoMovel>();
        readonly List<SaqueNaMira> _saqueDePerto = new List<SaqueNaMira>();

        /// <summary>
        /// Põe os móveis novos no objeto da construção ('andar': o do prédio, ou -100 numa casa ou loja). Fica com os de
        /// longe (devolvidos ao estoque quando viram Mesh ou quando a construção sai). Nada, sem móvel novo (nem móvel antigo
        /// com saque da farmácia ou do hospital: o saque dele entra de perto, sem o móvel, 05/11/2026). Sem
        /// 'comSaque', os móveis ficam vazios (as casas da rua do Dia 0).
        /// </summary>
        public static void Por(GameObject go, List<PontoDeMovel> pontos, MalhaDaMobilia dados, int semente, int andar, bool comSaque = true)
        {
            bool tem = false;
            if (pontos != null) foreach (var p in pontos) tem |= MobiliaNova.EhNovo(p.tipo) || (comSaque && AntigoComSaque(p));
            if (!tem || MobiliaNovaDoMundo.Instancia == null)
            {
                MalhaDaMobilia.Devolver(dados);
                return;
            }
            var m = go.AddComponent<MobiliaDaConstrucao>();
            m._pontos = pontos.ToArray();
            m._dados = dados;
            m._semente = semente;
            m._andar = andar;
            m._comSaque = comSaque;
            bool primeiro = true;
            foreach (var p in m._pontos)
            {
                // (os antigos com saque da farmácia e do hospital também: sem eles, numa farmácia com apartamento em cima, o
                // "perto" só ligaria nos andares de cima)
                if (!MobiliaNova.EhNovo(p.tipo) && !(comSaque && AntigoComSaque(p))) continue;
                var pe = p.Pe;
                var b = new Bounds(new Vector3(pe.center.x, p.centro.y + p.altura + p.tamanho.y / 2, pe.center.y), new Vector3(pe.width, p.tamanho.y, pe.height));
                if (primeiro) m._caixa = b;
                else m._caixa.Encapsulate(b);
                primeiro = false;
            }
        }

        /// <summary>
        /// O móvel antigo com saque de uma unidade de loja (a farmácia, o hospital; 05/11/2026): desenhado na malha da obra,
        /// sem prefab; de perto, só o saque em cima dele (<see cref="PorOSaqueDoAntigo"/>).
        /// </summary>
        static bool AntigoComSaque(PontoDeMovel p) => p.saque && p.DeLoja && !MobiliaNova.EhNovo(p.tipo);

        void OnEnable()
        {
            Todas.Add(this);
            if (_mostrarOSaque) MostrarOSaqueAqui();
        }

        void OnDisable()
        {
            Todas.Remove(this);
            TirarPerto();
            EsconderOSaque();
        }

        void OnDestroy()
        {
            MalhaDaMobilia.Devolver(_dados);
            _dados = null;
            if (_malha != null) Destroy(_malha);
            _malha = null;
        }

        // ------------------------------------------------------------------ o saque (o Plano_Saque.md, a parte 3: só o F1)

        List<ItemDoSaque> _saque;
        GameObject _caixasDoSaque;
        Mesh _malhaDoSaque;
        static bool _mostrarOSaque;

        /// <summary>
        /// O saque das moradias desta construção (<see cref="SaqueDaObra"/>, pela semente dela: o mesmo em todas as
        /// máquinas), nas coordenadas dela; sorteado na primeira vez que alguém pede.
        /// </summary>
        public List<ItemDoSaque> Saque
        {
            get
            {
                if (_saque == null)
                {
                    _saque = new List<ItemDoSaque>();
                    if (_comSaque) SaqueDaObra.Sortear(_pontos, _semente, _saque);
                }
                return _saque;
            }
        }

        /// <summary>A chave da construção na rede (<see cref="MobiliaNova.ChaveDaObra"/>: o lugar e o andar dela).</summary>
        public ulong Obra
        {
            get
            {
                if (!_comChave)
                {
                    _obra = MobiliaNova.ChaveDaObra(transform.position, _andar);
                    _comChave = true;
                }
                return _obra;
            }
        }

        /// <summary>A construção carregada com a chave (null: nenhuma).</summary>
        public static MobiliaDaConstrucao DaObra(ulong obra)
        {
            foreach (var m in Todas)
                if (m.Obra == obra) return m;
            return null;
        }

        /// <summary>O item do saque no lugar (o ponto do móvel, a peça e o lugar dela).</summary>
        public bool AcharNoSaque(int ponto, int peca, int lugar, out ItemDoSaque item)
        {
            foreach (var it in Saque)
                if (it.ponto == ponto && it.peca == peca && it.lugar == lugar)
                {
                    item = it;
                    return true;
                }
            item = default;
            return false;
        }

        /// <summary>
        /// O F1 do mundo: uma caixa da cor da raridade em cada item sorteado, do tamanho dele, com um palito para cima (o que
        /// está dentro de um móvel fechado aparece por cima dele), em todas as construções carregadas. Só nesta máquina.
        /// </summary>
        public static bool MostrarOSaque
        {
            get => _mostrarOSaque;
            set
            {
                _mostrarOSaque = value;
                foreach (var m in Todas)
                {
                    if (value) m.MostrarOSaqueAqui();
                    else m.EsconderOSaque();
                }
            }
        }

        /// <summary>Os itens à mostra no F1, por raridade (de todas as construções carregadas).</summary>
        public static int[] SaqueAMostra()
        {
            var r = new int[5];
            if (!_mostrarOSaque) return r;
            foreach (var m in Todas)
                if (m._saque != null)
                    foreach (var it in m._saque) r[(int)it.raridade]++;
            return r;
        }

        void MostrarOSaqueAqui()
        {
            var mundo = MobiliaNovaDoMundo.Instancia;
            if (_caixasDoSaque != null || mundo == null || Saque.Count == 0) return;
            _malhaDoSaque = MalhaDoSaque(Saque);
            _caixasDoSaque = new GameObject("Saque (F1)");
            _caixasDoSaque.transform.SetParent(transform, false);
            _caixasDoSaque.AddComponent<MeshFilter>().sharedMesh = _malhaDoSaque;
            var r = _caixasDoSaque.AddComponent<MeshRenderer>();
            r.sharedMaterial = mundo.Material;
            r.shadowCastingMode = ShadowCastingMode.Off;
        }

        void EsconderOSaque()
        {
            if (_caixasDoSaque != null) Destroy(_caixasDoSaque);
            if (_malhaDoSaque != null) Destroy(_malhaDoSaque);
            _caixasDoSaque = null;
            _malhaDoSaque = null;
        }

        // as caixas com o material da mobília nova: a textura lisa do plástico tingida pela cor do vértice (a da raridade)
        static Mesh MalhaDoSaque(List<ItemDoSaque> itens)
        {
            var vs = new List<Vector3>();
            var ns = new List<Vector3>();
            var cs = new List<Color>();
            var uv1 = new List<Vector2>();
            var tris = new List<int>();
            float textura = CatalogoDaMobilia.Textura("Plastico");
            foreach (var it in itens)
            {
                var cor = CatalogoDoSaque.Cores[(int)it.raridade].linear;
                var giro = Quaternion.Euler(0f, it.giro, 0f);
                var t = Vector3.Max(it.tamanho, Vector3.one * 0.04f);
                Caixa(it.apoio + Vector3.up * t.y / 2f, giro, t + Vector3.one * 0.01f);
                // o palito: 4 cm de lado, 0,6 m para cima
                Caixa(it.apoio + Vector3.up * (t.y + 0.3f), giro, new Vector3(0.04f, 0.6f, 0.04f));

                void Caixa(Vector3 meio, Quaternion q, Vector3 tam)
                {
                    for (int f = 0; f < 6; f++)
                    {
                        var n = f switch { 0 => Vector3.right, 1 => Vector3.left, 2 => Vector3.up, 3 => Vector3.down, 4 => Vector3.forward, _ => Vector3.back };
                        var a = Mathf.Abs(n.x) > 0.5f ? Vector3.forward : Vector3.right;
                        var b = Vector3.Cross(n, a);
                        int k = vs.Count;
                        for (int c = 0; c < 4; c++)
                        {
                            float sa = c == 0 || c == 3 ? -1f : 1f, sb = c < 2 ? -1f : 1f;
                            vs.Add(meio + q * Vector3.Scale(n + a * sa + b * sb, tam / 2f));
                            ns.Add(q * n);
                            cs.Add(cor);
                            uv1.Add(new Vector2(textura, 0f));
                        }
                        // a frente da face para fora (na Unity, a normal do triângulo a, b, c é (b - a) × (c - a))
                        bool fora = Vector3.Dot(Vector3.Cross(vs[k + 1] - vs[k], vs[k + 2] - vs[k]), q * n) > 0f;
                        if (fora) { tris.Add(k); tris.Add(k + 1); tris.Add(k + 2); tris.Add(k); tris.Add(k + 2); tris.Add(k + 3); }
                        else { tris.Add(k); tris.Add(k + 2); tris.Add(k + 1); tris.Add(k); tris.Add(k + 3); tris.Add(k + 2); }
                    }
                }
            }
            var m = new Mesh { name = "Saque (F1)", indexFormat = vs.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
            m.SetVertices(vs);
            m.SetNormals(ns);
            m.SetColors(cs);
            m.SetUVs(0, new Vector2[vs.Count]);
            m.SetUVs(1, uv1);
            m.SetTriangles(tris, 0);
            m.RecalculateBounds();
            return m;
        }

        /// <summary>
        /// Todas as construções, do olho do jogador local: os de longe e os de perto, com 'pecas' móveis de perto e 'malhas'
        /// malhas de longe no máximo neste quadro.
        /// </summary>
        public static void Revisar(Vector3 olho, float raioDeLonge, float raioDePerto, int pecas, int malhas)
        {
            // a qualidade mudou (UI/Graficos): a sombra dos móveis de perto que já estão postos
            if (_versaoDosGraficos != UI.Graficos.Versao)
            {
                _versaoDosGraficos = UI.Graficos.Versao;
                for (int i = 0; i < Todas.Count; i++)
                    if (Todas[i]._perto != null) SombraDoMovel(Todas[i]._perto, UI.Graficos.SombraDosMoveis);
            }
            for (int i = 0; i < Todas.Count; i++) Todas[i].Revisar(olho, raioDeLonge, raioDePerto, ref pecas, ref malhas);
        }

        static int _versaoDosGraficos = -1;
        static readonly List<MeshRenderer> _renderers = new List<MeshRenderer>();

        // na Média e na Baixa, os móveis de perto não fazem a sombra do sol (os prefabs vêm com ela ligada em tudo)
        static void SombraDoMovel(GameObject go, bool sombra)
        {
            go.GetComponentsInChildren(true, _renderers);
            var modo = sombra ? ShadowCastingMode.On : ShadowCastingMode.Off;
            foreach (var r in _renderers) r.shadowCastingMode = modo;
        }

        void Revisar(Vector3 olho, float raioDeLonge, float raioDePerto, ref int pecas, ref int malhas)
        {
            var q = transform.InverseTransformPoint(olho);
            float dx = Mathf.Max(0f, Mathf.Abs(q.x - _caixa.center.x) - _caixa.extents.x);
            float dz = Mathf.Max(0f, Mathf.Abs(q.z - _caixa.center.z) - _caixa.extents.z);
            float dy = Mathf.Max(0f, Mathf.Abs(q.y - _caixa.center.y) - _caixa.extents.y);
            float fora = Mathf.Sqrt(dx * dx + dz * dz);
            bool longe = fora * fora + dy * dy <= raioDeLonge * raioDeLonge;
            // perto: na construção, no andar dos móveis (um prédio tem um destes por andar); com a folga para sair
            _querPerto = _querPerto ? fora <= raioDePerto + 4f && dy <= 1.0f : fora <= raioDePerto && dy <= 0.5f;

            if (_querPerto && pecas > 0) PorPerto(ref pecas);
            else if (!_querPerto && _perto != null) TirarPerto();
            bool pertoPronto = _perto != null && _proximo >= _pontos.Length;

            if (longe && !pertoPronto && _malha == null && _dados != null && malhas > 0)
            {
                malhas--;
                _malha = _dados.ParaMesh($"{name}: móveis novos");
                MalhaDaMobilia.Devolver(_dados);
                _dados = null;
                var go = new GameObject("Móveis novos (de longe)");
                go.transform.SetParent(transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = _malha;
                _longe = go.AddComponent<MeshRenderer>();
                _longe.sharedMaterial = MobiliaNovaDoMundo.Instancia.Material;
                _longe.shadowCastingMode = ShadowCastingMode.Off;
            }
            if (_longe != null) _longe.enabled = longe && !pertoPronto;
        }

        void PorPerto(ref int pecas)
        {
            var mundo = MobiliaNovaDoMundo.Instancia;
            if (mundo == null) return;
            if (_perto == null)
            {
                _perto = new GameObject("Móveis novos (de perto)");
                _perto.transform.SetParent(transform, false);
                _proximo = 0;
            }
            ulong obra = Obra;
            while (pecas > 0 && _proximo < _pontos.Length)
            {
                int i = _proximo++;
                var p = _pontos[i];
                if (!MobiliaNova.EhNovo(p.tipo))
                {
                    if (_comSaque && AntigoComSaque(p) && PorOSaqueDoAntigo(mundo, p, i, obra)) pecas--;
                    continue;
                }
                _pecas.Clear();
                MobiliaNova.Pecas(p, MobiliaNova.Sorte(p, _semente, i), _pecas);
                foreach (var peca in _pecas)
                {
                    var go = mundo.Novo(peca.modelo, peca.variacao, _perto.transform);
                    if (go == null) continue;
                    go.transform.SetLocalPositionAndRotation(peca.origem, Quaternion.Euler(0f, peca.giro, 0f));
                    go.transform.localScale = Vector3.one * MedidasDaMobilia.Escala;
                    if (!UI.Graficos.SombraDosMoveis) SombraDoMovel(go, false);
                    // o saque, com as partes ainda fechadas (o lugar de cada item é o do móvel fechado)
                    if (_comSaque) mundo.PorOSaque(this, go, peca.modelo, obra, i, peca.indice, _saqueDePerto);
                    pecas--;
                    _dePerto++;
                    QuantosDePerto++;
                    go.GetComponentsInChildren(true, _partesDoMovel);
                    for (int k = 0; k < _partesDoMovel.Count; k++)
                    {
                        var parte = _partesDoMovel[k];
                        parte.chave = MobiliaNova.ChaveDaParte(obra, i, peca.indice, k);
                        mundo.Registrar(parte);
                        _partes.Add(parte);
                    }
                    // a mesa de sinuca se joga (o jogo dela fica no SinucaDoMundo, pela chave)
                    if (peca.modelo == "MesaSinuca") SinucaDoMundo.Ligar(go, MobiliaNova.ChaveDaParte(obra, i, peca.indice, SinucaDoMundo.ParteDaMesa));
                }
            }
        }

        /// <summary>
        /// O saque em cima de um móvel antigo da farmácia ou do hospital (05/11/2026): o móvel já está na malha da obra, então
        /// só um objeto vazio no ponto (com o giro dele), e os itens nele, com a caixa do ponto (sem ela, a colisão do móvel
        /// na obra tapava o item na mira). False se o ponto não tem nenhum item (nada é posto).
        /// </summary>
        bool PorOSaqueDoAntigo(MobiliaNovaDoMundo mundo, PontoDeMovel p, int i, ulong obra)
        {
            bool tem = false;
            foreach (var it in Saque) tem |= it.ponto == i;
            if (!tem) return false;
            var go = new GameObject($"Antigo: {p.tipo}");
            go.transform.SetParent(_perto.transform, false);
            go.transform.SetLocalPositionAndRotation(p.centro + Vector3.up * p.altura, Quaternion.Euler(0f, p.giro, 0f));
            var caixa = new Bounds(Vector3.up * (p.tamanho.y / 2f), p.tamanho);
            mundo.PorOSaque(this, go, null, obra, i, 0, _saqueDePerto, caixa);
            return true;
        }

        void TirarPerto()
        {
            var mundo = MobiliaNovaDoMundo.Instancia;
            if (mundo != null) foreach (var p in _partes) if (p != null) mundo.Esquecer(p);
            _partes.Clear();
            if (mundo != null) foreach (var s in _saqueDePerto) mundo.EsquecerOSaque(s);
            _saqueDePerto.Clear();
            QuantosDePerto -= _dePerto;
            _dePerto = 0;
            if (_perto != null) Destroy(_perto);
            _perto = null;
            _proximo = 0;
        }
    }
}
