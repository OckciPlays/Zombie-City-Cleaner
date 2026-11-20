using System.Collections.Generic;
using UnityEngine;

namespace ZombieCityCleaner.Combat
{
    /// <summary>Os canais do mixer de som (a aba Som das Configurações). Novos só no fim (o PlayerPrefs guarda pelo nome).</summary>
    public enum CanalDoSom : byte
    {
        /// <summary>Sem canal: o som não passa por nenhum volume do mixer (só o geral).</summary>
        Nenhum,
        Animais, Armas, Golpes, Passos, Corpo, Clima, Agua, Fogo, Veiculos, Vozes, Objetos, Avisos,
    }

    /// <summary>
    /// O mixer de som detalhado (20/11/2026, a pedido: "Quero um mixer de som bem legal e detalhado ... antes alguns sons não
    /// tinha sentido, tipo o som dos animais ... deixe os que existem hoje também, apenas adicione mais"). Os volumes de
    /// antes continuam onde estavam (o geral no <see cref="UI.Opcoes"/>, a música e o ambiente no MaestroDaTrilha, os zumbis
    /// no ZombieManager); aqui ficam os canais novos, de 0 a 100%, gravados no computador (PlayerPrefs "som.&lt;canal&gt;").
    /// <list type="bullet">
    /// <item>Quase todo som passa pelo <see cref="Efeitos.Som"/>, que multiplica pelo volume do canal do clipe
    /// (<see cref="CanalDe"/>): o dos gravados sai da pasta de Resources em que o <see cref="SonsGravados"/> os achou
    /// (Armas, Bichos, Passos...); o dos feitos em código, do nome do clipe.</item>
    /// <item>Os sistemas com a fonte própria multiplicam onde põem o volume: a chuva e o trovão (Clima), a água e o nado
    /// (Agua), as chamas e a comida no fogo (Fogo), o motor, a buzina e o arranque (Veiculos), a tosse do Dia 0 (Vozes); as
    /// fontes paradas (a TV, o rádio, o cortador, a mangueira) vão pelo <see cref="NoCanal"/>.</item>
    /// <item>O volume é só de quem ouve: cada computador tem o seu.</item>
    /// </list>
    /// </summary>
    public static class MixerDeSom
    {
        static float[] _volumes;
        static readonly Dictionary<AudioClip, CanalDoSom> _canais = new Dictionary<AudioClip, CanalDoSom>();

        static float[] Volumes
        {
            get
            {
                if (_volumes != null) return _volumes;
                var nomes = System.Enum.GetNames(typeof(CanalDoSom));
                _volumes = new float[nomes.Length];
                for (int i = 0; i < nomes.Length; i++) _volumes[i] = i == 0 ? 1f : Mathf.Clamp01(PlayerPrefs.GetFloat("som." + nomes[i], 1f));
                return _volumes;
            }
        }

        /// <summary>O volume do canal (0 a 1; o Nenhum é sempre 1).</summary>
        public static float Volume(CanalDoSom c)
        {
            int i = (int)c;
            var v = Volumes;
            return i > 0 && i < v.Length ? v[i] : 1f;
        }

        /// <summary>Muda o volume do canal (o PlayerPrefs.Save fica para quem chama: as Configurações gravam ao soltar).</summary>
        public static void Mudar(CanalDoSom c, float volume)
        {
            int i = (int)c;
            if (i <= 0 || i >= Volumes.Length) return;
            _volumes[i] = Mathf.Clamp01(volume);
            PlayerPrefs.SetFloat("som." + c, _volumes[i]);
        }

        /// <summary>O SonsGravados marca os clipes de cada pasta de Resources que ele carrega.</summary>
        internal static void MarcarDaPasta(string caminho, AudioClip[] clips)
        {
            if (clips == null) return;
            int barra = caminho.IndexOf('/');
            string pasta = barra < 0 ? caminho : caminho.Substring(0, barra);
            foreach (var c in clips)
            {
                if (c == null) continue;
                var canal = DaPasta(pasta, c.name);
                if (canal != CanalDoSom.Nenhum) _canais[c] = canal;
            }
        }

        /// <summary>Marca clipes num canal (quem os carrega por conta própria: as vozes, por exemplo).</summary>
        public static void Marcar(AudioClip[] clips, CanalDoSom canal)
        {
            if (clips == null) return;
            foreach (var c in clips) if (c != null) _canais[c] = canal;
        }

        static CanalDoSom DaPasta(string pasta, string nome) => pasta switch
        {
            "Armas" => CanalDoSom.Armas,
            "Golpes" => CanalDoSom.Golpes,
            "Passos" => CanalDoSom.Passos,
            "Bichos" => CanalDoSom.Animais,
            "Corpo" or "Comida" => CanalDoSom.Corpo,
            "Clima" => CanalDoSom.Clima,
            "Agua" => CanalDoSom.Agua,
            "Fogo" or "Fumo" => CanalDoSom.Fogo,
            "Carro" => CanalDoSom.Veiculos,
            "Vozes" => CanalDoSom.Vozes,
            "Portas" or "Pesca" or "Arvore" or "Armadilhas" or "Interface" or "Bunker" => CanalDoSom.Objetos,
            "DiaZero" => nome.StartsWith("tosse_") ? CanalDoSom.Vozes : nome.StartsWith("carro_") ? CanalDoSom.Veiculos : CanalDoSom.Objetos,
            _ => CanalDoSom.Nenhum,
        };

