using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using ZombieCityCleaner.Combat;
using ZombieCityCleaner.Loot;
using ZombieCityCleaner.Mundo;
using ZombieCityCleaner.Player;

namespace ZombieCityCleaner.World
{
    /// <summary>
    /// As janelas que abrem (o Plano_Portas.md da pasta de arte, a parte 5), como as portas: começam fechadas (uma em cada
    /// sete aberta), metade das fechadas trancada; o estado vai na mesma lista da rede (pela chave da janela); por dentro, o
    /// trinco destranca; trancada, treme; sabida trancada, a gazua e o pé de cabra arrombam.
    /// </summary>
    public partial class PortasDoMundo
    {
        readonly Dictionary<ulong, (PortasDaConstrucao c, int i)> _registroDasJanelas = new Dictionary<ulong, (PortasDaConstrucao, int)>();
        readonly Dictionary<ulong, JanelaDaCasa> _janelasVivas = new Dictionary<ulong, JanelaDaCasa>();
        readonly Dictionary<(int, int), Mesh> _folhasDeJanela = new Dictionary<(int, int), Mesh>();

        /// <summary>A chave de uma janela na rede: a da casa e o número dela.</summary>
        public static ulong ChaveDaJanela(ulong obra, int i) => Sorteio.Misturar(obra ^ 0x4A414E454C410000UL ^ (ulong)(uint)i);

        public static bool JanelaAbertaNoComeco(int semente, int i) => Sorte(semente, i + 1000, 5) < 0.15f;

        public static bool JanelaTrancadaNoComeco(int semente, int i) => !JanelaAbertaNoComeco(semente, i) && Sorte(semente, i + 1000, 6) < 0.5f;

        bool JanelaAbertaAgora(PortasDaConstrucao c, int i) =>
            _estado.TryGetValue(c.chavesDasJanelas[i], out byte bits) ? (bits & BitAberta) != 0 : JanelaAbertaNoComeco(c.semente, i);

        bool JanelaTrancadaAgora(PortasDaConstrucao c, int i) =>
            JanelaTrancadaNoComeco(c.semente, i) && !(_estado.TryGetValue(c.chavesDasJanelas[i], out byte bits) && (bits & BitDestrancada) != 0);

        public bool Trancada(JanelaDaCasa j) => j.construcao != null && JanelaTrancadaAgora(j.construcao, j.indice);

        internal string DicaDaTrancada(JanelaDaCasa j, ArmaJogador quem) => DicaDaTrancada(FerramentaDe(quem.Inventario));

        internal float TempoDeArrombar(JanelaDaCasa j, ArmaJogador quem)
        {
            if (j.Aberta || !Trancada(j) || !SabidaTrancada(j.chave) || j.DoLadoDeDentro(quem.transform.position)) return 0f;
            if (Barricada(j.chave)) return 0f;   // (a barricada de tábuas, 11/11/2026)
            return TempoDa(FerramentaDe(quem.Inventario), quem.Inventario);
        }

        internal string TextoDoArrombar(JanelaDaCasa j, ArmaJogador quem) => TextoDoArrombar(FerramentaDe(quem.Inventario));

        // ------------------------------------------------------------ o E

        /// <summary>O E numa janela: pelo anfitrião (na rede) ou direto.</summary>
        public void PedirJanela(JanelaDaCasa j)
        {
            if (j == null) return;
            if (!IsSpawned)
            {
                DecidirJanela(j.construcao, j.indice, JogadorRede.Local != null ? JogadorRede.Local.transform.position : j.Meio, false);
                return;
            }
            UsarJanelaRpc(j.chave);
        }

        [Rpc(SendTo.Server)]
        void UsarJanelaRpc(ulong chave, RpcParams parametros = default)
        {
            if (!NetworkManager.ConnectedClients.TryGetValue(parametros.Receive.SenderClientId, out var cliente) || cliente.PlayerObject == null) return;
            if (!_registroDasJanelas.TryGetValue(chave, out var r) || r.c == null) return;
            var pes = cliente.PlayerObject.transform.position;
            if ((MeioDaJanela(r.c, r.i) - (pes + Vector3.up)).sqrMagnitude > distanciaMaxima * distanciaMaxima) return;
            DecidirJanela(r.c, r.i, pes, true);
        }

        void DecidirJanela(PortasDaConstrucao c, int i, Vector3 quem, bool naRede)
        {
            if (c == null) return;
            ulong chave = c.chavesDasJanelas[i];
            // a barricada de tábuas (11/11/2026, a etapa 5b): não abre, dos dois lados (as folhas tremem)
            if (Barricada(chave))
            {
                if (naRede) TremerRpc(chave);
                else Tremeu(chave);
                return;
            }
            if (JanelaAbertaAgora(c, i)) EscreverChave(chave, false, false);
            else if (!JanelaTrancadaAgora(c, i)) EscreverChave(chave, true, false);
            else if (DoLadoDeDentroDaJanela(c, i, quem)) EscreverChave(chave, true, true);
            else if (naRede) TremerRpc(chave);
            else Tremeu(chave);
        }

