using System.Collections.Generic;
using UnityEngine;

namespace ZombieCityCleaner.Mundo
{
    // A mobília nova nas casas e nos apartamentos (Plano_Mobilia.md da pasta de arte, a etapa 4, 29/10/2026): as receitas
    // com os modelos novos (o tipo do ponto é o nome do modelo; o tamanho é o do conjunto dele em ×1,30, sem esticar,
    // MobiliaNova.Pe) e as regras novas, além da parede e da fileira:
    //  - a bancada da cozinha (Bancada): os módulos juntos numa parede, a pia embaixo da janela (a da cozinha é mais alta:
    //    Medidas.peitorilDaCozinha), o fogão perto dela, a geladeira na ponta e os armários de parede em cima;
    //  - no meio do cômodo (Meio: a mesa com as cadeiras, a ilha, a sinuca), na frente de outro (FrenteDe: a mesa de centro
    //    na frente do sofá, o baú no pé da cama), ao lado (AoLado: os criados-mudos da cama);
    //  - pendurado na parede (Alto: os quadros, o espelho em cima da pia, o toalheiro), sem tapar porta, janela nem móvel
    //    alto, e o tapete no chão (Tapete: embaixo da mesa de centro, na frente do sofá ou no pé da cama); os dois não
    //    mexem na passagem.
    // Os móveis novos podem ficar um pouco mais altos que o peitoril na frente de uma janela (o encosto do sofá passando
    // dele): em ×1,30 eles são maiores que as janelas de 0,9 m de peitoril.
    public static partial class MoveisDaObra
    {
        sealed partial class Item
        {
            /// <summary>Os modelos (um sorteado a cada vez, cada um com o tamanho dele).</summary>
            public Item[] opcoes;
            /// <summary>A chance de o item entrar (as receitas antigas: 1).</summary>
            public float chance = 1f;
            /// <summary>Quanto ele pode passar do peitoril na frente de uma janela.</summary>
            public float tolerancia = 0.01f;
            /// <summary>Os modelos de que ele depende (FrenteDe, AoLado, Tapete) ou a cujo pé ele fica de frente (Parede: o rack para o sofá).</summary>
            public string[] de, oposto;
            /// <summary>FrenteDe: o vão até o anfitrião.</summary>
            public float folga;
            /// <summary>Parede: não põe se o cômodo já tem um destes (a bancada já pôs a pia, o fogão ou a geladeira).</summary>
            public string[] unico;
            /// <summary>Alto: a altura da base sobre o chão (no jogo) e o anfitrião preferido embaixo (o espelho em cima da pia).</summary>
            public float altura;
            public string[] acima;
            /// <summary>Parede: de lado, o comprido encostado (a cama de solteiro e o colchão num quarto estreito).</summary>
            public bool deLado;

            public Item Escolher(System.Random rnd) => opcoes == null ? this : opcoes.Length == 0 ? null : opcoes[rnd.Next(opcoes.Length)];
        }

        // um item novo: os modelos (os que não estão na tabela ficam de fora), a regra e a quantidade
        static Item N(Regra regra, int min, int max, params string[] modelos)
        {
            var ops = new List<Item>();
            foreach (var m in modelos)
            {
                if (!MobiliaNova.EhNovo(m)) continue;
                var pe = MobiliaNova.Pe(m);
                var t = CatalogoDaMobilia.DoModelo(m);
                ops.Add(new Item
                {
                    tipo = m, w = pe.x, d = pe.z, h = pe.y, min = min, max = max, regra = regra, tolerancia = 0.3f,
                    altura = t != null && t.lugar == CatalogoDaMobilia.Lugar.Alto ? t.altura * MedidasDaMobilia.Escala : 0f,
                });
            }
            return new Item { tipo = modelos.Length > 0 ? modelos[0] : "", min = min, max = max, regra = regra, opcoes = ops.ToArray() };
        }

        static Item Com(this Item it, float chance = 1f, bool saque = false, string[] de = null, string[] oposto = null, float folga = 0f,
                        string[] unico = null, string[] acima = null, bool deLado = false)
        {
            it.chance = chance;
            foreach (var o in it.opcoes ?? new[] { it })
            {
                o.saque = saque; o.de = de; o.oposto = oposto; o.folga = folga; o.unico = unico; o.acima = acima; o.deLado = deLado;
            }
            return it;
        }

        static Item Senao(this Item it, Item outro)
        {
            var x = it;
            while (x.senao != null) x = x.senao;
            x.senao = outro;
            return it;
        }

        static readonly string[] Sofas = { "Sofa" }, Camas = { "Cama", "Beliche", "Colchao" }, Pias = { "Pia" };
        static readonly string[] Quadros = { "QuadroPaisagem", "QuadroRetrato", "QuadroBarco", "QuadroFlores" };
        static readonly string[] Tapetes = { "TapetePersa", "TapeteListras", "TapeteRedondo", "TapeteFelpudo" };

        // ------------------------------------------------------------ as receitas

