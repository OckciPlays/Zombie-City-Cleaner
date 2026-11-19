using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using ZombieCityCleaner.Player;

namespace ZombieCityCleaner.World
{
    /// <summary>
    /// Uma árvore caída no chão (18/11/2026, a pedido: "quando cortar a árvore, a árvore cair inteira e a gente dividir ela em 3
    /// troncos se for uma árvore grande - caso for árvore pequena, a gente consegue pegar alguns gravetos"). O pé, o rumo e a
    /// inclinação da queda (o anfitrião acha onde o tronco encosta no chão), o tamanho, em quantos pedaços ela vira e o corte
    /// em andamento. Vai pela rede (<see cref="MundoDeLakewood"/>) e no save.
    /// </summary>
    [System.Serializable]
    public struct TroncoNoChao : INetworkSerializeByMemcpy, System.IEquatable<TroncoNoChao>
    {
        public long chave;
        /// <summary>O pé da árvore (no mundo).</summary>
        public Vector3 pe;
        /// <summary>Para onde ela caiu (graus, como o giro do jogador) e quanto ela deitou desde de pé (graus).</summary>
        public float rumo, inclinacao;
        /// <summary>A altura da árvore em pé e o raio do tronco embaixo (m).</summary>
        public float altura, raio;
        /// <summary>Quando caiu (o tempo do servidor; para a queda aparecer só em quem viu cair).</summary>
        public float caiu;
        /// <summary>Em quantos pedaços ela vira (1: a pequena, só gravetos; 2 ou 3 toras), quantos já saíram e os golpes do corte de agora.</summary>
        public byte pedacos, cortes, golpes;

        public bool Equals(TroncoNoChao o) => chave == o.chave && cortes == o.cortes && golpes == o.golpes && caiu == o.caiu && pe == o.pe;

        /// <summary>O giro do tronco deitado: o "para cima" da árvore vira o <see cref="Eixo"/>.</summary>
        public Quaternion Giro => GiroDe(rumo, inclinacao);
        public Vector3 Eixo => Giro * Vector3.up;
        /// <summary>O tronco que vira tora (sem a copa).</summary>
        public float Comprimento => altura * 0.62f;
        public Vector3 Ponto(float s) => pe + Eixo * s;
        /// <summary>Os golpes de cada corte do tronco deitado.</summary>
        public int GolpesPorCorte => pedacos <= 1 ? 2 : pedacos == 2 ? 3 : 4;
        /// <summary>Onde fica o corte i, do pé (m): as divisas entre as toras (a pequena, no meio).</summary>
        public float Corte(int i) => pedacos <= 1 ? Comprimento * 0.5f : Comprimento * (i + 1) / pedacos;
        /// <summary>O meio da tora i, do pé (m).</summary>
        public float MeioDaTora(int i) => Comprimento * (i + 0.5f) / Mathf.Max(1, (int)pedacos);
        /// <summary>O raio do tronco a s m do pé (o pinheiro afina para cima).</summary>
        public float RaioEm(float s) => raio * Mathf.Lerp(1f, 0.35f, Mathf.Clamp01(s / Mathf.Max(1f, altura)));

        public static Quaternion GiroDe(float rumo, float inclinacao)
        {
            var d = Quaternion.Euler(0f, rumo, 0f) * Vector3.forward;
            return Quaternion.AngleAxis(inclinacao, Vector3.Cross(Vector3.up, d).normalized);
        }

        /// <summary>Em quantos pedaços vira uma árvore desta altura: até 8 m, a pequena (gravetos); até 14 m, 2 toras; depois, 3.</summary>
        public static byte PedacosPelaAltura(float altura) => (byte)(altura < 8f ? 1 : altura < 14f ? 2 : 3);
    }

    /// <summary>
    /// As árvores caídas do Lakewood (18/11/2026), no anfitrião: a queda (onde o tronco encosta no chão), o dano em quem está
    /// embaixo quando ela bate, o tronco deitado cortado em toras (ou em gravetos, a pequena) e o save. Quem desenha é o
    /// <see cref="LakewoodNaCena"/> (LakewoodNaCena.Troncos.cs).
    /// </summary>
    public partial class MundoDeLakewood
    {
        /// <summary>Quanto tempo a árvore leva caindo até bater no chão (s; o mesmo do desenho).</summary>
        public const float SegundosCaindo = 2.4f;
        /// <summary>No máximo de árvores caídas ao mesmo tempo (a mais velha some quando passa).</summary>
        const int MaxTroncos = 40;

