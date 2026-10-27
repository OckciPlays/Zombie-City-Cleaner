using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ZombieCityCleaner.Mundo
{
    /// <summary>
    /// A cena de testes do abandono das cidades (Plano_Mapa, Parte M9b; para o usuário aprovar antes de ir para o jogo):
    /// 4 trechos de rua lado a lado, cada um com as mesmas construções do kit (3 casas, uma loja e um prédio de 6 andares)
    /// e a rua, num grau de abandono: como está no jogo hoje (o material da cidade, sem nada), pouco, médio e muito.
    /// Em cada trecho, com a intensidade k dele:
    ///  - o material do abandono (shader ZombieCityCleaner/Abandono) nas construções, na rua e nos objetos, com k e a altura
    ///    do chão por objeto (um MaterialPropertyBlock);
    ///  - as janelas de fora (<see cref="AbandonoDaObra.Janelas"/>: quebradas e com tábuas), a hera
    ///    (<see cref="AbandonoDaObra.Hera"/>, com o material da vegetação) e as pichações
    ///    (<see cref="AbandonoDaObra.Pichacoes"/>, com o material delas);
    ///  - o mato: a grama e o capim nos quintais (mais alto e mais fechado com k), os arbustos e as árvores novas nascendo,
    ///    as plantas nas frestas das calçadas e na sarjeta, e a mata chegando atrás dos lotes;
    ///  - o lixo: os sacos, os tijolos, as caixas de papelão e a caçamba; os carros parados (a ferrugem do shader).
    /// As plantas são desenhadas em instâncias (Graphics.RenderMeshInstanced a cada câmera), sem um objeto por tufo. A
    /// neblina de altura do jogo (o passe de tela cheia, que lê os globais do AmbienteDoDia) fica leve para ver os
    /// detalhes, ou a do jogo de dia com <see cref="neblinaDoJogo"/>.
    /// Montada por Editor/MontagemDoExemploDasCasas (menu Zombie City Cleaner > Exemplos > Abandono (Parte M9b)).
    /// </summary>
    [ExecuteAlways]
    public class CenaDoAbandono : CenaDeExemplo
    {
        [Tooltip("O material da cidade de hoje (o trecho de comparação).")]
        public Material materialDeHoje;
        [Tooltip("O material da vegetação (as plantas e a hera).")]
        public Material vegetacao;
        [Tooltip("O material das pichações (o atlas T_Pichacoes).")]
        public Material pichacao;
        public Mesh cacamba, sacos, lixeira;
        [Tooltip("As plantas do Cidade_Arvores.fbx: a grama, o capim, os arbustos e as árvores (os nomes de Floresta.Nomes).")]
        public Mesh gramaA, gramaB, capimA, capimB, arbustoA, arbustoB, arbustoC, abetoA, abetoB, abetoC, pinheiroBranco, bordoVerde, bordoVermelho, betula, morta;
        [Tooltip("A neblina do jogo de dia (densidade 0,032 e a de altura 0,07); sem, uma leve, para ver os detalhes.")]
        public bool neblinaDoJogo;

        public static readonly (string nome, float k)[] Trechos =
        {
            ("Hoje (como está no jogo)", 0f), ("Pouco abandono", 0.33f), ("Médio abandono", 0.66f), ("Muito abandono", 1f),
        };
        public const float Comprimento = 75f, Entre = 12f;

        protected override string Chave => $"{semente}|abandono|{neblinaDoJogo}";

        // as plantas em instâncias, por malha
        readonly Dictionary<Mesh, List<Matrix4x4>> _plantas = new Dictionary<Mesh, List<Matrix4x4>>();
        readonly List<Rect> _construcoes = new List<Rect>();
        MaterialPropertyBlock _bloco;
        System.Random _giro;

        // (a base já tem o OnEnable e o OnDisable, que geram e apagam: o desenho das plantas é ligado no Montar)
        void OnDestroy() => RenderPipelineManager.beginCameraRendering -= Desenhar;

        /// <summary>Onde começa o trecho t, em x.</summary>
        public static float InicioDoTrecho(int t) => t * (Comprimento + Entre);

        public override string Texto(Vector3 posicao)
        {
            int t = Mathf.Clamp(Mathf.FloorToInt(posicao.x / (Comprimento + Entre)), 0, Trechos.Length - 1);
            return $"{Trechos[t].nome} (k = {Trechos[t].k:0.00})\n\n{Resumo}";
        }

        protected override void Montar()
        {
            RenderPipelineManager.beginCameraRendering -= Desenhar;
            RenderPipelineManager.beginCameraRendering += Desenhar;
            _plantas.Clear();
            _giro = new System.Random(semente * 3 + 1);
            var abandono = material;
            for (int t = 0; t < Trechos.Length; t++)
            {
                float k = Trechos[t].k, x0 = InicioDoTrecho(t);
                material = t == 0 && materialDeHoje != null ? materialDeHoje : abandono;
                var trecho = Novo(Trechos[t].nome, raiz).transform;
                var antes = raiz;
                raiz = trecho;
                chao = new Obra(semente + t, Medidas.DoJogo);
                buracos.Clear();
                _construcoes.Clear();
                // as mesmas construções em todos os trechos (a mesma semente), para comparar
                var r = new System.Random(semente);
                float xn = x0 + 3f, xs = x0 + 3f;
                for (int i = 0; i < 3; i++)
                {
                    var planta = PlantasDeCasa.Sortear(new System.Random(Misturar(semente, i, 977)), Medidas.DoJogo);
                    if (k > 0f) planta.janelas = new[] { 1, 0 };
                    var casa = new Casa(planta, Medidas.DoJogo);
                    casa.Montar();
                    var pichCasa = new MalhaDePichacao();
                    AbandonoDaObra.Janelas(casa, k, planta.semente, pichCasa);
                    int lado = i < 2 ? 1 : -1;
                    float x = lado > 0 ? xn : xs;
                    float fim = PorCasa(casa, $"Casa {i + 1}", x, lado, out var go);
                    if (lado > 0) xn = fim; else xs = fim;
                    PorHera(go, casa, k, planta.semente, planta.espelhar, 4f);
                    PorPichacoes(go, casa, k, planta.semente, planta.espelhar, pichCasa);
                    Guardar(go);
                }
                // a loja no fim do lado norte e o prédio no lado sul
                var pl = PlantasDeLoja.ParaOLote(new System.Random(semente + 11), Medidas.DoJogo, TipoDeLoja.Comum, 13f, 16f, 2, "ParedeBege");
                var loja = new Loja(pl, Medidas.DoJogo);
                loja.Montar((x, z) => 0f, 0f);
                var pichLoja = new MalhaDePichacao();
                AbandonoDaObra.Janelas(loja, k, pl.semente, pichLoja);
                var goLoja = PorConstrucao(loja.vis, loja.col, pl.W, pl.D, pl.espelhar, "Loja", xn + 1f, 1);
                PorHera(goLoja, loja, k, pl.semente, pl.espelhar, 5f);
                PorPichacoes(goLoja, loja, k, pl.semente, pl.espelhar, pichLoja);
                Guardar(goLoja);
                var pp = PlantasDePredio.ParaOLote(new System.Random(semente + 23), Medidas.DoJogo, 16f, 15f, 6, "Tijolo");
                var predio = new Predio(pp, Medidas.DoJogo, -1);
                predio.MontarCasca((x, z) => 0f, 0f);
                var pichPredio = new MalhaDePichacao();
                AbandonoDaObra.Janelas(predio, k, pp.semente, pichPredio);
                var goPredio = PorConstrucao(predio.vis, predio.col, pp.W, pp.D, pp.espelhar, "Prédio", xs + 1f, -1);
                for (int f = 0; f < predio.Escuros.Count; f++)
                {
                    var esc = Novo($"Janelas escuras do andar {f}", goPredio.transform);
                    esc.AddComponent<MeshFilter>().sharedMesh = Malha(predio.Escuros[f], $"Escuros {f}", pp.espelhar);
                    esc.AddComponent<MeshRenderer>().sharedMaterial = material;
                }
                PorHera(goPredio, predio, k, pp.semente, pp.espelhar, 7f);
                PorPichacoes(goPredio, predio, k, pp.semente, pp.espelhar, pichPredio);
                Guardar(goPredio);

                RuaDoTrecho(x0, x0 + Comprimento, k, r);
                Intensidade(trecho.gameObject, k);
                raiz = antes;
            }
            material = abandono;
            resumo.AppendLine($"4 trechos de {Comprimento:0} m: {string.Join(", ", System.Array.ConvertAll(Trechos, t => t.nome))}.");
        }

        // a loja ou o prédio no lote de frente para a rua, com a frente na calçada (lado 1: o norte; -1: o sul, girado)
        GameObject PorConstrucao(MalhaDeObra vis, MalhaDeObra col, float W, float D, bool espelhar, string nome, float x, int lado)
        {
            float cx = x + W / 2f, cz = lado * (Calcada + 0.4f + D / 2f);
            var go = Objeto(nome, raiz, Malha(vis, nome, espelhar), Malha(col, nome + " (colisão)", espelhar), new Vector3(cx, 0f, cz), lado > 0 ? 0f : 180f);
            Construcoes.Add(go);
            triangulos += vis.Triangulos;
            resumo.AppendLine($"{nome}: {W:0.0} × {D:0.0} m, {vis.Triangulos:N0} triângulos");
            return go;
        }

        void PorHera(GameObject go, Obra obra, float k, int sementeDaObra, bool espelhar, float alturaMax)
        {
            if (k <= 0f || vegetacao == null) return;
            var hera = AbandonoDaObra.Hera(obra, k, sementeDaObra, alturaMax);
            if (hera.Folhas == 0) return;
            var h = Novo("Hera", go.transform);
            var m = hera.ParaMesh("Hera", espelhar);
            m.hideFlags = HideFlags.DontSave;
            h.AddComponent<MeshFilter>().sharedMesh = m;
            h.AddComponent<MeshRenderer>().sharedMaterial = vegetacao;
            triangulos += hera.Folhas * 2;
        }

        // as pichações das paredes, somadas às das chapas das janelas ('pich')
        void PorPichacoes(GameObject go, Obra obra, float k, int sementeDaObra, bool espelhar, MalhaDePichacao pich)
        {
            if (k <= 0f || pichacao == null) return;
            AbandonoDaObra.Pichacoes(obra, k, sementeDaObra, pich);
            if (pich.Quadros == 0) return;
            var g = Novo("Pichações", go.transform);
            var m = pich.ParaMesh("Pichações", espelhar);
            m.hideFlags = HideFlags.DontSave;
            g.AddComponent<MeshFilter>().sharedMesh = m;
            var r = g.AddComponent<MeshRenderer>();
            r.sharedMaterial = pichacao;
            r.shadowCastingMode = ShadowCastingMode.Off;
            triangulos += pich.Quadros * 2;
        }

        // o retângulo da construção no chão (para o mato não nascer dentro)
        void Guardar(GameObject go)
        {
            var b = go.GetComponent<Renderer>().bounds;
            _construcoes.Add(Rect.MinMaxRect(b.min.x - 0.6f, b.min.z - 0.6f, b.max.x + 0.6f, b.max.z + 0.6f));
        }

        bool Livre(float x, float z)
        {
            foreach (var c in _construcoes) if (c.Contains(new Vector2(x, z))) return false;
            return true;
        }

        // a intensidade e o chão de cada objeto do trecho com o material do abandono
        void Intensidade(GameObject trecho, float k)
        {
            _bloco ??= new MaterialPropertyBlock();
            _bloco.SetFloat("_Intensidade", k);
            _bloco.SetFloat("_Chao", 0f);
            foreach (var r in trecho.GetComponentsInChildren<MeshRenderer>(true))
                if (r.sharedMaterial != vegetacao && r.sharedMaterial != pichacao) r.SetPropertyBlock(_bloco);
        }

        // ------------------------------------------------------------------ a rua, o mato e o lixo

        void RuaDoTrecho(float xa, float xb, float k, System.Random rnd)
        {
            // a grama com o buraco da rua e das calçadas (sem ele, ela fica no mesmo plano do asfalto e as duas piscam)
            buracos.Add(Rect.MinMaxRect(xa - Entre / 2f, -Calcada, xb + Entre / 2f, Calcada));
            chao.Laje(xa - Entre / 2f, xb + Entre / 2f, -60f, 60f, 0f, 0.3f, "Grama", true, buracos);
            float meio = (xa + xb) / 2, comp = xb - xa + Entre;
            chao.Caixa(new Vector3(meio, -0.15f, 0f), new Vector3(comp, 0.3f, 8f), "Asfalto", colide: true);
            for (float x = xa - Entre / 2f + 1.5f; x < xb + Entre / 2f; x += 6f) chao.Caixa(new Vector3(x, 0.003f, 0f), new Vector3(3f, 0.006f, 0.15f), "Faixa");
            foreach (int lado in new[] { 1, -1 })
            {
                chao.Caixa(new Vector3(meio, 0f, lado * 4.075f), new Vector3(comp, 0.3f, 0.15f), "MeioFio", colide: true);
                chao.Caixa(new Vector3(meio, -0.09f, lado * 5.325f), new Vector3(comp, 0.3f, 2.35f), "Calcada", colide: true);
                for (float x = xa - Entre / 2f; x < xb + Entre / 2f; x += 2f) chao.Caixa(new Vector3(x, 0.064f, lado * 5.325f), new Vector3(0.02f, 0.12f, 2.35f), "Concreto");
            }
            // o lixo no chão: os tijolos e as caixas de papelão (na calçada e na sarjeta)
            int tijolos = Mathf.RoundToInt(14 * k), caixas = Mathf.RoundToInt(6 * k);
            for (int i = 0; i < tijolos + caixas; i++)
            {
                int lado = rnd.NextDouble() < 0.5 ? 1 : -1;
                float x = Mathf.Lerp(xa, xb, (float)rnd.NextDouble()), z = lado * Mathf.Lerp(3.2f, 6.2f, (float)rnd.NextDouble());
                float y = Mathf.Abs(z) < 4f ? 0f : 0.06f;
                if (i < tijolos)
                    chao.Caixa(new Vector3(x, y + 0.04f, z), new Vector3(0.23f, 0.075f, 0.11f), rnd.NextDouble() < 0.5 ? "Tijolo" : "TijoloEscuro", (float)rnd.NextDouble() * 360f);
                else
                {
                    float t = 0.35f + 0.3f * (float)rnd.NextDouble();
                    chao.Caixa(new Vector3(x, y + t * 0.35f, z), new Vector3(t, t * 0.7f, t * 0.8f), "Papelao", (float)rnd.NextDouble() * 360f);
                }
            }
            Objeto("Chão (rua, calçadas e grama)", raiz, Malha(chao.vis, "Chão"), Malha(chao.col, "Chão (colisão)"), Vector3.zero, 0f);

            // os carros parados e os objetos da rua
            if (carros != null && carros.Length > 0)
                for (int i = 0; i < 2; i++)
                {
                    int lado = i % 2 == 0 ? 1 : -1;
                    float x = Mathf.Lerp(xa + 15f, xb - 10f, (float)rnd.NextDouble());
                    Objeto("Carro parado", raiz, carros[rnd.Next(carros.Length)], null, new Vector3(x, 0f, lado * 2.9f), lado > 0 ? 90f : 270f);
                }
            if (poste != null)
                for (float x = xa + 8f, n = 0; x < xb; x += 22f, n++)
                {
                    int lado = ((int)n % 2 == 0) ? 1 : -1;
                    Objeto("Poste", raiz, poste, null, new Vector3(x, 0.12f, lado * 5.9f), lado > 0 ? 180f : 0f);
                }
            if (lixeira != null) Objeto("Lixeira", raiz, lixeira, null, new Vector3(xa + 30f, 0.06f, 4.6f), 0f);
            if (sacos != null)
                for (int i = 0; i < 1 + Mathf.RoundToInt(3 * k); i++)
                    Objeto("Sacos", raiz, sacos, null, new Vector3(Mathf.Lerp(xa, xb, (float)rnd.NextDouble()), 0.06f, (rnd.NextDouble() < 0.5 ? 1 : -1) * 4.8f), (float)rnd.NextDouble() * 360f);
            if (cacamba != null && k >= 0.5f) Objeto("Caçamba", raiz, cacamba, null, new Vector3(xb - 12f, 0f, -3.1f), 90f);

            if (k <= 0f) return;
            // o mato: nas frestas da calçada e na sarjeta
            for (float x = xa; x < xb; x += 0.5f)
                foreach (int lado in new[] { 1, -1 })
                {
                    if (rnd.NextDouble() < 0.12 + 0.55 * k) PorPlanta(rnd.NextDouble() < 0.6 ? gramaA : capimB, x + R(rnd, 0.2f), 0f, lado * R2(rnd, 3.85f, 3.98f), R2(rnd, 0.6f, 0.9f + 0.6f * k));
                    if (rnd.NextDouble() < 0.06 + 0.4 * k) PorPlanta(rnd.NextDouble() < 0.7 ? gramaB : capimB, x + R(rnd, 0.2f), 0.06f, lado * R2(rnd, 6.1f, 6.45f), R2(rnd, 0.5f, 0.8f + 0.5f * k));
                }
            for (float x = xa - Entre / 2f; x < xb + Entre / 2f; x += 2f)
                foreach (int lado in new[] { 1, -1 })
                    if (rnd.NextDouble() < 0.1 + 0.6 * k) PorPlanta(gramaB, x + R(rnd, 0.03f), 0.06f, lado * R2(rnd, 4.3f, 6.3f), R2(rnd, 0.4f, 0.7f + 0.5f * k));
            // os quintais e os terrenos: a grama mais alta e fechada, o capim, os arbustos e as árvores novas
            int tufos = Mathf.RoundToInt(900 * k);
            for (int i = 0; i < tufos; i++)
            {
                int lado = rnd.NextDouble() < 0.5 ? 1 : -1;
                float x = Mathf.Lerp(xa - Entre / 2f, xb + Entre / 2f, (float)rnd.NextDouble()), z = lado * Mathf.Lerp(6.7f, 34f, (float)rnd.NextDouble());
                if (!Livre(x, z)) continue;
                double s = rnd.NextDouble();
                Mesh m = s < 0.5 ? gramaA : s < 0.7 ? gramaB : s < 0.88 ? capimA : s < 0.96 ? (k > 0.5f ? arbustoA : capimB) : (k > 0.6f ? arbustoC : gramaA);
                PorPlanta(m, x, 0f, z, R2(rnd, 0.9f, 1.2f + 0.5f * k));
            }
            int novas = Mathf.RoundToInt(10 * k * k);
            var arvoresNovas = new[] { abetoC, betula, bordoVerde, abetoC, pinheiroBranco };
            for (int i = 0; i < novas; i++)
            {
                int lado = rnd.NextDouble() < 0.5 ? 1 : -1;
                float x = Mathf.Lerp(xa, xb, (float)rnd.NextDouble()), z = lado * Mathf.Lerp(8f, 30f, (float)rnd.NextDouble());
                if (!Livre(x, z)) continue;
                PorPlanta(arvoresNovas[rnd.Next(arvoresNovas.Length)], x, 0f, z, R2(rnd, 0.35f, 0.75f));
            }
            // a mata chegando atrás dos lotes
            int mata = Mathf.RoundToInt(30 + 60 * k);
            var arvores = new[] { abetoA, abetoB, abetoB, pinheiroBranco, bordoVerde, bordoVermelho, betula, morta };
            for (int i = 0; i < mata; i++)
            {
                int lado = rnd.NextDouble() < 0.5 ? 1 : -1;
                float x = Mathf.Lerp(xa - Entre / 2f, xb + Entre / 2f, (float)rnd.NextDouble()), z = lado * Mathf.Lerp(36f - 10f * k, 60f, (float)rnd.NextDouble());
                if (!Livre(x, z)) continue;
                PorPlanta(arvores[rnd.Next(arvores.Length)], x, -0.1f, z, R2(rnd, 0.8f, 1.2f));
            }
        }

        static float R(System.Random rnd, float a) => ((float)rnd.NextDouble() * 2f - 1f) * a;
        static float R2(System.Random rnd, float a, float b) => a + (float)rnd.NextDouble() * (b - a);

        void PorPlanta(Mesh m, float x, float y, float z, float escala)
        {
            if (m == null) return;
            if (!_plantas.TryGetValue(m, out var lista)) _plantas[m] = lista = new List<Matrix4x4>();
            float giro = (float)_giro.NextDouble() * 360f;
            lista.Add(Matrix4x4.TRS(transform.TransformPoint(new Vector3(x, y, z)), Quaternion.Euler(0f, giro, 0f), Vector3.one * escala));
        }

        // as plantas em instâncias, em cada câmera (também nas fotos sem Play)
        void Desenhar(ScriptableRenderContext contexto, Camera cam)
        {
            if (this == null || !isActiveAndEnabled)
            {
                RenderPipelineManager.beginCameraRendering -= Desenhar;
                return;
            }
            // a neblina de altura (os globais que o AmbienteDoDia põe no jogo)
            Shader.SetGlobalColor("_NeblinaCor", new Color(0.63f, 0.65f, 0.67f, 1f));
            Shader.SetGlobalVector("_NeblinaParams", new Vector4(neblinaDoJogo ? 0.07f : 0.015f, 0.2f, 0.32f, 140f));
            RenderSettings.fogDensity = neblinaDoJogo ? 0.032f : 0.009f;
            if (vegetacao == null || _plantas.Count == 0) return;
            var rp = new RenderParams(vegetacao)
            {
                camera = cam,
                receiveShadows = true,
                shadowCastingMode = ShadowCastingMode.On,
                worldBounds = new Bounds(transform.position + new Vector3(Trechos.Length * (Comprimento + Entre) / 2f, 0f, 0f), new Vector3(500f, 100f, 200f)),
            };
            foreach (var kv in _plantas)
            {
                var lista = kv.Value;
                for (int i = 0; i < lista.Count; i += 1023)
                    Graphics.RenderMeshInstanced(rp, kv.Key, 0, lista, Mathf.Min(1023, lista.Count - i), i);
            }
        }
    }
}
