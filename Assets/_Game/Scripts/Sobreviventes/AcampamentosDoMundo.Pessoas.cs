using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using ZombieCityCleaner.Combat;
using ZombieCityCleaner.Loot;
using ZombieCityCleaner.Player;
using ZombieCityCleaner.World;

namespace ZombieCityCleaner.Sobreviventes
{
    /// <summary>
    /// As pessoas dos acampamentos (02/11/2026), no anfitrião: com alguém a menos de (o raio + <see cref="AcampamentosDoMundo.chegarPerto"/>)
    /// m, os vivos aparecem nos lugares deles (os mortos continuam mortos: a máscara, no save); com todos longe, saem.
    ///  - os guardas: rifle ou espingarda, roupa militar ou de policial; no portão e, no grande, nos cantos; andam um pouco;
    ///  - os comerciantes: pistola; atrás da banca; E abre o comércio;
    ///  - os moradores: pistola ou espingarda; em volta das fogueiras.
    /// Amigos: ninguém atira nos jogadores, e quem mira muito tempo num deles ouve "Abaixa essa arma". Os zumbis que chegam
    /// perto levam tiro. Hostis: todos lutam como os grupos, e os comerciantes correm para dentro da casa.
    /// </summary>
    public partial class AcampamentosDoMundo
    {
        void ConferirAsPessoas()
        {
            var prefab = SobreviventesDoMundo.Instancia != null ? SobreviventesDoMundo.Instancia.prefab : null;
            if (prefab == null) return;
            Estados();
            for (int a = 0; a < Quantos; a++)
            {
                var p = Planta(a);
                var e = _estados[a];
                float perto = GrupoDeSobreviventes.PertoDeUmJogador(p.Mundo(Vector3.zero));
                // (13/11/2026, a pedido: "os NPCs da comunidade estavam sem reagir") o NavMesh em cima do acampamento inteiro,
                // pedido 40 m antes de as pessoas aparecerem: a área em volta de quem chegava (60 m para cada lado) não cobria
                // a maior parte dele, e quem nascia fora do NavMesh ficava parado, com o agente desligado, sem pensar
                if (perto < p.Lugar.Raio + chegarPerto + 40f) NavMeshDoMundo.Ancorar(e, p.Mundo(Vector3.zero), LadoDoNavMesh(p));
                else if (perto > p.Lugar.Raio + sairLonge) NavMeshDoMundo.Soltar(e);
                if (e.grupo == null && perto < p.Lugar.Raio + chegarPerto) Povoar(a, p, e, prefab);
                else if (e.grupo != null && perto > p.Lugar.Raio + sairLonge) Recolher(a, e);
            }
        }

        // o lado da área do NavMesh do acampamento: a cerca (2 × a meia) e uns metros de fora, na frente do portão
        static float LadoDoNavMesh(PlantaDoAcampamento p) => 2f * p.Meia + 16f;

        // as âncoras saem (o fim da partida)
        void SoltarAsAncoras()
        {
            foreach (var e in _estados) NavMeshDoMundo.Soltar(e);
        }

