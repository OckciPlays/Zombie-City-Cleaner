using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using ZombieCityCleaner.Loot;
using ZombieCityCleaner.Player;

namespace ZombieCityCleaner.Zombies
{
    /// <summary>
    /// O fogo da etapa 4 do ramo Fogo (11/11/2026; os números no Player/FogoDoJogador.cs e no <see cref="AjustesDeZumbi"/>,
    /// "O fogo, etapa 4"), no anfitrião:
    ///  - os fogos novos que os seguram na beira: a área do molotov (como uma fogueira) e o lampião aceso largado no chão (como
    ///    a tocha); o da mão, a tocha e a fogueira estão no <see cref="AtualizarFogos"/>. Com a Muralha de fogo do dono, o raio
    ///    é maior e o fogo dá pânico (o <see cref="Fogo.panico"/>);
    ///  - quem está na área do molotov pega fogo: queima dentro dela e continua queimando um pouco depois de sair. O fogo não
    ///    o marca como ferido (o ferido entra no fogo) nem o faz caçar quem jogou; quem mata ganha o XP. O Brutamonte não
    ///    queima (a casca). Cada máquina vê o fogo no corpo (<see cref="PegouFogoRpc"/>).
    /// </summary>
    public partial class ZombieManager
    {
        const int ChaveDoLampiao = 200000, ChaveDoMolotov = 300000;
        readonly List<(ushort id, Vector3 pos, ulong dono)> _lampioes = new List<(ushort, Vector3, ulong)>();

        void FogosDaEtapa4(AjustesDeZumbi a)
        {
            var c = ConstrucoesNoMundo.Instancia;
            if (c != null)
                foreach (var area in c.AreasDeFogo)
                    Fogos.Add(new Fogo { centro = area.centro, raio = a.raioDoMolotov * area.muralha, chave = ChaveDoMolotov + area.id, panico = area.muralha > 1f });
            var itens = ItensNoMundo.Instancia;
            if (itens != null)
            {
                _lampioes.Clear();
                itens.LampioesAcesos(_lampioes);
                foreach (var (id, pos, dono) in _lampioes)
                {
                    float m = FogoDoJogador.MuralhaDaChave(dono);
                    Fogos.Add(new Fogo { centro = pos, raio = a.raioDoLampiao * m, chave = ChaveDoLampiao + id, panico = m > 1f });
                }
            }
            FogosDasArmadilhas();   // a cerca de fogo acesa (11/11/2026, a etapa 5a: ZombieManager.Armadilhas.cs)
            QueimarOsQueimando();
        }

        // ------------------------------------------------------------------ quem pegou fogo

        sealed class Queima
        {
            public float ate, dentroAte, visualAte;
            public JogadorRede dono;
        }

        readonly Dictionary<Zumbi, Queima> _queimando = new Dictionary<Zumbi, Queima>();
        readonly List<Zumbi> _apagados = new List<Zumbi>();
        readonly List<Zumbi> _naArea = new List<Zumbi>();
        float _proximaQueima;

        /// <summary>Quantos zumbis estão queimando agora (o F1 e o teste).</summary>
        public int Queimando => _queimando.Count;

