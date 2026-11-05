using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using ZombieCityCleaner.Combat;
using ZombieCityCleaner.Player;

namespace ZombieCityCleaner.Zombies
{
    /// <summary>
    /// O Brutamonte enterrado no caminho (05/11/2026, a pedido: "o spawn normal do Brutamonte está muito longe ... ele
    /// deveria spawnar mais próximo da gente, dando uma sensação de que ele estava debaixo da terra, e quando chegamos perto
    /// ele spawnou"). Quando o sorteio de sempre o escolhe (um por mundo; 30% de noite e 5% de dia, a cada 3 min:
    /// ZombieManager.Especiais.cs), ele não nasce mais a 30–45 m fora da vista: fica enterrado num ponto no caminho de um
    /// jogador, a <see cref="distanciaDoEnterrado"/> m à frente de onde ele olha, ao ar livre e no chão do mundo (numa laje,
    /// numa ponte ou num andar, o corpo subindo apareceria por baixo). Quem chega a <see cref="raioDoDespertar"/> m ouve o
    /// chão roncar e vê a terra levantando poeira por <see cref="avisoDoDespertar"/> s, a <see cref="distanciaDaSaida"/> m
    /// dele, e ele sai da terra ali (o Emergir de sempre, com a terra abrindo), a pelo menos <see cref="longeAoSair"/> m de
    /// todos e de costas para quem chegou: o olho dele, nas costas, já o vê. Se ninguém passar por ele em <see cref="tempoEnterrado"/> s, ele se enterra de novo à frente
    /// de alguém. Só no anfitrião; o ponto não vai para o save (como o Brutamonte).
    /// </summary>
    public partial class ZombieManager
    {
        [Header("O Brutamonte enterrado (05/11/2026; ZombieManager.Enterrado.cs)")]
        [Tooltip("Onde ele se enterra: à frente de onde o jogador olha, entre estas distâncias (m).")]
        public Vector2 distanciaDoEnterrado = new Vector2(25f, 40f);
        [Tooltip("Quem chegar a esta distância (m) o acorda.")]
        public float raioDoDespertar = 10f;
        [Tooltip("Ele sai da terra a esta distância (m) de quem o acordou, no caminho até onde estava.")]
        public float distanciaDaSaida = 5.5f;
        [Tooltip("O aviso antes de ele sair (s): o chão ronca e solta poeira.")]
        public float avisoDoDespertar = 1.2f;
        [Tooltip("Sem ninguém passar por ele neste tempo (s), ele se enterra de novo à frente de alguém.")]
        public float tempoEnterrado = 120f;
        [Tooltip("Ao sair, a pelo menos esta distância (m) de quem o acordou.")]
        public float longeAoSair = 4.5f;

        bool _temEnterrado;
        Vector3 _enterrado;
        float _enterradoDesde, _acordaEm = -1f;
        // o que acabou de sair: ao terminar o Emergir, trava em quem o olho vê (Zumbi.Enterrado.cs)
        Zumbi _saiuDaTerra;
        float _saiuDaTerraEm;
        int _adiamentos;   // quantas vezes a saída esperou alguém sair de cima do ponto

        /// <summary>No anfitrião: há um Brutamonte enterrado, esperando alguém passar.</summary>
        public bool TemBrutamonteEnterrado => _temEnterrado;

        /// <summary>No anfitrião: onde ele está enterrado (o F1 e os testes).</summary>
        public Vector3 PontoDoBrutamonteEnterrado => _enterrado;

        /// <summary>
        /// No anfitrião: enterra o Brutamonte no caminho de um jogador (a partir de um sorteado; sem lugar à frente dele, o
        /// próximo). Devolve se achou o lugar.
        /// </summary>
        internal bool PlantarBrutamonte(float agora)
        {
            int inicio = Random.Range(0, Mathf.Max(1, QuantosJogadores));
            for (int n = 0; n < QuantosJogadores; n++)
                if (PlantarBrutamonte(agora, (inicio + n) % QuantosJogadores, distanciaDoEnterrado)) return true;
            return false;
        }

        /// <summary>
        /// Um lugar onde ele pode sair da terra: ao ar livre, no chão do mundo (numa laje, numa ponte ou num andar, o corpo
        /// subindo apareceria por baixo), fora das áreas livres e das zonas seguras, do fogo e do bunker.
        /// </summary>
        bool LugarDeSair(Vector3 p)
        {
            if (EmAreaLivre(p) || NaZonaSegura(p) || Coberto(p) || NoFogo(p) || World.BunkerDoMundo.NaArea(p)) return false;
            var dados = Dados;
            return dados == null || Mathf.Abs(p.y - dados.Altura(p.x, p.z)) <= 0.6f;
        }

