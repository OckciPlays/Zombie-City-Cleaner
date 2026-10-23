using System.Collections.Generic;
using UnityEngine;

namespace ZombieCityCleaner.Zombies
{
    /// <summary>
    /// O que o zumbi já perdeu. Perder o braço inteiro (ou a perna inteira) já inclui o antebraço (ou a canela).
    /// Vai pela rede em cada zumbi (16 bits), então quem chega depois já vê o zumbi como ele está.
    /// </summary>
    [System.Flags]
    public enum Membros : ushort
    {
        Nenhum = 0,
        Cabeca = 1 << 0,
        BracoE = 1 << 1,
        AntebracoE = 1 << 2,
        BracoD = 1 << 3,
        AntebracoD = 1 << 4,
        CoxaE = 1 << 5,
        CanelaE = 1 << 6,
        CoxaD = 1 << 7,
        CanelaD = 1 << 8,
        Pernas = CoxaE | CanelaE | CoxaD | CanelaD,
    }

    /// <summary>As regiões da malha (submeshes com a textura do zumbi). Cada uma some quando o membro dela cai.</summary>
    public enum RegiaoDaMalha { Corpo, Cabeca, BracoE, AntebracoE, BracoD, AntebracoD, CoxaE, CanelaE, CoxaD, CanelaD }

    /// <summary>As tampas de carne (submeshes escondidos dentro do corpo), uma em cada corte.</summary>
    public enum Tampa { Pescoco, OmbroE, CotoveloE, OmbroD, CotoveloD, QuadrilE, JoelhoE, QuadrilD, JoelhoD }

    /// <summary>Qual submesh da malha completa é cada região e cada tampa (a montagem dos zumbis mede e preenche).</summary>
    [System.Serializable]
    public class MapaDaMalha
    {
        public int[] regioes = new int[10];
        public int[] tampas = new int[9];
    }

    public static class MembrosDoZumbi
    {
        /// <summary>O membro que uma hitbox derruba (Nenhum para o tronco e a cápsula).</summary>
        public static Membros DaRegiao(RegiaoDoCorpo r)
        {
            switch (r)
            {
                case RegiaoDoCorpo.Cabeca: return Membros.Cabeca;
                case RegiaoDoCorpo.BracoE: return Membros.BracoE;
                case RegiaoDoCorpo.AntebracoE: return Membros.AntebracoE;
                case RegiaoDoCorpo.BracoD: return Membros.BracoD;
                case RegiaoDoCorpo.AntebracoD: return Membros.AntebracoD;
                case RegiaoDoCorpo.CoxaE: return Membros.CoxaE;
                case RegiaoDoCorpo.CanelaE: return Membros.CanelaE;
                case RegiaoDoCorpo.CoxaD: return Membros.CoxaD;
                case RegiaoDoCorpo.CanelaD: return Membros.CanelaD;
                default: return Membros.Nenhum;
            }
        }

        /// <summary>O que cai junto: o braço leva o antebraço, a coxa leva a canela.</summary>
        public static Membros ComOQueVemJunto(Membros m)
        {
            if ((m & Membros.BracoE) != 0) m |= Membros.AntebracoE;
            if ((m & Membros.BracoD) != 0) m |= Membros.AntebracoD;
            if ((m & Membros.CoxaE) != 0) m |= Membros.CanelaE;
            if ((m & Membros.CoxaD) != 0) m |= Membros.CanelaD;
            return m;
        }

        public static bool Perdeu(Membros todos, Membros m) => (todos & m) != 0;

        static bool RegiaoVisivel(RegiaoDaMalha r, Membros m)
        {
            switch (r)
            {
                case RegiaoDaMalha.Cabeca: return !Perdeu(m, Membros.Cabeca);
                case RegiaoDaMalha.BracoE: return !Perdeu(m, Membros.BracoE);
                case RegiaoDaMalha.AntebracoE: return !Perdeu(m, Membros.AntebracoE);
                case RegiaoDaMalha.BracoD: return !Perdeu(m, Membros.BracoD);
                case RegiaoDaMalha.AntebracoD: return !Perdeu(m, Membros.AntebracoD);
                case RegiaoDaMalha.CoxaE: return !Perdeu(m, Membros.CoxaE);
                case RegiaoDaMalha.CanelaE: return !Perdeu(m, Membros.CanelaE);
                case RegiaoDaMalha.CoxaD: return !Perdeu(m, Membros.CoxaD);
                case RegiaoDaMalha.CanelaD: return !Perdeu(m, Membros.CanelaD);
                default: return true;
            }
        }

        // a tampa aparece no último corte de cada lado: sem o braço inteiro, a do ombro (e não a do cotovelo)
        static bool TampaVisivel(Tampa t, Membros m)
        {
            switch (t)
            {
                case Tampa.Pescoco: return Perdeu(m, Membros.Cabeca);
                case Tampa.OmbroE: return Perdeu(m, Membros.BracoE);
                case Tampa.CotoveloE: return Perdeu(m, Membros.AntebracoE) && !Perdeu(m, Membros.BracoE);
                case Tampa.OmbroD: return Perdeu(m, Membros.BracoD);
                case Tampa.CotoveloD: return Perdeu(m, Membros.AntebracoD) && !Perdeu(m, Membros.BracoD);
                case Tampa.QuadrilE: return Perdeu(m, Membros.CoxaE);
                case Tampa.JoelhoE: return Perdeu(m, Membros.CanelaE) && !Perdeu(m, Membros.CoxaE);
                case Tampa.QuadrilD: return Perdeu(m, Membros.CoxaD);
                case Tampa.JoelhoD: return Perdeu(m, Membros.CanelaD) && !Perdeu(m, Membros.CoxaD);
                default: return false;
            }
        }

        static readonly Dictionary<ushort, Mesh> _variantes = new Dictionary<ushort, Mesh>();
        static Mesh _origem;

        /// <summary>
        /// A malha do zumbi sem os membros perdidos e com as tampas dos cortes: submesh 0 com a textura, 1 com a carne.
        /// Cada combinação é montada uma vez (na primeira vez que acontece) e reaproveitada por todos os zumbis.
        /// </summary>
        public static Mesh Variante(Mesh completa, MapaDaMalha mapa, Membros m)
        {
            if (_origem != completa)
            {
                _variantes.Clear();
                _origem = completa;
            }
            if (_variantes.TryGetValue((ushort)m, out var pronta) && pronta != null) return pronta;
            var corpo = new List<int>();
            var carne = new List<int>();
            for (int r = 0; r < mapa.regioes.Length; r++)
                if (RegiaoVisivel((RegiaoDaMalha)r, m)) corpo.AddRange(completa.GetTriangles(mapa.regioes[r]));
            for (int t = 0; t < mapa.tampas.Length; t++)
                if (TampaVisivel((Tampa)t, m)) carne.AddRange(completa.GetTriangles(mapa.tampas[t]));
            var malha = Object.Instantiate(completa);
            malha.name = $"Zumbi_{(ushort)m:X3}";
            malha.subMeshCount = 2;
            malha.SetTriangles(corpo, 0, false);
            malha.SetTriangles(carne, 1, false);
            malha.UploadMeshData(true);
            _variantes[(ushort)m] = malha;
            return malha;
        }
    }
}
