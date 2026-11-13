using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using ZombieCityCleaner.Combat;
using ZombieCityCleaner.Mundo;

namespace ZombieCityCleaner.World
{
    /// <summary>
    /// O cinzeiro da mesinha do fumo (a parte 8 do Plano_NPCs_Bunker.md da pasta de arte, a segunda fatia): as 12 bitucas do
    /// modelo (as partes "Cinzeiro.Bituca.1" a ".12") aparecem uma a uma, quantas o bunker diz (<see cref="BunkerDoMundo.Bitucas"/>:
    /// cada cigarro que um morador apaga nele), as de baixo antes.
    /// </summary>
    public class CinzeiroDoBunker : MonoBehaviour
    {
        readonly List<(int k, GameObject go)> _bitucas = new List<(int, GameObject)>();
        int _mostradas = -1;

        void Awake()
        {
            foreach (Transform t in transform)
            {
                int i = t.name.IndexOf(".Bituca.", System.StringComparison.Ordinal);
                if (i >= 0 && int.TryParse(t.name.Substring(i + 8), out int k)) _bitucas.Add((k, t.gameObject));
            }
        }

        void Update()
        {
            var b = BunkerDoMundo.Instancia;
            int n = b != null ? b.Bitucas : 0;
            if (n == _mostradas) return;
            _mostradas = n;
            foreach (var (k, go) in _bitucas) if (go.activeSelf != k <= n) go.SetActive(k <= n);
        }
    }

    /// <summary>
    /// A TV de chão da sala de estar (a parte 8): quando um morador vê TV (<see cref="BunkerDoMundo.TvLigada"/>), a tela acende
    /// com o chiado (um ruído que muda a cada quadro, com o tom azulado do tubo) e, de vez em quando, uma fita velha (as
    /// faixas de cor rolando, sem sinal bom), com o som do chiado e uma luz fraca na frente dela.
    /// </summary>
    public class TelaDaTV : MonoBehaviour
    {
        // a tela no modelo (o tv_console do mobilia_sala.py: o x do Blender é o -x daqui, e o -y, o z), no tamanho real
        static readonly Vector3 MeioDaTela = new Vector3(0.17f, 0.50f, 0.2845f);
        static readonly Vector2 TamanhoDaTela = new Vector2(0.44f, 0.32f);
        const int W = 64, H = 48;

        static AudioClip _chiado;
        Material _material;
        Texture2D _textura;
        Color32[] _pixels;
        MeshRenderer _tela;
        Light _luz;
        AudioSource _som;
        float _proximo, _fitaAte, _proximaFita;
        bool _ligada;
        System.Random _acaso;

        public static void Ligar(GameObject tv) => tv.AddComponent<TelaDaTV>();

        void Start()
        {
            _acaso = new System.Random(System.Environment.TickCount);
            _textura = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "A tela da TV" };
            _pixels = new Color32[W * H];
            var sh = Shader.Find("Universal Render Pipeline/Unlit");
            _material = new Material(sh) { name = "A tela da TV" };
            _material.SetTexture("_BaseMap", _textura);
            _material.SetColor("_BaseColor", new Color(0.85f, 0.9f, 1f));
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "Tela";
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(transform, false);
            go.transform.localPosition = MeioDaTela;
            go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            go.transform.localScale = new Vector3(TamanhoDaTela.x, TamanhoDaTela.y, 1f);
            _tela = go.GetComponent<MeshRenderer>();
            _tela.sharedMaterial = _material;
            _tela.shadowCastingMode = ShadowCastingMode.Off;
            _tela.enabled = false;
            var luz = new GameObject("Luz da TV");
            luz.transform.SetParent(transform, false);
            luz.transform.localPosition = MeioDaTela + Vector3.forward * 0.35f;
            _luz = luz.AddComponent<Light>();
            _luz.type = LightType.Point;
            _luz.range = 3.2f;
            _luz.color = new Color(0.65f, 0.75f, 1f);
            _luz.shadows = LightShadows.None;
            _luz.enabled = false;
            _som = gameObject.AddComponent<AudioSource>();
            _som.clip = Chiado();
            _som.loop = true;
            _som.spatialBlend = 1f;
            _som.rolloffMode = AudioRolloffMode.Linear;
            _som.minDistance = 1f;
            _som.maxDistance = 11f;
            _som.volume = 0.16f;
            _som.playOnAwake = false;
        }

        void OnDestroy()
        {
            if (_material != null) Destroy(_material);
            if (_textura != null) Destroy(_textura);
        }

