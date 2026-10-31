using UnityEngine;

namespace ZombieCityCleaner.Mundo
{
    /// <summary>
    /// Um móvel do mostruário da mobília nova (a cena das variações): troca a variação de cor e de material dele (o C do
    /// AndarNoExemplo, mirando nele), pintando de novo as malhas do corpo, das partes que abrem e do de longe
    /// (<see cref="CatalogoDaMobilia.Pintar"/>). As malhas pintadas não são salvas e são apagadas na troca seguinte.
    /// </summary>
    public class MovelDoMostruario : MonoBehaviour
    {
        /// <summary>O modelo (o nome do prefab, o do catálogo).</summary>
        public string modelo;
        public int variacao;
        [SerializeField] MeshFilter[] _filtros;
        [SerializeField] Mesh[] _originais;

        public CatalogoDaMobilia.Tipo Tipo => CatalogoDaMobilia.DoModelo(modelo);
        public int Quantas => Tipo?.variacoes?.Length ?? 1;
        public string NomeDaVariacao => Tipo?.variacoes != null && variacao < Tipo.variacoes.Length ? Tipo.variacoes[variacao].nome : "";

        /// <summary>Guarda as malhas do modelo (chame logo depois de pôr o prefab na cena).</summary>
        public void Guardar(string nome)
        {
            modelo = nome;
            _filtros = GetComponentsInChildren<MeshFilter>(true);
            _originais = new Mesh[_filtros.Length];
            for (int i = 0; i < _filtros.Length; i++) _originais[i] = _filtros[i].sharedMesh;
        }

        public void Mostrar(int v)
        {
            var t = Tipo;
            if (t == null || _filtros == null) return;
            variacao = ((v % Quantas) + Quantas) % Quantas;
            for (int i = 0; i < _filtros.Length; i++)
            {
                if (_filtros[i] == null) continue;
                var velha = _filtros[i].sharedMesh;
                _filtros[i].sharedMesh = CatalogoDaMobilia.Pintar(_originais[i], t.variacoes[variacao]);
                // as pintadas são só daqui (as do modelo são assets)
                if (velha != null && velha != _originais[i] && velha != _filtros[i].sharedMesh)
                {
                    if (Application.isPlaying) Destroy(velha);
                    else DestroyImmediate(velha);
                }
                if (_filtros[i].sharedMesh != _originais[i]) _filtros[i].sharedMesh.hideFlags = HideFlags.DontSave;
            }
        }

        public void Proxima() => Mostrar(variacao + 1);
    }
}
