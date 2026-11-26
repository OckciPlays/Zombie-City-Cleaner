using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using ZombieCityCleaner.DiaZero;
using ZombieCityCleaner.Loot;
using ZombieCityCleaner.Mundo;
using ZombieCityCleaner.Player;

namespace ZombieCityCleaner.World
{
    /// <summary>
    /// O armazém da casa da família (25/11/2026, a pedido do Pedro: "começar a aparecer as missões: D0 - comprar recursos...
    /// Voltar para casa... Guardar itens em armazém. Depois começa o D30 com o caos que ja é, seria interessante a gente sair
    /// de casa apenas com as roupas"):
    ///  - <b>as estantes</b>: duas estantes de aço no fundo da garagem da casa da família (o lote 104, na Spruce Lane), lado a
    ///    lado, de frente para o portão (as <see cref="EstantesDoBunker.OArmazem"/>, de 8 × 10 cada). Em cada máquina, uma vez,
    ///    no lugar da obra (a mesma conta da âncora da casa no Dia 0); ficam nas duas épocas, e o que está nelas vai no save
    ///    (a lista das estantes). O portão da garagem dessa casa fica aberto (o carro chega na boca dela);
    ///  - <b>descarregar</b>: na tela da caçamba, com a caminhonete na entrada da garagem, o botão "Descarregar no armazém"
    ///    passa tudo o que couber (<see cref="EstantesDoBunker.Passar"/>);
    ///  - <b>a fuga</b>: nos 30 dias depois, cada um sai de casa só com as roupas, a lanterna e o dinheiro; o resto (a mochila
    ///    com o que tem dentro, o pneu) fica no armazém (<see cref="GuardarTudoNoArmazem"/>);
    ///  - <b>os 5 anos</b>: o que ficou na caçamba vai para o armazém (a caminhonete quebrada começa vazia), e a comida que
    ///    não aguenta 5 anos estraga (<see cref="CincoAnosNoArmazem"/>).
    /// </summary>
    public partial class LakewoodNaCena
    {
        bool _garagemAberta, _armazemPosto;
        float _proximoArmazem;

        /// <summary>O lote da casa da família no chão do mapa (o de casa mais perto do ponto escolhido), ou null.</summary>
        ChaoDeLakewood.LoteNoChao LoteDaFamilia()
        {
            if (ChaoNoMapa == null) return null;
            ChaoDeLakewood.LoteNoChao lote = null;
            float melhor = float.MaxValue;
            foreach (var l in ChaoNoMapa.Lotes)
            {
                if (l.Plano == null || l.Plano.Uso != UsoDoLote.Casa || l.Plano.Casa == null) continue;
                float d = (l.Lote.meio - WoodcreekDoDiaZero.CasaDaFamilia).sqrMagnitude;
                if (d < melhor)
                {
                    melhor = d;
                    lote = l;
                }
            }
            return lote;
        }

