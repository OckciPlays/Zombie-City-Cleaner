using System.Collections.Generic;
using UnityEngine;
using ZombieCityCleaner.Player;

namespace ZombieCityCleaner.DiaZero
{
    /// <summary>
    /// O carrinho de quem joga nesta máquina (o Dia 0): os produtos postos nele e as roupas provadas, de uma loja só.
    /// Sair da loja sem pagar devolve tudo (as roupas provadas também saem do corpo); pagar no caixa
    /// (<see cref="DiaZeroDaPartida.Pagar"/>) manda o pedido ao anfitrião, que confere o dinheiro pela tabela.
    /// </summary>
    public static class Carrinho
    {
        public static LojaDoDiaZero Loja { get; private set; }
        /// <summary>Os produtos (o índice na tabela da loja), na ordem em que entraram.</summary>
        public static readonly List<int> Produtos = new List<int>();
        static AparenciaDoJogador? _antes;   // como ele estava antes de provar a primeira roupa nesta loja

        /// <summary>O aviso curto embaixo da mira ("lata de comida no carrinho") e até quando.</summary>
        public static string Aviso { get; private set; }
        public static float AvisoAte { get; private set; }

        public static void Avisar(string texto, float segundos = 3f)
        {
            Aviso = texto;
            AvisoAte = Time.time + segundos;
        }

        /// <summary>As roupas provadas que ainda não foram pagas: o lugar e a peça (a diferença para o antes de provar).</summary>
        public static List<(int lugar, byte valor)> Roupas()
        {
            var r = new List<(int, byte)>();
            var j = JogadorRede.Local;
            if (j == null || !_antes.HasValue) return r;
            var a = j.Aparencia.Value;
            for (int l = 0; l < AparenciaDoJogador.LugaresDaLoja.Length; l++)
            {
                byte v = a.DaLoja(l);
                if (v > 0 && v != _antes.Value.DaLoja(l)) r.Add((l, v));
            }
            return r;
        }

        public static int Quantos => Produtos.Count + Roupas().Count;

        public static int Total
        {
            get
            {
                if (Loja == null) return 0;
                int t = 0;
                foreach (int i in Produtos) t += PrecosDoDiaZero.DaLoja(Loja.Tipo, i)?.preco ?? 0;
                foreach (var (l, v) in Roupas()) t += PrecosDoDiaZero.PrecoDaRoupa(l, v);
                return t;
            }
        }

        static void NaLoja(LojaDoDiaZero loja)
        {
            if (Loja == loja) return;
            Esvaziar(true);
            Loja = loja;
        }

        /// <summary>Um produto no carrinho.</summary>
        public static void Por(LojaDoDiaZero loja, int indice)
        {
            var p = PrecosDoDiaZero.DaLoja(loja.Tipo, indice);
            if (!p.HasValue) return;
            NaLoja(loja);
            Produtos.Add(indice);
            int quantos = 0;
            foreach (int i in Produtos) if (i == indice) quantos++;
            Avisar($"{Primeira(p.Value.Nome)} no carrinho{(quantos > 1 ? $" ({quantos})" : "")}  ·  total {PrecosDoDiaZero.Dinheiro(Total)}");
        }

        /// <summary>Tira um do produto (a tela do caixa).</summary>
        public static void Tirar(int indice)
        {
            int k = Produtos.LastIndexOf(indice);
            if (k >= 0) Produtos.RemoveAt(k);
        }

        /// <summary>Prova a peça (ou tira, se já está nela): a aparência muda na hora, para todos.</summary>
        public static void Provar(LojaDoDiaZero loja, int lugar, byte valor)
        {
            var j = JogadorRede.Local;
            if (j == null || !j.Criado) return;
            NaLoja(loja);
            var a = j.Aparencia.Value;
            if (!_antes.HasValue) _antes = a;
            string peca = AparenciaDoJogador.LugaresDaLoja[lugar].pecas[valor - 1];
            if (a.DaLoja(lugar) == valor)
            {
                j.Aparencia.Value = a.ComDaLoja(lugar, _antes.Value.DaLoja(lugar) == valor ? (byte)0 : _antes.Value.DaLoja(lugar));
                Avisar($"Tirou {Nome(peca)}.");
            }
            else
            {
                j.Aparencia.Value = a.ComDaLoja(lugar, valor);
                Avisar($"Provando {Nome(peca)} ({PrecosDoDiaZero.Dinheiro(PrecosDoDiaZero.PrecoDaRoupa(peca))})  ·  V: ver de fora  ·  pague no caixa");
            }
        }

        /// <summary>Tira a peça provada do lugar (a tela do caixa): volta a de antes.</summary>
        public static void Desprovar(int lugar)
        {
            var j = JogadorRede.Local;
            if (j == null || !_antes.HasValue) return;
            j.Aparencia.Value = j.Aparencia.Value.ComDaLoja(lugar, _antes.Value.DaLoja(lugar));
        }

        /// <summary>Esvazia o carrinho (as roupas provadas saem do corpo, se 'devolver').</summary>
        public static void Esvaziar(bool devolver)
        {
            var j = JogadorRede.Local;
            if (devolver && _antes.HasValue && j != null && j.IsOwner)
            {
                var a = _antes.Value;
                a.definida = 1;
                j.Aparencia.Value = a;
            }
            Produtos.Clear();
            _antes = null;
            Loja = null;
        }

        /// <summary>O anfitrião aceitou o pagamento: as roupas ficam, o carrinho esvazia.</summary>
        public static void Pago()
        {
            Produtos.Clear();
            _antes = null;
        }

        /// <summary>O nome de uma peça para o texto ("Capacete_Militar" → "capacete militar").</summary>
        public static string Nome(string peca) => peca.Replace('_', ' ').ToLowerInvariant()
            .Replace("bone", "boné").Replace("oculos", "óculos").Replace("mascara", "máscara").Replace("calca", "calça")
            .Replace("chapeu cauboi", "chapéu de caubói").Replace("protecao", "proteção").Replace("tatico", "tático")
            .Replace("cirurgica", "cirúrgica").Replace("visao noturna", "visão noturna").Replace("mascara gas", "máscara de gás")
            .Replace("máscara gas", "máscara de gás").Replace("cauboi", "caubói").Replace("ziper", "zíper").Replace("sem dedos", "sem dedos");

        public static string Primeira(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0]) + s.Substring(1);
    }
}
