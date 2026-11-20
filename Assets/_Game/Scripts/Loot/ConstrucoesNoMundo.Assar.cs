using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering;
using ZombieCityCleaner.Player;

namespace ZombieCityCleaner.Loot
{
    /// <summary>
    /// O que está assando numa fogueira (18/11/2026, a etapa 2 da fogueira): a fogueira (o id da construção), o lugar nela
    /// (0 a 2: os espetos; 3: a panela), a comida e os segundos de fogo que ela já pegou (só conta com o fogo aceso).
    /// </summary>
    public struct Assando : INetworkSerializeByMemcpy, IEquatable<Assando>
    {
        public ushort fogueira;
        public byte lugar;
        public Item comida;
        public float tempo;

        public bool Equals(Assando o) => fogueira == o.fogueira && lugar == o.lugar && comida == o.comida && tempo == o.tempo;
    }

    /// <summary>
    /// O assar na fogueira (18/11/2026, a etapa 2 da fogueira, a pedido: "poder assar os peixes/carne/latas na fogueira direto
    /// com um espeto ou uma panela", no lugar da fabricação). Com o peixe cru, a carne crua ou uma lata na mão, o clique na
    /// fogueira acesa põe a comida no fogo: o peixe e a carne num espeto (até 3, em volta do fogo, fincados no chão), a lata
    /// na panela (1, em cima da lenha). O espeto e a panela saem da mochila e voltam quando a comida sai. Cada um pega fogo
    /// só com a fogueira acesa: o peixe fica pronto em 45 s, a carne em 60 s, a lata em 30 s, e esquecido, queima (1 min
    /// depois; a lata, 1,5 min). O E na comida tira: crua, ela volta; pronta, o peixe vira os filés assados (pelo tamanho,
    /// como era a receita) e a carne, a assada; a lata quente se come ali mesmo, e acalma a cabeça; queimada, se perde (e o
    /// espeto queima junto; a panela fica). Dá para assar sentado. A lista vai pela rede (o anfitrião conta o fogo, a cada
    /// segundo), e cada cópia monta os espetos e a panela; nada disso vai no save (a comida no fogo some ao sair).
    /// </summary>
    public partial class ConstrucoesNoMundo
    {
        /// <summary>O lugar da panela (os espetos: 0 a 2).</summary>
        public const int LugarDaPanela = 3;
        const int Espetos = 3;
        const float AlcanceDeAssar = 4f;

        NetworkList<Assando> _assando;
        float _proximoAssar;
        readonly Dictionary<int, GameObject> _assandoObjetos = new Dictionary<int, GameObject>();
        readonly HashSet<int> _assandoVistos = new HashSet<int>();
        readonly List<int> _assandoSobrando = new List<int>();
        MaterialPropertyBlock _tinta;

        static int Chave(ushort fogueira, int lugar) => fogueira * 8 + lugar;

        // ------------------------------------------------------------------ o que assa e quanto tempo

        /// <summary>Vai no espeto: o peixe cru e a carne de caça crua.</summary>
        public static bool VaiNoEspeto(Item i) => Itens.EhPeixeCru(i) || i == Item.CarneCrua;

        /// <summary>Vai na panela: as latas de comida.</summary>
        public static bool VaiNaPanela(Item i) => i == Item.Comida || i == Item.Feijao || i == Item.Carne || i == Item.Milho || i == Item.Sopa;

        public static bool Assavel(Item i) => VaiNoEspeto(i) || VaiNaPanela(i);

        /// <summary>Os segundos de fogo até ficar pronto.</summary>
        public static float Pronto(Item i) => VaiNaPanela(i) ? 30f : i == Item.CarneCrua ? 60f : 45f;

        /// <summary>Os segundos de fogo até queimar (esquecido).</summary>
        public static float Queima(Item i) => Pronto(i) + (VaiNaPanela(i) ? 90f : 60f);

