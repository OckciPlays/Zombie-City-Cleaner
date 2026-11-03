using Unity.Netcode;
using UnityEngine;
using ZombieCityCleaner.Combat;
using ZombieCityCleaner.Loot;
using ZombieCityCleaner.Player;
using ZombieCityCleaner.Zombies;

namespace ZombieCityCleaner.Sobreviventes
{
    /// <summary>
    /// O sobrevivente de arma branca (03/11/2026, a pedido: os civis das cidades "com armas brancas, tacos, facas,
    /// pistolas, raramente uma submetralhadora"): a faca, o machado, o bastão com pregos ou a lança, na mão do corpo, com o
    /// controlador do item (o mesmo do corpo dos outros jogadores) e o golpe no "Fire".
    /// No anfitrião, no combate e contra os zumbis, ele chega perto (de longe, pelo lado, para não ir reto na mira) e golpeia
    /// a menos do alcance da arma; o dano sai no impacto da animação, se o alvo ainda estiver perto: o da arma
    /// (<see cref="Itens.Branca"/>), 70% no jogador (como o tiro) e uma vez e meia no zumbi. Testado em 03/11/2026: o
    /// golpe sai na mão (a foto do machado), e o saque de 5 mortos deu 2 armas e 3 balas.
    /// </summary>
    public partial class Sobrevivente
    {
        float _proximoGolpe, _golpeEm = -1f, _ladoDoAtaque = 1f;
        JogadorRede _golpeNoJogador;
        Zumbi _golpeNoZumbi;

        /// <summary>Ele luta com uma arma branca (em qualquer máquina, pela identidade).</summary>
        public bool ComBranca => Itens.EhBranca(Identidade.Value.branca);

        DadosDaBranca Branca => Itens.Branca(Identidade.Value.branca);

        // o controlador do corpo com o item na mão (a faca, o machado...), como o dos outros jogadores (JogadorRede)
        RuntimeAnimatorController ControladorDoItem(Item item)
        {
            int k = Itens.MaoDe(item) - Armas.Quantas;
            var itens = corpo.controladoresDosItens;
            return itens != null && k >= 0 && k < itens.Length && itens[k] != null ? itens[k]
                : corpo.controladorDasMaos != null ? corpo.controladorDasMaos : corpo.controladores[0];
        }

        /// <summary>Anfitrião → todos: o golpe (a animação e o vento da arma).</summary>
        [Rpc(SendTo.Everyone)]
        void GolpeRpc()
        {
            if (corpo != null && corpo.animador != null && !_caido) corpo.animador.SetTrigger(FireId);
            Efeitos.Som(Efeitos.SocoVento, transform.position + Vector3.up * 1.4f, 0.6f, true, 18f);
        }

        /// <summary>Anfitrião → todos: o golpe pegou (o som e o sangue).</summary>
        [Rpc(SendTo.Everyone)]
        void GolpeAcertouRpc(Vector3 ponto, Vector3 normal)
        {
            Efeitos.Som(Efeitos.SocoCarne, ponto, 0.85f, true, 20f);
            Sangue.Respingo(ponto, normal);
        }

