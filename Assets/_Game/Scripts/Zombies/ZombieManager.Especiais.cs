using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using ZombieCityCleaner.Combat;
using ZombieCityCleaner.Player;
using ZombieCityCleaner.World;

namespace ZombieCityCleaner.Zombies
{
    /// <summary>
    /// Os zumbis especiais no mundo (01/11/2026, a pedido; o comportamento de cada um em Zumbi.*.cs):
    /// <list type="bullet">
    /// <item>o Corredor nasce no lugar de um Oco, às vezes: poucos de dia, mais de noite, com as hordas
    /// (<see cref="AjustesDoCorredor.fracaoDeDia"/>, <see cref="AjustesDoCorredor.fracaoDeNoite"/>);</item>
    /// <item>o Long, só de noite e raro: a cada <c>intervaloDoNascimento</c>, uma chance de aparecer a 35-50 m de um
    /// jogador, fora da vista (no máximo <c>maximo</c> ao mesmo tempo);</item>
    /// <item>o Brutamonte, um por mundo: a cada <c>intervaloDoNascimento</c>, a chance (maior de noite) de brotar da terra
    /// a 30-45 m de um jogador. Sem ninguém por perto um tempo, ele volta para a terra e guarda a vida que perdeu.</item>
    /// </list>
    /// Também o grito do Long (a horda vem), os sons e os efeitos (a terra abrindo, o rugido, os baques). Os sons provisórios
    /// ficam em Resources/Especiais (feitos por Ferramentas/especiais_sons.py; o Pedro troca depois).
    /// </summary>
    public partial class ZombieManager
    {
        readonly Dictionary<TipoDeZumbi, int[]> _modelosPorTipo = new Dictionary<TipoDeZumbi, int[]>();
        float _proximoBrutamonte = 60f, _proximoLong = 45f, _vidaGuardadaDoBruto = -1f;

        /// <summary>Os zumbis desta máquina: no anfitrião, os de verdade; nos clientes, as cópias (a barra do Brutamonte).</summary>
        internal IReadOnlyList<Zumbi> Visiveis() => IsServer ? _ativos : _copias;

        /// <summary>Os índices dos modelos de um tipo (as variações de roupa).</summary>
        internal int[] ModelosDoTipo(TipoDeZumbi tipo)
        {
            if (_modelosPorTipo.TryGetValue(tipo, out var lista)) return lista;
            var l = new List<int>();
            for (int i = 0; i < modelos.Length; i++)
                if (modelos[i] != null && modelos[i].tipo == tipo) l.Add(i);
            lista = l.ToArray();
            _modelosPorTipo[tipo] = lista;
            return lista;
        }

        /// <summary>O modelo de quem nasce: um Oco, ou (às vezes) um Corredor.</summary>
        int ModeloParaNascer(int id, byte geracao)
        {
            var corredores = ModelosDoTipo(TipoDeZumbi.Corredor);
            if (corredores.Length > 0)
            {
                var c = ajustes.corredor;
                if (Random.value < (EDia ? c.fracaoDeDia : c.fracaoDeNoite)) return corredores[Random.Range(0, corredores.Length)];
            }
            return ModeloDe(id, geracao);
        }

        /// <summary>Um especial agora (o anfitrião; o F1 também), numa das roupas dele.</summary>
        public Zumbi NascerEspecial(TipoDeZumbi tipo, Vector3 posicao, float yaw)
        {
            if (!IsServer || !IsSpawned) return null;
            var lista = ModelosDoTipo(tipo);
            if (lista.Length == 0)
            {
                Debug.LogWarning($"Sem modelo de {tipo}: rode Zombie City Cleaner > Zumbis > Montar os zumbis especiais.");
                return null;
            }
            var z = Nascer(posicao, lista[Random.Range(0, lista.Length)]);
            if (z == null) return null;
            z.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            if (tipo == TipoDeZumbi.Brutamonte && _vidaGuardadaDoBruto > 0f) z.GuardarDano(_vidaGuardadaDoBruto);
            return z;
        }

