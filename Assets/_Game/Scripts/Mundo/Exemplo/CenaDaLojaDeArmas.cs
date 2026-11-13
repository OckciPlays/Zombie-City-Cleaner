using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using static ZombieCityCleaner.Mundo.PlantaDaLojaDeArmas;

namespace ZombieCityCleaner.Mundo
{
    /// <summary>
    /// A cena de teste da loja de armas (o Plano_LojaDeArmas.md da pasta de arte, 08/11/2026): a <see cref="LojaDeArmas"/> (a
    /// casca e o lado de dentro) no terreno dela (a calçada da frente no nível da porta, o estacionamento com as vagas e os
    /// frades, a calçada e a rua, os becos dos lados e dos fundos com o alambrado, a caçamba e os paletes, o letreiro de poste
    /// na rua), os móveis da mobília nova (com a variação da semente e o estado da sala segura), as marcas, as pichações, a
    /// hera, os carros e as árvores. As teclas: 1, 2 e 3 põem a sala segura no estado A, B ou C (e 0 volta ao da semente), R
    /// sorteia outra loja, L liga e desliga a lanterna e J toca um som. Sem energia: a luz de dentro é a do jogo, e o estande,
    /// a sala segura, o estoque e o depósito (sem janela) ficam escuros.
    /// Os papéis e os sons (do assembly do jogo) acompanham a variação pelo <see cref="Gerada"/>.
    /// </summary>
    [ExecuteAlways]
    public class CenaDaLojaDeArmas : CenaDeExemplo
    {
        /// <summary>O estado da sala segura: o da semente ou um dos três.</summary>
        public enum Sala { DaSemente, A_Saqueada, B_Parcial, C_Intacta }

        public Sala sala = Sala.DaSemente;
        [Range(0f, 1f)] public float luzDentro = 1f, luzNoEstoque = 0.35f, luzNoEstande = 0.1f, luzNaSalaSegura = 0.06f;
        [Range(0f, 0.1f)] public float neblinaDeFora = 0.012f;
        [Range(0f, 1f)] public float abandono = 0.9f;
        public Material vegetacao;
        public Mesh arbustoA, arbustoB, arbustoC, lixeira, cacamba;
        [Tooltip("Os prefabs da mobília nova (Prefabs/MobiliaNova: com o lote da loja de armas).")]
        public GameObject[] mobilia;
        public Material materialDasMarcas, materialDasPichacoes;

        /// <summary>Avisa quando a loja foi gerada de novo (os papéis e os sons, que são do assembly do jogo, refazem os deles).</summary>
        public static event System.Action<CenaDaLojaDeArmas> Gerada;

        /// <summary>A variação de agora (a semente e o estado da sala segura).</summary>
        public Variacao Variacao { get; private set; }

        protected override string Chave => $"{semente}:{sala}";

        const float ForcaDaLanterna = 34f, AberturaDaLanterna = 62f, AberturaInternaDaLanterna = 52f, AlcanceDaLanterna = 28f;
        static readonly Vector3 LanternaNaCamera = new Vector3(0.1f, -0.3f, 0.15f);
        Light _lanterna;
        bool _lanternaLigada = true;
        int _marcas, _pichacoes;

