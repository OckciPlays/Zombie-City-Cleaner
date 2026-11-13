using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using ZombieCityCleaner.Combat;
using ZombieCityCleaner.Player;
using ZombieCityCleaner.Sobreviventes;
using ZombieCityCleaner.Zombies;

namespace ZombieCityCleaner.Loot
{
    /// <summary>
    /// O fogo da etapa 4 do ramo Fogo (11/11/2026; os números no <see cref="FogoDoJogador"/>), tudo do anfitrião:
    ///  - <b>o coquetel molotov</b>: a garrafa voa em arco (a gravidade, sem o Rigidbody: o anfitrião confere a batida a cada
    ///    quadro; as outras máquinas veem a mesma conta, só a imagem). Onde ela bate, quebra e acende uma <see cref="AreaDeFogo"/>
    ///    no chão embaixo (a parede derruba o fogo no chão): ela queima os zumbis (que pegam fogo e continuam queimando um
    ///    pouco: o ZombieManager.Fogo.cs), os moradores e os jogadores, e os zumbis a contam como uma fogueira (o
    ///    <see cref="AjustesDeZumbi.raioDoMolotov"/>). Fora de um teto, na chuva, ela se apaga mais rápido;
    ///  - <b>a fogueira portátil</b> (o kit <see cref="Item.KitFogueira"/>): uma construção como a fogueira de lenha, com o
    ///    modelo dela, que queima <see cref="FogoDoJogador.MinutosDaFogueiraPortatil"/> min. O E recolhe: acesa, volta o kit
    ///    com a lenha que sobrou (o desgaste do kit é a lenha que já foi); apagada (a lenha acabou, a chuva ou a gosma), sobra
    ///    só a corda. Com a tocha apagada na mão, o E nela acesa reacende a tocha, como na de lenha.
    /// </summary>
    public partial class ConstrucoesNoMundo
    {
        // ------------------------------------------------------------------ a fogueira portátil

        // o fogo que o kit dá (s): a lenha que sobrou nele
        static float SegundosDoKit(byte desgaste) => FogoDoJogador.MinutosDaFogueiraPortatil * 60f * (100 - Mathf.Clamp(desgaste, 0, 100)) / 100f;

        // no anfitrião, o E na portátil: reacende a tocha ou recolhe
        void RecolherAPortatil(int k, Construcao c, InventarioJogador inventario)
        {
            bool acesa = Acesa(c);
            if (acesa && inventario.TochaDaMaoApagada)
            {
                inventario.AcenderTochaNaMao();
                return;
            }
            _lista.RemoveAt(k);
            _molhadas.Remove(c.id);
            _tetosVistos.Remove(c.id);
            if (acesa)
            {
                float resta = c.valor - Agora;
                int desgaste = Mathf.Clamp(Mathf.RoundToInt(100f - resta / (FogoDoJogador.MinutosDaFogueiraPortatil * 60f) * 100f), 0, 99);
                if (!inventario.Receber(Item.KitFogueira, -1, desgaste)) ItensNoMundo.Instancia?.Criar(Item.KitFogueira, c.pos, c.giro, -1, desgaste);
                inventario.AvisarDono($"Recolheu a fogueira portátil ({Mathf.CeilToInt(resta / 60f)} min de lenha)");
            }
            else
            {
                inventario.EntregarItem(Item.Corda, 1);
                inventario.AvisarDono("A lenha virou cinza: sobrou a corda");
            }
        }

        /// <summary>A dica da fogueira portátil na mira.</summary>
        public string DicaDaPortatil(Construcao c, ArmaJogador quem)
        {
            bool acesa = Acesa(c);
            if (acesa && quem.Inventario != null && quem.Inventario.TochaDaMaoApagada) return "[E] Reacender a tocha no fogo";
            if (!acesa) return "Fogueira portátil apagada   ·   [E] Recolher (sobra a corda)";
            float resta = Mathf.Max(0f, c.valor - Agora);
            return $"Fogueira portátil ({Mathf.CeilToInt(resta / 60f)} min)" + (ChuvaEm(c.pos) ? "   ·   a chuva está apagando" : "") + "   ·   [E] Recolher (volta o kit)";
        }