        NetworkList<TroncoNoChao> _troncos;
        // as quedas que ainda vão bater no chão (o dano), no anfitrião
        readonly List<(float quando, TroncoNoChao t, JogadorRede autor)> _quedas = new List<(float, TroncoNoChao, JogadorRede)>();

        void IniciarOsTroncos()
        {
            _troncos = new NetworkList<TroncoNoChao>();
            _troncos.OnListChanged += _ => LakewoodNaCena.Instancia?.AcertarTroncos(CopiaDosTroncos());
        }

        List<TroncoNoChao> CopiaDosTroncos()
        {
            var c = new List<TroncoNoChao>(_troncos != null ? _troncos.Count : 0);
            if (_troncos != null) foreach (var t in _troncos) c.Add(t);
            return c;
        }

        void AoNascerOsTroncos() => LakewoodNaCena.Instancia?.AcertarTroncos(CopiaDosTroncos());
        void AoSumirOsTroncos() => LakewoodNaCena.Instancia?.AcertarTroncos(new List<TroncoNoChao>());

        float AgoraNoServidor => NetworkManager != null ? NetworkManager.ServerTime.TimeAsFloat : Time.time;

        /// <summary>As árvores caídas (o save).</summary>
        public List<TroncoNoChao> TroncosNoChao() => CopiaDosTroncos();

        /// <summary>No anfitrião: as árvores caídas de um save (deitadas, sem a queda).</summary>
        public void RestaurarTroncos(IEnumerable<TroncoNoChao> troncos)
        {
            if (!IsServer || _troncos == null) return;
            _troncos.Clear();
            if (troncos == null) return;
            foreach (var t in troncos)
            {
                var c = t;
                c.caiu = -1000f;
                c.golpes = 0;
                _troncos.Add(c);
            }
        }

        /// <summary>
        /// No anfitrião: a árvore cai para o rumo (o pé, a altura e o raio do tronco): acha onde ela encosta no chão, põe na
        /// lista e marca o tombo para o dano de quem estiver embaixo.
        /// </summary>
        void Cair(long chave, Vector3 pe, Vector3 rumo, float altura, float raio, JogadorRede autor)
        {
            if (_troncos == null) return;
            rumo.y = 0f;
            if (rumo.sqrMagnitude < 0.01f) rumo = Vector3.forward;
            float giro = Mathf.Atan2(rumo.x, rumo.z) * Mathf.Rad2Deg;
            var t = new TroncoNoChao
            {
                chave = chave, pe = pe, rumo = giro, altura = altura, raio = raio, caiu = AgoraNoServidor,
                pedacos = TroncoNoChao.PedacosPelaAltura(altura),
            };
            t.inclinacao = OndeEncosta(t);
            for (int i = _troncos.Count - 1; i >= 0; i--) if (_troncos[i].chave == chave) _troncos.RemoveAt(i);
            while (_troncos.Count >= MaxTroncos) _troncos.RemoveAt(0);
            _troncos.Add(t);
            _quedas.Add((Time.time + SegundosCaindo, t, autor));
        }

        // o ângulo em que o tronco deitado encosta no chão (de 45 a 110°: no morro abaixo, passa do deitado). Só do meio do
        // tronco para cima: perto do pé, que é o eixo da queda, o tronco está sempre rente ao chão (18/11/2026: olhando
        // dali, ela parava a uns 68°, com o tronco no ar)
        static float OndeEncosta(TroncoNoChao t)
        {
            var chao = LakewoodNaCena.Instancia != null ? LakewoodNaCena.Instancia.ChaoNoMundo : null;
            if (chao == null) return 88f;
            for (float a = 30f; a <= 110f; a += 1f)
            {
                t.inclinacao = a;
                for (int k = 2; k <= 6; k++)
                {
                    float s = t.altura * 0.92f * k / 6f;
                    var p = t.Ponto(s);
                    if (p.y - t.RaioEm(s) < chao.Altura(p.x, p.z)) return Mathf.Max(a, 45f);
                }
            }
            return 110f;
        }