        /// <summary>O que sai do espeto pronto, e quantos (os filés pelo tamanho do peixe, como era a receita).</summary>
        public static Parte Assado(Item cru) => cru switch
        {
            Item.CarneCrua => new Parte(Item.CarneAssada, 1),
            Item.PeixePequeno => new Parte(Item.PeixeAssado, 1),
            Item.PeixeMedio => new Parte(Item.PeixeAssado, 2),
            Item.PeixeGrande => new Parte(Item.PeixeAssado, 3),
            Item.PeixeDourado => new Parte(Item.PeixeAssado, 3),
            Item.Bagre => new Parte(Item.PeixeAssado, 2),
            Item.Salmao => new Parte(Item.PeixeAssado, 3),
            _ => new Parte(Item.Nada, 0),
        };

        /// <summary>A lata quente acalma a cabeça (de uma vez).</summary>
        public const float MenteDaLataQuente = 6f;

        // ------------------------------------------------------------------ a lista

        void PrepararAssar() => _assando = new NetworkList<Assando>();

        void AssarAoNascer()
        {
            if (IsServer) _assando.Clear();
            _assando.OnListChanged += AoMudarAssando;
            AtualizarAssando();
        }

        void AssarAoSumir()
        {
            _assando.OnListChanged -= AoMudarAssando;
            foreach (var o in _assandoObjetos.Values) if (o != null) Destroy(o);
            _assandoObjetos.Clear();
        }

        void AoMudarAssando(NetworkListEvent<Assando> e) => AtualizarAssando();

        int IndiceAssando(ushort fogueira, int lugar)
        {
            for (int i = 0; i < _assando.Count; i++)
                if (_assando[i].fogueira == fogueira && _assando[i].lugar == lugar) return i;
            return -1;
        }

        /// <summary>O que assa no lugar da fogueira (null: nada).</summary>
        public Assando? AssandoEm(ushort fogueira, int lugar)
        {
            int i = IndiceAssando(fogueira, lugar);
            return i < 0 ? (Assando?)null : _assando[i];
        }

        /// <summary>A fogueira tem comida no fogo (a portátil não se recolhe assim).</summary>
        public bool TemComidaNoFogo(ushort fogueira)
        {
            foreach (var a in _assando)
                if (a.fogueira == fogueira) return true;
            return false;
        }

        /// <summary>
        /// O lugar livre para a comida (os espetos ou a panela), ou -1 e o porquê: falta o espeto ou a panela, ou os lugares
        /// estão ocupados.
        /// </summary>
        public int LugarParaAssar(ushort fogueira, Item comida, InventarioJogador inv, out string porque)
        {
            porque = null;
            if (VaiNaPanela(comida))
            {
                if (inv.Quantos(Item.Panela) == 0) { porque = "Precisa de uma panela para esquentar a lata"; return -1; }
                if (IndiceAssando(fogueira, LugarDaPanela) >= 0) { porque = "A panela já está no fogo"; return -1; }
                return LugarDaPanela;
            }
            if (!VaiNoEspeto(comida)) { porque = "Isso não vai no fogo"; return -1; }
            if (inv.Quantos(Item.Espeto) == 0) { porque = "Precisa de um espeto (2 gravetos, na aba Criar)"; return -1; }
            for (int k = 0; k < Espetos; k++)
                if (IndiceAssando(fogueira, k) < 0) return k;
            porque = "Os espetos em volta do fogo estão todos ocupados";
            return -1;
        }

        // ------------------------------------------------------------------ pôr e tirar (o anfitrião confere)

        /// <summary>Põe no fogo a comida da mão (o clique na fogueira).</summary>
        public void PedirAssar(ushort fogueira) => AssarRpc(fogueira);

        /// <summary>Tira do fogo o que está no lugar (o E na comida).</summary>
        public void PedirTirarDoFogo(ushort fogueira, byte lugar) => TirarDoFogoRpc(fogueira, lugar);