        static readonly Item[] SalaNova =
        {
            N(Regra.Parede, 1, 1, "Sofa3", "Sofa3", "SofaL").Senao(N(Regra.Parede, 1, 1, "Sofa2")).Senao(N(Regra.Parede, 1, 1, "Sofa1")),
            N(Regra.Parede, 1, 1, "RackSimples", "RackArmario", "RackEstante").Com(saque: true, oposto: Sofas).Senao(N(Regra.Parede, 1, 1, "TVConsole")),
            N(Regra.FrenteDe, 1, 1, "MesaDeCentro").Com(chance: 0.75f, de: Sofas, folga: 0.3f),
            N(Regra.Tapete, 1, 1, Tapetes).Com(chance: 0.6f, de: new[] { "MesaDeCentro", "Sofa" }),
            N(Regra.Parede, 0, 2, "Sofa1"),
            N(Regra.Parede, 0, 1, "EstanteLivros", "EstanteBaixa").Com(saque: true),
            N(Regra.Parede, 0, 1, "MesaDeCanto").Com(saque: true),
            N(Regra.Parede, 0, 1, "AbajurDePe", "VasoPlanta", "VasoTerra"),
            N(Regra.Alto, 0, 2, Quadros),
        };

        static readonly Item[] QuartoNovo =
        {
            // (num quarto estreito, a de solteiro de lado, e no fim o colchão)
            N(Regra.Parede, 1, 1, "CamaCasal", "CamaCasal", "CamaSolteiro", "Beliche")
                .Senao(N(Regra.Parede, 1, 1, "CamaSolteiro")).Senao(N(Regra.Parede, 1, 1, "CamaSolteiro").Com(deLado: true))
                .Senao(N(Regra.Parede, 1, 1, "ColchaoNoChao")).Senao(N(Regra.Parede, 1, 1, "ColchaoNoChao").Com(deLado: true)),
            N(Regra.AoLado, 0, 2, "CriadoMudo").Com(saque: true, de: Camas),
            N(Regra.Parede, 1, 1, "GuardaRoupa4", "GuardaRoupa3").Com(saque: true).Senao(N(Regra.Parede, 1, 1, "GuardaRoupa2").Com(saque: true)),
            N(Regra.Parede, 0, 1, "Comoda6", "Comoda3", "Penteadeira").Com(saque: true),
            N(Regra.FrenteDe, 1, 1, "Bau").Com(chance: 0.45f, saque: true, de: Camas, folga: 0.06f),
            N(Regra.Parede, 1, 1, "MesaEscritorio").Com(chance: 0.25f, saque: true),
            N(Regra.Parede, 1, 1, "Berco").Com(chance: 0.15f),
            N(Regra.Tapete, 1, 1, Tapetes).Com(chance: 0.4f, de: Camas),
            N(Regra.Alto, 0, 1, Quadros),
        };

        static readonly Item[] CozinhaNova =
        {
            new Item { tipo = "bancada", min = 1, max = 1, regra = Regra.Bancada, saque = true },
            // sem lugar para a bancada (as cozinhas pequenas dos apartamentos): cada um sozinho
            N(Regra.Parede, 1, 1, "Fogao").Com(unico: new[] { "Fogao" }),
            N(Regra.Parede, 1, 1, "Pia").Com(saque: true, unico: new[] { "Pia" }),
            N(Regra.Parede, 1, 1, "GeladeiraGrande", "Geladeira").Com(saque: true, unico: new[] { "Geladeira" }).Senao(N(Regra.Parede, 1, 1, "Geladeira").Com(saque: true)),
            N(Regra.Meio, 1, 1, "Mesa4", "MesaRedonda", "Mesa6").Com(chance: 0.75f).Senao(N(Regra.Meio, 1, 1, "MesaRedonda")),
            N(Regra.Meio, 1, 1, "Ilha").Com(chance: 0.25f, saque: true),
        };

        static readonly Item[] BanheiroNovo =
        {
            N(Regra.Parede, 1, 1, "Privada"),
            N(Regra.Parede, 1, 1, "PiaGabinete", "PiaColuna", "PiaDupla").Com(saque: true).Senao(N(Regra.Parede, 1, 1, "PiaColuna")),
            N(Regra.Parede, 1, 1, "Box", "Box", "Banheira").Com(chance: 0.85f).Senao(N(Regra.Parede, 1, 1, "Box")),
            N(Regra.Alto, 1, 1, "EspelhoArmario").Com(chance: 0.8f, saque: true, acima: Pias),
            N(Regra.Alto, 1, 1, "Toalheiro").Com(chance: 0.7f),
        };

        static readonly Item[] PoraoNovo =
        {
            N(Regra.Parede, 1, 3, "EstanteAco120", "EstanteAco90", "PrateleiraMadeira150", "PrateleiraMadeira90").Com(saque: true),
            N(Regra.Parede, 0, 1, "Bancada").Com(saque: true),
            N(Regra.Parede, 1, 1, "FreezerHorizontal").Com(chance: 0.5f, saque: true),
            N(Regra.Meio, 1, 1, "MesaSinuca").Com(chance: 0.25f),
            // o acampamento de quem se escondeu lá
            N(Regra.Parede, 1, 2, "Colchonete", "SacoDeDormir").Com(chance: 0.25f),
            N(Regra.Parede, 1, 2, "Fogareiro", "Latas", "Garrafas").Com(chance: 0.25f),
        };