        protected override void Montar()
        {
            EstadoDaSala? forcado = sala == Sala.DaSemente ? (EstadoDaSala?)null : (EstadoDaSala)((int)sala - 1);
            Variacao = Sortear(semente, forcado);
            var v = Variacao;
            var M = Medidas.DoJogo;
            _marcas = _pichacoes = 0;
            var go = Novo("Loja de armas", raiz).transform;
            var casca = new LojaDeArmas(M, LojaDeArmas.Parte.Casca, v);
            casca.Montar();
            int triCasca = casca.vis.Triangulos;
            Hera(casca, go);
            Parte(casca, "Loja de armas (casca)", go);
            var dentro = new LojaDeArmas(M, LojaDeArmas.Parte.Dentro, v);
            dentro.Montar();
            int triDentro = dentro.vis.Triangulos;
            Parte(dentro, "Loja de armas (dentro)", go);
            int moveis = Moveis(go, v);
            Terreno();
            Objetos();
            Objeto("Chão (o terreno, o estacionamento e a rua)", raiz, Malha(chao.vis, "Chão"), Malha(chao.col, "Chão (colisão)"), Vector3.zero, 0f);
            Carros();
            Arvores();
            Arbustos();
            Abandono();
            Paradas.Clear();
            ParadasDaLoja();
            _zona = -1;
            LuzDeFora();
            resumo.AppendLine($"Loja de armas: 24 × 30 m, o estande de 5 baias; semente {semente}, a sala segura {NomesDosEstados[(int)v.sala]}" +
                              $"{(v.barricada ? ", a barricada da entrada" : "")}{(v.acampamento ? ", o acampamento no estande" : "")}" +
                              $"{(v.gondolaTombada ? ", a gôndola tombada" : "")}{(v.baiaQuebrada >= 0 ? $", a baia {v.baiaQuebrada + 1} quebrada" : "")}, " +
                              $"{v.vitrinesQuebradas} balcões quebrados; sem energia; {moveis} móveis, {_marcas} marcas, {_pichacoes} pichações; " +
                              $"triângulos: casca {triCasca:N0}, dentro {triDentro:N0} (total {triangulos:N0})");
            if (_faltam.Count > 0) resumo.AppendLine($"Faltam os modelos (monte a mobília nova): {string.Join(", ", _faltam)}");
            Gerada?.Invoke(this);
        }

        // ------------------------------------------------------------ as partes e os móveis

        void Parte(LojaDeArmas l, string nome, Transform pai)
        {
            Objeto(nome, pai, Malha(l.vis, nome), Malha(l.col, nome + " (colisão)"), Vector3.zero, 0f);
            triangulos += l.vis.Triangulos;
            void Junto(MalhaDePichacao p, Material mat, string sufixo)
            {
                if (p.Quadros == 0 || mat == null) return;
                var o = Novo(nome + sufixo, pai);
                var malha = p.ParaMesh(nome + sufixo);
                malha.hideFlags = HideFlags.DontSave;
                o.AddComponent<MeshFilter>().sharedMesh = malha;
                var mr = o.AddComponent<MeshRenderer>();
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = ShadowCastingMode.Off;
            }
            Junto(l.marcas, materialDasMarcas, " (marcas)");
            Junto(l.pichacoes, materialDasPichacoes, " (pichações)");
            _marcas += l.marcas.Quadros;
            _pichacoes += l.pichacoes.Quadros;
            MalhaDeObra.Devolver(l.vis);
            MalhaDeObra.Devolver(l.col);
        }

        Dictionary<string, GameObject> _prefabs;
        readonly HashSet<string> _faltam = new HashSet<string>();

        GameObject Prefab(string modelo)
        {
            if (_prefabs == null)
            {
                _prefabs = new Dictionary<string, GameObject>();
                if (mobilia != null)
                    foreach (var p in mobilia)
                        if (p != null) _prefabs[p.name] = p;
            }
            return _prefabs.TryGetValue(modelo, out var pf) ? pf : null;
        }

        // os móveis, sem a colisão do corpo (a dele está na colisão da obra); a porta de cofre com o estado da sala
        int Moveis(Transform go, Variacao v)
        {
            _prefabs = null;
            _faltam.Clear();
            int n = 0;
            var pai = Novo("Móveis", go).transform;
            foreach (var m in MoveisDaLojaDeArmas.Da(v))
            {
                var pf = Prefab(m.modelo);
                if (pf == null) { _faltam.Add(m.modelo); continue; }
                var o = Instantiate(pf, pai);
                o.name = m.modelo;
                o.hideFlags = HideFlags.DontSave;
                o.transform.SetLocalPositionAndRotation(m.lugar, Quaternion.Euler(0f, m.giro, 0f));
                o.transform.localScale = Vector3.one * m.escala;
                var corpo = o.GetComponent<MeshCollider>();
                if (corpo != null) DestroyImmediate(corpo);
                if (m.porta >= 0) PortaDoCofreNaCena(o, m.porta);
                n++;
            }
            return n;
        }