        void Update() => CuidarDasQuedas();

        // a cada quadro, no anfitrião: as árvores que acabaram de bater no chão machucam quem está embaixo
        void CuidarDasQuedas()
        {
            if (!IsServer || _quedas.Count == 0) return;
            for (int i = _quedas.Count - 1; i >= 0; i--)
            {
                if (Time.time < _quedas[i].quando) continue;
                var q = _quedas[i];
                _quedas.RemoveAt(i);
                Esmagar(q.t, q.autor);
            }
        }

        /// <summary>
        /// O dano da árvore que caiu (a pedido: "ela pode machucar, dar bastante dano"): em quem está perto do tronco deitado,
        /// mais perto da copa larga e da árvore grande. Os jogadores, os zumbis, os sobreviventes e os bichos.
        /// </summary>
        void Esmagar(TroncoNoChao t, JogadorRede autor)
        {
            float forca = Mathf.Lerp(45f, 140f, Mathf.InverseLerp(6f, 20f, t.altura));
            Vector3 a = t.Ponto(0.8f), b = t.Ponto(t.altura * 0.95f);
            // o quanto pega (0 a 1) e onde, para um ponto do corpo
            float Pega(Vector3 p, out Vector3 ponto)
            {
                Vector3 ab = b - a;
                float u = Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude);
                ponto = a + ab * u;
                float alcance = 0.6f + t.RaioEm(u * t.altura) + 1.8f * u;   // a copa pega mais longe
                float d = Vector3.Distance(p, ponto);
                return d > alcance ? 0f : 1f - 0.5f * d / alcance;
            }
            foreach (var j in JogadorRede.Todos)
            {
                if (j == null || j.Vida == null) continue;
                float k = Pega(j.transform.position + Vector3.up * 0.9f, out var ponto);
                if (k > 0f) j.Vida.Dano(forca * k, ponto + Vector3.up, "Esmagado por uma árvore.", TipoDeGolpe.Esmagamento);
            }
            var zm = Zombies.ZombieManager.Instancia;
            if (zm != null)
            {
                var ativos = new List<Zombies.Zumbi>(zm.Ativos);
                foreach (var z in ativos)
                {
                    if (z == null || z.Morto) continue;
                    float k = Pega(z.transform.position + Vector3.up * 0.9f, out var ponto);
                    if (k > 0f) Zombies.ZombieManager.Acertar(z.Id, z.Geracao, Zombies.RegiaoDoCorpo.Tronco, Zombies.Membro.Nenhum, forca * 1.6f * k, ponto, true, autor);
                }
            }
            foreach (var s in new List<Sobreviventes.Sobrevivente>(Sobreviventes.Sobrevivente.Todos))
            {
                if (s == null || s.Morto) continue;
                float k = Pega(s.transform.position + Vector3.up * 0.9f, out var ponto);
                if (k > 0f) s.LevarTiro(Sobreviventes.RegiaoDoSobrevivente.Tronco, forca * k, ponto, autor, false);
            }
            var fauna = Fauna.FaunaDoMundo.Instancia;
            if (fauna != null)
                foreach (var bicho in new List<Fauna.Bicho>(fauna.Ativos))
                {
                    if (bicho == null || bicho.Morto) continue;
                    float k = Pega(bicho.transform.position + Vector3.up * 0.4f, out _);
                    if (k > 0f) fauna.Acertar(bicho.Id, bicho.Geracao, forca * 1.4f * k, bicho.transform.position + Vector3.up * bicho.Altura * 0.6f, false, false, autor);
                }
        }

        // ------------------------------------------------------------------ o tronco deitado

        /// <summary>No dono: um golpe do machado na árvore caída, no ponto.</summary>
        public void PedirGolpeNoTronco(long chave, Vector3 ponto)
        {
            if (IsSpawned) GolpeNoTroncoRpc(chave, ponto);
        }

