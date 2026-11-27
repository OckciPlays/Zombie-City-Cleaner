using UnityEngine;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// A ponta da vara que entorta (27/11/2026, a pesca nova, a pedido: "quando o peixe realmente fisgou, ele afunda a boia de
    /// uma vez, puxando ela pra baixo, a ponta da vara de pesca ela entorta também"). As duas varas (a do corpo,
    /// <c>Resources/VaraDoCorpo</c>, e a dos braços de primeira pessoa, <c>Resources/BracosVara</c>) ganharam do Editor
    /// (<c>MontagemDaVaraQueEntorta</c>) uma corrente de 4 ossos dos 35% do comprimento até a ponta ("Dobra1" a "Dobra4"), com
    /// os pesos da malha ao longo dela, e a <see cref="NomeDaPonta"/> no último (de onde a linha sai). Aqui os ossos dobram
    /// juntos para o lado da linha, mais na ponta, pela força (de 0, solta, a 1, o peixe puxando forte), numa mola: sobe rápido
    /// e balança um pouco antes de assentar, como uma vara de verdade.
    /// </summary>
    public sealed class VaraQueEntorta
    {
        /// <summary>O começo do nome dos ossos (Dobra1 a Dobra4, da base para a ponta).</summary>
        public const string Prefixo = "Dobra";
        public const int Ossos = 4;
        /// <summary>A ponta no último osso, de onde a linha sai com a vara entortada.</summary>
        public const string NomeDaPonta = "Ponta que entorta";
        /// <summary>Quanto a vara dobra com a força 1 (graus, somando os ossos).</summary>
        const float DobraMaxima = 75f;
        /// <summary>A parte da dobra de cada osso, da base para a ponta (a ponta dobra mais).</summary>
        static readonly float[] Parte = { 0.12f, 0.2f, 0.29f, 0.39f };
        /// <summary>A mola: a dureza e o amortecimento (meio amortecida: passa um pouco e volta).</summary>
        const float Dureza = 220f, Amortecimento = 17f;

        readonly Transform[] _ossos = new Transform[Ossos];
        readonly Quaternion[] _repouso = new Quaternion[Ossos];
        readonly Vector3[] _eixo = new Vector3[Ossos];
        float _forca, _velocidade;

        /// <summary>A força de agora, na mola (pode passar um pouco de 1 e ficar um pouco abaixo de 0, balançando).</summary>
        public float Forca => _forca;

        /// <summary>Os ossos da vara que entorta embaixo de 'raiz', ou null (a vara de antes, dura).</summary>
        public static VaraQueEntorta De(Transform raiz)
        {
            if (raiz == null) return null;
            var v = new VaraQueEntorta();
            foreach (var t in raiz.GetComponentsInChildren<Transform>(true))
                if (t.name.StartsWith(Prefixo) && t.name.Length == Prefixo.Length + 1)
                {
                    int i = t.name[Prefixo.Length] - '1';
                    if (i >= 0 && i < Ossos) v._ossos[i] = t;
                }
            for (int i = 0; i < Ossos; i++)
            {
                if (v._ossos[i] == null) return null;
                v._repouso[i] = v._ossos[i].localRotation;
                // a vara no espaço do osso: para o próximo (no último, para a ponta)
                var seguinte = i + 1 < Ossos ? v._ossos[i + 1] : Filho(v._ossos[i], NomeDaPonta);
                v._eixo[i] = seguinte != null && seguinte.localPosition.sqrMagnitude > 1e-8f ? seguinte.localPosition.normalized : Vector3.up;
            }
            return v;
        }

        static Transform Filho(Transform pai, string nome)
        {
            for (int i = 0; i < pai.childCount; i++)
                if (pai.GetChild(i).name == nome) return pai.GetChild(i);
            return null;
        }

        /// <summary>
        /// A cada quadro, depois da animação: a força que a linha faz (0 a 1) e para onde ela puxa (no mesmo espaço dos ossos:
        /// no mundo, para a vara do corpo; no dos braços, para a de primeira pessoa).
        /// </summary>
        public void Aplicar(float alvo, Vector3 rumo, float dt)
        {
            dt = Mathf.Min(dt, 0.05f);
            _velocidade += (Dureza * (alvo - _forca) - Amortecimento * _velocidade) * dt;
            _forca = Mathf.Clamp(_forca + _velocidade * dt, -0.3f, 1.3f);
            for (int i = 0; i < Ossos; i++) _ossos[i].localRotation = _repouso[i];
            if (rumo.sqrMagnitude < 1e-8f || Mathf.Abs(_forca) < 1e-4f) return;
            rumo.Normalize();
            for (int i = 0; i < Ossos; i++)
            {
                var osso = _ossos[i];
                Vector3 eixo = osso.TransformDirection(_eixo[i]);
                Vector3 giro = Vector3.Cross(eixo, rumo);
                if (giro.sqrMagnitude < 1e-8f) continue;
                // (a ponta não passa da linha: dobra no máximo até quase ela)
                float falta = Vector3.Angle(eixo, rumo);
                float angulo = Mathf.Clamp(DobraMaxima * Parte[i] * _forca, -falta, falta * 0.9f);
                osso.rotation = Quaternion.AngleAxis(angulo, giro.normalized) * osso.rotation;
            }
        }

        /// <summary>A vara reta e parada (sem a linha).</summary>
        public void Soltar()
        {
            _forca = _velocidade = 0f;
            for (int i = 0; i < Ossos; i++)
                if (_ossos[i] != null) _ossos[i].localRotation = _repouso[i];
        }

        /// <summary>
        /// A força na vara de quem é visto pela rede (não é o dono), pela fase da boia e pelo que vai junto: na puxada, a força
        /// mandada pelo dono (a fração do <c>w</c> da <see cref="InventarioJogador.BoiaNaRede"/>); beliscando, uma beliscada média
        /// (como a boia do <see cref="PescaVista"/>), desde 'desde'.
        /// </summary>
        public static float ForcaPelaRede(PescaJogador.Fase fase, float fracao, float desde)
        {
            switch (fase)
            {
                case PescaJogador.Fase.Voando:
                case PescaJogador.Fase.Esperando:
                    return PescaJogador.ForcaEsperando;
                case PescaJogador.Fase.Beliscando:
                    return PescaJogador.ForcaEsperando + PescaJogador.ForcaDaBeliscada * 0.4f * Mathf.Max(0f, PescaJogador.CurvaDaBeliscada((Time.time - desde) / 0.5f));
                case PescaJogador.Fase.Mordendo:
                    return PescaJogador.ForcaDaMordida;
                case PescaJogador.Fase.Puxando:
                    return fracao;
                default:
                    return 0f;
            }
        }
    }
}
