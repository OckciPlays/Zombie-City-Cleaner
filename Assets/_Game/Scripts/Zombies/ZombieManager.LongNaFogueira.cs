using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.AI;
using ZombieCityCleaner.Player;

namespace ZombieCityCleaner.Zombies
{
    /// <summary>
    /// O Long da fogueira (05/11/2026, a pedido: "à noite ... se >5 zumbis em volta da fogueira, e jogador parado por >1
    /// minuto, um Long spawna obrigatoriamente, assim apagando a fogueira"). No anfitrião, a cada segundo: à noite, numa
    /// fogueira acesa com <see cref="AjustesDeZumbi.zumbisParaOLong"/> ou mais zumbis caçando em volta (até 3 m além da
    /// beira) e um jogador parado dentro dela (sem sair de um círculo de 3 m) há <see cref="AjustesDeZumbi.paradoParaOLong"/>
    /// s, um Long nasce a 17–24 m dela, a mais de 15 m de todos os jogadores, de preferência num ponto de onde ele vê o fogo.
    /// Dali ele faz o de sempre (Zumbi.Long.cs): vê quem está ali e cospe na luz mais perto, a fogueira, e a gosma a apaga
    /// (ZombieManager.Gosma.cs). À noite, numa fogueira, a coragem da horda espera mais
    /// (<see cref="AjustesDeZumbi.esperaAteACoragemDeNoiteNaFogueira"/>, 90 s): o Long vem antes, e a coragem fica de plano B se
    /// ele morrer antes de cuspir. Uma vez por fogueira a cada <see cref="AjustesDeZumbi.recargaDoLongDaFogueira"/> s, e não com
    /// outro Long já perto dela.
    /// </summary>
    public partial class ZombieManager
    {
        const float ParadoAte = 3f;            // quanto (m) o jogador anda sem deixar de estar parado
        const float EmVoltaDaFogueira = 3f;    // os zumbis contam até aqui além da beira

        // o lugar de cada jogador (pelo cliente) e desde quando ele está ali
        readonly Dictionary<ulong, (Vector3 ponto, float desde)> _parados = new Dictionary<ulong, (Vector3, float)>();
        readonly Dictionary<int, float> _recargaDoLongDaFogueira = new Dictionary<int, float>();
        float _proximoOlharDaFogueira;

        /// <summary>A cada quadro, no anfitrião (a conta, a cada segundo).</summary>
        void LongNaFogueira(float agora)
        {
            if (agora < _proximoOlharDaFogueira) return;
            _proximoOlharDaFogueira = agora + 1f;
            AtualizarParados(agora);
            if (EDia || QuantosJogadores == 0 || Povoando || Fogos.Count == 0) return;
            var a = ajustes;
            for (int i = 0; i < Fogos.Count; i++)
            {
                var f = Fogos[i];
                if (f.chave >= ChaveDaTocha) continue;   // só a fogueira (a tocha tem a coragem de sempre)
                if (_recargaDoLongDaFogueira.TryGetValue(f.chave, out float ate) && agora < ate) continue;
                int quem = QuemEstaParado(f, agora, out float parado);
                if (quem < 0 || parado < a.paradoParaOLong) continue;
                if (ZumbisEmVolta(f) < a.zumbisParaOLong) continue;
                if (LongPerto(f.centro, a.cuspidor.alcanceDoCuspe + 4f)) continue;   // um já está ali (e ainda cospe)
                if (NascerLongDaFogueira(f, Jogadores[quem].pes + Vector3.up * 1.3f)) _recargaDoLongDaFogueira[f.chave] = agora + a.recargaDoLongDaFogueira;
            }
        }

        // o minuto conta só parado dentro de uma fogueira acesa, de noite: quem se mexe mais de ParadoAte, sai do fogo, entra
        // no carro ou renasce em outro lugar começa de novo (e quem saiu do jogo é esquecido)
        readonly HashSet<ulong> _vistosAgora = new HashSet<ulong>();
        readonly List<ulong> _sairam = new List<ulong>();

        void AtualizarParados(float agora)
        {
            _vistosAgora.Clear();
            for (int k = 0; k < QuantosJogadores; k++)
            {
                var j = Jogadores[k].jogador;
                if (j == null) continue;
                ulong id = j.OwnerClientId;
                _vistosAgora.Add(id);
                Vector3 pe = Jogadores[k].pes;
                bool conta = !EDia && !(j.Passageiro != null && j.Passageiro.NoCarro) && NumaFogueira(pe);
                if (!conta || !_parados.TryGetValue(id, out var p) || (pe - p.ponto).sqrMagnitude > ParadoAte * ParadoAte)
                    _parados[id] = (pe, agora);
            }
            if (_parados.Count == _vistosAgora.Count) return;
            _sairam.Clear();
            foreach (var id in _parados.Keys)
                if (!_vistosAgora.Contains(id)) _sairam.Add(id);
            foreach (var id in _sairam) _parados.Remove(id);
        }

        bool NumaFogueira(Vector3 pe)
        {
            for (int i = 0; i < Fogos.Count; i++)
                if (Fogos[i].chave < ChaveDaTocha && DentroDoFogo(Fogos[i], pe)) return true;
            return false;
        }

        /// <summary>O jogador mais parado dentro da fogueira (o índice em Jogadores, ou -1) e há quanto tempo (s).</summary>
        int QuemEstaParado(in Fogo f, float agora, out float tempo)
        {
            tempo = 0f;
            int quem = -1;
            for (int k = 0; k < QuantosJogadores; k++)
            {
                var j = Jogadores[k].jogador;
                if (j == null || !DentroDoFogo(f, Jogadores[k].pes) || !_parados.TryGetValue(j.OwnerClientId, out var p)) continue;
                if (agora - p.desde > tempo)
                {
                    tempo = agora - p.desde;
                    quem = k;
                }
            }
            return quem;
        }