        static readonly Item[] DepositoNovo =
        {
            N(Regra.Parede, 1, 4, "EstanteAco120", "EstanteAco90", "PrateleiraMadeira150", "PrateleiraMadeira90").Com(saque: true),
        };

        // as casas e os apartamentos ("casa") e os cômodos de todos do prédio ("predio"): a mobília nova onde há modelo; a
        // lavanderia, as lojas e o hospital ficam com a antiga
        static Item[] ReceitaNova(string contexto, string tipo)
        {
            if (contexto != "casa" && contexto != "predio") return null;
            switch (tipo)
            {
                case "sala": return SalaNova;
                case "quarto": return QuartoNovo;
                case "cozinha": return CozinhaNova;
                case "banheiro": return BanheiroNovo;
                case "porao": return PoraoNovo;
                case "deposito": return DepositoNovo;
                default: return null;
            }
        }

        // ------------------------------------------------------------ ajudantes

        // os pendurados do cômodo (não mexem na passagem): o retângulo na planta e as alturas
        [System.ThreadStatic] static List<(Rect pe, float baixo, float alto)> _altosDoComodo;
        static List<(Rect pe, float baixo, float alto)> _altos => _altosDoComodo ??= new List<(Rect, float, float)>();

        static bool Comeca(string tipo, string[] prefixos)
        {
            if (prefixos == null || tipo == null) return false;
            foreach (var p in prefixos) if (tipo.StartsWith(p)) return true;
            return false;
        }

        static bool Tem(List<PontoDeMovel> saida, int inicio, string[] prefixos) => Achar(saida, inicio, prefixos, out _);

        // o primeiro ponto do cômodo que começa com um dos nomes
        static bool Achar(List<PontoDeMovel> saida, int inicio, string[] prefixos, out PontoDeMovel achado)
        {
            for (int i = inicio; i < saida.Count; i++)
                if (Comeca(saida[i].tipo, prefixos)) { achado = saida[i]; return true; }
            achado = default;
            return false;
        }

        // a parede em que o móvel encosta (o lado do cômodo: 0 = z0, 1 = x1, 2 = z1, 3 = x0), pelo giro (Giros)
        static int LadoDoGiro(float giro)
        {
            int g = ((Mathf.RoundToInt(giro) % 360) + 360) % 360;
            return g == 270 ? 1 : g == 180 ? 2 : g == 90 ? 3 : 0;
        }

        static bool Dentro(Rect r, Rect pe) => pe.xMin >= r.xMin - 0.001f && pe.xMax <= r.xMax + 0.001f && pe.yMin >= r.yMin - 0.001f && pe.yMax <= r.yMax + 0.001f;

        // ao longo do lado (o x nos lados 0 e 2, o z nos outros)
        static float Ao(Rect a, int lado, bool fim) => lado == 0 || lado == 2 ? (fim ? a.xMax : a.xMin) : (fim ? a.yMax : a.yMin);

        // ------------------------------------------------------------ na frente, ao lado, no meio

        // o anfitrião de costas na parede do giro dele (a cama de lado, encostada pelo comprido, não)
        static bool DeCostas(Rect r, PontoDeMovel h)
        {
            var hp = h.Pe;
            switch (LadoDoGiro(h.giro))
            {
                case 0: return hp.yMin < r.yMin + 0.02f;
                case 1: return hp.xMax > r.xMax - 0.02f;
                case 2: return hp.yMax > r.yMax - 0.02f;
                default: return hp.xMin < r.xMin + 0.02f;
            }
        }

