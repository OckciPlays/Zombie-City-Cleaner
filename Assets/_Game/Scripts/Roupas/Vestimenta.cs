using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ZombieCityCleaner.Roupas
{
    /// <summary>
    /// Veste um corpo com as peças do catálogo (o Plano_Roupas.md da pasta de arte, 30/10/2026). Fica na raiz do modelo
    /// do <c>Roupas.fbx</c> (o esqueleto do personagem principal, o <c>NPC_Corpo</c> e todas as peças, cada uma um
    /// <see cref="SkinnedMeshRenderer"/> com o nome <c>Roupa_&lt;nome&gt;</c>, presa aos mesmos ossos):
    ///  - liga só as peças vestidas;
    ///  - dá as três cores de cada uma (o <see cref="MaterialPropertyBlock"/> do shader das roupas, <c>_Cor1</c> a <c>_Cor3</c>);
    ///  - tira as faces cobertas (as do corpo embaixo da calça, as da camiseta embaixo da jaqueta, o alto do cabelo
    ///    embaixo do boné): cada vértice tem no UV1.x o número da face dele, e o catálogo diz as faces que cada peça
    ///    esconde dos outros. A malha sem elas é guardada para a mesma combinação (as malhas são lidas: o FBX é Read/Write);
    ///  - com um chapéu na cabeça, desenha os cabelos grandes na versão de baixo do chapéu, com as cores deles
    ///    (<see cref="Desenhada"/>).
    /// O corpo: o do homem ou o de uma forma (<see cref="corpo"/>, a "Mulher": a blend shape com o mesmo nome no corpo e
    /// em cada peça, a 100), e as cores dele no mesmo material das roupas: a pele (<see cref="pele"/>), a cueca e as
    /// sobrancelhas, com a cor dos pelos vestidos (o cabelo, a barba).
    /// Com <see cref="npc"/> preenchido, veste esse NPC do catálogo no começo; sem ele e com <see cref="tipo"/>, um NPC
    /// sorteado desse tipo, com a <see cref="semente"/> (o <see cref="SorteioDosNPCs"/>: a mesma semente, o mesmo NPC).
    /// Quem já foi vestido antes do começo (o jogador, pela aparência que chega pela rede; as cópias do retrato e do
    /// boneco do F1, por <see cref="VestirComo"/>) fica como está.
    /// Só cuida do <c>NPC_Corpo</c> e das peças (<c>Roupa_&lt;nome&gt;</c>): no jogador, o mesmo corpo tem as armas e os itens
    /// da mão, que são de outro dono.
    /// </summary>
    public class Vestimenta : MonoBehaviour
    {
        public const string Corpo = "NPC_Corpo";
        public const string PrefixoDasPecas = "Roupa_";

        public TextAsset catalogo;
        [Tooltip("O NPC do catálogo vestido no começo (vazio: nenhuma peça, ou um sorteado do tipo)")]
        public string npc;
        [Tooltip("Sem o NPC: o tipo do sorteio (o \"Policial\", o \"Civil\"...; vazio: nenhuma peça)")]
        public string tipo;
        [Tooltip("A semente do sorteio (a mesma semente, o mesmo NPC)")]
        public int semente;
        [Tooltip("O corpo: \"Homem\" ou uma forma do catálogo (\"Mulher\")")]
        public string corpo = CatalogoDasRoupas.Homem;
        [Tooltip("O tom de pele (hex; vazio: o do personagem principal)")]
        public string pele = "#D08A63";

        // (nunca gravado: a Unity grava os campos particulares ao recarregar os scripts, e um catálogo nulo voltava vazio, com
        // nenhuma peça, em vez de ser lido de novo)
        [System.NonSerialized] CatalogoDasRoupas _catalogo;
        readonly Dictionary<string, SkinnedMeshRenderer> _renderers = new Dictionary<string, SkinnedMeshRenderer>();
        readonly Dictionary<SkinnedMeshRenderer, Mesh> _originais = new Dictionary<SkinnedMeshRenderer, Mesh>();
        MaterialPropertyBlock _bloco;
        bool _vestiu;
        static readonly Dictionary<(Mesh, string), Mesh> Guardadas = new Dictionary<(Mesh, string), Mesh>();

        public CatalogoDasRoupas Catalogo => _catalogo ??= CatalogoDasRoupas.Ler(catalogo);

        /// <summary>As peças vestidas agora (o nome e as cores).</summary>
        public List<CatalogoDasRoupas.PecaVestida> Vestidas { get; } = new List<CatalogoDasRoupas.PecaVestida>();

        /// <summary>Os triângulos desenhados agora (o corpo e as peças, sem as faces cobertas).</summary>
        public int Triangulos { get; private set; }

        void Start()
        {
            if (_vestiu) return;
            if (!string.IsNullOrEmpty(npc)) Vestir(npc);
            else if (!string.IsNullOrEmpty(tipo)) VestirSorteado(tipo, semente);
            else Vestir(new CatalogoDasRoupas.PecaVestida[0]);
        }

        void Achar()
        {
            if (_renderers.Count > 0) return;
            foreach (var r in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (r.name != Corpo && !r.name.StartsWith(PrefixoDasPecas)) continue;
                _renderers[r.name] = r;
                _originais[r] = r.sharedMesh;
            }
        }

        /// <summary>
        /// Veste esta cópia como a outra (o corpo, a pele, as peças e as cores): o retrato do inventário e o boneco do F1
        /// são cópias do corpo do jogador. As malhas inteiras são as da outra (a cópia do corpo animado já vem com as
        /// faces cobertas tiradas).
        /// </summary>
        public void VestirComo(Vestimenta outra)
        {
            Achar();
            outra.Achar();
            foreach (var par in _renderers)
                if (outra._renderers.TryGetValue(par.Key, out var dela)) _originais[par.Value] = outra._originais[dela];
            corpo = outra.corpo;
            pele = outra.pele;
            Vestir(outra.Vestidas.ToList());
        }

        public void Vestir(string nomeDoNpc)
        {
            var n = Catalogo.DoNpc(nomeDoNpc);
            if (n == null)
            {
                Debug.LogWarning($"O NPC {nomeDoNpc} não está no catálogo das roupas.");
                return;
            }
            npc = nomeDoNpc;
            tipo = n.tipo;
            if (!string.IsNullOrEmpty(n.corpo)) corpo = n.corpo;
            if (!string.IsNullOrEmpty(n.pele)) pele = n.pele;
            Vestir(n.pecas);
        }

        /// <summary>
        /// Veste um NPC sorteado do tipo (o corpo, a pele, as peças e as cores; <see cref="SorteioDosNPCs"/>). Devolve se o
        /// tipo existe no catálogo.
        /// </summary>
        public bool VestirSorteado(string tipoDoSorteio, int sementeDoSorteio)
        {
            var n = SorteioDosNPCs.Sortear(Catalogo, tipoDoSorteio, sementeDoSorteio);
            if (n == null)
            {
                Debug.LogWarning($"O tipo {tipoDoSorteio} não está no catálogo das roupas.");
                return false;
            }
            npc = "";
            tipo = tipoDoSorteio;
            semente = sementeDoSorteio;
            corpo = n.corpo;
            pele = n.pele;
            Vestir(n.pecas);
            return true;
        }

        public void Vestir(IEnumerable<CatalogoDasRoupas.PecaVestida> pecas)
        {
            Achar();
            _vestiu = true;
            var pedidas = pecas.Where(p => Catalogo.DaPeca(p.nome) != null && _renderers.ContainsKey(PrefixoDasPecas + p.nome)).ToList();
            Vestidas.Clear();
            Vestidas.AddRange(pedidas.Select(p => new CatalogoDasRoupas.PecaVestida { nome = p.nome, cores = (string[])p.cores.Clone() }));
            // as que se desenham: com um chapéu na cabeça, os cabelos grandes viram a versão de baixo do chapéu (as mesmas
            // cores); as vestidas continuam as pedidas (sem o chapéu, o cabelo volta)
            var lista = pedidas.Select(p => new CatalogoDasRoupas.PecaVestida { nome = Desenhada(p.nome), cores = p.cores }).ToList();
            var vestidas = new HashSet<string>(lista.Select(p => PrefixoDasPecas + p.nome));
            // as faces cobertas de cada um (o corpo e as peças vestidas), por todas as peças vestidas
            var escondidas = new Dictionary<string, HashSet<int>>();
            foreach (var p in lista)
                foreach (var e in Catalogo.DaPeca(p.nome).esconde ?? new CatalogoDasRoupas.Esconde[0])
                {
                    if (e.alvo != Corpo && !vestidas.Contains(e.alvo)) continue;
                    if (!escondidas.TryGetValue(e.alvo, out var faces)) escondidas[e.alvo] = faces = new HashSet<int>();
                    CatalogoDasRoupas.Faces(e.faces, faces);
                }
            Triangulos = 0;
            foreach (var par in _renderers)
            {
                bool liga = par.Key == Corpo || vestidas.Contains(par.Key);
                par.Value.gameObject.SetActive(liga);
                if (!liga) continue;
                escondidas.TryGetValue(par.Key, out var faces);
                var malha = SemAsFaces(_originais[par.Value], faces);
                par.Value.sharedMesh = malha;
                for (int s = 0; s < malha.subMeshCount; s++) Triangulos += (int)malha.GetIndexCount(s) / 3;
            }
            Formas();
            foreach (var p in pedidas) Pintar(p.nome, p.cores);
            PintarCorpo();
        }

        /// <summary>
        /// A peça que se desenha no lugar de uma vestida: com um chapéu vestido (os bonés, o quepe, o gorro, o capacete e o
        /// chapéu de caubói), o cabelo grande vira a versão dele de baixo do chapéu (só a calota rente: ele saía por cima e
        /// por trás deles); senão, ela mesma.
        /// </summary>
        public string Desenhada(string peca)
        {
            var dados = Catalogo.DaPeca(peca);
            if (dados == null || string.IsNullOrEmpty(dados.sob_chapeu)) return peca;
            if (!Vestidas.Any(v => Catalogo.DaPeca(v.nome)?.cobre_cabelo == true)) return peca;
            return Catalogo.DaPeca(dados.sob_chapeu) != null && _renderers.ContainsKey(PrefixoDasPecas + dados.sob_chapeu)
                ? dados.sob_chapeu : peca;
        }

        /// <summary>
        /// Troca a peça vestida num lugar do corpo (o lugar do catálogo: "Cabeca", "Cabelo", "Oculos"...) pela próxima do
        /// catálogo nesse lugar, com as cores do começo dela; depois da última, nenhuma. As outras peças do mesmo lugar saem
        /// (na barba, a barba, o bigode e o cavanhaque são do mesmo lugar). As versões de baixo do chapéu não entram (elas
        /// se desenham sozinhas, <see cref="Desenhada"/>). Devolve o nome da peça vestida (ou null).
        /// </summary>
        public string TrocarNoLugar(string lugar)
        {
            var doLugar = Catalogo.pecas.Where(p => p.lugar == lugar && string.IsNullOrEmpty(p.de)).ToList();
            var lista = Vestidas.ToList();
            var atual = lista.FirstOrDefault(p => Catalogo.DaPeca(p.nome)?.lugar == lugar);
            int proxima = (atual == null ? -1 : doLugar.FindIndex(p => p.nome == atual.nome)) + 1;
            lista.RemoveAll(p => Catalogo.DaPeca(p.nome)?.lugar == lugar);
            string nova = proxima < doLugar.Count ? doLugar[proxima].nome : null;
            if (nova != null)
                lista.Add(new CatalogoDasRoupas.PecaVestida { nome = nova, cores = (string[])doLugar[proxima].cores.Clone() });
            Vestir(lista);
            return nova;
        }

        /// <summary>Troca o corpo ("Homem" ou uma forma do catálogo, a "Mulher"), com as mesmas peças.</summary>
        public void TrocarCorpo(string novo)
        {
            corpo = novo;
            Formas();
        }

        /// <summary>Troca o tom de pele (hex).</summary>
        public void TrocarPele(string hex)
        {
            pele = hex;
            PintarCorpo();
        }

        // as blend shapes das formas: a do corpo pedido a 100, as outras a 0 (no corpo e em cada peça ligada)
        void Formas()
        {
            Achar();
            var formas = Catalogo.formas ?? new string[0];
            foreach (var r in _renderers.Values)
            {
                if (!r.gameObject.activeSelf || r.sharedMesh == null) continue;
                foreach (var f in formas)
                {
                    int i = r.sharedMesh.GetBlendShapeIndex(f);
                    if (i >= 0) r.SetBlendShapeWeight(i, f == corpo ? 100f : 0f);
                }
            }
        }

        // as cores do corpo: a pele, a cueca e a dos pelos vestidos (a primeira peça de pelos: o cabelo, a barba)
        void PintarCorpo()
        {
            if (!_renderers.TryGetValue(Corpo, out var r)) return;
            string pelos = Catalogo.sobrancelha;
            foreach (var p in Vestidas)
            {
                var dados = Catalogo.DaPeca(p.nome);
                if (dados != null && dados.pelos && p.cores != null && p.cores.Length > 0)
                {
                    pelos = p.cores[0];
                    break;
                }
            }
            _bloco ??= new MaterialPropertyBlock();
            r.GetPropertyBlock(_bloco);
            _bloco.SetColor("_Cor1", CatalogoDasRoupas.Cor(string.IsNullOrEmpty(pele) ? "#D08A63" : pele));
            _bloco.SetColor("_Cor2", CatalogoDasRoupas.Cor(Catalogo.cueca ?? "#434348"));
            _bloco.SetColor("_Cor3", CatalogoDasRoupas.Cor(pelos ?? "#2B1E16"));
            r.SetPropertyBlock(_bloco);
        }

        /// <summary>As três cores (hex) de uma peça vestida.</summary>
        public void Pintar(string peca, string[] cores)
        {
            Achar();
            if (!_renderers.TryGetValue(PrefixoDasPecas + Desenhada(peca), out var r)) return;
            _bloco ??= new MaterialPropertyBlock();
            r.GetPropertyBlock(_bloco);
            for (int k = 0; k < 3; k++)
                _bloco.SetColor(k == 0 ? "_Cor1" : k == 1 ? "_Cor2" : "_Cor3", CatalogoDasRoupas.Cor(cores[Mathf.Min(k, cores.Length - 1)]));
            r.SetPropertyBlock(_bloco);
            var v = Vestidas.Find(x => x.nome == peca);
            if (v != null) v.cores = (string[])cores.Clone();
            if (Catalogo.DaPeca(peca)?.pelos == true) PintarCorpo();
        }

        /// <summary>A malha sem as faces pedidas (o UV1.x de cada vértice é o número da face), guardada por combinação.</summary>
        static Mesh SemAsFaces(Mesh original, HashSet<int> faces)
        {
            if (faces == null || faces.Count == 0) return original;
            string chave = string.Join(",", faces.OrderBy(f => f));
            if (Guardadas.TryGetValue((original, chave), out var m) && m != null) return m;
            var uv = new List<Vector2>();
            original.GetUVs(1, uv);
            m = Instantiate(original);
            m.name = original.name + " (vestido)";
            var lista = new List<int>();
            for (int s = 0; s < original.subMeshCount; s++)
            {
                var tris = original.GetTriangles(s);
                lista.Clear();
                for (int i = 0; i < tris.Length; i += 3)
                {
                    if (faces.Contains(Mathf.FloorToInt(uv[tris[i]].x))) continue;
                    lista.Add(tris[i]);
                    lista.Add(tris[i + 1]);
                    lista.Add(tris[i + 2]);
                }
                m.SetTriangles(lista, s);
            }
            Guardadas[(original, chave)] = m;
            return m;
        }
    }
}