        /// <summary>Ao sair da partida (ZombieManager.OnNetworkDespawn): a cena continua, a próxima partida começa do zero.</summary>
        void EsquecerOLongDaFogueira()
        {
            _parados.Clear();
            _recargaDoLongDaFogueira.Clear();
            _proximoOlharDaFogueira = 0f;
        }

        /// <summary>Os zumbis caçando em volta da fogueira (os Ocos e os Corredores, na beira ou dentro, até 3 m além dela).</summary>
        int ZumbisEmVolta(in Fogo f)
        {
            int n = 0;
            for (int i = 0; i < _ativos.Count; i++)
            {
                var z = _ativos[i];
                if (z.Morto || !z.Engajado || (z.tipo != TipoDeZumbi.Oco && z.tipo != TipoDeZumbi.Corredor)) continue;
                if (DentroDoFogo(f, z.transform.position, EmVoltaDaFogueira)) n++;
            }
            return n;
        }

        // um Long que ainda cospe perto (o sem papo não apaga mais nada: não segura outro)
        bool LongPerto(Vector3 ponto, float raio)
        {
            for (int i = 0; i < _ativos.Count; i++)
            {
                var z = _ativos[i];
                if (z.LongQueCospe && (z.transform.position - ponto).sqrMagnitude < raio * raio) return true;
            }
            return false;
        }

        /// <summary>
        /// No anfitrião: um Long a 17–24 m da fogueira (dentro do alcance do cuspe), a mais de 15 m de todos os jogadores (a
        /// menos de 14 m ele recua antes de cuspir), no NavMesh, virado para ela. Ele só cospe vendo alguém na faixa dele e a
        /// fogueira: o melhor lugar vê os dois (o fogo e o peito de quem está parado nele); sem esse, um que veja o fogo; sem
        /// nenhum (a fogueira dentro de uma casa), não nasce (tenta de novo no segundo seguinte).
        /// </summary>
        bool NascerLongDaFogueira(in Fogo f, Vector3 peitoDoAlvo)
        {
            var c = ajustes.cuspidor;
            Vector3 lugar = default;
            bool achou = false, veTudo = false;
            for (int t = 0; t < 24 && !veTudo; t++)
            {
                Vector2 r = Random.insideUnitCircle.normalized * Random.Range(17f, 24f);
                if (!NavMesh.SamplePosition(f.centro + new Vector3(r.x, 0f, r.y), out var hit, 3f, NavMesh.AllAreas)) continue;
                Vector3 p = hit.position;
                if (Mathf.Abs(p.y - f.centro.y) > 4f || EmAreaLivre(p) || NaZonaSegura(p) || NoFogo(p)) continue;
                Vector3 d = p - f.centro;
                d.y = 0f;
                if (d.magnitude > c.alcanceDoCuspe - 1f) continue;
                bool perto = false;
                for (int k = 0; k < QuantosJogadores && !perto; k++) perto = (Jogadores[k].pes - p).sqrMagnitude < 15f * 15f;
                if (perto) continue;
                Vector3 boca = p + Vector3.up * 1.6f;
                if (!Enxerga(boca, f.centro + Vector3.up * 0.6f)) continue;   // de onde não vê o fogo, não adianta
                veTudo = Enxerga(boca, peitoDoAlvo);
                if (achou && !veTudo) continue;   // já tem um que vê o fogo; só troca por um que vê os dois
                lugar = p;
                achou = true;
            }
            if (!achou) return false;
            Vector3 para = f.centro - lugar;
            para.y = 0f;
            return NascerEspecial(TipoDeZumbi.Long, lugar, Mathf.Atan2(para.x, para.z) * Mathf.Rad2Deg) != null;
        }

        /// <summary>O F1: um Long para a fogueira acesa mais perto (até 25 m), já, sem esperar a regra.</summary>
        public string LongDaFogueiraAgora(Vector3 perto)
        {
            if (!IsServer) return null;
            int melhor = -1;
            float menor = 25f * 25f;
            for (int i = 0; i < Fogos.Count; i++)
            {
                if (Fogos[i].chave >= ChaveDaTocha) continue;
                float d = (Fogos[i].centro - perto).sqrMagnitude;
                if (d < menor)
                {
                    menor = d;
                    melhor = i;
                }
            }
            if (melhor < 0) return "Nenhuma fogueira acesa a menos de 25 m";
            return NascerLongDaFogueira(Fogos[melhor], perto + Vector3.up * 1.3f) ? "Um Long nasceu perto da fogueira: ele vem apagá-la"
                : "Sem lugar para o Long perto da fogueira (de onde ele a veja)";
        }

        /// <summary>O F1: a regra do Long em cada fogueira acesa perto (os zumbis em volta, há quanto tempo alguém está parado, a recarga).</summary>
        public string ResumoDoLongNaFogueira(Vector3 perto)
        {
            var a = ajustes;
            float agora = Time.time;
            var sb = new StringBuilder();
            for (int i = 0; i < Fogos.Count; i++)
            {
                var f = Fogos[i];
                if (f.chave >= ChaveDaTocha || (f.centro - perto).sqrMagnitude > 40f * 40f) continue;
                QuemEstaParado(f, agora, out float parado);
                sb.Append($"\nLong da fogueira {f.chave}: {ZumbisEmVolta(f)}/{a.zumbisParaOLong} em volta, parado {parado:0}/{a.paradoParaOLong:0} s");
                if (EDia) sb.Append(" (de dia, não)");
                if (_recargaDoLongDaFogueira.TryGetValue(f.chave, out float ate) && agora < ate) sb.Append($" · recarga {ate - agora:0} s");
            }
            return sb.ToString();
        }
    }
}
