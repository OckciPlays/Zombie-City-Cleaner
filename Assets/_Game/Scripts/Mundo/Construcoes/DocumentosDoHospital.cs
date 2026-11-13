using System.Collections.Generic;
using UnityEngine;
using static ZombieCityCleaner.Mundo.PlantaDoHospital;

namespace ZombieCityCleaner.Mundo
{
    /// <summary>
    /// Onde ficam os documentos do Hospital Principal (o Plano_Hospital.md da pasta de arte, a parte 6, "A narrativa, os
    /// documentos"): 20 papéis em lugares fixos, cada um no cômodo da história dele (na mesa, na cama, no banco, no chão ou
    /// pregado na parede), nas coordenadas da obra. Os textos ficam no <c>Resources/Documentos/Hospital.txt</c> (pelo id), e
    /// quem faz os papéis e a tela de ler é o jogo (o <c>PapeisDoLugar</c>, no assembly do jogo; aqui, só os lugares).
    /// Em cima de um móvel, o papel vai no tampo dele (as alturas foram medidas nas malhas dos prefabs, em ×1,30) e guarda a
    /// caixa de colisão do móvel: a mira passa por ela até o papel, como no saque.
    /// </summary>
    public static class DocumentosDoHospital
    {
        public enum Forma { Folha, Carta, Aviso, Prancheta, Pasta, Bilhete, Caderno }

        public struct Papel
        {
            public string id;
            public Forma forma;
            public int andar;
            /// <summary>O meio do papel, nas coordenadas da obra (em cima do tampo ou 4 mm na frente da parede).</summary>
            public Vector3 lugar;
            /// <summary>O giro: o papel deitado no plano xz do giro, a frente em +y e o alto da página em +z.</summary>
            public Quaternion giro;
            public bool naParede;
            /// <summary>A caixa de colisão do móvel embaixo (sem: no chão ou na parede), nas coordenadas da obra.</summary>
            public bool temCaixa;
            public Vector3 caixaMeio, caixaTamanho;
            public float caixaGiro;
        }

        static List<Papel> _papeis;
        static readonly object _trava = new object();

        /// <summary>Os que não deram certo na montagem (o cômodo ou o móvel não achado): o teste do hospital confere.</summary>
        public static readonly List<string> Faltas = new List<string>();