        // na frente do anfitrião (a mesa de centro na frente do sofá, o baú no pé da cama), com o mesmo giro
        static bool NaFrenteDe(Item it, Rect r, Comodo k, float chao, List<PontoDeMovel> saida, int inicio)
        {
            if (!Achar(saida, inicio, it.de, out var h) || !DeCostas(r, h)) return false;
            int lado = LadoDoGiro(h.giro);
            var hp = h.Pe;
            float w = it.w, d = it.d, f = it.folga;
            Rect pe, frente;
            switch (lado)
            {
                case 0:
                    pe = Rect.MinMaxRect(hp.center.x - w / 2, hp.yMax + f, hp.center.x + w / 2, hp.yMax + f + d);
                    frente = Rect.MinMaxRect(pe.center.x - 0.2f, pe.yMax + 0.05f, pe.center.x + 0.2f, pe.yMax + 0.45f);
                    break;
                case 2:
                    pe = Rect.MinMaxRect(hp.center.x - w / 2, hp.yMin - f - d, hp.center.x + w / 2, hp.yMin - f);
                    frente = Rect.MinMaxRect(pe.center.x - 0.2f, pe.yMin - 0.45f, pe.center.x + 0.2f, pe.yMin - 0.05f);
                    break;
                case 1:
                    pe = Rect.MinMaxRect(hp.xMin - f - d, hp.center.y - w / 2, hp.xMin - f, hp.center.y + w / 2);
                    frente = Rect.MinMaxRect(pe.xMin - 0.45f, pe.center.y - 0.2f, pe.xMin - 0.05f, pe.center.y + 0.2f);
                    break;
                default:
                    pe = Rect.MinMaxRect(hp.xMax + f, hp.center.y - w / 2, hp.xMax + f + d, hp.center.y + w / 2);
                    frente = Rect.MinMaxRect(pe.xMax + 0.05f, pe.center.y - 0.2f, pe.xMax + 0.45f, pe.center.y + 0.2f);
                    break;
            }
            if (!Dentro(r, pe) || !Livre(pe) || TapaJanela(it, pe, r)) return false;
            // a frente do anfitrião fica tapada (chega-se ao sofá pelos lados da mesa): as que caem neste saem, e a deste fica
            _tapadas ??= new List<(int, Rect)>();
            _tapadas.Clear();
            for (int i = _frentes.Count - 1; i >= 0; i--)
                if (_frentes[i].Overlaps(pe))
                {
                    _tapadas.Add((i, _frentes[i]));
                    _frentes.RemoveAt(i);
                }
            if (!Colocar(pe, frente))
            {
                for (int i = _tapadas.Count - 1; i >= 0; i--) _frentes.Insert(_tapadas[i].Item1, _tapadas[i].Item2);
                return false;
            }
            Guardar(it, pe, h.giro, k, chao, saida);
            return true;
        }

        [System.ThreadStatic] static List<(int, Rect)> _tapadas;

        // ao lado do anfitrião, na mesma parede (os criados-mudos da cama)
        static bool AoLadoDe(Item it, Rect r, Comodo k, float chao, System.Random rnd, List<PontoDeMovel> saida, int inicio)
        {
            if (!Achar(saida, inicio, it.de, out var h) || !DeCostas(r, h)) return false;
            int lado = LadoDoGiro(h.giro);
            var hp = h.Pe;
            float lo = lado == 0 || lado == 2 ? r.xMin : r.yMin, hi = lado == 0 || lado == 2 ? r.xMax : r.yMax;
            float a = Ao(hp, lado, false), b = Ao(hp, lado, true);
            bool primeiro = rnd.Next(2) == 0;
            for (int vez = 0; vez < 2; vez++)
            {
                float ini = (vez == 0) == primeiro ? a - 0.06f - it.w : b + 0.06f;
                if (ini < lo || ini + it.w > hi) continue;
                var pe = Faixa(r, lado, ini, ini + it.w, 0f, it.d);
                if (!Livre(pe) || TapaJanela(it, pe, r)) continue;
                var frente = Faixa(r, lado, ini + it.w / 2 - 0.2f, ini + it.w / 2 + 0.2f, it.d + 0.05f, it.d + 0.45f);
                if (!Colocar(pe, frente)) continue;
                Guardar(it, pe, Giros[lado], k, chao, saida);
                return true;
            }
            return false;
        }

        [System.ThreadStatic] static List<Vector2> _lugaresDoMeio;

        // solto no meio do cômodo (a mesa com as cadeiras, a ilha, a sinuca): o comprido dele ao longo do comprido do
        // cômodo, do meio para fora
        static bool NoMeio(Item it, Rect r, Comodo k, float chao, System.Random rnd, List<PontoDeMovel> saida)
        {
            bool deitar = (r.width >= r.height) != (it.w >= it.d);
            float lx = deitar ? it.d : it.w, lz = deitar ? it.w : it.d;
            if (lx > r.width || lz > r.height) return false;
            var lugares = _lugaresDoMeio ??= new List<Vector2>();
            lugares.Clear();
            float mx = (r.width - lx) / 2, mz = (r.height - lz) / 2;
            for (float dx = 0f; dx <= mx + 0.001f; dx += 0.3f)
                for (float dz = 0f; dz <= mz + 0.001f; dz += 0.3f)
                {
                    lugares.Add(new Vector2(dx, dz));
                    if (dx > 0f) lugares.Add(new Vector2(-dx, dz));
                    if (dz > 0f) lugares.Add(new Vector2(dx, -dz));
                    if (dx > 0f && dz > 0f) lugares.Add(new Vector2(-dx, -dz));
                }
            // do meio para fora (os de mesma distância, sorteados)
            for (int i = lugares.Count - 1; i > 0; i--)
            {
                int j = rnd.Next(i + 1);
                (lugares[i], lugares[j]) = (lugares[j], lugares[i]);
            }
            lugares.Sort((p, q) => Mathf.RoundToInt(p.sqrMagnitude * 100f).CompareTo(Mathf.RoundToInt(q.sqrMagnitude * 100f)));
            int tentativas = 0;
            foreach (var o in lugares)
            {
                float cx = r.center.x + o.x, cz = r.center.y + o.y;
                var pe = Rect.MinMaxRect(cx - lx / 2, cz - lz / 2, cx + lx / 2, cz + lz / 2);
                if (!Dentro(r, pe) || !Livre(pe) || TapaJanela(it, pe, r)) continue;
                // virado para o lado com mais espaço (a frente dele é para onde ele olha)
                float giro;
                Rect frente;
                if (!deitar)
                {
                    bool mais = r.yMax - pe.yMax >= pe.yMin - r.yMin;
                    giro = mais ? 0f : 180f;
                    frente = mais ? Rect.MinMaxRect(cx - 0.2f, pe.yMax + 0.05f, cx + 0.2f, pe.yMax + 0.45f)
                                  : Rect.MinMaxRect(cx - 0.2f, pe.yMin - 0.45f, cx + 0.2f, pe.yMin - 0.05f);
                }
                else
                {
                    bool mais = r.xMax - pe.xMax >= pe.xMin - r.xMin;
                    giro = mais ? 90f : 270f;
                    frente = mais ? Rect.MinMaxRect(pe.xMax + 0.05f, cz - 0.2f, pe.xMax + 0.45f, cz + 0.2f)
                                  : Rect.MinMaxRect(pe.xMin - 0.45f, cz - 0.2f, pe.xMin - 0.05f, cz + 0.2f);
                }
                if (Colocar(pe, frente)) { Guardar(it, pe, giro, k, chao, saida); return true; }
                if (++tentativas >= TentativasDoPrecisa) return false;
            }
            return false;
        }

