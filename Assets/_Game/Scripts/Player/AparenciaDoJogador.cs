using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using ZombieCityCleaner.Roupas;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// A aparência do jogador (o Plano_Personagem.md da pasta de arte, 31/10/2026): o nome e o que ele escolheu ao criar o
    /// personagem (o sexo, a cor da pele, o cabelo, a barba, a cor dos dois, a camiseta e a calça; o tênis de cano é o
    /// de todos). Vai pela rede (<see cref="JogadorRede.Aparencia"/>, escrita pelo dono quando ele entra), em uns 40
    /// bytes: cada escolha é o índice numa das listas abaixo, as mesmas em todas as máquinas. As roupas achadas no mapa
    /// vão pelo inventário (a parte 4 do plano).
    /// </summary>
    public struct AparenciaDoJogador : INetworkSerializeByMemcpy, System.IEquatable<AparenciaDoJogador>
    {
        public FixedString32Bytes nome;
        public byte mulher;         // 0 o homem, 1 a mulher
        public byte pele;           // o índice nos tons de pele do catálogo das roupas
        public byte cabelo;         // o índice em Cabelos (0: careca)
        public byte corDosPelos;    // o índice em CoresDosPelos (o cabelo, a barba e as sobrancelhas)
        public byte barba;          // o índice em Barbas (0: sem; só no homem)
        public byte camiseta;       // o índice em Camisetas
        public byte calca;          // o índice em Calcas
        public byte definida;       // 1 depois que o dono escreveu (antes, o corpo fica sem roupa)

        public const int LetrasDoNome = 16;

        /// <summary>Os cabelos: todos servem nos dois corpos (a cabeça é a mesma).</summary>
        public static readonly string[] Cabelos =
            { "", "Cabelo_Curto", "Cabelo_Penteado", "Cabelo_Para_Tras", "Cabelo_Cacheado", "Cabelo_Rabo", "Cabelo_Rabo_Baixo", "Cabelo_Coque" };
        public static readonly string[] Barbas = { "", "Barba_Curta", "Barba_Cheia", "Bigode", "Cavanhaque" };
        public static readonly (string nome, string cor)[] CoresDosPelos =
        {
            ("preto", "#151313"), ("castanho-escuro", "#2B1E16"), ("castanho", "#4A3020"), ("castanho-claro", "#7A5A3A"),
            ("ruivo", "#7A3B1E"), ("loiro", "#B8945A"), ("grisalho", "#8C8A86"), ("branco", "#D2CEC6"),
        };
        /// <summary>A camiseta simples, nas 8 cores do começo (a cor e as costuras um pouco mais escuras).</summary>
        public static readonly (string nome, string cor)[] Camisetas =
        {
            ("branca", "#D9D6CE"), ("preta", "#2B2B2E"), ("cinza", "#8A8A88"), ("vermelha", "#8E2B26"),
            ("verde", "#4F6146"), ("azul-marinho", "#2E3D5C"), ("bege", "#CDBF9F"), ("vinho", "#6B2A2A"),
        };
        /// <summary>A calça do começo: o jeans em 3 lavagens e a cargo em 4 cores.</summary>
        public static readonly (string nome, string peca, string[] cores)[] Calcas =
        {
            ("jeans claro", "Jeans", new[] { "#6E8BB0", "#B5803C", "#B5803C" }),
            ("jeans", "Jeans", new[] { "#3F5A7E", "#B5803C", "#B5803C" }),
            ("jeans escuro", "Jeans", new[] { "#2A3346", "#8E8E86", "#8E8E86" }),
            ("cargo preta", "Calca_Cargo", new[] { "#2B2B2E", "#232326", "#3E3E42" }),
            ("cargo marrom", "Calca_Cargo", new[] { "#5C4636", "#4E3A2E", "#6E5844" }),
            ("cargo cáqui", "Calca_Cargo", new[] { "#7A6E54", "#685E48", "#8E8266" }),
            ("cargo verde-oliva", "Calca_Cargo", new[] { "#4E5338", "#424630", "#5E6446" }),
        };
        /// <summary>O calçado de todos: o tênis de cano (o All Star), preto.</summary>
        public const string Tenis = "Tenis_Cano";
        static readonly string[] CoresDoTenis = { "#1E1E20", "#E6E3DC", "#E6E3DC" };

        public bool EhMulher => mulher != 0;
        public string Corpo => EhMulher ? SorteioDosNPCs.Mulher : CatalogoDasRoupas.Homem;
        public string CorDosPelos => CoresDosPelos[Mathf.Min(corDosPelos, CoresDosPelos.Length - 1)].cor;

        /// <summary>O tom de pele (hex).</summary>
        public string Pele(CatalogoDasRoupas catalogo) =>
            catalogo.peles != null && catalogo.peles.Length > 0 ? catalogo.peles[Mathf.Min(pele, catalogo.peles.Length - 1)] : "#D08A63";

        /// <summary>As peças vestidas pela aparência (o cabelo, a barba, os cílios da mulher, a camiseta, a calça e o tênis).</summary>
        public List<CatalogoDasRoupas.PecaVestida> Pecas(CatalogoDasRoupas catalogo)
        {
            var pecas = new List<CatalogoDasRoupas.PecaVestida>();
            string pelos = CorDosPelos;
            void Por(string nome, string[] cores)
            {
                var dados = catalogo.DaPeca(nome);
                if (dados == null) return;
                pecas.Add(new CatalogoDasRoupas.PecaVestida { nome = nome, cores = cores ?? SorteioDosNPCs.CoresDoPelo(dados, pelos) });
            }
            if (cabelo > 0 && cabelo < Cabelos.Length) Por(Cabelos[cabelo], null);
            if (!EhMulher && barba > 0 && barba < Barbas.Length) Por(Barbas[barba], null);
            if (EhMulher) Por("Cilios", null);
            string c = Camisetas[Mathf.Min(camiseta, Camisetas.Length - 1)].cor;
            string costura = "#" + ColorUtility.ToHtmlStringRGB(CatalogoDasRoupas.Cor(c) * 0.9f);
            Por("Camiseta", new[] { c, costura, costura });
            var calcaEscolhida = Calcas[Mathf.Min(calca, Calcas.Length - 1)];
            Por(calcaEscolhida.peca, (string[])calcaEscolhida.cores.Clone());
            Por(Tenis, (string[])CoresDoTenis.Clone());
            return pecas;
        }

        /// <summary>Uma aparência ao acaso (o "aleatório" da tela de criar; e, antes dela, a de quem entra).</summary>
        public static AparenciaDoJogador Sortear(System.Random r, string nome, int tonsDePele = 6)
        {
            var a = new AparenciaDoJogador { nome = new FixedString32Bytes(Cortar(nome)), definida = 1 };
            a.mulher = (byte)(r.NextDouble() < 0.5 ? 1 : 0);
            a.pele = (byte)r.Next(Mathf.Max(1, tonsDePele));
            // o cabelo do sexo, quase sempre (o careca, às vezes, no homem); a cor sem os grisalhos, quase sempre
            a.cabelo = (byte)(a.EhMulher ? 5 + r.Next(3) : r.NextDouble() < 0.12 ? 0 : 1 + r.Next(4));
            a.corDosPelos = (byte)(r.NextDouble() < 0.9 ? r.Next(6) : 6 + r.Next(2));
            a.barba = (byte)(!a.EhMulher && r.NextDouble() < 0.5 ? 1 + r.Next(Barbas.Length - 1) : 0);
            a.camiseta = (byte)r.Next(Camisetas.Length);
            a.calca = (byte)r.Next(Calcas.Length);
            return a;
        }

        /// <summary>O nome sem espaços nas pontas e com até <see cref="LetrasDoNome"/> letras (e que caiba nos 29 bytes).</summary>
        public static string Cortar(string nome)
        {
            nome = (nome ?? "").Trim();
            if (nome.Length > LetrasDoNome) nome = nome.Substring(0, LetrasDoNome).TrimEnd();
            while (System.Text.Encoding.UTF8.GetByteCount(nome) > FixedString32Bytes.UTF8MaxLengthInBytes) nome = nome.Substring(0, nome.Length - 1);
            return nome;
        }

        public bool Equals(AparenciaDoJogador o) =>
            nome.Equals(o.nome) && mulher == o.mulher && pele == o.pele && cabelo == o.cabelo && corDosPelos == o.corDosPelos &&
            barba == o.barba && camiseta == o.camiseta && calca == o.calca && definida == o.definida;

        public override bool Equals(object o) => o is AparenciaDoJogador a && Equals(a);
        public override int GetHashCode() => System.HashCode.Combine(nome, mulher, pele, cabelo, corDosPelos, barba, camiseta, calca);
    }
}
