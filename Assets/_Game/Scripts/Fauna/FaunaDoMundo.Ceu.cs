using UnityEngine;
using ZombieCityCleaner.Player;
using ZombieCityCleaner.World;

namespace ZombieCityCleaner.Fauna
{
    /// <summary>
    /// O céu e os testes da água (a etapa 3, 12/11/2026): de tempo em tempo (de 4 a 9 minutos), de dia, um V de 7 a 11
    /// gansos passa alto (uns 55 m) por cima de um jogador, de um lado ao outro, grasnando; some longe. E o F1 da água: ir
    /// para o lago, o V agora, o urso pescando e os castores roendo depressa.
    /// </summary>
    public sealed partial class FaunaDoMundo
    {
        float _proximoV = -1f;

        void Ceu(float agora)
        {
            if (_proximoV < 0f) _proximoV = agora + Random.Range(240f, 540f);
            if (agora < _proximoV) return;
            _proximoV = agora + Random.Range(240f, 540f);
            if (CicloDoDia.Noite > 0.5f || Pessoas.Count == 0 || Random.value > 0.6f) return;
            var g = Pessoas[Random.Range(0, Pessoas.Count)];
            if (!g.valido) return;
            PassarV(g.pes);
        }

        /// <summary>Um V de gansos passa por cima do ponto (de um lado ao outro, a ~55 m de altura).</summary>
        int PassarV(Vector3 sobre)
        {
            Vector3 rumo = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward;
            Vector3 lado = Vector3.Cross(Vector3.up, rumo);
            Vector3 comeco = sobre - rumo * 230f, fim = sobre + rumo * 600f;
            int n = Random.Range(7, 12);
            var grupo = new Grupo { especie = Especie.Ganso, pasto = comeco };
            Grupos.Add(grupo);
            int nasceram = 0;
            for (int i = 0; i < n; i++)
            {
                // o V: o da frente no bico, os outros atrás, alternando os lados
                int fila = (i + 1) / 2;
                float s = i == 0 ? 0f : (i % 2 == 0 ? 1f : -1f);
                Vector3 off = -rumo * fila * 2.6f + lado * s * fila * 2.2f;
                var b = Nascer(Especie.Ganso, Variante.Adulto, comeco + off, Quaternion.LookRotation(rumo).eulerAngles.y, grupo);
                if (b == null) continue;
                b.LugarNoGrupo = off;
                b.Passar(fim + off, 55f);
                if (i == 0) grupo.lider = b;
                nasceram++;
            }
            if (nasceram == 0) Grupos.Remove(grupo);
            return nasceram;
        }

        // ------------------------------------------------------------------ o F1 da água

        /// <summary>O F1: o jogador vai para a margem do lago mais perto (o anfitrião manda a posição ao dono).</summary>
        public bool LugarNoLago(JogadorRede j, out Vector3 margem, out float giro)
        {
            margem = default;
            giro = 0f;
            var d = DadosDoMapa;
            if (j == null || d?.Lago == null) return false;
            var lago = d.Lago;
            Vector3 p = j.transform.position;
            Vector2 para = new Vector2(p.x - lago.Centro.x, p.z - lago.Centro.y);
            if (para.sqrMagnitude < 1f) para = Vector2.right;
            para.Normalize();
            float r = lago.RaioNaDirecao(para.x, para.y);
            for (float fora = 4f; fora < 40f; fora += 2f)
            {
                var c = new Vector3(lago.Centro.x + para.x * (r + fora), 0f, lago.Centro.y + para.y * (r + fora));
                if (d.FunduraDaAgua(c.x, c.z) > 0.01f) continue;
                c.y = d.Altura(c.x, c.z);
                margem = c;
                giro = Quaternion.LookRotation(new Vector3(-para.x, 0f, -para.y)).eulerAngles.y;
                return true;
            }
            return false;
        }

        /// <summary>O F1: um V de gansos passa agora por cima do jogador.</summary>
        public string VDeTeste(JogadorRede j)
        {
            if (!_servidor || j == null) return "Só o anfitrião.";
            int n = PassarV(j.transform.position);
            return n > 0 ? $"{n} gansos em V passando por cima (uns 55 m de altura)." : "Não deu para pôr.";
        }

        /// <summary>O F1: um cardume de salmões no raso e um urso-pardo na margem, que vai pescar.</summary>
        public string PescaDeTeste(JogadorRede j)
        {
            if (!_servidor || j == null) return "Só o anfitrião.";
            if (!PontoNaAgua(j.transform.position, 8f, 60f, 0.6f, 1.2f, out var raso))
                return "O lago está longe: use \"Ir para o lago\" antes.";
            int peixes = NascerGrupo(Especie.Salmao, raso, true);
            if (!PontoNaMargem(raso, 30f, 2f, 8f, out var margem)) margem = j.transform.position;
            int ursos = NascerGrupo(Especie.UrsoPardo, margem, true);
            if (peixes == 0 || ursos == 0) return "Não deu para pôr.";
            var g = Grupos[Grupos.Count - 1];
            foreach (var b in g.membros)
                if (b != null && b.Variante != Variante.Filhote)
                {
                    b.Distrair(Time.time + 120f);
                    b.PescarAgora(this);
                }
            return $"{peixes} salmões no raso e um urso-pardo indo pescar (sem notar você por 2 min).";
        }
    }
}