        // ------------------------------------------------------------ pendurado e no chão

        // pendurado na parede (os quadros, o espelho, o toalheiro), na altura dele: sem tapar porta nem janela, nem ficar em
        // cima de um móvel mais alto que a base dele ou de outro pendurado; o espelho, em cima da pia, se der
        static bool NaParedeAlto(Item it, Rect r, Comodo k, float chao, System.Random rnd, List<PontoDeMovel> saida, int inicio)
        {
            float y0 = it.altura, y1 = it.altura + it.h;
            bool Cabe(int lado, float ini, out Rect pe)
            {
                pe = default;
                float lo = lado == 0 || lado == 2 ? r.xMin : r.yMin, hi = lado == 0 || lado == 2 ? r.xMax : r.yMax;
                if (ini < lo + 0.1f || ini + it.w > hi - 0.1f) return false;
                foreach (var f in _furos[lado])
                    if (ini < f.b + 0.15f && ini + it.w > f.a - 0.15f) return false;
                pe = Faixa(r, lado, ini, ini + it.w, 0f, it.d);
                var naFrente = Faixa(r, lado, ini, ini + it.w, 0f, 0.5f);
                for (int i = inicio; i < saida.Count; i++)
                {
                    var q = saida[i];
                    float baixo = q.altura, alto = baixo + q.tamanho.y;
                    if (q.Pe.Overlaps(naFrente) && baixo < y1 && alto > y0 - 0.05f) return false;
                }
                foreach (var (ap, ab, aa) in _altos)
                    if (ap.Overlaps(naFrente) && ab < y1 && aa > y0 - 0.05f) return false;
                return true;
            }
            void Pendurar(int lado, Rect pe)
            {
                Guardar(it, pe, Giros[lado], k, chao, saida, it.altura);
                _altos.Add((pe, y0, y1));
            }
            if (it.acima != null)
                for (int i = inicio; i < saida.Count; i++)
                {
                    if (!Comeca(saida[i].tipo, it.acima)) continue;
                    int lado = LadoDoGiro(saida[i].giro);
                    float meio = (Ao(saida[i].Pe, lado, false) + Ao(saida[i].Pe, lado, true)) / 2;
                    if (Cabe(lado, meio - it.w / 2, out var pe)) { Pendurar(lado, pe); return true; }
                }
            _ordem.Clear();
            for (int lado = 0; lado < 4; lado++)
            {
                float comprimento = lado == 0 || lado == 2 ? r.width : r.height;
                int n = Mathf.FloorToInt((comprimento - it.w) / Passo);
                for (int i = 0; i <= n; i++) _ordem.Add(lado * 1000 + i);
            }
            for (int i = _ordem.Count - 1; i > 0; i--)
            {
                int j = rnd.Next(i + 1);
                (_ordem[i], _ordem[j]) = (_ordem[j], _ordem[i]);
            }
            foreach (int codigo in _ordem)
            {
                int lado = codigo / 1000;
                float ini = (lado == 0 || lado == 2 ? r.xMin : r.yMin) + (codigo % 1000) * Passo;
                if (Cabe(lado, ini, out var pe)) { Pendurar(lado, pe); return true; }
            }
            return false;
        }

