using System.Collections.Generic;
using UnityEngine;

namespace ZombieCityCleaner.Mundo
{
    /// <summary>
    /// O saque do Hospital Principal (o Plano_Hospital.md da pasta de arte, a parte 7, 08/11/2026), andar por andar, pela
    /// semente do mundo (o mesmo em todas as máquinas e em qualquer thread), nos móveis do <see cref="MoveisDoHospital"/>
    /// com lugares (<see cref="LugaresDoSaque"/>):
    ///  - <b>quantos</b> (<see cref="QuantosNoAndar"/>): o subsolo de 6 a 10, o térreo de 10 a 16, o 2º de 9 a 14 e o 3º de
    ///    6 a 10 (uns 30 a 50 no prédio: pouco, como no resto do mapa, num prédio de 7.400 m²);
    ///  - <b>as raridades</b>: as do hospital sorteado (<see cref="CatalogoDoSaque.ChancesDoHospital"/>: normal 45, incomum 40
    ///    e raro 15, sem épico nem lendário);
    ///  - <b>o que cada móvel guarda</b>, pelo cômodo (<see cref="RegraDe"/>): os remédios e os curativos nos armários, nos
    ///    carrinhos e nos leitos da área médica; o escritório, a lanterna e as gavetas de cabeceira (os comprimidos, a gazua,
    ///    o maço) nas mesas da administração; a pistola e a munição na segurança; as ferramentas, a luz e o galão nas salas
    ///    de máquinas e na oficina; o pano e a limpeza na lavanderia; a roupa da equipe no vestiário e nos carrinhos de
    ///    roupa; o lanche e a bebida na copa e no descanso;
    ///  - <b>o peso de cada cômodo</b> (<see cref="PesoDoComodo"/>): a medicação e os depósitos mais.
    /// Os cômodos lacrados (a A-01, as salas técnicas dela, o quarto 4) não têm saque.
    /// </summary>
    public static class SaqueDoHospital
    {
        public static readonly (int min, int max)[] QuantosNoAndar = { (6, 10), (10, 16), (9, 14), (6, 10) };

        /// <summary>A "unidade" do saque do hospital (só para o item: a chave vem da obra de cada andar).</summary>
        public const int Unidade = 9000;

        const EspecieDoSaque Med = EspecieDoSaque.Farmacia;
        const EspecieDoSaque Mesa = EspecieDoSaque.Escritorio | EspecieDoSaque.Luz | EspecieDoSaque.Cabeceira;
        const EspecieDoSaque Arma = EspecieDoSaque.ArmaCurta | EspecieDoSaque.Municao;
        const EspecieDoSaque Oficina = EspecieDoSaque.Ferramenta | EspecieDoSaque.Luz | EspecieDoSaque.Galao;
        const EspecieDoSaque Copa = EspecieDoSaque.Lanche | EspecieDoSaque.Comida | EspecieDoSaque.Bebida;

        static CatalogoDoSaque.Regra G(EspecieDoSaque cima, EspecieDoSaque dentro, EspecieDoSaque gavetas, float peso) =>
            new CatalogoDoSaque.Regra { cima = cima, dentro = dentro, gavetas = gavetas, peso = peso };

        static readonly HashSet<string> Medicos = new HashSet<string>
        {
            "ArmarioDeRemedios", "CarrinhoDeParada", "CriadoHospitalar", "MesaDeInstrumentos", "MacaEmergencia", "MacaExame",
            "CamaHospital", "BancadaLaboratorio", "BancadaInox", "GeladeiraLaboratorio",
        };

        static readonly HashSet<string> DeMaquinas = new HashSet<string> { "manutencao", "oficina", "geradores", "eletrica", "bombas", "garagem", "camaras" };
        static readonly HashSet<string> DeEstoque = new HashSet<string> { "deposito", "medicacao" };
        static readonly HashSet<string> DeComer = new HashSet<string> { "copa", "descanso", "espera" };

