using System.Collections.Generic;
using UnityEngine;
using ZombieCityCleaner.Mundo;

namespace ZombieCityCleaner.World
{
    /// <summary>
    /// As árvores do mundo do jogo (12/11/2026, o Plano_Lakewood, "O jogo do mundo sorteado no Lakewood", as partes 2 e 3):
    /// a floresta da semente do mundo sorteado (<see cref="DadosDoMundo.Floresta"/>, desenhada pela
    /// <see cref="FlorestaDoMundo"/>) ou os pinheiros do Lakewood (<see cref="ChaoDeLakewood.Plantas"/>, desenhados pelo
    /// <see cref="LakewoodNaCena"/>). Os dois saem da semente, em blocos de 64 m, e a chave de uma árvore é a mesma
    /// (<see cref="FlorestaDoMundo.Chave"/>: o bloco e a ordem dela no bloco). As cortadas (pelo machado ou pelo castor) ficam
    /// na mesma lista (<see cref="FlorestaDoMundo.Cortada"/>), que vem da rede do mundo (<see cref="MundoDaPartida"/> ou
    /// <see cref="MundoDeLakewood"/>). Para a fauna (o castor, o guaxinim subindo, as copas) e o machado.
    /// </summary>
    public static class ArvoresDoMundo
    {
        // os blocos do Lakewood já feitos (a conta dos pinheiros de um bloco custa ~1 ms): os últimos 256
        static readonly Dictionary<Vector2Int, List<PlantaDaFloresta>> _doLakewood = new Dictionary<Vector2Int, List<PlantaDaFloresta>>();
        static ChaoDeLakewood _chaoDoCache;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Limpar()
        {
            _doLakewood.Clear();
            _chaoDoCache = null;
        }

        static DadosDoMundo Dados => MundoDaPartida.Instancia != null ? MundoDaPartida.Instancia.Dados : null;

        // (16/11/2026, OrigemDeLakewood: tudo aqui fala no mundo do jogo; o Lakewood converte os pontos e as plantas)
        static ChaoDeLakewood Chao => Dados == null && LakewoodNaCena.Instancia != null ? LakewoodNaCena.Instancia.ChaoNoMapa : null;

        /// <summary>O mundo da cena tem árvores (o sorteado com a floresta, ou o Lakewood).</summary>
        public static bool Tem => Dados?.Floresta != null || Chao != null;

        /// <summary>No Lakewood (os pinheiros do chão dele).</summary>
        public static bool NoLakewood => Chao != null;

        /// <summary>O bloco de 64 m do ponto (do mundo).</summary>
        public static Vector2Int BlocoDe(float x, float z)
        {
            var d = Dados;
            if (d != null) return d.BlocoDe(x, z);
            return new Vector2Int(Mathf.FloorToInt((x + OrigemDeLakewood.X) / ChaoDeLakewood.TamanhoDoBloco),
                                  Mathf.FloorToInt((z + OrigemDeLakewood.Z) / ChaoDeLakewood.TamanhoDoBloco));
        }

        /// <summary>
        /// As plantas do bloco, na ordem das chaves (no sorteado, também o mato; no Lakewood, só os pinheiros), com as posições
        /// no mundo.
        /// </summary>
        public static void DoBloco(int bx, int bz, List<PlantaDaFloresta> saida)
        {
            saida.Clear();
            var d = Dados;
            if (d?.Floresta != null)
            {
                d.Floresta.DoBloco(bx, bz, saida);
                return;
            }
            var chao = Chao;
            if (chao == null || bx < 0 || bz < 0) return;
            if (chao != _chaoDoCache)
            {
                _doLakewood.Clear();
                _chaoDoCache = chao;
            }
            var chave = new Vector2Int(bx, bz);
            if (!_doLakewood.TryGetValue(chave, out var lista))
            {
                lista = new List<PlantaDaFloresta>(300);
                chao.Plantas(bx, bz, lista);
                // (as plantas do chão estão no mapa: guardadas no mundo)
                for (int k = 0; k < lista.Count; k++)
                {
                    var pl = lista[k];
                    pl.Posicao = OrigemDeLakewood.NoMundo(pl.Posicao);
                    lista[k] = pl;
                }
                if (_doLakewood.Count >= 256) _doLakewood.Clear();
                _doLakewood[chave] = lista;
            }
            saida.AddRange(lista);
        }