        // o tapete no chão, com o giro do anfitrião: embaixo da mesa de centro, na frente do sofá ou no pé da cama (passando
        // embaixo dela); não mexe na passagem, só não sai do cômodo nem fica em cima de outro tapete
        static bool NoChao(Item it, Rect r, Comodo k, float chao, List<PontoDeMovel> saida, int inicio)
        {
            foreach (var nome in it.de)
            {
                if (!Achar(saida, inicio, new[] { nome }, out var h)) continue;
                var frente = Quaternion.Euler(0f, h.giro, 0f) * Vector3.forward;
                var c = new Vector2(h.centro.x, h.centro.z);
                if (Comeca(h.tipo, Sofas)) c += new Vector2(frente.x, frente.z) * (h.tamanho.z / 2 + 0.95f);
                else if (Comeca(h.tipo, Camas)) c += new Vector2(frente.x, frente.z) * (h.tamanho.z * 0.3f);
                bool deLado = Mathf.Abs(Mathf.Sin(h.giro * Mathf.Deg2Rad)) > 0.5f;
                float lx = deLado ? it.d : it.w, lz = deLado ? it.w : it.d;
                var pe = Rect.MinMaxRect(c.x - lx / 2, c.y - lz / 2, c.x + lx / 2, c.y + lz / 2);
                var dentro = Rect.MinMaxRect(r.xMin + 0.05f, r.yMin + 0.05f, r.xMax - 0.05f, r.yMax - 0.05f);
                if (!Dentro(dentro, pe) || Tem(saida, inicio, Tapetes) && Sobre(saida, inicio, pe)) continue;
                Guardar(it, pe, h.giro, k, chao, saida);
                return true;
            }
            return false;
        }

        static bool Sobre(List<PontoDeMovel> saida, int inicio, Rect pe)
        {
            for (int i = inicio; i < saida.Count; i++)
                if (Comeca(saida[i].tipo, Tapetes) && saida[i].Pe.Overlaps(pe)) return true;
            return false;
        }

        // ------------------------------------------------------------ a bancada da cozinha

        static readonly (string modelo, string deParede, string aberto)[] Modulos =
        {
            ("ArmarioChao80", "ArmarioParede80", "ArmarioParedeAberto80"), ("ArmarioChao60", "ArmarioParede60", "ArmarioParedeAberto60"),
            ("ArmarioChao40", "ArmarioParede40", null), ("Gaveteiro40", "ArmarioParede40", null),
        };

        // um trecho livre ao longo de um lado (de a até b) e o meio da janela da pia nele (NaN sem janela)
        struct Trecho { public int lado; public float a, b, janela, nota; }

        [System.ThreadStatic] static List<Trecho> _trechos;
        [System.ThreadStatic] static List<Vector2> _livres, _livresNovos;
        [System.ThreadStatic] static List<(string modelo, float s)> _plano;
        [System.ThreadStatic] static List<Rect> _frentesDaBancada;