        /// <summary>O que o modelo guarda no cômodo (null: nada).</summary>
        public static CatalogoDoSaque.Regra RegraDe(string modelo, string comodo)
        {
            if (Medicos.Contains(modelo))
            {
                float peso = modelo == "ArmarioDeRemedios" ? 3f : modelo == "CarrinhoDeParada" ? 2f : modelo.StartsWith("Maca") || modelo == "CamaHospital" ? 0.5f : 1f;
                return G(Med, Med, Med, peso);
            }
            switch (modelo)
            {
                case "MesaMonitoramento":
                    return G(Mesa | Arma, Mesa | Arma, Mesa | Arma, 2f);
                case "MesaEscritorio": case "MesaDiretor": case "EstacaoDeTrabalho": case "Arquivo": case "EstanteArquivo":
                {
                    var e = Mesa;
                    if (comodo == "seguranca" || comodo == "diretor") e |= Arma;
                    if (comodo == "consultorio" || comodo == "triagem" || comodo == "medicacao") e |= Med;
                    return G(e, e, e, modelo == "MesaDiretor" ? 2f : 1f);
                }
                case "ArmarioDeAco":
                    if (comodo == "seguranca") return G(0, Arma | EspecieDoSaque.Luz | EspecieDoSaque.RoupaDaSeguranca | EspecieDoSaque.Mochila, 0, 3f);
                    if (DeMaquinas.Contains(comodo)) return G(0, Oficina, 0, 2f);
                    if (DeEstoque.Contains(comodo)) return G(0, Med | EspecieDoSaque.Pano, 0, 2f);
                    return G(0, EspecieDoSaque.Pano | EspecieDoSaque.Luz | EspecieDoSaque.Escritorio, 0, 1f);
                case "Vestiario":
                    return G(0, EspecieDoSaque.RoupaDoHospital | EspecieDoSaque.Mochila | EspecieDoSaque.Cabeceira | EspecieDoSaque.Luz | EspecieDoSaque.Lanche, 0, 3f);
                case "EstanteAco90": case "EstanteAco120": case "Bancada": case "CaixasDeSuprimentos":
                {
                    EspecieDoSaque e;
                    if (DeEstoque.Contains(comodo)) e = Med | EspecieDoSaque.Pano;
                    else if (DeMaquinas.Contains(comodo)) e = Oficina;
                    else if (comodo == "lavanderia" || comodo == "limpeza") e = EspecieDoSaque.Pano | EspecieDoSaque.Limpeza;
                    else if (comodo == "arquivo") e = EspecieDoSaque.Escritorio;
                    else e = EspecieDoSaque.Pano | EspecieDoSaque.Luz | EspecieDoSaque.Ferramenta;
                    return G(e, e, e, modelo == "CaixasDeSuprimentos" ? 2f : 1f);
                }
                case "CarrinhoDeLimpeza":
                    return G(EspecieDoSaque.Limpeza | EspecieDoSaque.Pano, EspecieDoSaque.Limpeza | EspecieDoSaque.Pano, 0, 1f);
                case "CarrinhoDeRoupa":
                    return G(EspecieDoSaque.Pano | EspecieDoSaque.RoupaDoHospital, 0, 0, 1f);
                case "Geladeira":
                    return DeComer.Contains(comodo) ? G(0, EspecieDoSaque.Bebida, 0, 2f) : null;
                case "Mesa4": case "Mesa6": case "MesaDeCentro":
                    return G(Copa, 0, 0, 1f);
                case "ArmarioChao60": case "Pia":
                    return G(Copa, Copa | EspecieDoSaque.Limpeza, EspecieDoSaque.GavetaDaCozinha, 1f);
                case "EstanteLivros":
                    return G(EspecieDoSaque.Lanche | EspecieDoSaque.Luz, 0, 0, 1f);
            }
            return null;
        }

        /// <summary>O modelo guarda saque no hospital, em algum cômodo.</summary>
        public static bool Guarda(string modelo) =>
            RegraDe(modelo, "deposito") != null || RegraDe(modelo, "copa") != null || RegraDe(modelo, "seguranca") != null;

        /// <summary>O peso do cômodo no sorteio do andar (a medicação e os depósitos mais; os corredores quase nada).</summary>
        public static float PesoDoComodo(string comodo) => comodo switch
        {
            "medicacao" => 4f,
            "deposito" => 3f,
            "emergencia" or "vermelha" or "uti" or "enfermaria" or "seguranca" or "vestiario" => 2f,
            "corredor" or "espera" or "entrada" or "hall" => 0.5f,
            _ => 1f,
        };

        /// <summary>O saque do andar f (a peça é a 0; o ponto, o número do móvel em <see cref="MoveisDoHospital.DoAndar"/>), nas coordenadas da obra.</summary>
        public static List<ItemDoSaque> DoAndar(int semente, int f)
        {
            var saida = new List<ItemDoSaque>();
            var an = PlantaDoHospital.DoAndar(f);
            var moveis = MoveisDoHospital.DoAndar(f);
            var pecas = new List<SaqueDaObra.PecaComSaque>();
            for (int i = 0; i < moveis.Count; i++)
            {
                var m = moveis[i];
                if (!LugaresDoSaque.Tem(m.modelo)) continue;
                var k = an.Em(m.lugar.x, m.lugar.z);
                if (k == null || an.fechados.Contains(k)) continue;
                var regra = RegraDe(m.modelo, k.tipo);
                if (regra == null) continue;
                pecas.Add(new SaqueDaObra.PecaComSaque
                {
                    modelo = m.modelo, comodo = k.tipo, ponto = i, sala = an.comodos.IndexOf(k),
                    origem = m.lugar, giro = m.giro, escala = m.escala, regra = regra,
                });
            }
            var (min, max) = QuantosNoAndar[Mathf.Clamp(f, 0, QuantosNoAndar.Length - 1)];
            ulong s = Sorteio.Misturar(((ulong)(uint)semente << 16) ^ (ulong)(uint)f ^ 0x484F5350534151UL);
            SaqueDaObra.SortearPecas(pecas, s, Unidade, min, max, CatalogoDoSaque.ChancesDoHospital, PesoDoComodo, saida);
            return saida;
        }
    }
}
