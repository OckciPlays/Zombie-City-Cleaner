using UnityEngine;
using UnityEngine.AI;

namespace ZombieCityCleaner.Zombies
{
    /// <summary>
    /// A luz (a pedido em 28/10/2026): a lanterna e o farol do carro não petrificam (só o sol), mas os Ocos fogem do facho.
    /// De longe, a lanterna chama (à noite, quem a carrega é visto de longe: Perceber). No miolo do facho (14 m, 15° para
    /// cada lado), ele se encolhe e pula de lado para fora dele (<see cref="Estado.Esquivando"/>), mais rápido do que anda,
    /// e não ataca enquanto pula. Quem persegue avança um pouco no pulo e volta pelo escuro, pelo lado: o lugar dele no
    /// cerco gira para o lado do pulo. Quem não persegue recua e fica olhando para a luz. O farol aceso o tira da frente do
    /// carro (do caminho, não do facho inteiro). De perto (no alcance do golpe; o carro a menos de 3 m), a luz já não o
    /// afasta. De dia, a luz só conta sob um teto. Sem dano: só afasta.
    /// </summary>
    public partial class Zumbi
    {
        const float MaiorEsquiva = 4.5f;
        const float CaminhoDoCarro = 2.5f;   // de lado, até aqui do meio da frente do carro, ele está no caminho

        float _proximaEsquiva, _esquivaAte;
        Estado _antesDaEsquiva;
        Vector3 _deOndeVeioALuz;

        void ZerarLuz()
        {
            _proximaEsquiva = 0f;
            _esquivaAte = 0f;
        }

        /// <summary>
        /// Uma decisão, antes de todas: true enquanto ele pula para fora da luz (não pensa em mais nada); senão, se a luz bate
        /// nele agora, começa o pulo.
        /// </summary>
        bool Luz(float agora, AjustesDeZumbi a)
        {
            if (Atual == Estado.Esquivando)
            {
                if (agora < _esquivaAte && !Chegou()) return true;
                FimDaEsquiva(agora);
                return true;
            }
            // fugindo do sol, o desespero vence a luz
            if (agora < _proximaEsquiva || Atual == Estado.Fugindo || _m.Fachos.Count == 0) return false;
            if (!_m.NoFacho(transform.position + Vector3.up * 1.2f, out var f)) return false;

            Vector3 frente = f.direcao;
            frente.y = 0f;
            if (frente.sqrMagnitude < 0.01f) return false;
            frente.Normalize();
            Vector3 lado = Vector3.Cross(Vector3.up, frente);
            Vector3 para = transform.position - f.origem;
            para.y = 0f;
            float aoLongo = Vector3.Dot(para, frente), deLado = Vector3.Dot(para, lado);
            // de perto, a luz não o afasta mais (já está em cima de quem segura a lanterna; o carro já vai bater)
            if (aoLongo < (f.carro ? 3f : a.alcanceDoAtaque + 0.3f)) return false;
            if (f.carro && Mathf.Abs(deLado) > CaminhoDoCarro) return false;   // fora do caminho do carro

            // quanto pular: até sair do facho (o da lanterna; o do carro, até sair do caminho), com uma folga
            float precisa = f.carro ? CaminhoDoCarro + 0.8f : aoLongo * f.tanMeio + 0.8f;
            float quanto = Mathf.Clamp(precisa - Mathf.Abs(deLado), a.distanciaDaEsquiva, MaiorEsquiva);
            float sinal = Mathf.Abs(deLado) > 0.15f ? Mathf.Sign(deLado) : (Random.value < 0.5f ? -1f : 1f);
            bool persegue = _alvo != null && (Atual == Estado.Perseguindo || Atual == Estado.Atacando);
            // quem persegue avança um pouco (vai pelo lado); quem não persegue recua; do carro, só de lado
            float frenteNoPulo = f.carro ? 0f : persegue ? -0.6f : 1f;
            Vector3 aqui = transform.position;
            if (!PontoDaEsquiva(aqui + lado * (sinal * quanto) + frente * frenteNoPulo, out var ponto))
            {
                sinal = -sinal;
                if (f.carro) quanto = Mathf.Clamp(precisa + Mathf.Abs(deLado), a.distanciaDaEsquiva, MaiorEsquiva + 1f);
                if (!PontoDaEsquiva(aqui + lado * (sinal * quanto) + frente * frenteNoPulo, out ponto))
                {
                    _proximaEsquiva = agora + a.intervaloDaEsquiva;   // sem para onde pular (um corredor): fica
                    return false;
                }
            }

            _antesDaEsquiva = Atual;
            _deOndeVeioALuz = f.origem;
            Atual = Estado.Esquivando;
            _golpeEm = 0f;   // o golpe que ia sair não sai: ele se encolhe
            _noAlcanceDesde = -1f;
            Ir(ponto, a.velocidadeDaEsquiva, 0.2f);
            float vel = Mathf.Max(0.5f, a.velocidadeDaEsquiva * FatorDasPernas * FatorDaPedra);
            _esquivaAte = agora + Vector3.Distance(aqui, ponto) / vel + 0.4f;
            _proximaEsquiva = agora + a.intervaloDaEsquiva;
            Reacoes++;
            MostrarReacao();

            // a luz de quem ele persegue: o lugar dele no cerco gira para o lado do pulo (volta pelo escuro, pelo lado)
            if (persegue && f.dono == _alvo && _alvo != null)
            {
                Vector3 c = _alvo.transform.position;
                float rumoDoPonto = Mathf.Atan2(ponto.x - c.x, ponto.z - c.z) * Mathf.Rad2Deg;
                float rumoDaLuz = Mathf.Atan2(frente.x, frente.z) * Mathf.Rad2Deg;
                float afasta = Mathf.Sign(Mathf.DeltaAngle(rumoDaLuz, rumoDoPonto));
                DefinirCerco((rumoDoPonto + afasta * 25f) * Mathf.Deg2Rad);
            }
            return true;
        }

        /// <summary>Um ponto do NavMesh perto do pulo, sem parede no caminho (a parede encurta o pulo), a 1 m ou mais.</summary>
        bool PontoDaEsquiva(Vector3 destino, out Vector3 ponto)
        {
            ponto = default;
            if (!NavMesh.SamplePosition(destino, out var hit, 1.2f, NavMesh.AllAreas)) return false;
            ponto = hit.position;
            if (Mathf.Abs(ponto.y - transform.position.y) > 1.2f) return false;
            if (NavMesh.Raycast(transform.position, ponto, out var bate, NavMesh.AllAreas)) ponto = bate.position;
            Vector3 d = ponto - transform.position;
            d.y = 0f;
            return d.sqrMagnitude >= 1f;
        }

        // o pulo acabou: quem perseguia volta a perseguir (pelo lado); quem ia para o abrigo, vai de novo; os outros ficam
        // ali olhando para a luz
        void FimDaEsquiva(float agora)
        {
            var antes = _antesDaEsquiva;
            if ((antes == Estado.Perseguindo || antes == Estado.Atacando) && _alvo != null && _alvo.IsSpawned)
            {
                Atual = Estado.Perseguindo;
                _proximoCaminho = 0f;
                return;
            }
            _alvo = null;
            _casa = transform.position;
            Pausar(agora);
            if (antes == Estado.Abrigando)
            {
                ComecarAbrigo(_m.ajustes);
                if (Atual == Estado.Abrigando) return;
            }
            _fimDaPausa = agora + Random.Range(1.5f, 3f);
            OlharPara(_deOndeVeioALuz);
        }
    }
}
