using System.Collections.Generic;
using UnityEngine;

namespace ZombieCityCleaner.Combat
{
    /// <summary>O que a voz faz (as pastas em Resources/Vozes/&lt;pessoa&gt;).</summary>
    public enum TipoDeVoz : byte { Golpe, Dor, Esforco, Ataque, Pulo, Queda, Tosse, Susto, Expressao, RespiracaoForte, RespiracaoFrio }

    /// <summary>
    /// As vozes dos personagens (19/11/2026, a pedido do Pedro: "os sons aos npcs, aos jogadores também, determinando o
    /// sexo"), do Voices - Essentials: 2 homens e 2 mulheres, cada um com o golpe, a dor, o esforço, o ataque, o pulo, a
    /// queda, a tosse, o susto, as expressões (o "hum", o suspiro) e as respirações (forte e de frio).
    ///  - O jogador fala com a voz do sexo da aparência dele, e uma das duas pelo nome (sempre a mesma).
    ///  - O NPC (os corpos dos sobreviventes são de homem) fala com um dos dois homens, pela semente.
    ///  - Uma voz por vez em cada um: a dor, o golpe e o ataque passam na frente do resto. Sem a voz daquela pessoa, vai a
    ///    outra do mesmo sexo.
    /// </summary>
    public static class VozDoPersonagem
    {
        static readonly string[] Pessoas = { "Homem1", "Homem2", "Mulher1", "Mulher2" };
        static readonly Dictionary<int, (float ate, bool forte)> _falando = new Dictionary<int, (float, bool)>();
        static readonly Dictionary<int, AudioClip[]> _clips = new Dictionary<int, AudioClip[]>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Zerar() => _falando.Clear();

        /// <summary>As vozes estão no jogo (Resources/Vozes)?</summary>
        public static bool TemVozes => Clips(0, TipoDeVoz.Expressao) != null;

        /// <summary>A voz do jogador: o sexo e uma das duas, pelo nome.</summary>
        public static int DoJogador(bool mulher, string nome)
        {
            int h = 0;
            if (nome != null)
                foreach (char c in nome) h = h * 31 + c;
            return (mulher ? 2 : 0) + (h & 1);
        }

        /// <summary>A voz do NPC: um dos dois homens, pela semente.</summary>
        public static int DoNpc(int semente) => semente & 1;

        /// <summary>Os clipes da voz daquela pessoa (null se não houver nenhum, nem na outra do mesmo sexo).</summary>
        public static AudioClip[] Clips(int pessoa, TipoDeVoz v)
        {
            pessoa = Mathf.Clamp(pessoa, 0, 3);
            int chave = pessoa * 32 + (int)v;
            if (!_clips.TryGetValue(chave, out var c) || (c != null && c.Length > 0 && c[0] == null))
            {
                c = Resources.LoadAll<AudioClip>($"Vozes/{Pessoas[pessoa]}/{v}");
                if (c == null || c.Length == 0) c = Resources.LoadAll<AudioClip>($"Vozes/{Pessoas[pessoa ^ 1]}/{v}");
                _clips[chave] = c;
                MixerDeSom.Marcar(c, CanalDoSom.Vozes);   // (20/11/2026: o canal Vozes do mixer)
            }
            return c != null && c.Length > 0 ? c : null;
        }

        /// <summary>Um clipe sorteado da voz (null se não houver).</summary>
        public static AudioClip Um(int pessoa, TipoDeVoz v)
        {
            var c = Clips(pessoa, v);
            return c != null ? c[Random.Range(0, c.Length)] : null;
        }

        static bool Forte(TipoDeVoz v) => v == TipoDeVoz.Golpe || v == TipoDeVoz.Dor || v == TipoDeVoz.Ataque;

        static float Alcance(TipoDeVoz v) => v switch
        {
            TipoDeVoz.Dor => 45f,
            TipoDeVoz.Golpe => 35f,
            TipoDeVoz.Ataque => 35f,
            TipoDeVoz.Expressao => 16f,
            TipoDeVoz.Tosse => 20f,
            _ => 22f,
        };

        /// <summary>
        /// A voz de quem (o objeto: uma voz por vez em cada um), na voz da pessoa, no ponto. Espacial: 3D, no corpo; senão,
        /// na cabeça (o próprio jogador). Devolve se tocou.
        /// </summary>
        public static bool Tocar(Object quem, int pessoa, TipoDeVoz v, Vector3 onde, bool espacial, float volume = 1f)
        {
            int id = quem != null ? quem.GetHashCode() : 0;
            float agora = Time.time;
            bool forte = Forte(v);
            if (_falando.TryGetValue(id, out var f) && agora < f.ate && (f.forte || !forte)) return false;
            var clip = Um(pessoa, v);
            if (clip == null) return false;
            _falando[id] = (agora + (forte ? Mathf.Min(clip.length, 0.6f) : clip.length), forte);
            Efeitos.Som(clip, onde, volume, espacial, Alcance(v));
            return true;
        }
    }
}
