using System.Collections.Generic;
using UnityEngine;
using static ZombieCityCleaner.Mundo.PlantaDaLojaDeArmas;
using Forma = ZombieCityCleaner.Mundo.DocumentosDoHospital.Forma;
using Papel = ZombieCityCleaner.Mundo.DocumentosDoHospital.Papel;

namespace ZombieCityCleaner.Mundo
{
    /// <summary>
    /// Onde ficam os papéis da loja de armas (o Plano_LojaDeArmas.md, a decisão 9; o sistema dos documentos do hospital): o
    /// aviso de fechamento colado na porta da frente (por fora), o livro de vendas caído atrás do caixa, o bilhete do
    /// funcionário na mesa do escritório, o relatório do estoque pregado na parede dos fundos, o inventário da sala segura na
    /// bancada dela, o quadro das regras do estande na antessala e, conforme a variação, o diário do sobrevivente no
    /// acampamento do estande e o bilhete do dono colado na porta de cofre fechada (o estado C). Os textos ficam no
    /// <c>Resources/Documentos/LojaDeArmas.txt</c> (pelo id); quem faz os papéis e a tela de ler é o jogo (o PapeisDoLugar).
    /// </summary>
    public static class DocumentosDaLojaDeArmas
    {
        /// <summary>Os que não deram certo (o móvel não achado): o teste da loja confere.</summary>
        public static readonly List<string> Faltas = new List<string>();

        /// <summary>Os ids de todos os papéis que algum estado usa (o texto de cada um tem de estar no arquivo).</summary>
        public static readonly string[] Todos =
        {
            "fechamento", "livro_vendas", "bilhete_funcionario", "relatorio_estoque", "inventario_cofre", "regras_estande",
            "diario_sobrevivente", "bilhete_dono",
        };

        public static List<Papel> Papeis(Variacao v)
        {
            Faltas.Clear();
            var l = new List<Papel>();
            var moveis = MoveisDaLojaDeArmas.Da(v);
            // o aviso de fechamento, colado por fora na folha da direita da entrada (a fechada, de vidro)
            NaFace(l, "fechamento", Forma.Aviso, new Vector3(MeioDaEntrada + 0.55f, 1.5f, Z0 - 0.03f), Vector3.back, -3f);
            // o livro de vendas, caído atrás do caixa (perto da poça)
            NoChao(l, "livro_vendas", Forma.Caderno, X(10.2f), Z(20.25f), 168f);
            // o bilhete do funcionário na mesa do escritório (em cima da pilha de papéis, ao lado do computador)
            SobreMovel(l, moveis, "bilhete_funcionario", Forma.Bilhete, "MesaEscritorio", Escritorio, 0, -0.50f, 0.06f, 1.012f, 186f);
            // o relatório do estoque, pregado na parede dos fundos, ao lado da porta dos fundos
            NaFace(l, "relatorio_estoque", Forma.Prancheta, new Vector3(X(10.45f), 1.5f, Estoque.yMax - 0.1f - 0.01f), Vector3.back, 2f);
            // o inventário da sala segura, na bancada dela
            SobreMovel(l, moveis, "inventario_cofre", Forma.Prancheta, "Bancada", SalaSegura, 0, 0.36f, 0.12f, 1.17f, 188f);
            // as regras do estande, pregadas na antessala, em cima do banco
            NaFace(l, "regras_estande", Forma.Aviso, new Vector3(X(16.7f), 1.75f, Antessala.yMax - 0.1f - 0.004f), Vector3.back, -2f);
            if (v.acampamento) NoChao(l, "diario_sobrevivente", Forma.Caderno, X(18.1f), Z(17.3f), 74f);
            // o bilhete do dono, colado na folha da porta de cofre fechada (do lado do estoque, em cima do segredo)
            if (v.sala == EstadoDaSala.Intacta)
                NaFace(l, "bilhete_dono", Forma.Aviso, PortaDoCofre + new Vector3(0.215f, 1.98f, -0.25f), Vector3.right, 4f);
            return l;
        }

        static void SobreMovel(List<Papel> l, List<MoveisDaLojaDeArmas.Movel> moveis, string id, Forma forma, string modelo, Rect sala, int n,
                               float x, float z, float tampo, float giro)
        {
            int i = 0;
            foreach (var m in moveis)
            {
                if (m.modelo != modelo || !sala.Contains(new Vector2(m.lugar.x, m.lugar.z))) continue;
                if (i++ != n) continue;
                var q = Quaternion.Euler(0f, m.giro, 0f);
                var p = new Papel
                {
                    id = id, forma = forma, andar = 0,
                    lugar = m.lugar + q * new Vector3(x, tampo + DocumentosDoHospital.Tamanho(forma).z / 2f + 0.002f, z),
                    giro = Quaternion.Euler(0f, m.giro + giro, 0f),
                };
                if (m.caixa && MedidasDaMobilia.Tem(m.modelo))
                {
                    Vector3 t = MedidasDaMobilia.Real(m.modelo) * m.escala;
                    Vector2 meio = MedidasDaMobilia.Meio(m.modelo) * m.escala;
                    float baixo = MedidasDaMobilia.Baixo(m.modelo) * m.escala;
                    p.temCaixa = true;
                    p.caixaMeio = m.lugar + q * new Vector3(meio.x, baixo + t.y / 2f, meio.y);
                    p.caixaTamanho = t;
                    p.caixaGiro = m.giro;
                }
                l.Add(p);
                return;
            }
            Faltas.Add($"{id}: o móvel {modelo} nº {n} não está no cômodo");
        }

        // pregado numa face (de pé), o meio em 'lugar', virado para 'fora' (já com a grossura do papel)
        static void NaFace(List<Papel> l, string id, Forma forma, Vector3 lugar, Vector3 fora, float torto) =>
            l.Add(new Papel
            {
                id = id, forma = forma, andar = 0, naParede = true,
                lugar = lugar + fora * (DocumentosDoHospital.Tamanho(forma).z / 2f + 0.002f),
                giro = Quaternion.AngleAxis(torto, fora) * Quaternion.LookRotation(Vector3.up, fora),
            });

        static void NoChao(List<Papel> l, string id, Forma forma, float x, float z, float giro) =>
            l.Add(new Papel
            {
                id = id, forma = forma, andar = 0,
                lugar = new Vector3(x, DocumentosDoHospital.Tamanho(forma).z / 2f + 0.012f, z),
                giro = Quaternion.Euler(0f, giro, 0f),
            });
    }
}