        /// <summary>O F1: um especial na frente do jogador (a 6 m, olhando para ele).</summary>
        public Zumbi CriarEspecialPerto(TipoDeZumbi tipo, Vector3 pes, Vector3 frente)
        {
            frente.y = 0f;
            if (frente.sqrMagnitude < 0.01f) frente = Vector3.forward;
            frente.Normalize();
            float dist = tipo == TipoDeZumbi.Long ? 18f : tipo == TipoDeZumbi.Brutamonte ? 9f : 6f;
            if (!Sortear(pes + frente * dist, 1.5f, out var p, false)) return null;
            Vector3 olhando = pes - p;
            return NascerEspecial(tipo, p, Mathf.Atan2(olhando.x, olhando.z) * Mathf.Rad2Deg);
        }

        /// <summary>A cada quadro, no anfitrião: o Long e o Brutamonte aparecem (e o Brutamonte volta para a terra).</summary>
        void EspeciaisNoServidor(float agora)
        {
            if (QuantosJogadores == 0 || Povoando) return;
            // o Brutamonte: um por mundo
            Zumbi bruto = null;
            int longs = 0;
            for (int i = 0; i < _ativos.Count; i++)
            {
                var z = _ativos[i];
                if (z.Morto) continue;
                if (z.tipo == TipoDeZumbi.Brutamonte) bruto = z;
                else if (z.tipo == TipoDeZumbi.Long) longs++;
            }
            var b = ajustes.brutamonte;
            if (bruto != null)
            {
                // sem ninguém por perto um tempo: volta para a terra (guarda a vida)
                float perto = float.MaxValue;
                for (int k = 0; k < QuantosJogadores; k++) perto = Mathf.Min(perto, Vector3.Distance(Jogadores[k].pes, bruto.transform.position));
                if (perto > b.voltaParaATerra)
                {
                    if (bruto.semNinguemDesde < 0f) bruto.semNinguemDesde = agora;
                    else if (agora - bruto.semNinguemDesde > b.tempoSemNinguem)
                    {
                        _vidaGuardadaDoBruto = bruto.DanoNoCorpo;
                        Devolver(bruto);
                        _proximoBrutamonte = agora + b.intervaloDoNascimento;
                    }
                }
                else bruto.semNinguemDesde = -1f;
            }
            else if (agora >= _proximoBrutamonte)
            {
                _proximoBrutamonte = agora + b.intervaloDoNascimento;
                if (Random.value < (EDia ? b.chanceDeDia : b.chanceDeNoite) && LugarForaDaVista(30f, 45f, out var p))
                    NascerEspecial(TipoDeZumbi.Brutamonte, p, Random.Range(0f, 360f));
            }
            // o Long: só de noite
            var c = ajustes.cuspidor;
            if (!EDia && agora >= _proximoLong)
            {
                _proximoLong = agora + c.intervaloDoNascimento;
                if (longs < c.maximo && Random.value < c.chance && LugarForaDaVista(35f, 50f, out var p))
                    NascerEspecial(TipoDeZumbi.Long, p, Random.Range(0f, 360f));
            }
        }