        /// <summary>Em cada máquina (do AcompanharODiaZero): o portão da garagem aberto e as estantes do armazém postas.</summary>
        void PorOArmazem()
        {
            if (_armazemPosto || ChaoNoMapa == null) return;
            var lote = !_garagemAberta || Time.time >= _proximoArmazem ? LoteDaFamilia() : null;
            if (!_garagemAberta)
            {
                // antes de a obra da casa ser montada (ela entra quando alguém chega perto): o portão recolhido no teto
                _garagemAberta = true;
                if (lote != null && lote.Plano.Casa.garagem != null) lote.Plano.Casa.garagem.fechada = false;
            }
            if (Time.time < _proximoArmazem) return;
            _proximoArmazem = Time.time + 1f;
            var mobilia = MobiliaNovaDoMundo.Instancia;
            if (lote == null || mobilia == null) return;
            var p = lote.Plano.Casa;
            var g = p.garagem;
            _armazemPosto = true;
            if (g == null)
            {
                Debug.LogWarning("[Armazém] A casa da família não tem garagem: sem o armazém.");
                return;
            }
            var M = lote.Plano.Medidas ?? Medidas.DoJogo;
            var forma = EstantesDoBunker.FormaDe("EstanteAco120");
            // a garagem (nas coordenadas da planta, sem espelhar): da parede da casa (xi) para fora (xo), de z0 (o portão) a z1
            float xi = g.lado > 0 ? p.X1 : p.X0, xo = xi + g.lado * g.largura, xm = (xi + xo) * 0.5f;
            // as duas, lado a lado, no fundo; numa garagem estreita, um pouco menores
            const float LarguraDoModelo = 1.2f, FundoDoModelo = 0.42f;
            float s = Mathf.Min(MedidasDaMobilia.Escala, (Mathf.Abs(xo - xi) - M.esp - 0.3f) / (2f * LarguraDoModelo + 0.06f));
            s = Mathf.Max(0.8f, s);
            float z = g.z1 - M.esp * 0.5f - FundoDoModelo * s * 0.5f - 0.05f;
            var pose = PoseDaObra(lote);
            var raiz = new GameObject("O armazém da casa da família").transform;
            raiz.SetParent(transform, true);
            raiz.SetPositionAndRotation(pose.position, pose.rotation);
            for (int i = 0; i < EstantesDoBunker.EstantesDoArmazem; i++)
            {
                float x = xm + (i == 0 ? -1f : 1f) * (LarguraDoModelo * s * 0.5f + 0.03f);
                if (p.espelhar) x = -x;
                var go = mobilia.Novo("EstanteAco120", 0, raiz);
                if (go == null) continue;
                go.name = $"Armazém {i + 1}";
                go.transform.SetLocalPositionAndRotation(new Vector3(x, 0.05f, z), Quaternion.Euler(0f, 180f, 0f));
                go.transform.localScale = Vector3.one * s;
                int estante = EstantesDoBunker.OArmazem[i];
                EstantesDoBunker.Ligar(go, estante, "EstanteAco120");
                EstantesDoBunker.LugarDoArmazem[estante] = go.transform;
            }
            Debug.Log($"[Armazém] As estantes no fundo da garagem da casa da família (o lote {lote.Indice}, {SetorDe(pose.position)}), na escala {s:F2}; " +
                      $"{forma?.colunas ?? 0} × {(forma != null ? forma.Grade.y : 0)} cada.");
        }

        /// <summary>
        /// Só no anfitrião (25/11/2026, a revisão: antes, o que não cabia no armazém nem na caçamba sumia, só com um Debug.Log):
        /// o que ficou de fora no começo da fuga (o item e o que vai com ele). Vai para o chão da garagem nos 5 anos
        /// (<see cref="DeixarNaGaragemOQueNaoCoube"/>), e não já: na fuga, a garagem fica para trás, e o chão dela estaria ao
        /// alcance de todos. (25/11/2026, o save do Dia 0) A fuga agora grava: a lista vai no save
        /// (<c>MundoSalvo.foraDoArmazem</c>, o <see cref="ParaSalvarODiaZero"/>) e volta com a fuga do save.
        /// </summary>
        readonly List<(Item item, int qtd, int desgaste, ulong cores)> _foraDoArmazem = new List<(Item, int, int, ulong)>();

        // (25/11/2026, o save do Dia 0) no anfitrião: o que não coube no armazém, para o save
        void ForaDoArmazemParaSalvar(List<Salvar.ItemForaDoArmazem> lista)
        {
            lista.Clear();
            foreach (var c in _foraDoArmazem)
                lista.Add(new Salvar.ItemForaDoArmazem { item = (int)c.item, qtd = c.qtd, desgaste = c.desgaste, cores = unchecked((long)c.cores) });
        }

        // (25/11/2026, o save do Dia 0) no anfitrião, na fuga que voltou do save: o que não coube no armazém, de volta à espera
        // dos 5 anos. Devolve quantas coisas
        int RestaurarOForaDoArmazem(List<Salvar.ItemForaDoArmazem> lista)
        {
            if (lista == null) return 0;
            int n = 0;
            foreach (var c in lista)
            {
                var item = (Item)(byte)Mathf.Clamp(c.item, 0, 255);
                if (item == Item.Nada || !System.Enum.IsDefined(typeof(Item), item)) continue;
                _foraDoArmazem.Add((item, c.qtd, c.desgaste, unchecked((ulong)c.cores)));
                n++;
            }
            return n;
        }