        // a porta de cofre: aberta a 85° (arrombada), entreaberta a 35° ou fechada e trancada (sem a parte que abre: o E não a abre)
        static void PortaDoCofreNaCena(GameObject o, int estado)
        {
            var parte = o.GetComponentInChildren<ParteQueAbre>(true);
            if (parte == null) return;
            parte.rotulo = "a porta de cofre";
            parte.duracao = 1.4f;
            if (estado == 2)
            {
                parte.Pousar(false);
                DestroyImmediate(parte);
                return;
            }
            parte.quanto = Mathf.Sign(parte.quanto) * (estado == 0 ? 85f : 35f);
            parte.Pousar(true);
        }

        // ------------------------------------------------------------ o terreno

        static readonly float ZCalcada = Z0 - Calcada, ZEstacionamento = Z0 - Calcada - 12f, ZRua = Z0 - Calcada - 12f - 2.5f;
        static readonly Rect Estacionamento = Rect.MinMaxRect(X0 - 4f, ZEstacionamento, X1 + 4f, ZCalcada),
                             CalcadaDaLoja = Rect.MinMaxRect(X0 - 4f, ZCalcada, X1 + 4f, Z0),
                             Becos = Rect.MinMaxRect(X0 - 4f, Z0, X1 + 4f, Z1 + 5f),
                             CalcadaDaRua = Rect.MinMaxRect(-140f, ZRua, 140f, ZEstacionamento),
                             ARua = Rect.MinMaxRect(-140f, ZRua - 8f, 140f, ZRua),
                             CalcadaDeLa = Rect.MinMaxRect(-140f, ZRua - 10.5f, 140f, ZRua - 8f);

        void Terreno()
        {
            const float c = ChaoDeFora;
            var buracos = new List<Rect> { Estacionamento, CalcadaDaLoja, Becos, CalcadaDaRua, ARua, CalcadaDeLa };
            chao.Laje(-140f, 140f, -140f, 140f, c, 0.3f, "GramaSeca", true, buracos);
            // a calçada da loja, no nível da porta (o meio-fio de 15 cm para o estacionamento), com as juntas
            chao.Laje(CalcadaDaLoja.xMin, CalcadaDaLoja.xMax, CalcadaDaLoja.yMin, CalcadaDaLoja.yMax, 0f, 0.3f, "Calcada", true);
            for (float x = CalcadaDaLoja.xMin + 2f; x < CalcadaDaLoja.xMax; x += 2f)
                chao.Caixa(new Vector3(x, 0.002f, (CalcadaDaLoja.yMin + CalcadaDaLoja.yMax) / 2f), new Vector3(0.03f, 0.004f, Calcada), "Concreto");
            chao.Caixa(new Vector3(0f, -0.075f, ZCalcada - 0.08f), new Vector3(CalcadaDaLoja.width, 0.15f, 0.16f), "MeioFio", colide: true);
            // o estacionamento: o asfalto, as vagas de frente para a loja, os batentes das rodas e as manchas de óleo
            chao.Laje(Estacionamento.xMin, Estacionamento.xMax, Estacionamento.yMin, Estacionamento.yMax, c, 0.3f, "Asfalto", true);
            for (float x = Estacionamento.xMin + 1.2f; x < Estacionamento.xMax - 1f; x += 2.8f)
                chao.Caixa(new Vector3(x, c + 0.004f, ZCalcada - 2.9f), new Vector3(0.12f, 0.008f, 5.2f), "ListraBranca");
            for (float x = Estacionamento.xMin + 2.6f; x < Estacionamento.xMax - 2f; x += 2.8f)
            {
                chao.Caixa(new Vector3(x, c + 0.06f, ZCalcada - 0.9f), new Vector3(1.6f, 0.12f, 0.2f), "Concreto", colide: true);
                if (Mathf.Abs(Mathf.Sin(x * 7.1f)) > 0.6f) chao.Caixa(new Vector3(x + 0.3f, c + 0.003f, ZCalcada - 2.6f), new Vector3(0.7f, 0.004f, 1.1f), "Oleo");
            }
            // as calçadas da rua e a rua, com a faixa
            chao.Laje(CalcadaDaRua.xMin, CalcadaDaRua.xMax, CalcadaDaRua.yMin, CalcadaDaRua.yMax, -0.03f, 0.3f, "Calcada", true);
            chao.Laje(ARua.xMin, ARua.xMax, ARua.yMin, ARua.yMax, c, 0.3f, "Asfalto", true);
            chao.Laje(CalcadaDeLa.xMin, CalcadaDeLa.xMax, CalcadaDeLa.yMin, CalcadaDeLa.yMax, -0.03f, 0.3f, "Calcada", true);
            for (float x = ARua.xMin; x < ARua.xMax; x += 6f) chao.Caixa(new Vector3(x, c + 0.004f, ARua.center.y), new Vector3(3f, 0.008f, 0.15f), "Faixa");
            // os becos dos lados e dos fundos (o concreto), sem o prédio
            chao.Laje(Becos.xMin, Becos.xMax, Becos.yMin, Becos.yMax, c, 0.3f, "Concreto", true, new[] { Rect.MinMaxRect(X0 - 0.11f, Z0, X1 + 0.11f, Z1 + 0.11f) });
            Alambrado();
        }