        /// <summary>O dono da construção (a chave do personagem; 0, nenhuma): a Muralha de fogo dele vale na fogueira.</summary>
        internal ulong DonoDe(ushort id)
        {
            foreach (var c in _lista)
                if (c.id == id) return c.dono;
            return 0UL;
        }

        // ------------------------------------------------------------------ o molotov: a garrafa voando

        sealed class Garrafa
        {
            public Vector3 pos, vel, origem;
            public float ate;
            public JogadorRede dono;
            public bool servidor;
            public Transform visual;
        }

        readonly List<Garrafa> _garrafas = new List<Garrafa>();
        // o que a garrafa não pega: o Ignore Raycast (a cápsula do zumbi de perto), os braços e os corpos caídos
        static readonly int MascaraDaGarrafa = ~((1 << 2) | (1 << JogadorRede.CamadaBracos) | (1 << CorpoDeZumbi.Camada));
        static readonly RaycastHit[] _batidas = new RaycastHit[8];

        /// <summary>Só no anfitrião: a garrafa sai da origem com a velocidade (o arremesso de quem é dono).</summary>
        public void LancarMolotov(Vector3 origem, Vector3 velocidade, JogadorRede dono)
        {
            if (!IsServer || !IsSpawned) return;
            _garrafas.Add(new Garrafa { pos = origem, origem = origem, vel = velocidade, ate = Time.time + 5f, dono = dono, servidor = true });
            GarrafaRpc(origem, velocidade);
        }

        [Rpc(SendTo.ClientsAndHost)]
        void GarrafaRpc(Vector3 origem, Vector3 velocidade)
        {
            var visual = NovaGarrafa(origem, velocidade);
            if (IsServer)
            {
                // no anfitrião, a imagem vai na garrafa que decide
                for (int i = _garrafas.Count - 1; i >= 0; i--)
                    if (_garrafas[i].servidor && _garrafas[i].visual == null && (_garrafas[i].origem - origem).sqrMagnitude < 0.01f)
                    {
                        _garrafas[i].visual = visual;
                        return;
                    }
            }
            _garrafas.Add(new Garrafa { pos = origem, origem = origem, vel = velocidade, ate = Time.time + 5f, visual = visual });
        }

        Transform NovaGarrafa(Vector3 origem, Vector3 velocidade)
        {
            var d = ItensDoFogo.Dados;
            var go = new GameObject("Molotov voando");
            go.transform.SetParent(transform, false);
            go.transform.SetPositionAndRotation(origem, Quaternion.LookRotation(velocidade.sqrMagnitude > 0.01f ? velocidade : Vector3.forward));
            if (d != null && d.molotovNoCorpo != null)
            {
                go.AddComponent<MeshFilter>().sharedMesh = d.molotovNoCorpo;
                go.AddComponent<MeshRenderer>().sharedMaterial = d.material;
            }
            var fogo = Fogo(go.transform, d != null ? d.pavioDoMolotov : new Vector3(0f, 0f, 0.09f), materialDoFogo, 0.08f, 3f, true, go.layer);
            if (fogo.TryGetComponent<Light>(out var luz)) luz.intensity = 1.2f;
            return go.transform;
        }

        // cada máquina, a cada quadro: as garrafas andam; a do anfitrião, ao bater, acende o fogo
        void GarrafasAgora()
        {
            if (_garrafas.Count == 0) return;
            float dt = Time.deltaTime;
            for (int i = _garrafas.Count - 1; i >= 0; i--)
            {
                var g = _garrafas[i];
                Vector3 prox = g.pos + g.vel * dt + 0.5f * dt * dt * Physics.gravity;
                g.vel += Physics.gravity * dt;
                bool bateu = Bate(g, prox, out var batida);
                if (bateu || Time.time > g.ate)
                {
                    if (g.servidor) Quebrar(bateu ? batida.point : g.pos, bateu ? batida.normal : Vector3.up, g.dono);
                    if (g.visual != null) Destroy(g.visual.gameObject);
                    _garrafas.RemoveAt(i);
                    continue;
                }
                g.pos = prox;
                if (g.visual != null)
                {
                    g.visual.position = prox;
                    // ela gira no ar (o pano à frente, rodando de ponta a ponta)
                    g.visual.rotation = Quaternion.LookRotation(g.vel) * Quaternion.Euler((5f - (g.ate - Time.time)) * 720f, 0f, 0f);
                }
            }
        }