        static bool Bancada(Item it, Rect r, Comodo k, float chao, System.Random rnd, List<PontoDeMovel> saida, int inicio)
        {
            if (!MobiliaNova.EhNovo("Pia") || !MobiliaNova.EhNovo("Fogao") || !MobiliaNova.EhNovo("ArmarioChao40")) return false;
            float wPia = MobiliaNova.Pe("Pia").x, wFogao = MobiliaNova.Pe("Fogao").x, w40 = MobiliaNova.Pe("ArmarioChao40").x;
            float tampo = MobiliaNova.Pe("ArmarioChao60").y;
            float fundo = Mathf.Max(MobiliaNova.Pe("ArmarioChao60").z, Mathf.Max(MobiliaNova.Pe("Pia").z, MobiliaNova.Pe("Fogao").z));
            string geladeira = rnd.Next(2) == 0 && MobiliaNova.EhNovo("GeladeiraGrande") ? "GeladeiraGrande" : "Geladeira";
            var gel = MobiliaNova.Pe(geladeira);
            float fundoTudo = Mathf.Max(fundo, gel.z);
            _trechos ??= new List<Trecho>();
            _livres ??= new List<Vector2>();
            _livresNovos ??= new List<Vector2>();
            _plano ??= new List<(string, float)>();
            _trechos.Clear();
            for (int lado = 0; lado < 4; lado++)
            {
                Livres(r, lado, fundoTudo, tampo);
                foreach (var t in _livres)
                {
                    if (t.y - t.x < Mathf.Min(wPia, gel.x) + wFogao) continue;
                    float janela = float.NaN;
                    foreach (var f in _furos[lado])
                        if (!f.porta && f.peitoril >= tampo + 0.01f && (f.a + f.b) / 2 > t.x + wPia / 2 && (f.a + f.b) / 2 < t.y - wPia / 2) janela = (f.a + f.b) / 2;
                    _trechos.Add(new Trecho { lado = lado, a = t.x, b = t.y, janela = janela, nota = (float.IsNaN(janela) ? 0f : 10f) + (t.y - t.x) + (float)rnd.NextDouble() });
                }
            }
            _trechos.Sort((p, q) => q.nota.CompareTo(p.nota));
            for (int c = 0; c < _trechos.Count && c < 4; c++)
            {
                var t = _trechos[c];
                bool comGeladeira = true;
                for (int corte = 0; corte < 8; corte++)
                {
                    if (!Planejar(t, corte * w40, wPia, wFogao, w40, comGeladeira ? geladeira : null, gel.x, rnd)) break;
                    // a geladeira (alta) não fica na frente de uma janela: a bancada sai sem ela (ela vai sozinha depois)
                    if (comGeladeira && TapaAGeladeira(r, t.lado, geladeira, gel))
                    {
                        comGeladeira = false;
                        corte--;
                        continue;
                    }
                    float s0 = _plano[0].s, s1 = _plano[_plano.Count - 1].s + MobiliaNova.Pe(_plano[_plano.Count - 1].modelo).x;
                    var pe = Faixa(r, t.lado, s0, s1, 0f, fundoTudo);
                    // a frente de cada um (a bancada inteira se alcança)
                    _frentesDaBancada ??= new List<Rect>();
                    _frentesDaBancada.Clear();
                    foreach (var (modelo, s) in _plano)
                    {
                        float meio = s + MobiliaNova.Pe(modelo).x / 2;
                        _frentesDaBancada.Add(Faixa(r, t.lado, meio - 0.2f, meio + 0.2f, fundoTudo + 0.05f, fundoTudo + 0.45f));
                    }
                    if (!Colocar(pe, _frentesDaBancada)) continue;
                    // os módulos, a geladeira e os armários de parede em cima (não em cima da geladeira, do fogão nem da janela)
                    foreach (var (modelo, s) in _plano)
                    {
                        var p = MobiliaNova.Pe(modelo);
                        var peca = new Item { tipo = modelo, w = p.x, d = p.z, h = p.y, saque = modelo != "Fogao" };
                        Guardar(peca, Faixa(r, t.lado, s, s + p.x, 0f, p.z), Giros[t.lado], k, chao, saida);
                        string deParede = null;
                        foreach (var (m, dp, ab) in Modulos)
                            if (m == modelo) deParede = ab != null && rnd.Next(10) < 3 ? ab : dp;
                        if (modelo == "Pia") deParede = rnd.Next(10) < 3 ? "ArmarioParedeAberto80" : "ArmarioParede80";
                        if (deParede == null || !MobiliaNova.EhNovo(deParede)) continue;
                        var ap = MobiliaNova.Pe(deParede);
                        float ca = s + (p.x - ap.x) / 2, cb = ca + ap.x;
                        bool naJanela = false;
                        foreach (var f in _furos[t.lado]) if (!f.porta && ca < f.b + 0.05f && cb > f.a - 0.05f) naJanela = true;
                        if (naJanela) continue;
                        var tipo = CatalogoDaMobilia.DoModelo(deParede);
                        var alto = new Item { tipo = deParede, w = ap.x, d = ap.z, h = ap.y, saque = !deParede.Contains("Aberto") };
                        float y = (tipo?.altura ?? 1.4f) * MedidasDaMobilia.Escala;
                        Guardar(alto, Faixa(r, t.lado, ca, cb, 0f, ap.z), Giros[t.lado], k, chao, saida, y);
                        _altos.Add((Faixa(r, t.lado, ca, cb, 0f, ap.z), y, y + ap.y));
                    }
                    return true;
                }
            }
            return false;
        }

        static bool TapaAGeladeira(Rect r, int lado, string geladeira, Vector3 gel)
        {
            foreach (var (modelo, s) in _plano)
                if (modelo == geladeira)
                    return TapaJanela(new Item { h = gel.y, tolerancia = 0.3f }, Faixa(r, lado, s, s + gel.x, 0f, gel.z), r);
            return false;
        }

        // os trechos livres de um lado (em _livres): o lado inteiro menos o que ocupa o chão, os móveis postos e as janelas
        // baixas (de qualquer lado) perto dele
        static void Livres(Rect r, int lado, float fundo, float tampo)
        {
            bool horizontal = lado == 0 || lado == 2;
            float lo = horizontal ? r.xMin : r.yMin, hi = horizontal ? r.xMax : r.yMax;
            var faixa = Faixa(r, lado, lo, hi, 0f, fundo + Vao);
            _livres.Clear();
            _livres.Add(new Vector2(lo, hi));
            void Tirar(Rect a)
            {
                if (!a.Overlaps(faixa)) return;
                float x0 = horizontal ? a.xMin : a.yMin, x1 = horizontal ? a.xMax : a.yMax;
                _livresNovos.Clear();
                foreach (var t in _livres)
                {
                    if (x1 <= t.x || x0 >= t.y) { _livresNovos.Add(t); continue; }
                    if (x0 > t.x) _livresNovos.Add(new Vector2(t.x, x0));
                    if (x1 < t.y) _livresNovos.Add(new Vector2(x1, t.y));
                }
                _livres.Clear();
                _livres.AddRange(_livresNovos);
            }
            foreach (var b in _bloqueios) Tirar(b);
            foreach (var c in _colocados) Tirar(Rect.MinMaxRect(c.xMin - Vao, c.yMin - Vao, c.xMax + Vao, c.yMax + Vao));
            for (int l = 0; l < 4; l++)
                foreach (var f in _furos[l])
                    if (!f.porta && f.peitoril < tampo + 0.01f) Tirar(Faixa(r, l, f.a - 0.1f, f.b + 0.1f, 0f, 0.35f));
        }

