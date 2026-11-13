using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using ZombieCityCleaner.Mundo;
using ZombieCityCleaner.Player;

namespace ZombieCityCleaner.World
{
    /// <summary>
    /// O machado nos pinheiros do Lakewood (12/11/2026, o Plano_Lakewood, "O jogo do mundo sorteado no Lakewood", a parte 3):
    /// o mesmo do mundo sorteado (<see cref="MundoDaPartida"/>.Arvores.cs): os 4 lados do tronco, cada um fundo em 2 a 4
    /// golpes; com dois lados opostos fundos ela cai para o lado do primeiro, e ficam as toras no pé dela (de 2 a 5), o XP e
    /// o barulho. A lista das cortadas vai pela rede (e no save do Lakewood); o castor também derruba. Quem desenha o toco, a
    /// queda e os entalhes é o <see cref="LakewoodNaCena"/> (LakewoodNaCena.Arvores.cs).
    /// </summary>
    public partial class MundoDeLakewood
    {
        NetworkList<long> _arvoresCortadas;
        readonly Dictionary<long, byte[]> _ladosCortados = new Dictionary<long, byte[]>();

        partial void IniciarAsListas()
        {
            _arvoresCortadas = new NetworkList<long>();
            _arvoresCortadas.OnListChanged += _ => FlorestaDoMundo.DefinirCortadas(Copia(_arvoresCortadas));
            IniciarAsOutrasListas();
        }

        // as listas das partes depois do corte (os painéis, as marcas)
        partial void IniciarAsOutrasListas();

        static List<long> Copia(NetworkList<long> l)
        {
            var c = new List<long>(l.Count);
            foreach (var k in l) c.Add(k);
            return c;
        }

        // (no spawn: quem entra recebe as cortadas; ao sair, a lista estática do jogo fica vazia)
        void AoNascerAsArvores() => FlorestaDoMundo.DefinirCortadas(Copia(_arvoresCortadas));
        void AoSumirAsArvores() => FlorestaDoMundo.DefinirCortadas(new List<long>());

        /// <summary>As árvores cortadas (o save).</summary>
        public List<long> ArvoresCortadas() => _arvoresCortadas != null ? Copia(_arvoresCortadas) : new List<long>();

        /// <summary>No anfitrião: as cortadas de um save.</summary>
        public void RestaurarArvores(IEnumerable<long> chaves)
        {
            if (!IsServer || _arvoresCortadas == null) return;
            _arvoresCortadas.Clear();
            if (chaves != null) foreach (var k in chaves) _arvoresCortadas.Add(k);
        }

        /// <summary>No dono: um golpe do machado no lado 'lado' (0 a 3, de 90 em 90° a partir do norte) da árvore, a 'altura' m do pé.</summary>
        public void PedirGolpeNaArvore(long chave, byte lado, float altura)
        {
            if (IsSpawned) GolpeNaArvoreRpc(chave, lado, altura);
        }

        [Rpc(SendTo.Server)]
        void GolpeNaArvoreRpc(long chave, byte lado, float altura, RpcParams parametros = default)
        {
            ulong quem = parametros.Receive.SenderClientId;
            if (!ArvoreDoGolpe(chave, quem, out var j, out var planta)) return;
            lado &= 3;
            if (!_ladosCortados.TryGetValue(chave, out var lados))
            {
                if (_ladosCortados.Count > 128) _ladosCortados.Clear();
                lados = new byte[4];
                _ladosCortados[chave] = lados;
            }
            int precisa = MundoDaPartida.GolpesPorLado(planta.Escala);
            bool fundo = lados[lado] >= precisa;
            if (!fundo) lados[lado]++;
            byte oposto = lados[(lado + 2) & 3];
            EntalheRpc(chave, lado, lados[lado], oposto, (byte)precisa, Mathf.Clamp(altura, 0.35f, 1.4f), quem, fundo);
            if (lados[lado] < precisa || oposto < precisa) return;
            _ladosCortados.Remove(chave);
            Vector3 deste = Quaternion.Euler(0f, lado * 90f, 0f) * Vector3.forward;
            Derrubar(chave, planta, j, planta.Posicao + deste * 3f);
        }

