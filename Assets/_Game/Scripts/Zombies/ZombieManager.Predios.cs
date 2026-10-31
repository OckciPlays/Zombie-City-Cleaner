using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using ZombieCityCleaner.Combat;
using ZombieCityCleaner.Mundo;
using ZombieCityCleaner.World;

namespace ZombieCityCleaner.Zombies
{
    /// <summary>
    /// Os zumbis dentro dos prédios (a pedido em 29/10/2026: "não estão spawnando zumbis dentro dos prédios, nos
    /// apartamentos"). O povoamento das células sorteia pontos no chão do mapa, e os andares de cima de um prédio só
    /// existem (com a colisão e o NavMesh) quando alguém chega perto (World/AndaresDoPredio: de fora, o térreo e o
    /// primeiro; dentro, dois acima e dois abaixo de quem está lá). Então cada andar (fora o térreo, que as células já
    /// cobrem) ganha os seus zumbis na primeira vez que carrega: <see cref="acordadosPorAndar"/>, vezes a densidade de
    /// agora (×2 de dia, ×3 à noite), dentro dos apartamentos, onde nenhum jogador está vendo. Subindo a escada, cada andar
    /// novo tem os seus. Como as células: ao escurecer, os andares do dia ganham a diferença; longe de todos, o andar se
    /// esquece (e se povoa de novo na volta).
    /// </summary>
    public partial class ZombieManager
    {
        [Header("Os prédios")]
        [Tooltip("Zumbis acordados em cada andar de prédio (fora o térreo), quando o andar carrega perto de alguém (mínimo, máximo; vezes a densidade de agora).")]
        public Vector2Int acordadosPorAndar = new Vector2Int(0, 1);

        // os andares já povoados (o prédio, pela posição, e o andar), com a densidade com que foram povoados
        readonly Dictionary<(Vector3Int, int), float> _andaresPovoados = new Dictionary<(Vector3Int, int), float>();
        readonly List<(Vector3Int, int)> _andaresParaEsquecer = new List<(Vector3Int, int)>();

        /// <summary>Para conferir (o teste): quantos andares de prédio já se povoaram.</summary>
        public int AndaresPovoados => _andaresPovoados.Count;

        // a cada segundo, no anfitrião: até 2 andares por vez
        void PovoarAndares(DadosDoMundo dados)
        {
            float agoraD = DensidadeAgora;
            int feitos = 0;
            foreach (var predio in AndaresDoPredio.Ativos)
            {
                var planta = predio != null ? predio.Planta : null;
                if (planta == null) continue;
                var id = Vector3Int.RoundToInt(predio.transform.position);
                for (int f = 1; f < planta.andares; f++)
                {
                    if (feitos == 2) return;
                    if (!predio.CarregadosDe(f, f)) continue;
                    var chave = (id, f);
                    float antes = _andaresPovoados.TryGetValue(chave, out float ja) ? ja : -1f;
                    if (antes >= agoraD - 0.01f) continue;
                    if (!AndarNoNavMesh(predio, planta, f)) continue;   // o NavMesh do andar ainda não veio: no próximo segundo
                    _andaresPovoados[chave] = agoraD;
                    int rodada = antes < 0f ? 0 : 1 + Mathf.RoundToInt(antes * 10f);
                    PovoarAndar(predio, planta, f, antes < 0f ? agoraD : agoraD - antes, Misturar(dados.Semente + rodada * 7919, id.x * 31 + f, id.z));
                    feitos++;
                }
            }
        }

        // o NavMesh já cobre o andar? (o meio do corredor, na altura do piso dele)
        static bool AndarNoNavMesh(AndaresDoPredio predio, PlantaDePredio planta, int f)
        {
            var meio = predio.transform.TransformPoint(new Vector3(0f, planta.Base(f) + 0.05f, (planta.zc0 + planta.zc1) / 2f));
            return NavMesh.SamplePosition(meio, out var hit, 1.5f, NavMesh.AllAreas) && Mathf.Abs(hit.position.y - meio.y) < 0.6f;
        }

        void PovoarAndar(AndaresDoPredio predio, PlantaDePredio planta, int f, float densidade, int semente)
        {
            var sorte = new System.Random(semente);
            int n = Vezes(sorte.Next(acordadosPorAndar.x, acordadosPorAndar.y + 1), densidade, sorte);
            if (n == 0) return;
            PovoamentoQuis += n;
            var comodos = planta.ComodosDo(f);
            var aptos = comodos.FindAll(k => k.apto >= 0);   // os cômodos dos apartamentos (no hospital, as salas)
            if (aptos.Count == 0) aptos = comodos;
            float sx = planta.espelhar ? -1f : 1f, chao = planta.Base(f);
            for (int i = 0; i < n; i++)
                for (int t = 0; t < 12; t++)
                {
                    var k = aptos[sorte.Next(aptos.Count)];
                    float x = Mathf.Lerp(k.x0 + 0.5f, k.x1 - 0.5f, (float)sorte.NextDouble());
                    float z = Mathf.Lerp(k.z0 + 0.5f, k.z1 - 0.5f, (float)sorte.NextDouble());
                    var alvo = predio.transform.TransformPoint(new Vector3(sx * x, chao + 0.05f, z));
                    if (!NavMesh.SamplePosition(alvo, out var hit, 1f, NavMesh.AllAreas) || Mathf.Abs(hit.position.y - alvo.y) > 0.6f) continue;
                    var p = hit.position;
                    if (ProtegerDoSol && Exposto(p)) continue;   // o sol entrando pela janela
                    if (AlguemVe(p)) continue;
                    if (Nascer(p) != null) PovoamentoPos++;
                    break;
                }
        }

        // ninguém pode ver o zumbi aparecer: a mais de 6 m de cada jogador e com parede (ou laje) entre os olhos dele e o zumbi
        bool AlguemVe(Vector3 p)
        {
            for (int k = 0; k < QuantosJogadores; k++)
            {
                Vector3 pes = Jogadores[k].pes;
                if ((p - pes).sqrMagnitude < 36f) return true;
                if (!Physics.Linecast(pes + Vector3.up * 1.6f, p + Vector3.up * 1.4f, Abafamento.Mascara, QueryTriggerInteraction.Ignore)) return true;
            }
            return false;
        }

        // no Recolher: o andar de um prédio longe de todos se esquece (e se povoa de novo quando alguém voltar)
        void EsquecerAndares(float raio2)
        {
            _andaresParaEsquecer.Clear();
            foreach (var chave in _andaresPovoados.Keys)
                if (!PertoDeAlguem(chave.Item1, raio2)) _andaresParaEsquecer.Add(chave);
            foreach (var chave in _andaresParaEsquecer) _andaresPovoados.Remove(chave);
        }
    }
}
