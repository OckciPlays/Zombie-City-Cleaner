using System.Collections.Generic;
using UnityEngine;
using ZombieCityCleaner.World;

namespace ZombieCityCleaner.Combat
{
    /// <summary>
    /// Efeitos de tiro compartilhados: clarão do cano (luz rápida + brilho), faíscas no impacto e os sons das armas, da
    /// comida, dos passos e do soco. Desde 20/11/2026 (a pedido: "podemos excluir os sons que já foram substituídos"), os
    /// gravados (<see cref="SonsGravados"/>: o tiro de cada arma, o clique sem bala, o pente, a bomba e o cartucho da
    /// espingarda, o ferrolho, a luneta, a lanterna, os interruptores, as mordidas, os goles, o soco e os passos) não têm
    /// mais a reserva feita em código; sem um deles, o som não toca. Ficam feitos aqui só os que não têm gravação: a
    /// recarga, abrir a lata e a tampa, largar, o soco na parede (<see cref="SocoParede"/>) e os passos no carpete.
    /// Com a porta do bunker fechada, o som de um lado não chega do outro (<see cref="BunkerRede.Isolado"/>).
    /// Fica num objeto da cena, com os materiais das partículas.
    /// </summary>
    public class Efeitos : MonoBehaviour
    {
        public Material materialParticulas;
        public Material materialClarao;

        static Efeitos _instancia;
        /// <summary>As variações do tiro de cada arma (na ordem de <see cref="TipoDeArma"/>).</summary>
        public static AudioClip[][] Tiros;
        public static AudioClip Clique, Recarga, Carregador, Bomba, Cartucho, Lanterna, Ferrolho, LunetaEntra, LunetaSai;
        /// <summary>O interruptor do bunker.</summary>
        public static AudioClip Chave;
        /// <summary>A comida e a água: abrir e largar no chão (<see cref="SomDosItens"/>); as mordidas e os goles, os gravados.</summary>
        public static AudioClip AbrirLata, AbrirTampa, Largar;
        public static AudioClip[] Mastigar, Gole;
        /// <summary>Os passos (os gravados; o carpete, do <see cref="SomDePasso"/>): [superfície][correndo ? 1 : 0][variação].</summary>
        public static AudioClip[][][] Passos;
        /// <summary>O soco: o vento do braço e a pancada num zumbi (os gravados) e a batida numa parede (<see cref="SomDoSoco"/>).</summary>
        public static AudioClip[] SocoVento, SocoCarne;
        public static AudioClip SocoParede;
        /// <summary>O soco (ou a arma branca) na parede: as variações gravadas (19/11/2026; desde 20/11/2026, só elas).</summary>
        public static AudioClip[] SocosNaParede;
        /// <summary>As cápsulas caindo no chão depois do tiro (19/11/2026, gravadas; null sem elas).</summary>
        public static AudioClip[] Capsulas;

        readonly Queue<AudioSource> _fontes = new Queue<AudioSource>();
        // (20/11/2026) até quando cada fonte está tocando: o som novo vai para uma livre, e não mais para a próxima da fila
        // (com os gravados mais longos, o uivo do lobo era "roubado" no meio por um passo ou uma cápsula: pulava de lugar
        // ou sumia)
        readonly Dictionary<AudioSource, float> _ocupadaAte = new Dictionary<AudioSource, float>();
        const int FontesNoComeco = 24, FontesNoMaximo = 64;
        ParticleSystem _faiscas, _poeira, _lascas;