        // o alambrado em volta dos becos (os postes, o tubo de cima, os arames e a tela em losango, de longe só os arames)
        void Alambrado()
        {
            void Trecho(Vector2 a, Vector2 b)
            {
                float L = (b - a).magnitude;
                int n = Mathf.Max(1, Mathf.RoundToInt(L / 2.5f));
                for (int i = 0; i <= n; i++)
                {
                    var p = Vector2.Lerp(a, b, i / (float)n);
                    chao.Cilindro(new Vector3(p.x, ChaoDeFora, p.y), 0.035f, 0.035f, 1.9f, 6, "Metal");
                }
                foreach (float y in new[] { 0.15f, 0.8f, 1.45f, 1.88f })
                    chao.Tubo(new Vector3(a.x, ChaoDeFora + y, a.y), new Vector3(b.x, ChaoDeFora + y, b.y), y > 1.8f ? 0.022f : 0.006f, "Metal");
                // a colisão
                chao.CaixaEntre(new Vector3(a.x, ChaoDeFora + 0.95f, a.y), new Vector3(b.x, ChaoDeFora + 0.95f, b.y), 0.06f, 1.9f, "Metal", colide: true, visual: false);
            }
            Trecho(new Vector2(X0 - 4f, Z0 + 1f), new Vector2(X0 - 4f, Z1 + 5f));
            Trecho(new Vector2(X0 - 4f, Z1 + 5f), new Vector2(X1 + 4f, Z1 + 5f));
            Trecho(new Vector2(X1 + 4f, Z1 + 5f), new Vector2(X1 + 4f, Z0 + 1f));
        }