        [Rpc(SendTo.Server)]
        void GolpeNoTroncoRpc(long chave, Vector3 ponto, RpcParams parametros = default)
        {
            ulong quem = parametros.Receive.SenderClientId;
            if (_troncos == null || !NetworkManager.ConnectedClients.TryGetValue(quem, out var cliente) || cliente.PlayerObject == null) return;
            var j = cliente.PlayerObject.GetComponent<JogadorRede>();
            int i = -1;
            for (int k = 0; k < _troncos.Count; k++) if (_troncos[k].chave == chave) { i = k; break; }
            if (j == null || i < 0) return;
            var t = _troncos[i];
            if (AgoraNoServidor - t.caiu < SegundosCaindo) return;   // ainda caindo
            // quem bateu está perto do tronco deitado
            Vector3 a = t.pe, b = t.Ponto(t.Comprimento);
            Vector3 ab = b - a;
            var de = j.transform.position + Vector3.up;
            var perto = a + ab * Mathf.Clamp01(Vector3.Dot(de - a, ab) / ab.sqrMagnitude);
            if ((perto - de).sqrMagnitude > 4.5f * 4.5f) return;
            t.golpes++;
            GolpeNoTroncoEfeitoRpc(ponto, quem, t.golpes, (byte)t.GolpesPorCorte, t.cortes, t.pedacos);
            if (t.golpes >= t.GolpesPorCorte)
            {
                // um pedaço sai: as toras (ou os gravetos da pequena) no chão, ao lado do tronco
                t.golpes = 0;
                SoltarPedaco(t, t.cortes, j);
                t.cortes++;
            }
            if (t.cortes >= t.pedacos) _troncos.RemoveAt(i);
            else _troncos[i] = t;
        }

        void SoltarPedaco(TroncoNoChao t, int qual, JogadorRede j)
        {
            var itens = Loot.ItensNoMundo.Instancia;
            var chao = LakewoodNaCena.Instancia != null ? LakewoodNaCena.Instancia.ChaoNoMundo : null;
            Vector3 lado = Vector3.Cross(Vector3.up, t.Eixo);
            lado = lado.sqrMagnitude > 0.01f ? lado.normalized : Vector3.right;
            if (j != null && Vector3.Dot(j.transform.position - t.pe, lado) < 0f) lado = -lado;   // para o lado de quem cortou
            var onde = t.Ponto(t.pedacos <= 1 ? t.Comprimento * 0.5f : t.MeioDaTora(qual)) + lado * (t.RaioEm(0f) + 0.5f);
            onde.y = (chao != null ? chao.Altura(onde.x, onde.z) : onde.y) + 0.05f;
            if (t.pedacos <= 1)
            {
                itens?.Criar(Loot.Item.Gravetos, onde, Random.Range(0f, 360f), Random.Range(3, 6));
                if (j != null) AvisoRpc("A árvore pequena virou gravetos", RpcTarget.Single(j.OwnerClientId, RpcTargetUse.Temp));
            }
            else
            {
                itens?.Criar(Loot.Item.Toras, onde, Random.Range(0f, 360f), 1);
                if (j != null)
                {
                    int faltam = t.pedacos - qual - 1;
                    AvisoRpc(faltam > 0 ? $"Uma tora saiu do tronco (faltam {faltam})" : "A última tora saiu do tronco",
                             RpcTarget.Single(j.OwnerClientId, RpcTargetUse.Temp));
                }
            }
            j?.Inventario?.GanharXp(1, "tora");
        }

        [Rpc(SendTo.SpecifiedInParams)]
        void AvisoRpc(string texto, RpcParams parametros = default) => InventarioJogador.AvisoLocal(texto);

        [Rpc(SendTo.Everyone)]
        void GolpeNoTroncoEfeitoRpc(Vector3 ponto, ulong quem, byte golpes, byte precisa, byte cortes, byte pedacos)
        {
            LakewoodNaCena.Instancia?.GolpeNaMadeira(ponto, Vector3.up, 0.8f);
            if (NetworkManager == null || quem != NetworkManager.LocalClientId || golpes >= precisa) return;
            InventarioJogador.AvisoLocal(pedacos <= 1 ? $"Quebrando a árvore pequena: {golpes} de {precisa}"
                                                      : $"Cortando a tora {cortes + 1} de {pedacos}: {golpes} de {precisa}");
        }
    }
}