        [Rpc(SendTo.Server)]
        void AssarRpc(ushort id, RpcParams parametros = default)
        {
            var inv = InventarioDe(NetworkManager, parametros.Receive.SenderClientId);
            int k = Indice(id);
            if (inv == null || k < 0) return;
            var c = _lista[k];
            if (!Itens.EhFogueira(c.tipo) || (inv.transform.position - c.pos).sqrMagnitude > AlcanceDeAssar * AlcanceDeAssar) return;
            if (!Acesa(c))
            {
                inv.AvisarDono("Acenda a fogueira antes de assar");
                return;
            }
            Item comida = inv.NaMao;
            ushort daMao = inv.IdNaMao;
            int lugar = LugarParaAssar(id, comida, inv, out string porque);
            if (lugar < 0 || daMao == 0)
            {
                if (porque != null) inv.AvisarDono(porque);
                return;
            }
            if (!inv.GastarItem(lugar == LugarDaPanela ? Item.Panela : Item.Espeto, 1)) return;
            if (inv.TirarUm(daMao) != comida) return;
            _assando.Add(new Assando { fogueira = id, lugar = (byte)lugar, comida = comida, tempo = 0f });
        }

        [Rpc(SendTo.Server)]
        void TirarDoFogoRpc(ushort id, byte lugar, RpcParams parametros = default)
        {
            var inv = InventarioDe(NetworkManager, parametros.Receive.SenderClientId);
            int i = IndiceAssando(id, lugar);
            var c = Dados(id);
            if (inv == null || i < 0 || c == null || (inv.transform.position - c.Value.pos).sqrMagnitude > AlcanceDeAssar * AlcanceDeAssar) return;
            var a = _assando[i];
            _assando.RemoveAt(i);
            bool panela = a.lugar == LugarDaPanela;
            Item ferramenta = panela ? Item.Panela : Item.Espeto;
            if (a.tempo < Pronto(a.comida))
            {
                // ainda cru (ou frio): volta como estava
                inv.EntregarItem(a.comida, 1);
                inv.EntregarItem(ferramenta, 1);
                return;
            }
            if (a.tempo >= Queima(a.comida))
            {
                // queimou: a comida se perde (e o espeto, que é de pau, queima junto; a panela fica)
                if (panela)
                {
                    inv.EntregarItem(Item.Panela, 1);
                    inv.AvisarDono("A comida grudou na panela e queimou");
                }
                else inv.AvisarDono($"{TelaDoInventario.Maiuscula(Itens.NomeComArtigo(a.comida))} queimou, e o espeto junto");
                return;
            }
            if (panela)
            {
                // a lata quente se come ali mesmo: mata a fome como a fria, e acalma a cabeça
                inv.GetComponent<FomeESede>()?.Consumir(Itens.Consumivel(a.comida));
                inv.GetComponent<VidaJogador>()?.AcalmarAgora(MenteDaLataQuente, MotivoDaMente.ComidaQuente);
                inv.EntregarItem(Item.Panela, 1);
                inv.AvisarDono($"Comeu {Itens.NomeComArtigo(a.comida)} quente, ali no fogo");
                return;
            }
            var assado = Assado(a.comida);
            if (assado.qtd > 0) inv.EntregarItem(assado.item, assado.qtd);
            inv.EntregarItem(Item.Espeto, 1);
        }

        // o anfitrião, a cada segundo: o fogo aceso conta para o que está nele; a fogueira que sumiu (recolhida) leva junto
        void AssarNoServidor()
        {
            if (Time.time < _proximoAssar) return;
            float passo = 1f;
            _proximoAssar = Time.time + passo;
            for (int i = _assando.Count - 1; i >= 0; i--)
            {
                var a = _assando[i];
                var c = Dados(a.fogueira);
                if (c == null)
                {
                    _assando.RemoveAt(i);
                    continue;
                }
                if (!Acesa(c.Value) || a.tempo >= Queima(a.comida) + 1f) continue;
                a.tempo += passo;
                _assando[i] = a;
            }
        }

        // ------------------------------------------------------------------ o que se vê (cada cópia)