        // os frades amarelos na beira da calçada, a caçamba e as lixeiras no beco dos fundos, os postes do estacionamento e o
        // letreiro de poste na beira da rua ("GUNS · AMMO · RANGE")
        void Objetos()
        {
            void Frade(float x, float z)
            {
                chao.Cilindro(new Vector3(x, 0f, z), 0.1f, 0.1f, 0.95f, 8, "PlacaAmarela");
                chao.Cilindro(new Vector3(x, 0.95f, z), 0.1f, 0.06f, 0.06f, 8, "PlacaAmarela");
                chao.Caixa(new Vector3(x, 0.475f, z), new Vector3(0.2f, 0.95f, 0.2f), "Concreto", colide: true, visual: false);
            }
            foreach (float x in new[] { X(1.4f), X(4.3f), X(6.0f), X(9.6f), X(11.3f), X(14.2f), X(17.4f), X(19.8f) }) Frade(x, ZCalcada + 0.45f);
            if (cacamba != null) Objeto("Caçamba", raiz, cacamba, null, new Vector3(X(21.6f), ChaoDeFora, Z1 + 3.3f), 180f);
            else chao.Caixa(new Vector3(X(21.6f), ChaoDeFora + 0.9f, Z1 + 3.3f), new Vector3(3.6f, 1.8f, 2f), "Cacamba", colide: true);
            chao.Caixa(new Vector3(X(21.6f), ChaoDeFora + 0.9f, Z1 + 3.3f), new Vector3(3.4f, 1.6f, 1.8f), "Cacamba", colide: true, visual: false);
            if (lixeira != null) Objeto("Lixeira", raiz, lixeira, null, new Vector3(X(15.8f), 0f, ZCalcada + 0.8f), 0f);
            if (poste != null)
                foreach (float x in new[] { X0 - 2.5f, X1 + 2.5f })
                    Objeto("Poste", raiz, poste, null, new Vector3(x, ChaoDeFora, ZEstacionamento + 0.6f), 0f);
            // o letreiro de poste: os dois postes, a caixa vermelha e as letras (apagado: sem energia)
            var b = new Vector3(X1 + 1.2f, -0.03f, ZEstacionamento + 1.2f);
            foreach (float dx in new[] { -0.9f, 0.9f })
            {
                chao.Cilindro(b + new Vector3(dx, 0f, 0f), 0.09f, 0.09f, 5.2f, 8, "Metal");
                chao.Caixa(b + new Vector3(dx, 2.6f, 0f), new Vector3(0.18f, 5.2f, 0.18f), "Metal", colide: true, visual: false);
            }
            chao.Caixa(b + new Vector3(0f, 4.55f, 0f), new Vector3(2.6f, 1.3f, 0.36f), "PlacaVermelha");
            chao.Caixa(b + new Vector3(0f, 4.55f, 0f), new Vector3(2.7f, 1.4f, 0.3f), "Palha");
            foreach (var (z, fora) in new[] { (-0.181f, Vector3.back), (0.181f, Vector3.forward) })
            {
                var u = Vector3.Cross(Vector3.up, -fora);
                chao.Letreiro("GUNS", b + new Vector3(0f, 4.72f, z), u, fora, 0.36f, "Palha", plano: true, sai: 0.002f);
                chao.Letreiro("AMMO * RANGE", b + new Vector3(0f, 4.12f, z), u, fora, 0.2f, "Palha", plano: true, sai: 0.002f);
            }
            // a placa das vagas
            var pv = new Vector3(X0 - 1.5f, ChaoDeFora, ZCalcada - 0.6f);
            chao.Cilindro(pv, 0.03f, 0.03f, 2.2f, 6, "Metal");
            chao.Caixa(pv + new Vector3(0f, 1.95f, -0.035f), new Vector3(0.5f, 0.36f, 0.02f), "PlacaBranca");
            chao.Letreiro("CUSTOMER", pv + new Vector3(0f, 1.98f, -0.046f), Vector3.right, Vector3.back, 0.07f, "PlacaVermelha", plano: true, sai: 0.002f);
            chao.Letreiro("PARKING ONLY", pv + new Vector3(0f, 1.86f, -0.046f), Vector3.right, Vector3.back, 0.06f, "Pneu", plano: true, sai: 0.002f);
        }

        void Carros()
        {
            if (carros == null || carros.Length == 0) return;
            var rnd = new System.Random(semente * 13 + 5);
            // a picape largada na frente da loja, torta, e uns carros nas vagas
            var picape = System.Array.Find(carros, m => m != null && m.name.Contains("Pickup")) ?? carros[0];
            Objeto("Carro na frente", raiz, picape, null, new Vector3(X(-1.5f), ChaoDeFora + 0.05f, ZCalcada - 4.2f), 168f);
            for (int i = 0; i < 9; i++)
            {
                if (rnd.NextDouble() < 0.6) continue;
                float x = Estacionamento.xMin + 2.6f + i * 2.8f * 1.3f;
                if (x > Estacionamento.xMax - 2f || x < X(3f)) continue;
                Objeto("Carro na vaga", raiz, carros[rnd.Next(carros.Length)], null, new Vector3(x, ChaoDeFora + 0.05f, ZCalcada - 2.9f), 180f + (float)(rnd.NextDouble() - 0.5) * 8f);
            }
            Objeto("Carro na rua", raiz, carros[rnd.Next(carros.Length)], null, new Vector3(-22f, ChaoDeFora, ARua.center.y - 2f), 90f);
        }