        static Vector3 MeioDaJanela(PortasDaConstrucao c, int i)
        {
            var j = c.janelas[i];
            return c.transform.TransformPoint(j.peitoril + Vector3.up * (j.altura / 2));
        }

        static bool DoLadoDeDentroDaJanela(PortasDaConstrucao c, int i, Vector3 ponto)
        {
            var j = c.janelas[i];
            var q = c.transform.InverseTransformPoint(ponto);
            return (q.x - j.peitoril.x) * j.fora.x + (q.z - j.peitoril.z) * j.fora.y < 0f;
        }

        void AplicarJanela(JanelaDaCasa j, bool ja)
        {
            if (j == null) return;
            bool antes = j.Aberta;
            bool aberta = _estado.TryGetValue(j.chave, out byte bits) ? (bits & BitAberta) != 0 : JanelaAbertaNoComeco(j.construcao.semente, j.indice);
            if (aberta && Barricada(j.chave)) aberta = false;   // (a barricada de tábuas, 11/11/2026)
            j.Definir(aberta, ja);
            if (!ja && j.Aberta != antes) Efeitos.Som(j.Aberta ? SomDasPortas.Abrir : SomDasPortas.Fechar, j.Meio, 0.55f, true, 20f);
        }

        internal void Esquecer(JanelaDaCasa j)
        {
            if (_janelasVivas.TryGetValue(j.chave, out var v) && v == j) _janelasVivas.Remove(j.chave);
        }

        void CriarJanela(PortasDaConstrucao c, int i)
        {
            var d = c.janelas[i];
            var go = new GameObject($"Janela {i}") { layer = Camada };
            go.transform.SetParent(c.transform, false);
            go.transform.localPosition = d.peitoril;
            var j = go.AddComponent<JanelaDaCasa>();
            j.construcao = c;
            j.indice = i;
            j.chave = c.chavesDasJanelas[i];
            j.dados = d;
            bool aberta = _estado.TryGetValue(j.chave, out byte bits) ? (bits & BitAberta) != 0 : JanelaAbertaNoComeco(c.semente, i);
            if (Barricada(j.chave)) aberta = false;   // (a barricada de tábuas, 11/11/2026)
            j.Preparar(FolhaDeJanela(d.largura / 2 - 0.02f, d.altura - 0.02f), Material, aberta);
            _janelasVivas[j.chave] = j;
        }

        // uma folha de janela (a origem na dobradiça, ao longo do +X): o caixilho de madeira com a travessa do meio, o vidro
        // sujo e o puxador por dentro
        Mesh FolhaDeJanela(float largura, float altura)
        {
            var k = (Mathf.RoundToInt(largura * 1000f), Mathf.RoundToInt(altura * 1000f));
            if (_folhasDeJanela.TryGetValue(k, out var m) && m != null) return m;
            var o = MalhaDeObra.Emprestar();
            const float b = 0.05f, e = 0.04f;
            o.Caixa(Matrix4x4.Translate(new Vector3(largura / 2, b / 2, 0f)), new Vector3(largura, b, e), "Batente");
            o.Caixa(Matrix4x4.Translate(new Vector3(largura / 2, altura - b / 2, 0f)), new Vector3(largura, b, e), "Batente");
            o.Caixa(Matrix4x4.Translate(new Vector3(b / 2, altura / 2, 0f)), new Vector3(b, altura, e), "Batente");
            o.Caixa(Matrix4x4.Translate(new Vector3(largura - b / 2, altura / 2, 0f)), new Vector3(b, altura, e), "Batente");
            o.Caixa(Matrix4x4.Translate(new Vector3(largura / 2, altura / 2, 0f)), new Vector3(largura - 2 * b, 0.035f, e * 0.8f), "Batente");
            o.Caixa(Matrix4x4.Translate(new Vector3(largura / 2, altura / 2, 0f)), new Vector3(largura - 2 * b + 0.01f, altura - 2 * b + 0.01f, 0.008f), "VidroSujo");
            o.Caixa(Matrix4x4.Translate(new Vector3(largura - 0.07f, altura / 2, -0.035f)), new Vector3(0.025f, 0.08f, 0.025f), "Cromo");
            m = o.ParaMesh($"Folha de janela {largura:0.00} × {altura:0.00}");
            MalhaDeObra.Devolver(o);
            _folhasDeJanela[k] = m;
            return m;
        }
    }
}
