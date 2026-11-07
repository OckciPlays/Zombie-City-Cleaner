using System.Collections.Generic;
using UnityEngine;
using ZombieCityCleaner.World;

namespace ZombieCityCleaner.Music
{
    /// <summary>
    /// O clima na música (a pedido em 02/11/2026; World/CicloDoDia.Tempo.cs e World/ClimaNaTela.cs):
    /// <list type="bullet">
    /// <item>No chuvisco e na chuva, a trilha de sempre (a neblina de dia, a noite à noite).</item>
    /// <item>Na tempestade (com ou sem raios), os trechos da exploração saem da família da tempestade (o grave pesado, o
    /// cello batendo como a chuva, os tímpanos como trovão).</item>
    /// <item>Com o tornado perto, a família do tornado, mais angustiante; quando quem joga aqui avista o tornado, o que estiver
    /// tocando da exploração (ou o silêncio) dá lugar na hora a um trecho do tornado, que abre com o golpe (a entrada dele).
    /// Enquanto ele está perto, o silêncio entre os trechos é curto.</item>
    /// <item>O perigo dos zumbis continua por cima de tudo (o tornado não interrompe uma perseguição). A sombra (só a
    /// tensão, baixinho, no silêncio; 04/11/2026) não é perigo de verdade: o tornado avistado passa na frente dela.</item>
    /// </list>
    /// Os blocos saem do Ferramentas/trilha/clima.py.
    /// </summary>
    public partial class MaestroDaTrilha
    {
        static readonly Dictionary<string, string[]> SeguinteNaTempestade = new Dictionary<string, string[]>
        {
            { "entrada", new[] { "ostinato", "coro", "tambores" } }, { "ostinato", new[] { "coro", "tambores", "cordas", "ostinato" } },
            { "coro", new[] { "ostinato", "cordas", "tambores" } }, { "tambores", new[] { "ostinato", "coro", "cordas" } },
            { "cordas", new[] { "ostinato", "coro", "tambores" } },
        };
        static readonly Dictionary<string, string[]> SeguinteNoTornado = new Dictionary<string, string[]>
        {
            { "entrada", new[] { "espiral", "coracao" } }, { "espiral", new[] { "lamento", "coracao", "metal" } },
            { "coracao", new[] { "espiral", "lamento", "metal" } }, { "lamento", new[] { "espiral", "coracao", "metal" } },
            { "metal", new[] { "espiral", "lamento", "coracao" } },
        };

        float _avistouVisto = -999f;

        static bool TemBlocos(TrilhaDoJogo.Familia f) => f != null && f.blocos != null && f.blocos.Length > 0 && f.comprimento > 0f;

        /// <summary>A família da exploração agora: a do tornado perto, a da tempestade ou a de sempre.</summary>
        string FamiliaDoTempo(bool dia)
        {
            if (Tempo.Tornado && ClimaNaTela.PertoDoTornado > 0.1f && TemBlocos(_trilha.tornado)) return "tornado";
            if (Tempo.Nivel >= NivelDoTempo.Tempestade && TemBlocos(_trilha.tempestade)) return "tempestade";
            return dia ? "neblina" : "noite";
        }

        TrilhaDoJogo.Familia FamiliaPeloNome(string nome) => nome switch
        {
            "tempestade" => _trilha.tempestade,
            "tornado" => _trilha.tornado,
            "neblina" => _trilha.neblina,
            _ => _trilha.noite,
        };

        Dictionary<string, string[]> SeguintePeloNome(string nome) => nome switch
        {
            "tempestade" => SeguinteNaTempestade,
            "tornado" => SeguinteNoTornado,
            "neblina" => SeguinteNaNeblina,
            _ => SeguinteNaNoite,
        };

        Vector2Int BlocosPeloNome(string nome) => nome == "neblina" ? blocosNaNeblina : blocosNaNoite;

        static string NomeDaFamilia(string nome) => nome switch
        {
            "tempestade" => "Tempestade",
            "tornado" => "Tornado",
            "neblina" => "Neblina",
            _ => "Noite",
        };

        // a cada quadro, na partida (antes de decidir o próximo trecho): o tornado avistado corta para a família dele
        void TempoNaMusica(double agora, bool dia)
        {
            if (_trilha == null) return;
            bool avistou = ClimaNaTela.AvistouEm > _avistouVisto + 0.01f;
            if (avistou) _avistouVisto = ClimaNaTela.AvistouEm;
            // a sombra (a tensão baixinho no silêncio, 04/11/2026) é como o silêncio: o tornado avistado passa na frente dela
            if (_estado != Estado.Silencio && _estado != Estado.Exploracao && !NaSombra) return;
            if (avistou && _familia != "tornado" && TemBlocos(_trilha.tornado))
            {
                if (NaSombra) SairDaSombra(agora, 1.2f);
                ApagarGrupos(agora, 1.2f, "exploracao");
                Anotar(agora, "o tornado foi avistado");
                ComecarTrecho(agora, dia);
                return;
            }
            // com o tornado perto, o silêncio entre os trechos é curto
            if (_estado == Estado.Silencio && Tempo.Tornado && ClimaNaTela.PertoDoTornado > 0.3f && _proximaMusica - agora > 8.0)
                _proximaMusica = agora + 8.0;
        }
    }
}