        void Arvores()
        {
            if (arvores == null || arvores.Length == 0) return;
            var rnd = new System.Random(semente);
            var longe = new[] { Estacionamento, CalcadaDaLoja, Becos, CalcadaDaRua, ARua, CalcadaDeLa };
            for (int i = 0; i < 180; i++)
            {
                float x = Mathf.Lerp(-110f, 110f, (float)rnd.NextDouble()), z = Mathf.Lerp(-110f, 110f, (float)rnd.NextDouble());
                bool ok = true;
                foreach (var r in longe)
                    if (Rect.MinMaxRect(r.xMin - 4f, r.yMin - 4f, r.xMax + 4f, r.yMax + 4f).Contains(new Vector2(x, z))) ok = false;
                if (!ok) continue;
                var a = Objeto("Árvore", raiz, arvores[rnd.Next(arvores.Length)], null, new Vector3(x, ChaoDeFora, z), (float)rnd.NextDouble() * 360f);
                a.transform.localScale = Vector3.one * Mathf.Lerp(0.9f, 1.5f, (float)rnd.NextDouble());
            }
        }

        // os arbustos e o mato junto das paredes dos lados (por fora do alambrado, e uns dentro do beco)
        void Arbustos()
        {
            var malhas = new List<Mesh>();
            foreach (var m in new[] { arbustoA, arbustoB, arbustoC }) if (m != null) malhas.Add(m);
            if (malhas.Count == 0 || vegetacao == null) return;
            var rnd = new System.Random(semente + 11);
            void Um(float x, float z, float escala)
            {
                var a = Objeto("Arbusto", raiz, malhas[rnd.Next(malhas.Count)], null, new Vector3(x, ChaoDeFora, z), (float)rnd.NextDouble() * 360f);
                a.GetComponent<MeshRenderer>().sharedMaterial = vegetacao;
                a.transform.localScale = Vector3.one * escala;
            }
            for (float z = Z0 + 2f; z < Z1 + 4f; z += 2.4f)
            {
                if (rnd.NextDouble() < 0.5) Um(X0 - 4.6f - (float)rnd.NextDouble(), z, Mathf.Lerp(0.9f, 1.5f, (float)rnd.NextDouble()));
                if (rnd.NextDouble() < 0.5) Um(X1 + 4.6f + (float)rnd.NextDouble(), z, Mathf.Lerp(0.9f, 1.5f, (float)rnd.NextDouble()));
                if (rnd.NextDouble() < 0.25) Um(X0 - 0.5f, z, Mathf.Lerp(0.5f, 0.8f, (float)rnd.NextDouble()));
            }
        }

        void Hera(LojaDeArmas obra, Transform pai)
        {
            if (vegetacao == null) return;
            var hera = AbandonoDaObra.Hera(obra, abandono, semente + 5, 6f);
            if (hera.Folhas == 0) return;
            var h = Novo("Hera", pai);
            var m = hera.ParaMesh("Hera", false);
            m.hideFlags = HideFlags.DontSave;
            h.AddComponent<MeshFilter>().sharedMesh = m;
            var r = h.AddComponent<MeshRenderer>();
            r.sharedMaterial = vegetacao;
            var bloco = new MaterialPropertyBlock();
            bloco.SetFloat("_Cima", 0.6f);
            r.SetPropertyBlock(bloco);
            triangulos += hera.Folhas * 2;
        }