        /// <summary>Os 20 papéis do hospital (montados na primeira vez; os móveis são os da semente 1, os da cena e do mundo).</summary>
        public static List<Papel> Papeis()
        {
            lock (_trava)
            {
                if (_papeis != null) return _papeis;
                Faltas.Clear();
                var l = new List<Papel>();
                // ------------------------------------------------------------ o térreo: o caos
                // nas mesas de escritório com o computador, em cima da pilha de papéis da mesa (o mouse fica no meio)
                SobreMovel(l, "triagem", Forma.Prancheta, 1, "Triagem 1", "MesaEscritorio", 0, -0.50f, 0.06f, 1.012f, 186f);
                NaParedeDoMovel(l, "evacuacao", Forma.Aviso, 1, "Sala de espera", "QuadroDeAvisos", 0.32f, 0.70f, 0.052f, 3f);
                SobreMovel(l, "seguranca", Forma.Caderno, 1, "Segurança", "MesaMonitoramento", 0, 1.02f, 0.22f, 0.99f, 176f);
                SobreMovel(l, "bilhete_ellen", Forma.Bilhete, 1, "Medicação", "BancadaInox", 0, -0.55f, 0.10f, 1.17f, 168f);
                NoChao(l, "ambulancias", Forma.Prancheta, 1, "Recebimento de ambulâncias", -24.55f, 0.90f, 37f);
                // ------------------------------------------------------------ o 2º: o isolamento
                NaParede(l, "ficha_iso4", Forma.Prancheta, 2, "Corredor do isolamento", 0, 4.12f, 1.48f, -2f);
                NaParede(l, "recado_iso4", Forma.Aviso, 2, "Corredor do isolamento", 0, 1.55f, 1.62f, 4f);
                NaParede(l, "protocolo", Forma.Aviso, 2, "Corredor do isolamento", 2, -8.45f, 1.50f, -1.5f);
                SobreMovel(l, "diario_lowell", Forma.Caderno, 2, "Descanso da equipe", "Mesa4", 0, 0.06f, 0.02f, 0.99f, 9f);
                SobreMovel(l, "lista_uti", Forma.Prancheta, 2, "UTI", "CamaHospital", 2, -0.05f, 0.22f, 0.87f, 14f);
                // ------------------------------------------------------------ o 3º: o que a administração sabia
                SobreMovel(l, "memorando", Forma.Folha, 3, "Secretaria", "MesaEscritorio", 0, -0.50f, 0.07f, 1.012f, 184f);
                SobreMovel(l, "ata", Forma.Pasta, 3, "Sala de reunião", "Mesa6", 0, -0.32f, 0.04f, 0.99f, -7f);
                SobreMovel(l, "ordem_estado", Forma.Pasta, 3, "Sala de planejamento", "Mesa6", 0, 0.26f, -0.08f, 0.99f, 15f);
                SobreMovel(l, "carta_whitaker", Forma.Carta, 3, "Sala do diretor", "MesaDiretor", 0, -0.25f, 0.17f, 1.012f, 192f);
                SobreMovel(l, "email_ti", Forma.Folha, 3, "TI e servidores", "MesaEscritorio", 0, -0.10f, 0.14f, 0.99f, 172f);
                // ------------------------------------------------------------ o subsolo: a A-01
                SobreMovel(l, "livro_necroterio", Forma.Caderno, 0, "Necrotério", "BancadaInox", 0, -0.45f, 0.06f, 1.17f, 182f);
                // a bandeja da bancada fica do lado de +x (até 1,21 m): o laudo vai do outro lado
                SobreMovel(l, "laudo", Forma.Prancheta, 0, "Sala de autópsia", "BancadaInox", 0, -0.45f, 0.06f, 1.17f, 171f);
                SobreMovel(l, "ordem_servico", Forma.Folha, 0, "Manutenção", "Bancada", 0, 0.36f, 0.12f, 1.17f, 188f);
                NaParede(l, "registro_a01", Forma.Prancheta, 0, "Acesso A-01", 1, -11.30f, 1.48f, 2f);
                SobreMovel(l, "recado_ray", Forma.Bilhete, 0, "Vestiário", "BancoRefeitorio", 0, -0.65f, 0.10f, 0.59f, 12f);
                _papeis = l;
                return l;
            }
        }

        /// <summary>O tamanho de cada forma no jogo (a largura, a altura da página e a grossura), em metros.</summary>
        public static Vector3 Tamanho(Forma f) => f switch
        {
            Forma.Prancheta => new Vector3(0.26f, 0.36f, 0.012f),
            Forma.Pasta => new Vector3(0.27f, 0.34f, 0.014f),
            Forma.Bilhete => new Vector3(0.15f, 0.11f, 0.002f),
            Forma.Caderno => new Vector3(0.46f, 0.31f, 0.024f),
            _ => new Vector3(0.25f, 0.32f, 0.002f),
        };

        // ------------------------------------------------------------ os jeitos de pôr

        static Comodo Sala(int f, string nome, string id)
        {
            var an = DoAndar(f);
            foreach (var k in an.comodos)
                if (an.nomes[k] == nome) return k;
            Faltas.Add($"{id}: o cômodo \"{nome}\" do andar {f} não existe");
            return null;
        }

        // o n-ésimo móvel do modelo no cômodo
        static bool Movel(int f, Comodo k, string modelo, int n, string id, out MoveisDoHospital.Movel achado)
        {
            achado = default;
            int i = 0;
            foreach (var m in MoveisDoHospital.DoAndar(f))
            {
                if (m.modelo != modelo || !k.Contem(m.lugar.x, m.lugar.z)) continue;
                if (i++ == n) { achado = m; return true; }
            }
            Faltas.Add($"{id}: o móvel {modelo} nº {n} não está no cômodo");
            return false;
        }