        void Povoar(int a, PlantaDoAcampamento p, Estado e, GameObject prefab)
        {
            var g = new GrupoDeSobreviventes { cidade = -1, acampamento = a, quantos = p.Pessoas };
            var s = new System.Random(unchecked(Dados.SementeDaPartida * 5113 + a * 70001 + 17));
            for (int i = 0; i < p.Pessoas; i++)
            {
                var papel = p.PapelDe(i);
                // (09/11/2026, a sucata, 2ª leva: as de sucata no lugar das reais; os guardas, o rifle de sucata)
                var arma = SobreviventesDoMundo.ArmaDoCivil(papel == PapelDoSobrevivente.Guarda ? (s.Next(100) < 65 ? TipoDeArma.Rifle : TipoDeArma.Espingarda)
                         : papel == PapelDoSobrevivente.Comerciante ? TipoDeArma.Pistola
                         : s.Next(100) < 60 ? TipoDeArma.Pistola : TipoDeArma.Espingarda);
                byte roupa = papel == PapelDoSobrevivente.Guarda ? (byte)(s.Next(2) == 0 ? 1 : 2) : (byte)(s.Next(100) < 55 ? 3 : 0);
                int sementeDaRoupa = s.Next();
                if ((e.mortos & (1 << i)) != 0) continue;   // este morreu
                var (local, giro) = p.LugarDe(i);
                var pos = NoChao(p.Mundo(local));
                var go = Instantiate(prefab, pos, Quaternion.Euler(0f, p.GiroNoMundo(giro), 0f));
                var sob = go.GetComponent<Sobrevivente>();
                sob.Identidade.Value = new IdentidadeDoSobrevivente
                {
                    semente = sementeDaRoupa, arma = arma, roupa = roupa, grupo = (ushort)(GrupoBase + a), papel = papel, indice = (byte)i,
                };
                sob.grupo = g;
                sob.acampamento = a;
                sob.posto = pos;
                sob.ronda = papel == PapelDoSobrevivente.Guarda ? p.Mundo(p.Guardas[i].ronda) : pos;
                sob.giroDoPosto = p.GiroNoMundo(giro);
                // cada um num cômodo (e, no mesmo cômodo, um pouco para o lado)
                int n = p.Esconderijos.Count, volta = i / n;
                var lado = new Vector3((volta % 3 - 1) * 0.8f, 0f, (volta / 3 % 3 - 1) * 0.8f);
                sob.esconderijo = p.Mundo(p.Esconderijos[i % n] + lado);
                SobreviventesDoMundo.Encher(sob, arma);
                go.GetComponent<NetworkObject>().Spawn(true);
                g.membros.Add(sob);
            }
            e.grupo = g;
            Debug.Log($"[Acampamentos] As pessoas do {p.Lugar.Nome} apareceram: {g.membros.Count} de {p.Pessoas}{(Hostil(a) ? " (hostil)" : "")}.");
        }

        // o ponto do NavMesh no chão perto do lugar (03/11/2026: o NavMesh mais perto podia ser o tampo do balcão da banca, e o
        // comerciante nascia lá em cima, ilhado): primeiro no próprio lugar, depois em volta, sempre na altura do chão
        static Vector3 NoChao(Vector3 pos)
        {
            for (int volta = 0; volta < 3; volta++)
                for (int k = 0; k < (volta == 0 ? 1 : 8); k++)
                {
                    var q = pos + Quaternion.Euler(0f, k * 45f, 0f) * Vector3.forward * (volta * 0.6f);
                    if (NavMesh.SamplePosition(q, out var hit, 0.6f, NavMesh.AllAreas) && Mathf.Abs(hit.position.y - pos.y) < 0.4f) return hit.position;
                }
            return NavMesh.SamplePosition(pos, out var perto, 2f, NavMesh.AllAreas) && perto.position.y < pos.y + 0.4f ? perto.position : pos;
        }

        void Recolher(int a, Estado e)
        {
            foreach (var m in e.grupo.membros.ToArray())
            {
                if (m == null) continue;
                var no = m.GetComponent<NetworkObject>();
                if (no != null && no.IsSpawned) no.Despawn(true);
            }
            e.grupo.membros.Clear();
            e.grupo = null;
            e.mirando.Clear();
        }

        /// <summary>No anfitrião (o F1): as pessoas de todos os acampamentos saem (voltam quando alguém chegar perto).</summary>
        public void TirarAsPessoas()
        {
            for (int a = 0; a < _estados.Count; a++)
                if (_estados[a].grupo != null) Recolher(a, _estados[a]);
        }

        /// <summary>As pessoas que estão no acampamento agora (no anfitrião).</summary>
        public GrupoDeSobreviventes Grupo(int a) => a >= 0 && a < _estados.Count ? _estados[a].grupo : null;

