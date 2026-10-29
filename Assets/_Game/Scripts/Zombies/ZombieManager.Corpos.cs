using System.Collections.Generic;
using UnityEngine;

namespace ZombieCityCleaner.Zombies
{
    /// <summary>
    /// Os corpos que caem (ragdoll), em todas as máquinas. Cada modelo tem um pool de corpos com física, criados no começo
    /// da partida. O zumbi morto some e um corpo do pool dele cai no lugar; depois de um tempo a física para (o corpo fica
    /// parado no chão), ele afunda devagar (a neblina e o escuro escondem) e volta ao pool. Com todos em uso, o corpo mais
    /// antigo daquele modelo que está fora da tela é reaproveitado (se todos estão na tela, o mais antigo).
    /// </summary>
    public partial class ZombieManager
    {
        [Header("Corpos (ragdoll)")]
        [Tooltip("Corpos de cada modelo, criados no começo da partida.")]
        public int corposPorModelo = 12;
        [Tooltip("Tempo até a física do corpo parar (s): depois ele fica parado no lugar.")]
        public float tempoAteAssentar = 6f;
        [Tooltip("Tempo que o corpo fica no chão antes de afundar e voltar ao pool (s).")]
        public float tempoDoCorpo = 15f;
        [Tooltip("O empurrão do tiro na parte do corpo atingida (N·s).")]
        public float empurraoDoTiro = 30f;
        [Tooltip("A espingarda de perto joga o corpo inteiro: a velocidade para trás e para cima (m/s).")]
        public Vector2 arremesso = new Vector2(4.5f, 2.2f);

        List<CorpoDeZumbi>[] _corpos;

        void PrepararCorpos()
        {
            if (_corpos != null || corpos == null) return;
            var pasta = new GameObject("Corpos").transform;
            _corpos = new List<CorpoDeZumbi>[corpos.Length];
            for (int m = 0; m < corpos.Length; m++)
            {
                _corpos[m] = new List<CorpoDeZumbi>();
                for (int i = 0; i < corposPorModelo; i++)
                {
                    var c = Instantiate(corpos[m], pasta);
                    c.Guardar();
                    _corpos[m].Add(c);
                }
            }
        }

        /// <summary>
        /// Um corpo do modelo deste zumbi cai no lugar dele, com a velocidade dele e o empurrão do tiro. Arremessado (a
        /// espingarda de perto), o corpo inteiro voa para trás.
        /// </summary>
        internal void Cair(Zumbi z, Vector3 velocidade, bool semCabeca, int parte, Vector3 direcao, bool arremessado, byte cortes)
        {
            if (_corpos == null || z.modelo >= _corpos.Length) return;
            var lista = _corpos[z.modelo];
            CorpoDeZumbi livre = null;
            foreach (var c in lista)
                if (!c.emUso)
                {
                    livre = c;
                    break;
                }
            if (livre == null)
            {
                // todos em uso: some o mais antigo, de preferência um que ninguém está vendo aqui
                foreach (var c in lista)
                    if (!c.pele.isVisible && (livre == null || c.caiuEm < livre.caiuEm)) livre = c;
                if (livre == null)
                {
                    livre = lista[0];
                    foreach (var c in lista)
                        if (c.caiuEm < livre.caiuEm) livre = c;
                }
                livre.Guardar();
            }
            // um pouco para cima: o tiro tira o pé do chão, e o corpo não arrasta
            Vector3 empurrao = (direcao.normalized + Vector3.up * 0.15f) * empurraoDoTiro;
            if (arremessado) velocidade = direcao.normalized * arremesso.x + Vector3.up * arremesso.y;
            livre.Cair(z.raizDoModelo, z.esqueleto, velocidade, semCabeca, parte, empurrao, cortes, z.MaterialDaPele, z.Pedra);
        }

        void AtualizarCorpos(float agora, float dt)
        {
            if (_corpos == null) return;
            foreach (var lista in _corpos)
                foreach (var c in lista)
                {
                    if (!c.emUso) continue;
                    float t = agora - c.caiuEm;
                    if (t > tempoDoCorpo + 2.5f) c.Guardar();
                    else if (t > tempoDoCorpo) c.Afundar(0.25f * dt);
                    else if (t > tempoAteAssentar) c.Assentar();
                }
        }

        void GuardarCorpos()
        {
            if (_corpos == null) return;
            foreach (var lista in _corpos)
                foreach (var c in lista) c.Guardar();
        }
    }
}
