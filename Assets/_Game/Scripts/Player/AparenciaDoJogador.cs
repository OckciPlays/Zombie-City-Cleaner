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
        // a loja de roupas do Dia 0 (01/11/2026): a peça comprada em cada lugar de LugaresDaLoja (o índice + 1; 0: nada, ou,
        // no tronco, nas pernas e nos pés, a camiseta, a calça e o tênis da criação)
        public byte cabeca, oculos, mascara, tronco, porCima, colete, cintura, pernas, pes, maos;

        public const int LetrasDoNome = 16;

        /// <summary>
        /// Os lugares da loja de roupas do Dia 0 (o lugar do catálogo, o nome para a tela e as peças à venda, na ordem). O
        /// tronco, as pernas e os pés trocam a camiseta, a calça e o tênis da criação; o colete e o "por cima" não vão juntos.
        /// </summary>
        public static readonly (string lugar, string nome, string[] pecas)[] LugaresDaLoja =
        {
            ("Cabeca", "cabeça", new[] { "Bone", "Bone_Militar", "Gorro", "Capacete", "Chapeu_Cauboi", "Capacete_Militar", "Bandana" }),
            ("Oculos", "óculos", new[] { "Oculos_Sol", "Oculos_Aviador", "Oculos_Protecao", "Oculos_Esportivo", "Oculos_Tatico", "Visao_Noturna" }),
            ("Mascara", "rosto", new[] { "Mascara_Cirurgica", "Mascara_Pano", "Bandana_Rosto", "Gola_Tubular", "Respirador", "Mascara_Gas", "Balaclava" }),
            ("Tronco", "camisa", new[] { "Camiseta_V", "Camisa_Xadrez", "Camisa_Social", "Regata", "Camisa_Militar" }),
            ("Por_Cima", "jaqueta", new[] { "Jaqueta_Capuz", "Jaqueta_Lona", "Jaqueta_Couro", "Jaqueta_Esportiva", "Moletom", "Moletom_Ziper" }),
            ("Colete", "colete", new[] { "Colete_Refletivo", "Colete_Tatico" }),
            ("Cintura", "cinto", new[] { "Cinto_Ferramentas", "Cinto_Tatico" }),
            ("Pernas", "calça", new[] { "Calca_Cargo", "Calca_Militar", "Calca_Trabalho", "Jardineira", "Jeans_Rasgado" }),
            ("Pes", "calçado", new[] { "Bota_Trabalho", "Bota_Couro", "Coturno", "Tenis_Baixo", "Bota_Cauboi" }),
            ("Maos", "luvas", new[] { "Luva_Tatica", "Luva_Sem_Dedos" }),
        };

        public const int LugarPorCima = 4, LugarColete = 5;

        /// <summary>A peça comprada no lugar (o índice + 1 em <see cref="LugaresDaLoja"/>; 0: nada).</summary>
        public byte DaLoja(int lugar) => lugar switch
        {
            0 => cabeca, 1 => oculos, 2 => mascara, 3 => tronco, 4 => porCima, 5 => colete, 6 => cintura, 7 => pernas, 8 => pes, 9 => maos, _ => 0,
        };

        /// <summary>A mesma aparência com a peça 'valor' no lugar (o colete tira o "por cima", e vice-versa).</summary>
        public AparenciaDoJogador ComDaLoja(int lugar, byte valor)
        {
            var a = this;
            switch (lugar)
            {
                case 0: a.cabeca = valor; break;
                case 1: a.oculos = valor; break;
                case 2: a.mascara = valor; break;
                case 3: a.tronco = valor; break;
                case 4: a.porCima = valor; if (valor > 0) a.colete = 0; break;
                case 5: a.colete = valor; if (valor > 0) a.porCima = 0; break;
                case 6: a.cintura = valor; break;
                case 7: a.pernas = valor; break;
                case 8: a.pes = valor; break;
                case 9: a.maos = valor; break;
            }
            return a;
        }

        /// <summary>O lugar da loja de uma peça (-1: não está à venda) e o valor dela (o índice + 1).</summary>
        public static int LugarDaPeca(string peca, out byte valor)
        {
            for (int l = 0; l < LugaresDaLoja.Length; l++)
            {
                int i = System.Array.IndexOf(LugaresDaLoja[l].pecas, peca);
                if (i >= 0)
                {
                    valor = (byte)(i + 1);
                    return l;
                }
            }
            valor = 0;
            return -1;
        }

        static string PecaDaLoja(int lugar, byte valor) =>
            valor > 0 && valor <= LugaresDaLoja[lugar].pecas.Length ? LugaresDaLoja[lugar].pecas[valor - 1] : null;

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
            // as compradas na loja do Dia 0 (com as cores do catálogo); o tronco, as pernas e os pés no lugar dos da criação
            var eu = this;
            void DaLojaNoCorpo(int lugar)
            {
                string nome = PecaDaLoja(lugar, eu.DaLoja(lugar));
                var dados = nome != null ? catalogo.DaPeca(nome) : null;
                if (dados != null) pecas.Add(new CatalogoDasRoupas.PecaVestida { nome = nome, cores = (string[])dados.cores?.Clone() });
            }
            if (tronco > 0) DaLojaNoCorpo(3);
            else
            {
                string c = Camisetas[Mathf.Min(camiseta, Camisetas.Length - 1)].cor;
                string costura = "#" + ColorUtility.ToHtmlStringRGB(CatalogoDasRoupas.Cor(c) * 0.9f);
                Por("Camiseta", new[] { c, costura, costura });
            }
            if (pernas > 0) DaLojaNoCorpo(7);
            else
            {
                var calcaEscolhida = Calcas[Mathf.Min(calca, Calcas.Length - 1)];
                Por(calcaEscolhida.peca, (string[])calcaEscolhida.cores.Clone());
            }
            if (pes > 0) DaLojaNoCorpo(8);
            else Por(Tenis, (string[])CoresDoTenis.Clone());
            foreach (int l in new[] { 0, 1, 2, 4, 5, 6, 9 }) DaLojaNoCorpo(l);
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
            barba == o.barba && camiseta == o.camiseta && calca == o.calca && definida == o.definida &&
            cabeca == o.cabeca && oculos == o.oculos && mascara == o.mascara && tronco == o.tronco && porCima == o.porCima &&
            colete == o.colete && cintura == o.cintura && pernas == o.pernas && pes == o.pes && maos == o.maos;

        public override bool Equals(object o) => o is AparenciaDoJogador a && Equals(a);
        public override int GetHashCode() => System.HashCode.Combine(System.HashCode.Combine(nome, mulher, pele, cabelo, corDosPelos, barba, camiseta, calca),
                                                                     System.HashCode.Combine(cabeca, oculos, mascara, tronco, porCima, colete, cintura, pernas),
                                                                     pes, maos);
    }
}