        void AtualizarAssando()
        {
            _assandoVistos.Clear();
            foreach (var a in _assando)
            {
                var c = Dados(a.fogueira);
                if (c == null) continue;
                int chave = Chave(a.fogueira, a.lugar);
                _assandoVistos.Add(chave);
                if (!_assandoObjetos.TryGetValue(chave, out var o) || o == null) _assandoObjetos[chave] = o = MontarAssando(c.Value, a);
                if (o != null)
                {
                    Tingir(o, a);
                    Chiar(o, Acesa(c.Value));
                }
            }
            _assandoSobrando.Clear();
            foreach (var chave in _assandoObjetos.Keys)
                if (!_assandoVistos.Contains(chave)) _assandoSobrando.Add(chave);
            foreach (var chave in _assandoSobrando)
            {
                if (_assandoObjetos[chave] != null) Destroy(_assandoObjetos[chave]);
                _assandoObjetos.Remove(chave);
            }
        }

        // (20/11/2026, o som do sfxjar que o Pedro aprovou) a comida chiando no fogo: em laço, só com a fogueira acesa
        static void Chiar(GameObject o, bool acesa)
        {
            var som = o.GetComponent<AudioSource>();
            if (som == null)
            {
                var clip = Combat.SonsGravados.Um("Comida", "chiando_");
                if (clip == null) return;
                som = o.AddComponent<AudioSource>();
                som.clip = clip;
                som.loop = true;
                som.playOnAwake = false;
                som.spatialBlend = 1f;
                som.rolloffMode = AudioRolloffMode.Linear;
                som.minDistance = 1f;
                som.maxDistance = 14f;
                som.dopplerLevel = 0f;
                Combat.MixerDeSom.NoCanal(som, Combat.CanalDoSom.Fogo, 0.35f);   // (o canal Fogo do mixer)
                som.time = UnityEngine.Random.Range(0f, clip.length * 0.9f);
            }
            if (acesa && !som.isPlaying) som.Play();
            else if (!acesa && som.isPlaying) som.Stop();
        }

        static GameObject Peca(Transform pai, string nome, Mesh malha, Material material)
        {
            var go = new GameObject(nome);
            go.transform.SetParent(pai, false);
            go.AddComponent<MeshFilter>().sharedMesh = malha;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = ShadowCastingMode.On;
            return go;
        }

        // a altura de cima da lenha (onde a panela apoia)
        float AlturaDaLenha(Item tipo)
        {
            var m = MalhaDe(tipo);
            return m != null ? Mathf.Clamp(m.bounds.max.y * 0.85f, 0.12f, 0.35f) : 0.2f;
        }

