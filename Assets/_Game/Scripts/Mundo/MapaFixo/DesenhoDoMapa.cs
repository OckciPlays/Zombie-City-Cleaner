using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZombieCityCleaner.Mundo
{
    public enum TipoDeVia { Interestadual, Estadual, Condado, Terra }

    public enum TipoDeCidade { Grande, Media, Pequena, Vila }

    public enum TipoDeDistrito { Centro, RuaPrincipal, Comercial, Bairro, Suburbio, Industrial, Parque, Marina }

    public enum TipoDeLugarFixo { BaseMilitar, Represa, Usina, Serraria, Pedreira, TorreDeVigia, EstacaoDosGuardas, Resort, PostoDeEstrada, Fazenda, CasaDoLago, PatioDaFerrovia }

    public enum TipoDeZona { Lavouras, Brejo, FlorestaNacional }

    /// <summary>O tipo da ponte. Automática: pela via e pela largura do rio (a construção escolhe).</summary>
    public enum TipoDePonte { Automatica, Vigas, Trelica, Pequena, Madeira, Represa }

    public enum EstadoDaPonte { Inteira, Bloqueada }

    /// <summary>O que cobre o chão numa célula da grade do desenho (tirado da imagem de referência).</summary>
    public enum Cobertura : byte { Mata, Campo, Lavoura, Rocha, Agua, Brejo, AguaDoBrejo }

    /// <summary>Uma linha de crista com o morro em volta (a cordilheira, os morros das bordas).</summary>
    [Serializable]
    public class SerraDoDesenho
    {
        public string nome;
        public List<Vector2> crista = new List<Vector2>();
        [Tooltip("A distância da crista em que a serra cai para 37% da altura (m).")] public float largura = 2000f;
        [Tooltip("A altura em cima da crista (m).")] public float altura = 300f;
        [Tooltip("O quanto as cristas do ruído somam por cima (m).")] public float aspereza;
    }

    [Serializable]
    public class PicoDoDesenho
    {
        public string nome;
        public Vector2 ponto;
        [Tooltip("O quanto o pico sobe por cima da serra (m). A altura escrita no mapa é a do relevo.")] public float subida = 600f;
        public float raio = 1300f;
        [Tooltip("O rótulo, a partir do ponto (m).")] public Vector2 rotulo = new Vector2(0f, -400f);
    }

    /// <summary>Uma área achatada (a planície das fazendas): o relevo ali vale o fator do normal.</summary>
    [Serializable]
    public class PlanicieDoDesenho
    {
        public List<Vector2> contorno = new List<Vector2>();
        [Range(0f, 1f)] public float fator = 0.35f;
    }

    /// <summary>Um desfiladeiro (onde a estrada atravessa a serra): perto da linha, o relevo desce até a altura dela.</summary>
    [Serializable]
    public class PassagemDoDesenho
    {
        public string nome;
        public List<Vector2> pontos = new List<Vector2>();
        [Tooltip("A distância da linha em que a passagem vale 37% (m).")] public float largura = 700f;
        [Tooltip("A altura do fundo dela (m).")] public float altura = 250f;
    }

    [Serializable]
    public class ParametrosDoRelevo
    {
        [Tooltip("A altura do fundo dos vales (m).")] public float baseDoVale = 100f;
        [Tooltip("O quanto as colinas sobem e descem (m).")] public float amplitude = 70f;
        [Tooltip("O tamanho das colinas (m).")] public float escala = 2600f;
        [Tooltip("O quanto o vale de cada rio abre para os lados (m).")] public float aberturaDosVales = 380f;
        [Tooltip("A altura das montanhas da grade (m, onde a grade da montanha vale 1).")] public float alturaDasMontanhas;
        [Tooltip("O quanto as cristas do ruído somam nas montanhas da grade (m).")] public float asperezaDasMontanhas;
        [Tooltip("O raio (m) do amaciamento da grade das montanhas: a borda da mancha da imagem vira a encosta (zero, o paredão da grade).")] public float suavidadeDasMontanhas;
        public List<SerraDoDesenho> serras = new List<SerraDoDesenho>();
        public List<PicoDoDesenho> picos = new List<PicoDoDesenho>();
        public List<PassagemDoDesenho> passagens = new List<PassagemDoDesenho>();
        public List<PlanicieDoDesenho> planicies = new List<PlanicieDoDesenho>();
    }

    [Serializable]
    public class LagoDoDesenho
    {
        public string nome;
        public List<Vector2> contorno = new List<Vector2>();
        [Tooltip("A altura da água (m).")] public float nivel = 80f;
        [Tooltip("A fundura no meio (m).")] public float fundura = 28f;
        [Tooltip("A água na altura do chão em volta (o relevo calcula); senão, a do nível.")] public bool nivelPeloRelevo;
        [Tooltip("Onde vai o nome (zero: no meio do lago).")] public Vector2 rotulo;
        public float giroDoRotulo;
    }

    [Serializable]
    public class IlhaDoDesenho
    {
        public string nome;
        public List<Vector2> contorno = new List<Vector2>();
        [Tooltip("A altura no meio da ilha, acima da água (m).")] public float altura = 25f;
    }

    [Serializable]
    public class RioDoDesenho
    {
        public string nome;
        [Tooltip("Da nascente à foz.")] public List<Vector2> pontos = new List<Vector2>();
        public float larguraNoComeco = 12f, larguraNoFim = 60f;
        [Tooltip("A altura da água na nascente e na foz (m).")] public float nivelNoComeco = 300f, nivelNoFim = 81f;
        [Tooltip("Como a água desce: 1 por igual; 3 rápida no começo (a serra) e quase parada no fim.")] public float curva = 3f;
        [Tooltip("A água pelo chão (o relevo calcula, da nascente à foz, sempre descendo e chegando na água de onde ele cai); senão, a dos níveis.")]
        public bool nivelPeloRelevo;
        [Range(0f, 1f), Tooltip("Onde vai o nome, na fração do comprimento.")] public float posicaoDoRotulo = 0.5f;
    }

    [Serializable]
    public class ViaDoDesenho
    {
        public string nome;
        [Tooltip("O número do escudo (I-90, 41); vazio, sem escudo.")] public string numero;
        public TipoDeVia tipo;
        public List<Vector2> pontos = new List<Vector2>();
    }

    /// <summary>O que o desenho diz de uma ponte: a construção acha o cruzamento da via com o rio mais perto de "perto".</summary>
    [Serializable]
    public class PonteDoDesenho
    {
        public string nome;
        public Vector2 perto;
        public TipoDePonte tipo;
        public EstadoDaPonte estado;
    }

    /// <summary>Um bloqueio da quarentena, onde uma estrada sai do mapa.</summary>
    [Serializable]
    public class BloqueioDoDesenho
    {
        public string nome;
        public Vector2 ponto;
    }

    [Serializable]
    public class DistritoDoDesenho
    {
        public string nome;
        public TipoDeDistrito tipo;
        public List<Vector2> contorno = new List<Vector2>();
        [Tooltip("O giro da grade de ruas (graus).")] public float giro;
        [Tooltip("O lado de um quarteirão (m).")] public float quadra = 100f;
        [Tooltip("Onde vai o nome (zero: no meio; sem nome, nada).")] public Vector2 rotulo;
    }

    [Serializable]
    public class AvenidaDoDesenho
    {
        public string nome;
        public List<Vector2> pontos = new List<Vector2>();
    }

    [Serializable]
    public class CidadeDoDesenho
    {
        public string nome;
        public TipoDeCidade tipo;
        public Vector2 centro;
        [Tooltip("O nome, a partir do centro (m).")] public Vector2 rotulo;
        [Tooltip("Pintados em ordem: o de depois fica por cima.")] public List<DistritoDoDesenho> distritos = new List<DistritoDoDesenho>();
        public List<AvenidaDoDesenho> avenidas = new List<AvenidaDoDesenho>();
    }

    [Serializable]
    public class LugarDoDesenho
    {
        public string nome;
        public TipoDeLugarFixo tipo;
        public Vector2 ponto;
        public float giro;
        public float raio = 150f;
        [Tooltip("O nome, a partir do ponto (m).")] public Vector2 rotulo = new Vector2(0f, -260f);
    }

    [Serializable]
    public class ZonaDoDesenho
    {
        [Tooltip("Sem nome, não vai rótulo.")] public string nome;
        public TipoDeZona tipo;
        public List<Vector2> contorno = new List<Vector2>();
        [Tooltip("Onde vai o nome (zero: no meio).")] public Vector2 rotulo;
    }

    /// <summary>Um nome solto, de região (EAGLE MOUNTAINS), escrito espaçado.</summary>
    [Serializable]
    public class RegiaoDoDesenho
    {
        public string nome;
        public Vector2 ponto;
        [Tooltip("Graus, no sentido anti-horário.")] public float giro;
        [Tooltip("A altura das letras (m).")] public float tamanho = 300f;
    }

    /// <summary>
    /// O desenho do mapa fixo (Plano_Mapa_Fixo, Parte F1): a fonte feita à mão do mapa de 15 km, com o que só uma pessoa
    /// decide. A construção (Parte F2) enche o resto. Os pontos são os de controle, em metros no mundo (x para o leste,
    /// y do Vector2 para o norte, o meio do mapa em 0): quem usa suaviza com <see cref="Linha"/> e <see cref="Contorno"/>
    /// (as mesmas passadas para todos). A primeira versão veio do esboço da pasta de arte (`Imgs/Mapas/MapaFixo_Esboco`).
    /// </summary>
    [CreateAssetMenu(menuName = "Zombie City Cleaner/Desenho do mapa fixo", fileName = "DesenhoDoMapa")]
    public sealed class DesenhoDoMapa : ScriptableObject
    {
        /// <summary>O lado do quadrado do mapa (m) e a metade dele.</summary>
        public const float Lado = 15000f, Metade = Lado * 0.5f;

        /// <summary>Onde o desenho fica no projeto.</summary>
        public const string Caminho = "Assets/_Game/Settings/MapaFixo/DesenhoDoMapa.asset";

        public string nome = "Blackwater County";
        [Tooltip("O sorteio do que o desenho não diz (o ruído do relevo).")] public int semente = 1;

        [Header("A quarentena")]
        [Tooltip("O fim do mapa jogável: a cerca onde dá para passar e o paredão onde não dá.")] public List<Vector2> quarentena = new List<Vector2>();
        public List<BloqueioDoDesenho> bloqueios = new List<BloqueioDoDesenho>();

        [Header("O relevo e a água")]
        public ParametrosDoRelevo relevo = new ParametrosDoRelevo();
        public List<LagoDoDesenho> lagos = new List<LagoDoDesenho>();
        public List<IlhaDoDesenho> ilhas = new List<IlhaDoDesenho>();
        public List<RioDoDesenho> rios = new List<RioDoDesenho>();

        [Header("As vias")]
        public List<ViaDoDesenho> estradas = new List<ViaDoDesenho>();
        public List<Vector2> ferrovia = new List<Vector2>();
        public List<PonteDoDesenho> pontes = new List<PonteDoDesenho>();

        [Header("As cidades e os lugares")]
        public List<CidadeDoDesenho> cidades = new List<CidadeDoDesenho>();
        public List<LugarDoDesenho> lugares = new List<LugarDoDesenho>();
        public List<ZonaDoDesenho> zonas = new List<ZonaDoDesenho>();
        public List<RegiaoDoDesenho> regioes = new List<RegiaoDoDesenho>();
        public Vector2 casaDaFamilia;
        public Vector2 rotuloDaFamilia = new Vector2(0f, -300f);

        [Header("As grades (da imagem de referência)")]
        [Tooltip("As células de cada lado das grades, nos 15 km (0: sem grade).")] public int grade;
        [Tooltip("O que cobre cada célula (Cobertura: 0 mata, 1 campo, 2 lavoura, 3 rocha, 4 água, 5 brejo, 6 a água do brejo), da linha do sul para a do norte.")] public byte[] cobertura;
        [Tooltip("O quanto cada célula é montanha (0 a 255), da linha do sul para a do norte.")] public byte[] montanha;

        public bool TemGrade => grade > 1 && cobertura != null && montanha != null && cobertura.Length == grade * grade && montanha.Length == grade * grade;

        // a posição na grade (o meio da célula 0 fica meia célula dentro da borda)
        void NaGrade(float x, float z, out int i, out int j, out float tx, out float tz)
        {
            float lado = Lado / grade;
            float fx = Mathf.Clamp((x + Metade) / lado - 0.5f, 0f, grade - 1.001f), fz = Mathf.Clamp((z + Metade) / lado - 0.5f, 0f, grade - 1.001f);
            i = (int)fx;
            j = (int)fz;
            tx = fx - i;
            tz = fz - j;
        }

        /// <summary>O quanto o ponto é montanha (de 0 a 1, entre as 4 células em volta).</summary>
        public float Montanha(float x, float z)
        {
            if (!TemGrade) return 0f;
            NaGrade(x, z, out int i, out int j, out float tx, out float tz);
            int k = j * grade + i;
            float a = Mathf.Lerp(montanha[k], montanha[k + 1], tx), b = Mathf.Lerp(montanha[k + grade], montanha[k + grade + 1], tx);
            return Mathf.Lerp(a, b, tz) / 255f;
        }

        /// <summary>O que cobre a célula do ponto.</summary>
        public Cobertura CoberturaEm(float x, float z)
        {
            if (!TemGrade) return Cobertura.Mata;
            float lado = Lado / grade;
            int i = Mathf.Clamp(Mathf.FloorToInt((x + Metade) / lado), 0, grade - 1), j = Mathf.Clamp(Mathf.FloorToInt((z + Metade) / lado), 0, grade - 1);
            return (Cobertura)cobertura[j * grade + i];
        }

        /// <summary>O quanto o ponto é de um tipo de cobertura (de 0 a 1, entre as 4 células em volta: as beiradas saem suaves).</summary>
        public float Fracao(Cobertura tipo, float x, float z)
        {
            if (!TemGrade) return 0f;
            NaGrade(x, z, out int i, out int j, out float tx, out float tz);
            int k = j * grade + i;
            byte t = (byte)tipo;
            float a = Mathf.Lerp(cobertura[k] == t ? 1f : 0f, cobertura[k + 1] == t ? 1f : 0f, tx);
            float b = Mathf.Lerp(cobertura[k + grade] == t ? 1f : 0f, cobertura[k + grade + 1] == t ? 1f : 0f, tx);
            return Mathf.Lerp(a, b, tz);
        }

        /// <summary>Uma linha aberta do desenho (as vias, os rios, as cristas), suavizada.</summary>
        public static List<Vector2> Linha(IList<Vector2> pontos) => Linhas.Suavizar(pontos, 3, false);

        /// <summary>Um contorno fechado do desenho (os lagos, as ilhas, os distritos, as zonas, a quarentena), suavizado.</summary>
        public static List<Vector2> Contorno(IList<Vector2> pontos) => Linhas.Suavizar(pontos, 3, true);

        /// <summary>Uma cópia solta (para pintar em outra thread enquanto este é editado). Só na thread principal.</summary>
        public DesenhoDoMapa Copia()
        {
            var c = CreateInstance<DesenhoDoMapa>();
            c.hideFlags = HideFlags.HideAndDontSave;
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(this), c);
            return c;
        }

        /// <summary>A largura do rio na fração do comprimento (m).</summary>
        public static float LarguraDoRio(RioDoDesenho r, float fracao) => Mathf.Lerp(r.larguraNoComeco, r.larguraNoFim, fracao);

        /// <summary>A altura da água do rio na fração do comprimento (m).</summary>
        public static float NivelDoRio(RioDoDesenho r, float fracao) =>
            r.nivelNoFim + (r.nivelNoComeco - r.nivelNoFim) * Mathf.Pow(1f - Mathf.Clamp01(fracao), Mathf.Max(0.1f, r.curva));
    }
}
