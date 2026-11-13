using System;
using System.Collections.Generic;
using UnityEngine;
using ZombieCityCleaner.Combat;
using Random = UnityEngine.Random;

namespace ZombieCityCleaner.Music
{
    /// <summary>
    /// Os sons raros de um lugar (feito para o Hospital Principal, 07/11/2026: o Plano_Hospital.md da pasta de arte, a parte 6,
    /// "o mais importante é o silêncio"; serve para outra construção). Um som de cada vez, de um dos <see cref="pontos"/> do
    /// andar em que quem escuta está (as <see cref="areas"/>), de tempos em tempos (<see cref="intervalo"/>, de 45 a 120 s):
    /// <list type="bullet">
    /// <item>o tipo é sorteado pelo peso dele naquele andar (<see cref="Tipo.pesos"/>: no térreo, o vento e as portas; no
    /// subsolo, as goteiras e os canos...), entre os que têm um ponto na distância certa (<see cref="Tipo.distancia"/>);</item>
    /// <item>o som sai de lá (uma fonte 3D, das duas guardadas aqui), abafado pelo que estiver no meio (<see cref="Abafamento"/>:
    /// as paredes e as lajes tiram os agudos), com o volume dos sons de ambiente das Configurações
    /// (<see cref="MaestroDaTrilha.VolumeDoAmbiente"/>);</item>
    /// <item>só roda com quem escuta (a câmera de quem joga) dentro dos <see cref="limites"/> (com a <see cref="margem"/>):
    /// fora, nada toca e nada é sorteado; ao entrar, o primeiro som espera um intervalo inteiro;</item>
    /// <item>dentro (sem a margem), com <see cref="pedeSilencio"/>, ele pede o silêncio ao maestro
    /// (<see cref="MaestroDaTrilha.QuietoAte"/>): a música da exploração não toca ali, e os sons de fora ficam raros (no
    /// hospital, a pedido, só os sons: sem música nenhuma).</item>
    /// </list>
    /// Cada jogador ouve os seus (nada passa pela rede: é só o ambiente).
    /// </summary>
    public class AmbienteSonoro : MonoBehaviour
    {
        [Serializable]
        public class Tipo
        {
            public string nome;
            public AudioClip[] clipes;
            [Tooltip("O peso do sorteio em cada área (andar), na ordem das áreas (0: não toca nela).")]
            public float[] pesos = new float[0];
            [Tooltip("O volume (de 0 a 1), sorteado entre os dois.")]
            public Vector2 volume = new Vector2(0.45f, 0.65f);
            [Tooltip("O tom, sorteado entre os dois.")]
            public Vector2 tom = new Vector2(0.94f, 1.06f);
            [Tooltip("A distância de quem escuta até o ponto (m), mínima e máxima.")]
            public Vector2 distancia = new Vector2(8f, 30f);
            [Tooltip("Até onde ele se ouve (m): o maxDistance da fonte.")]
            public float alcance = 40f;
            [Tooltip("Só um trecho do clipe (s, mínimo e máximo), de um ponto sorteado; (0, 0): o clipe inteiro.")]
            public Vector2 trecho;
            [Tooltip("O corte dos agudos sempre (Hz; 0: só o do abafamento): as vozes atrás de uma porta.")]
            public float corte;
        }

        [Serializable]
        public struct Ponto
        {
            public Vector3 lugar;   // no espaço deste objeto
            public int tipo, area;
        }

