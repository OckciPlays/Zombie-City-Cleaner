using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZombieCityCleaner.Roupas
{
    /// <summary>
    /// O catálogo das roupas (o Plano_Roupas.md da pasta de arte, 30/10/2026): o <c>Art/Characters/Roupas/Roupas.json</c>,
    /// escrito pelo <c>roupas_exportar.py</c> do <c>Roupas.blend</c> junto com o <c>Roupas.fbx</c>. Cada peça (uma roupa, um
    /// cabelo, um acessório) tem o lugar no corpo, a camada (a de cima cobre a de baixo), a camada da textura nas
    /// Texture2DArray das roupas, as cores do começo (hex, as partes 1, 2 e 3) e as faces que ela esconde de cada um dos
    /// outros (o <c>NPC_Corpo</c> e as peças de camada menor), em faixas ("0-12 15 20-31"). Os NPCs prontos são o corpo
    /// (o do homem ou uma das formas, a "Mulher": a blend shape do corpo e de cada peça), o tom de pele e as peças e as
    /// cores de cada um, e o tipo dele. Os tipos são o sorteio (a etapa 4: o sobrevivente, o civil, o trabalhador, o
    /// policial, o militar, o médico, o fazendeiro, o idoso e o estudante; quem sorteia é o <see cref="SorteioDosNPCs"/>).
    /// Os tons de pele, a cor da cueca e a das sobrancelhas sem pelos vêm junto.
    /// </summary>
    [Serializable]
    public class CatalogoDasRoupas
    {
        [Serializable]
        public class Esconde
        {
            public string alvo;     // o nome do objeto (NPC_Corpo ou Roupa_<nome>)
            public string faces;
        }

        [Serializable]
        public class Peca
        {
            public string nome;
            public string lugar;
            public int camada;
            public int textura;
            public bool pelos;      // o cabelo e as barbas: as sobrancelhas pegam a cor deles
            public bool cobre_cabelo;   // os chapéus: com eles, os cabelos grandes viram a versão de baixo do chapéu
            public string sob_chapeu;   // o cabelo: o nome da versão dele de baixo do chapéu (vazio: ele mesmo serve)
            public string de;           // a versão de baixo do chapéu: o cabelo de que ela é (não entra nas trocas)
            public int triangulos;
            public string[] cores;
            public Esconde[] esconde;
        }

        [Serializable]
        public class PecaVestida
        {
            public string nome;
            public string[] cores;
        }

        [Serializable]
        public class Npc
        {
            public string nome;
            public string tipo;     // o tipo do sorteio de que ele é (o "Policial", o "Civil"...; vazio: nenhum)
            public string corpo;    // "Homem" ou o nome da forma ("Mulher")
            public string pele;     // hex
            public PecaVestida[] pecas;
        }

        // ---- o sorteio por tipo (o SorteioDosNPCs)

        [Serializable]
        public class Opcao
        {
            public string peca;     // vazio: nenhuma (o careca)
            public float peso = 1f;
            public string corpo;    // só nesse corpo ("Homem" ou "Mulher"; vazio: nos dois)
            public string esquema;  // só nesse esquema de cor (vazio: em todos)
            public string[] cores;  // as receitas de cor ("c1 c2 c3"), uma sorteada (nenhuma: as do começo da peça)
            public string[] sem;    // os lugares que precisam estar vazios para ela sair
        }

        [Serializable]
        public class LugarDoTipo
        {
            public string lugar;
            public float chance;    // a chance de ter uma peça nesse lugar
            public Opcao[] opcoes;
        }

        [Serializable]
        public class Esquema
        {
            public string nome;
            public float peso = 1f;
        }

        [Serializable]
        public class Tipo
        {
            public string nome;
            public float mulher;        // a chance de ser mulher
            public int[] peles;         // os tons de pele que ele pode ter (os índices em peles)
            public string[] cabelos;    // as cores dos pelos (o cabelo, a barba e as sobrancelhas: uma por NPC)
            public Esquema[] esquemas;  // os esquemas de cor (um por NPC, para as peças combinarem; nenhum: sem esquema)
            public LugarDoTipo[] lugares;   // na ordem do sorteio
        }

        public const string Homem = "Homem";

        public Peca[] pecas;
        public Npc[] npcs;
        public Tipo[] tipos;
        public int colunas_da_tira; // as texturas das peças numa grade (tantas por linha, a 0 em cima à esquerda; 0: uma)
        public string[] peles;      // os tons de pele (hex), do mais claro ao mais escuro
        public string[] formas;     // as blend shapes dos corpos ("Mulher")
        public string cueca;
        public string sobrancelha;

        public static CatalogoDasRoupas Ler(TextAsset json) => JsonUtility.FromJson<CatalogoDasRoupas>(json.text);

        public Peca DaPeca(string nome) => Array.Find(pecas, p => p.nome == nome);

        public Npc DoNpc(string nome) => Array.Find(npcs, n => n.nome == nome);

        public Tipo DoTipo(string nome) => tipos == null ? null : Array.Find(tipos, t => t.nome == nome);

        /// <summary>"0-2 5 7-8" -> {0, 1, 2, 5, 7, 8}.</summary>
        public static void Faces(string faixas, HashSet<int> em)
        {
            if (string.IsNullOrEmpty(faixas)) return;
            foreach (var parte in faixas.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                int traco = parte.IndexOf('-');
                int a = int.Parse(traco < 0 ? parte : parte.Substring(0, traco));
                int b = traco < 0 ? a : int.Parse(parte.Substring(traco + 1));
                for (int f = a; f <= b; f++) em.Add(f);
            }
        }

        public static Color Cor(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.gray;
    }
}
