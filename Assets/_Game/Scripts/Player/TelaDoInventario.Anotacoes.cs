using UnityEngine;
using ZombieCityCleaner.UI;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// A aba ANOTAÇÕES da mochila (25/11/2026, a pedido do Pedro: "No tab por exemplo, quando vamos em anotações, tudo bem ter
    /// as missões disponíveis ainda e as missões que já realizamos, as riscadas"): o caderno aberto, do meio até o fim da coluna
    /// da direita, como a árvore das habilidades (o desenho: <see cref="CadernoDeMissoes.DesenharAsAnotacoes"/>). A tecla P
    /// abre direto nela (como o H do CORPO e o K das HABILIDADES); a página vira com o som do papel.
    /// </summary>
    public partial class TelaDoInventario
    {
        static readonly Rect AbaAnotacoes = new Rect(718, 14, 112, 36);
        static readonly Rect AreaDasAnotacoes = new Rect(GradeX + 6f, 64, 1142f - GradeX - 12f, 504);

        // a tecla P: fechada abre nela; aberta nela fecha; aberta em outra, troca
        void TeclaDasAnotacoes()
        {
            if (Aberta)
            {
                if (_aba == Aba.Anotacoes) Abrir(false);
                else IrParaAbaAnotacoes();
            }
            else if (MenuOnline.Controla && rede != null && rede.IsOwner && !MenuOnline.Aberto)
            {
                IrParaAbaAnotacoes();
                Abrir(true);
            }
        }

        void IrParaAbaAnotacoes()
        {
            if (_aba != Aba.Anotacoes) CadernoDeMissoes.TocarAqui(CadernoDeMissoes.SomDaPagina, 0.45f);
            _aba = Aba.Anotacoes;
            if (_apertou == Origem.Estante)
            {
                _apertou = Origem.Nada;
                _arrastando = false;
            }
            _estante = -1;
        }

        /// <summary>Para os testes: abre a mochila na aba ANOTAÇÕES.</summary>
        public void AbrirNaAbaAnotacoes()
        {
            IrParaAbaAnotacoes();
            Abrir(true);
        }

        void AbaDasAnotacoes() => CadernoDeMissoes.DesenharAsAnotacoes(Em(AreaDasAnotacoes), _u, inventario);
    }
}
