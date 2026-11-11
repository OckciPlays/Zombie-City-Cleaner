using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using ZombieCityCleaner.Player;
using ZombieCityCleaner.Sobreviventes;
using ZombieCityCleaner.Zombies;
using A = ZombieCityCleaner.Player.ArmadilhasDoJogador;

namespace ZombieCityCleaner.Loot
{
    /// <summary>
    /// A cerca de fogo (11/11/2026, a etapa 5a; os números no Player/ArmadilhasDoJogador.cs): o galão com o pavio e um rastro
    /// de combustível de 4 m para +Z (do vazio Pavio; uma parede no caminho o corta, e ele acompanha o chão). O valor da
    /// construção é 0 apagada e, acesa, o fim do fogo no relógio do servidor (como a fogueira).
    ///  - Apagada, o E recolhe; o E com a tocha acesa ou o lampião aceso na mão acende pelo pavio; o fogo do molotov ou de
    ///    outra cerca acesa encostando no rastro também acende.
    ///  - Acesa, o fogo corre do pavio até a ponta (1 s) e queima 90 s (o Armadilheiro aumenta). Os zumbis não atravessam:
    ///    cada ponto da linha (5) é um fogo que segura, como a área do molotov, e a Muralha de fogo do dono vale nela (mais
    ///    longe, com pânico). Quem atravessa queima como no molotov (os zumbis pegam fogo; os moradores e os jogadores, um
    ///    golpe por segundo). Na chuva, fora de um teto, ela queima mais depressa (1 + 2 × a chuva). Depois, some (se gasta).
    ///  - No save, a acesa não vai (o combustível queimou: ela volta gasta); a apagada vai.
    /// </summary>
    public partial class ConstrucoesNoMundo
    {
        const int AmostrasDaLinha = 17;   // os pontos do rastro (a cada 25 cm)
        readonly Dictionary<ushort, Vector3[]> _linhas = new Dictionary<ushort, Vector3[]>();
        float _proximoTiqueDaCerca, _proximoSegundoDaCerca;

        /// <summary>Os pontos do rastro da cerca (do pavio até a ponta, no chão), em cada máquina a mesma conta.</summary>
        Vector3[] LinhaDaCerca(Construcao c)
        {
            if (_linhas.TryGetValue(c.id, out var l)) return l;
            var rot = Quaternion.Euler(0f, c.giro, 0f);
            Vector3 p0 = c.pos + rot * ItensDasArmadilhas.PavioDaCerca;
            Vector3 dir = rot * Vector3.forward;
            float comprimento = A.ComprimentoDaCerca;
            // a parede (ou um móvel, ou as estacas) corta o rastro: de ponto em ponto pelo chão, a 0,25 m de altura (um raio reto
            // batia na própria subida da rua e cortava o rastro na ladeira)
            float passo = comprimento / (AmostrasDaLinha - 1);
            Vector3 antes = new Vector3(p0.x, c.pos.y, p0.z);
            for (int i = 1; i < AmostrasDaLinha; i++)
            {
                Vector3 p = p0 + dir * (passo * i);
                float yi = antes.y;
                if (Physics.Raycast(new Vector3(p.x, antes.y + 0.8f, p.z), Vector3.down, out var chao0, 2.5f, MascaraDoChao, QueryTriggerInteraction.Ignore))
                    yi = chao0.point.y;
                var de = antes + Vector3.up * 0.25f;
                var ate = new Vector3(p.x, yi + 0.25f, p.z);
                if (Physics.Linecast(de, ate, out var h, MascaraDoChao, QueryTriggerInteraction.Ignore))
                {
                    comprimento = Mathf.Max(0.5f, passo * (i - 1) + Vector3.Distance(de, h.point) - 0.1f);
                    break;
                }
                antes = new Vector3(p.x, yi, p.z);
            }
            l = new Vector3[AmostrasDaLinha];
            for (int i = 0; i < AmostrasDaLinha; i++)
            {
                Vector3 p = p0 + dir * (comprimento * i / (AmostrasDaLinha - 1));
                float y = i == 0 ? c.pos.y : l[i - 1].y;
                if (Physics.Raycast(new Vector3(p.x, y + 0.8f, p.z), Vector3.down, out var chao, 2.5f, MascaraDoChao, QueryTriggerInteraction.Ignore))
                    y = chao.point.y;
                l[i] = new Vector3(p.x, y, p.z);
            }
            _linhas[c.id] = l;
            return l;
        }

