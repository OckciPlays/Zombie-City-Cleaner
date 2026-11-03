using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace ZombieCityCleaner.Mundo
{
    /// <summary>
    /// Os lugares do saque no mostruário da mobília nova (o Plano_Saque.md da pasta de arte, a parte 2, 01/11/2026): em
    /// cada móvel com lugares (<see cref="LugaresDoSaque"/>), uma caixa por lugar (verde à vista, amarela dentro de uma
    /// porta, gaveta ou tampa; a do chão da gaveta e a da prateleira da porta vão com ela) e um item de teste que cabe
    /// nele, no tamanho real, do que o plano põe naquele cômodo (no quarto as roupas e as mochilas, na cozinha a comida
    /// e a bebida, no banheiro os remédios...). O L troca: nada, as caixas, as caixas com os itens e só os itens.
    /// </summary>
    public partial class CenaDasVariacoes
    {
        /// <summary>O que o L mostra.</summary>
        enum MostrarOSaque { Nada, Caixas, CaixasEItens, Itens }
        static readonly string[] NomesDoMostrar = { "nada", "as caixas", "as caixas e os itens", "só os itens" };
        MostrarOSaque _saque = MostrarOSaque.Nada;

        /// <summary>Os grupos das mesas dos itens que vão em cada cômodo do mostruário (as roupas vão no quarto).</summary>
        static readonly Dictionary<CatalogoDaMobilia.Grupo, string[]> ItensDoGrupo = new Dictionary<CatalogoDaMobilia.Grupo, string[]>
        {
            [CatalogoDaMobilia.Grupo.Sala] = new[] { "a comida", "a bebida", "a lanterna, as pilhas e o galão" },
            [CatalogoDaMobilia.Grupo.Quarto] = new[] { "as mochilas" },
            [CatalogoDaMobilia.Grupo.Cozinha] = new[] { "a comida", "a bebida" },
            [CatalogoDaMobilia.Grupo.Banheiro] = new[] { "os remédios" },
            [CatalogoDaMobilia.Grupo.Servico] = new[] { "os materiais", "a lanterna, as pilhas e o galão", "a munição", "as armas", "as armas brancas" },
        };

        // um item de teste num lugar: o objeto, o pai (o móvel ou a parte), o meio do lugar no pai e o pé do item (o meio
        // de baixo dele, já girado, no tamanho real)
        sealed class ItemNoLugar
        {
            public Transform go, pai;
            public Vector3 lugar, pe;
        }

        readonly List<GameObject> _caixasDoSaque = new List<GameObject>();
        readonly List<ItemNoLugar> _itensDoSaque = new List<ItemNoLugar>();
        int _lugaresSemItem;

        // um candidato a item de teste: a malha (do coletável ou da roupa), o giro de base e como fazer o objeto
        struct Candidato
        {
            public Mesh malha;
            public Quaternion giro;
            public GameObject prefab;
            public Material material;
            public string nome;
        }

        void ComecarOSaque()
        {
            _caixasDoSaque.Clear();
            _itensDoSaque.Clear();
            _lugaresSemItem = 0;
        }

        List<Candidato> Candidatos(CatalogoDaMobilia.Grupo grupo)
        {
            var r = new List<Candidato>();
            if (grupo == CatalogoDaMobilia.Grupo.Quarto && roupas != null)
                foreach (var p in roupas)
                {
                    var mf = p != null ? p.GetComponent<MeshFilter>() : null;
                    if (mf != null && mf.sharedMesh != null)
                        r.Add(new Candidato { malha = mf.sharedMesh, giro = Quaternion.Euler(0f, 180f, 0f), prefab = p, nome = p.name });
                }
            if (coletaveis != null && ItensDoGrupo.TryGetValue(grupo, out var grupos))
                foreach (var c in coletaveis)
                {
                    var material = c.material != null ? c.material : materialDosColetaveis;
                    if (c.malha == null || material == null || !grupos.Contains(c.grupo)) continue;
                    r.Add(new Candidato { malha = c.malha, giro = c.deitado ? Quaternion.Euler(0f, 0f, 90f) : Quaternion.identity, material = material, nome = c.nome });
                }
            return r;
        }

        /// <summary>As caixas e os itens de teste dos lugares do móvel do mostruário (nada, se ele não tem lugares).</summary>
        void PorOsLugares(GameObject movel, string modelo)
        {
            var lugares = LugaresDoSaque.Do(modelo);
            if (lugares.Length == 0) return;
            var tipo = CatalogoDaMobilia.DoModelo(modelo);
            var candidatos = tipo != null ? Candidatos(tipo.grupo) : new List<Candidato>();
            float e = MedidasDaMobilia.Escala;
            // as caixas: uma malha por pai (o corpo e cada parte em que há um lugar)
            var malhas = new Dictionary<Transform, MalhaDeObra>();
            uint sorte = (uint)modelo.GetHashCode();
            for (int i = 0; i < lugares.Length; i++)
            {
                var l = lugares[i];
                var pai = movel.transform;
                var meio = l.meio;
                if (l.naParte)
                {
                    var p = movel.transform.Find($"{modelo}.{l.parte}");
                    if (p != null)
                    {
                        meio = Matrix4x4.TRS(p.localPosition, p.localRotation, p.localScale).inverse.MultiplyPoint3x4(meio);
                        pai = p;
                    }
                }
                if (!malhas.TryGetValue(pai, out var m)) malhas[pai] = m = MalhaDeObra.Emprestar();
                Caixa(m, meio, l.tamanho, l.Dentro ? "PlacaAmarela" : "PlacaVerde");

                // o item: um dos que cabem no lugar (no jogo, ×1,30), deitado ou girado 90°
                var cabem = new List<(Candidato c, Quaternion giro, Bounds b)>();
                foreach (var c in candidatos)
                    foreach (float yaw in new[] { 0f, 90f })
                    {
                        var q = Quaternion.Euler(0f, yaw, 0f) * c.giro;
                        var b = Girado(c.malha.bounds, q);
                        if (b.size.x <= l.tamanho.x * e - 0.02f && b.size.z <= l.tamanho.z * e - 0.02f && b.size.y <= l.tamanho.y * e - 0.01f)
                        {
                            cabem.Add((c, q, b));
                            break;
                        }
                    }
                if (cabem.Count == 0) { _lugaresSemItem++; continue; }
                sorte = sorte * 1664525u + 1013904223u;
                var (esc, giro, bb) = cabem[(int)((sorte >> 8) % (uint)cabem.Count)];
                GameObject go;
                if (esc.prefab != null)
                {
                    go = Instantiate(esc.prefab, pai);
                    foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.hideFlags = HideFlags.DontSave;
                }
                else
                {
                    go = Novo(esc.malha.name, pai);
                    go.AddComponent<MeshFilter>().sharedMesh = esc.malha;
                    var r = go.AddComponent<MeshRenderer>();
                    r.sharedMaterial = esc.material;
                }
                go.name = $"Saque de teste: {esc.nome}";
                go.transform.localRotation = giro;
                _itensDoSaque.Add(new ItemNoLugar { go = go.transform, pai = pai, lugar = meio, pe = new Vector3(bb.center.x, bb.min.y, bb.center.z) });
            }
            foreach (var kv in malhas)
            {
                var go = Novo("Lugares do saque", kv.Key);
                go.AddComponent<MeshFilter>().sharedMesh = Malha(kv.Value, $"{modelo}: lugares do saque");
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = material;
                r.shadowCastingMode = ShadowCastingMode.Off;
                MalhaDeObra.Devolver(kv.Value);
                _caixasDoSaque.Add(go);
            }
        }

        // a caixa de um lugar: só as arestas (a moldura na superfície, os cantos e a de cima), até a altura livre (no
        // máximo 30 cm, para não tapar o móvel)
        static void Caixa(MalhaDeObra m, Vector3 meio, Vector3 tam, string cor)
        {
            const float aresta = 0.012f;
            float h = Mathf.Min(tam.y, 0.3f), w = tam.x, d = tam.z;
            foreach (float sx in new[] { -1f, 1f })
                foreach (float sz in new[] { -1f, 1f })
                    m.Caixa(Matrix4x4.Translate(meio + new Vector3(sx * (w - aresta) / 2f, h / 2f, sz * (d - aresta) / 2f)), new Vector3(aresta, h, aresta), cor);
            foreach (float y in new[] { aresta / 2f, h - aresta / 2f })
            {
                foreach (float sz in new[] { -1f, 1f })
                    m.Caixa(Matrix4x4.Translate(meio + new Vector3(0f, y, sz * (d - aresta) / 2f)), new Vector3(w, aresta, aresta), cor);
                foreach (float sx in new[] { -1f, 1f })
                    m.Caixa(Matrix4x4.Translate(meio + new Vector3(sx * (w - aresta) / 2f, y, 0f)), new Vector3(aresta, aresta, d), cor);
            }
        }

        /// <summary>O próximo do L: nada, as caixas, as caixas com os itens, só os itens.</summary>
        public void TrocarOSaque()
        {
            _saque = (MostrarOSaque)(((int)_saque + 1) % 4);
            MostrarOsLugares();
        }

        /// <summary>O que o L mostra (para as fotos).</summary>
        public void MostrarOSaqueDoMostruario(bool caixas, bool itens)
        {
            _saque = caixas ? (itens ? MostrarOSaque.CaixasEItens : MostrarOSaque.Caixas) : itens ? MostrarOSaque.Itens : MostrarOSaque.Nada;
            MostrarOsLugares();
        }

        void MostrarOsLugares()
        {
            bool caixas = _saque == MostrarOSaque.Caixas || _saque == MostrarOSaque.CaixasEItens;
            bool itens = _saque == MostrarOSaque.CaixasEItens || _saque == MostrarOSaque.Itens;
            foreach (var c in _caixasDoSaque) if (c != null) c.SetActive(caixas);
            foreach (var i in _itensDoSaque) if (i.go != null) i.go.gameObject.SetActive(itens);
        }

        // os itens de teste no tamanho real, com o pé no meio do lugar, em qualquer escala dos móveis (o K): o pai escala
        // junto com o suporte, e o item, ao contrário
        void AjustarOSaque()
        {
            foreach (var i in _itensDoSaque)
            {
                if (i.go == null || i.pai == null) continue;
                float s = Mathf.Max(1e-4f, i.pai.lossyScale.x);
                i.go.localScale = Vector3.one / s;
                i.go.localPosition = i.lugar - i.pe / s;
            }
            MostrarOsLugares();
        }

        string AjudaDoSaque => _caixasDoSaque.Count > 0 ? $" · L os lugares do saque (agora: {NomesDoMostrar[(int)_saque]})" : "";

        /// <summary>Os lugares de um modelo, para a ficha ("4 lugares do saque: 1 à vista, 3 dentro").</summary>
        static string LugaresNaFicha(string modelo)
        {
            var l = LugaresDoSaque.Do(modelo);
            if (l.Length == 0) return "";
            int dentro = l.Count(x => x.Dentro);
            return $"; {l.Length} {(l.Length == 1 ? "lugar" : "lugares")} do saque ({l.Length - dentro} à vista, {dentro} dentro)";
        }
    }
}
