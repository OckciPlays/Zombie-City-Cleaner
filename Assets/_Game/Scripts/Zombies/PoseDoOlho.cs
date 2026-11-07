using System.Collections.Generic;
using UnityEngine;

namespace ZombieCityCleaner.Zombies
{
    /// <summary>
    /// A pose de repouso do olho do Brutamonte (04/11/2026): o giro de repouso do globo e das duas pálpebras (cada um no
    /// espaço do seu pai) e os eixos do olho: a <see cref="frente"/> (para onde o olho aberto olha: as costas do personagem),
    /// o <see cref="cima"/> (o do personagem) e o <see cref="lado"/> (cima × frente: girar o cima em volta dele leva à frente),
    /// no espaço do pai do globo (o "chest"); <see cref="ladoSup"/> e <see cref="ladoInf"/> são o mesmo lado no espaço do pai
    /// de cada pálpebra (no contrato do Blender, o mesmo chest).
    /// <para>
    /// Tudo sai das bindposes da malha (a pose em que ela foi presa aos ossos: o repouso do Blender, com o olho aberto olhando
    /// para trás), e não da pose do quadro. Antes, o Zumbi pegava o repouso no primeiro quadro animado: com o peito curvado
    /// da animação, os eixos saíam tortos (e, com o Animator desligado num quadro, o "repouso" era a pálpebra já fechada).
    /// Os eixos partem do cima e das costas da raiz do personagem, nunca dos eixos locais dos ossos (a importação do FBX
    /// pode espelhar o X). Sem a bindpose de algum osso (fora da malha), aquele osso usa a pose atual dele
    /// (<see cref="dasBindposes"/> falso): só certo se ainda não houve animação.
    /// </para>
    /// Usada pelo <see cref="Zumbi"/> (Zumbi.Brutamonte.cs) e pelo <see cref="OlhoDoBrutamonte"/> (a cena de comparação).
    /// </summary>
    public struct PoseDoOlho
    {
        public Quaternion olho, sup, inf;
        public Vector3 lado, cima, frente, ladoSup, ladoInf;
        /// <summary>Tudo saiu das bindposes (senão, algum osso veio da pose atual).</summary>
        public bool dasBindposes;

        static readonly List<Matrix4x4> _binds = new List<Matrix4x4>(64);

        /// <summary>
        /// A pose de repouso. <paramref name="raiz"/> é a raiz do personagem (o cima dela é o cima dele, as costas são o
        /// -forward); <paramref name="smr"/>, a malha presa aos ossos (pode ser nula: aí vale a pose atual). Falso só sem os ossos.
        /// </summary>
        public static bool Calcular(Transform raiz, SkinnedMeshRenderer smr, Transform olho, Transform sup, Transform inf, out PoseDoOlho p)
        {
            p = default;
            if (raiz == null || olho == null || sup == null || inf == null || olho.parent == null || sup.parent == null || inf.parent == null)
                return false;
            Transform[] ossos = null;
            if (smr != null && smr.sharedMesh != null)
            {
                ossos = smr.bones;
                _binds.Clear();
                smr.sharedMesh.GetBindposes(_binds);
                if (_binds.Count != ossos.Length) ossos = null;
            }
            bool tudo = true;
            // o giro de repouso de um osso no espaço da malha (a inversa da bindpose), se ele é um osso da malha
            bool NaMalha(Transform t, out Quaternion giro)
            {
                giro = Quaternion.identity;
                int i = ossos != null ? System.Array.IndexOf(ossos, t) : -1;
                if (i < 0) return false;
                var m = _binds[i].inverse;
                giro = Quaternion.LookRotation(m.GetColumn(2), m.GetColumn(1));
                return true;
            }
            Quaternion Local(Transform t)
            {
                if (NaMalha(t, out var giro) && NaMalha(t.parent, out var doPai)) return Quaternion.Inverse(doPai) * giro;
                tudo = false;
                return t.localRotation;
            }
            // o pai de um osso, no repouso, em relação à raiz do personagem
            Quaternion PaiNaRaiz(Transform pai)
            {
                if (NaMalha(pai, out var giro)) return Quaternion.Inverse(raiz.rotation) * smr.transform.rotation * giro;
                tudo = false;
                return Quaternion.Inverse(raiz.rotation) * pai.rotation;
            }
            p.olho = Local(olho);
            p.sup = Local(sup);
            p.inf = Local(inf);
            // na raiz: as costas, o cima e o lado (cima × costas)
            Vector3 costas = Vector3.back, cimaNaRaiz = Vector3.up, ladoNaRaiz = Vector3.Cross(cimaNaRaiz, costas);
            var inv = Quaternion.Inverse(PaiNaRaiz(olho.parent));
            p.frente = (inv * costas).normalized;
            p.cima = (inv * cimaNaRaiz).normalized;
            p.lado = (inv * ladoNaRaiz).normalized;
            p.ladoSup = sup.parent == olho.parent ? p.lado : (Quaternion.Inverse(PaiNaRaiz(sup.parent)) * ladoNaRaiz).normalized;
            p.ladoInf = inf.parent == olho.parent ? p.lado : (Quaternion.Inverse(PaiNaRaiz(inf.parent)) * ladoNaRaiz).normalized;
            p.dasBindposes = tudo;
            return true;
        }

        /// <summary>
        /// Aplica a pose: as pálpebras fechando <paramref name="fechar"/> graus cada uma (a de cima desce, a de baixo sobe) e
        /// o globo girado (<paramref name="giro"/> para o lado, <paramref name="inclinacao"/> para cima), em volta do repouso.
        /// </summary>
        public readonly void Aplicar(Transform olhoT, Transform supT, Transform infT, float fechar, float giro, float inclinacao)
        {
            supT.localRotation = Quaternion.AngleAxis(fechar, ladoSup) * sup;
            infT.localRotation = Quaternion.AngleAxis(-fechar, ladoInf) * inf;
            olhoT.localRotation = Quaternion.AngleAxis(giro, cima) * Quaternion.AngleAxis(-inclinacao, lado) * olho;
        }

        /// <summary>A malha presa ao osso (a primeira dos renderers que tem o osso; senão, a primeira com malha).</summary>
        public static SkinnedMeshRenderer MalhaDe(IReadOnlyList<Renderer> renderers, Transform osso)
        {
            if (renderers == null) return null;
            SkinnedMeshRenderer primeira = null;
            for (int i = 0; i < renderers.Count; i++)
            {
                if (!(renderers[i] is SkinnedMeshRenderer s) || s.sharedMesh == null) continue;
                if (primeira == null) primeira = s;
                if (osso != null && System.Array.IndexOf(s.bones, osso) >= 0) return s;
            }
            return primeira;
        }
    }
}