        // até que ponto da linha o fogo já correu (o índice; o fogo leva CorridaDoFogo segundos do pavio até a ponta)
        int PontoAceso(Construcao c)
        {
            float inicio = c.valor - A.SegundosDaCercaDe(c.nivel);
            float p = Mathf.Clamp01((Agora - inicio) / A.CorridaDoFogo);
            return Mathf.Clamp(Mathf.RoundToInt(p * (AmostrasDaLinha - 1)), 1, AmostrasDaLinha - 1);
        }

        bool CercaAcesa(Construcao c) => c.tipo == Item.CercaFogo && c.valor > 0f && c.valor > Agora;

        // ------------------------------------------------------------------ no anfitrião

        void AcenderACerca(int k, Construcao c)
        {
            c.valor = Agora + A.SegundosDaCercaDe(c.nivel);
            _lista[k] = c;
            var linha = LinhaDaCerca(c);
            ArmadilhaRpc(c.id, EfeitoAcendeu, linha[0] + Vector3.up * 0.2f);
        }

        /// <summary>Só no anfitrião (o teste): acende a cerca (como o E com a tocha acesa).</summary>
        public bool AcenderCercaDeTeste(ushort id)
        {
            if (!IsServer) return false;
            int k = Indice(id);
            if (k < 0 || _lista[k].tipo != Item.CercaFogo || _lista[k].valor > 0f) return false;
            AcenderACerca(k, _lista[k]);
            return true;
        }

        // a cada quarto de segundo: o fogo que encosta acende as apagadas; as acesas queimam quem está nelas, a chuva as
        // gasta mais depressa, e a que acabou some
        void CercasNoServidor(float agora)
        {
            if (agora < _proximoTiqueDaCerca) return;
            const float passo = 0.25f;
            _proximoTiqueDaCerca = agora + passo;
            bool porSegundo = agora >= _proximoSegundoDaCerca;
            if (porSegundo) _proximoSegundoDaCerca = agora + 1f;
            var zm = ZombieManager.Instancia;
            for (int k = _lista.Count - 1; k >= 0; k--)
            {
                var c = _lista[k];
                if (c.tipo != Item.CercaFogo) continue;
                if (c.valor <= 0f)
                {
                    if (FogoEncostaNa(c)) AcenderACerca(k, c);
                    continue;
                }
                if (Agora >= c.valor)
                {
                    _lista.RemoveAt(k);   // queimou: se gastou
                    continue;
                }
                var linha = LinhaDaCerca(c);
                int ate = PontoAceso(c);
                var dono = DonoNaPartida(c.dono);
                float fator = A.FatorDeDano(c.nivel);
                zm?.FogoNaLinha(linha, ate, A.LarguraDaQueima, passo, FogoDoJogador.DanoNosZumbis * fator, dono,
                                dono != null ? FogoDoJogador.SegundosQueimandoDe(dono) : FogoDoJogador.SegundosQueimando);
                if (!porSegundo) continue;
                // os moradores e os jogadores na linha: um golpe por segundo (o morador sai andando sozinho)
                foreach (var s in Sobrevivente.Todos)
                    if (s != null && s.AlvoValido && NaLinha(linha, ate, s.transform.position, A.LarguraDaQueima, out var ponto))
                        s.LevarGolpe(FogoDoJogador.DanoNosMoradores * fator, ponto);
                foreach (var j in JogadorRede.Todos)
                    if (j != null && j.IsSpawned && j.Vida != null && NaLinha(linha, ate, j.transform.position, A.LarguraDaQueima, out var ponto))
                        j.Vida.Dano(FogoDoJogador.DanoNosJogadores, ponto, "Queimou-se.");
                // a chuva, fora de um teto: ela queima mais depressa (como a área do molotov)
                if (ChoveNoFogo && !World.Tempo.SobTeto(c.pos))
                {
                    c.valor -= FogoDoJogador.ChuvaNoMolotov * World.Tempo.Chuva;
                    _lista[k] = c;
                }
            }
        }

        // o fogo do molotov ou de outra cerca acesa encosta no rastro (ou no galão)?
        bool FogoEncostaNa(Construcao c)
        {
            var linha = LinhaDaCerca(c);
            int fim = AmostrasDaLinha - 1;
            foreach (var a in _areas)
                if (NaLinha(linha, fim, a.centro, a.raio + 0.15f, out _) || (a.centro - c.pos).sqrMagnitude < (a.raio + 0.3f) * (a.raio + 0.3f)) return true;
            foreach (var o in _lista)
            {
                if (o.id == c.id || !CercaAcesa(o)) continue;
                var outra = LinhaDaCerca(o);
                int ate = PontoAceso(o);
                foreach (var p in linha)
                    if (NaLinha(outra, ate, p, 0.5f, out _)) return true;
            }
            return false;
        }

