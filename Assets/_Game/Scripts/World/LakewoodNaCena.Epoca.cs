using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using ZombieCityCleaner.Mundo;
using ZombieCityCleaner.Zombies;

namespace ZombieCityCleaner.World
{
    /// <summary>
    /// A época do Lakewood na cena (22/11/2026, a Woodcreek de antes, a etapa 3 do Docs/23-Campanha-e-Mapa.md): a mesma
    /// geografia (as árvores, as ruas, os lotes e as obras nos mesmos lugares), em duas épocas. Quando a época da rede muda
    /// (<see cref="MundoDeLakewood.Epoca"/>), esta máquina troca o mundo dela:
    ///  - os materiais do abandono (as casas, as caixas de longe, as ruas, a usina) pelas cópias limpas (<see cref="NaEpoca"/>:
    ///    o abandono, as rachaduras, a umidade, a ferrugem e a sujeira da rua em 0);
    ///  - as ruas das cidades pela versão de antes (os postes de madeira nova com a lente acesa e a placa sem o risco);
    ///  - as obras de perto, montadas de novo na época (<see cref="ObrasDeLakewood.Montar"/>: novas, com as janelas inteiras,
    ///    sem a hera, as pichações e o saque), e o hospital (<see cref="HospitalDoMundo.FicarNovo"/>);
    ///  - os blocos do chão das cidades (os quintais com o gramado aparado: <see cref="ChaoDeLakewood.Antes"/>) e a grama;
    ///  - os sistemas do mundo de depois: o antes é o "Dia 0" do Lakewood (<see cref="IMundoDoJogo.NoDiaZero"/>: os grupos,
    ///    as emboscadas, o acampamento, as bases e os carros quebrados desligam), sem os zumbis
    ///    (<see cref="ZombieManager.ForaDaEpoca"/>), os carros abandonados da beira e os painéis de sucata
    ///    (<see cref="BeiraDoMapa.Atual"/> nulo) e com o dia limpo (<see cref="AmbienteDoDia.DiaLimpo"/>).
    /// </summary>
    public partial class LakewoodNaCena
    {
        bool _antes, _diaLimpoNosso;
        // as ruas de cada cidade com lotes nas duas versões (o depois e o antes; uma ligada de cada vez)
        readonly List<(GameObject depois, GameObject antes)> _ruasDasCidades = new List<(GameObject, GameObject)>();
        // as lâmpadas dos postes de antes (no mapa) e as luzes acesas nos mais perto da câmera, à noite
        readonly List<Vector3> _luzesDosPostes = new List<Vector3>();
        readonly List<Light> _luzesAcesas = new List<Light>();
        float _proximaLuz;
        const int LuzesDosPostesAcesas = 10;
        const float AlcanceDaLuzDoPoste = 26f;
        // as obras que estavam montando quando a época mudou: quando chegam, voltam para a reserva
        readonly List<Task<ObraPronta>> _obrasOrfas = new List<Task<ObraPronta>>();

        // as cópias limpas dos materiais do abandono (o original → a limpa) e o caminho de volta
        static readonly Dictionary<Material, Material> _limpos = new Dictionary<Material, Material>(), _sujos = new Dictionary<Material, Material>();
        static readonly string[] MarcasDoAbandono = { "_Intensidade", "_Escorrido", "_Desbotado", "_Descascado", "_Umidade", "_Rachadura", "_MusgoNoTelhado", "_Ferrugem", "_Rua", "_Pocas", "_Mofo" };

        /// <summary>A cena do Lakewood está no antes do apocalipse.</summary>
        public static bool NoAntes => Instancia != null && Instancia._antes;

        /// <summary>O material na época de agora: no antes, a cópia limpa de um material do abandono; senão, ele mesmo.</summary>
        public static Material NaEpoca(Material m) => m != null && NoAntes ? Limpo(m) : m;

