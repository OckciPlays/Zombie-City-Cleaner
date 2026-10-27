using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using ZombieCityCleaner.Mundo;

namespace ZombieCityCleaner.World
{
    /// <summary>
    /// Os pontos dos móveis de uma construção na cena (Parte M6): de uma casa, de uma loja, de um andar de prédio ou da loja
    /// do posto, nas coordenadas do objeto dela (<see cref="MoveisDaObra"/>). Por enquanto são só dados: os móveis e o
    /// saque entram depois, nos pontos. Com o "Mostrar os pontos dos móveis" do F1 (<see cref="Mostrar"/>, só nesta
    /// máquina), cada ponto vira uma caixa do tamanho do móvel, amarela onde vai ter saque.
    /// </summary>
    public class MoveisDaConstrucao : MonoBehaviour
    {
        public PontoDeMovel[] pontos;

        static readonly List<MoveisDaConstrucao> Todos = new List<MoveisDaConstrucao>();
        static bool _mostrar;

        GameObject _caixas;
        Mesh _malha;

        /// <summary>As caixas dos pontos aparecem em todas as construções (as que entram depois também).</summary>
        public static bool Mostrar
        {
            get => _mostrar;
            set
            {
                _mostrar = value;
                foreach (var m in Todos) m.Atualizar();
            }
        }

        /// <summary>As construções com os pontos na cena e quantos pontos elas têm (o F1 mostra).</summary>
        public static int Construcoes => Todos.Count;

        public static int Pontos
        {
            get
            {
                int n = 0;
                foreach (var m in Todos) n += m.pontos?.Length ?? 0;
                return n;
            }
        }

        void OnEnable()
        {
            Todos.Add(this);
            Atualizar();
        }

        void OnDisable()
        {
            Todos.Remove(this);
            Esconder();
        }

        void Atualizar()
        {
            if (_mostrar && _caixas == null) Montar();
            else if (!_mostrar) Esconder();
        }

        void Montar()
        {
            if (pontos == null || pontos.Length == 0 || TerrenoDoMundo.Instancia == null) return;
            var obra = MalhaDeObra.Emprestar();
            foreach (var p in pontos)
            {
                var tam = p.tamanho * 0.97f;
                obra.Caixa(Matrix4x4.TRS(p.centro + Vector3.up * (tam.y / 2 + 0.01f), Quaternion.Euler(0f, p.giro, 0f), Vector3.one), tam,
                           p.saque ? "PlacaAmarela" : "ConcretoClaro");
            }
            _malha = obra.ParaMesh($"{name}: pontos dos móveis");
            MalhaDeObra.Devolver(obra);
            _caixas = new GameObject("Pontos dos móveis");
            _caixas.transform.SetParent(transform, false);
            _caixas.AddComponent<MeshFilter>().sharedMesh = _malha;
            var r = _caixas.AddComponent<MeshRenderer>();
            r.sharedMaterial = TerrenoDoMundo.Instancia.materialDaCidade;
            r.shadowCastingMode = ShadowCastingMode.Off;
        }

        void Esconder()
        {
            if (_caixas != null) Destroy(_caixas);
            if (_malha != null) Destroy(_malha);
            _caixas = null;
            _malha = null;
        }

        /// <summary>Põe os pontos no objeto da construção (nada, sem pontos).</summary>
        public static void Por(GameObject go, List<PontoDeMovel> pontos)
        {
            if (pontos == null || pontos.Count == 0) return;
            var m = go.AddComponent<MoveisDaConstrucao>();
            m.pontos = pontos.ToArray();
            m.Atualizar();
        }
    }
}