        public Tipo[] tipos = new Tipo[0];
        public List<Ponto> pontos = new List<Ponto>();
        [Tooltip("As áreas (os andares): a faixa de altura de cada uma, no espaço deste objeto.")]
        public Vector2[] areas = new Vector2[0];
        [Tooltip("Onde ele roda (no espaço deste objeto).")]
        public Bounds limites = new Bounds(Vector3.zero, Vector3.one * 50f);
        [Tooltip("Além dos limites, até onde quem escuta ainda conta como perto (m).")]
        public float margem = 6f;
        [Tooltip("De um som ao seguinte (s), mínimo e máximo.")]
        public Vector2 intervalo = new Vector2(45f, 120f);
        [Range(0f, 2f)] public float volume = 1f;
        [Tooltip("Com quem escuta dentro, a trilha do jogo fica quieta (a exploração não toca; o perigo, sim).")]
        public bool pedeSilencio = true;
        [Tooltip("Mostra no console o que tocou (para ajustar).")]
        public bool anotar;

        readonly AudioSource[] _fontes = new AudioSource[2];
        readonly AudioLowPassFilter[] _filtros = new AudioLowPassFilter[2];
        readonly float[] _paraEm = new float[2];
        readonly List<int> _candidatos = new List<int>();
        readonly float[] _pesoDoTipo = new float[32];
        float _proximo = -1f, _proximaOlhada;
        bool _perto;
        int _vez;

        /// <summary>Sorteia já um som (a tecla J da cena de teste, pelo SendMessage, e os testes).</summary>
        public void TocarUmSom()
        {
            if (Abafamento.Ouvido(out var o) && Tocar(o) == null && anotar) Debug.Log("[Ambiente] nenhum ponto na distância certa daqui");
        }

        void Awake()
        {
            for (int i = 0; i < _fontes.Length; i++)
            {
                var go = new GameObject($"Fonte {i + 1}") { hideFlags = HideFlags.DontSave };
                go.transform.SetParent(transform, false);
                var f = go.AddComponent<AudioSource>();
                f.playOnAwake = false;
                f.spatialBlend = 1f;
                f.rolloffMode = AudioRolloffMode.Logarithmic;
                f.minDistance = 2.5f;
                f.dopplerLevel = 0f;
                f.spread = 40f;
                _fontes[i] = f;
                _filtros[i] = go.AddComponent<AudioLowPassFilter>();
            }
        }

        void Update()
        {
            // quem escuta perto? (a cada meio segundo)
            if (Time.time >= _proximaOlhada)
            {
                _proximaOlhada = Time.time + 0.5f;
                bool perto = Abafamento.Ouvido(out var ouvido) && Perto(ouvido);
                if (perto && !_perto) _proximo = Time.time + Random.Range(intervalo.x, intervalo.y);
                if (perto && pedeSilencio && limites.Contains(transform.InverseTransformPoint(ouvido))) MaestroDaTrilha.QuietoAte = Time.unscaledTime + 1f;
                if (!perto && _perto) Calar();
                _perto = perto;
            }
            // os trechos (a voz atrás da porta): sai devagar no fim
            for (int i = 0; i < _fontes.Length; i++)
            {
                var f = _fontes[i];
                if (_paraEm[i] <= 0f || !f.isPlaying) continue;
                if (Time.time >= _paraEm[i]) { f.Stop(); _paraEm[i] = 0f; }
                else if (_paraEm[i] - Time.time < 0.4f) f.volume *= 0.9f;
            }
            if (!_perto || _proximo < 0f || Time.time < _proximo) return;
            _proximo = Time.time + Random.Range(intervalo.x, intervalo.y);
            if (Abafamento.Ouvido(out var o)) Tocar(o);
        }

        bool Perto(Vector3 ouvido)
        {
            var b = limites;
            b.Expand(margem * 2f);
            return b.Contains(transform.InverseTransformPoint(ouvido));
        }

        int AreaDe(Vector3 local)
        {
            for (int i = 0; i < areas.Length; i++)
                if (local.y >= areas[i].x && local.y < areas[i].y) return i;
            return -1;
        }

        void Calar()
        {
            foreach (var f in _fontes) if (f != null) f.Stop();
            _proximo = -1f;
        }