        /// <summary>A planta é uma árvore em pé com tronco (não o mato, o toco, o tronco caído).</summary>
        public static bool EmPe(PlantaDaFloresta pl) =>
            Floresta.EhArvore(pl.Especie) && !Floresta.Deitada(pl.Especie) && Floresta.RaioDoTronco[(int)pl.Especie] > 0f;

        /// <summary>O raio do tronco da árvore (m).</summary>
        public static float RaioDoTronco(PlantaDaFloresta pl) => Floresta.RaioDoTronco[(int)pl.Especie] * pl.Escala;

        public static bool Cortada(long chave) => FlorestaDoMundo.Cortada(chave);

        /// <summary>A planta da chave (falso se o bloco não tem essa ordem).</summary>
        public static bool Planta(long chave, out PlantaDaFloresta planta)
        {
            planta = default;
            FlorestaDoMundo.Abrir(chave, out int bx, out int bz, out int i);
            var l = _uma;
            DoBloco(bx, bz, l);
            if (i < 0 || i >= l.Count) return false;
            planta = l[i];
            return true;
        }

        static readonly List<PlantaDaFloresta> _uma = new List<PlantaDaFloresta>(300);
        static readonly List<PlantaDaFloresta> _perto = new List<PlantaDaFloresta>(300);

        /// <summary>
        /// A árvore em pé (e não cortada) mais perto do ponto, a até 'raio' m, que passa no filtro (o pé, ou null): a chave,
        /// o pé e o raio do tronco.
        /// </summary>
        public static bool MaisPerto(Vector3 p, float raio, out long chave, out Vector3 pe, out float raioDoTronco, System.Func<Vector3, bool> filtro = null)
        {
            chave = 0;
            pe = default;
            raioDoTronco = 0f;
            if (!Tem) return false;
            var a = BlocoDe(p.x - raio, p.z - raio);
            var b = BlocoDe(p.x + raio, p.z + raio);
            float melhor = raio * raio;
            bool achou = false;
            for (int bz = a.y; bz <= b.y; bz++)
                for (int bx = a.x; bx <= b.x; bx++)
                {
                    DoBloco(bx, bz, _perto);
                    for (int i = 0; i < _perto.Count; i++)
                    {
                        var pl = _perto[i];
                        if (!EmPe(pl)) continue;
                        float dd = (new Vector2(pl.Posicao.x, pl.Posicao.z) - new Vector2(p.x, p.z)).sqrMagnitude;
                        if (dd >= melhor) continue;
                        long k = FlorestaDoMundo.Chave(bx, bz, i);
                        if (Cortada(k) || (filtro != null && !filtro(pl.Posicao))) continue;
                        melhor = dd;
                        chave = k;
                        pe = pl.Posicao;
                        raioDoTronco = RaioDoTronco(pl);
                        achou = true;
                    }
                }
            return achou;
        }

        /// <summary>
        /// O ponto está embaixo das copas (os bichos enxergam menos ali). No sorteado, pelas copas dos blocos montados; no
        /// Lakewood, na mata fechada (fora das clareiras, das cidades e da água).
        /// </summary>
        public static bool SobAsCopas(Vector3 p)
        {
            if (Dados != null) return FlorestaDoMundo.Instancia != null && FlorestaDoMundo.Instancia.SobAsCopas(p);
            var chao = Chao;
            if (chao == null) return false;
            float x = p.x + OrigemDeLakewood.X, z = p.z + OrigemDeLakewood.Z;   // (do mundo para o mapa)
            if (chao.Mapa.CoberturaEm(x, z) != Cobertura.Mata || chao.Clareira(x, z) > 0.35f) return false;
            return chao.LoteEm(x, z, 3f) == null && !chao.Agua(x, z, out _);
        }

        /// <summary>
        /// Só no anfitrião: o castor roeu a árvore até cair (ela entra nas cortadas do mundo, cai como a do machado, sem as
        /// toras). Falso se ela já caiu ou não há mundo.
        /// </summary>
        public static bool DerrubarPeloCastor(long chave, Vector3 deOnde)
        {
            var m = MundoDaPartida.Instancia;
            if (m != null && m.Dados != null) return m.DerrubarPeloCastor(chave, deOnde);
            var l = MundoDeLakewood.Instancia;
            return l != null && l.DerrubarPeloCastor(chave, deOnde);
        }
    }
}