        // o grau do abandono (por dentro, 60% do de fora) e a altura do chão de fora
        void Abandono()
        {
            var fora = new MaterialPropertyBlock();
            fora.SetFloat("_Intensidade", abandono);
            fora.SetFloat("_Chao", ChaoDeFora);
            var dentro = new MaterialPropertyBlock();
            dentro.SetFloat("_Intensidade", abandono * 0.6f);
            dentro.SetFloat("_Chao", ChaoDeFora);
            foreach (var r in raiz.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (r.sharedMaterial != material) continue;
                r.SetPropertyBlock(r.gameObject.name == "Loja de armas (dentro)" ? dentro : fora);
            }
        }

        // ------------------------------------------------------------ as paradas, a tela e a planta

        void ParadasDaLoja()
        {
            var limites = new Bounds(new Vector3(0f, 2f, 0f), new Vector3(X1 - X0 + 8f, 8f, Z1 - Z0 + 20f));
            void Por(string onde, Vector3 chegada, float giro)
            {
                var p = new Parada { Ficha = $"Loja de armas · {onde}\n24 × 30 m, um pavimento", Chegada = chegada, Giro = giro, Limites = limites };
                p.Pisos.Add(0f);
                p.NomesDosPisos.Add("Térreo");
                foreach (var (centro, rotulo) in Rotulos()) p.Comodos.Add((centro, 0, rotulo));
                Paradas.Add(p);
            }
            Por("a frente (o estacionamento)", new Vector3(MeioDaEntrada, ChaoDeFora + 0.1f, ZEstacionamento + 3f), 0f);
            Por("a área de vendas (a entrada)", new Vector3(MeioDaEntrada, 0.1f, Z(3.5f)), 0f);
            Por("o caixa", new Vector3(X(13.8f), 0.1f, Z(16.5f)), -20f);
            Por("o estoque e a porta de cofre", new Vector3(X(9.6f), 0.1f, Z(24.8f)), -90f);
            Por("o estande (a área de espera)", new Vector3(X(18.9f), 0.1f, Z(8.1f)), 0f);
            Por("os fundos (o beco)", new Vector3(X(8.9f), ChaoDeFora + 0.1f, Z1 + 3.6f), 180f);
        }

        /// <summary>O meio (no chão) e o rótulo de cada cômodo para a planta (o P): o nome e o tamanho.</summary>
        public static IEnumerable<(Vector3 centro, string rotulo)> Rotulos()
        {
            var an = PlantaPronta;
            foreach (var k in an.comodos)
                yield return (new Vector3(k.Cx, 0f, k.Cz), $"{an.nomes[k]}\n{(k.x1 - k.x0):0.#} × {(k.z1 - k.z0):0.#} m");
        }

        public override string Texto(Vector3 pos)
        {
            var k = Onde(pos);
            string onde = k == null ? "Fora da loja" : $"{PlantaPronta.nomes[k]} ({(k.x1 - k.x0):0.#} × {(k.z1 - k.z0):0.#} m)";
            return onde + "\n" + Resumo;
        }

        public override string AjudaDasTeclas => "1, 2, 3 a sala segura A, B ou C (0 a da semente) · L lanterna (sem energia) · J toca um som";

        public override void Teclas(Keyboard k)
        {
            if (k.lKey.wasPressedThisFrame) _lanternaLigada = !_lanternaLigada;
            if (k.jKey.wasPressedThisFrame) GameObject.Find("Sons da loja de armas")?.SendMessage("TocarUmSom", SendMessageOptions.DontRequireReceiver);
            Sala? nova = k.digit1Key.wasPressedThisFrame ? Sala.A_Saqueada : k.digit2Key.wasPressedThisFrame ? Sala.B_Parcial
                       : k.digit3Key.wasPressedThisFrame ? Sala.C_Intacta : k.digit0Key.wasPressedThisFrame ? Sala.DaSemente : (Sala?)null;
            if (nova.HasValue && nova.Value != sala)
            {
                sala = nova.Value;
                Gerar();
            }
        }

        // ------------------------------------------------------------ a luz