        /// <summary>
        /// No anfitrião (o começo da fuga, 25/11/2026: "seria interessante a gente sair de casa apenas com as roupas"): o que
        /// cada um leva (a mochila com o que tem dentro, o pneu) fica no armazém da casa; o que não couber, na caçamba. As
        /// roupas vestidas e o dinheiro ficam com ele. (25/11/2026, a revisão) O que não couber nem na caçamba não some: fica
        /// para o chão da garagem (<see cref="_foraDoArmazem"/>), e o dono é avisado.
        /// </summary>
        public void GuardarTudoNoArmazem()
        {
            var estantes = EstantesDoBunker.Instancia;
            if (estantes == null || !estantes.IsSpawned) return;
            var lugares = new List<int>(EstantesDoBunker.OArmazem) { CacambaDaFamilia };
            int guardados = 0, deFora = 0;
            var coisas = new List<(Item item, int qtd, int desgaste, ulong cores)>();
            foreach (var j in JogadorRede.Todos)
            {
                if (j == null || j.Inventario == null) continue;
                var tudo = j.Inventario.TirarTudo(out var mochila, out int pneu);
                coisas.Clear();
                foreach (var it in tudo) if (Itens.NoJogo(it.item)) coisas.Add((it.item, it.qtd, it.desgaste, it.cores));   // (o que saiu do jogo, não)
                if (mochila != Item.Nada) coisas.Add((mochila, 0, 0, 0));
                if (pneu != InventarioJogador.SemPneu) coisas.Add((Item.Pneu, pneu, 0, 0));
                int dele = 0;
                foreach (var c in coisas)
                {
                    // (a pilha pode entrar só em parte: o resto é o que fica de fora)
                    if (estantes.GuardarNasEstantes(lugares, c.item, c.qtd, c.desgaste, c.cores, out int sobrou)) guardados++;
                    else
                    {
                        _foraDoArmazem.Add((c.item, sobrou, c.desgaste, c.cores));
                        dele++;
                    }
                }
                if (dele > 0)
                    j.Inventario.AvisarODono(dele == 1 ? "1 coisa sua não coube no armazém nem na caçamba: ela fica no chão da garagem."
                        : $"{dele} coisas suas não couberam no armazém nem na caçamba: elas ficam no chão da garagem.");
                deFora += dele;
            }
            Debug.Log($"[Armazém] A fuga: cada um sai só com as roupas; {guardados} coisas ficaram no armazém da garagem" +
                      (deFora > 0 ? $" ({deFora} não couberam: ficam para o chão da garagem, nos 5 anos)." : "."));
        }

        /// <summary>
        /// No anfitrião (25/11/2026, a revisão), nos 5 anos (o <see cref="CincoAnosNoArmazem"/>, também com o "Pular a fuga" do
        /// F1) ou no fim de uma fuga sem a chegada (o LimparAFuga): o que não coube no começo da fuga sai da espera. A comida que não aguenta 5 anos estragou
        /// (<paramref name="estragar"/>); o resto entra no armazém, se agora couber (a comida estragada abre lugar), e senão
        /// fica no chão da garagem, em fileiras na frente das estantes. O chão do mundo vai no save.
        /// </summary>
        void DeixarNaGaragemOQueNaoCoube(bool estragar)
        {
            if (_foraDoArmazem.Count == 0) return;
            var estantes = EstantesDoBunker.Instancia;
            var chao = ItensNoMundo.Instancia;
            int noArmazem = 0, noChao = 0, estragou = 0, perdidos = 0;
            foreach (var c in _foraDoArmazem)
            {
                if (estragar && EstragaEmCincoAnos(c.item))
                {
                    estragou++;
                    continue;
                }
                int resto = c.qtd;
                if (estantes != null && estantes.IsSpawned && estantes.GuardarNasEstantes(EstantesDoBunker.OArmazem, c.item, c.qtd, c.desgaste, c.cores, out resto))
                {
                    noArmazem++;
                    continue;
                }
                if (chao == null || !chao.IsSpawned)
                {
                    perdidos++;
                    continue;
                }
                var ponto = NoChaoDaGaragem(noChao, out float giro);
                chao.Criar(c.item, ponto, giro, resto, c.desgaste, c.cores);
                noChao++;
            }
            _foraDoArmazem.Clear();
            Debug.Log($"[Armazém] O que não coube na fuga: {noArmazem} no armazém, {noChao} no chão da garagem" +
                      (estragou > 0 ? $", {estragou} comidas estragaram" : "") + (perdidos > 0 ? $", {perdidos} sem o chão do mundo (perdidos)" : "") + ".");
        }

