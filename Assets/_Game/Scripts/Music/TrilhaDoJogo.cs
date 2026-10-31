using System;
using UnityEngine;

namespace ZombieCityCleaner.Music
{
    /// <summary>
    /// A trilha sonora (a pedido em 29/10/2026), em Audio/Resources/TrilhaDoJogo.asset (o <see cref="MaestroDaTrilha"/>
    /// carrega pelo nome). São blocos curtos, todos em ré menor, que se encaixam em qualquer ordem: a neblina (o dia), a
    /// noite, o perigo (cada bloco em três camadas: a tensão, a perseguição, a horda), o que liga o perigo ao resto (a
    /// entrada "te viram", a saída e a saída pela pedra), o vento (o silêncio) e os momentos (o amanhecer e a morte).
    /// Os arquivos (Audio/Trilha) saem do Ferramentas/trilha (blocos.py, compor.py e exportar.py), e o asset é refeito
    /// pelo menu Zombie City Cleaner > Trilha > Montar a trilha.
    /// </summary>
    [CreateAssetMenu(menuName = "Zombie City Cleaner/Trilha do jogo", fileName = "TrilhaDoJogo")]
    public class TrilhaDoJogo : ScriptableObject
    {
        [Serializable]
        public class Bloco
        {
            [Tooltip("entrada (abre o trecho), saida (fecha) ou um do meio (na neblina: piano, cordas, cello, vazio; na noite: cello, coro, coracao, vidro).")]
            public string tipo;
            public AudioClip clipe;
        }

        [Serializable]
        public class Familia
        {
            public float bpm;
            [Tooltip("O comprimento de um bloco (s): o próximo começa aqui; o resto do arquivo é a cauda do eco.")]
            public float comprimento;
            public Bloco[] blocos;
        }

        [Serializable]
        public class BlocoDoPerigo
        {
            [Tooltip("As notas graves do bloco: A (ré), B (si bemol, lá) ou C (si bemol, dó). Toca A, depois B ou C, depois A...")]
            public string plano;
            [Tooltip("As três camadas, tocadas juntas: 1 a tensão, 2 a perseguição, 3 a horda.")]
            public AudioClip[] camadas;
        }

        [Serializable]
        public class Rajadas
        {
            [Tooltip("O meio de cada rajada do bloco de vento (s do começo do arquivo): o ambiente bate as janelas ali.")]
            public float[] tempos;
        }

        public Familia neblina = new Familia();
        public Familia noite = new Familia();

        [Header("O perigo")]
        public float bpmDoPerigo = 88f;
        public float comprimentoDoPerigo;
        public BlocoDoPerigo[] perigo;
        [Tooltip("Te viram: dois tempos de piano ao contrário até o golpe, que cai no primeiro tempo do perigo.")]
        public AudioClip[] entradas;
        [Tooltip("Do começo do arquivo da entrada até o golpe (s).")]
        public float encaixeDaEntrada;
        [Tooltip("O perigo acabou.")]
        public AudioClip[] saidas;
        [Tooltip("O perigo acabou porque quem te caçava virou pedra no sol.")]
        public AudioClip[] saidasPelaPedra;

        [Header("O vento (o silêncio)")]
        public AudioClip[] ventoDeDia;
        public AudioClip[] ventoDeNoite;
        [Tooltip("Cada bloco de vento entra e sai devagar: o próximo começa estes segundos antes de o anterior acabar.")]
        public float sobreposicaoDoVento = 4f;
        [Tooltip("As rajadas de cada bloco de vento (na mesma ordem dos blocos).")]
        public Rajadas[] rajadasDoVentoDeDia, rajadasDoVentoDeNoite;

        [Header("O ambiente (a pedido em 29/10/2026; Audio/Ambiente, do Ferramentas/ambiente.py)")]
        [Tooltip("Os grilos, à noite (em laço, estéreo).")]
        public AudioClip[] grilos;
        [Tooltip("Os cachorros longe: latidos e uivos (os uivos no ré menor da trilha).")]
        public AudioClip[] latidos, uivos;
        [Tooltip("O que o vento mexe: a janela, a porta, a placa de metal e a lata rolando.")]
        public AudioClip[] janelas, portas, placas, latas;
        [Tooltip("De dentro dos prédios: a madeira rangendo.")]
        public AudioClip[] madeira;
        [Tooltip("De dia: os corvos.")]
        public AudioClip[] corvos;

        [Header("Os momentos")]
        public AudioClip amanhecer;
        public AudioClip morte;
        [Tooltip("A trilha do menu do jogo (em laço; UI/FundoDoMenu toca).")]
        public AudioClip menu;
    }
}