        bool Bate(Garrafa g, Vector3 prox, out RaycastHit batida)
        {
            batida = default;
            Vector3 d = prox - g.pos;
            float dist = d.magnitude;
            if (dist < 1e-5f) return false;
            int n = Physics.SphereCastNonAlloc(g.pos, 0.05f, d / dist, _batidas, dist, MascaraDaGarrafa, QueryTriggerInteraction.Ignore);
            float menor = float.MaxValue;
            bool achou = false;
            for (int k = 0; k < n; k++)
            {
                var h = _batidas[k];
                if (h.collider == null) continue;
                // quem arremessa (e os outros jogadores bem ali: a cápsula e as partes do corpo) não quebram a garrafa na saída
                if ((h.collider.gameObject.layer == JogadorRede.CamadaJogador || h.collider.GetComponentInParent<JogadorRede>() != null)
                    && (h.point - g.origem).sqrMagnitude < 2.5f * 2.5f) continue;
                if (h.distance <= 0f && h.point == Vector3.zero) continue;   // começou dentro dele
                if (h.distance < menor)
                {
                    menor = h.distance;
                    batida = h;
                    achou = true;
                }
            }
            return achou;
        }

        // ------------------------------------------------------------------ o molotov: a área de fogo

        /// <summary>Uma área de fogo do molotov (só no anfitrião).</summary>
        internal sealed class AreaDeFogo
        {
            public int id;
            public Vector3 centro;
            public float raio, resta;
            /// <summary>Debaixo de um teto (a chuva não a apaga mais rápido).</summary>
            public bool coberta;
            public JogadorRede dono;
            /// <summary>O dono tem a Muralha de fogo (os zumbis fogem dela em pânico, e mais longe).</summary>
            public float muralha = 1f;
            public float proximoTique;
        }

        readonly List<AreaDeFogo> _areas = new List<AreaDeFogo>();
        int _proximaArea = 1;
        float _proximoTiqueDoFogo;
        readonly Dictionary<int, GameObject> _areasVistas = new Dictionary<int, GameObject>();
        // o chão embaixo da batida: o cenário (sem os jogadores, os zumbis e os corpos)
        static readonly int MascaraDoChao = ~((1 << 2) | (1 << JogadorRede.CamadaBracos) | (1 << JogadorRede.CamadaJogador) | (1 << CorpoDeZumbi.Camada) | (1 << 7));

        /// <summary>Só no anfitrião: as áreas de fogo acesas agora (os zumbis as contam como fogos).</summary>
        internal IReadOnlyList<AreaDeFogo> AreasDeFogo => _areas;

