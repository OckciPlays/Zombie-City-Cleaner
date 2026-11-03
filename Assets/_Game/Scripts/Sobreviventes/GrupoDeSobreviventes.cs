using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using ZombieCityCleaner.Player;
using ZombieCityCleaner.Zombies;

namespace ZombieCityCleaner.Sobreviventes
{
    public enum EstadoDoGrupo : byte { Explorando, Combate }

    /// <summary>
    /// Um grupo de busca (no anfitrião): os membros, o líder (o primeiro vivo), o que ele está fazendo e o inimigo que
    /// todos conhecem (quem um deles viu, ouviu atirando ou o acertou). Explorando, o líder escolhe um lugar da cidade e os
    /// outros vão atrás, em formação; em combate, cada um se protege e atira (Sobrevivente.Cabeca.cs). Sem ver o inimigo
    /// por 30 s, voltam a explorar. Sobrando um só, ele foge.
    /// </summary>
    public class GrupoDeSobreviventes
    {
        public int cidade, geracao;
        /// <summary>A parte da cidade do grupo de busca (a cidade grande tem 6 grupos, um por parte).</summary>
        public int setor;
        /// <summary>As pessoas de um acampamento (o índice dele; -1: um grupo de busca).</summary>
        public int acampamento = -1;
        /// <summary>Os dois do carro quebrado (o índice dele em <c>Objetos.Quebrados</c>; -1: não é da estrada).</summary>
        public int cena = -1;
        /// <summary>Os militares de uma base (03/11/2026: o índice dela em <see cref="BasesMilitaresDoMundo"/>; -1: não é).</summary>
        public int baseMilitar = -1;
        /// <summary>Os membros no começo (o que o save guarda: quem morreu sai da máscara).</summary>
        public int quantos;
        public readonly List<Sobrevivente> membros = new List<Sobrevivente>();
        public EstadoDoGrupo estado = EstadoDoGrupo.Explorando;
        public IAlvoDeZumbi inimigo;
        public Vector3 ondeViu;
        public float viuEm = -99f;
        public Vector3 destino;
        public bool temDestino;
        float _proximoDestino;
        // (as pessoas do acampamento não fogem: estão em casa; os do carro quebrado não largam o carro; os militares, a base)
        public bool Fugindo => acampamento < 0 && cena < 0 && baseMilitar < 0 && quantos >= 2 && Vivos == 1;

        /// <summary>
        /// Contra os jogadores: o grupo de busca e os militares, sempre; o acampamento, depois que alguém feriu um deles; os
        /// do carro quebrado, depois de alguém mirar neles, chegar armado ou ferir um deles.
        /// </summary>
        public bool Hostil => baseMilitar >= 0 || (cena >= 0 ? CarrosQuebradosDoMundo.Instancia != null && CarrosQuebradosDoMundo.Instancia.Hostil(cena)
            : acampamento < 0 || (AcampamentosDoMundo.Instancia != null && AcampamentosDoMundo.Instancia.Hostil(acampamento)));

        public Sobrevivente Lider
        {
            get
            {
                foreach (var m in membros) if (m != null && !m.Morto) return m;
                return null;
            }
        }

        public int Vivos
        {
            get
            {
                int n = 0;
                foreach (var m in membros) if (m != null && !m.Morto) n++;
                return n;
            }
        }

        /// <summary>A posição do grupo (a do líder; sem ele, a do primeiro membro que sobrou).</summary>
        public Vector3 Centro
        {
            get
            {
                var l = Lider;
                if (l != null) return l.transform.position;
                foreach (var m in membros) if (m != null) return m.transform.position;
                return Vector3.zero;
            }
        }

        /// <summary>Alguém do grupo viu (ouviu, levou um tiro de) um inimigo: todos passam a lutar com ele.</summary>
        public void Ver(IAlvoDeZumbi quem, Vector3 onde, Sobrevivente quemViu)
        {
            if (quem == null || !quem.AlvoValido) return;
            bool novo = estado != EstadoDoGrupo.Combate || inimigo != quem;
            inimigo = quem;
            ondeViu = onde;
            viuEm = Time.time;
            estado = EstadoDoGrupo.Combate;
            if (novo && quemViu != null) quemViu.Falar(Frase.Contato);
        }

        /// <summary>Uns 5 vezes por segundo: perder o inimigo de vista, e o próximo lugar para explorar.</summary>
        public void Pensar()
        {
            if (estado == EstadoDoGrupo.Combate && (inimigo == null || !inimigo.AlvoValido || Time.time - viuEm > 30f))
            {
                if (inimigo != null) Lider?.Falar(Frase.Sumiu);
                estado = EstadoDoGrupo.Explorando;
                inimigo = null;
                temDestino = false;
            }
            if (acampamento >= 0 || cena >= 0 || baseMilitar >= 0) return;   // o acampamento, o carro quebrado e a base não exploram: cada um no seu posto
            var lider = Lider;
            if (estado != EstadoDoGrupo.Explorando || lider == null) return;
            bool chegou = temDestino && Vector3.Distance(lider.transform.position, destino) < 2.5f;
            if (!temDestino || chegou || Time.time > _proximoDestino) NovoDestino(lider);
        }

        // um lugar a 20–40 m do líder, no NavMesh, na cidade e perto de algum jogador (senão o grupo se afastava e sumia)
        void NovoDestino(Sobrevivente lider)
        {
            _proximoDestino = Time.time + Random.Range(25f, 45f);
            var d = World.MundoDaPartida.Instancia != null ? World.MundoDaPartida.Instancia.Dados : null;
            Vector3 c = lider.transform.position;
            for (int t = 0; t < 10; t++)
            {
                var r = Random.insideUnitCircle.normalized * Random.Range(20f, 40f);
                var p = c + new Vector3(r.x, 0f, r.y);
                if (!NavMesh.SamplePosition(p, out var hit, 4f, NavMesh.AllAreas)) continue;
                if (PertoDeUmJogador(hit.position) > 60f) continue;
                if (d != null && d.CidadeEm(hit.position.x, hit.position.z) == null && t < 7) continue;
                destino = hit.position;
                temDestino = true;
                return;
            }
            temDestino = false;
        }

        public static float PertoDeUmJogador(Vector3 p)
        {
            float menor = float.MaxValue;
            foreach (var j in JogadorRede.Todos)
                if (j != null && j.IsSpawned && j.Criado) menor = Mathf.Min(menor, Vector3.Distance(j.transform.position, p));
            return menor;
        }

        /// <summary>Um membro morreu: os outros reagem.</summary>
        public void Morreu(Sobrevivente quem)
        {
            foreach (var m in membros)
                if (m != null && m != quem && !m.Morto)
                {
                    m.Falar(Fugindo ? Frase.Fugindo : Frase.CaiuUm);   // (quem não foge, como o acampamento, não diz que foge)
                    break;
                }
        }
    }
}
