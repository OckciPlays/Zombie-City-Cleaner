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
    ///    perto saem e os de longe voltam.
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
        bool _comChave;
        Bounds _caixa;                 // a dos pontos, nas coordenadas da obra
        Mesh _malha;
        MeshRenderer _longe;
        GameObject _perto;
        int _proximo;                  // o próximo ponto a pôr de perto
        bool _querPerto;
        int _dePerto;
        readonly List<ParteDoMovel> _partes = new List<ParteDoMovel>();

        /// <summary>
        /// Põe os móveis novos no objeto da construção ('andar': o do prédio, ou -100 numa casa ou loja). Fica com os de
        /// longe (devolvidos ao estoque quando viram Mesh ou quando a construção sai). Nada, sem móvel novo.
        /// </summary>
        public static void Por(GameObject go, List<PontoDeMovel> pontos, MalhaDaMobilia dados, int semente, int andar)
        {
            bool tem = false;
            if (pontos != null) foreach (var p in pontos) tem |= MobiliaNova.EhNovo(p.tipo);
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
            bool primeiro = true;
            foreach (var p in m._pontos)
            {
                if (!MobiliaNova.EhNovo(p.tipo)) continue;
                var pe = p.Pe;
                var b = new Bounds(new Vector3(pe.center.x, p.centro.y + p.altura + p.tamanho.y / 2, pe.center.y), new Vector3(pe.width, p.tamanho.y, pe.height));
                if (primeiro) m._caixa = b;
                else m._caixa.Encapsulate(b);
                primeiro = false;
            }
        }

        void OnEnable() => Todas.Add(this);

        void OnDisable()
        {
            Todas.Remove(this);
            TirarPerto();
        }

        void OnDestroy()
        {
            MalhaDaMobilia.Devolver(_dados);
            _dados = null;
            if (_malha != null) Destroy(_malha);
            _malha = null;
        }

        /// <summary>
        /// Todas as construções, do olho do jogador local: os de longe e os de perto, com 'pecas' móveis de perto e 'malhas'
        /// malhas de longe no máximo neste quadro.
        /// </summary>
        public static void Revisar(Vector3 olho, float raioDeLonge, float raioDePerto, int pecas, int malhas)
        {
            for (int i = 0; i < Todas.Count; i++) Todas[i].Revisar(olho, raioDeLonge, raioDePerto, ref pecas, ref malhas);
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
            if (!_comChave)
            {
                _obra = MobiliaNova.ChaveDaObra(transform.position, _andar);
                _comChave = true;
            }
            while (pecas > 0 && _proximo < _pontos.Length)
            {
                int i = _proximo++;
                var p = _pontos[i];
                if (!MobiliaNova.EhNovo(p.tipo)) continue;
                _pecas.Clear();
                MobiliaNova.Pecas(p, MobiliaNova.Sorte(p, _semente, i), _pecas);
                foreach (var peca in _pecas)
                {
                    var go = mundo.Novo(peca.modelo, peca.variacao, _perto.transform);
                    if (go == null) continue;
                    go.transform.SetLocalPositionAndRotation(peca.origem, Quaternion.Euler(0f, peca.giro, 0f));
                    go.transform.localScale = Vector3.one * MedidasDaMobilia.Escala;
                    pecas--;
                    _dePerto++;
                    QuantosDePerto++;
                    go.GetComponentsInChildren(true, _partesDoMovel);
                    for (int k = 0; k < _partesDoMovel.Count; k++)
                    {
                        var parte = _partesDoMovel[k];
                        parte.chave = MobiliaNova.ChaveDaParte(_obra, i, peca.indice, k);
                        mundo.Registrar(parte);
                        _partes.Add(parte);
                    }
                }
            }
        }

        void TirarPerto()
        {
            var mundo = MobiliaNovaDoMundo.Instancia;
            if (mundo != null) foreach (var p in _partes) if (p != null) mundo.Esquecer(p);
            _partes.Clear();
            QuantosDePerto -= _dePerto;
            _dePerto = 0;
            if (_perto != null) Destroy(_perto);
            _perto = null;
            _proximo = 0;
        }
    }
}