        // os feitos em código (SomGerado, pelo nome que eles dão ao clipe) e os gravados carregados à parte (pelo nome do arquivo)
        static readonly (string comeco, CanalDoSom canal)[] PeloNome =
        {
            ("Bicho ", CanalDoSom.Animais), ("Passo ", CanalDoSom.Passos),
            ("Agua_", CanalDoSom.Agua), ("caiaque_", CanalDoSom.Agua),
            ("Recarga", CanalDoSom.Armas), ("Soco", CanalDoSom.Golpes),
            ("Pano", CanalDoSom.Corpo), ("Velcro", CanalDoSom.Corpo), ("Cartela", CanalDoSom.Corpo), ("Spray", CanalDoSom.Corpo),
            ("Desmaio", CanalDoSom.Corpo), ("Calafrio", CanalDoSom.Corpo), ("Dentes batendo", CanalDoSom.Corpo),
            ("Respiração", CanalDoSom.Corpo), ("Abrir lata", CanalDoSom.Corpo), ("Abrir tampa", CanalDoSom.Corpo),
            ("Solenoide", CanalDoSom.Veiculos), ("Arranque", CanalDoSom.Veiculos), ("Motor", CanalDoSom.Veiculos),
            ("Macaco", CanalDoSom.Veiculos), ("Abastecendo", CanalDoSom.Veiculos), ("Porta do carro", CanalDoSom.Veiculos),
            ("Alarme", CanalDoSom.Veiculos), ("Cross_", CanalDoSom.Veiculos), ("FatBoy_", CanalDoSom.Veiculos),
            ("Sinuca", CanalDoSom.Objetos), ("Porta:", CanalDoSom.Objetos), ("Plop", CanalDoSom.Objetos),
            ("Linha arrebentando", CanalDoSom.Objetos), ("Barbante", CanalDoSom.Objetos), ("Pregos", CanalDoSom.Objetos),
            ("Tábuas", CanalDoSom.Objetos), ("Sucata", CanalDoSom.Objetos), ("Largar", CanalDoSom.Objetos),
            ("Maço", CanalDoSom.Fogo), ("Tragada", CanalDoSom.Fogo), ("Sopro", CanalDoSom.Fogo), ("Peteleco", CanalDoSom.Fogo),
            ("labareda_", CanalDoSom.Fogo), ("fogo_", CanalDoSom.Fogo), ("fogueira", CanalDoSom.Fogo),
            ("Estalo_0", CanalDoSom.Objetos), ("TV_", CanalDoSom.Objetos),   // (a árvore estalando, a TV ligando)
            ("Bipe", CanalDoSom.Avisos),
        };

        /// <summary>O canal do clipe (o marcado, senão pelo nome; sem nenhum, <see cref="CanalDoSom.Nenhum"/>).</summary>
        public static CanalDoSom CanalDe(AudioClip clip)
        {
            if (clip == null) return CanalDoSom.Nenhum;
            if (_canais.TryGetValue(clip, out var c)) return c;
            c = CanalDoSom.Nenhum;
            string nome = clip.name;
            foreach (var (comeco, canal) in PeloNome)
                if (nome.StartsWith(comeco, System.StringComparison.Ordinal)) { c = canal; break; }
            _canais[clip] = c;
            return c;
        }

        /// <summary>
        /// Uma fonte parada (o volume posto uma vez só: a TV, o rádio, o cortador, a mangueira, os carros do Dia 0) no canal: a
        /// cada quadro, o volume dela = 'volume' × o do canal (mexer no mixer muda na hora).
        /// </summary>
        public static void NoCanal(AudioSource fonte, CanalDoSom canal, float volume)
        {
            if (fonte == null) return;
            // (uma por fonte: o mesmo objeto pode ter mais de uma, como o vizinho do Dia 0 com a voz e o cortador)
            FonteNoCanal n = null;
            foreach (var f in fonte.GetComponents<FonteNoCanal>()) if (f.Fonte == fonte) { n = f; break; }
            if (n == null) n = fonte.gameObject.AddComponent<FonteNoCanal>();
            n.Ligar(fonte, canal, volume);
        }
    }

    /// <summary>Uma fonte de volume fixo seguindo o volume do canal (<see cref="MixerDeSom.NoCanal"/>).</summary>
    public class FonteNoCanal : MonoBehaviour
    {
        AudioSource _fonte;
        CanalDoSom _canal;
        float _volume;

        public AudioSource Fonte => _fonte;

        public void Ligar(AudioSource fonte, CanalDoSom canal, float volume)
        {
            _fonte = fonte;
            _canal = canal;
            _volume = volume;
            _fonte.volume = volume * MixerDeSom.Volume(canal);
        }

        void Update()
        {
            if (_fonte != null) _fonte.volume = _volume * MixerDeSom.Volume(_canal);
        }
    }
}
