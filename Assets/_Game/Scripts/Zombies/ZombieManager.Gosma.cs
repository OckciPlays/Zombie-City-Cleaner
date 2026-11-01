using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using ZombieCityCleaner.Loot;
using ZombieCityCleaner.Player;
using ZombieCityCleaner.Vehicles;

namespace ZombieCityCleaner.Zombies
{
    /// <summary>
    /// A gosma do Long (01/11/2026, a pedido): o cuspe voa em arco do papo até o alvo; onde cai, fica uma poça preta
    /// (<see cref="AjustesDoLong.raioDaPoca"/>, por <see cref="AjustesDoLong.tempoDaPoca"/> s). O anfitrião decide os efeitos:
    /// quem leva em cheio fica preso no chão (segurar E para se soltar: MovimentoPrimeiraPessoa) e a lanterna de quem
    /// estiver na poça (ou pisar nela depois) apaga e só volta trocando as pilhas; na poça, a fogueira apaga, a tocha na mão
    /// apaga e os faróis do carro ficam cobertos por um tempo. O papo estourado (um tiro nele) também faz uma poça. O voo e a
    /// poça aparecem em todas as máquinas (as RPCs abaixo); os efeitos são só do anfitrião.
    /// </summary>
    public partial class ZombieManager
    {
        struct Cuspida
        {
            public Vector3 de, para;
            public float chega;
        }

        class Poca
        {
            public Vector3 ponto;
            public float raio, ate;
            public readonly HashSet<ulong> atingidos = new HashSet<ulong>();
        }

        const float RaioDoAcertoEmCheio = 1.0f;
        const float FaroisCobertos = 75f;   // s

        readonly List<Cuspida> _cuspidas = new List<Cuspida>();
        readonly List<Poca> _pocas = new List<Poca>();
        readonly List<Vector3> _luzesPerto = new List<Vector3>();
        float _proximaPoca;

        // ------------------------------------------------------------------ anfitrião

        /// <summary>O Long cospe (anfitrião): a gosma sai da boca e cai no ponto, no tempo do voo.</summary>
        internal void LancarGosma(Vector3 de, Vector3 para, Zumbi quem)
        {
            float voo = Mathf.Clamp(Vector3.Distance(de, para) / Mathf.Max(4f, ajustes.cuspidor.velocidadeDaGosma), 0.25f, 2.5f);
            _cuspidas.Add(new Cuspida { de = de, para = para, chega = Time.time + voo });
            GosmaRpc(de, para, voo);
        }

        /// <summary>O papo estourou (anfitrião): a gosma cai em volta dele.</summary>
        internal void EstourarPapo(Vector3 ponto, Zumbi quem)
        {
            if (Physics.Raycast(ponto, Vector3.down, out var hit, 4f, 1, QueryTriggerInteraction.Ignore)) ponto = hit.point;
            NovaPoca(ponto, ajustes.cuspidor.raioDaPoca * 0.8f, false);
            TocarEspecial("Gosma_Splash", ponto, 1f, 30f);
        }

        /// <summary>A luz mais perto para o Long cuspir (a fogueira acesa, a tocha na mão, os faróis), se ele a vê.</summary>
        internal bool LuzParaCuspir(Vector3 centro, float alcance, Vector3 boca, out Vector3 ponto)
        {
            ponto = default;
            _luzesPerto.Clear();
            ConstrucoesNoMundo.Instancia?.FogueirasAcesas(centro, alcance, _luzesPerto);
            for (int k = 0; k < QuantosJogadores; k++)
            {
                var j = Jogadores[k].jogador;
                if (j != null && j.NaMao == Item.Tocha && (Jogadores[k].pes - centro).sqrMagnitude < alcance * alcance) _luzesPerto.Add(Jogadores[k].pes);
            }
            foreach (var c in Carro.Todos)
            {
                if (c == null || !c.FaroisLigados) continue;
                Vector3 frente = c.transform.position + c.transform.forward * 2f;
                if ((frente - centro).sqrMagnitude < alcance * alcance) _luzesPerto.Add(frente);
            }
            float menor = float.MaxValue;
            bool achou = false;
            foreach (var p in _luzesPerto)
            {
                float d = (p - centro).sqrMagnitude;
                if (d >= menor || !Enxerga(boca, p + Vector3.up * 0.6f)) continue;
                menor = d;
                ponto = p;
                achou = true;
            }
            return achou;
        }

        /// <summary>A cada quadro, no anfitrião: as gosmas que chegaram e quem pisa nas poças.</summary>
        void GosmaNoServidor(float agora)
        {
            for (int i = _cuspidas.Count - 1; i >= 0; i--)
            {
                var c = _cuspidas[i];
                if (agora < c.chega) continue;
                _cuspidas.RemoveAt(i);
                Vector3 ponto = c.para;
                if (Physics.Raycast(ponto + Vector3.up * 1.5f, Vector3.down, out var hit, 4f, 1, QueryTriggerInteraction.Ignore)) ponto = hit.point;
                NovaPoca(ponto, ajustes.cuspidor.raioDaPoca, true);
            }
            if (agora < _proximaPoca) return;
            _proximaPoca = agora + 0.25f;
            for (int i = _pocas.Count - 1; i >= 0; i--)
            {
                var p = _pocas[i];
                if (agora >= p.ate)
                {
                    _pocas.RemoveAt(i);
                    continue;
                }
                NaPoca(p, false);
            }
        }

