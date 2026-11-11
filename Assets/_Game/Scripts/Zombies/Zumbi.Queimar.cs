using UnityEngine;

namespace ZombieCityCleaner.Zombies
{
    /// <summary>
    /// O fogo da etapa 4 do ramo Fogo no zumbi (11/11/2026):
    ///  - <see cref="Queimar"/>: o dano do fogo do molotov (o ZombieManager.Fogo.cs), sem marcar o ferido (o ferido entra no
    ///    fogo: o que queima tem que sair dele) e sem caçar quem jogou;
    ///  - <see cref="FugirEmPanico"/>: dentro do fogo de quem tem a Muralha de fogo, ele foge mais longe e mais rápido, larga
    ///    quem caçava e grita (no lugar do recuo até a beira).
    /// </summary>
    public partial class Zumbi
    {
        /// <summary>Só no anfitrião: o fogo tira da vida do corpo (a casca de pedra protege). True: morreu agora.</summary>
        internal bool Queimar(float dano, float agora)
        {
            if (Morto || tipo == TipoDeZumbi.Brutamonte || dano <= 0f) return false;
            _danoNoCorpo += dano * Mathf.Lerp(1f, _m.ajustes.danoNaPedra, Pedra);
            if (_danoNoCorpo < VidaDoCorpo) return false;
            Morrer(agora, false, transform.position + Vector3.up, -transform.forward, false);
            return true;
        }

        // a Muralha de fogo: foge em pânico para longe da beira (false: sem para onde ir; ele recua como sempre)
        bool FugirEmPanico(float agora, AjustesDeZumbi a, in ZombieManager.Fogo fogo, Vector3 deFora, float d)
        {
            var longe = fogo;
            longe.raio = fogo.raio + a.distanciaDoPanico;
            float rumo = d > 0.1f ? Mathf.Atan2(deFora.x, deFora.z) : (transform.eulerAngles.y + 180f) * Mathf.Deg2Rad;
            bool sol = false;
            if (!PontoNaBeira(longe, rumo, out var ponto, ref sol) && !PontoNaBeira(longe, rumo + 0.8f, out ponto, ref sol)
                && !PontoNaBeira(longe, rumo - 0.8f, out ponto, ref sol)) return false;
            _antesDaEsquiva = Estado.Parado;   // no fim do pulo, ele não volta a caçar (o FimDaEsquiva larga o alvo)
            _alvo = null;
            _deOndeVeioALuz = fogo.centro + Vector3.up * 1.2f;
            Atual = Estado.Esquivando;
            _golpeEm = 0f;
            _noAlcanceDesde = -1f;
            Ir(ponto, a.velocidadeDoPanico, 0.2f);
            float vel = Mathf.Max(0.5f, a.velocidadeDoPanico * FatorDasPernas * FatorDaPedra);
            _esquivaAte = agora + Vector3.Distance(transform.position, ponto) / vel + 0.4f;
            _proximaEsquiva = agora + 0.3f;
            _naBeiraAte = agora + 3f;
            Reacoes++;
            MostrarReacao();
            Gemer(1.25f);
            PanicosNoTeste++;
            return true;
        }

        /// <summary>O dano que o corpo já levou (o teste pelo MCP).</summary>
        public float DanoRecebido => _danoNoCorpo;

        /// <summary>Quantas vezes um zumbi fugiu em pânico do fogo (o teste pelo MCP).</summary>
        public static int PanicosNoTeste;
    }
}
