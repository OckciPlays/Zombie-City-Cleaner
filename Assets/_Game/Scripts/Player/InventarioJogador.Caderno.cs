using Unity.Netcode;
using UnityEngine;
using ZombieCityCleaner.UI;
using ZombieCityCleaner.Zombies;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// O caderno do P (25/11/2026, a etapa 9 do plano da v17, a pedido: "Zumbis: cada tipo, desenhado quando é visto pela
    /// primeira vez; Animais: os caçados, desenhados, com quantos"). O anfitrião anota no <see cref="Progresso"/> (vai no save
    /// do personagem): a cada meio segundo olha os zumbis na frente do jogador, a até 35 m (os chefes, 60 m) e sem parede no
    /// meio, e o primeiro de cada tipo entra no caderno; e cada bicho que ele caça (a FaunaDoMundo.Matar) soma na espécie. Na
    /// primeira vez de cada um, o dono vê "Anotado no caderno: o Corredor", com o som da caneta. As páginas: a aba
    /// ANOTAÇÕES da mochila (CadernoDeMissoes.Fichas.cs).
    /// </summary>
    public partial class InventarioJogador
    {
        const float SegundosEntreAsOlhadasNosZumbis = 0.5f;
        // (o Long fica de 14 a 30 m da gente: com 24 m, ele quase nunca entrava)
        const float DistanciaParaVerUmZumbi = 35f, DistanciaParaVerUmChefe = 60f;
        // o cenário (o chão, as paredes, as árvores): o zumbi atrás dele não conta como visto
        const int MascaraDaVista = 1;

        float _proximaOlhadaNosZumbis;

        // no anfitrião, do Update: os zumbis que ele está vendo agora (só os tipos que ainda não estão no caderno)
        void OlharOsZumbis()
        {
            if (Time.time < _proximaOlhadaNosZumbis) return;
            _proximaOlhadaNosZumbis = Time.time + SegundosEntreAsOlhadasNosZumbis;
            var zm = ZombieManager.Instancia;
            if (zm == null || !zm.IsSpawned || (_rede != null && !_rede.AlvoValido)) return;
            var p = Progresso.Value;
            Vector3 olho = transform.position + Vector3.up * 1.6f;
            Vector3 frente = transform.forward;
            frente.y = 0f;
            if (frente.sqrMagnitude < 0.01f) return;
            frente.Normalize();
            var lista = zm.Visiveis();
            for (int i = 0; i < lista.Count; i++)
            {
                var z = lista[i];
                if (z == null || z.Morto) continue;
                int bit = CadernoDeMissoes.BitDoZumbi(z.tipo, z.qualChefe);
                if (p.ViuOZumbi(bit)) continue;
                bool chefe = z.tipo == TipoDeZumbi.Chefe;
                Vector3 alvo = z.transform.position + Vector3.up * (chefe ? 2.5f : 1.2f);
                Vector3 d = alvo - olho;
                float dist = d.magnitude;
                if (dist < 0.1f || dist > (chefe ? DistanciaParaVerUmChefe : DistanciaParaVerUmZumbi)) continue;
                Vector3 plano = d;
                plano.y = 0f;
                if (plano.sqrMagnitude > 0.25f && Vector3.Dot(frente, plano.normalized) < 0.25f) continue;   // (uns 75° para cada lado)
                if (Physics.Raycast(olho, d / dist, Mathf.Max(0f, dist - (chefe ? 3f : 0.8f)), MascaraDaVista, QueryTriggerInteraction.Ignore)) continue;
                p.VerOZumbi(bit);
                Progresso.Value = p;
                AnotadoNoCadernoRpc($"Anotado no caderno: {CadernoDeMissoes.NomeDoZumbi(bit)}");
                return;   // (um por olhada: dois tipos de uma vez saem um depois do outro)
            }
        }

        /// <summary>No anfitrião (a FaunaDoMundo, quando um bicho morre por ele): mais um dessa espécie no caderno.</summary>
        public void AnotarOBicho(Fauna.Especie especie)
        {
            if (!IsServer) return;
            var p = Progresso.Value;
            bool primeiro = p.Cacados((int)especie) == 0;
            p.Cacar((int)especie);
            Progresso.Value = p;
            if (primeiro) AnotadoNoCadernoRpc($"Anotado no caderno: {Fauna.Especies.De(especie).comArtigo}");
        }

        [Rpc(SendTo.Owner)]
        void AnotadoNoCadernoRpc(string texto)
        {
            AvisoLocal(texto);
            CadernoDeMissoes.TocarAqui(CadernoDeMissoes.SomDeEscrever, 0.45f);
        }

        /// <summary>No anfitrião (o F1): todos os zumbis vistos e de 1 a 4 de cada bicho caçados. Devolve quantas anotações eram novas.</summary>
        public int AnotarTudoNoCaderno()
        {
            if (!IsServer) return 0;
            var p = Progresso.Value;
            int novas = 0;
            foreach (int bit in CadernoDeMissoes.BitsDosZumbis)
            {
                if (p.ViuOZumbi(bit)) continue;
                p.VerOZumbi(bit);
                novas++;
            }
            for (int e = 0; e < Fauna.Especies.Quantas; e++)
            {
                if (p.Cacados(e) > 0) continue;
                for (int n = 1 + (e * 7) % 4; n > 0; n--) p.Cacar(e);
                novas++;
            }
            Progresso.Value = p;
            return novas;
        }

        /// <summary>No anfitrião (o F1): o caderno limpo (nenhum zumbi visto, nenhum bicho caçado).</summary>
        public void LimparOCaderno()
        {
            if (!IsServer) return;
            var p = Progresso.Value;
            p.zumbisVistos = 0u;
            p.cacados0 = p.cacados1 = p.cacados2 = 0UL;
            Progresso.Value = p;
        }
    }
}