        /// <summary>
        /// Um ponto no chão da garagem da casa da família, na frente das estantes do armazém (a coisa número
        /// <paramref name="k"/>: fileiras de 6, de 40 cm, a primeira a 75 cm do pé delas, e as outras para o portão), e um giro
        /// para ela. Sem as estantes (a casa sem garagem), na frente da casa.
        /// </summary>
        Vector3 NoChaoDaGaragem(int k, out float giro)
        {
            Vector3 meio = default, frente = Vector3.forward;
            int n = 0;
            foreach (int e in EstantesDoBunker.OArmazem)
            {
                if (!EstantesDoBunker.LugarDoArmazem.TryGetValue(e, out var t) || t == null) continue;
                meio += t.position;
                frente = t.forward;
                n++;
            }
            if (n == 0) meio = CasaDaFamiliaNoMundo();
            else meio /= n;
            frente.y = 0f;
            frente = frente.sqrMagnitude > 0.001f ? frente.normalized : Vector3.forward;
            var lado = Vector3.Cross(Vector3.up, frente);
            const int PorFileira = 6;
            int fileira = k / PorFileira, coluna = k % PorFileira;
            // (o pé das estantes está no chão da garagem: a laje dela, 5 cm acima da obra)
            var ponto = meio + frente * (0.75f + fileira * 0.45f) + lado * ((coluna - (PorFileira - 1) * 0.5f) * 0.4f);
            ponto.y = meio.y;
            giro = Mathf.Atan2(frente.x, frente.z) * Mathf.Rad2Deg + k * 47f;
            return ponto;
        }

        /// <summary>A comida que não aguenta 5 anos guardada (o resto, as latas, a água e os remédios, aguenta).</summary>
        public static bool EstragaEmCincoAnos(Item i) =>
            i == Item.Leite || i == Item.Suco || i == Item.Chocolate || i == Item.Salgadinho || Itens.EhPeixeCru(i);

        /// <summary>
        /// No anfitrião, nos 5 anos (o DeixarACaminhoneteNaCasa): o que ficou na caçamba vai para o armazém (a caminhonete
        /// quebrada começa vazia; o que não couber, fica nela), e a comida que não aguenta 5 anos estraga nos dois.
        /// </summary>
        void CincoAnosNoArmazem()
        {
            var estantes = EstantesDoBunker.Instancia;
            if (estantes == null || !estantes.IsSpawned)
            {
                // (25/11/2026, a revisão) sem as estantes, o que não coube na fuga vai assim mesmo para o chão da garagem
                DeixarNaGaragemOQueNaoCoube(true);
                return;
            }
            estantes.Passar(CacambaDaFamilia, EstantesDoBunker.OArmazem, out int foram, out int ficaram);
            int estragou = 0;
            foreach (var it in estantes.Copia())
            {
                if (!EstragaEmCincoAnos(it.item) || !(EstantesDoBunker.EhDoArmazem(it.estante) || it.estante == CacambaDaFamilia)) continue;
                if (estantes.TirarOItem(it.estante, it.id, out _, out _)) estragou++;
            }
            Debug.Log($"[Armazém] 5 anos depois: da caçamba, {foram} para o armazém e {ficaram} nela; {(estragou == 1 ? "1 comida estragou" : $"{estragou} comidas estragaram")}.");
            // (25/11/2026, a revisão) e o que não coube no começo da fuga: no armazém, se agora couber, ou no chão da garagem
            DeixarNaGaragemOQueNaoCoube(true);
        }

        /// <summary>Alguém está perto de uma estante do armazém (a até <paramref name="raio"/> m da frente dela).</summary>
        bool AlguemNoArmazem(float raio)
        {
            foreach (var kv in EstantesDoBunker.LugarDoArmazem)
            {
                if (kv.Value == null) continue;
                var frente = kv.Value.position + kv.Value.forward * 0.6f;
                if (AlguemPerto(frente, raio)) return true;
            }
            return false;
        }
    }
}
