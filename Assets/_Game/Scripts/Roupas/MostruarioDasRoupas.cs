using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ZombieCityCleaner.Roupas
{
    /// <summary>
    /// O mostruário das roupas (a cena <c>Scenes/Exemplos/Roupas.unity</c>, o Plano_Roupas.md da pasta de arte): os NPCs
    /// do catálogo numa fila, e a câmera em volta do escolhido.
    ///  - ← e → passam de um NPC para o outro; o mouse com o botão esquerdo gira a câmera, e a roda aproxima;
    ///  - 1 a 8 trocam a animação de todos (parado, andando, correndo, agachado, andando agachado, sentado, pulando a
    ///    janela e em guarda);
    ///  - R sorteia as cores das peças do escolhido (os pelos todos da mesma cor), C volta às do catálogo, V tira e põe
    ///    a roupa dele, B troca o corpo (o do homem e as formas do catálogo: a mulher) e P passa para o próximo tom de pele;
    ///  - Tab escolhe um lugar do corpo (a cabeça, o cabelo, os óculos, a máscara, o tronco...), e T troca a peça do escolhido nesse
    ///    lugar pela próxima do catálogo (<see cref="Vestimenta.TrocarNoLugar"/>);
    ///  - G escolhe um tipo do sorteio (o sobrevivente, o civil, o policial...), e N veste o escolhido com um NPC novo
    ///    sorteado desse tipo (<see cref="Vestimenta.VestirSorteado"/>, com uma semente ao acaso);
    ///  - H esconde o texto.
    /// No fim da fila ficam os sorteados de cada tipo (os da montagem da cena, com as sementes 1 e 2).
    /// As animações são as do personagem principal (o controlador do mostruário, <c>NPC_Mostruario.controller</c>).
    /// </summary>
    public class MostruarioDasRoupas : MonoBehaviour
    {
        public static readonly (string estado, string nome)[] Animacoes =
        {
            ("Parado", "parado"), ("Andar", "andando"), ("Correr", "correndo"), ("Agachado", "agachado"),
            ("Andar_Agachado", "andando agachado"), ("Sentado_Motorista", "sentado"), ("Pular_Janela", "pulando a janela"),
            ("Guarda", "em guarda"),
        };

        // as cores do sorteio: os panos, os jeans, as solas e os cabelos (hex)
        static readonly string[] Panos =
        {
            "#56653F", "#6B2A2A", "#2E3D5C", "#A07A2C", "#3B3B3E", "#7E7F82", "#CDBF9F", "#D9D6CE", "#4F6146",
            "#8E2B26", "#5C4636", "#2F4A44", "#9A6A3A", "#596A7A", "#B8A57E", "#24262B",
        };
        static readonly string[] Jeans = { "#3F5A7E", "#2A3346", "#6E8BB0", "#2B2B2E", "#4A4F57", "#55607A" };
        static readonly string[] Cabelos = { "#2B1E16", "#1A1716", "#5A3A22", "#9C7A45", "#7A3B1E", "#8C8A86" };

        // os nomes dos lugares do catálogo, para o texto
        static readonly Dictionary<string, string> NomesDosLugares = new Dictionary<string, string>
        {
            { "Cabelo", "o cabelo" }, { "Cabeca", "a cabeça" }, { "Barba", "a barba" }, { "Cilios", "os cílios" },
            { "Oculos", "os óculos" }, { "Mascara", "a máscara" }, { "Tronco", "o tronco" }, { "Por_Cima", "por cima" },
            { "Colete", "o colete" }, { "Cintura", "a cintura" }, { "Pernas", "as pernas" }, { "Pes", "os pés" },
            { "Maos", "as mãos" },
        };

        public float distancia = 3.2f;
        float _giro = 200f, _inclinacao = 12f;
        int _atual, _animacao, _lugar, _tipo;
        string[] _lugares = new string[0];
        string[] _tipos = new string[0];
        bool _texto = true;
        Vestimenta[] _npcs = new Vestimenta[0];
        readonly Dictionary<Vestimenta, List<CatalogoDasRoupas.PecaVestida>> _guardadas = new Dictionary<Vestimenta, List<CatalogoDasRoupas.PecaVestida>>();
        GUIStyle _estilo;

        void Start()
        {
            // a fila vai para -X: de frente (a câmera olha para -Z), da esquerda para a direita da tela
            _npcs = FindObjectsByType<Vestimenta>().OrderByDescending(v => v.transform.position.x).ToArray();
            _atual = Mathf.Min(1, _npcs.Length - 1);    // o primeiro vestido (o 0 é o corpo sem roupa)
            if (_npcs.Length > 0)
            {
                _lugares = _npcs[0].Catalogo.pecas.Select(p => p.lugar).Distinct().ToArray();
                _lugar = Mathf.Max(0, System.Array.IndexOf(_lugares, "Cabeca"));
                _tipos = (_npcs[0].Catalogo.tipos ?? new CatalogoDasRoupas.Tipo[0]).Select(t => t.nome).ToArray();
            }
        }

        void Update()
        {
            var k = Keyboard.current;
            var mouse = Mouse.current;
            if (k == null || _npcs.Length == 0) return;
            if (k.rightArrowKey.wasPressedThisFrame) _atual = (_atual + 1) % _npcs.Length;
            if (k.leftArrowKey.wasPressedThisFrame) _atual = (_atual + _npcs.Length - 1) % _npcs.Length;
            for (int i = 0; i < Animacoes.Length; i++)
                if (k[(Key)((int)Key.Digit1 + i)].wasPressedThisFrame) Animar(i);
            if (k.hKey.wasPressedThisFrame) _texto = !_texto;
            var v = _npcs[_atual];
            if (k.rKey.wasPressedThisFrame) Sortear(v);
            if (k.cKey.wasPressedThisFrame)
            {
                if (!string.IsNullOrEmpty(v.npc)) v.Vestir(v.npc);
                else if (!string.IsNullOrEmpty(v.tipo)) v.VestirSorteado(v.tipo, v.semente);
            }
            if (k.gKey.wasPressedThisFrame && _tipos.Length > 0) _tipo = (_tipo + 1) % _tipos.Length;
            if (k.nKey.wasPressedThisFrame && _tipos.Length > 0) v.VestirSorteado(_tipos[_tipo], Random.Range(1, 1000000));
            if (k.bKey.wasPressedThisFrame)
            {
                var corpos = new[] { CatalogoDasRoupas.Homem }.Concat(v.Catalogo.formas ?? new string[0]).ToArray();
                v.TrocarCorpo(corpos[(System.Array.IndexOf(corpos, v.corpo) + 1) % corpos.Length]);
            }
            if (k.pKey.wasPressedThisFrame && v.Catalogo.peles != null && v.Catalogo.peles.Length > 0)
            {
                var peles = v.Catalogo.peles;
                v.TrocarPele(peles[(System.Array.IndexOf(peles, v.pele) + 1) % peles.Length]);
            }
            if (k.tabKey.wasPressedThisFrame && _lugares.Length > 0) _lugar = (_lugar + 1) % _lugares.Length;
            if (k.tKey.wasPressedThisFrame && _lugares.Length > 0) v.TrocarNoLugar(_lugares[_lugar]);
            if (k.vKey.wasPressedThisFrame)
            {
                if (v.Vestidas.Count > 0)
                {
                    _guardadas[v] = v.Vestidas.ToList();
                    v.Vestir(new CatalogoDasRoupas.PecaVestida[0]);
                }
                else if (_guardadas.TryGetValue(v, out var antes)) v.Vestir(antes);
            }
            if (mouse != null)
            {
                if (mouse.leftButton.isPressed)
                {
                    var d = mouse.delta.ReadValue();
                    _giro += d.x * 0.3f;
                    _inclinacao = Mathf.Clamp(_inclinacao - d.y * 0.2f, -20f, 70f);
                }
                distancia = Mathf.Clamp(distancia - mouse.scroll.ReadValue().y * 0.002f, 0.8f, 8f);
            }
            var alvo = v.transform.position + Vector3.up * 1.0f;
            var rot = Quaternion.Euler(_inclinacao, _giro, 0f);
            var lugar = alvo - rot * Vector3.forward * distancia;
            transform.position = Vector3.Lerp(transform.position, lugar, 1f - Mathf.Exp(-10f * Time.deltaTime));
            transform.rotation = Quaternion.LookRotation(alvo - transform.position);
        }

        void Animar(int i)
        {
            _animacao = i;
            string estado = Animacoes[i].estado;
            foreach (var v in _npcs)
            {
                var a = v.GetComponentInChildren<Animator>();
                if (a == null) continue;
                bool guarda = estado == "Guarda";
                a.CrossFadeInFixedTime(guarda ? "Parado" : estado, 0.2f, 0);
                a.SetLayerWeight(1, guarda ? 1f : 0f);
            }
        }

        static string Uma(string[] lista) => lista[Random.Range(0, lista.Length)];

        void Sortear(Vestimenta v)
        {
            string pelos = Uma(Cabelos);
            foreach (var p in v.Vestidas.ToList())
            {
                var dados = v.Catalogo.DaPeca(p.nome);
                string[] cores = dados.pelos
                    ? new[] { pelos, Uma(Panos), Uma(Panos) }
                    : p.nome == "Jeans" ? new[] { Uma(Jeans), "#B5803C", "#B5803C" }
                    : new[] { Uma(Panos), Uma(Panos), Uma(Panos) };
                if (p.nome == "Tenis") cores[1] = Random.value < 0.7f ? "#E6E3DC" : "#2E2F33";
                v.Pintar(p.nome, cores);
            }
        }

        void OnGUI()
        {
            if (!_texto || _npcs.Length == 0) return;
            float u = Screen.height / 720f;
            _estilo ??= new GUIStyle(GUI.skin.label) { richText = true };
            _estilo.fontSize = Mathf.RoundToInt(15 * u);
            var v = _npcs[_atual];
            string nome = !string.IsNullOrEmpty(v.npc)
                ? v.npc + (string.IsNullOrEmpty(v.tipo) ? "" : $"  <color=#9a9a9a>({v.tipo.ToLowerInvariant()})</color>")
                : !string.IsNullOrEmpty(v.tipo) ? $"{v.tipo} sorteado  <color=#9a9a9a>(semente {v.semente})</color>"
                : "Sem roupa";
            string tipo = _tipos.Length == 0 ? "" : _tipos[_tipo].ToLowerInvariant();
            string pecas = v.Vestidas.Count == 0 ? "o corpo do personagem principal, sem nenhuma peça"
                : string.Join("\n", v.Vestidas.Select(p => $"  {p.nome.Replace('_', ' ')}  <color=#9a9a9a>{string.Join(" ", p.cores)}</color>"));
            string corpo = v.corpo == CatalogoDasRoupas.Homem ? "homem" : v.corpo.ToLowerInvariant();
            int tom = v.Catalogo.peles == null ? -1 : System.Array.IndexOf(v.Catalogo.peles, v.pele);
            string lugar = _lugares.Length == 0 ? "" : NomesDosLugares.TryGetValue(_lugares[_lugar], out var n) ? n
                : _lugares[_lugar].Replace('_', ' ').ToLowerInvariant();
            string texto = $"<b>{nome}</b>  ({_atual + 1} de {_npcs.Length})  ·  {corpo}, pele {(tom < 0 ? v.pele : (tom + 1).ToString())}" +
                           $"  ·  {v.Triangulos:N0} triângulos\n{pecas}\n\n" +
                           $"← →  outro NPC    1 a 8  a animação ({Animacoes[_animacao].nome})\n" +
                           "R  sortear as cores    C  as cores do catálogo    V  tirar e pôr a roupa    B  o corpo    P  a pele\n" +
                           $"Tab  o lugar ({lugar})    T  trocar a peça dele\n" +
                           $"G  o tipo do sorteio ({tipo})    N  sortear um NPC desse tipo\n" +
                           "mouse  girar    roda  aproximar    H  esconder o texto";
            var r = new Rect(16 * u, 12 * u, 900 * u, 400 * u);
            var sombra = new Rect(r.x + 1, r.y + 1, r.width, r.height);
            GUI.color = Color.black;
            GUI.Label(sombra, texto, _estilo);
            GUI.color = Color.white;
            GUI.Label(r, texto, _estilo);
        }
    }
}