        bool PlantarBrutamonte(float agora, int qual, Vector2 distancia)
        {
            if (QuantosJogadores == 0) return false;
            ref readonly var j = ref Jogadores[Mathf.Clamp(qual, 0, QuantosJogadores - 1)];
            Vector3 frente = j.olhar;
            frente.y = 0f;
            if (frente.sqrMagnitude < 0.01f) frente = Vector3.forward;
            frente.Normalize();
            for (int t = 0; t < 18; t++)
            {
                // à frente; sem lugar ali (um muro, um prédio), abre o leque
                float giro = Random.Range(-35f, 35f) * (t < 10 ? 1f : 2.5f);
                Vector3 tentativa = j.pes + Quaternion.Euler(0f, giro, 0f) * frente * Random.Range(distancia.x, distancia.y);
                if (!NavMesh.SamplePosition(tentativa, out var hit, 3f, NavMesh.AllAreas)) continue;
                Vector3 p = hit.position;
                if (!LugarDeSair(p)) continue;
                _enterrado = p;
                _temEnterrado = true;
                _enterradoDesde = agora;
                _acordaEm = -1f;
                return true;
            }
            return false;
        }

        /// <summary>A cada quadro, no anfitrião: quem chega perto acorda o enterrado; sem ninguém passar, ele muda de lugar.</summary>
        void BrutamonteEnterradoNoServidor(float agora)
        {
            if (_saiuDaTerra != null)
            {
                // 3,6 s saindo; depois, até 4 s para o olho achar quem o acordou
                if (_saiuDaTerra.Morto || agora - _saiuDaTerraEm > 8f) _saiuDaTerra = null;
                else if (_saiuDaTerra.BrutamonteProcurando && _saiuDaTerra.TravarSeOlhoVe()) _saiuDaTerra = null;
            }
            if (!_temEnterrado || QuantosJogadores == 0) return;
            if (_acordaEm >= 0f)
            {
                if (agora >= _acordaEm) Despertar(agora);
                return;
            }
            for (int k = 0; k < QuantosJogadores; k++)
            {
                // quem passa de carro não o acorda (sairia atrás do carro, sem ninguém ver)
                var jr = Jogadores[k].jogador;
                if (jr != null && jr.Passageiro != null && jr.Passageiro.NoCarro) continue;
                Vector3 d = Jogadores[k].pes - _enterrado;
                if (Mathf.Abs(d.y) > 3f) continue;
                d.y = 0f;
                if (d.sqrMagnitude > raioDoDespertar * raioDoDespertar) continue;
                // ele sai a uns distanciaDaSaida m de quem chegou, no caminho até onde estava enterrado (o chão ronca ali)
                float dist = d.magnitude;
                if (dist > distanciaDaSaida + 0.5f)
                {
                    Vector3 saida = Jogadores[k].pes - d / dist * distanciaDaSaida;
                    if (NavMesh.SamplePosition(saida, out var hit, 2f, NavMesh.AllAreas) && LugarDeSair(hit.position)) _enterrado = hit.position;
                }
                _adiamentos = 0;
                _acordaEm = agora + avisoDoDespertar;
                TerraRoncandoRpc(_enterrado, avisoDoDespertar);
                return;
            }
            // ninguém passou: ele se enterra de novo à frente de alguém (sem lugar, tenta de novo depois)
            if (agora - _enterradoDesde > tempoEnterrado && !PlantarBrutamonte(agora)) _enterradoDesde = agora;
        }

