using UnityEngine;
using ZombieCityCleaner.Mundo;
using ZombieCityCleaner.Player;
using Forma = ZombieCityCleaner.Mundo.DocumentosDoHospital.Forma;

namespace ZombieCityCleaner.Documentos
{
    /// <summary>
    /// Um papel que se lê: mirando nele de perto, "[E] Ler" abre a folha na tela (<see cref="LeituraDePapel"/>). No jogo é um
    /// <see cref="Interagivel"/> (a mira do <see cref="ArmaJogador"/>); nas cenas de exemplo, o <see cref="IUsavelNoExemplo"/>
    /// do <see cref="AndarNoExemplo"/>. Em cima de um móvel, ele ganha da caixa de colisão do móvel (que vai até o alto dele):
    /// a mira passa por ela até o papel, como no saque.
    /// </summary>
    public class PapelNaMira : Interagivel, IUsavelNoExemplo
    {
        [System.NonSerialized] public Documento documento;
        public Forma forma;
        public bool temCaixa;
        public Matrix4x4 paraACaixa;
        public Vector3 caixa;

        public override string Dica(ArmaJogador quem) => documento != null ? "[E] Ler" : null;

        public override void Usar(ArmaJogador quem) => LeituraDePapel.Abrir(documento, forma);

        public override bool GanhaDe(Vector3 ponto, Interagivel antes) => antes == null && NaCaixa(ponto);

        public string DicaNoExemplo => documento != null ? "[E] Ler" : null;

        public void UsarNoExemplo() => LeituraDePapel.Abrir(documento, forma);

        public bool GanhaNoExemplo(Vector3 ponto) => NaCaixa(ponto);

        // o ponto está na caixa do móvel embaixo (com 6 cm de folga)
        bool NaCaixa(Vector3 ponto)
        {
            if (!temCaixa) return false;
            Vector3 l = paraACaixa.MultiplyPoint3x4(ponto);
            Vector3 m = caixa / 2f + Vector3.one * 0.06f;
            return Mathf.Abs(l.x) <= m.x && Mathf.Abs(l.y) <= m.y && Mathf.Abs(l.z) <= m.z;
        }
    }
}
