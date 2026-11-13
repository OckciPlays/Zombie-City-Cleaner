using System;
using Unity.Netcode;
using UnityEngine;
using ZombieCityCleaner.Loot;
using ZombieCityCleaner.World;

namespace ZombieCityCleaner.Player
{
    /// <summary>O quanto cada roupa vestida está molhada (0 a 100), e a pele onde não há roupa. Escrito pelo anfitrião.</summary>
    public struct RoupasMolhadas : INetworkSerializeByMemcpy, IEquatable<RoupasMolhadas>
    {
        public byte chapeu, camiseta, porCima, calca, sapato, pele;

        public byte De(LugarDaRoupa l) => l switch
        {
            LugarDaRoupa.Chapeu => chapeu, LugarDaRoupa.Camiseta => camiseta, LugarDaRoupa.PorCima => porCima,
            LugarDaRoupa.Calca => calca, LugarDaRoupa.Sapato => sapato, _ => 0,
        };

        public bool Equals(RoupasMolhadas o) => chapeu == o.chapeu && camiseta == o.camiseta && porCima == o.porCima && calca == o.calca
                                               && sapato == o.sapato && pele == o.pele;
        public override bool Equals(object o) => o is RoupasMolhadas r && Equals(r);
        public override int GetHashCode() => HashCode.Combine(chapeu, camiseta, porCima, calca, sapato, pele);
    }

