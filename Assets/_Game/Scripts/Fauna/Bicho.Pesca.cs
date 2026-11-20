using UnityEngine;
using ZombieCityCleaner.World;

namespace ZombieCityCleaner.Fauna
{
    /// <summary>
    /// O urso pescando o salmão (a etapa 3, 12/11/2026), no anfitrião. De dia, calmo e com um cardume a até 70 m, às vezes
    /// vai pescar: entra no raso (até ~0,9 m) perto do cardume, espera parado de cabeça baixa e, de 4 em 4 a 9 em 9 s, dá a
    /// patada na água (o respingo); com um salmão a até 2,8 m, pega em 40%. Pegou: leva na boca até a margem e come
    /// (de 15 a 30 s); o salmão some. A gente, o perigo e o tiro interrompem (a mente do urso, Bicho.Urso.cs).
    /// Desde a etapa 5 (13/11/2026, a pedido: "toda vez que um urso ver um salmão, vai caçar o salmão"), o urso olha a água
    /// de tempo em tempo e, vendo um salmão a até 35 m, vai pescar, de dia ou de noite; pegou, come sentado na margem, e os
    /// filhotes sentam do lado e comem junto. De barriga cheia, fica de 2 a 4 minutos sem pescar.
    /// Desde 14/11/2026 (a pedido: "sem filhotes ele come. Com filhotes ele dá a comida"): sozinho, o urso deita de barriga
    /// na margem e come com o peixe entre as patas; a mãe com filhotes perto larga o salmão no chão na frente deles e deita
    /// do lado, olhando, e os filhotes deitam em volta do peixe e comem (se nenhum chegar em 25 s, ela mesma come).
    /// </summary>
    public sealed partial class Bicho
    {
        bool _pescando, _temPontoDePesca, _temMargem, _largouOPeixe;
        Bicho _salmao, _peixeNaBoca, _levadoPor;
        float _pescaAte, _proximaPesca, _proximaTentativa, _trocarPonto, _comerAte, _patadaNaAguaAte, _chegarAte, _filhoteDesde, _esperarFilhotesAte;
        Vector3 _pontoDePesca, _margem, _ondeLargou;

        /// <summary>(14/11) O salmão na boca (ou nas patas) de um urso: não cai como os outros corpos.</summary>
        internal bool Levado => _levadoPor != null && _levadoPor._peixeNaBoca == this;

        /// <summary>(14/11) O salmão que a mãe largou para os filhotes (null: não largou).</summary>
        internal Bicho PeixeDosFilhotes => _largouOPeixe ? _peixeNaBoca : null;

        /// <summary>(14/11) Um filhote chegou no peixe: começa a contar o tempo de comer.</summary>
        internal void FilhoteChegou(float agora)
        {
            if (_filhoteDesde < 0f) _filhoteDesde = agora;
        }

        bool FilhotesPerto(float raio)
        {
            if (Grupo == null) return false;
            foreach (var b in Grupo.membros)
                if (b != null && b != this && !b.Morto && b.Variante == Variante.Filhote && Vector3.Distance(b.transform.position, transform.position) < raio)
                    return true;
            return false;
        }

        // o ponto do chão embaixo da boca (no modelo pronto, a boca de verdade)
        Vector3 ChaoDaBoca(float aFrente)
        {
            Vector3 b = Modelo != null && Modelo.boca != null ? Modelo.boca.position : transform.position + transform.forward * Dados.comprimento * Escala * 0.45f;
            Vector3 q = b + transform.forward * aFrente;
            q.y = Chao(q, out _) + 0.02f;
            return q;
        }

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

        /// <summary>
        /// (14/11) O F1: pega agora o salmão mais perto (a até 60 m) e leva para comer na margem (sozinho, come deitado;
        /// com filhotes, larga o peixe para eles). Sem notar ninguém por 90 s, como na pesca do F1.
        /// </summary>
        internal bool PegarSalmaoAgora(FaunaDoMundo f)
        {
            if (Variante == Variante.Filhote) return false;
            if (_peixeNaBoca != null) return true;
            var s = f.SalmaoPerto(transform.position, 60f);
            if (s == null) return false;
            f.Matar(s, null, false);
            _peixeNaBoca = s;
            s._levadoPor = this;
            _pescando = true;
            _pescaAte = Time.time + 120f;
            _temMargem = false;
            _largouOPeixe = false;
            _comerAte = 0f;
            Distrair(Time.time + 90f);
            return true;
        }