        // os módulos do trecho (em _plano, em ordem, cada um com o começo ao longo do lado): a geladeira numa ponta (se
        // couber), a pia embaixo da janela (ou sorteada), o fogão a um módulo dela (longe da janela) e os outros módulos
        // em volta; o que sobra (menos de um módulo) fica vazio na ponta. 'corte' encurta o trecho (do lado longe da pia).
        static bool Planejar(Trecho t, float corte, float wPia, float wFogao, float w40, string geladeira, float wGel, System.Random rnd)
        {
            _plano.Clear();
            float a = t.a, b = t.b;
            bool janela = !float.IsNaN(t.janela);
            // o corte: do lado mais longe da janela (sem janela, do fim)
            if (janela && t.janela - a > b - t.janela) a += corte; else b -= corte;
            // sem lugar para a pia junto (a cozinha pequena): o fogão e a geladeira (a pia vai sozinha, se couber)
            if (b - a < wPia + wFogao)
            {
                if (geladeira == null || b - a < wFogao + wGel) return false;
                bool noComeco = rnd.Next(2) == 0;
                float sg = noComeco ? a : b - wGel, sf0 = noComeco ? a + wGel : b - wGel - wFogao;
                _plano.Add((geladeira, sg));
                _plano.Add(("Fogao", sf0));
                if (noComeco) Encher(sf0 + wFogao, b, true, rnd);
                else Encher(a, sf0, false, rnd);
                _plano.Sort((p, q) => p.s.CompareTo(q.s));
                return true;
            }
            // a geladeira na ponta longe da janela (antes do módulo entre a pia e o fogão: a cozinha pequena fica sem ele)
            float ca = a, cb = b;
            if (geladeira != null && b - a >= wGel + wPia + wFogao)
            {
                bool noComeco = janela ? t.janela - a > b - t.janela : rnd.Next(2) == 0;
                if (noComeco) { _plano.Add((geladeira, a)); ca = a + wGel; }
                else { _plano.Add((geladeira, b - wGel)); cb = b - wGel; }
            }
            // a pia
            float sp;
            if (janela && t.janela - wPia / 2 >= ca && t.janela + wPia / 2 <= cb) sp = t.janela - wPia / 2;
            else
            {
                int vagas = Mathf.FloorToInt((cb - ca - wPia) / w40);
                sp = ca + rnd.Next(vagas + 1) * w40;
            }
            // o fogão: do lado com mais espaço, um módulo de 0,40 entre os dois se der
            float sf;
            float direita = cb - (sp + wPia), esquerda = sp - ca;
            if (direita >= esquerda)
                sf = direita >= wFogao + w40 ? sp + wPia + w40 : direita >= wFogao ? sp + wPia : float.NaN;
            else
                sf = esquerda >= wFogao + w40 ? sp - w40 - wFogao : esquerda >= wFogao ? sp - wFogao : float.NaN;
            if (float.IsNaN(sf)) return false;
            _plano.Add(("Pia", sp));
            _plano.Add(("Fogao", sf));
            float x0 = Mathf.Min(sp, sf), x1 = Mathf.Max(sp + wPia, sf + wFogao);
            // entre a pia e o fogão (o 0,40)
            if (x1 - x0 > wPia + wFogao + 0.01f) _plano.Add((rnd.Next(2) == 0 ? "Gaveteiro40" : "ArmarioChao40", Mathf.Min(sp + wPia, sf + wFogao)));
            // para fora, de dentro para a ponta: os módulos que couberem; o resto fica vazio na ponta
            Encher(x1, cb, true, rnd);
            Encher(ca, x0, false, rnd);
            _plano.Sort((p, q) => p.s.CompareTo(q.s));
            return true;
        }

        static void Encher(float de, float ate, bool paraFrente, System.Random rnd)
        {
            float livre = ate - de, pos = paraFrente ? de : ate;
            while (true)
            {
                string escolhido = null;
                float largura = 0f;
                // o maior que cabe, às vezes o do meio (a bancada não sai sempre igual)
                foreach (var (m, _, _) in Modulos)
                {
                    if (!MobiliaNova.EhNovo(m)) continue;
                    float w = MobiliaNova.Pe(m).x;
                    if (w > livre + 0.001f) continue;
                    if (escolhido == null || rnd.Next(3) == 0 && w < largura) { escolhido = m; largura = w; }
                    if (escolhido != null && rnd.Next(3) != 0) break;
                }
                if (escolhido == null) return;
                if (escolhido == "ArmarioChao40" && rnd.Next(3) == 0) escolhido = "Gaveteiro40";
                if (paraFrente) { _plano.Add((escolhido, pos)); pos += largura; }
                else { pos -= largura; _plano.Add((escolhido, pos)); }
                livre -= largura;
            }
        }
    }
}
