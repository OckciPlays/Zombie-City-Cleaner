using UnityEngine;
using ZombieCityCleaner.World;

namespace ZombieCityCleaner.Fauna
{
    /// <summary>
    /// O urso pescando o salmão (a etapa 3, 12/11/2026), no anfitrião. De dia, calmo e com um cardume a até 70 m, às vezes
    /// vai pescar: entra no raso (até ~0,9 m) perto do cardume, espera parado de cabeça baixa e, de 4 em 4 a 9 em 9 s, dá a
    /// patada na água (o respingo); com um salmão a até 2,8 m, pega em 40%. Pegou: leva na boca até a margem e come
    /// (de 15 a 30 s); o salmão some. A gente, o perigo e o tiro interrompem (a mente do urso, Bicho.Urso.cs).
    /// </summary>
    public sealed partial class Bicho
    {
        bool _pescando, _temPontoDePesca, _temMargem;
        Bicho _salmao, _peixeNaBoca;
        float _pescaAte, _proximaPesca, _proximaTentativa, _trocarPonto, _comerAte, _patadaNaAguaAte;
        Vector3 _pontoDePesca, _margem;

        /// <summary>O F1: este urso vai pescar agora (o cardume mais perto).</summary>
        internal void PescarAgora(FaunaDoMundo f)
        {
            _proximaPesca = 0f;
            _salmao = f.SalmaoPerto(transform.position, 150f);
            if (_salmao == null) return;
            _pescando = true;
            _pescaAte = Time.time + 120f;
            _temPontoDePesca = false;
            _proximaTentativa = Time.time + 3f;
        }

        /// <summary>No ramo calmo do urso: pescar (ou começar a pescar). Falso: não está pescando.</summary>
        bool Pescar(FaunaDoMundo f, float agora)
        {
            if (Variante == Variante.Filhote) return false;
            if (!_pescando)
            {
                if (agora < _proximaPesca || CicloDoDia.Noite > 0.5f) return false;
                _proximaPesca = agora + Random.Range(20f, 60f);
                var s = f.SalmaoPerto(transform.position, 70f);
                if (s == null || Random.value > 0.6f) return false;
                _salmao = s;
                _pescando = true;
                _pescaAte = agora + Random.Range(45f, 100f);
                _temPontoDePesca = false;
                _proximaTentativa = agora + Random.Range(3f, 6f);
            }
            Vector3 p = transform.position;
            if (_peixeNaBoca != null)
            {
                // pegou: leva até a margem e come
                if (!_temMargem) _temMargem = FaunaDoMundo.PontoNaMargem(p, 40f, 1f, 5f, out _margem);
                if (_temMargem && Vector3.Distance(Plano(p), Plano(_margem)) > 1.5f)
                {
                    Estado = EstadoDoBicho.Andando;
                    Ir(_margem, Dados.andar * 1.5f);
                    return true;
                }
                Estado = EstadoDoBicho.Pastando;
                _temDestino = false;
                _velAlvo = 0f;
                _podeMolhar = false;
                if (_comerAte <= 0f) _comerAte = agora + Random.Range(15f, 30f);
                if (agora > _comerAte)
                {
                    f.Sumir(_peixeNaBoca);
                    _peixeNaBoca = null;
                    _comerAte = 0f;
                    PararDePescar(agora);
                }
                return true;
            }
            if (agora > _pescaAte || _salmao == null || _salmao.Morto)
            {
                _salmao = f.SalmaoPerto(p, 60f);
                if (_salmao == null || agora > _pescaAte) { PararDePescar(agora); return false; }
            }
            _podeMolhar = true;
            Vector3 alvo = _salmao.transform.position;
            // o lugar de esperar: no raso (até 0,9 m), o mais perto do cardume; troca de tempo em tempo (o cardume anda)
            if (!_temPontoDePesca || agora > _trocarPonto)
            {
                _trocarPonto = agora + Random.Range(8f, 14f);
                _temPontoDePesca = LugarDePescar(p, alvo, out _pontoDePesca);
                if (!_temPontoDePesca) { PararDePescar(agora); return false; }
            }
            if (agora < _patadaNaAguaAte)
            {
                Estado = EstadoDoBicho.Investindo;   // (a pose da patada)
                _temDestino = false;
                _velAlvo = 0f;
                return true;
            }
            if (Vector3.Distance(Plano(p), Plano(_pontoDePesca)) > 1.2f)
            {
                Estado = EstadoDoBicho.Andando;
                Ir(_pontoDePesca, Dados.andar * 1.3f);
                return true;
            }
            // esperando parado, de cabeça baixa, olhando a água
            Estado = EstadoDoBicho.Pastando;
            _temDestino = false;
            _velAlvo = 0f;
            Olhar(alvo);
            if (agora > _proximaTentativa)
            {
                _proximaTentativa = agora + Random.Range(4f, 9f);
                _patadaNaAguaAte = agora + 0.7f;
                _proximoSom = 0f;
                Tocar(SomDoBicho.Respingo);
                var perto = f.SalmaoPerto(p + transform.forward * 1f, 2.8f);
                if (perto != null && Random.value < 0.4f)
                {
                    f.Matar(perto, null, false);
                    _peixeNaBoca = perto;
                    _temMargem = false;
                }
            }
            return true;
        }

        void PararDePescar(float agora)
        {
            _pescando = false;
            _podeMolhar = false;
            _salmao = null;
            _proximaPesca = agora + Random.Range(40f, 120f);
            if (Estado == EstadoDoBicho.Investindo) Estado = EstadoDoBicho.Pastando;
        }

        /// <summary>O urso largou a pesca (a gente, o perigo): o peixe da boca cai.</summary>
        void LargarPesca()
        {
            _pescando = false;
            _podeMolhar = false;
            _peixeNaBoca = null;
            _comerAte = 0f;
        }

        // o ponto no raso (de 0,3 a 0,9 m) entre o urso e o cardume, o mais perto do cardume
        static bool LugarDePescar(Vector3 de, Vector3 cardume, out Vector3 p)
        {
            p = default;
            bool achou = false;
            for (int k = 0; k <= 12; k++)
            {
                Vector3 c = Vector3.Lerp(de, cardume, k / 12f);
                float f = FaunaDoMundo.FunduraEm(c);
                if (f >= 0.3f && f <= 0.9f) { p = c; achou = true; }
            }
            if (achou) return true;
            // em volta do cardume
            for (int a = 0; a < 12; a++)
                for (float r = 1.5f; r <= 8f; r += 1.5f)
                {
                    Vector3 c = cardume + Quaternion.Euler(0f, a * 30f, 0f) * Vector3.forward * r;
                    float f = FaunaDoMundo.FunduraEm(c);
                    if (f >= 0.3f && f <= 0.9f) { p = c; return true; }
                }
            return false;
        }

        // o salmão na boca: segue a cabeça do urso (as cópias recebem a posição dele pela rede)
        void Levar(Bicho peixe)
        {
            if (peixe == null) return;
            Vector3 boca = Modelo != null && Modelo.cabeca != null ? Modelo.cabeca.position : transform.position + transform.forward * Dados.comprimento * Escala * 0.5f + Vector3.up * Altura;
            peixe.transform.SetPositionAndRotation(boca + transform.forward * 0.25f - Vector3.up * 0.1f, Quaternion.LookRotation(transform.right));
        }
    }
}