        GameObject MontarAssando(Construcao c, Assando a)
        {
            var itens = ItensNoMundo.Instancia;
            if (itens == null) return null;
            var raiz = new GameObject($"Assando {a.fogueira}.{a.lugar} ({Itens.Nome(a.comida)})");
            raiz.transform.SetParent(transform, false);
            var giro = Quaternion.Euler(0f, c.giro, 0f);
            raiz.transform.SetPositionAndRotation(c.pos, giro);
            var comidaMalha = itens.MalhaDoLoot(a.comida);
            var comidaMaterial = itens.MaterialDe(a.comida);
            Vector3 centroDaComida;
            if (a.lugar == LugarDaPanela)
            {
                // a panela em cima da lenha, a lata dentro dela
                float y = AlturaDaLenha(c.tipo);
                var panela = Peca(raiz.transform, "Panela", ItensDaCaca.MalhaDoItem(Item.Panela), itens.MaterialDe(Item.Panela));
                panela.transform.localPosition = new Vector3(0.09f * 1.25f, y, 0f);   // (o corpo no meio do fogo: a malha é centrada com o cabo)
                panela.transform.localScale = Vector3.one * 1.25f;
                if (comidaMalha != null)
                {
                    var lata = Peca(raiz.transform, "Comida", comidaMalha, comidaMaterial);
                    lata.transform.localPosition = new Vector3(0f, y + 0.015f, 0f);
                }
                centroDaComida = new Vector3(0f, y + 0.07f, 0f);
            }
            else
            {
                // o espeto fincado no chão, do lado de fora das pedras, inclinado por cima do fogo; a comida espetada nele
                float angulo = 30f + a.lugar * 120f;
                Vector3 dir = Quaternion.Euler(0f, angulo, 0f) * Vector3.forward;
                Vector3 baixo = dir * 0.68f, alto = dir * 0.02f + Vector3.up * 0.56f;
                Vector3 eixo = (alto - baixo).normalized;
                Vector3 lado = Vector3.Cross(Vector3.up, eixo).normalized;
                Quaternion noEixo = Quaternion.LookRotation(Vector3.Cross(eixo, lado), lado);   // X no eixo, Y de lado
                float comprimento = (alto - baixo).magnitude;
                var espeto = Peca(raiz.transform, "Espeto", ItensDaCaca.MalhaDoItem(Item.Espeto), itens.MaterialDe(Item.Espeto));
                espeto.transform.localPosition = (baixo + alto) * 0.5f;
                espeto.transform.localRotation = Quaternion.FromToRotation(Vector3.right, eixo);
                espeto.transform.localScale = new Vector3(comprimento / 0.48f, 1.3f, 1.3f);
                centroDaComida = Vector3.Lerp(baixo, alto, 0.7f);
                if (comidaMalha != null)
                {
                    var comida = Peca(raiz.transform, "Comida", comidaMalha, comidaMaterial);
                    // a malha do saque tem o apoio embaixo do meio: o meio dela vai no espeto
                    var b = comidaMalha.bounds;
                    comida.transform.localRotation = noEixo;
                    comida.transform.localPosition = centroDaComida - noEixo * b.center;
                }
            }
            // o E na comida: um gatilho em volta dela (na camada que os tiros ignoram)
            var pegar = new GameObject("Pegar");
            pegar.layer = 2;
            pegar.transform.SetParent(raiz.transform, false);
            pegar.transform.localPosition = centroDaComida;
            var caixa = pegar.AddComponent<BoxCollider>();
            caixa.isTrigger = true;
            caixa.size = a.lugar == LugarDaPanela ? new Vector3(0.42f, 0.2f, 0.36f) : new Vector3(0.32f, 0.26f, 0.32f);
            var mira = pegar.AddComponent<ComidaNoFogo>();
            mira.mundo = this;
            mira.fogueira = a.fogueira;
            mira.lugar = a.lugar;
            return raiz;
        }

        // a comida doura com o fogo e escurece queimando (a lata não muda)
        void Tingir(GameObject o, Assando a)
        {
            if (VaiNaPanela(a.comida)) return;
            var comida = o.transform.Find("Comida");
            if (comida == null || !comida.TryGetComponent<MeshRenderer>(out var r)) return;
            float pronto = Pronto(a.comida), queima = Queima(a.comida);
            Color cor = a.tempo < pronto ? Color.Lerp(Color.white, new Color(0.8f, 0.6f, 0.42f), a.tempo / pronto)
                : Color.Lerp(new Color(0.8f, 0.6f, 0.42f), new Color(0.16f, 0.13f, 0.11f), Mathf.InverseLerp(pronto, queima, a.tempo));
            _tinta ??= new MaterialPropertyBlock();
            r.GetPropertyBlock(_tinta);
            _tinta.SetColor("_BaseColor", cor);
            _tinta.SetColor("_Color", cor);
            r.SetPropertyBlock(_tinta);
        }

        /// <summary>A dica da fogueira com comida na mão (null: não é comida que assa).</summary>
        public string DicaDeAssar(Construcao c, ArmaJogador quem)
        {
            var inv = quem.Inventario;
            if (inv == null || !Assavel(inv.NaMao)) return null;
            if (!Acesa(c)) return "Acenda a fogueira para assar";
            int lugar = LugarParaAssar(c.id, inv.NaMao, inv, out string porque);
            if (lugar < 0) return porque;
            return lugar == LugarDaPanela ? $"[clique] Esquentar na panela ({Pronto(inv.NaMao):0} s)"
                : $"[clique] Assar no espeto ({Pronto(inv.NaMao):0} s)";
        }

        // ------------------------------------------------------------------ o teste (F1, o painel da Fauna)