        /// <summary>Em cima de um móvel: o ponto (x, z) no espaço dele (no jogo, a frente em +z), a altura do tampo e o giro
        /// do papel em relação ao móvel (180: o alto da página para o fundo, de frente para quem senta na frente).</summary>
        static void SobreMovel(List<Papel> l, string id, Forma forma, int f, string sala, string modelo, int n,
                               float x, float z, float tampo, float giro)
        {
            var k = Sala(f, sala, id);
            if (k == null || !Movel(f, k, modelo, n, id, out var m)) return;
            var q = Quaternion.Euler(0f, m.giro, 0f);
            var p = new Papel
            {
                id = id, forma = forma, andar = f,
                lugar = m.lugar + q * new Vector3(x, tampo + Tamanho(forma).z / 2f + 0.002f, z),
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
        }

        /// <summary>Pregado na frente de um móvel de parede (o mural): o ponto (x, y) no espaço dele, quanto sai da parede e o
        /// giro do papel no plano dela.</summary>
        static void NaParedeDoMovel(List<Papel> l, string id, Forma forma, int f, string sala, string modelo, float x, float y,
                                    float frente, float torto)
        {
            var k = Sala(f, sala, id);
            if (k == null || !Movel(f, k, modelo, 0, id, out var m)) return;
            var q = Quaternion.Euler(0f, m.giro, 0f);
            Vector3 n = q * Vector3.forward;
            l.Add(new Papel
            {
                id = id, forma = forma, andar = f, naParede = true,
                lugar = m.lugar + q * new Vector3(x, y, frente + Tamanho(forma).z / 2f + 0.002f),
                giro = NaParede(n, torto),
            });
        }

        /// <summary>
        /// Pregado numa parede do cômodo: o lado (0 a parede de z0, que olha para +z; 1 a de x1; 2 a de z1; 3 a de x0), onde
        /// ao longo dela (o x ou o z da obra), a altura do meio do papel e o quanto ele fica torto. A parede tem 20 cm (a face a
        /// 10 cm da linha).
        /// </summary>
        static void NaParede(List<Papel> l, string id, Forma forma, int f, string sala, int lado, float aoLongo, float altura, float torto)
        {
            var k = Sala(f, sala, id);
            if (k == null) return;
            float sai = 0.1f + Tamanho(forma).z / 2f + 0.004f, y = Base(f) + altura;
            Vector3 lugar, n;
            switch (lado)
            {
                case 0: lugar = new Vector3(aoLongo, y, k.z0 + sai); n = Vector3.forward; break;
                case 1: lugar = new Vector3(k.x1 - sai, y, aoLongo); n = Vector3.left; break;
                case 2: lugar = new Vector3(aoLongo, y, k.z1 - sai); n = Vector3.back; break;
                default: lugar = new Vector3(k.x0 + sai, y, aoLongo); n = Vector3.right; break;
            }
            l.Add(new Papel { id = id, forma = forma, andar = f, naParede = true, lugar = lugar, giro = NaParede(n, torto) });
        }

        /// <summary>Caído no chão do cômodo (x e z da obra), com o giro.</summary>
        static void NoChao(List<Papel> l, string id, Forma forma, int f, string sala, float x, float z, float giro)
        {
            var k = Sala(f, sala, id);
            if (k == null) return;
            if (!k.Contem(x, z)) Faltas.Add($"{id}: o ponto ({x}, {z}) não está no cômodo \"{sala}\"");
            l.Add(new Papel
            {
                id = id, forma = forma, andar = f,
                lugar = new Vector3(x, Base(f) + Tamanho(forma).z / 2f + 0.012f, z),
                giro = Quaternion.Euler(0f, giro, 0f),
            });
        }

        // o papel de pé na parede: a frente (o +y dele) para fora da parede e o alto da página (o +z) para cima, girado no
        // plano da parede
        static Quaternion NaParede(Vector3 fora, float torto) =>
            Quaternion.AngleAxis(torto, fora) * Quaternion.LookRotation(Vector3.up, fora);
    }
}