        /// <summary>Sorteia e toca um som perto de quem escuta (no andar dele). Devolve o nome do tipo (ou null).</summary>
        public string Tocar(Vector3 ouvido)
        {
            var local = transform.InverseTransformPoint(ouvido);
            int area = AreaDe(local);
            if (area < 0 || tipos.Length == 0) return null;
            // o peso de cada tipo que tem um ponto na distância certa
            float soma = 0f;
            for (int t = 0; t < tipos.Length && t < _pesoDoTipo.Length; t++)
            {
                _pesoDoTipo[t] = 0f;
                var tp = tipos[t];
                if (tp.clipes == null || tp.clipes.Length == 0 || tp.pesos == null || area >= tp.pesos.Length || tp.pesos[area] <= 0f) continue;
                foreach (var p in pontos)
                {
                    if (p.tipo != t || p.area != area) continue;
                    float d = Vector3.Distance(p.lugar, local);
                    if (d >= tp.distancia.x && d <= tp.distancia.y) { _pesoDoTipo[t] = tp.pesos[area]; break; }
                }
                soma += _pesoDoTipo[t];
            }
            if (soma <= 0f) return null;
            float r = Random.value * soma;
            int tipo = 0;
            for (; tipo < tipos.Length - 1; tipo++)
            {
                r -= _pesoDoTipo[tipo];
                if (r <= 0f && _pesoDoTipo[tipo] > 0f) break;
            }
            var tp2 = tipos[tipo];
            _candidatos.Clear();
            for (int i = 0; i < pontos.Count; i++)
            {
                var p = pontos[i];
                if (p.tipo != tipo || p.area != area) continue;
                float d = Vector3.Distance(p.lugar, local);
                if (d >= tp2.distancia.x && d <= tp2.distancia.y) _candidatos.Add(i);
            }
            if (_candidatos.Count == 0) return null;
            var ponto = transform.TransformPoint(pontos[_candidatos[Random.Range(0, _candidatos.Count)]].lugar);
            var clipe = tp2.clipes[Random.Range(0, tp2.clipes.Length)];
            if (clipe == null) return null;

            int k = _vez++ % _fontes.Length;
            var f = _fontes[k];
            f.transform.position = ponto;
            f.clip = clipe;
            f.maxDistance = tp2.alcance;
            f.pitch = Random.Range(tp2.tom.x, tp2.tom.y);
            // abafado pelo que estiver no meio (as paredes, as lajes) e, se o tipo pede, sempre (a voz atrás da porta)
            float quanto = Abafamento.Quanto(ouvido, ponto);
            float corte = Abafamento.Corte(quanto);
            if (tp2.corte > 0f) corte = Mathf.Min(corte, tp2.corte);
            _filtros[k].cutoffFrequency = corte;
            f.volume = Random.Range(tp2.volume.x, tp2.volume.y) * Mathf.Lerp(1f, Abafamento.VolumeTapado, quanto) * volume * MaestroDaTrilha.VolumeDoAmbiente;
            _paraEm[k] = 0f;
            if (tp2.trecho.y > 0f && clipe.length > tp2.trecho.y + 0.5f)
            {
                float dur = Random.Range(tp2.trecho.x, tp2.trecho.y);
                f.time = Random.Range(0f, clipe.length - dur);
                f.Play();
                _paraEm[k] = Time.time + dur / Mathf.Max(0.1f, f.pitch);
            }
            else
            {
                f.time = 0f;
                f.Play();
            }
            if (anotar) Debug.Log($"[Ambiente] {tp2.nome} ({clipe.name}) a {Vector3.Distance(ouvido, ponto):0} m, abafado {quanto:0.##}");
            return tp2.nome;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.6f);
            Gizmos.DrawWireCube(limites.center, limites.size);
            foreach (var p in pontos)
            {
                Gizmos.color = Color.HSVToRGB((p.tipo * 0.13f) % 1f, 0.8f, 1f);
                Gizmos.DrawSphere(p.lugar, 0.2f);
            }
        }
    }
}