        /// <summary>Só no anfitrião (o F1 e o teste): as áreas acesas e o resumo delas.</summary>
        public int AreasAcesas => _areas.Count;
        public string ResumoDasAreas()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var a in _areas)
                sb.Append(System.FormattableString.Invariant($"[área {a.id}: ({a.centro.x:F1}, {a.centro.y:F1}, {a.centro.z:F1}), raio {a.raio:F2}, resta {a.resta:F1} s{(a.coberta ? ", coberta" : "")}{(a.muralha > 1f ? ", muralha" : "")}] "));
            return sb.ToString();
        }

        /// <summary>Só no anfitrião: o ponto está numa área de fogo (com a folga a mais no raio)? O centro e o raio dela.</summary>
        public bool AreaDeFogoEm(Vector3 ponto, float folga, out Vector3 centro, out float raio)
        {
            centro = default;
            raio = 0f;
            foreach (var a in _areas)
            {
                Vector3 d = ponto - a.centro;
                if (Mathf.Abs(d.y) > 2f) continue;
                d.y = 0f;
                if (d.sqrMagnitude > (a.raio + folga) * (a.raio + folga)) continue;
                centro = a.centro;
                raio = a.raio;
                return true;
            }
            return false;
        }

        /// <summary>Só no anfitrião (o F1 e o teste): uma área de fogo no ponto, como a de um molotov que quebrou ali.</summary>
        public void FogoDeTeste(Vector3 ponto, JogadorRede dono) => Quebrar(ponto + Vector3.up * 0.3f, Vector3.up, dono);

        // a garrafa quebrou: o fogo pega no chão embaixo dela
        void Quebrar(Vector3 ponto, Vector3 normal, JogadorRede dono)
        {
            Vector3 chao = ponto;
            if (Physics.Raycast(ponto + normal * 0.2f + Vector3.up * 0.3f, Vector3.down, out var h, 8f, MascaraDoChao, QueryTriggerInteraction.Ignore)) chao = h.point;
            var a = new AreaDeFogo
            {
                id = _proximaArea++, centro = chao, dono = dono,
                raio = FogoDoJogador.RaioDoMolotovDe(dono), resta = FogoDoJogador.SegundosDoMolotovDe(dono),
                coberta = World.Tempo.SobTeto(chao), muralha = FogoDoJogador.MuralhaDe(dono),
            };
            _areas.Add(a);
            AreaDeFogoRpc(a.id, chao, a.raio, a.resta);
        }

        // no anfitrião, a cada quadro: as garrafas e, a cada quarto de segundo, as áreas (o tempo, a chuva e quem queima)
        void FogoNoServidor()
        {
            if (Time.time < _proximoTiqueDoFogo) return;
            const float passo = 0.25f;
            _proximoTiqueDoFogo = Time.time + passo;
            var zm = ZombieManager.Instancia;
            for (int i = _areas.Count - 1; i >= 0; i--)
            {
                var a = _areas[i];
                float ritmo = !a.coberta && ChoveNoFogo ? 1f + FogoDoJogador.ChuvaNoMolotov * World.Tempo.Chuva : 1f;
                a.resta -= passo * ritmo;
                if (a.resta <= 0f)
                {
                    _areas.RemoveAt(i);
                    ApagouAreaRpc(a.id);
                    continue;
                }
                // os zumbis dentro pegam fogo (e continuam queimando um pouco depois de sair)
                if (zm != null) zm.FogoNaArea(a.centro, a.raio, passo, a.dono, FogoDoJogador.SegundosQueimandoDe(a.dono));
                // os moradores e os jogadores dentro: um golpe por segundo (eles saem andando: o morador sozinho, o
                // Sobrevivente.Fogo.cs)
                if (Time.time < a.proximoTique) continue;
                a.proximoTique = Time.time + 1f;
                foreach (var s in Sobrevivente.Todos)
                {
                    if (s == null || !s.AlvoValido || !DentroDaArea(a, s.transform.position)) continue;
                    if (a.dono != null) s.LevarTiro(RegiaoDoSobrevivente.Tronco, FogoDoJogador.DanoNosMoradores, s.transform.position + Vector3.up, a.dono, false);
                    else s.LevarGolpe(FogoDoJogador.DanoNosMoradores, a.centro);
                }
                foreach (var j in JogadorRede.Todos)
                    if (j != null && j.IsSpawned && j.Vida != null && DentroDaArea(a, j.transform.position))
                        j.Vida.Dano(FogoDoJogador.DanoNosJogadores, a.centro, "Queimou-se.");
            }
        }

        static bool DentroDaArea(AreaDeFogo a, Vector3 p)
        {
            Vector3 d = p - a.centro;
            if (d.y < -1f || d.y > 2f) return false;
            d.y = 0f;
            return d.sqrMagnitude < a.raio * a.raio;
        }

        // ------------------------------------------------------------------ o molotov: o que se vê (em cada máquina)

        [Rpc(SendTo.ClientsAndHost)]
        void AreaDeFogoRpc(int id, Vector3 centro, float raio, float segundos)
        {
            // a garrafa que voava para lá some (a do anfitrião já sumiu na batida)
            for (int i = _garrafas.Count - 1; i >= 0; i--)
                if (!_garrafas[i].servidor && (_garrafas[i].pos - centro).sqrMagnitude < 8f * 8f)
                {
                    if (_garrafas[i].visual != null) Destroy(_garrafas[i].visual.gameObject);
                    _garrafas.RemoveAt(i);
                    break;
                }
            MostrarAreaDeFogo(id, centro, raio, segundos);
            SomDoFogo.Quebrar(centro);
        }

        // as chamas da área de fogo (o molotov e, desde 12/11/2026, o combustível do galão que estourou)
        void MostrarAreaDeFogo(int id, Vector3 centro, float raio, float segundos)
        {
            var go = new GameObject($"Fogo do molotov {id}");
            go.transform.SetParent(transform, false);
            go.transform.position = centro;
            // as chamas: uma grande no meio (com a luz) e um anel em volta, cada uma no chão embaixo dela
            var meio = Fogo(go.transform, Vector3.up * 0.05f, materialDoFogo, 1.5f, 11f, true, go.layer);
            if (meio.TryGetComponent<Light>(out var luz)) luz.intensity = 3.6f;
            // o tapete de fogo: um anel de dentro e um de fora (o combustível espalhado)
            int n = Mathf.Clamp(Mathf.RoundToInt(raio * 6f), 12, 24);
            for (int k = 0; k < n; k++)
            {
                float ang = k * Mathf.PI * 2f / n * 2.3f + Random.Range(-0.25f, 0.25f);
                float r = raio * (k % 2 == 0 ? Random.Range(0.25f, 0.55f) : Random.Range(0.6f, 0.9f));
                Vector3 p = centro + new Vector3(Mathf.Sin(ang), 0f, Mathf.Cos(ang)) * r;
                if (Physics.Raycast(p + Vector3.up * 1f, Vector3.down, out var h, 2.5f, MascaraDoChao, QueryTriggerInteraction.Ignore)) p = h.point;
                Fogo(go.transform, go.transform.InverseTransformPoint(p) + Vector3.up * 0.04f, materialDoFogo, Random.Range(0.8f, 1.15f), 0f, true, go.layer);
            }
            var tremula = go.AddComponent<ChamaTremula>();
            tremula.luz = luz;
            _areasVistas[id] = go;
            Destroy(go, segundos * 2f + 10f);   // (se o aviso do fim não chegar)
        }

        [Rpc(SendTo.ClientsAndHost)]
        void ApagouAreaRpc(int id)
        {
            if (!_areasVistas.TryGetValue(id, out var go)) return;
            _areasVistas.Remove(id);
            if (go == null) return;
            // as chamas param de nascer e as que estão no ar terminam
            foreach (var ps in go.GetComponentsInChildren<ParticleSystem>()) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            var t = go.GetComponent<ChamaTremula>();
            if (t != null) t.apagando = true;
            Destroy(go, 1.2f);
        }

        void EsquecerOFogo()
        {
            foreach (var g in _garrafas) if (g.visual != null) Destroy(g.visual.gameObject);
            _garrafas.Clear();
            _areas.Clear();
            foreach (var go in _areasVistas.Values) if (go != null) Destroy(go);
            _areasVistas.Clear();
        }
    }

    /// <summary>A luz do fogo do molotov tremulando (e se apagando no fim).</summary>
    public class ChamaTremula : MonoBehaviour
    {
        public Light luz;
        public bool apagando;
        float _forca = 1f;

        void Update()
        {
            if (luz == null) return;
            if (apagando) _forca = Mathf.MoveTowards(_forca, 0f, Time.deltaTime / 1f);
            luz.intensity = _forca * (2.8f + 0.8f * Mathf.PerlinNoise(Time.time * 3.4f, transform.position.x) + 0.3f * Mathf.Sin(Time.time * 19f));
        }
    }
}