        /// <summary>
        /// Só no anfitrião: os pontos de fogo das cercas acesas que seguram os zumbis (a chave de cada um, o ponto e o dono:
        /// a Muralha de fogo dele), só até onde o fogo já correu.
        /// </summary>
        internal void PontosDeFogoDasCercas(List<(int chave, Vector3 pos, ulong dono)> saida)
        {
            foreach (var c in _lista)
            {
                if (!CercaAcesa(c)) continue;
                var linha = LinhaDaCerca(c);
                int ate = PontoAceso(c);
                for (int k = 0; k < A.PontosQueSeguram; k++)
                {
                    int i = Mathf.RoundToInt(k * (AmostrasDaLinha - 1) / (float)(A.PontosQueSeguram - 1));
                    if (i > ate) break;
                    saida.Add((c.id * 8 + k, linha[i], c.dono));
                }
            }
        }

        /// <summary>O ponto está a menos de <paramref name="largura"/> da linha (no plano, até o índice), na altura dela? O ponto mais perto.</summary>
        internal static bool NaLinha(Vector3[] linha, int ate, Vector3 p, float largura, out Vector3 perto)
        {
            perto = default;
            float melhor = float.MaxValue;
            for (int i = 0; i < ate && i + 1 < linha.Length; i++)
            {
                Vector3 a = linha[i], b = linha[i + 1];
                Vector2 ab = new Vector2(b.x - a.x, b.z - a.z), ap = new Vector2(p.x - a.x, p.z - a.z);
                float t = ab.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector2.Dot(ap, ab) / ab.sqrMagnitude) : 0f;
                Vector3 q = Vector3.Lerp(a, b, t);
                float d = new Vector2(p.x - q.x, p.z - q.z).sqrMagnitude;
                if (d < melhor)
                {
                    melhor = d;
                    perto = q;
                }
            }
            float dy = p.y - perto.y;
            return melhor < largura * largura && dy > -1f && dy < 2f;
        }

        // ------------------------------------------------------------------ o que cada cópia vê

        // o rastro de combustível: uma faixa escura no chão, do pavio até a ponta
        void MontarORastro(GameObject go, Construcao c)
        {
            var d = ItensDasArmadilhas.Dados;
            if (d == null || d.materialDoRastro == null) return;
            var linha = LinhaDaCerca(c);
            var rastro = new GameObject("Rastro") { layer = go.layer };
            rastro.transform.SetParent(go.transform, false);
            var lado = Quaternion.Euler(0f, c.giro, 0f) * Vector3.right * 0.13f;
            var malha = FaixaNoChao(linha, lado, go.transform);
            rastro.AddComponent<MeshFilter>().sharedMesh = malha;
            var r = rastro.AddComponent<MeshRenderer>();
            r.sharedMaterial = d.materialDoRastro;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = true;
            rastro.AddComponent<MalhaQueSome>().malha = malha;
        }