        // a árvore em pé desta chave (pela semente do chão) e quem bateu (perto dela)
        bool ArvoreDoGolpe(long chave, ulong quem, out JogadorRede j, out PlantaDaFloresta planta)
        {
            j = null;
            planta = default;
            if (_arvoresCortadas == null || _arvoresCortadas.Contains(chave)) return false;
            if (!NetworkManager.ConnectedClients.TryGetValue(quem, out var cliente) || cliente.PlayerObject == null) return false;
            j = cliente.PlayerObject.GetComponent<JogadorRede>();
            if (j == null || !ArvoresDoMundo.Planta(chave, out planta) || !ArvoresDoMundo.EmPe(planta)) return false;
            var pe = planta.Posicao;
            var de = j.transform.position;
            return new Vector2(de.x - pe.x, de.z - pe.z).magnitude <= 4.5f;
        }

        [Rpc(SendTo.Everyone)]
        void EntalheRpc(long chave, byte lado, byte fundura, byte oposto, byte precisa, float altura, ulong quem, bool jaFundo)
        {
            LakewoodNaCena.Instancia?.MarcarEntalhe(chave, lado, fundura / (float)Mathf.Max(1, (int)precisa), altura);
            if (NetworkManager == null || quem != NetworkManager.LocalClientId) return;
            if (fundura >= precisa && oposto >= precisa) return;
            if (jaFundo) InventarioJogador.AvisoLocal("Esse lado já está cortado: corte do lado oposto");
            else if (fundura >= precisa) InventarioJogador.AvisoLocal("Um lado cortado: agora corte o lado oposto (ela cai para o lado do primeiro corte)");
            else InventarioJogador.AvisoLocal($"Cortando a árvore: este lado {fundura} de {precisa}" + (oposto == 0 ? " (corte dois lados opostos para ela cair)" : ""));
        }

        void Derrubar(long chave, PlantaDaFloresta planta, JogadorRede j, Vector3 caiLongeDe)
        {
            var pe = planta.Posicao;
            var de = j.transform.position;
            _arvoresCortadas.Add(chave);
            Vector3 lado = de - pe;
            lado.y = 0f;
            lado = lado.sqrMagnitude > 0.01f ? lado.normalized : Vector3.forward;
            var onde = pe + lado * 1.2f;
            var chao = LakewoodNaCena.Instancia != null ? LakewoodNaCena.Instancia.Chao : null;
            onde.y = (chao != null ? chao.Altura(onde.x, onde.z) : pe.y) + 0.05f;
            int toras = Mathf.Clamp(Mathf.RoundToInt(1.5f + planta.Escala * 1.5f), 2, 5);
            Loot.ItensNoMundo.Instancia?.Criar(Loot.Item.Toras, onde, Random.Range(0f, 360f), toras);
            j.Inventario?.GanharXp(Habilidades.XpDaArvore, "árvore");
            Zombies.ZombieManager.Barulho(pe + Vector3.up * 2f, j, 30f);
            TombouRpc(chave, caiLongeDe, j.OwnerClientId);
        }

        /// <summary>Só no anfitrião: o castor roeu a árvore até cair (sem as toras e sem o aviso). Falso se ela já caiu.</summary>
        public bool DerrubarPeloCastor(long chave, Vector3 deOnde)
        {
            if (!IsServer || _arvoresCortadas == null || _arvoresCortadas.Contains(chave)) return false;
            _arvoresCortadas.Add(chave);
            _ladosCortados.Remove(chave);
            TombouRpc(chave, deOnde, ulong.MaxValue);
            return true;
        }

        [Rpc(SendTo.Everyone)]
        void TombouRpc(long chave, Vector3 deQuem, ulong quem)
        {
            var cortadas = Copia(_arvoresCortadas);
            if (!cortadas.Contains(chave)) cortadas.Add(chave);
            var cena = LakewoodNaCena.Instancia;
            cena?.TirarEntalhes(chave);
            // (a queda primeiro: a árvore de pé ainda está no bloco, com a cara dela)
            cena?.Tombar(chave, deQuem);
            FlorestaDoMundo.DefinirCortadas(cortadas);
            if (NetworkManager != null && quem == NetworkManager.LocalClientId) InventarioJogador.AvisoLocal("A árvore caiu: as toras ficaram no pé dela");
        }
    }
}