        void Awake()
        {
            _instancia = this;
            Tiros = new AudioClip[Armas.Quantas][];
            // (as de sucata, 09/11/2026, usam o som da real: elas vêm depois dela)
            // (20/11/2026: só os gravados; o tiro feito em código, o SomDeTiro, saiu: as 5 reais têm os seus em Resources/Armas)
            for (int i = 0; i < Armas.Quantas; i++)
                Tiros[i] = Armas.DeSucata((TipoDeArma)i) ? Tiros[(int)Armas.Real((TipoDeArma)i)] : SonsGravados.Tiros((TipoDeArma)i);
            // os que não têm gravação continuam feitos aqui (a recarga, a lata, a tampa, largar e o soco na parede, que o carro,
            // a árvore caindo e os zumbis também tocam)
            Recarga = Gerar("Recarga", 0.5f, (t, r) => (float)(r.NextDouble() * 2 - 1) * (Mathf.Exp(-t * 120f) + Mathf.Exp(-Mathf.Max(0, t - 0.28f) * 140f) * (t > 0.28f ? 1 : 0)) * 0.6f);
            AbrirLata = SomDosItens.Clip("Abrir lata", SomDosItens.AbrirLata(21));
            AbrirTampa = SomDosItens.Clip("Abrir tampa", SomDosItens.AbrirTampa(22));
            Largar = SomDosItens.Clip("Largar", SomDosItens.Largar(23));
            SocoParede = SomGerado.Clip("Soco na parede", SomDoSoco.NaParede(71));
            PorOsGravados();   // (19/11/2026, a repaginada dos sons: SonsGravados)
            for (int i = 0; i < FontesNoComeco; i++) _fontes.Enqueue(NovaFonte());
            _faiscas = Particulas("Faíscas", new Color(1f, 0.8f, 0.4f), 0.03f, 0.25f, 6f);
            _poeira = Particulas("Poeira", new Color(0.55f, 0.52f, 0.48f), 0.07f, 0.6f, 1.2f);
            // as lascas do machado na madeira (18/11/2026): pedacinhos claros que pulam do corte e caem
            _lascas = Particulas("Lascas", new Color(0.78f, 0.62f, 0.42f), 0.035f, 1.1f, 3.4f);
            var mainDasLascas = _lascas.main;
            mainDasLascas.gravityModifier = 1.3f;
            mainDasLascas.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.05f);
            mainDasLascas.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 4.2f);
            mainDasLascas.startColor = new ParticleSystem.MinMaxGradient(new Color(0.85f, 0.70f, 0.48f), new Color(0.45f, 0.32f, 0.20f));
            var formaDasLascas = _lascas.shape; formaDasLascas.angle = 50f;
            PrepararOGolpeNoChao();
        }

        // ------------------------------------------------------------------ o golpe no chão do Brutamonte (05/11/2026)

        ParticleSystem _nuvemDoGolpe, _terraDoGolpe;

        /// <summary>
        /// As partículas do golpe no chão (uma vez, como as outras): a nuvem (fumaça macia, que cresce, freia e some) e os
        /// torrões de terra (pequenos, caem e quicam no chão). Tudo sai por <see cref="GolpeNoChao"/>, partícula a partícula.
        /// </summary>
        void PrepararOGolpeNoChao()
        {
            _nuvemDoGolpe = Particulas("Nuvem do golpe", Color.white, 0.8f, 2f, 0f);
            var main = _nuvemDoGolpe.main;
            main.maxParticles = 160; main.gravityModifier = -0.02f;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, 2f * Mathf.PI);
            var freio = _nuvemDoGolpe.limitVelocityOverLifetime; freio.enabled = true; freio.limit = 0.25f; freio.dampen = 0.05f;
            var cresce = _nuvemDoGolpe.sizeOverLifetime; cresce.enabled = true;
            cresce.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.55f), new Keyframe(0.25f, 1.2f), new Keyframe(1f, 1.9f)));
            var gira = _nuvemDoGolpe.rotationOverLifetime; gira.enabled = true; gira.z = new ParticleSystem.MinMaxCurve(-0.6f, 0.6f);
            var cor = _nuvemDoGolpe.colorOverLifetime; cor.enabled = true;
            var some = new Gradient();
            some.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                         new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.8f, 0.06f), new GradientAlphaKey(0.35f, 0.4f), new GradientAlphaKey(0f, 1f) });
            cor.color = some;
            var desenho = _nuvemDoGolpe.GetComponent<ParticleSystemRenderer>();
            if (materialParticulas != null)
            {
                // o material das partículas com uma nuvem macia (o ponto dele, grande, virava uma bola)
                var nuvem = TexturaDeNuvem();
                var m = new Material(materialParticulas) { name = "Nuvem do golpe" };
                m.SetTexture("_BaseMap", nuvem);
                m.mainTexture = nuvem;
                desenho.sharedMaterial = m;
            }
            desenho.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            desenho.sortMode = ParticleSystemSortMode.Distance;

            _terraDoGolpe = Particulas("Terra do golpe", Color.white, 0.06f, 1.4f, 0f);
            var mt = _terraDoGolpe.main;
            mt.maxParticles = 200; mt.gravityModifier = 1.6f;
            var colisao = _terraDoGolpe.collision;   // os torrões param no chão, quicando um pouco
            colisao.enabled = true; colisao.type = ParticleSystemCollisionType.World; colisao.mode = ParticleSystemCollisionMode.Collision3D;
            colisao.dampen = 0.6f; colisao.bounce = 0.25f; colisao.lifetimeLoss = 0.15f; colisao.quality = ParticleSystemCollisionQuality.Low;
            colisao.collidesWith = 1 << 0;
        }

        /// <summary>Uma mancha redonda que some devagar até a borda, com um pouco de ruído (64 px).</summary>
        static Texture2D TexturaDeNuvem()
        {
            const int n = 64;
            var textura = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Nuvem do golpe" };
            var pixels = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                    float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                    a = Mathf.Pow(a * a * (3f - 2f * a), 1.6f) * (0.6f + 0.4f * Mathf.PerlinNoise(x * 0.13f + 5.3f, y * 0.13f + 1.7f));
                    pixels[y * n + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            textura.SetPixels32(pixels);
            textura.Apply();
            return textura;
        }

        /// <summary>
        /// O golpe no chão do Brutamonte (05/11/2026, a fúria): uma onda de poeira que corre rente ao chão em volta dos
        /// punhos, um bafo subindo do meio e os torrões de terra voando e caindo. 'forca' em volta de 1 (o terceiro golpe,
        /// 1,35, é o maior). De noite, a poeira fica um pouco mais escura (o material é sem luz).
        /// </summary>
        public static void GolpeNoChao(Vector3 ponto, float forca)
        {
            if (_instancia == null || _instancia._nuvemDoGolpe == null) return;
            // (de noite, só um pouco mais escura: com 1/3, no chão escuro ela sumia; o primeiro teste no bunker, 05/11/2026. E
            // mais rala e mais curta: no segundo teste, ao ar livre, depois do terceiro golpe a nuvem escondia o Brutamonte)
            float tom = Mathf.Lerp(1f, 0.62f, CicloDoDia.Noite);
            Color Poeira()
            {
                var c = Color.Lerp(new Color(0.55f, 0.51f, 0.45f), new Color(0.66f, 0.62f, 0.55f), Random.value) * tom;
                c.a = 0.6f;
                return c;
            }
            var nuvem = _instancia._nuvemDoGolpe;
            var p = new ParticleSystem.EmitParams();
            // a onda: um anel saindo rápido, rente ao chão, que freia e cresce
            int anel = Mathf.RoundToInt(20f * forca);
            float giro = Random.value * 360f;
            for (int i = 0; i < anel; i++)
            {
                float a = (giro + i * 360f / anel + Random.Range(-6f, 6f)) * Mathf.Deg2Rad;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                p.position = ponto + dir * Random.Range(0.25f, 0.5f) + Vector3.up * Random.Range(0.12f, 0.3f);
                p.velocity = dir * (Random.Range(3.5f, 6.5f) * forca) + Vector3.up * Random.Range(0.1f, 0.5f);
                p.startSize = Random.Range(0.45f, 0.9f) * forca;
                p.startLifetime = Random.Range(1.0f, 1.7f);
                p.startColor = Poeira();
                nuvem.Emit(p, 1);
            }
            // o bafo do meio, subindo devagar
            int meio = Mathf.RoundToInt(6f * forca);
            for (int i = 0; i < meio; i++)
            {
                Vector2 r = Random.insideUnitCircle * 0.35f;
                p.position = ponto + new Vector3(r.x, 0.2f, r.y);
                p.velocity = new Vector3(r.x * 2.2f, Random.Range(0.7f, 1.8f) * forca, r.y * 2.2f);
                p.startSize = Random.Range(0.6f, 1.1f) * forca;
                p.startLifetime = Random.Range(1.2f, 2.0f);
                p.startColor = Poeira();
                nuvem.Emit(p, 1);
            }
            // os torrões
            var terra = _instancia._terraDoGolpe;
            var q = new ParticleSystem.EmitParams();
            int torroes = Mathf.RoundToInt(34f * forca);
            for (int i = 0; i < torroes; i++)
            {
                Vector2 r = Random.insideUnitCircle;
                var dir = new Vector3(r.x, 0f, r.y);
                q.position = ponto + dir * 0.4f + Vector3.up * 0.06f;
                q.velocity = dir * (Random.Range(1f, 3.4f) * forca) + Vector3.up * (Random.Range(2.4f, 5.2f) * forca);
                q.startSize = Random.Range(0.025f, 0.075f);
                q.startLifetime = Random.Range(1.1f, 1.8f);
                q.startColor = Color.Lerp(new Color(0.15f, 0.12f, 0.09f), new Color(0.30f, 0.25f, 0.19f), Random.value) * Mathf.Lerp(1f, 0.6f, CicloDoDia.Noite);
                terra.Emit(q, 1);
            }
        }

        static AudioClip Gerar(string nome, float duracao, System.Func<float, System.Random, float> onda)
        {
            const int taxa = 44100;
            int n = Mathf.RoundToInt(duracao * taxa);
            var dados = new float[n];
            var r = new System.Random(nome.GetHashCode());
            float filtro = 0f;
            for (int i = 0; i < n; i++)
            {
                filtro = Mathf.Lerp(filtro, onda(i / (float)taxa, r), 0.35f);   // passa-baixa simples: som mais grave
                dados[i] = filtro;
            }
            return SomGerado.Clip(nome, dados, taxa);
        }

        ParticleSystem Particulas(string nome, Color cor, float tamanho, float vida, float velocidade)
        {
            var go = new GameObject(nome);
            go.transform.SetParent(transform);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false; main.loop = false; main.startLifetime = vida; main.startSpeed = velocidade;
            main.startSize = tamanho; main.startColor = cor; main.gravityModifier = 0.6f; main.maxParticles = 300;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emissao = ps.emission; emissao.enabled = false;
            var forma = ps.shape; forma.shapeType = ParticleSystemShapeType.Cone; forma.angle = 35f; forma.radius = 0.01f;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = materialParticulas;
            return ps;
        }

        // os sons gravados (19/11/2026: Combat/SonsGravados). Desde 20/11/2026, sem a reserva feita em código: os feitos aqui
        // que eles trocavam saíram (o pente, a bomba, o cartucho, o ferrolho, a luneta, o soco, o clique, as mordidas, os
        // goles, a lanterna, o interruptor e os passos de cada chão); sem a gravação, null, e o Som não toca
        static void PorOsGravados()
        {
            Carregador = SonsGravados.Manuseio("carregador_entra_rifle");
            Bomba = SonsGravados.Manuseio("bomba");
            Cartucho = SonsGravados.Manuseio("cartucho");
            Ferrolho = SonsGravados.Manuseio("ferrolho");
            LunetaEntra = SonsGravados.Manuseio("luneta_entra");
            LunetaSai = SonsGravados.Manuseio("luneta_sai");
            Capsulas = SonsGravados.Capsulas;
            SocoVento = SonsGravados.Golpes("SocoVento");
            SocoCarne = SonsGravados.Golpes("SocoCarne");
            SocosNaParede = SonsGravados.Golpes("SocoParede");
            // (20/11/2026, os do sfxjar que o Pedro mandou: a arma sem bala, as mordidas e os goles)
            Clique = SonsGravados.Manuseio("arma_clique");
            Mastigar = SonsGravados.Varios("Comida", "mastigar_");
            Gole = SonsGravados.Varios("Comida", "gole_");
            // (20/11/2026) os cliques gravados: o da lanterna e o dos interruptores (o bunker, o capô)
            var cliques = SonsGravados.Varios("Interface", "clique_");
            Lanterna = cliques != null ? cliques[0] : null;
            Chave = cliques != null ? cliques[cliques.Length > 1 ? 1 : 0] : null;
            // os passos: os gravados de cada chão; só o carpete, que não tem gravação, continua o feito em código (SomDePasso)
            int chaos = System.Enum.GetValues(typeof(Superficie)).Length;
            Passos = new AudioClip[chaos][][];
            for (int s = 0; s < chaos; s++)
            {
                Passos[s] = new AudioClip[2][];
                for (int c = 0; c < 2; c++)
                    Passos[s][c] = SonsGravados.Passos((Superficie)s, c == 1)
                                   ?? ((Superficie)s == Superficie.Carpete ? SomDePasso.Gerar(Superficie.Carpete, c == 1) : null);
            }
        }

        /// <summary>A arma que emperrou (20/11/2026: o gatilho e o ferrolho travado, gravados; sem eles, o clique).</summary>
        public static AudioClip ArmaEmperrada => SonsGravados.Manuseio("arma_emperrada") ?? Clique;

        /// <summary>As mordidas (20/11/2026): o salgadinho crocante; o resto, as mordidas de sempre.</summary>
        public static AudioClip[] MastigarDe(Loot.Item item) =>
            item == Loot.Item.Salgadinho ? SonsGravados.Varios("Comida", "crocante_") ?? Mastigar : Mastigar;

        /// <summary>Os goles da bebida (20/11/2026): o suco e o copo de água pelo canudo; o resto, os goles da garrafa.</summary>
        public static AudioClip[] GoleDe(Loot.Item item) =>
            item == Loot.Item.Suco || item == Loot.Item.CopoDeAgua ? SonsGravados.Varios("Comida", "canudo_") ?? Gole : Gole;

        /// <summary>O pente saindo da arma (o da pistola e da submetralhadora, ou o do fuzil).</summary>
        public static AudioClip CarregadorSai(TipoDeArma a) => SonsGravados.Manuseio(DePistola(a) ? "carregador_sai_pistola" : "carregador_sai_rifle") ?? Carregador;

        /// <summary>O pente entrando na arma.</summary>
        public static AudioClip CarregadorEntra(TipoDeArma a) => SonsGravados.Manuseio(DePistola(a) ? "carregador_entra_pistola" : "carregador_entra_rifle") ?? Carregador;

        /// <summary>A alavanca (ou o ferrolho da pistola) puxada no fim da recarga com a arma vazia (null sem os gravados).</summary>
        public static AudioClip Engatilhar(TipoDeArma a) => SonsGravados.Manuseio(DePistola(a) ? "engatilhar_pistola" : "engatilhar_rifle");

        static bool DePistola(TipoDeArma a) { var r = Armas.Real(a); return r == TipoDeArma.Pistola || r == TipoDeArma.Submetralhadora; }

        /// <summary>O golpe de uma arma branca que pegou (a lâmina corta; o bastão bate; a tocha e o resto, a pancada).</summary>
        public static AudioClip[] GolpeDaArma(Loot.Item item) =>
            item == Loot.Item.Faca || item == Loot.Item.Machado || item == Loot.Item.Lanca ? SonsGravados.Golpes("Corte") ?? SocoCarne
            : item == Loot.Item.Bastao ? SonsGravados.Golpes("Bastao") ?? SocoCarne : SocoCarne;

        /// <summary>
        /// Toca um som. Espacial = 3D no ponto, ouvido até o alcance e mais abafado quanto mais longe; senão, sai
        /// "dentro da cabeça" (o próprio tiro).
        /// </summary>
        public static void Som(AudioClip clip, Vector3 posicao, float volume = 1f, bool espacial = true, float alcance = 60f)
        {
            if (_instancia == null || clip == null) return;
            var cam = Camera.main;
            // a porta do bunker fechada separa quem ouve do som
            if (espacial && cam != null && BunkerRede.Isolado(cam.transform.position, posicao)) return;
            float pitch = Random.Range(0.94f, 1.06f);
            var s = _instancia.FonteLivre(clip.length / pitch);
            s.transform.position = posicao;
            s.spatialBlend = espacial ? 1f : 0f;
            s.maxDistance = alcance;
            float longe = espacial && cam != null ? Mathf.Clamp01(Vector3.Distance(cam.transform.position, posicao) / alcance) : 0f;
            float corte = 22000f * Mathf.Pow(0.08f, longe);
            // com parede, laje ou o telhado no meio, abafado e mais baixo (Abafamento)
            float tapado = espacial && cam != null ? Abafamento.Quanto(cam.transform.position, posicao) : 0f;
            if (tapado > 0f)
            {
                corte = Mathf.Min(corte, Abafamento.Corte(tapado));
                volume *= Mathf.Lerp(1f, Abafamento.VolumeTapado, tapado);
            }
            s.GetComponent<AudioLowPassFilter>().cutoffFrequency = corte;
            s.pitch = pitch;
            s.PlayOneShot(clip, volume);
        }

        AudioSource NovaFonte()
        {
            var go = new GameObject("Som");
            go.transform.SetParent(transform);
            var s = go.AddComponent<AudioSource>();
            s.playOnAwake = false; s.rolloffMode = AudioRolloffMode.Linear; s.minDistance = 2f; s.maxDistance = 60f;
            go.AddComponent<AudioLowPassFilter>().cutoffFrequency = 22000f;
            return s;
        }

        // uma fonte que já terminou o som dela; sem nenhuma, uma nova (até 64); com todas ocupadas, a que termina primeiro,
        // parada antes (o som velho acaba ali, em vez de pular para o lugar do novo)
        AudioSource FonteLivre(float duracao)
        {
            float agora = Time.unscaledTime;
            AudioSource livre = null, antes = null;
            float maisCedo = float.MaxValue;
            for (int i = 0; i < _fontes.Count; i++)
            {
                var f = _fontes.Dequeue(); _fontes.Enqueue(f);
                if (f == null) continue;
                float ate = _ocupadaAte.TryGetValue(f, out float t) ? t : 0f;
                if (ate <= agora) { livre = f; break; }
                if (ate < maisCedo) { maisCedo = ate; antes = f; }
            }
            if (livre == null && _fontes.Count < FontesNoMaximo) { livre = NovaFonte(); _fontes.Enqueue(livre); }
            if (livre == null) { livre = antes; livre.Stop(); }
            _ocupadaAte[livre] = agora + duracao + 0.05f;
            return livre;
        }

        /// <summary>Toca uma das variações, sorteada.</summary>
        public static void Som(AudioClip[] clips, Vector3 posicao, float volume = 1f, bool espacial = true, float alcance = 60f)
        {
            if (clips == null || clips.Length == 0) return;
            Som(clips[Random.Range(0, clips.Length)], posicao, volume, espacial, alcance);
        }

        /// <summary>Clarão do cano: uma luz de um instante e um brilho na boca da arma (na camada informada).</summary>
        public static void Clarao(Transform boca, int camada) => Clarao(boca, camada, null);

        /// <summary>
        /// O clarão com o brilho virado para a câmera <paramref name="olho"/> (null: a do mundo). A dos braços de primeira
        /// pessoa, que ficam perto da origem (<see cref="Player.BracosNaOrigem"/>): lá, a luz dele só ilumina os braços, e a do
        /// mundo vem da <see cref="LuzDoClarao"/>.
        /// </summary>
        public static void Clarao(Transform boca, int camada, Camera olho)
        {
            if (_instancia == null || boca == null) return;
            _instancia.StartCoroutine(_instancia.ClaraoRotina(boca, camada, olho));
        }

        System.Collections.IEnumerator ClaraoRotina(Transform boca, int camada, Camera olho)
        {
            var (go, luz, brilho) = PegarClarao();
            go.layer = camada;
            brilho.gameObject.layer = camada;
            go.transform.SetPositionAndRotation(boca.position, boca.rotation);
            luz.intensity = 5f;
            go.SetActive(true);
            for (int i = 0; i < 3; i++)
            {
                if (boca != null) go.transform.position = boca.position;
                var cam = olho != null ? olho : Camera.main;
                if (cam != null) brilho.rotation = Quaternion.LookRotation(brilho.position - cam.transform.position) * Quaternion.Euler(0, 0, Random.Range(0f, 360f));
                luz.intensity *= 0.6f;
                yield return null;
            }
            if (go != null) go.SetActive(false);
        }

        /// <summary>O nome da luz do clarão no mundo (a <see cref="Player.BracosNaOrigem"/> não a copia para os braços).</summary>
        public const string NomeDaLuzDoClaraoNoMundo = "Clarão (a luz no mundo)";

        /// <summary>
        /// Só a luz do clarão, num ponto do mundo: a do tiro em primeira pessoa, na frente da câmera, onde a boca da arma
        /// estaria (o clarão dos braços fica com eles, perto da origem).
        /// </summary>
        public static void LuzDoClarao(Vector3 ponto)
        {
            if (_instancia == null) return;
            _instancia.StartCoroutine(_instancia.LuzDoClaraoRotina(ponto));
        }

        readonly List<Light> _luzesDosClaroes = new List<Light>();

        System.Collections.IEnumerator LuzDoClaraoRotina(Vector3 ponto)
        {
            Light luz = null;
            for (int i = _luzesDosClaroes.Count - 1; i >= 0; i--)
            {
                if (_luzesDosClaroes[i] == null) _luzesDosClaroes.RemoveAt(i);
                else if (!_luzesDosClaroes[i].gameObject.activeSelf) { luz = _luzesDosClaroes[i]; break; }
            }
            if (luz == null)
            {
                var novo = new GameObject(NomeDaLuzDoClaraoNoMundo);
                novo.SetActive(false);
                luz = novo.AddComponent<Light>();
                luz.type = LightType.Point; luz.color = new Color(1f, 0.75f, 0.4f); luz.range = 6f; luz.shadows = LightShadows.None;
                _luzesDosClaroes.Add(luz);
            }
            luz.transform.position = ponto;
            luz.intensity = 5f;
            luz.gameObject.SetActive(true);
            for (int i = 0; i < 3; i++)
            {
                luz.intensity *= 0.6f;
                yield return null;
            }
            if (luz != null) luz.gameObject.SetActive(false);
        }

        // os clarões guardados (31/10/2026: eram um objeto novo, com a luz e o quadrado, a cada tiro, ~10 por segundo numa
        // automática, em todas as máquinas); um livre é reaproveitado, e só faltando um é feito outro
        readonly List<(GameObject go, Light luz, Transform brilho)> _claroes = new List<(GameObject, Light, Transform)>();

        (GameObject go, Light luz, Transform brilho) PegarClarao()
        {
            for (int i = _claroes.Count - 1; i >= 0; i--)
            {
                var c = _claroes[i];
                if (c.go == null) _claroes.RemoveAt(i);
                else if (!c.go.activeSelf) return c;
            }
            var go = new GameObject("Clarão");
            go.SetActive(false);
            var luz = go.AddComponent<Light>();
            luz.type = LightType.Point; luz.color = new Color(1f, 0.75f, 0.4f); luz.intensity = 5f; luz.range = 6f; luz.shadows = LightShadows.None;
            var brilho = GameObject.CreatePrimitive(PrimitiveType.Quad);
            // na hora: com Destroy o colisor só sumiria no fim do quadro, e o raio do tiro, feito logo depois, podia batê-lo
            DestroyImmediate(brilho.GetComponent<Collider>());
            brilho.transform.SetParent(go.transform, false);
            brilho.transform.localScale = Vector3.one * 0.12f;
            var r = brilho.GetComponent<MeshRenderer>();
            r.sharedMaterial = materialClarao; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var novo = (go, luz, brilho.transform);
            _claroes.Add(novo);
            return novo;
        }

        /// <summary>Marca de tiro no cenário: faíscas e poeira saindo da superfície.</summary>
        /// <summary>As lascas da madeira saindo do corte (o machado na árvore, 18/11/2026), para o lado da normal.</summary>
        public static void Lascas(Vector3 ponto, Vector3 normal, int quantas = 14)
        {
            if (_instancia == null || _instancia._lascas == null) return;
            _instancia._lascas.transform.SetPositionAndRotation(ponto + normal * 0.03f, Quaternion.LookRotation(normal));
            _instancia._lascas.Emit(quantas);
        }

        public static void Impacto(Vector3 ponto, Vector3 normal)
        {
            if (_instancia == null) return;
            var giro = Quaternion.LookRotation(normal);
            _instancia._faiscas.transform.SetPositionAndRotation(ponto + normal * 0.02f, giro);
            _instancia._faiscas.Emit(8);
            _instancia._poeira.transform.SetPositionAndRotation(ponto + normal * 0.02f, giro);
            _instancia._poeira.Emit(10);
        }
    }
}