        void NovaPoca(Vector3 ponto, float raio, bool cuspe)
        {
            var c = ajustes.cuspidor;
            var p = new Poca { ponto = ponto, raio = raio, ate = Time.time + c.tempoDaPoca };
            _pocas.Add(p);
            PocaRpc(ponto, raio, c.tempoDaPoca);
            // em cheio: quem estava bem onde caiu fica preso
            if (cuspe)
                for (int k = 0; k < QuantosJogadores; k++)
                {
                    ref readonly var j = ref Jogadores[k];
                    Vector3 d = j.pes - ponto;
                    if (Mathf.Abs(d.y) < 1.6f && new Vector2(d.x, d.z).sqrMagnitude < RaioDoAcertoEmCheio * RaioDoAcertoEmCheio)
                        j.jogador.PrenderNaGosma(c.tempoParaSoltar);
                }
            NaPoca(p, true);
            // as luzes na poça
            ConstrucoesNoMundo.Instancia?.ApagarFogueiras(ponto, raio + 0.6f);
            foreach (var carro in Carro.Todos)
            {
                if (carro == null) continue;
                Vector3 frente = carro.transform.position + carro.transform.forward * 2f;
                if ((frente - ponto).sqrMagnitude < (raio + 1.2f) * (raio + 1.2f)) carro.SujarFarois(FaroisCobertos);
            }
        }

        /// <summary>Quem está na poça (e ainda não foi atingido por ela): a lanterna apaga, a tocha na mão apaga.</summary>
        void NaPoca(Poca p, bool agora)
        {
            for (int k = 0; k < QuantosJogadores; k++)
            {
                ref readonly var j = ref Jogadores[k];
                if (j.jogador == null || p.atingidos.Contains(j.jogador.OwnerClientId)) continue;
                Vector3 d = j.pes - p.ponto;
                if (Mathf.Abs(d.y) > 1.6f || new Vector2(d.x, d.z).sqrMagnitude > p.raio * p.raio) continue;
                p.atingidos.Add(j.jogador.OwnerClientId);
                var inv = j.jogador.Inventario;
                if (inv == null) continue;
                inv.EsvaziarLanterna();
                inv.ApagarTochaNaMao();
            }
        }

        // ------------------------------------------------------------------ em todas as máquinas: o que se vê

        static Material _materialDaGosma;

        static Material MaterialDaGosma()
        {
            if (_materialDaGosma != null) return _materialDaGosma;
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            _materialDaGosma = new Material(shader) { name = "Gosma do Long" };
            _materialDaGosma.SetColor("_BaseColor", new Color(0.015f, 0.015f, 0.018f));
            _materialDaGosma.SetFloat("_Smoothness", 0.92f);
            _materialDaGosma.SetFloat("_Metallic", 0f);
            return _materialDaGosma;
        }

        [Rpc(SendTo.ClientsAndHost)]
        void GosmaRpc(Vector3 de, Vector3 para, float voo)
        {
            StartCoroutine(Voar(de, para, voo));
        }

        System.Collections.IEnumerator Voar(Vector3 de, Vector3 para, float voo)
        {
            var bola = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(bola.GetComponent<Collider>());
            bola.name = "Gosma no ar";
            bola.transform.localScale = Vector3.one * 0.22f;
            bola.GetComponent<Renderer>().sharedMaterial = MaterialDaGosma();
            float altura = 0.12f * Vector3.Distance(de, para) + 0.4f;
            float t0 = Time.time;
            Vector3 antes = de;
            while (Time.time - t0 < voo)
            {
                float u = (Time.time - t0) / voo;
                Vector3 p = Vector3.Lerp(de, para, u) + Vector3.up * (4f * altura * u * (1f - u));
                bola.transform.position = p;
                // ela se estica no voo (gota viscosa)
                Vector3 v = p - antes;
                if (v.sqrMagnitude > 1e-6f) bola.transform.rotation = Quaternion.LookRotation(v);
                bola.transform.localScale = new Vector3(0.2f, 0.2f, 0.32f);
                antes = p;
                yield return null;
            }
            Destroy(bola);
        }

        [Rpc(SendTo.ClientsAndHost)]
        void PocaRpc(Vector3 ponto, float raio, float dura)
        {
            TocarEspecial("Gosma_Splash", ponto, 1f, 35f);
            StartCoroutine(MostrarPoca(ponto, raio, dura));
        }

        System.Collections.IEnumerator MostrarPoca(Vector3 ponto, float raio, float dura)
        {
            var raiz = new GameObject("Poça de gosma");
            raiz.transform.position = ponto + Vector3.up * 0.015f;
            var pecas = new List<Transform>();
            // a mancha grande e uns respingos em volta, achatados no chão
            for (int i = 0; i < 9; i++)
            {
                var m = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Destroy(m.GetComponent<Collider>());
                m.GetComponent<Renderer>().sharedMaterial = MaterialDaGosma();
                m.transform.SetParent(raiz.transform, false);
                if (i == 0) m.transform.localScale = new Vector3(raio * 1.6f, 0.006f, raio * 1.35f);
                else
                {
                    Vector2 r = Random.insideUnitCircle * raio * 0.95f;
                    m.transform.localPosition = new Vector3(r.x, 0.002f * i, r.y);
                    float s = Random.Range(0.25f, 0.7f) * raio;
                    m.transform.localScale = new Vector3(s, 0.006f, s * Random.Range(0.6f, 1f));
                }
                m.transform.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                pecas.Add(m.transform);
            }
            float t0 = Time.time;
            // espalha rápido ao cair, fica e, no fim, encolhe (seca)
            while (Time.time - t0 < dura)
            {
                float t = Time.time - t0;
                float k = t < 0.25f ? t / 0.25f : t > dura - 3f ? Mathf.Clamp01((dura - t) / 3f) : 1f;
                raiz.transform.localScale = new Vector3(k, 1f, k);
                yield return null;
            }
            Destroy(raiz);
        }
    }
}
