using System.Collections.Generic;
using UnityEngine;
using ZombieCityCleaner.Combat;
using ZombieCityCleaner.Mundo;

namespace ZombieCityCleaner.World
{
    /// <summary>
    /// O machado corta as árvores (06/11/2026, a pedido: "colocar um sistema para o machado cortar árvore"). A floresta sai
    /// da semente e nada dela vai pela rede (<see cref="Floresta"/>); a árvore cortada é a exceção: a chave dela (o bloco e
    /// a ordem no <c>Floresta.DoBloco</c>) vai na lista do mundo (<see cref="MundoDaPartida"/>.Arvores.cs, que o save guarda).
    /// Aqui, em cada máquina: a cortada vira toco (a espécie troca no bloco montado, e a colisão dos troncos refaz), e, se
    /// for perto, ela cai (<see cref="ArvoreCaindo"/>). O dono acha a árvore do golpe pelo tronco (<see cref="TroncoEm"/>).
    /// </summary>
    public partial class FlorestaDoMundo
    {
        // as cortadas: a lista inteira copiada a cada mudança (a colisão e o desenho leem aqui)
        static HashSet<long> _cortadas = new HashSet<long>();
        // a espécie de antes (para a árvore cair com a cara dela)
        readonly Dictionary<long, (Especie e, Vector3 pos, float giro, float escala)> _antes = new Dictionary<long, (Especie, Vector3, float, float)>();

        /// <summary>A chave de uma planta: o bloco (x, z) e a ordem dela no bloco.</summary>
        public static long Chave(int bx, int bz, int i) => ((long)(bx & 0xFFFFF) << 40) | ((long)(bz & 0xFFFFF) << 20) | (uint)(i & 0xFFFFF);

        public static void Abrir(long chave, out int bx, out int bz, out int i)
        {
            bx = (int)((chave >> 40) & 0xFFFFF);
            bz = (int)((chave >> 20) & 0xFFFFF);
            i = (int)(chave & 0xFFFFF);
        }

        public static bool Cortada(long chave) => _cortadas.Contains(chave);

        /// <summary>As cortadas do mundo (a lista da rede mudou, ou chegou): os blocos montados viram toco onde precisa.</summary>
        public static void DefinirCortadas(IEnumerable<long> chaves)
        {
            var novas = new HashSet<long>(chaves);
            _cortadas = novas;
            var f = Instancia;
            if (f == null) return;
            foreach (var b in f._blocos.Values) f.AplicarCortadas(b);
        }

        // as árvores do bloco que estão na lista viram toco (e a colisão refaz, se mudou)
        void AplicarCortadas(Bloco b)
        {
            var p = b?.Plantas;
            if (p == null || _cortadas.Count == 0) return;
            bool mudou = false;
            for (int i = 0; i < p.Especies.Length; i++)
            {
                var e = (Especie)p.Especies[i];
                if (!Floresta.EhArvore(e)) continue;
                long k = Chave(b.Chave.x, b.Chave.y, i);
                if (!_cortadas.Contains(k)) continue;
                _antes[k] = (e, p.Posicoes[i], p.Giros[i], p.Escalas[i]);
                p.Especies[i] = (byte)Especie.TocoA;
                mudou = true;
            }
            if (!mudou || b.Colisao == null) return;
            Destroy(b.Colisao);
            b.Colisao = null;
            b.ProximoTronco = 0;
            b.ColisaoFeita = false;
        }

        /// <summary>
        /// No dono, o golpe do machado num tronco: a árvore em pé desse tronco (a chave, o pé e a espécie). Falso se não for
        /// uma árvore da floresta (a parede, o toco, o tronco caído).
        /// </summary>
        public bool TroncoEm(Collider c, Vector3 ponto, out long chave, out Vector3 pe)
        {
            chave = 0;
            pe = default;
            if (c == null || _raiz == null || !c.transform.IsChildOf(_raiz)) return false;
            foreach (var b in _blocos.Values)
            {
                if (b.Colisao == null || b.Plantas == null || c.gameObject != b.Colisao) continue;
                var p = b.Plantas;
                float melhor = float.MaxValue;
                int qual = -1;
                for (int i = 0; i < p.Especies.Length; i++)
                {
                    var e = (Especie)p.Especies[i];
                    if (!Floresta.EhArvore(e) || Floresta.Deitada(e) || Floresta.RaioDoTronco[(int)e] <= 0f) continue;
                    var q = p.Posicoes[i];
                    float d = new Vector2(q.x - ponto.x, q.z - ponto.z).magnitude - Floresta.RaioDoTronco[(int)e] * p.Escalas[i];
                    if (d < melhor)
                    {
                        melhor = d;
                        qual = i;
                    }
                }
                if (qual < 0 || melhor > 0.6f) return false;
                chave = Chave(b.Chave.x, b.Chave.y, qual);
                pe = p.Posicoes[qual];
                return !_cortadas.Contains(chave);
            }
            return false;
        }

        /// <summary>A árvore cortada cai (em cada máquina, se o bloco estiver montado e perto), para longe de quem cortou.</summary>
        public void Tombar(long chave, Vector3 deQuem)
        {
            if (!_antes.TryGetValue(chave, out var a)) return;
            var cam = Camera.main;
            if (cam != null && (cam.transform.position - a.pos).sqrMagnitude > 90f * 90f) return;
            int k = (int)a.e * 3;
            if (malhas == null || k >= malhas.Length || malhas[k] == null) return;
            var go = new GameObject("Árvore caindo");
            go.transform.SetPositionAndRotation(a.pos, Quaternion.Euler(0f, a.giro, 0f));
            go.transform.localScale = Vector3.one * a.escala;
            go.AddComponent<MeshFilter>().sharedMesh = malhas[k];
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            Vector3 longe = a.pos - deQuem;
            longe.y = 0f;
            if (longe.sqrMagnitude < 0.01f) longe = Vector3.forward;
            go.AddComponent<ArvoreCaindo>().Comecar(longe.normalized);
            Efeitos.Som(Efeitos.SocoParede, a.pos + Vector3.up, 1f, true, 45f);
        }
    }

    /// <summary>A árvore cortada caindo (06/11/2026): gira em volta do pé, bate no chão (o som e a poeira) e afunda.</summary>
    public class ArvoreCaindo : MonoBehaviour
    {
        Vector3 _eixo, _pe;
        Quaternion _inicio;
        float _tempo;
        bool _bateu;

        public void Comecar(Vector3 para)
        {
            _pe = transform.position;
            _inicio = transform.rotation;
            _eixo = Vector3.Cross(Vector3.up, para).normalized;
        }

        void Update()
        {
            _tempo += Time.deltaTime;
            // cai acelerando, como uma árvore de verdade (devagar no começo)
            float k = Mathf.Clamp01(_tempo / 2.2f);
            float angulo = 88f * k * k;
            transform.SetPositionAndRotation(_pe, Quaternion.AngleAxis(angulo, _eixo) * _inicio);
            if (!_bateu && k >= 1f)
            {
                _bateu = true;
                var topo = _pe + Vector3.Cross(_eixo, Vector3.up) * -6f;
                Efeitos.Som(Efeitos.SocoParede, topo, 1f, true, 50f);
                Efeitos.Impacto(topo, Vector3.up);
            }
            if (_tempo > 4.5f)
            {
                transform.position = _pe + Vector3.down * ((_tempo - 4.5f) * 1.5f);
                if (_tempo > 7f) Destroy(gameObject);
            }
        }
    }
}