        /// <summary>Só no anfitrião: uma fogueira acesa (10 min) a 2 m na frente do jogador.</summary>
        public string FogueiraDeTeste(JogadorRede j)
        {
            if (!IsServer || j == null) return null;
            Vector3 frente = j.transform.forward;
            frente.y = 0f;
            frente = frente.sqrMagnitude > 0.01f ? frente.normalized : Vector3.forward;
            Vector3 ponto = j.transform.position + frente * 2f;
            if (Physics.Raycast(ponto + Vector3.up * 2f, Vector3.down, out var chao, 6f, 1, QueryTriggerInteraction.Ignore)) ponto = chao.point;
            _lista.Add(new Construcao { id = _proximoId++, tipo = Item.Fogueira, pos = ponto, giro = j.transform.eulerAngles.y, valor = Agora + 10f * 60f });
            return "Fogueira acesa a 2 m (10 min de lenha). Segure E nela para sentar; com a comida na mão, clique nela.";
        }

        /// <summary>Só no anfitrião: o que se usa na fogueira (a lenha, os espetos, a panela e a comida para assar).</summary>
        public static string ItensDaFogueiraDeTeste(InventarioJogador inv)
        {
            if (inv == null) return null;
            var itens = new[] { (Item.Madeira, 4), (Item.Gravetos, 6), (Item.Espeto, 3), (Item.Panela, 1), (Item.PeixeMedio, 1), (Item.CarneCrua, 1), (Item.Feijao, 1) };
            foreach (var (item, n) in itens) inv.EntregarItem(item, n);   // (o que não couber cai aos pés)
            return "4 madeiras, 6 gravetos, 3 espetos, a panela, um peixe médio, a carne crua e a lata de feijão (o que não coube na mochila está aos seus pés).";
        }

        /// <summary>O clique na fogueira põe a comida da mão no fogo.</summary>
        public bool CliqueAssa(Construcao c, ArmaJogador quem) =>
            quem.Inventario != null && Assavel(quem.Inventario.NaMao) && Acesa(c) && LugarParaAssar(c.id, quem.Inventario.NaMao, quem.Inventario, out _) >= 0;
    }

    /// <summary>A comida no fogo (o espeto ou a panela): a dica de como ela está e o E que tira (18/11/2026).</summary>
    public class ComidaNoFogo : Interagivel
    {
        public ConstrucoesNoMundo mundo;
        public ushort fogueira;
        public byte lugar;

        public override string Dica(ArmaJogador quem)
        {
            var a = mundo != null ? mundo.AssandoEm(fogueira, lugar) : null;
            if (a == null) return null;
            var v = a.Value;
            bool panela = lugar == ConstrucoesNoMundo.LugarDaPanela;
            float pronto = ConstrucoesNoMundo.Pronto(v.comida), queima = ConstrucoesNoMundo.Queima(v.comida);
            string nome = TelaDoInventario.Maiuscula(Itens.Nome(v.comida));
            if (v.tempo < pronto)
                return panela ? $"{nome} esquentando: {pronto - v.tempo:0} s   ·   [E] Tirar (ainda fria)"
                    : $"{nome} no espeto: {pronto - v.tempo:0} s para assar   ·   [E] Tirar (ainda cru)";
            if (v.tempo >= queima)
                return panela ? "A comida queimou na panela   ·   [E] Tirar a panela" : $"{nome} queimou   ·   [E] Tirar (o espeto queimou junto)";
            string pressa = v.tempo > pronto + (queima - pronto) * 0.6f ? "Vai queimar!   ·   " : "Pronto!   ·   ";
            if (panela) return pressa + $"[E] Comer {Itens.NomeComArtigo(v.comida)} quente (acalma)";
            var assado = ConstrucoesNoMundo.Assado(v.comida);
            return pressa + $"[E] Pegar {Itens.ComNumero(assado.item, assado.qtd)}";
        }

        public override void Usar(ArmaJogador quem) => mundo?.PedirTirarDoFogo(fogueira, lugar);
    }
}