        // a cópia limpa (criada uma vez); um material que não é do abandono volta como está
        static Material Limpo(Material m)
        {
            if (m == null || _sujos.ContainsKey(m)) return m;
            if (_limpos.TryGetValue(m, out var l) && l != null) return l;
            if (!m.HasProperty("_Intensidade")) return m;
            l = new Material(m) { name = m.name + " (de antes)" };
            foreach (var n in MarcasDoAbandono) if (l.HasProperty(n)) l.SetFloat(n, 0f);
            _limpos[m] = l;
            _sujos[l] = m;
            return l;
        }

        // a cada quadro: a época da rede (mudou, troca o mundo) e o dia limpo do antes
        void AcompanharAEpoca()
        {
            var m = MundoDeLakewood.Instancia;
            bool antes = m != null && m.Antes;
            if (antes != _antes) AplicarAEpoca(antes);
            AcenderOsPostes();
            if (_antes)
            {
                AmbienteDoDia.DiaLimpo = 1f;
                _diaLimpoNosso = true;
            }
            else if (_diaLimpoNosso)
            {
                AmbienteDoDia.DiaLimpo = 0f;
                _diaLimpoNosso = false;
            }
        }

        // à noite, no antes: as luzes dos postes mais perto da câmera (poucas, sem sombra; as outras ficam só com a lente acesa)
        void AcenderOsPostes()
        {
            bool acesas = _antes && CicloDoDia.Noite > 0.3f && _luzesDosPostes.Count > 0;
            if (!acesas)
            {
                foreach (var l in _luzesAcesas) if (l != null && l.enabled) l.enabled = false;
                return;
            }
            if (Time.time < _proximaLuz) return;
            _proximaLuz = Time.time + 0.5f;
            var cam = Camera.main;
            if (cam == null) return;
            var c = OrigemDeLakewood.NoMapa(cam.transform.position);
            _postesPerto.Clear();
            foreach (var p in _luzesDosPostes)
            {
                float d = (p.x - c.x) * (p.x - c.x) + (p.z - c.z) * (p.z - c.z);
                if (d < 160f * 160f) _postesPerto.Add((d, p));
            }
            _postesPerto.Sort((a, b) => a.d.CompareTo(b.d));
            while (_luzesAcesas.Count < LuzesDosPostesAcesas)
            {
                var go = new GameObject("Luz do poste");
                go.transform.SetParent(_pai, false);
                var l = go.AddComponent<Light>();
                l.type = LightType.Spot;
                l.spotAngle = 75f;   // (o círculo na rua, uns 5 m: sem sombra, um cone largo acendia o chão de dentro das lojas)
                l.innerSpotAngle = 40f;
                l.range = AlcanceDaLuzDoPoste;
                l.intensity = 25f;   // (a 7 m do chão: com a queda pela distância, a 2,2 das lojas não chegava lá embaixo)
                l.color = new Color(1f, 0.82f, 0.55f);
                l.shadows = LightShadows.None;
                go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                _luzesAcesas.Add(l);
            }
            for (int i = 0; i < _luzesAcesas.Count; i++)
            {
                var l = _luzesAcesas[i];
                if (i < _postesPerto.Count)
                {
                    l.transform.localPosition = _postesPerto[i].p;   // (no mapa: embaixo da raiz deslocada)
                    l.enabled = true;
                }
                else l.enabled = false;
            }
        }

        readonly List<(float d, Vector3 p)> _postesPerto = new List<(float, Vector3)>();

        void AplicarAEpoca(bool antes)
        {
            float t0 = Time.realtimeSinceStartup;
            _antes = antes;
            Chao.Antes = antes;
            int materiais = TrocarOsMateriais(antes);
            for (int i = 0; i < _ruasDasCidades.Count; i++)
            {
                var (depois, deAntes) = _ruasDasCidades[i];
                // a versão de antes, na primeira vez (LakewoodNaCena.Cidades.cs)
                if (antes && deAntes == null && i < _ruasPorFazer.Count)
                {
                    var (cidade, pop, pai) = _ruasPorFazer[i];
                    deAntes = MontarAsRuas(cidade, pop, pai, true);
                    _ruasDasCidades[i] = (depois, deAntes);
                }
                if (depois != null) depois.SetActive(!antes);
                if (deAntes != null) deAntes.SetActive(antes);
            }
            int obras = RefazerAsObras();
            var hospital = HospitalDoMundo.Instancia;
            if (hospital != null) hospital.FicarNovo(antes);
            int blocos = RefazerOChaoDasCidades();
            if (_grama != null) _grama.Chao = Chao;
            // os zumbis (no anfitrião): no antes, nenhum
            ZombieManager.ForaDaEpoca = antes;
            var zm = ZombieManager.Instancia;
            if (antes && zm != null) zm.RecolherTodos();
            Debug.Log($"[Lakewood] A época: {(antes ? "antes do apocalipse" : "depois (os 5 anos)")}; {materiais} objetos com o material trocado, " +
                      $"{obras} obras e {blocos} blocos do chão refeitos, em {(Time.realtimeSinceStartup - t0) * 1000f:F0} ms.");
        }