        static Light Facho(GameObject go)
        {
            var l = go.AddComponent<Light>();
            l.type = LightType.Spot;
            l.intensity = ForcaDaLanterna;
            l.spotAngle = AberturaDaLanterna;
            l.innerSpotAngle = AberturaInternaDaLanterna;
            l.range = AlcanceDaLanterna;
            l.color = new Color(1f, 0.96f, 0.88f);
            l.shadows = LightShadows.Soft;
            l.shadowNearPlane = 0.2f;
            return l;
        }

        /// <summary>A lanterna das fotos: em 'pos', apontada para 'alvo'. Quem pede destrói.</summary>
        public static Light LanternaDaFoto(Vector3 pos, Vector3 alvo)
        {
            var go = new GameObject("Lanterna da foto") { hideFlags = HideFlags.HideAndDontSave };
            var giro = Quaternion.LookRotation(alvo - pos);
            go.transform.SetPositionAndRotation(pos + giro * LanternaNaCamera, giro);
            return Facho(go);
        }

        /// <summary>Dentro da loja (do chão ao teto, na pegada dela).</summary>
        public static bool Dentro(Vector3 p) => Onde(p) != null;

        bool _guardada;
        Color _ceu, _equador, _chao;
        float _sol, _reflexo = 1f;
        bool _neblina;
        Light _luzDoSol;
        int _zona = -1;

        /// <summary>
        /// A luz de onde a câmera está: fora, a da cena, com a neblina de altura rala; dentro, sem neblina, a luz ambiente pelo
        /// cômodo (a área de vendas, o escritório, a antessala e o banheiro com a de dentro do jogo; o estoque e o depósito, sem
        /// janela, a 35%; o estande a 10%; a sala segura a 6%) e, nos escuros, o sol junto.
        /// </summary>
        public void Iluminar(Vector3 camera)
        {
            var k = Onde(camera);
            int zona = k == null ? 0 : k.tipo switch { "estande" => 3, "segura" => 4, "estoque" or "deposito" => 2, _ => 1 };
            Shader.SetGlobalColor("_NeblinaCor", new Color(0.63f, 0.65f, 0.67f, 1f));
            Shader.SetGlobalVector("_NeblinaParams", new Vector4(zona == 0 ? neblinaDeFora : 0f, 0.2f, 0.32f, 140f));
            if (zona == _zona) return;
            if (!_guardada)
            {
                _ceu = RenderSettings.ambientSkyColor; _equador = RenderSettings.ambientEquatorColor; _chao = RenderSettings.ambientGroundColor;
                _neblina = RenderSettings.fog;
                _reflexo = RenderSettings.reflectionIntensity;
                foreach (var l in FindObjectsByType<Light>())
                    if (l.type == LightType.Directional) { _luzDoSol = l; _sol = l.intensity; break; }
                _guardada = true;
            }
            _zona = zona;
            float f = zona switch { 0 => 1f, 1 => luzDentro, 2 => luzNoEstoque, 3 => luzNoEstande, _ => luzNaSalaSegura };
            RenderSettings.ambientSkyColor = _ceu * f;
            RenderSettings.ambientEquatorColor = _equador * f;
            RenderSettings.ambientGroundColor = _chao * f;
            RenderSettings.reflectionIntensity = _reflexo * f;
            RenderSettings.fog = _neblina && zona == 0;
            if (_luzDoSol != null) _luzDoSol.intensity = zona >= 2 ? _sol * f : _sol;
        }

        public void LuzDeFora() => Iluminar(new Vector3(0f, 100f, -200f));

        void OnApplicationQuit() => LuzDeFora();

        void Update()
        {
            if (!Application.isPlaying) return;
            var cam = Camera.main;
            if (cam == null) return;
            Iluminar(cam.transform.position);
            if (_lanterna == null)
            {
                var go = new GameObject("Lanterna") { hideFlags = HideFlags.DontSave };
                go.transform.SetParent(cam.transform, false);
                go.transform.localPosition = LanternaNaCamera;
                _lanterna = Facho(go);
            }
            _lanterna.enabled = _lanternaLigada;
        }
    }
}