        // no anfitrião: chega perto do ponto e golpeia o jogador (o inimigo do grupo)
        void BrigarDePerto(IAlvoDeZumbi inimigo, Vector3 onde, bool vejo, float agora)
        {
            var d = Branca;
            Vector3 para = onde - transform.position;
            para.y = 0f;
            float dist = para.magnitude;
            _mirandoEm = onde + Vector3.up * 1.2f;   // vira para ele (o Mirar)
            if (!vejo || dist > d.alcance + 0.35f)
            {
                if (agora >= _proximoCaminho)
                {
                    _proximoCaminho = agora + 0.35f;
                    if (Random.value < 0.08f) _ladoDoAtaque = -_ladoDoAtaque;
                    // de longe, pelo lado (contorna a mira de quem atira); perto, reto
                    Vector3 lado = dist > 7f ? Vector3.Cross(Vector3.up, para.normalized) * (_ladoDoAtaque * Mathf.Min(4f, dist * 0.3f)) : Vector3.zero;
                    Ir(onde + lado, dist > 4f ? 3.9f : 2.8f, Mathf.Max(0.3f, d.alcance * 0.55f));
                }
                return;
            }
            Parar();
            if (agora < _proximoGolpe || _golpeEm >= 0f || Vector3.Angle(transform.forward, para) > 35f) return;
            // (03/11/2026, no teste: dois de faca e machado tiravam ~30 de vida por segundo; agora ~10 cada)
            _proximoGolpe = agora + d.intervalo * 1.6f + Random.Range(0.2f, 0.6f);
            _golpeEm = agora + Mathf.Max(0.15f, d.impacto);
            _golpeNoJogador = inimigo as JogadorRede;
            _golpeNoZumbi = null;
            GolpeRpc();
        }

        // no anfitrião: o zumbi que ameaça (perto) leva o golpe; um pouco mais longe, ele vai até o zumbi
        void BrigarComZumbi(float agora)
        {
            var d = Branca;
            Vector3 onde = _zumbi.transform.position;
            Vector3 para = onde - transform.position;
            para.y = 0f;
            float dist = para.magnitude;
            _mirandoEm = onde + Vector3.up * 1.2f;
            if (dist > d.alcance + 0.3f)
            {
                if (agora >= _proximoCaminho)
                {
                    _proximoCaminho = agora + 0.3f;
                    Ir(onde, 3.4f, Mathf.Max(0.3f, d.alcance * 0.55f));
                }
                return;
            }
            Parar();
            if (agora < _proximoGolpe || _golpeEm >= 0f || Vector3.Angle(transform.forward, para) > 40f) return;
            _proximoGolpe = agora + d.intervalo * 1.3f + Random.Range(0f, 0.25f);
            _golpeEm = agora + Mathf.Max(0.15f, d.impacto);
            _golpeNoZumbi = _zumbi;
            _golpeNoJogador = null;
            GolpeRpc();
            if (Random.value < 0.05f) Falar(Frase.Zumbis);
        }

        // no anfitrião, a cada quadro: o golpe começado chega no impacto
        void ImpactoDoGolpe(float agora)
        {
            if (_golpeEm < 0f || agora < _golpeEm) return;
            _golpeEm = -1f;
            var d = Branca;
            if (d == null || _morto.Value) return;
            float alcance = d.alcance + 0.6f;
            Vector3 boca = transform.position + Vector3.up * 1.3f;
            if (_golpeNoJogador != null && _golpeNoJogador.AlvoValido)
            {
                var j = _golpeNoJogador;
                if ((j.transform.position - transform.position).sqrMagnitude <= alcance * alcance)
                {
                    j.Vida?.Dano(d.dano * DanoNoJogador, boca, $"Golpeado com {Itens.NomeComArtigo(Identidade.Value.branca)} de um sobrevivente.");
                    var ponto = j.transform.position + Vector3.up * 1.2f;
                    GolpeAcertouRpc(ponto, (boca - ponto).normalized);
                }
            }
            else if (_golpeNoZumbi != null && !_golpeNoZumbi.Morto)
            {
                var z = _golpeNoZumbi;
                if ((z.transform.position - transform.position).sqrMagnitude <= alcance * alcance)
                {
                    var ponto = z.transform.position + Vector3.up * 1.2f;
                    ZombieManager.Acertar(z.Id, z.Geracao, RegiaoDoCorpo.Tronco, Membro.Nenhum, d.dano * 1.5f, ponto, false, this, d.corte);
                    GolpeAcertouRpc(ponto, (boca - ponto).normalized);
                }
            }
            _golpeNoJogador = null;
            _golpeNoZumbi = null;
        }
    }
}