        // uma faixa pelos pontos (no espaço do pai), um pouco acima do chão
        static Mesh FaixaNoChao(Vector3[] pontos, Vector3 lado, Transform pai)
        {
            int n = pontos.Length;
            var v = new Vector3[n * 2];
            var uv = new Vector2[n * 2];
            var tri = new int[(n - 1) * 6];
            for (int i = 0; i < n; i++)
            {
                // a faixa afina na ponta (o combustível acabando)
                float largura = i == n - 1 ? 0.45f : i == 0 ? 0.7f : 1f;
                var p = pontos[i] + Vector3.up * 0.018f;
                v[i * 2] = pai != null ? pai.InverseTransformPoint(p - lado * largura) : p - lado * largura;
                v[i * 2 + 1] = pai != null ? pai.InverseTransformPoint(p + lado * largura) : p + lado * largura;
                uv[i * 2] = new Vector2(0f, i / (float)(n - 1));
                uv[i * 2 + 1] = new Vector2(1f, i / (float)(n - 1));
            }
            for (int i = 0; i < n - 1; i++)
            {
                int a = i * 2, t = i * 6;
                tri[t] = a; tri[t + 1] = a + 2; tri[t + 2] = a + 1;
                tri[t + 3] = a + 1; tri[t + 4] = a + 2; tri[t + 5] = a + 3;
            }
            var m = new Mesh { name = "Rastro da cerca", vertices = v, uv = uv, triangles = tri };
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        /// <summary>
        /// A sombra de montar da cerca de fogo (o MontadorDeConstrucao): o rastro de 4 m reto, para frente, para ver onde o
        /// fogo vai passar.
        /// </summary>
        public static void ExtrasDoFantasma(Item tipo, Transform sombra)
        {
            var d = ItensDasArmadilhas.Dados;
            if (tipo != Item.CercaFogo || d == null || d.materialDoRastro == null || sombra == null) return;
            var linha = new Vector3[AmostrasDaLinha];
            for (int i = 0; i < AmostrasDaLinha; i++)
                linha[i] = d.pavioDaCerca + Vector3.forward * (A.ComprimentoDaCerca * i / (AmostrasDaLinha - 1));
            var go = new GameObject("Rastro");
            go.transform.SetParent(sombra, false);
            var malha = FaixaNoChao(linha, Vector3.right * 0.13f, null);
            go.AddComponent<MeshFilter>().sharedMesh = malha;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = d.materialDoRastro;
            r.shadowCastingMode = ShadowCastingMode.Off;
            go.AddComponent<MalhaQueSome>().malha = malha;
        }

        // a cada quadro, em cada cópia: a linha de fogo das cercas acesas (as chamas acendem do pavio até a ponta)
        void CercasAgora()
        {
            foreach (var c in _lista)
            {
                if (c.tipo != Item.CercaFogo || !_objetos.TryGetValue(c.id, out var o) || o == null) continue;
                var fogo = o.GetComponentInChildren<LinhaDeFogo>(true);
                bool acesa = CercaAcesa(c);
                if (!acesa)
                {
                    if (fogo != null && fogo.gameObject.activeSelf) fogo.gameObject.SetActive(false);
                    continue;
                }
                if (fogo == null) fogo = MontarALinhaDeFogo(o, c);
                if (!fogo.gameObject.activeSelf) fogo.gameObject.SetActive(true);
                int ate = PontoAceso(c);
                for (int i = 0; i < fogo.chamas.Length; i++)
                {
                    bool viva = fogo.indices[i] <= ate;
                    if (fogo.chamas[i].activeSelf != viva) fogo.chamas[i].SetActive(viva);
                }
            }
        }

        // as chamas ao longo do rastro (a cada 50 cm; duas com a luz)
        LinhaDeFogo MontarALinhaDeFogo(GameObject o, Construcao c)
        {
            var linha = LinhaDaCerca(c);
            var go = new GameObject("Linha de fogo") { layer = o.layer };
            go.transform.SetParent(o.transform, false);
            var lf = go.AddComponent<LinhaDeFogo>();
            var chamas = new List<GameObject>();
            var indices = new List<int>();
            for (int i = 0; i < AmostrasDaLinha; i += 2)
            {
                bool comLuz = i == 4 || i == 12;
                var chama = Fogo(go.transform, go.transform.InverseTransformPoint(linha[i]) + Vector3.up * 0.03f, materialDoFogo,
                                 i == 0 ? 0.55f : Random.Range(0.7f, 0.95f), comLuz ? 9f : 0f, true, go.layer);
                if (comLuz && chama.TryGetComponent<Light>(out var luz))
                {
                    var t = chama.AddComponent<ChamaTremula>();
                    t.luz = luz;
                }
                chamas.Add(chama);
                indices.Add(i);
            }
            lf.chamas = chamas.ToArray();
            lf.indices = indices.ToArray();
            return lf;
        }

        // a cerca que se gastou (ou foi recolhida): as chamas param de nascer e as que estão no ar terminam
        void AntesDeSumir(GameObject go)
        {
            var fogo = go.GetComponentInChildren<LinhaDeFogo>();
            if (fogo == null || !fogo.gameObject.activeInHierarchy) return;
            fogo.transform.SetParent(transform, true);
            foreach (var ps in fogo.GetComponentsInChildren<ParticleSystem>()) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            foreach (var t in fogo.GetComponentsInChildren<ChamaTremula>()) t.apagando = true;
            Destroy(fogo.gameObject, 1.5f);
        }
    }

    /// <summary>As chamas da cerca de fogo acesa (11/11/2026) e o ponto do rastro de cada uma.</summary>
    public class LinhaDeFogo : MonoBehaviour
    {
        public GameObject[] chamas;
        public int[] indices;
    }

    /// <summary>Uma malha feita no Play (o rastro da cerca): some junto com o objeto.</summary>
    public class MalhaQueSome : MonoBehaviour
    {
        public Mesh malha;
        void OnDestroy()
        {
            if (malha != null) Destroy(malha);
        }
    }
}
