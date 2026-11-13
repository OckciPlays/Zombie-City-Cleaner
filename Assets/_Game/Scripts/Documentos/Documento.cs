using System.Collections.Generic;
using UnityEngine;

namespace ZombieCityCleaner.Documentos
{
    /// <summary>Como o papel foi escrito: datilografado, à mão ou o formulário impresso preenchido à mão.</summary>
    public enum Letra { Maquina, Mao, Formulario }

    /// <summary>
    /// Um documento que se lê (o Plano_Hospital.md da pasta de arte, a parte 6, "A narrativa, os documentos"): o timbre, o
    /// tipo, o carimbo, a letra e as linhas do texto, lidos de um arquivo de texto em <c>Resources/Documentos</c> (o formato
    /// está no começo do <c>Hospital.txt</c>). Onde cada um fica e a forma do papel vêm do lugar (o
    /// <c>DocumentosDoHospital</c>), pelo id.
    /// </summary>
    public sealed class Documento
    {
        public string id, titulo, cabecalho, tipo, carimbo;
        public Letra letra;

        /// <summary>As linhas do texto, na ordem; a vazia separa os parágrafos. aMao: escrita à mão por cima (o "~ ").</summary>
        public readonly List<(string texto, bool aMao)> linhas = new List<(string, bool)>();

        /// <summary>Quantas letras o texto tem (para escolher o tamanho da letra na folha).</summary>
        public int Tamanho
        {
            get
            {
                int n = 0;
                foreach (var (t, _) in linhas) n += t.Length;
                return n;
            }
        }

        static readonly Dictionary<string, Dictionary<string, Documento>> _arquivos = new Dictionary<string, Dictionary<string, Documento>>();

        // a cada Play, os textos são lidos de novo (o Play sem recarregar o domínio guardaria os de antes de mudar o arquivo)
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Zerar() => _arquivos.Clear();

        /// <summary>Os documentos de um arquivo de Resources (sem a extensão: "Documentos/Hospital"), pelo id; lidos uma vez.</summary>
        public static Dictionary<string, Documento> DoArquivo(string caminho)
        {
            if (_arquivos.TryGetValue(caminho, out var pronto)) return pronto;
            var a = Resources.Load<TextAsset>(caminho);
            var d = a != null ? Ler(a.text) : new Dictionary<string, Documento>();
            if (a == null) Debug.LogWarning($"[Documentos] Não achei o arquivo Resources/{caminho}.txt.");
            _arquivos[caminho] = d;
            return d;
        }

        /// <summary>Lê o texto inteiro de um arquivo de documentos.</summary>
        public static Dictionary<string, Documento> Ler(string texto)
        {
            var d = new Dictionary<string, Documento>();
            Documento atual = null;
            bool noCorpo = false;
            foreach (var cru in texto.Replace("\r\n", "\n").Split('\n'))
            {
                string linha = cru.TrimEnd();
                if (linha.StartsWith("=== "))
                {
                    Fechar(atual);
                    atual = new Documento { id = linha.Substring(4).Trim() };
                    d[atual.id] = atual;
                    noCorpo = false;
                    continue;
                }
                if (atual == null) continue;
                if (!noCorpo)
                {
                    if (linha.StartsWith("#") || linha.Length == 0) continue;
                    if (linha == "---") { noCorpo = true; continue; }
                    int dois = linha.IndexOf(':');
                    if (dois < 0) continue;
                    string campo = linha.Substring(0, dois).Trim(), valor = linha.Substring(dois + 1).Trim();
                    switch (campo)
                    {
                        case "titulo": atual.titulo = valor; break;
                        case "cabecalho": atual.cabecalho = valor; break;
                        case "tipo": atual.tipo = valor; break;
                        case "carimbo": atual.carimbo = valor; break;
                        case "letra":
                            atual.letra = valor == "mao" ? Letra.Mao : valor == "formulario" ? Letra.Formulario : Letra.Maquina;
                            break;
                    }
                    continue;
                }
                if (linha.StartsWith("~ ")) atual.linhas.Add((linha.Substring(2), true));
                else atual.linhas.Add((linha, false));
            }
            Fechar(atual);
            return d;
        }

        // sem as linhas vazias do fim e sem as repetidas no meio
        static void Fechar(Documento d)
        {
            if (d == null) return;
            while (d.linhas.Count > 0 && d.linhas[d.linhas.Count - 1].texto.Length == 0) d.linhas.RemoveAt(d.linhas.Count - 1);
            for (int i = d.linhas.Count - 1; i > 0; i--)
                if (d.linhas[i].texto.Length == 0 && d.linhas[i - 1].texto.Length == 0) d.linhas.RemoveAt(i);
        }
    }
}