        void Update()
        {
            var b = BunkerDoMundo.Instancia;
            bool ligada = b != null && b.TvLigada;
            if (ligada != _ligada)
            {
                _ligada = ligada;
                _tela.enabled = ligada;
                _luz.enabled = ligada;
                if (ligada) _som.Play();
                else _som.Stop();
                _proximaFita = Time.time + 6f;
            }
            if (!ligada || Time.time < _proximo) return;
            _proximo = Time.time + 1f / 15f;
            if (Time.time > _proximaFita)
            {
                _fitaAte = Time.time + 3f + (float)_acaso.NextDouble() * 5f;
                _proximaFita = _fitaAte + 6f + (float)_acaso.NextDouble() * 10f;
            }
            Pintar(Time.time < _fitaAte);
            _luz.intensity = 0.25f + 0.1f * (float)_acaso.NextDouble();
        }

        // o chiado (o ruído de cada pixel, mais forte em umas linhas) ou a fita (as faixas de cor rolando, com o ruído por cima)
        void Pintar(bool fita)
        {
            float rola = Time.time * 9f;
            for (int y = 0; y < H; y++)
            {
                float linha = 0.75f + 0.25f * (float)_acaso.NextDouble();
                for (int x = 0; x < W; x++)
                {
                    float r = (float)_acaso.NextDouble();
                    Color c;
                    if (fita)
                    {
                        int faixa = (int)((x + Mathf.Sin((y + rola) * 0.3f) * 3f) / (W / 7f));
                        c = Faixas[Mathf.Clamp(faixa, 0, Faixas.Length - 1)] * (0.55f + 0.25f * r);
                        if (((y + (int)rola) % 16) < 2) c *= 0.4f;
                    }
                    else c = new Color(r, r, r) * linha;
                    _pixels[y * W + x] = c;
                }
            }
            _textura.SetPixels32(_pixels);
            _textura.Apply(false);
        }

        static readonly Color[] Faixas =
        {
            new Color(0.75f, 0.75f, 0.75f), new Color(0.75f, 0.75f, 0.2f), new Color(0.2f, 0.75f, 0.75f), new Color(0.2f, 0.75f, 0.2f),
            new Color(0.75f, 0.2f, 0.75f), new Color(0.75f, 0.2f, 0.2f), new Color(0.2f, 0.2f, 0.75f),
        };