        /// <summary>Só no anfitrião (o teste): os fogos que os seguram agora (a chave, o raio, o pânico e o ponto).</summary>
        public string ResumoDosFogos()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var f in Fogos) sb.Append(System.FormattableString.Invariant($"[{f.chave}: raio {f.raio:F1}{(f.panico ? ", pânico" : "")} em ({f.centro.x:F1}, {f.centro.z:F1})] "));
            return sb.ToString();
        }

        /// <summary>Só no anfitrião: os zumbis dentro da área de fogo queimam (o passo, em s) e pegam fogo.</summary>
        internal void FogoNaArea(Vector3 centro, float raio, float passo, JogadorRede dono, float segundosQueimando)
        {
            _naArea.Clear();
            foreach (var z in Ativos)
            {
                if (z == null || z.Morto) continue;
                Vector3 d = z.transform.position - centro;
                if (d.y < -1.5f || d.y > 2f) continue;
                d.y = 0f;
                if (d.sqrMagnitude < raio * raio) _naArea.Add(z);
            }
            foreach (var z in _naArea)
            {
                PorFogo(z, segundosQueimando, dono);
                Queimar(z, FogoDoJogador.DanoNosZumbis * passo, dono);
            }
        }

        void PorFogo(Zumbi z, float segundos, JogadorRede dono)
        {
            if (z.Morto || z.tipo == TipoDeZumbi.Brutamonte) return;
            if (!_queimando.TryGetValue(z, out var q)) _queimando[z] = q = new Queima();
            float agora = Time.time;
            q.ate = Mathf.Max(q.ate, agora + segundos);
            q.dentroAte = agora + 0.3f;   // dentro da área, o dano é o dela (não soma o de queimando)
            if (dono != null) q.dono = dono;
            if (q.ate > q.visualAte - 0.5f)
            {
                q.visualAte = q.ate + 1.5f;
                PegouFogoRpc(z.Id, z.Geracao, q.visualAte - agora);
            }
        }

        // a cada quarto de segundo: quem saiu da área ainda queima, até o fogo dele acabar
        void QueimarOsQueimando()
        {
            if (_queimando.Count == 0 || Time.time < _proximaQueima) return;
            const float passo = 0.25f;
            _proximaQueima = Time.time + passo;
            _apagados.Clear();
            _naArea.Clear();
            foreach (var kv in _queimando)
            {
                if (kv.Key == null || kv.Key.Morto || Time.time > kv.Value.ate) _apagados.Add(kv.Key);
                else if (Time.time >= kv.Value.dentroAte) _naArea.Add(kv.Key);
            }
            foreach (var z in _apagados) _queimando.Remove(z);
            foreach (var z in _naArea) Queimar(z, FogoDoJogador.DanoQueimando * passo, _queimando[z].dono);
        }

        // o dano do fogo (sem o ferido e sem caçar quem jogou) e o XP de quem matou
        void Queimar(Zumbi z, float dano, JogadorRede dono)
        {
            bool abatido = z.SemComando;
            if (!z.Queimar(dano, Time.time)) return;
            if (!abatido && dono != null && dono.Inventario != null) dono.Inventario.GanharXp(XpDoZumbi(z), NomeNoXp(z));
        }

        [Rpc(SendTo.ClientsAndHost)]
        void PegouFogoRpc(int id, byte geracao, float segundos)
        {
            if (id < 0 || id >= MaxZumbis) return;
            var z = _porId[id];
            var c = ConstrucoesNoMundo.Instancia;
            if (z == null || z.Geracao != geracao || c == null) return;
            var t = z.transform.Find("Fogo no corpo");
            if (t == null)
            {
                var go = new GameObject("Fogo no corpo");
                t = go.transform;
                t.SetParent(z.transform, false);
                t.localPosition = new Vector3(0f, 0.95f, 0f);
                var fogo = ConstrucoesNoMundo.Fogo(t, Vector3.zero, c.materialDoFogo, 0.7f, 4.5f, true, 0);
                if (fogo.TryGetComponent<Light>(out var luz)) luz.intensity = 1.8f;
                go.AddComponent<FogoQueApaga>();
            }
            var apaga = t.GetComponent<FogoQueApaga>();
            if (apaga != null) apaga.ate = Time.time + segundos;
            t.gameObject.SetActive(true);
        }
    }

    /// <summary>O fogo no corpo do zumbi (11/11/2026): some sozinho quando o tempo dele acaba.</summary>
    public class FogoQueApaga : MonoBehaviour
    {
        public float ate;
        bool _apagando;

        void Update()
        {
            if (_apagando || Time.time < ate) return;
            _apagando = true;
            gameObject.name = "Fogo apagando";   // (um fogo novo no mesmo corpo nasce de novo, não neste)
            foreach (var ps in GetComponentsInChildren<ParticleSystem>()) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            foreach (var l in GetComponentsInChildren<Light>()) l.enabled = false;
            Destroy(gameObject, 1f);
        }
    }
}
