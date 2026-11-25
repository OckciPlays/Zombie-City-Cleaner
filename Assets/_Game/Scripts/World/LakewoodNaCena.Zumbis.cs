using UnityEngine;
using ZombieCityCleaner.Mundo;

namespace ZombieCityCleaner.World
{
    /// <summary>
    /// O Lakewood como o chão dos zumbis (Plano_Lakewood, Etapa 6, a parte 5): o <see cref="NavMeshDoMundo"/> monta as
    /// áreas em volta dos jogadores quando os blocos do chão (no detalhe máximo, com a colisão) e as obras de perto (as
    /// casas, as lojas, o posto e a loja de armas, com a colisão cozida) estão prontos, e o povoamento põe os zumbis pelas
    /// células, como no mundo sorteado (a cidade é o que fica perto de um lote). No anfitrião, o chão com a colisão vai
    /// também em volta dos outros jogadores (o <see cref="Revisar"/>): os zumbis deles andam no NavMesh dali.
    /// </summary>
    public partial class LakewoodNaCena : IChaoDosZumbis, IMundoDoJogo
    {
        // o mundo do jogo (12/11/2026, a parte 1 do "O jogo do mundo sorteado no Lakewood"): o pronto e a semente vêm do
        // MundoDeLakewood (a rede); a área, a borda e as estradas, do chão
        bool IMundoDoJogo.PartidaPronta => MundoDeLakewood.Instancia != null && MundoDeLakewood.Instancia.Pronto;
        // (22/11/2026, a Woodcreek de antes: o antes do apocalipse é o Dia 0 do Lakewood: os grupos, as emboscadas, o
        // acampamento, as bases e os carros quebrados desligam, e os moradores do bunker esperam)
        bool IMundoDoJogo.NoDiaZero => _antes;
        int IMundoDoJogo.SementeDaPartida => MundoDeLakewood.Instancia != null && MundoDeLakewood.Instancia.Semente != 0
            ? MundoDeLakewood.Instancia.Semente : (mapa != null ? mapa.semente : 1);
        // (16/11/2026, OrigemDeLakewood: as interfaces falam no mundo do jogo; os pontos vão para o mapa aqui)
        Rect IMundoDoJogo.Area => mapa != null ? OrigemDeLakewood.NoMundo(new Rect(0f, 0f, mapa.largura, mapa.comprimento)) : default;

        bool IMundoDoJogo.Dentro(float x, float z, float margem)
        {
            if (mapa == null) return false;
            x += OrigemDeLakewood.X;
            z += OrigemDeLakewood.Z;
            return x > margem && z > margem && x < mapa.largura - margem && z < mapa.comprimento - margem;
        }

        bool IMundoDoJogo.NaEstrada(float x, float z) => Chao != null && ChaoNoMundo.EstradaPerto(x, z, 0f, out _, out _, out _, out _);

        int IChaoDosZumbis.Semente => mapa != null ? mapa.semente : 1;
        bool IChaoDosZumbis.Ligado => Chao != null;
        float IChaoDosZumbis.Altura(float x, float z) => ChaoNoMundo.Altura(x, z);
        bool IChaoDosZumbis.NaCidade(float x, float z) => ChaoNoMundo.LoteEm(x, z, 12f) != null;

        bool IChaoDosZumbis.TemHospital
        {
            get
            {
                if (Chao == null) return false;
                foreach (var l in Chao.Lotes) if (l.Plano != null && l.Plano.Uso == UsoDoLote.Hospital) return true;
                return false;
            }
        }

        bool IChaoDosZumbis.Pronto(Vector3 centro, float raio) => QueFaltaAosZumbis(centro, raio) == null;
        string IChaoDosZumbis.QueFalta(Vector3 centro, float raio) => QueFaltaAosZumbis(centro, raio);

        // a água do lago, dos rios e do canal da usina: em todo o mapa (as estradas e as pontes passam por cima)
        bool IChaoDosZumbis.PodeTerAgua(Bounds caixa) => Chao != null;

        float IChaoDosZumbis.FunduraDaAgua(float x, float z, out float nivel)
        {
            if (!ChaoNoMundo.Agua(x, z, out nivel)) return 0f;
            return Mathf.Max(0f, nivel - ChaoNoMundo.Altura(x, z));
        }

        // o primeiro bloco ou obra que falta no círculo (o centro no mundo), ou null
        string QueFaltaAosZumbis(Vector3 centro, float raio)
        {
            if (Chao == null) return "sem o chão do Lakewood";
            var p = NoMapa2(centro);
            foreach (var chave in BlocosEm(p, raio))
            {
                if (!_blocos.TryGetValue(chave, out var b)) return $"o bloco ({chave.x}, {chave.y}) não está montado";
                if (b.Passo <= 0f || b.Passo > 2.01f) return $"o bloco ({chave.x}, {chave.y}) não está no detalhe máximo";
                if (b.Colisor == null || !b.Colisor.enabled) return $"o bloco ({chave.x}, {chave.y}) está sem a colisão";
                // (18/11/2026: ligado sem a malha, ele não colide; PorAMalha)
                if (!ColideDeVerdade(b)) return $"o colisor do bloco ({chave.x}, {chave.y}) está ligado sem a malha";
                if (!b.ColisaoEmDia) return $"a colisão do bloco ({chave.x}, {chave.y}) está sendo feita";
            }
            float r2 = raio * raio;
            foreach (var o in _obras)
            {
                var meio = o.Lote.Lote.meio;
                if ((meio - p).sqrMagnitude > r2 || o.Falhou) continue;
                if (o.Perto == null) return $"a obra do lote {o.Lote.Indice} ({o.Plano.Nome}) não está montada";
                if (o.Cozinhando.Count > 0) return $"a colisão da obra do lote {o.Lote.Indice} está sendo feita";
            }
            return null;
        }
    }
}