        // os materiais do abandono dos objetos montados na hora (as caixas de longe, as ruas, os caminhos, a usina): para as
        // cópias limpas no antes, e de volta no depois. As obras de perto e o hospital são montados de novo, já na época
        int TrocarOsMateriais(bool antes)
        {
            int n = 0;
            foreach (var r in _pai.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                bool mudou = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    if (m == null) continue;
                    var outro = antes ? Limpo(m) : (_sujos.TryGetValue(m, out var s) && s != null ? s : m);
                    if (outro == m) continue;
                    mats[i] = outro;
                    mudou = true;
                }
                if (!mudou) continue;
                r.sharedMaterials = mats;
                n++;
            }
            return n;
        }

        // as obras de perto saem (montam de novo na época nova, perto de alguém); as que estavam montando viram órfãs
        int RefazerAsObras()
        {
            int n = 0;
            foreach (var o in _obras)
            {
                if (o.Perto != null)
                {
                    TirarAObra(o);
                    n++;
                }
                if (o.Pronta != null)
                {
                    Devolver(o.Pronta);
                    o.Pronta = null;
                }
                if (o.Montando != null)
                {
                    _obrasOrfas.Add(o.Montando);
                    o.Montando = null;
                }
                o.Etapa = 0;
                o.Falhou = false;
            }
            _emCurso = null;
            return n;
        }

        // as órfãs que chegaram voltam para a reserva (a hera e as pichações emprestadas)
        void ReceberAsOrfas()
        {
            for (int i = _obrasOrfas.Count - 1; i >= 0; i--)
            {
                var t = _obrasOrfas[i];
                if (!t.IsCompleted) continue;
                if (!t.IsFaulted) Devolver(t.Result);
                _obrasOrfas.RemoveAt(i);
            }
        }

        // os blocos do chão em volta das cidades com lotes são montados de novo (os pesos e a grama dos quintais mudam)
        int RefazerOChaoDasCidades()
        {
            float t = ChaoDeLakewood.TamanhoDoBloco;
            int n = 0;
            foreach (var c in mapa.cidades)
            {
                if (c.contorno == null || c.contorno.Count < 3 || !mapa.TemLotes(c.nome)) continue;
                var min = c.contorno[0];
                var max = c.contorno[0];
                foreach (var p in c.contorno)
                {
                    min = Vector2.Min(min, p);
                    max = Vector2.Max(max, p);
                }
                min -= Vector2.one * 40f;
                max += Vector2.one * 40f;
                foreach (var b in _blocos.Values)
                {
                    float x0 = b.Chave.x * t, z0 = b.Chave.y * t;
                    if (x0 > max.x || x0 + t < min.x || z0 > max.y || z0 + t < min.y) continue;
                    b.Passo = -1f;
                    n++;
                }
            }
            return n;
        }

        // ao sair da cena: o que é estático volta (o Editor não recarrega o domínio entre um Play e outro)
        void EsquecerAEpoca()
        {
            ZombieManager.ForaDaEpoca = false;
            if (_diaLimpoNosso) AmbienteDoDia.DiaLimpo = 0f;
            foreach (var l in _limpos.Values) if (l != null) Destroy(l);
            _limpos.Clear();
            _sujos.Clear();
        }
    }
}