        /// <summary>Um lugar no NavMesh entre as distâncias de um jogador (sorteado), onde nenhum jogador vê.</summary>
        bool LugarForaDaVista(float de, float ate, out Vector3 ponto)
        {
            ponto = default;
            ref readonly var j = ref Jogadores[Random.Range(0, QuantosJogadores)];
            for (int t = 0; t < 14; t++)
            {
                Vector2 r = Random.insideUnitCircle.normalized * Random.Range(de, ate);
                var tentativa = j.pes + new Vector3(r.x, 0f, r.y);
                if (!NavMesh.SamplePosition(tentativa, out var hit, 3f, NavMesh.AllAreas) || EmAreaLivre(hit.position)) continue;
                bool visto = false;
                for (int k = 0; k < QuantosJogadores && !visto; k++)
                    visto = Vector3.Distance(Jogadores[k].pes, hit.position) < de * 0.8f || Enxerga(Jogadores[k].olho, hit.position + Vector3.up * 1.2f);
                if (visto && t < 10) continue;
                ponto = hit.position;
                return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ o grito do Long

        /// <summary>
        /// O grito (no anfitrião): os zumbis até o raio vêm (os de perto atrás de quem o feriu; os outros vão ver), e chegam
        /// outros, de fora da vista, já atrás de quem o feriu.
        /// </summary>
        internal void GritoDoLong(Zumbi longo, JogadorRede autor)
        {
            var c = ajustes.cuspidor;
            float agora = Time.time;
            Vector3 onde = longo.transform.position;
            for (int i = 0; i < _ativos.Count; i++)
            {
                var z = _ativos[i];
                if (z == longo || z.Morto || z.Especial) continue;
                float d = Vector3.Distance(z.transform.position, onde);
                if (d > c.raioDoGrito) continue;
                if (autor != null && d < c.raioDoGrito * 0.6f) z.ComecarPerseguicao(autor, agora);
                else z.Investigar(autor != null ? autor.transform.position : onde, agora);
            }
            int novos = Random.Range(c.chamadosPeloGrito.x, c.chamadosPeloGrito.y + 1);
            for (int n = 0; n < novos; n++)
            {
                Vector2 r = Random.insideUnitCircle.normalized * Random.Range(30f, 50f);
                if (!NavMesh.SamplePosition(onde + new Vector3(r.x, 0f, r.y), out var hit, 3f, NavMesh.AllAreas) || EmAreaLivre(hit.position)) continue;
                var z = Nascer(hit.position);
                if (z != null && autor != null)
                {
                    z.MudarNivelNoServidor(Zumbi.Nivel.Medio);
                    z.ComecarPerseguicao(autor, agora);
                }
            }
        }

        // ------------------------------------------------------------------ sons e efeitos (em cada máquina)

        static readonly Dictionary<string, AudioClip> _sonsDosEspeciais = new Dictionary<string, AudioClip>();

        static AudioClip SomEspecial(string nome)
        {
            if (!_sonsDosEspeciais.TryGetValue(nome, out var c))
            {
                c = Resources.Load<AudioClip>("Especiais/" + nome);
                _sonsDosEspeciais[nome] = c;
            }
            return c;
        }

        void TocarEspecial(string nome, Vector3 ponto, float volume, float alcance)
        {
            var c = SomEspecial(nome);
            if (c == null || !PodeOuvir(ponto)) return;
            Efeitos.Som(c, ponto, volume * VolumeDosZumbis, true, alcance);
        }

        /// <summary>O Brutamonte brotando da terra: o chão treme e se abre (a poeira e a terra), o ronco grave.</summary>
        internal void EfeitoDeEmergir(Zumbi z)
        {
            Vector3 p = z.transform.position;
            TocarEspecial("Brutamonte_Terra", p, 1f, 70f);
            StartCoroutine(TerraSubindo(p));
        }

        System.Collections.IEnumerator TerraSubindo(Vector3 p)
        {
            for (int i = 0; i < 14; i++)
            {
                Vector2 r = Random.insideUnitCircle * 1.1f;
                Efeitos.Impacto(p + new Vector3(r.x, 0.05f, r.y), Vector3.up);
                yield return new WaitForSeconds(0.2f);
            }
        }

        internal void RugidoDoBrutamonte(Zumbi z, float tom) => TocarEspecial(tom > 1f ? "Brutamonte_Furia" : "Brutamonte_Rugido", z.transform.position + Vector3.up * 1.8f, 1f, 60f);

        internal void BaqueDoBrutamonte(Zumbi z) => TocarEspecial("Brutamonte_Baque", z.transform.position + Vector3.up * 0.5f, 0.9f, 40f);

        internal void SomDoLong(Zumbi z, bool grito) => TocarEspecial(grito ? "Long_Grito" : "Long_Cuspe", z.transform.position + Vector3.up * 2f, 1f, grito ? 110f : 35f);

        internal void AgoniaDoLong(Zumbi z) => TocarEspecial("Long_Agonia", z.transform.position + Vector3.up * 2f, 1f, 50f);
    }
}
