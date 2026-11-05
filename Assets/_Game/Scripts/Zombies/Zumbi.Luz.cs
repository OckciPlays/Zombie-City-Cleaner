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
    ///
    /// O fogo (05/11/2026, a pedido: "fazer os zumbis ficarem longe da luz da tocha e da fogueira"): eles não entram no raio
    /// do fogo (a tocha acesa na mão, 4 m; a fogueira acesa, 8 m: <see cref="AjustesDeZumbi.raioDaTocha"/>). Dentro, recuam
    /// até a beira (o susto só na primeira vez). Quem caça alguém que está no fogo rodeia a beira olhando para ele, até o
    /// fogo apagar (a chuva, a gosma do Long, a lenha): aí ataca na hora. Com 5 ou mais rodeando o mesmo fogo por 30 s, a
    /// horda cria coragem e entra (por 45 s). O ferido (tiro, golpe: <see cref="Ferido"/>) entra mesmo assim, como no sol.
    /// Quem caça alguém fora do fogo dá a volta por fora; quem vaga não escolhe ponto no fogo (o NoFacho do vagar); quem vai
    /// ver um barulho dentro dele para na beira. Na beira, ele ainda bate em quem está ao alcance do golpe. O Long não liga
    /// (ele cospe no fogo e o apaga) e o Brutamonte nem chega aqui. Sem lugar na beira por uma parede (um corredor estreito),
    /// ele age como sempre: encurralado, ataca; pelo sol do dia, fica parado olhando o fogo. De onde o fogo não se vê (atrás
    /// da parede da casa com a fogueira dentro), ele não vai: espera no vão da porta. As desistências do Perseguir (o alvo no
    /// sol, na zona segura) valem antes do fogo.
    /// </summary>
    public partial class Zumbi
    {
        const float MaiorEsquiva = 4.5f;
        const float CaminhoDoCarro = 2.5f;   // de lado, até aqui do meio da frente do carro, ele está no caminho

        float _proximaEsquiva, _esquivaAte;
        Estado _antesDaEsquiva;
        Vector3 _deOndeVeioALuz;

        const float BeiraDoFogo = 1.5f;     // a faixa em volta do raio em que ele rodeia (e dá a volta)
        const float FolgaNaBeira = 0.5f;    // o lugar dele na beira: o raio mais isto
        float _valenteAte, _naBeiraAte;
        int _chaveDaEspera = ZombieManager.SemEspera;

        void ZerarLuz()
        {
            _proximaEsquiva = 0f;
            _esquivaAte = 0f;
            _valenteAte = 0f;
            _naBeiraAte = 0f;
            _chaveDaEspera = ZombieManager.SemEspera;
        }

        /// <summary>A horda que esperava na beira do fogo criou coragem: ele não liga para o fogo até lá (o F1).</summary>
        internal bool Valente => Time.time < _valenteAte;

        /// <summary>
        /// Uma decisão, antes de todas: true enquanto ele pula para fora da luz (não pensa em mais nada); senão, se a luz bate
        /// nele agora, começa o pulo.
        /// </summary>
        bool Luz(float agora, AjustesDeZumbi a)
        {
            if (ImuneALuz) return false;   // o Brutamonte não liga para a luz
            if (Atual == Estado.Esquivando)
            {
                if (agora < _esquivaAte && !Chegou()) return true;
                FimDaEsquiva(agora);
                return true;
            }
            // fugindo do sol, o desespero vence a luz
            if (Atual != Estado.Fugindo && Fogo(agora, a)) return true;
            if (agora < _proximaEsquiva || Atual == Estado.Fugindo || _m.Fachos.Count == 0) return false;
            if (!_m.NoFacho(transform.position + Vector3.up * 1.2f, out var f, false)) return false;
            // o Long, de perto, não foge da lanterna: ela o petrifica (Zumbi.Long.cs)
            if (tipo == TipoDeZumbi.Long && !f.carro && (transform.position - f.origem).magnitude < A.cuspidor.alcanceDaLanterna + 1f) return false;

            Vector3 frente = f.direcao;
            frente.y = 0f;
            if (frente.sqrMagnitude < 0.01f) return false;
            frente.Normalize();
            Vector3 lado = Vector3.Cross(Vector3.up, frente);
            Vector3 para = transform.position - f.origem;
            para.y = 0f;
            float aoLongo = Vector3.Dot(para, frente), deLado = Vector3.Dot(para, lado);
            // de perto, a luz não o afasta mais (já está em cima de quem segura a lanterna; o carro já vai bater); o Corredor
            // no bote (a menos de 4 m) também não desvia: ele vem bater (02/11/2026)
            float naoDesvia = f.carro ? 3f : tipo == TipoDeZumbi.Corredor && _alvo != null ? a.corredor.distanciaDoBote : a.alcanceDoAtaque + 0.3f;
            if (aoLongo < naoDesvia) return false;
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

        // ------------------------------------------------------------------------------------------ o fogo

        /// <summary>
        /// O fogo (a tocha, a fogueira), antes dos fachos: true quando ele recua, rodeia a beira ou para nela (não pensa em
        /// mais nada); false para seguir como sempre.
        /// </summary>
        bool Fogo(float agora, AjustesDeZumbi a)
        {
            if (_m.Fogos.Count == 0 || tipo == TipoDeZumbi.Long || Ferido || agora < _valenteAte) return false;
            Vector3 aqui = transform.position;
            if (!_m.FogoPerto(aqui, BeiraDoFogo, out var fogo)) return false;
            Vector3 deFora = aqui - fogo.centro;
            deFora.y = 0f;
            float d = deFora.magnitude;
            bool caca = _alvo != null && _alvo.AlvoValido && (Atual == Estado.Perseguindo || Atual == Estado.Atacando);
            // dentro do fogo: recua até a beira (o susto só na primeira vez; quem já rodeava só recua)
            if (d < fogo.raio - 0.2f)
            {
                if (!RecuarDoFogo(agora, a, fogo, deFora, d, agora >= _naBeiraAte, out bool preso)) return false;
                // preso entre o sol e o fogo, ele larga quem caçava (não fica engajado o dia todo sem poder ir atrás)
                if (preso && caca)
                {
                    _alvo = null;
                    Atual = Estado.Parado;
                    _fimDaPausa = agora + 3f;
                }
                return true;
            }
            if (caca)
            {
                Vector3 alvo = _alvo.transform.position;
                Vector3 ate = alvo - aqui;
                float altura = Mathf.Abs(ate.y);
                ate.y = 0f;
                // as desistências do Perseguir valem antes do fogo: longe demais, o alvo no acampamento (a zona segura) ou
                // no sol
                if (ate.magnitude > a.distanciaDeDesistir || DesisteDoSol(alvo) || ZombieManager.NaZonaSegura(alvo)) return false;
                if (ZombieManager.DentroDoFogo(fogo, alvo))
                {
                    // na beira, quem está ao alcance do golpe ainda leva (só não entram): ele bate dali, sem dar a volta até o
                    // lugar do cerco (que podia cair dentro do fogo), com o mesmo alcance do Atacar (para entrar, como o
                    // Perseguir; para continuar, o de sair do ataque)
                    if (tipo == TipoDeZumbi.Corredor)
                    {
                        if (ate.magnitude < a.corredor.alcanceDoGolpe + 0.1f && altura < 1.2f)
                        {
                            if (agora >= _proximoGolpe) return false;   // o golpe pronto: o bote dele, como sempre
                            // entre um golpe e outro, para na beira olhando (o bote reto o levava para dentro do fogo)
                            if (NoNavMesh) agente.ResetPath();
                            OlharPara(alvo);
                            _ultimoVisto = agora;
                            _ultimaPosicao = alvo;
                            return true;
                        }
                    }
                    else if (Atual == Estado.Atacando ? ate.magnitude <= a.alcanceDoAtaque + 0.4f && altura <= 1.4f
                                                      : ate.magnitude < a.alcanceDoAtaque && altura < 1.2f)
                    {
                        if (Atual != Estado.Atacando)
                        {
                            Atual = Estado.Atacando;
                            agente.ResetPath();
                            agente.velocity = Vector3.zero;
                            _proximoGolpe = Mathf.Max(_proximoGolpe, agora + 0.25f);
                        }
                        return false;
                    }
                    // a horda que espera ali criou coragem: entra (os fogos que se tocam contam como um só; quem muda de
                    // grupo, a tocha encostando na fogueira ou saindo dela, leva o tempo de espera junto)
                    int chave = _m.ChaveDoGrupo(fogo);
                    bool coragem = _m.EsperarNoFogo(chave, this, agora, _chaveDaEspera);
                    _chaveDaEspera = chave;
                    if (coragem)
                    {
                        _valenteAte = agora + a.tempoDeCoragem;
                        return false;
                    }
                    Rondar(agora, a, fogo, _temCerco ? _anguloNoCerco : Mathf.Atan2(deFora.x, deFora.z), alvo, true);
                    return true;
                }
                // quem está fora do fogo: se o caminho reto corta o fogo, ele dá a volta pela beira; sem um passo (a parede, o
                // sol, outro fogo), o Perseguir acha o caminho por ele
                if (!CortaOFogo(aqui, alvo, fogo)) return false;
                return Rondar(agora, a, fogo, Mathf.Atan2(alvo.x - fogo.centro.x, alvo.z - fogo.centro.z), alvo, false);
            }
            // quem ia ver um barulho (ou vagava) para dentro do fogo para na beira e fica olhando
            if ((Atual == Estado.Investigando || Atual == Estado.Vagando) && NoNavMesh && agente.hasPath && ZombieManager.DentroDoFogo(fogo, agente.destination))
            {
                if (Atual == Estado.Investigando) Procurar(agora);
                else Pausar(agora);
                OlharPara(fogo.centro);
                _naBeiraAte = agora + 3f;
                return true;
            }
            return false;
        }

        // recua para fora do fogo, até a beira (a mesma esquiva das lanternas: no fim, quem caçava volta a caçar)
        bool RecuarDoFogo(float agora, AjustesDeZumbi a, in ZombieManager.Fogo fogo, Vector3 deFora, float d, bool susto, out bool preso)
        {
            preso = false;
            float rumo = d > 0.1f ? Mathf.Atan2(deFora.x, deFora.z) : (transform.eulerAngles.y + 180f) * Mathf.Deg2Rad;
            bool sol = false;
            if (!PontoNaBeira(fogo, rumo, out var ponto, ref sol) && !PontoNaBeira(fogo, rumo + 0.8f, out ponto, ref sol) && !PontoNaBeira(fogo, rumo - 0.8f, out ponto, ref sol))
            {
                if (!sol) return false;   // sem para onde recuar (um corredor estreito): encurralado, ele age como sempre
                // a beira está no sol (de dia, um fogo dentro de casa): entre o sol e o fogo, ele fica parado, olhando
                preso = true;
                if (NoNavMesh) agente.ResetPath();
                _golpeEm = 0f;
                _noAlcanceDesde = -1f;
                _naBeiraAte = agora + 3f;
                OlharPara(fogo.centro);
                return true;
            }
            _antesDaEsquiva = Atual;
            _deOndeVeioALuz = fogo.centro + Vector3.up * 1.2f;
            Atual = Estado.Esquivando;
            _golpeEm = 0f;   // o golpe que ia sair não sai
            _noAlcanceDesde = -1f;
            Ir(ponto, a.velocidadeDaEsquiva, 0.2f);
            float vel = Mathf.Max(0.5f, a.velocidadeDaEsquiva * FatorDasPernas * FatorDaPedra);
            _esquivaAte = agora + Vector3.Distance(transform.position, ponto) / vel + 0.4f;
            _proximaEsquiva = agora + 0.3f;
            _naBeiraAte = agora + 3f;
            if (susto)
            {
                Reacoes++;
                MostrarReacao();
            }
            return true;
        }

        // rodeia a beira do fogo até o lugar dele (o rumo, do centro do fogo), de 45° em 45° (o caminho reto para o outro
        // lado cortaria o fogo), olhando para quem caça quando chega; ele o vê (o fogo ilumina): quando o fogo apagar, ataca
        // na hora (sem isso, depois de tempoSemVer, ia investigar)
        // Devolve false só quando ele dá a volta (não espera) e não acha o passo: aí o Perseguir acha o caminho por ele.
        bool Rondar(float agora, AjustesDeZumbi a, in ZombieManager.Fogo fogo, float rumoDoLugar, Vector3 alvo, bool espera)
        {
            if (agora < _proximoCaminho)
            {
                MarcarNaBeira(agora, alvo);
                if (espera && Chegou()) OlharPara(alvo);
                return true;
            }
            Vector3 aqui = transform.position - fogo.centro;
            float rumoAqui = Mathf.Atan2(aqui.x, aqui.z);
            float passo = Mathf.Clamp(Mathf.DeltaAngle(rumoAqui * Mathf.Rad2Deg, rumoDoLugar * Mathf.Rad2Deg), -45f, 45f) * Mathf.Deg2Rad;
            bool sol = false;
            bool achou = PontoNaBeira(fogo, rumoAqui + passo, out var ponto, ref sol) || PontoNaBeira(fogo, rumoAqui + passo * 0.5f, out ponto, ref sol)
                         || (espera && PontoNaBeira(fogo, rumoAqui, out ponto, ref sol));
            // quem espera não vai para onde o fogo não se vê (atrás da parede de uma casa com a fogueira dentro): ia e voltava
            // pela porta, achando e perdendo o fogo; fica no vão, esperando. Quem dá a volta passa para trás da parede (lá o
            // fogo some e o Perseguir o leva)
            if (achou && espera && !_m.Enxerga(fogo.centro + Vector3.up * 1f, ponto + Vector3.up * 1.2f)) achou = false;
            if (!achou && !espera) return false;
            MarcarNaBeira(agora, alvo);
            _proximoCaminho = agora + 0.25f;
            if (achou)
            {
                // atrás da tocha (que anda) e dando a volta, na velocidade de caça; esperando em volta da fogueira, devagar
                Ir(ponto, fogo.dono != null || !espera ? VelocidadeDePerseguir : a.velocidadeInvestigando * FatorAndando, 0.3f);
                Vector3 falta = ponto - transform.position;
                falta.y = 0f;
                if (espera && falta.sqrMagnitude < 0.7f * 0.7f) OlharPara(alvo);
            }
            else
            {
                if (NoNavMesh) agente.ResetPath();
                OlharPara(alvo);
            }
            return true;
        }

        // na beira, caçando: sem atacar, e ele vê quem caça (o fogo ilumina): quando o fogo apagar, ataca na hora (sem isso,
        // depois de tempoSemVer, ia investigar)
        void MarcarNaBeira(float agora, Vector3 alvo)
        {
            Atual = Estado.Perseguindo;
            _golpeEm = 0f;
            _noAlcanceDesde = -1f;
            _naBeiraAte = agora + 3f;
            _ultimoVisto = agora;
            _ultimaPosicao = alvo;
        }

        /// <summary>
        /// Um ponto do NavMesh na beira do fogo, no rumo (radianos, do centro), fora dele, de outro fogo, da zona segura e do
        /// sol (<paramref name="sol"/> marca que um ponto foi recusado por estar no sol).
        /// </summary>
        bool PontoNaBeira(in ZombieManager.Fogo fogo, float rumo, out Vector3 ponto, ref bool sol)
        {
            ponto = default;
            Vector3 alvo = fogo.centro + new Vector3(Mathf.Sin(rumo), 0f, Mathf.Cos(rumo)) * (fogo.raio + FolgaNaBeira);
            if (!NavMesh.SamplePosition(alvo, out var hit, 1.5f, NavMesh.AllAreas)) return false;
            ponto = hit.position;
            if (Mathf.Abs(ponto.y - transform.position.y) > 2f) return false;
            if (ZombieManager.DentroDoFogo(fogo, ponto, -0.1f)) return false;   // a amostra puxou para dentro (uma parede)
            if (_m.NoOutroFogo(ponto, fogo.chave) || ZombieManager.NaZonaSegura(ponto)) return false;
            // no sol do dia, não (ao amanhecer, o sol que ainda vai bater não impede: ele recua e depois se abriga)
            if (_m.NoSol(ponto))
            {
                sol = true;
                return false;
            }
            return true;
        }

        // o caminho reto (no plano) corta o fogo
        static bool CortaOFogo(Vector3 de, Vector3 para, in ZombieManager.Fogo fogo)
        {
            Vector2 a = new Vector2(de.x, de.z), b = new Vector2(para.x, para.z), c = new Vector2(fogo.centro.x, fogo.centro.z);
            Vector2 ab = b - a;
            float t = ab.sqrMagnitude > 0.0001f ? Mathf.Clamp01(Vector2.Dot(c - a, ab) / ab.sqrMagnitude) : 0f;
            return (a + ab * t - c).sqrMagnitude < (fogo.raio - 0.3f) * (fogo.raio - 0.3f);
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
            // de dia, fugindo da lanterna na sombra, ele não pula para o sol (fica, se os dois lados forem sol)
            if (_m.ProtegerDoSol && _m.Exposto(ponto)) return false;
            return d.sqrMagnitude >= 1f;
        }

        // o pulo acabou: quem perseguia volta a perseguir (pelo lado); quem ia para o abrigo, vai de novo; os outros ficam
        // ali olhando para a luz
        void FimDaEsquiva(float agora)
        {
            var antes = _antesDaEsquiva;
            if ((antes == Estado.Perseguindo || antes == Estado.Atacando) && _alvo != null && _alvo.AlvoValido)
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