    /// <summary>
    /// A roupa molhada (12/11/2026, a pedido: "deixar que chuvas fortes (tempestades) podem fazer você ficar molhado e te
    /// pesar, fazendo com que você perca um pouco de stamina ... com o tempo, abrigo e fogueira, você se secaria (trocar de
    /// roupa ajudaria)"; "vamos deixar por peça"). Tudo no anfitrião, que sabe da chuva, do teto e do fogo:
    ///  - <b>Por peça:</b> cada roupa que conta (o chapéu, a camiseta, a jaqueta por cima, a calça e o sapato) tem o quanto
    ///    está molhada; onde não há roupa, conta a pele (que seca rápido). Tirar a roupa leva o molhado com ela (no
    ///    <see cref="ItemNaMochila.desgaste"/>, que as roupas não usavam, e no chão também): a jaqueta molhada guardada
    ///    continua molhada, e vestir uma seca no lugar ajuda na hora.
    ///  - <b>Molha:</b> fora de um teto, a chuva (<see cref="ChuvaPorMinuto"/>) e a tempestade (<see cref="TempestadePorMinuto"/>)
    ///    (o chuvisco não molha); a jaqueta segura a camiseta (que molha a um quarto). Entrar na água molha na hora: o sapato
    ///    na poça funda, a calça até a cintura, tudo nadando.
    ///  - <b>Seca:</b> sem molhar, perto do fogo (a fogueira, o calor do fogo: os mesmos da mente) muito rápido, debaixo de
    ///    um teto, ou no sol; na mochila, devagar.
    ///  - <b>Pesa:</b> o <see cref="Molhado01"/> (a média pelo tamanho de cada lugar) faz o vigor gastar mais e cair o máximo
    ///    (<see cref="VigorDoJogador"/>) e vira o símbolo "molhado" ou "encharcado".
    /// </summary>
    public partial class InventarioJogador
    {
        /// <summary>O quanto as roupas vestidas estão molhadas (o anfitrião escreve, de 1 em 1).</summary>
        public readonly NetworkVariable<RoupasMolhadas> Molhado = new(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        /// <summary>Molha (0 a 100 por minuto real): a chuva e a tempestade, fora de um teto.</summary>
        public const float ChuvaPorMinuto = 25f, TempestadePorMinuto = 60f;
        /// <summary>Seca (0 a 100 por minuto real): perto do fogo, debaixo de um teto, no sol (mais a base) e na mochila.</summary>
        public const float SecaNoFogo = 120f, SecaNoTeto = 30f, SecaNoSol = 25f, SecaAoAr = 10f, SecaNaMochila = 10f;
        /// <summary>A pele seca tantas vezes mais rápido que a roupa.</summary>
        public const float PeleSeca = 3f;

        // os lugares que contam e o peso de cada um na média
        static readonly LugarDaRoupa[] LugaresMolhados = { LugarDaRoupa.Chapeu, LugarDaRoupa.Camiseta, LugarDaRoupa.PorCima, LugarDaRoupa.Calca, LugarDaRoupa.Sapato };
        static readonly float[] PesoDoLugar = { 0.05f, 0.25f, 0.3f, 0.28f, 0.12f };

        readonly float[] _molhado = new float[5];
        float _pele, _mochilaSecando, _proximoAr;

        /// <summary>De 0 a 1: o quanto ele está molhado (a média das roupas pelo tamanho; a pele onde não há roupa).</summary>
        public float Molhado01
        {
            get
            {
                var m = Molhado.Value;
                var r = Roupas.Value;
                float Lugar(LugarDaRoupa l, byte v) => r.Peca(l) != Item.Nada ? v : m.pele;
                // o tronco: a jaqueta por cima conta mais que a camiseta embaixo dela
                bool jaqueta = r.Peca(LugarDaRoupa.PorCima) != Item.Nada, camiseta = r.Peca(LugarDaRoupa.Camiseta) != Item.Nada;
                float tronco = jaqueta ? (camiseta ? 0.6f * m.porCima + 0.4f * m.camiseta : m.porCima) : camiseta ? m.camiseta : m.pele;
                float soma = PesoDoLugar[0] * Lugar(LugarDaRoupa.Chapeu, m.chapeu) + (PesoDoLugar[1] + PesoDoLugar[2]) * tronco
                             + PesoDoLugar[3] * Lugar(LugarDaRoupa.Calca, m.calca) + PesoDoLugar[4] * Lugar(LugarDaRoupa.Sapato, m.sapato);
                return Mathf.Clamp01(soma / 100f);
            }
        }

        /// <summary>O quanto a roupa vestida no lugar está molhada (0 a 100), no anfitrião (para tirar e largar).</summary>
        byte MolhadoDe(LugarDaRoupa l)
        {
            int i = Array.IndexOf(LugaresMolhados, l);
            return i < 0 ? (byte)0 : (byte)Mathf.RoundToInt(Mathf.Clamp(_molhado[i], 0f, 100f));
        }

        /// <summary>No anfitrião: a roupa vestida no lugar fica com o molhado (vestir a que veio da mochila ou do chão).</summary>
        void PorMolhado(LugarDaRoupa l, float v)
        {
            int i = Array.IndexOf(LugaresMolhados, l);
            if (i >= 0) _molhado[i] = Mathf.Clamp(v, 0f, 100f);
        }

        /// <summary>Só no anfitrião, a cada quadro (do VidaJogador): a chuva, a água, o teto, o fogo e o sol.</summary>
        internal void AtualizarOMolhado(float dt, uint motivosDaMente, bool sentado)
        {
            if (!IsServer) return;
            var r = Roupas.Value;
            Vector3 pes = transform.position;
            float minuto = dt / 60f;
            // a água: molha na hora o que ficou embaixo dela
            float fundo = Agua.Nivel(pes, out float nivel) ? nivel - pes.y : 0f;
            if (!sentado && fundo > 0.12f)
            {
                _molhado[4] = 100f;   // o sapato
                if (fundo > 0.6f) _molhado[3] = 100f;   // a calça
                if (fundo > 1.05f)
                {
                    _molhado[1] = _molhado[2] = 100f;   // a camiseta e a jaqueta
                    _pele = 100f;
                }
                if (fundo > 1.6f) _molhado[0] = 100f;   // o chapéu (embaixo da água)
            }
            // a chuva, fora de um teto (o chuvisco não molha)
            float chuva = Tempo.Nivel >= NivelDoTempo.Tempestade ? TempestadePorMinuto : Tempo.Nivel == NivelDoTempo.Chuva ? ChuvaPorMinuto : 0f;
            bool teto = Tempo.SobTeto(pes);
            if (chuva > 0f && !teto)
            {
                float k = chuva * minuto;
                bool jaqueta = r.Peca(LugarDaRoupa.PorCima) != Item.Nada;
                _molhado[0] += k;
                _molhado[2] += k;
                _molhado[1] += jaqueta ? k * 0.25f : k;
                _molhado[3] += k * 0.9f;
                _molhado[4] += k * 0.9f;
                bool semRoupa = (r.Peca(LugarDaRoupa.Camiseta) == Item.Nada && !jaqueta) || r.Peca(LugarDaRoupa.Calca) == Item.Nada;
                if (semRoupa) _pele += k;
            }
            else if (fundo <= 0.12f)
            {
                // seca: o fogo, o teto, o sol (e a base ao ar)
                bool fogo = (motivosDaMente & (VidaJogador.Bit(MotivoDaMente.Fogueira) | VidaJogador.Bit(MotivoDaMente.CalorDoFogo))) != 0;
                float s = fogo ? SecaNoFogo : teto ? SecaNoTeto : SecaAoAr + SecaNoSol * Tempo.Sol * (1f - CicloDoDia.Noite);
                for (int i = 0; i < _molhado.Length; i++) _molhado[i] -= s * minuto;
                _pele -= s * PeleSeca * minuto;
            }
            for (int i = 0; i < _molhado.Length; i++)
            {
                // sem roupa no lugar, não há o que molhar
                if (r.Peca(LugaresMolhados[i]) == Item.Nada) _molhado[i] = 0f;
                else _molhado[i] = Mathf.Clamp(_molhado[i], 0f, 100f);
            }
            _pele = Mathf.Clamp(_pele, 0f, 100f);
            var novo = new RoupasMolhadas
            {
                chapeu = (byte)Mathf.RoundToInt(_molhado[0]), camiseta = (byte)Mathf.RoundToInt(_molhado[1]), porCima = (byte)Mathf.RoundToInt(_molhado[2]),
                calca = (byte)Mathf.RoundToInt(_molhado[3]), sapato = (byte)Mathf.RoundToInt(_molhado[4]), pele = (byte)Mathf.RoundToInt(_pele),
            };
            if (!novo.Equals(Molhado.Value)) Molhado.Value = novo;
            // as roupas guardadas na mochila secam devagar (de 10 em 10 s, para não mexer na lista toda hora)
            _mochilaSecando += SecaNaMochila * minuto;
            if (Time.time < _proximoAr || _mochilaSecando < 1f) return;
            _proximoAr = Time.time + 10f;
            int seca = Mathf.FloorToInt(_mochilaSecando);
            _mochilaSecando -= seca;
            for (int k = 0; k < Mochila.Count; k++)
            {
                var it = Mochila[k];
                if (!Itens.EhRoupa(it.item) || it.desgaste == 0) continue;
                it.desgaste = (byte)Mathf.Max(0, it.desgaste - seca);
                Mochila[k] = it;
            }
        }

        /// <summary>Só o teste (no anfitrião): todas as roupas vestidas e a pele com o molhado.</summary>
        public void MolharDeTeste(float v)
        {
            if (!IsServer) return;
            for (int i = 0; i < _molhado.Length; i++) _molhado[i] = v;
            _pele = v;
        }
    }
}