        // o chiado da TV: o ruído branco filtrado (4 s, em laço), com umas batidas da fita
        static AudioClip Chiado()
        {
            if (_chiado != null) return _chiado;
            int taxa = 22050, n = taxa * 4;
            var d = new float[n];
            var r = new System.Random(7);
            float a = 0f;
            for (int i = 0; i < n; i++)
            {
                float b = (float)r.NextDouble() * 2f - 1f;
                a = a * 0.55f + b * 0.45f;
                float hum = 0.08f * Mathf.Sin(2f * Mathf.PI * 60f * i / taxa);
                d[i] = (a * 0.5f + hum) * (0.85f + 0.15f * Mathf.Sin(2f * Mathf.PI * 0.5f * i / taxa));
            }
            _chiado = SomGerado.Clip("Chiado da TV", d, taxa);
            return _chiado;
        }
    }

    /// <summary>
    /// A estação de rádio das operações (a parte 8): quando um morador ouve o rádio (<see cref="BunkerDoMundo.RadioLigado"/>),
    /// o alto-falante chia, com os assobios da sintonia e, de vez em quando, um bipe de código ao longe (ninguém chamando: os
    /// pedidos de socorro vêm na parte 11).
    /// </summary>
    public class RadioDoBunker : MonoBehaviour
    {
        static AudioClip _som;
        AudioSource _fonte;
        bool _ligado;

        public static void Ligar(GameObject radio) => radio.AddComponent<RadioDoBunker>();

        void Start()
        {
            _fonte = gameObject.AddComponent<AudioSource>();
            _fonte.clip = Som();
            _fonte.loop = true;
            _fonte.spatialBlend = 1f;
            _fonte.rolloffMode = AudioRolloffMode.Linear;
            _fonte.minDistance = 1f;
            _fonte.maxDistance = 10f;
            _fonte.volume = 0.14f;
            _fonte.playOnAwake = false;
        }

        void Update()
        {
            var b = BunkerDoMundo.Instancia;
            bool ligado = b != null && b.RadioLigado;
            if (ligado == _ligado) return;
            _ligado = ligado;
            if (ligado)
            {
                _fonte.time = Random.Range(0f, _fonte.clip.length);
                _fonte.Play();
            }
            else _fonte.Stop();
        }

        // 8 s: o chiado com as ondas da sintonia, os assobios (o tom que sobe e desce) e os bipes do código
        static AudioClip Som()
        {
            if (_som != null) return _som;
            int taxa = 22050, n = taxa * 8;
            var d = new float[n];
            var r = new System.Random(11);
            float a = 0f, fase = 0f, faseBipe = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / taxa;
                float b = (float)r.NextDouble() * 2f - 1f;
                a = a * 0.7f + b * 0.3f;
                float onda = 0.6f + 0.4f * Mathf.Sin(2f * Mathf.PI * 0.23f * t) * Mathf.Sin(2f * Mathf.PI * 0.07f * t + 1f);
                float assobio = 0f;
                if (t > 2.2f && t < 3.4f)
                {
                    float f = 900f + 700f * Mathf.Sin((t - 2.2f) * 2.6f);
                    fase += 2f * Mathf.PI * f / taxa;
                    assobio = 0.12f * Mathf.Sin(fase) * Mathf.Sin(Mathf.PI * (t - 2.2f) / 1.2f);
                }
                float bipe = 0f;
                if (t > 5f && t < 7f)
                {
                    float u = (t - 5f) * 6f;
                    bool liga = (u % 1f) < ((int)u % 3 == 0 ? 0.6f : 0.25f);
                    faseBipe += 2f * Mathf.PI * 700f / taxa;
                    bipe = liga ? 0.06f * Mathf.Sin(faseBipe) : 0f;
                }
                d[i] = a * 0.45f * onda + assobio + bipe;
            }
            _som = SomGerado.Clip("O rádio do bunker", d, taxa);
            return _som;
        }
    }

    /// <summary>
    /// As pilhas de roupa na mesa de dobrar (a lavanderia e a rouparia do B3, a parte 8): na frente de cada lugar de dobrar,
    /// a pilha por dobrar à esquerda e a das dobradas à direita, onde o "Dobrar_Roupa" pega e põe a peça (o bloco_morador.py
    /// da pasta de arte: PILHA_ESQ e PILHA_DIR).
    /// </summary>
    public static class PilhasDeRoupa
    {
        static Material[] _cores;
        static readonly Color[] Cores =
        {
            new Color(0.32f, 0.36f, 0.42f), new Color(0.55f, 0.18f, 0.16f), new Color(0.78f, 0.76f, 0.70f), new Color(0.24f, 0.30f, 0.20f),
            new Color(0.45f, 0.38f, 0.28f), new Color(0.16f, 0.17f, 0.20f), new Color(0.62f, 0.55f, 0.36f),
        };

        static Material Cor(int k)
        {
            if (_cores == null)
            {
                var sh = Shader.Find("Universal Render Pipeline/Lit");
                _cores = new Material[Cores.Length];
                for (int i = 0; i < Cores.Length; i++)
                {
                    _cores[i] = new Material(sh) { name = "A roupa dobrada " + i };
                    _cores[i].SetColor("_BaseColor", Cores[i]);
                    _cores[i].SetFloat("_Smoothness", 0.05f);
                }
            }
            return _cores[((k % Cores.Length) + Cores.Length) % Cores.Length];
        }

        /// <summary>As pilhas da mesa (o móvel <paramref name="movel"/> do bunker), no andar (<paramref name="pai"/>, nas coordenadas da obra).</summary>
        public static void Por(Transform pai, int movel, float altoDaMesa)
        {
            var todos = LugaresDoBunker.Todos;
            for (int i = 0; i < todos.Length; i++)
            {
                var l = todos[i];
                if (l.tipo != TipoDoLugar.Dobrar || l.movel != movel) continue;
                var giro = Quaternion.Euler(0f, l.giro, 0f);
                // (a pilha por dobrar à esquerda de quem dobra; a das dobradas à direita)
                Pilha(pai, l.raiz + giro * new Vector3(-0.32f, altoDaMesa, 0.45f), giro, i * 7, 4, false);
                Pilha(pai, l.raiz + giro * new Vector3(0.27f, altoDaMesa, 0.43f), giro, i * 7 + 3, 3, true);
            }
        }

        static void Pilha(Transform pai, Vector3 base_, Quaternion giro, int semente, int n, bool dobradas)
        {
            var r = new System.Random(semente);
            float y = 0f;
            for (int k = 0; k < n; k++)
            {
                float h = dobradas ? 0.022f : 0.012f;
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = dobradas ? "Roupa dobrada" : "Roupa por dobrar";
                Object.Destroy(go.GetComponent<Collider>());
                go.transform.SetParent(pai, false);
                float gira = (float)(r.NextDouble() - 0.5) * (dobradas ? 8f : 30f);
                go.transform.localRotation = giro * Quaternion.Euler(0f, gira, 0f);
                var tam = dobradas ? new Vector3(0.20f, h, 0.23f) : new Vector3(0.36f + (float)r.NextDouble() * 0.06f, h, 0.40f + (float)r.NextDouble() * 0.06f);
                go.transform.localScale = tam;
                go.transform.localPosition = base_ + Vector3.up * (y + h / 2f) + giro * new Vector3((float)(r.NextDouble() - 0.5) * 0.03f, 0f, (float)(r.NextDouble() - 0.5) * 0.03f);
                var mr = go.GetComponent<MeshRenderer>();
                mr.sharedMaterial = Cor(r.Next(Cores.Length));
                mr.shadowCastingMode = UI.Graficos.SombraDosMoveis ? ShadowCastingMode.On : ShadowCastingMode.Off;
                y += h;
            }
        }
    }
}