        /// <summary>No anfitrião: uma pessoa do acampamento morreu (a máscara dos mortos e a mochila no chão; a do comerciante leva parte do estoque).</summary>
        public void AoMorrer(Sobrevivente s)
        {
            if (!IsServer || s == null) return;
            int a = s.acampamento;
            Estados();
            if (a < 0 || a >= _estados.Count) return;
            int i = s.Identidade.Value.indice;
            _estados[a].mortos |= 1 << i;
            var extras = new List<ItemNaMochila>();
            var p = Planta(a);
            if (p != null && s.Identidade.Value.papel == PapelDoSobrevivente.Comerciante)
            {
                int banca = i - p.PrimeiroComerciante;
                var itens = PrecosDosSobreviventes.Itens(p.Bancas[banca].categoria);
                for (int k = 0; k < _estoque.Count; k++)
                {
                    var x = _estoque[k];
                    if (x.acampamento != a || x.banca != banca || x.qtd == 0 || x.produto >= itens.Length) continue;
                    int leva = Mathf.Max(1, x.qtd / 2);
                    Mercadoria(itens[x.produto], leva, extras);
                    x.qtd = (byte)(x.qtd - leva);
                    _estoque[k] = x;
                }
            }
            SobreviventesDoMundo.LargarAMochila(s, extras);
            Debug.Log($"[Acampamentos] Morreu {(s.Identidade.Value.papel == PapelDoSobrevivente.Comerciante ? "um comerciante" : "uma pessoa")} do {p?.Lugar.Nome}.");
        }

        // n do item como ficam na mochila: a arma com o pente cheio, a caixa cheia, a pilha junta, a peça nova
        static void Mercadoria(Item item, int n, List<ItemNaMochila> saida)
        {
            if (Itens.EmPilha(item) && !Itens.EhMunicao(item))
            {
                saida.Add(new ItemNaMochila { item = item, qtd = (ushort)n });
                return;
            }
            for (int k = 0; k < n; k++)
                saida.Add(new ItemNaMochila { item = item, qtd = (ushort)Mathf.Max(1, Itens.QuantidadePadrao(item)), desgaste = (byte)(Itens.EhArma(item) ? Random.Range(5, 30) : 0) });
        }

        // ------------------------------------------------------------------ a mira e as boas-vindas

        // amigo: quem mira (com o botão direito) numa pessoa do acampamento por 2 s ouve "Abaixa essa arma"; quem chega
        // perto de um comerciante ouve "Precisa de alguma coisa?"
        void VigiarAMira(int a, float passo)
        {
            if (a >= _estados.Count || Hostil(a)) return;
            var e = _estados[a];
            if (e.grupo == null) return;
            float agora = Time.time;
            foreach (var j in JogadorRede.Todos)
            {
                if (j == null || !j.AlvoValido || !j.Criado) continue;
                Vector3 olho = j.transform.position + Vector3.up * 1.6f;
                Sobrevivente mirado = null;
                if (j.Mirando.Value)
                {
                    Vector3 dir = Quaternion.Euler(j.Pitch.Value, j.transform.eulerAngles.y, 0f) * Vector3.forward;
                    foreach (var m in e.grupo.membros)
                    {
                        if (m == null || m.Morto) continue;
                        Vector3 v = m.transform.position + Vector3.up * 1.3f - olho;
                        float t = Vector3.Dot(v, dir);
                        if (t < 0.5f || t > 35f || (v - dir * t).sqrMagnitude > 0.75f * 0.75f) continue;
                        mirado = m;
                        break;
                    }
                }
                if (mirado == null) e.mirando.Remove(j);
                else
                {
                    e.mirando.TryGetValue(j, out float tempo);
                    tempo += passo;
                    e.mirando[j] = tempo;
                    if (tempo >= 2f && agora >= e.proximaFalaDaMira)
                    {
                        e.proximaFalaDaMira = agora + 6f;
                        mirado.Falar(Frase.AbaixaArma);
                    }
                }
                // as boas-vindas do comerciante
                foreach (var m in e.grupo.membros)
                {
                    if (m == null || m.Morto || m.Identidade.Value.papel != PapelDoSobrevivente.Comerciante || agora < m.proximaSaudacao) continue;
                    if ((m.transform.position - j.transform.position).sqrMagnitude > 4.5f * 4.5f) continue;
                    m.proximaSaudacao = agora + 40f;
                    m.Falar(Frase.Ola);
                }
            }
        }
    }
}
