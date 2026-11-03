using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using ZombieCityCleaner.Player;

namespace ZombieCityCleaner.Zombies
{
    /// <summary>
    /// A horda que cerca (veja Zumbi.Cerco.cs). A cada 0,3 s, para cada jogador, os zumbis que o perseguem ou atacam ganham
    /// um lugar em volta dele: os lugares ficam a 90° uns dos outros (com 4 ou mais, o círculo inteiro), na mesma ordem em
    /// que eles chegam (pelo rumo de cada um, visto do jogador, começando depois do maior vão), para ninguém cruzar o
    /// caminho do outro; o círculo gira para ficar o mais perto possível de onde cada um já está. Com 2, eles vêm pelos dois
    /// lados; com 3, pela frente e pelos lados; com 4 ou mais, um vai por trás.
    /// </summary>
    public partial class ZombieManager
    {
        float _proximoCerco;
        readonly List<(Zumbi z, float rumo)> _noCerco = new List<(Zumbi, float)>();
        // a "assinatura" do grupo de cada jogador (quantos e quais): igual, os lugares ficam (senão o círculo girava a cada
        // 0,3 s, conforme eles andavam, e eles corriam atrás dos lugares)
        readonly Dictionary<IAlvoDeZumbi, long> _grupos = new Dictionary<IAlvoDeZumbi, long>();
        readonly List<IAlvoDeZumbi> _semAlvo = new List<IAlvoDeZumbi>();

        void OrganizarCerco()
        {
            // quem saiu da lista dos alvos (o sobrevivente que morreu cercado, o jogador que saiu) não fica preso aqui
            if (_grupos.Count > QuantosAlvos)
            {
                _semAlvo.Clear();
                _semAlvo.AddRange(_grupos.Keys);
                for (int k = 0; k < QuantosAlvos; k++) _semAlvo.Remove(AlvoNo(k).quem);
                foreach (var quem in _semAlvo) _grupos.Remove(quem);
                _semAlvo.Clear();
            }
            // cada alvo (os jogadores e os sobreviventes: 02/11/2026) com o grupo dele
            for (int k = 0; k < QuantosAlvos; k++)
            {
                var jogador = AlvoNo(k).quem;
                Vector3 c = AlvoNo(k).pes;
                _noCerco.Clear();
                foreach (var z in _ativos)
                {
                    if (z.Morto || z.Alvo != jogador || z.NivelAtual == Zumbi.Nivel.Longe) continue;
                    // quem pula para fora da luz (Zumbi.Luz.cs) continua no grupo: o lugar dele fica, e o dos outros não muda
                    if (z.Atual != Zumbi.Estado.Perseguindo && z.Atual != Zumbi.Estado.Atacando && z.Atual != Zumbi.Estado.Esquivando) continue;
                    Vector3 p = z.transform.position;
                    _noCerco.Add((z, Mathf.Atan2(p.x - c.x, p.z - c.z)));
                }
                int n = _noCerco.Count;
                if (n == 0)
                {
                    _grupos.Remove(jogador);
                    continue;
                }
                long assinatura = n;
                foreach (var (z, _) in _noCerco) assinatura = assinatura * 31 + z.Id * 7 + z.Geracao;
                if (_grupos.TryGetValue(jogador, out long antes) && antes == assinatura)
                {
                    foreach (var (z, _) in _noCerco) z.RenovarCerco();
                    continue;
                }
                _grupos[jogador] = assinatura;
                _noCerco.Sort((a, b) => a.rumo.CompareTo(b.rumo));
                // começa depois do maior vão entre dois rumos (um grupo que vem por trás não é cortado ao meio)
                int primeiro = 0;
                float maiorVao = -1f;
                for (int i = 0; i < n; i++)
                {
                    float vao = (i + 1 < n ? _noCerco[i + 1].rumo : _noCerco[0].rumo + 2f * Mathf.PI) - _noCerco[i].rumo;
                    if (vao > maiorVao)
                    {
                        maiorVao = vao;
                        primeiro = (i + 1) % n;
                    }
                }
                float passo = 2f * Mathf.PI / Mathf.Max(n, 4);
                // o giro do círculo: a média (circular) de onde cada um está menos o lugar dele na ordem
                float sx = 0f, sz = 0f;
                for (int i = 0; i < n; i++)
                {
                    int idx = (primeiro + i) % n;
                    float rumo = _noCerco[idx].rumo + (idx < primeiro ? 2f * Mathf.PI : 0f) - i * passo;
                    sx += Mathf.Sin(rumo);
                    sz += Mathf.Cos(rumo);
                }
                float inicio = Mathf.Atan2(sx, sz);
                for (int i = 0; i < n; i++) _noCerco[(primeiro + i) % n].z.DefinirCerco(inicio + i * passo);
            }
        }

        /// <summary>
        /// Uma horda já vindo atrás do alvo (o jogador, no teste do F1; o mecânico do carro quebrado, na troca do pneu),
        /// nascida junta num ponto (todos chegam pelo mesmo lado: o cerco é que os espalha em volta). Eles foram
        /// "ameaçados": vêm mesmo de dia.
        /// </summary>
        public int CriarHordaPerseguindo(IAlvoDeZumbi jogador, Vector3 centro, int quantidade)
        {
            if (!IsServer || !IsSpawned || jogador == null) return 0;
            float agora = Time.time;
            int n = 0;
            for (int t = 0; t < quantidade * 6 && n < quantidade; t++)
            {
                if (!Sortear(centro, 3f, out var p, false)) continue;
                var z = Nascer(p);
                if (z == null) break;
                z.Ameacar(agora);
                z.ComecarPerseguicao(jogador, agora);
                n++;
            }
            return n;
        }
    }
}