        /// <summary>No ramo calmo do urso: pescar (ou começar a pescar). Falso: não está pescando.</summary>
        bool Pescar(FaunaDoMundo f, float agora)
        {
            if (Variante == Variante.Filhote) return false;
            if (!_pescando)
            {
                if (agora < _proximaPesca) return false;
                _proximaPesca = agora + Random.Range(3f, 6f);   // (5a) olha a água de tempo em tempo
                var s = f.SalmaoPerto(transform.position, 35f);   // viu o salmão: vai caçar
                if (s == null) return false;
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
                if (!_largouOPeixe && _comerAte <= 0f && _temMargem && Vector3.Distance(Plano(p), Plano(_margem)) > 1.5f)
                {
                    Estado = EstadoDoBicho.Andando;
                    Ir(_margem, Dados.andar * 1.5f);
                    return true;
                }
                _temDestino = false;
                _velAlvo = 0f;
                _podeMolhar = false;
                // (14/11) com filhotes perto, a mãe larga o peixe na frente deles e deita do lado, olhando (os filhotes vêm
                // comer: PensarFilhoteDeUrso); sozinho, ou se nenhum filhote chegou em 25 s, deita e come entre as patas
                if (!_largouOPeixe && _comerAte <= 0f && FilhotesPerto(40f))
                {
                    _largouOPeixe = true;
                    _filhoteDesde = -1f;
                    _esperarFilhotesAte = agora + 25f;
                    _ondeLargou = ChaoDaBoca(0.3f);
                }
                if (_largouOPeixe && ((_filhoteDesde < 0f && agora > _esperarFilhotesAte) || !FilhotesPerto(40f))) _largouOPeixe = false;
                if (_largouOPeixe)
                {
                    Estado = EstadoDoBicho.Deitado;
                    Olhar(_ondeLargou);
                    if (_comerAte <= 0f && _filhoteDesde >= 0f) _comerAte = agora + Random.Range(25f, 40f);
                }
                else
                {
                    Estado = EstadoDoBicho.DeitadoComendo;
                    if (_comerAte <= 0f) _comerAte = agora + Random.Range(18f, 32f);
                    Mastigar(agora);   // (19/11/2026: o urso mastigando, Bicho.SomGravado.cs)
                }
                if (_comerAte > 0f && agora > _comerAte)
                {
                    f.Sumir(_peixeNaBoca);
                    _peixeNaBoca = null;
                    _comerAte = 0f;
                    _largouOPeixe = false;
                    PararDePescar(agora);
                    Estado = EstadoDoBicho.Andando;
                    _proximaPesca = agora + Random.Range(120f, 240f);   // barriga cheia
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
                bool novo = !_temPontoDePesca;
                _temPontoDePesca = LugarDePescar(p, alvo, out _pontoDePesca);
                if (!_temPontoDePesca) { PararDePescar(agora); return false; }
                if (novo) _chegarAte = agora + 25f;
            }
            if (agora < _patadaNaAguaAte)
            {
                Estado = EstadoDoBicho.Investindo;   // (a pose da patada)
                _temDestino = false;
                _velAlvo = 0f;
                return true;
            }
            // (13/11) não chegou em 25 s (a margem funda): pesca de onde está, se a água estiver ao alcance da pata; senão, desiste
            if (agora > _chegarAte && Vector3.Distance(Plano(p), Plano(_pontoDePesca)) > 1.2f)
            {
                if (FaunaDoMundo.FunduraEm(p + transform.forward * 1.2f) > 0.15f || FaunaDoMundo.FunduraEm(p) > 0.15f) { _pontoDePesca = p; _trocarPonto = agora + 30f; }
                else { PararDePescar(agora); return false; }
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
                // (13/11) a patada pega o salmão a até 3,5 m (45%); o cardume a até 8 m, às vezes um passa perto (12%): no
                // reservatório de margem funda o cardume fica longe do raso, e o urso passava minutos sem pegar nada
                var perto = f.SalmaoPerto(p + transform.forward * 1f, 3.5f);
                bool pegou = perto != null && Random.value < 0.45f;
                if (!pegou)
                {
                    perto = f.SalmaoPerto(p, 8f);
                    pegou = perto != null && Random.value < 0.12f;
                }
                if (pegou)
                {
                    f.Matar(perto, null, false);
                    _peixeNaBoca = perto;
                    perto._levadoPor = this;
                    _temMargem = false;
                    _largouOPeixe = false;
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
            _largouOPeixe = false;
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
                if (f >= 0.3f && f <= 1.1f) { p = c; achou = true; }
            }
            if (achou) return true;
            // em volta do cardume
            for (int a = 0; a < 12; a++)
                for (float r = 1.5f; r <= 8f; r += 1.5f)
                {
                    Vector3 c = cardume + Quaternion.Euler(0f, a * 30f, 0f) * Vector3.forward * r;
                    float f = FaunaDoMundo.FunduraEm(c);
                    if (f >= 0.3f && f <= 1.1f) { p = c; return true; }
                }
            return false;
        }

        // o salmão na boca: segue a cabeça do urso (as cópias recebem a posição dele pela rede); (14/11) largado para os
        // filhotes, fica no chão onde a mãe largou; comendo deitado, no chão embaixo da boca (entre as patas)
        void Levar(Bicho peixe)
        {
            if (peixe == null) return;
            if (_largouOPeixe)
            {
                peixe.transform.SetPositionAndRotation(_ondeLargou, Quaternion.LookRotation(transform.right));
                return;
            }
            if (Estado == EstadoDoBicho.DeitadoComendo)
            {
                peixe.transform.SetPositionAndRotation(ChaoDaBoca(0.05f), Quaternion.LookRotation(transform.right));
                return;
            }
            // (14/11) nos modelos prontos, a boca de verdade; nas caixas, um pouco à frente e abaixo da cabeça
            Vector3 boca = Modelo != null && Modelo.boca != null ? Modelo.boca.position
                : (Modelo != null && Modelo.cabeca != null ? Modelo.cabeca.position : transform.position + transform.forward * Dados.comprimento * Escala * 0.5f + Vector3.up * Altura)
                  + transform.forward * 0.25f - Vector3.up * 0.1f;
            peixe.transform.SetPositionAndRotation(boca, Quaternion.LookRotation(transform.right));
        }
    }
}