        // sai da terra: longe de todos o bastante para não nascer em cima de ninguém, e de costas para quem chegou
        void Despertar(float agora)
        {
            _temEnterrado = false;
            _acordaEm = -1f;
            // um Brutamonte já de pé (o do F1): um por mundo, este não sai
            for (int i = 0; i < _ativos.Count; i++)
                if (!_ativos[i].Morto && _ativos[i].tipo == TipoDeZumbi.Brutamonte) return;
            Vector3 p = _enterrado;
            int maisPerto = MaisPertoDe(p);
            float yaw = Random.Range(0f, 360f);
            if (maisPerto >= 0)
            {
                Vector3 de = Jogadores[maisPerto].pes;
                Vector3 fora = p - de;
                fora.y = 0f;
                if (fora.sqrMagnitude < longeAoSair * longeAoSair)
                {
                    fora = fora.sqrMagnitude > 0.01f ? fora.normalized : Vector3.forward;
                    if (NavMesh.SamplePosition(de + fora * (longeAoSair + 1f), out var hit, 2f, NavMesh.AllAreas) && LugarDeSair(hit.position)) p = hit.position;
                }
                // alguém ainda em cima do ponto (a beira do NavMesh, dois jogadores dos dois lados): espera um pouco (até 3 s)
                for (int k = 0; k < QuantosJogadores; k++)
                {
                    Vector3 d = Jogadores[k].pes - p;
                    d.y = 0f;
                    if (d.sqrMagnitude >= longeAoSair * longeAoSair || _adiamentos >= 10) continue;
                    _adiamentos++;
                    _temEnterrado = true;
                    _acordaEm = agora + 0.3f;
                    return;
                }
                Vector3 costas = p - Jogadores[maisPerto].pes;   // a frente dele aponta para longe de quem chegou: as costas (o olho), para ele
                costas.y = 0f;
                if (costas.sqrMagnitude > 0.01f) yaw = Mathf.Atan2(costas.x, costas.z) * Mathf.Rad2Deg;
            }
            _saiuDaTerra = NascerEspecial(TipoDeZumbi.Brutamonte, p, yaw);
            _saiuDaTerraEm = agora;
            // o próximo sorteio conta de quando ele saiu (não de quando foi enterrado)
            _proximoBrutamonte = agora + ajustes.brutamonte.intervaloDoNascimento;
        }

        int MaisPertoDe(Vector3 p)
        {
            int maisPerto = -1;
            float menor = float.MaxValue;
            for (int k = 0; k < QuantosJogadores; k++)
            {
                float d = (Jogadores[k].pes - p).sqrMagnitude;
                if (d < menor)
                {
                    menor = d;
                    maisPerto = k;
                }
            }
            return maisPerto;
        }

        /// <summary>Ao sair da partida (ZombieManager.OnNetworkDespawn): a cena continua, a próxima partida começa sem ele.</summary>
        void EsquecerOEnterrado()
        {
            _temEnterrado = false;
            _acordaEm = -1f;
            _saiuDaTerra = null;
            _adiamentos = 0;
        }

        /// <summary>Em cada máquina: o aviso de que ele vai sair (o ronco grave, até 45 m, e a terra levantando poeira).</summary>
        [Rpc(SendTo.ClientsAndHost)]
        void TerraRoncandoRpc(Vector3 ponto, float duracao)
        {
            var som = SomEspecial("Brutamonte_Terra");
            if (som != null && PodeOuvir(ponto, 45f)) Efeitos.Som(som, ponto, 0.85f * VolumeDosZumbis, true, 45f);
            StartCoroutine(PoeiraDoDespertar(ponto, duracao));
        }

        System.Collections.IEnumerator PoeiraDoDespertar(Vector3 p, float duracao)
        {
            int n = Mathf.Max(4, Mathf.RoundToInt(duracao / 0.12f));
            for (int i = 0; i < n; i++)
            {
                // a terra se mexendo num círculo que fecha onde ele vai sair
                Vector2 r = Random.insideUnitCircle * Mathf.Lerp(1.8f, 0.6f, i / (float)n);
                Efeitos.Impacto(p + new Vector3(r.x, 0.05f, r.y), Vector3.up);
                yield return new WaitForSeconds(0.12f);
            }
        }

        // ------------------------------------------------------------------ o F1

        /// <summary>No anfitrião (o F1): enterra o Brutamonte a 15–18 m à frente de quem pediu (sem outro vivo).</summary>
        public string EnterrarBrutamonteNaFrente(JogadorRede quem)
        {
            if (!IsServer || quem == null) return null;
            for (int i = 0; i < _ativos.Count; i++)
                if (!_ativos[i].Morto && _ativos[i].tipo == TipoDeZumbi.Brutamonte) return "Já há um Brutamonte de pé";
            int qual = -1;
            for (int k = 0; k < QuantosJogadores; k++)
                if (Jogadores[k].jogador == quem) qual = k;
            if (qual < 0) return null;
            if (!PlantarBrutamonte(Time.time, qual, new Vector2(15f, 18f))) return "Sem lugar ao ar livre à frente (olhe para a rua)";
            return $"Brutamonte enterrado a {Vector3.Distance(quem.transform.position, _enterrado):0} m à frente: chegue a {raioDoDespertar:0} m dele";
        }

        /// <summary>O F1: onde está o enterrado (a distância de quem olha).</summary>
        public string ResumoDoEnterrado(Vector3 de) =>
            !_temEnterrado ? "" : $"\nBrutamonte enterrado a {Vector3.Distance(de, _enterrado):0} m (acorda a {raioDoDespertar:0} m{(_acordaEm >= 0f ? ": acordando" : "")})";
    }
}
